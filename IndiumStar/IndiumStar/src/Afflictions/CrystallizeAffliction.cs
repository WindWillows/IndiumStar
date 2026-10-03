using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Afflictions;

/// <summary>
/// 「晶化 2」：挂在某张牌上的<strong>纯显示</strong>标记。
/// <para>表示"这张牌本回合被打出时，向手牌加入 2 张「晶化」"。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么用侵蚀（Affliction）来做显示：</b>「运用-Co」需要把「晶化 2」
/// 显示在<strong>被抽到的那张牌</strong>上，而那张牌是任意卡牌，改不了它的类型。
/// 原版正好有"挂在任意牌上、并且能在卡面显示额外文字"的机制——
/// <c>CardModel.GetDescriptionForPile</c> 会把
/// <c>Affliction?.DynamicExtraCardText</c> 拼到卡面描述后面（<c>CardModel.cs:1410-1414</c>）。
/// </para>
/// <para>
/// <b>侵蚀槽只有一格，所以本侵蚀不参与结算。</b>结算依据是
/// <see cref="Powers.ApplicationCoPower" /> 里的运行时附加状态（不占侵蚀槽）；
/// 被标记的牌已有别的侵蚀时，这个显示标记挂不上，但效果照常生效。
/// 层数只用于卡面数字（<c>{Amount}</c> 由 <c>AfflictionModel</c> 自己注入，
/// <c>AfflictionModel.cs:57</c>），不再有结算含义。
/// </para>
/// <para>
/// <b>「本回合」的边界必须自己管。</b>
/// 侵蚀不会自动消失，如果不管它，回合结束后卡面上那行「晶化 2」会一直留着，
/// 但效果已经失效了——显示和行为不一致。
/// 所以覆写 <see cref="AfterSideTurnEnd" />，自己的回合结束时清掉自己。
/// </para>
/// <para>
/// <b>侵蚀能收到战斗钩子：</b><c>CombatState.IterateHookListeners</c> 会把
/// 每个牌堆里每张牌的 <c>Affliction</c> 也加进监听列表
/// （<c>CombatState.cs:459-462</c>），所以这里覆写的钩子会被调用。
/// </para>
/// <para>
/// 清除用 <c>CardCmd.ClearAffliction</c>：它会触发 <c>AfflictionChanged</c>
/// （<c>CardModel.cs:1541</c>），卡面会跟着刷新，文字就消失了。
/// </para>
/// <para>
/// 这个侵蚀只负责显示，实际结算在 <see cref="Powers.ApplicationCoPower" /> 里。
/// </para>
/// </remarks>
[RegisterAffliction]
public sealed class CrystallizeAffliction : ModAfflictionTemplate
{
    /// <summary>在卡面显示额外文字（「晶化 2」）。</summary>
    public override bool HasExtraCardText => true;

    /// <summary>
    /// 层数就是"要加入几张晶化"。允许叠加，这样同一张牌被多次赋予时数字会累加。
    /// </summary>
    public override bool IsStackable => true;

    /// <summary>自己的回合结束时清掉自己，让卡面文字和效果一起消失。</summary>
    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 只管自己主人的回合；顺带避开"卡已经不在牌堆里"的情况。
        if (side != Card.Owner.Creature.Side || !HasCard)
        {
            return Task.CompletedTask;
        }

        CardCmd.ClearAffliction(Card);

        return Task.CompletedTask;
    }
}
