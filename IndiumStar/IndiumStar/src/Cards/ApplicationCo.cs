using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Afflictions;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Co：抽 3 张牌（升级 4）。抽到的第 1 张牌在本回合获得「晶化 2」和保留。
/// </summary>
/// <remarks>
/// <para>
/// <b>「第 1 张」是靠 <c>CardPileCmd.Draw</c> 的返回值定的，不是让能力去猜。</b>
/// 之前让能力在 <c>AfterCardDrawn</c> 里认"第一张"，结果<strong>三张牌全都被标记了</strong>——
/// 因为那个钩子对<strong>每一张</strong>抽到的牌都会触发，能力没有"我是第几张"的信息。
/// </para>
/// <para>
/// <c>CardPileCmd.Draw(choiceContext, count, player)</c> 返回
/// <c>Task&lt;IEnumerable&lt;CardModel&gt;&gt;</c>（<c>CardPileCmd.cs:985</c>），
/// 而且返回顺序就是抽牌顺序（<c>DrawInternal</c> 按顺序 <c>result.Add(card)</c>，
/// <c>CardPileCmd.cs:1060</c>）。所以直接取第一个就是"抽到的第 1 张牌"。
/// </para>
/// <para>
/// <b>三件事要分清楚，缺一不可：</b>
/// </para>
/// <list type="number">
/// <item>给它<strong>本回合</strong>的「保留」（<c>CardCmd.ApplySingleTurnRetain</c>）。</item>
/// <item>挂上 <see cref="ApplicationCoPower" /> 并记录结算标记——<strong>它才负责结算</strong>：
/// 那张牌本回合每次被打出时把「晶化 2」变成真的 2 张晶化。
/// 结算状态存在运行时的 <c>AttachedState</c> 里，不占侵蚀槽，
/// 所以那张牌已经带有别的侵蚀时效果也照常生效。</item>
/// <item>侵蚀槽空闲时再挂 <see cref="CrystallizeAffliction" />——<strong>只负责显示</strong>，
/// 让卡面出现「晶化 2」。槽被占用时跳过显示，效果不受影响。</item>
/// </list>
/// <para>
/// <b>第 2 步漏过一次，症状很有迷惑性：</b>卡面明明显示「晶化 2」，
/// 打出却没有效果——因为侵蚀只是显示，没有结算者。
/// 诊断日志里 <c>ApplicationCoPower.BeforeCardPlayed</c> 一次都没出现，就是这一行缺失。
/// </para>
/// <para>
/// 这张牌没有晶抗/晶逆——按规格它没写。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationCo : ModCardTemplate, IApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 抽几张。
        new CardsVar(3),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationCo()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先挂结算能力：抽到的那张牌被打出时要靠它来结算「晶化 2」。
        await PowerCmd.Apply<ApplicationCoPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        // 抽牌并拿到抽到的牌（返回顺序 = 抽牌顺序）。
        IEnumerable<CardModel> drawn = await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.IntValue,
            Owner);

        CardModel? first = drawn.FirstOrDefault();
        if (first == null)
        {
            return;
        }

        // 「保留」：本回合有效。
        CardCmd.ApplySingleTurnRetain(first);

        // 「晶化 2」的结算标记：本回合这张牌每次被打出都加入 2 张「晶化」。
        ApplicationCoPower.MarkForCrystallize(first, CrystallizationPerCard);

        // 卡面显示：侵蚀槽空闲才挂显示标记。槽被别的侵蚀占用时跳过显示，
        // 结算标记已经记在上面，效果不受影响。重复标记也不叠加显示。
        if (first.Affliction == null)
        {
            await CardCmd.Afflict<CrystallizeAffliction>(first, CrystallizationPerCard);
        }
    }

    /// <summary>「晶化 2」的 2。</summary>
    private const int CrystallizationPerCard = 2;

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
