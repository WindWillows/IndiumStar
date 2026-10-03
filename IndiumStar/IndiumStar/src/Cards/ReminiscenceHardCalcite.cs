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
/// 回忆-硬钙石：能力牌。回合结束时，若你本回合消耗过牌，
/// 对所有敌人造成 3 × 手牌中晶化数量的伤害。
/// <para>升级后：每张晶化的伤害 3 → 4。</para>
/// </summary>
/// <remarks>
/// <para>
/// 结算由 <see cref="ReminiscenceHardCalcitePower" /> 承担。
/// 这张牌自身没有晶抗/晶逆，所以是普通 <see cref="ModCardTemplate" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ReminiscenceHardCalcite : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每张手牌晶化造成的伤害。
        new DynamicVar("DamagePerCrystal", 3m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ReminiscenceHardCalcite()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ReminiscenceHardCalcitePower? power = await PowerCmd.Apply<ReminiscenceHardCalcitePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        power?.Configure(DynamicVars["DamagePerCrystal"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamagePerCrystal"].UpgradeValueBy(1m);
    }
}
