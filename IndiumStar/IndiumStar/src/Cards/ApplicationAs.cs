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
/// 运用-As：造成 23 点伤害，晶化 2（向手牌加入 2 张「晶化」）。
/// <para>升级后：伤害 23 → 27，晶化张数不变。</para>
/// </summary>
/// <remarks>
/// <para>
/// 它属于「运用」系列，但<strong>没有晶抗/晶逆</strong>——所以不重写
/// <see cref="ApplicationCard.CrystalResistance" /> 与 <see cref="ApplicationCard.CrystalReversal" />，
/// 基类默认什么都不做（同「运用-In」「运用-Mg」）。
/// </para>
/// <para>
/// 这里的「晶化 2」是<strong>不带元素后缀</strong>的写法，所以加入的是基础的「晶化」，
/// 用 <c>CrystallizationRules.AddToHand</c> 的默认重载；带后缀的（如「晶化（Mg）」）
/// 才走泛型重载 <c>AddToHand&lt;TCard&gt;</c>。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationAs : ApplicationCard, IApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(23m, ValueProp.Move),
        // 晶化：向手牌加入几张「晶化」。
        new CardsVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationAs()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 造成 23 点伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 晶化2：向手牌加入 2 张「晶化」状态卡。
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
