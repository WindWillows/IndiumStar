using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>Next own turn: hand cards cost +1 and use the native additional-play modifier.</summary>
[RegisterPower]
public sealed class CrystalMarrowPower : ModPowerTemplate
{
    private int[] _pendingTurns = [];
    private int _activeTurn = -1;
    // Only the current hand-origin play needs transient tracking; clear it when the series leaves Play.
    private HashSet<CardModel> _handPlays = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("echo_form_power");

    [SavedProperty]
    public int[] PendingTurns
    {
        get => _pendingTurns;
        set { AssertMutable(); _pendingTurns = value.Distinct().ToArray(); }
    }
    [SavedProperty]
    public int ActiveTurn { get => _activeTurn; set { AssertMutable(); _activeTurn = value; } }

    public void Schedule(int dueTurn)
    {
        AssertMutable();
        // "Have +1 cost and Replay 1" grants one status; repeated casts still stack draw and Extension.
        if (!_pendingTurns.Contains(dueTurn)) _pendingTurns = [.. _pendingTurns, dueTurn];
    }

    private bool IsActive => Owner.IsAlive && ActiveTurn >= 0
        && Owner.Player?.PlayerCombatState?.TurnNumber == ActiveTurn;

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.CombatState != combatState || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding || Owner.Player?.PlayerCombatState is not { } state)
            return Task.CompletedTask;
        if (_pendingTurns.Any(t => t <= state.TurnNumber))
        {
            ActiveTurn = state.TurnNumber;
            _pendingTurns = _pendingTurns.Where(t => t > state.TurnNumber).ToArray();
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool applies = IsActive && card.Owner.Creature == Owner && card.Pile?.Type == PileType.Hand
            && !card.EnergyCost.CostsX && originalCost >= 0 && !card.Keywords.Contains(CardKeyword.Unplayable);
        modifiedCost = applies ? originalCost + 1 : originalCost;
        return applies;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner.Creature != Owner) return Task.CompletedTask;
        if (IsActive && oldPileType == PileType.Hand && card.Pile?.Type == PileType.Play)
            _handPlays.Add(card);
        else if (card.Pile?.Type != PileType.Play)
            _handPlays.Remove(card);
        return Task.CompletedTask;
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
        => IsActive && card.Owner.Creature == Owner && _handPlays.Contains(card)
            ? checked(playCount + 1) : playCount;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;
        ActiveTurn = -1;
        _handPlays.Clear();
        if (_pendingTurns.Length == 0) await PowerCmd.Remove(this);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingTurns = (int[])_pendingTurns.Clone();
        _handPlays = new(_handPlays);
    }
}
