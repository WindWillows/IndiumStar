using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Mechanics;

namespace IndiumStar.Cards;

/// <summary>
/// 本角色卡牌效果的公共基类，负责统一晶抗 / 晶逆判定；不是运用牌族标记。
/// </summary>
/// <remarks>
/// <para>
/// 规则：牌自身的<strong>效果结算完毕之后</strong>，如果此时能量<strong>不为 0</strong>，触发<strong>晶抗</strong>；
/// 如果能量<strong>为 0</strong>，触发<strong>晶逆</strong>。默认互斥，特殊能力可选择双分支。
/// </para>
/// <para>
/// 判定时机的依据：游戏的状态机是确定的——玩家出牌时 <c>PlayCardAction.ExecuteAction</c>
/// 先调用 <c>SpendResources()</c> 扣费，然后才调用 <c>OnPlayWrapper</c> 执行卡牌效果。
/// 所以在 <see cref="OnPlay" /> 里 <c>await</c> 完自身效果之后读到的 <c>Energy</c>，
/// 已经是扣完费的实时值，而且自身效果里如果又花了费，也会被算进去。
/// </para>
/// <para>
/// 晶抗和晶逆都是可选的：用 AvailableCrystalBranches 声明，再重写对应方法。
/// </para>
/// </remarks>
public abstract class ApplicationCard : ModCardTemplate
{
    protected ApplicationCard(int baseCost, CardType type, CardRarity rarity, TargetType target)
        : base(baseCost, type, rarity, target)
    {
    }

    /// <summary>显式声明本牌具备的分支；防止默认空方法产生虚假的触发计数。</summary>
    public virtual CrystalBranch AvailableCrystalBranches => CrystalBranch.None;

    /// <summary>
    /// 牌自身的效果。
    /// <para>
    /// 返回值会原样传给 <see cref="CrystalResistance" /> 与 <see cref="CrystalReversal" />，
    /// 用来把「主效果里选中的那张牌」之类的中转数据交给晶抗/晶逆，
    /// 避免用卡牌实例字段保存（实例会被克隆/复用，字段可能残留上次的值）。
    /// 没有要传的就返回 <c>null</c>。
    /// </para>
    /// </summary>
    protected abstract Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    /// <summary>
    /// 晶抗：效果结算后能量不为 0 时触发。
    /// <para><strong>可选</strong>——不重写就表示这张牌没有晶抗。</para>
    /// </summary>
    /// <param name="playResult">主效果返回的中转数据，可能为 <c>null</c>。</param>
    protected virtual Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
        => Task.CompletedTask;

    /// <summary>
    /// 晶逆：效果结算后能量为 0 时触发。
    /// <para><strong>可选</strong>——不重写就表示这张牌没有晶逆。</para>
    /// </summary>
    /// <param name="playResult">主效果返回的中转数据，可能为 <c>null</c>。</param>
    protected virtual Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
        => Task.CompletedTask;

    protected sealed override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => ResolveEffect(choiceContext, cardPlay);

    /// <summary>
    /// 完整结算一次这张牌：主效果 + 晶抗/晶逆判定。
    /// </summary>
    protected async Task ResolveEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 牌自身的效果。返回值留给晶抗/晶逆用。
        CardModel? playResult = await PlayEffect(choiceContext, cardPlay);

        // 重放段（PlayIndex > 0）只重复主效果，不触发晶抗/晶逆——卡牌文本明确
        // 「重放不会触发晶抗/晶逆的效果」。这对本体系列重放（复制药水、附魔）
        // 和本模组自己的晶逆重放（见 CrystalReversalReplayCount）同样成立。
        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        // 主效果结束后只读取一次能量，并在任何分支开始之前确定全部分支。
        int remainingEnergy = Owner.PlayerCombatState?.Energy ?? 0;
        var context = new CrystalBranchContext(this, cardPlay, playResult, remainingEnergy, AvailableCrystalBranches);
        CrystalBranch branches = CrystalBranchRules.SelectBranches(context);

        // 双分支固定先晶抗后晶逆。中途能量变化不重新选择。
        if ((branches & CrystalBranch.Resistance) != 0)
        {
            await CrystalResistance(choiceContext, cardPlay, playResult);
            await CrystalBranchRules.NotifyTriggered(choiceContext, context, CrystalBranch.Resistance);
        }
        if ((branches & CrystalBranch.Reversal) != 0)
        {
            await CrystalReversal(choiceContext, cardPlay, playResult);
            await CrystalBranchRules.NotifyTriggered(choiceContext, context, CrystalBranch.Reversal);
        }
    }

    /// <summary>
    /// 晶逆触发时，通过延长本次出牌系列来重放的次数；0（默认）表示这张牌的晶逆不重放。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 重放走本体出牌系列（<see cref="ModifyCardPlayCount" />，与复制药水同一条路径），
    /// 每段都经过 <c>BeforeCardPlayed</c> / <c>AfterCardPlayed</c> 钩子和出牌历史。
    /// 重放段由 <see cref="ResolveEffect" /> 按 <c>IsFirstInSeries</c> 抑制分支，
    /// 所以晶逆方法本身保持空实现即可——重放效果已经由系列延长交付。
    /// </para>
    /// <para>
    /// <b>约束：出牌次数在主效果结算之前生成，所以这里只能按当前能量<em>预测</em>
    /// 分支选择。声明了重放的牌，其主效果（<see cref="PlayEffect" />）不得改变能量，
    /// 否则预测与实际分支选择可能不一致。</b>
    /// </para>
    /// </remarks>
    protected virtual int CrystalReversalReplayCount => 0;

    /// <summary>
    /// 预测本次出牌会不会选中晶逆；会的话延长出牌系列，让重放走本体出牌生命周期。
    /// </summary>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card != this || CrystalReversalReplayCount <= 0)
        {
            return playCount;
        }

        // 此时费用已支付、主效果还没结算。对声明了重放的牌，主效果不改变能量，
        // 所以当前能量就是分支选择时会读到的能量（预测上下文没有 CardPlay）。
        int energy = Owner.PlayerCombatState?.Energy ?? 0;
        var context = new CrystalBranchContext(this, null, null, energy, AvailableCrystalBranches);
        return (CrystalBranchRules.SelectBranches(context) & CrystalBranch.Reversal) != 0
            ? playCount + CrystalReversalReplayCount
            : playCount;
    }
}
