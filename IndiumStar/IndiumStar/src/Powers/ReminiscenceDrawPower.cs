using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-钒永」的「本回合每当你抽牌时，下回合开始时抽等量牌」。
/// </summary>
/// <remarks>
/// <para>
/// <b>规则：从「回忆-钒永」结算开始，本回合抽的每一张牌都计入——包括它自己抽的那张。</b>
/// </para>
/// <list type="table">
/// <item><description>打出 1 张钒永 → 下回合多抽 1（它自己抽的那张）</description></item>
/// <item><description>打出 2 张钒永 → 下回合多抽 2（各抽 1 张）</description></item>
/// <item><description>钒永 + 一张抽 2 张的卡 → 下回合多抽 3（1 + 2）</description></item>
/// <item><description>钒永 ×2 + 一张抽 2 张的卡 → 下回合多抽 4（1 + 1 + 2）</description></item>
/// </list>
/// <para>
/// <b>所以累加器必须在抽牌<em>之前</em>就开始计数</b>
/// （<see cref="Configure" /> 由「回忆-钒永」在 <c>Draw</c> 之前调用）。
/// </para>
/// <para>
/// <b>为什么自己数而不用历史：</b>之前试过数 <c>CombatHistory</c>，
/// 需要先在历史里找到"这张牌的出牌记录"再往后数，而下标查找很容易被
/// "同一回合打出第二张"破坏。改成在能力自己身上累加后，就没有这类依赖了。
/// </para>
/// <para>
/// <b>为什么 <see cref="SetAmount" /> 只在回合结束时调用一次：</b>
/// 同一回合打出多张「回忆-钒永」时，<c>PowerCmd.Apply</c> 走的是叠加分支
/// （<c>PowerCmd.cs:88</c> 的 <c>ModifyAmount</c>），<strong>两次共用一个实例</strong>。
/// 所以计数必须累加、不能覆盖；而 <c>Amount</c> 在回合结束时才写一次，
/// 避免第二次 <c>SetAmount</c> 把第一次的结果冲掉。
/// </para>
/// <para>
/// <b>为什么不用原版 <c>DrawCardsNextTurnPower</c>：</b>它靠 <c>AmountOnTurnStart</c>
/// 判断"这张能力是不是本回合开始时就存在"，所以只能"本回合施加、下回合生效"。
/// 而这里要<strong>回合结束时</strong>才确定抽几张（要统计整个回合），
/// 那时再挂它就已经晚了（<c>AmountOnTurnStart</c> 是 0，不生效）。
/// 所以自己写一个，<c>ModifyHandDraw</c> 直接读 <c>Amount</c>。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscenceDrawPower : ModPowerTemplate
{
    /// <summary>是否已经开始统计。</summary>
    private bool _counting;

    /// <summary>从开始统计到现在，本回合抽了几张牌。</summary>
    private int _drawnThisTurn;

    /// <summary>统计结果，下回合抽牌时用掉。</summary>
    private int _carriedDraws;

    /// <summary>是否有等待兑现的预约。预约数量可以是 0，所以不能用 _carriedDraws 判断。</summary>
    private bool _pendingSettlement;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>
    /// 开始统计。由「回忆-钒永」在抽牌<strong>之前</strong>调用。
    /// </summary>
    /// <remarks>
    /// 反复调用是安全的：同一回合打出第二张时只是继续累加，不会把已经数到的清掉。
    /// </remarks>
    public void Configure(CardPlay cardPlay)
    {
        _counting = true;
    }

    /// <summary>每抽到一张牌就 +1（含「回忆-钒永」自己抽的那张）。</summary>
    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (_counting && card.Owner.Creature == base.Owner)
        {
            _drawnThisTurn++;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 自己的回合结束时：把"本回合抽了几张"存起来，供下回合抽牌时使用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 统计结果存在自己的字段 <c>_carriedDraws</c> 里，<strong>不用 <c>Amount</c></strong>。
    /// 原因是同一回合打出两张时两次施加共用一个实例，若用 <c>Amount</c> 当输入就会互相覆盖。
    /// </para>
    /// <para>
    /// 回合结束必须关闭统计（<c>_counting = false</c>），否则下一回合的普通抽牌
    /// 会被错误计入。预约用 <c>_pendingSettlement</c> 标记而不是抽牌数，
    /// 这样本回合一张都没抽到时（预约 0 张），下回合开始也能正常结束本次预约。
    /// </para>
    /// <para>
    /// <c>Amount</c> 只作显示：回合结束时把实际预约数量写进去，
    /// 卡面提示的「下回合开始时抽 {Amount} 张牌」才和真实结算一致。
    /// </para>
    /// </remarks>
    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != base.Owner.Side)
        {
            return Task.CompletedTask;
        }

        _carriedDraws = _drawnThisTurn;
        _drawnThisTurn = 0;
        _counting = false;
        _pendingSettlement = true;

        // 显示用：让提示数量等于实际预约数量（可能是 0）。
        SetAmount(_carriedDraws, silent: true);

        return Task.CompletedTask;
    }

    /// <summary>下回合抽牌时生效。</summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != base.Owner.Player || _carriedDraws <= 0)
        {
            return count;
        }

        return count + _carriedDraws;
    }

    /// <summary>
    /// 兑现回合开始时结束本次预约——无论预约数量是否为 0。
    /// 用 <c>_pendingSettlement</c> 判断，不能拿 <c>Amount</c> 或 <c>_carriedDraws</c> 当判据。
    /// </summary>
    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(base.Owner) && _pendingSettlement)
        {
            _pendingSettlement = false;
            _carriedDraws = 0;
            await PowerCmd.Remove(this);
        }
    }
}
