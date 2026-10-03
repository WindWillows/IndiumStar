using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 合斩：0 费，对所有敌人造成 5 点伤害。
/// <para>升级后：伤害 5 → 7。</para>
/// </summary>
/// <remarks>
/// <para>
/// 全体攻击用 <c>TargetingAllOpponents(CombatState)</c>（同原版「旋风斩」「匕首飞溅」）。
/// 它和「指定一个目标」的区别是：目标列表<strong>每次命中都会重新取</strong>，
/// 所以中途新加入战斗的敌人也会被打到。
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
public sealed class Slash : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public Slash()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            // CombatState 的 `!`：出牌时卡牌必然在战斗牌堆里，它必然非空。
            // 原版所有群攻牌（AstralPulse、Shatter 等 29 处）也是这么直接传的。
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
