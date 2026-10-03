using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 溯源猛击：造成 14 点伤害。你的下一张「运用」牌可以免费打出。
/// <para>升级后：伤害 14 → 20。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>「下一张运用牌免费」</b>用 <see cref="FreeApplicationPower" /> 实现，
/// 和原版「技能全免费」「攻击全免费」的 <c>FreeSkillPower</c> / <c>FreeAttackPower</c> 完全同构：
/// 一个 <c>TryModifyEnergyCostInCombatLate</c> 把费用改成 0，一个 <c>BeforeCardPlayed</c> 减层。
/// </para>
/// <para>
/// 「运用」牌按稳定牌族标记判定（<see cref="ApplicationRules.IsApplicationCard" />），
/// 新运用牌实现 IApplicationCard 即可被认到，不用改这里。
/// </para>
/// <para>
/// 注意这个能力是 <c>Counter</c> 叠加的：如果打出两张「溯源猛击」，
/// 免费机会会累加到 2 次，而不是被覆盖成 1 次。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class OrigionStrike : ApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public OrigionStrike()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 下一张「运用」牌免费：挂一层，打出那张牌时消耗掉。
        await PowerCmd.Apply<FreeApplicationPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
