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
/// 运用-Mg：造成 8 点伤害，晶化（Mg）2——向手牌加入 2 张「晶化Mg」。
/// <para>升级后：伤害 8 → 11，晶化（Mg）的张数不变。</para>
/// </summary>
/// <remarks>
/// <para>
/// 它属于「运用」系列，但<strong>没有晶抗/晶逆</strong>——所以不重写
/// <see cref="ApplicationCard.CrystalResistance" /> 与 <see cref="ApplicationCard.CrystalReversal" />，
/// 基类默认什么都不做（同「运用-In」）。
/// </para>
/// <para>
/// 「晶化（Mg）n」和「晶化 n」的唯一区别是<strong>加入哪种晶化牌</strong>：
/// 前者走 <c>CrystallizationRules.AddToHand&lt;CrystallizationMg&gt;</c>，
/// 后者走默认的 <c>AddToHand</c>（加入基础的「晶化」）。
/// </para>
/// <para>
/// 晶化Mg继承公共晶化基类，自动共用手动出牌额度，
/// 也会被离析、键合-亚胺基等牌的稳定牌族筛选识别。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationMg : ApplicationCard, IApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        // 晶化（Mg）：向手牌加入几张「晶化Mg」。
        new CardsVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationMg()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 造成 8 点伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 晶化（Mg）2：向手牌加入 2 张「晶化Mg」状态卡。
        // CombatState 的 `!`：出牌时卡牌必然在战斗牌堆里，它必然非空（详见 README）。
        await CrystallizationRules.AddToHand<CrystallizationMg>(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
