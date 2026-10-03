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

/// <summary>
/// 「表征」的晶抗：<strong>下回合</strong>获得临时力量。
/// </summary>
/// <remarks>
/// <para>
/// 拆成两步，各自挂在明确的钩子上：
/// </para>
/// <list type="number">
/// <item><description>
/// <b>获得效果：下回合获得力量</b> —— 在<strong>你的下个回合开始</strong>时
/// （<see cref="AfterSideTurnStart" />）发放力量。
/// </description></item>
/// <item><description>
/// <b>获得效果：下回合结束时失去力量</b> —— 在那个回合<strong>结束</strong>时
/// （<see cref="AfterSideTurnEnd" />）收回力量。
/// </description></item>
/// </list>
/// <para>
/// 之所以不用原版的 <see cref="TemporaryStrengthPower" />：它是「<strong>立刻</strong>加力量、
/// 本回合结束移除」（基类 <c>BeforeApplied</c> 加、<c>AfterSideTurnEnd</c> 减），
/// 而且它<strong>一创建就把力量发出去了</strong>，没法延后到下一回合。
/// 所以这里不继承它，改成自己控制发放时机——能力挂上时不发力量。
/// </para>
/// <para>
/// 时序（打出当回合记为第 N 回合）：
/// </para>
/// <list type="bullet">
/// <item><description>第 N 回合打出「表征」→ 能力挂上，<strong>不发力量</strong></description></item>
/// <item><description>第 N+1 回合开始 → 按预约发力量</description></item>
/// <item><description>第 N+1 回合结束 → 收回已发放的力量</description></item>
/// </list>
/// <para>
/// <b>已发放与待发放分开记。</b>这个能力是 <see cref="PowerStackType.Counter" />，
/// 同一回合的再次施加会叠加进同一个实例的 <c>Amount</c>。如果发放回合内又打出
/// 「表征」，新增的预约只是抬高 <c>Amount</c>，并没有发出去。所以回合结束时
/// 只收回 <see cref="_grantedAmount" /> 记录的<strong>实际已发放</strong>部分，
/// 把剩余的预约留给再下一回合；只用一个布尔标记会把没发过的力量也扣掉，
/// 还会丢掉新的预约。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class CharacterizationStrengthPower : PowerModel
{
    /// <summary>
    /// 本回合开始时实际发放出去的力量。回合结束时只收回这部分；
    /// 发放之后再次施加叠进 <c>Amount</c> 的部分不算在内。
    /// </summary>
    private int _grantedAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 第一步：<strong>下回合获得力量</strong>。
    /// <para>
    /// 只在你自己的回合开始时发放。发放后 <see cref="_grantedAmount" /> 非 0，
    /// 同一回合内不会重复发放；回合结束收回并清零后，剩余的预约在下个回合开始时发放。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || _grantedAmount > 0 || base.Amount <= 0)
        {
            return;
        }

        _grantedAmount = base.Amount;

        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            base.Owner,
            base.Amount,
            base.Owner,
            null,
            silent: true);
    }

    /// <summary>
    /// 第二步：<strong>下回合结束时失去力量</strong>。
    /// <para>
    /// 只收回实际发放过的部分（<see cref="_grantedAmount" />）。打出的那个回合结束时
    /// 还没有发放过，直接跳过；发放回合内再次施加产生的新预约会保留到再下一回合。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner) || _grantedAmount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            base.Owner,
            -_grantedAmount,
            base.Owner,
            null);

        int pending = base.Amount - _grantedAmount;
        _grantedAmount = 0;

        if (pending > 0)
        {
            // 发放回合内又打出了「表征」：收回已发放的部分，新预约留到再下一回合。
            SetAmount(pending);
        }
        else
        {
            await PowerCmd.Remove(this);
        }
    }
}
