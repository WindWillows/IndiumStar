using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 延拓展延：能力牌。回合结束时，你每拥有 1 层延拓，获得 3 点格挡。
/// <para>升级后：费用 1 → 0。</para>
/// </summary>
/// <remarks>
/// <para>
/// 结算由 <see cref="ExtensionSpreadPower" /> 承担。这张牌自身没有晶抗/晶逆，
/// 所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ExtensionSpread : ModCardTemplate
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ExtensionSpread()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ExtensionSpreadPower? power = await PowerCmd.Apply<ExtensionSpreadPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        power?.Configure(3);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
