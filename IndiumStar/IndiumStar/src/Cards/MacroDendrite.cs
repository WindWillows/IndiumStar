using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>宏枝晶：当前数值先造成伤害，再按分支成长当前战斗实例。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class MacroDendrite : ApplicationCard
{
    public MacroDendrite() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Append(IndiumStarKeywords.CrystallizationKeyword);
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("HitCount", 3m),
        new DynamicVar("DamageGrowth", 1m),
        new DynamicVar("HitGrowth", 1m),
    ];
    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
            .Targeting(play.Target).WithHitCount(DynamicVars["HitCount"].IntValue)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
        return null;
    }
    protected override async Task CrystalResistance(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        await CrystallizationRules.AddToHand(context, Owner, CombatState!, 1, this);
        DynamicVars.Damage.BaseValue += DynamicVars["DamageGrowth"].BaseValue;
    }
    protected override async Task CrystalReversal(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        await CrystallizationRules.AddToHand(context, Owner, CombatState!, 1, this);
        DynamicVars["HitCount"].BaseValue += DynamicVars["HitGrowth"].BaseValue;
    }
    protected override void OnUpgrade()
    {
        DynamicVars["DamageGrowth"].UpgradeValueBy(1m);
        DynamicVars["HitGrowth"].UpgradeValueBy(1m);
    }
}
