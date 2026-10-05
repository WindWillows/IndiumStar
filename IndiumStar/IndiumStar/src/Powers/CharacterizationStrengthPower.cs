using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace IndiumStar.Powers;

/// <summary>按本人回合预约表征的临时力量；新预约不会混入当前回合的发放。</summary>
/// <remarks>
/// 回合开始的自动出牌可能早于AfterSideTurnStart，所以不能把全部Amount直接发放。
/// 每次施加记录本人TurnNumber+1；开始钩子只消费到期快照，回合末只收回本次成功授予部分。
/// 同回合多张表征仍累计，开始或结束的连锁中新加的预约保留到后续本人回合。
/// </remarks>
[RegisterPower]
public sealed class CharacterizationStrengthPower : PowerModel
{
    private sealed record Appointment(int DueTurn, int Amount);
    private List<Appointment> _appointments = [];
    private ICombatState? _combat;
    private int _grantedAmount;
    private int? _grantedTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature? applier, CardModel? source)
    {
        // BeforePowerAmountChanged仍是请求值，可能被本体given/received修正链拒绝或改变。
        // 使用最终生效的正增量，不让失败的再次施加产生虚假预约。
        if (power == this) Schedule(Owner, amount);
        return Task.CompletedTask;
    }

    private void Schedule(Creature target, decimal amount)
    {
        if (amount <= 0 || target.Player?.PlayerCombatState is not { } state || target.CombatState is not { } combat)
            return;
        if (_combat != combat)
        {
            _appointments.Clear();
            _grantedAmount = 0;
            _grantedTurn = null;
            _combat = combat;
        }
        _appointments.Add(new(checked(state.TurnNumber + 1), (int)amount));
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _appointments = new(_appointments);
    }

    public override async Task AfterSideTurnStart(CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.CombatState != _combat
            || combatState != _combat || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding
            || Owner.Player?.PlayerCombatState is not { } state || _grantedAmount > 0)
            return;
        int amount = _appointments.Where(a => a.DueTurn <= state.TurnNumber).Sum(a => a.Amount);
        if (amount <= 0) return;
        // 先消费到期快照和记录发放回合，重复/嵌套开始不能再次领取。
        _appointments.RemoveAll(a => a.DueTurn <= state.TurnNumber);
        _grantedAmount = amount;
        _grantedTurn = state.TurnNumber;
        StrengthPower? granted = await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),
            Owner, amount, Owner, null, silent: true);
        if (granted == null)
        {
            // 拒绝施加没有获得力量，不能在结束时倒扣未获得的数值。
            _grantedAmount = 0;
            _grantedTurn = null;
            SetAmount(_appointments.Sum(a => a.Amount));
            if (_appointments.Count == 0 && Owner.GetPower<CharacterizationStrengthPower>() == this)
                await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.CombatState != _combat
            || Owner.Player?.PlayerCombatState?.TurnNumber != _grantedTurn || _grantedAmount <= 0)
            return;
        int granted = _grantedAmount;
        _grantedAmount = 0;
        _grantedTurn = null;
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -granted, Owner, null);
        SetAmount(_appointments.Sum(a => a.Amount));
        if (_appointments.Count == 0 && Owner.GetPower<CharacterizationStrengthPower>() == this)
            await PowerCmd.Remove(this);
    }
}
