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
/// 回忆-污染H：能力牌。你每触发 4 次晶抗，延拓 1 并抽 1 张牌。
/// <para>升级后：阈值 4 → 3。</para>
/// </summary>
/// <remarks>
/// <para>
/// 计数与结算由 <see cref="ReminiscencePollutionHPower" /> 承担
/// （实现公共机制的 <c>ICrystalBranchListener</c>）。这张牌自身没有晶抗/晶逆，
/// 所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscencePollutionH : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 结算阈值：每触发这么多次晶抗结算一次。
        new DynamicVar("Threshold", 4m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscencePollutionH()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ReminiscencePollutionHPower? power = await PowerCmd.Apply<ReminiscencePollutionHPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        power?.Configure(DynamicVars["Threshold"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Threshold"].UpgradeValueBy(-1m);
    }
}
