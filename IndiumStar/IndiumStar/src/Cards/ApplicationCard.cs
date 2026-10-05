using MegaCrit.Sts2.Core.Entities.Cards;
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
    /// 晶抗/晶逆与延拓的卡牌关键字：有对应分支的牌挂对应关键字（悬浮提示由 RitsuLib 对
    /// <c>CardModel.HoverTips</c> 的补丁提供）；延拓牌（<see cref="IExtensionCard" />）
    /// 额外挂延拓关键字。卡面文字的金色由描述里的 <c>[gold]</c> 负责。
    /// 覆写本属性的子类必须拼接 <c>base.CanonicalKeywords</c>。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            foreach (var keyword in base.CanonicalKeywords)
            {
                yield return keyword;
            }
            if ((AvailableCrystalBranches & CrystalBranch.Resistance) != 0)
            {
                yield return IndiumStarKeywords.CrystalResistanceKeyword;
            }

            if ((AvailableCrystalBranches & CrystalBranch.Reversal) != 0)
            {
                yield return IndiumStarKeywords.CrystalReversalKeyword;
            }

            if (this is IExtensionCard)
            {
                yield return IndiumStarKeywords.ExtensionKeyword;
            }
        }
    }

    /// <summary>仅文本限制分支的牌抑制系列重放，普通本体重放默认完整结算。</summary>
    protected virtual bool SuppressCrystalBranchesOnReplay => false;

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

        if (!cardPlay.IsFirstInSeries && SuppressCrystalBranchesOnReplay)
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
            // 只在实际完成的首段晶逆上预约。主效果和通知中的能量变化不会重新选择分支。
            if (cardPlay.IsFirstInSeries && CrystalReversalReplayCount > 0)
            {
                CrystalReplayRules.Request(this, cardPlay, CrystalReversalReplayCount);
            }
        }
    }

    /// <summary>首段晶逆完成后，额外执行主效果的次数；0表示不重放。</summary>
    /// <remarks>
    /// 实际基础效果结算后选择分支，再由CrystalReplaySeriesPatch延长本体当前系列。
    /// 每段保留BeforeCardPlayed/AfterCardPlayed、历史、资源和目标，最终移牌只执行一次。
    /// 不在GeneratePlayCount时预测能量；格挡、伤害和晶化生成的外部钩子也能改变能量。
    /// 文本禁止分支的牌通过SuppressCrystalBranchesOnReplay抑制后续段的晶抗/晶逆。
    /// </remarks>
    protected virtual int CrystalReversalReplayCount => 0;
}
