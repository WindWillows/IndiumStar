using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 密铺形态：晶化 3。所有晶化牌具有「在手中时，你具有 2 点力量和 2 点敏捷」。
/// <para>升级后：获得保留。</para>
/// </summary>
/// <remarks>
/// <para>
/// 持续效果由 <see cref="TilingFormPower" /> 和手牌光环控制器提供真正的力量/敏捷。
/// 这张牌自身没有晶抗/晶逆，所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 升级的「保留」用 <c>AddKeyword</c>：它改的是实例的持久关键字状态，会随克隆保留。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class TilingForm : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 晶化 3。
        new CardsVar(3),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public TilingForm()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TilingFormPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
