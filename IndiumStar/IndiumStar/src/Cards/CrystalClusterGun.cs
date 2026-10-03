using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 晶簇枪：0 费攻击。造成 6 点伤害。若你没有晶化，晶化 1，抽 1 张牌。
/// <para>升级后：伤害 6 → 8。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「若你没有晶化」按<strong>手牌</strong>统计（和「碎晶雨」「构象打击」的口径一致），
/// 判定发生在伤害之后、生成晶化之前。判定点手里的晶化会阻止整条补充效果。
/// </para>
/// <para>
/// 它没有晶抗/晶逆，所以是普通 <see cref="ModCardTemplate" />，不继承
/// <see cref="ApplicationCard" />。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystalClusterGun : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        // 晶化 1：没有晶化时加入几张普通晶化。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public CrystalClusterGun()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 若你没有晶化（手牌）：晶化 1，抽 1 张牌。
        if (CrystallizationRules.CountCrystallizationInHand(this) > 0)
        {
            return;
        }

        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
