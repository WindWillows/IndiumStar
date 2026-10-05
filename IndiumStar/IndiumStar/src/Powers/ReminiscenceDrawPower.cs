using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>本回合从钒永生效后统计实际抽牌，预约本人下一回合的额外抽牌。</summary>
/// <remarks>
/// 保留既有的同回合多份共用统计器口径；多份是否独立累加另列设计问题。
/// 待兑现数量与本回合的新统计分开，BeforeHandDraw自动打出的新钒永不会被旧预约清理。
/// 不用AfterSideTurnStart兑现：多人开始回合遇到选牌暂停时，该钩子可能早于本人抽牌完成。
/// 正数预约在真正的AfterModifyingHandDraw消费；零预约在本人的AfterPlayerTurnStart清理。
/// </remarks>
[RegisterPower]
public sealed class ReminiscenceDrawPower : ModPowerTemplate
{
    private bool _counting;
    private int _countingTurn;
    private int _drawnThisTurn;
    private int _carriedDraws;
    private int _settlementTurn;
    private bool _pendingSettlement;
    private ICombatState? _combatState;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>必须在自身抽牌之前配置；同一本人回合内重复施加不清空累计。</summary>
    public void Configure(CardPlay cardPlay)
    {
        if (Owner.Player is not { PlayerCombatState: { } state } player
            || cardPlay.Player != player || Owner.CombatState is not { } combat)
            throw new InvalidOperationException("Vanadium draw tracking requires its player's active combat play.");
        if (_combatState != combat)
        {
            _drawnThisTurn = 0;
            _carriedDraws = 0;
            _pendingSettlement = false;
            _counting = false;
            _combatState = combat;
        }
        if (!_counting || _countingTurn != state.TurnNumber) _drawnThisTurn = 0;
        _countingTurn = state.TurnNumber;
        _counting = true;
        // Apply的层数只是创建能力的哨兵；不能把施加次数显示为预约抽牌数。
        SetAmount(_pendingSettlement ? _carriedDraws : _drawnThisTurn, silent: true);
    }

    public override Task AfterCardDrawn(PlayerChoiceContext context,
        MegaCrit.Sts2.Core.Models.CardModel card, bool fromHandDraw)
    {
        if (_counting && card.Owner.Creature == Owner && card.CombatState == _combatState
            && Owner.CombatState == _combatState && Owner.Player?.PlayerCombatState?.TurnNumber == _countingTurn)
        {
            _drawnThisTurn++;
            // End钩子后的伤害/抽牌连锁仍属于当前回合，直到下一次SideTurnStart才封存。
            if (_pendingSettlement && _settlementTurn == _countingTurn + 1)
            {
                _carriedDraws = _drawnThisTurn;
                SetAmount(_carriedDraws, silent: true);
            }
            else if (!_pendingSettlement)
            {
                SetAmount(_drawnThisTurn, silent: true);
            }
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (!_counting || Owner.CombatState != _combatState || combat != _combatState) return Task.CompletedTask;
        // 正常End已预约；也覆盖在End遍历中途才新加的钒永。此后敌方或他人的额外回合不计入。
        if ((_pendingSettlement && _countingTurn < _settlementTurn)
            || Owner.Player?.PlayerCombatState?.TurnNumber > _countingTurn || !participants.Contains(Owner))
        {
            QueueCountedTurn();
            _counting = false;
            _drawnThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext context,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (!_counting || side != Owner.Side || !participants.Contains(Owner)
            || Owner.CombatState != _combatState || Owner.Player?.PlayerCombatState?.TurnNumber != _countingTurn)
            return Task.CompletedTask;
        QueueCountedTurn();
        return Task.CompletedTask;
    }

    private void QueueCountedTurn()
    {
        _settlementTurn = checked(_countingTurn + 1);
        _carriedDraws = _drawnThisTurn;
        _pendingSettlement = true;
        SetAmount(_carriedDraws, silent: true);
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player.Creature == Owner && SettlementIsDue ? count + _carriedDraws : count;

    public override Task AfterModifyingHandDraw() =>
        SettlementIsDue && _carriedDraws > 0 ? SettlePreviousTurn() : Task.CompletedTask;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player.Creature == Owner && SettlementIsDue ? SettlePreviousTurn() : Task.CompletedTask;

    private bool SettlementIsDue => _pendingSettlement && Owner.CombatState == _combatState
        && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding
        && Owner.Player?.PlayerCombatState?.TurnNumber >= _settlementTurn;

    private async Task SettlePreviousTurn()
    {
        // 先消费旧预约。重入或重复回调不能消费下一份统计。
        _pendingSettlement = false;
        _carriedDraws = 0;
        if (_counting && _countingTurn == Owner.Player?.PlayerCombatState?.TurnNumber)
        {
            SetAmount(_drawnThisTurn, silent: true);
        }
        else if (Owner.GetPower<ReminiscenceDrawPower>() == this)
        {
            await PowerCmd.Remove(this);
        }
    }
}
