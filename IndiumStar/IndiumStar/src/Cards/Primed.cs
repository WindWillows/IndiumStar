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
/// 待发：获得 1 点能量，抽 1 张牌，本回合只能打出攻击牌。
/// </summary>
/// <remarks>
/// <para>
/// 升级只加抽牌数（1 → 2），能量与限制都不变。
/// </para>
/// <para>
/// 这张牌<strong>没有</strong>晶抗/晶逆，所以直接继承 <c>ModCardTemplate</c>，
/// 不继承 <c>ApplicationCard</c>（那个基类会强制走晶抗/晶逆的分支判定）。
/// </para>
/// <para>
/// 「本回合只能打出攻击牌」由隐藏能力 <see cref="OnlyAttacksThisTurnPower" /> 实现，
/// 它在你回合结束时自动移除。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Primed : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public Primed()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 1 点能量。
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);

        // 抽牌。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        // 本回合只能打出攻击牌。
        await PowerCmd.Apply<OnlyAttacksThisTurnPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
