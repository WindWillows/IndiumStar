using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 离析：打出 1 张「晶化」，然后抽 2 张牌。
/// </summary>
/// <remarks>
/// <para>
/// 升级只加张数（1 → 2），抽牌数不变。
/// </para>
/// <para>
/// 「晶化」按 ICrystallizationCard 标记识别，不限于基础的那张晶化。
/// </para>
/// <para>
/// 张数是<strong>恰好</strong> N 张（描述里没写「至多」），所以由玩家自己选、且必须选满 N 张。
/// </para>
/// <para>
/// <b>打出</b>走 <c>CardCmd.AutoPlay</c>（同原版「喧嚣」的写法）。
/// 注意它只检查 <c>Hook.ShouldPlay</c> 与「不可打出」关键字，
/// <strong>不</strong>检查卡牌自己的 <c>IsPlayable</c>，所以「晶化」的
/// 「每回合只能直接打出一次」限制不会拦下这里的自动打出——这是刻意的，
/// 否则升级后的「打出 2 张」在同一回合根本打不出第二张。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Segregation : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 至多打出几张「晶化」。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public Segregation()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 描述是「打出 1 张晶化」——恰好 1 张，所以 min = max，必须选满。
        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue),
            filter: CrystallizationRules.IsCrystallizationCard,
            source: this);

        foreach (CardModel crystal in picked)
        {
            await CardCmd.AutoPlay(choiceContext, crystal, null);
        }

        // 抽 2 张牌。
        await CardPileCmd.Draw(choiceContext, 2m, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
