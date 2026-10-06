using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>Use the native extra-turn loop; count card series before their effects so nested plays obey the cap.</summary>
[RegisterPower]
public sealed class TunnelingPower : ModPowerTemplate
{
    private int[] _pendingLimits = [];
    private int _activeTurn = -1;
    private int _cardLimit;
    private int _cardsPlayed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("draw_cards_next_turn_power");

    [SavedProperty]
    public int[] PendingLimits
    {
        get => _pendingLimits;
        set { AssertMutable(); _pendingLimits = value.Select(n => Math.Max(0, n)).ToArray(); }
    }
    [SavedProperty]
    public int ActiveTurn { get => _activeTurn; set { AssertMutable(); _activeTurn = value; } }
    [SavedProperty]
    public int CardLimit { get => _cardLimit; set { AssertMutable(); _cardLimit = Math.Max(0, value); } }
    [SavedProperty]
    public int CardsPlayed { get => _cardsPlayed; set { AssertMutable(); _cardsPlayed = Math.Max(0, value); } }

    public void Enqueue(int limit)
    {
        AssertMutable();
        _pendingLimits = [.. _pendingLimits, Math.Max(0, limit)];
        SetAmount(_pendingLimits.Length + (ActiveTurn >= 0 ? 1 : 0));
    }

    private bool IsActive => Owner.IsAlive && Owner.Player?.PlayerCombatState?.TurnNumber == ActiveTurn
        && ActiveTurn >= 0;

    public override bool ShouldTakeExtraTurn(Player player) => player == Owner.Player
        && Owner.IsAlive && _pendingLimits.Length > 0;

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner.Player || _pendingLimits.Length == 0) return Task.CompletedTask;
        // CombatManager increments this player's TurnNumber before this hook.
        ActiveTurn = player.PlayerCombatState!.TurnNumber;
        CardLimit = _pendingLimits[0];
        CardsPlayed = 0;
        _pendingLimits = _pendingLimits[1..];
        SetAmount(_pendingLimits.Length + 1);
        return Task.CompletedTask;
    }

    public override bool ShouldPlay(MegaCrit.Sts2.Core.Models.CardModel card, AutoPlayType autoPlayType)
        => card.Owner.Creature != Owner || !IsActive || CardsPlayed < CardLimit;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // A replay is part of the same card series. Manual and automatic first plays both count.
        if (cardPlay.Card.Owner.Creature == Owner && IsActive && cardPlay.IsFirstInSeries)
            CardsPlayed = checked(CardsPlayed + 1);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || !IsActive) return;
        ActiveTurn = -1;
        CardsPlayed = 0;
        CardLimit = 0;
        if (_pendingLimits.Length == 0) await PowerCmd.Remove(this);
        else SetAmount(_pendingLimits.Length);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingLimits = (int[])_pendingLimits.Clone();
    }
}
