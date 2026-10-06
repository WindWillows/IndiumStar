using IndiumStar.CardPools;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Tunneling : ApplicationCard
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ExtraCards", 0m)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => base.CanonicalKeywords.Append(CardKeyword.Exhaust);
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public Tunneling() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // X is captured before payment; an X=0 play still grants an extra turn.
        int limit = checked(ResolveEnergyXValue() + DynamicVars["ExtraCards"].IntValue);
        TunnelingPower? power = await PowerCmd.Apply<TunnelingPower>(
            choiceContext, Owner.Creature, 1, Owner.Creature, this);
        if (power != null && Owner.Creature.GetPower<TunnelingPower>() == power
            && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            power.Enqueue(limit);
            PlayerCmd.EndTurn(Owner, canBackOut: false);
        }
        return null;
    }

    protected override void OnUpgrade() => DynamicVars["ExtraCards"].UpgradeValueBy(1m);
}
