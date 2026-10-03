using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Nb：晶化（Nb）2。活力改为在打出攻击牌后只减少 2 层。
/// <para>升级后：打出时额外获得 2 层活力。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「活力扣减规则」的改造由 <see cref="ApplicationNbPower" /> 承担（差额补偿，
/// 不改原版 <see cref="VigorPower" />）。这张牌本身是能力牌，没有晶抗/晶逆，
/// 所以是普通 <see cref="ModCardTemplate" /> + <see cref="IApplicationCard" /> 标记。
/// </para>
/// <para>
/// 升级获得的活力层数用 <c>VigorPower</c> 变量承载：未升级时是 0，
/// <c>OnPlay</c> 里按值是否大于 0 决定是否发放，不用单独判断 <c>IsUpgraded</c>。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationNb : ModCardTemplate, IApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 晶化（Nb）2。
        new CardsVar(2),
        // 升级后打出时获得的活力层数（未升级为 0）。
        new PowerVar<VigorPower>(0m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationNb()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ApplicationNbPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        decimal vigor = DynamicVars["VigorPower"].BaseValue;
        if (vigor > 0m)
        {
            await PowerCmd.Apply<VigorPower>(
                choiceContext,
                Owner.Creature,
                vigor,
                Owner.Creature,
                this);
        }

        await CrystallizationRules.AddToHand<CrystallizationNb>(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VigorPower"].UpgradeValueBy(2m);
    }
}
