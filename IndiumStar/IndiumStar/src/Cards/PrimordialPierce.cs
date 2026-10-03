using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 原波穿刺：造成 11 点伤害，目标身上每有一种负面状态就额外造成 3 点伤害。
/// <para>升级后：每种的额外伤害 3 → 4（基础伤害仍是 11）。</para>
/// </summary>
/// <remarks>
/// <para>
/// 这张牌用的是原版的「计算伤害」写法（<see cref="CalculatedDamageVar" />），
/// 和原版「撕裂」（<c>Rend</c>）完全同构：
/// </para>
/// <list type="bullet">
///   <item><description><c>CalculationBaseVar</c>：基础伤害（11）。</description></item>
///   <item><description><c>ExtraDamageVar</c>：每一种负面状态额外加多少（3）。</description></item>
///   <item><description><c>CalculatedDamageVar</c>：把「负面状态种数」作为乘数交给上面两项。</description></item>
/// </list>
/// <para>
/// 好处是<strong>卡面会显示实时伤害</strong>：<c>CalculatedDamageVar</c> 会随当前目标重算
/// （<c>UpdateCardPreview</c>），所以鼠标悬停在敌人身上时卡面数字已经是加过成的值，
/// 出牌时也用同一个值，不需要在效果里自己算一遍。
/// </para>
/// <para>
/// 「负面状态」的判定沿用原版 <c>Rend</c> 的 <c>ShouldCountPower</c>：
/// 能力的<strong>当前层数</strong>对应的类型是 <see cref="PowerType.Debuff" />，
/// 并且<strong>排除</strong>临时能力（<see cref="ITemporaryPower" />，例如「临时敏捷」）。
/// 每个能力只算一种，所以同一能力叠 5 层也只加一次。
/// </para>
/// <para>
/// <b>不要</b>把乘数写成捕获 <c>this</c> 的 lambda：<c>WithMultiplier</c> 会检查委托的
/// <c>Target</c> 是不是模型对象，捕获实例会直接抛异常，所以这里用静态方法。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class PrimordialPierce : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 基础伤害。
        new CalculationBaseVar(11m),
        // 每一种负面状态额外造成的伤害。
        new ExtraDamageVar(3m),
        // 计算伤害 = 基础伤害 + 额外伤害 × 负面状态种数。
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(CountDebuffs),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public PrimordialPierce()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 直接用 CalculatedDamage：它已经按当前目标把「每种负面状态 +3」算进去了。
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    /// <summary>升级：只加「每种负面状态」的额外伤害（3 → 4），基础伤害不变。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }

    /// <summary>
    /// 目标身上有几种负面状态。
    /// <para>
    /// 必须是<strong>静态</strong>方法（见类型注释里关于 <c>WithMultiplier</c> 的说明）。
    /// </para>
    /// </summary>
    private static decimal CountDebuffs(CardModel card, Creature? target)
        => target?.Powers.Count(IsCountedDebuff) ?? 0;

    /// <summary>这个能力算不算「一种负面状态」。</summary>
    private static bool IsCountedDebuff(PowerModel power)
        => power.TypeForCurrentAmount == PowerType.Debuff && power is not ITemporaryPower;
}
