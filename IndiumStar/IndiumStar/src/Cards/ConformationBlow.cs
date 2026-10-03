using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 构象打击：造成 4 点伤害。
/// <para>晶抗（结算后能量不为 0）：失去 1 点能量；你每拥有 1 层延拓，额外造成 5 点伤害。</para>
/// <para>晶逆（结算后能量为 0）：手牌中每有 1 张晶化，额外造成 3 点伤害。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：基础伤害不变（仍 4），<strong>两个额外伤害都提高</strong>
/// ——晶抗 5 → 7，晶逆 3 → 4。
/// </para>
/// <para>
/// <b>「延拓」就是原版的「下回合能量」能力</b>（<c>EnergyNextTurnPower</c>），
/// 它是 <c>Counter</c> 叠加，所以层数直接读它的 <c>Amount</c>。
/// </para>
/// <para>
/// <b>顺序很重要：先失去 1 点能量，再造成额外伤害。</b>
/// 因为 <see cref="ApplicationCard.ResolveEffect" /> 是在<strong>主效果之后</strong>
/// 读一次能量来决定走晶抗还是晶逆，一旦进了晶抗分支就不会再改判，
/// 所以这里失去能量不会把晶抗"变成"晶逆。
/// </para>
/// <para>
/// 另外注意：这张牌基础费用是 0，所以只要出牌时还剩 1 点以上能量就会走晶抗，
/// 而晶抗又会把其中 1 点吃掉。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ConformationBlow : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        // 晶抗：每 1 层延拓额外造成的伤害。
        new DynamicVar("ResistanceBonus", 5m),
        // 晶逆：手牌每 1 张晶化额外造成的伤害。
        new DynamicVar("ReversalBonus", 3m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ConformationBlow()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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

        return null;
    }

    /// <summary>
    /// 晶抗：失去 1 点能量，然后每 1 层延拓额外造成一次伤害。
    /// </summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        // 先失去 1 点能量。
        await PlayerCmd.LoseEnergy(1m, Owner);

        if (cardPlay.Target == null)
        {
            return;
        }

        // 延拓层数 = 原版「下回合能量」能力的层数。
        int extension = ExtensionRules.GetAmount(Owner);
        if (extension <= 0)
        {
            return;
        }

        decimal bonusDamage = extension * DynamicVars["ResistanceBonus"].BaseValue;

        await DamageCmd.Attack(bonusDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    /// <summary>晶逆：手牌中每有 1 张晶化，额外造成一次伤害。</summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        int crystalsInHand = CrystallizationRules.CountCrystallizationInHand(this);
        if (crystalsInHand <= 0)
        {
            return;
        }

        decimal bonusDamage = crystalsInHand * DynamicVars["ReversalBonus"].BaseValue;

        await DamageCmd.Attack(bonusDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级不加基础伤害，只加两个额外伤害。
        DynamicVars["ResistanceBonus"].UpgradeValueBy(2m);
        DynamicVars["ReversalBonus"].UpgradeValueBy(1m);
    }
}
