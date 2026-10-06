using IndiumStar.CardPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>晶罚：造成9点伤害，再为本人当时手中的每张晶化累加3（4）层消亡效果。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystalPunishment : ModCardTemplate
{
    public CrystalPunishment() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DamageVar(9m, ValueProp.Move), new PowerVar<DemisePower>(3m)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<DemisePower>()];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive) return;
        // 在伤害及其钩子返回后取手牌；不写牌组原件，不覆盖侵蚀或附魔。
        foreach (var crystal in PileType.Hand.GetPile(Owner).Cards.OfType<CrystallizationCard>().ToArray())
            crystal.AddCrystalDoom(DynamicVars["DemisePower"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars["DemisePower"].UpgradeValueBy(1m);
}
