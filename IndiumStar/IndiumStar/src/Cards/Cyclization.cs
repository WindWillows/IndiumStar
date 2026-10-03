using IndiumStar.CardPools;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Cyclization : ModCardTemplate
{
    public Cyclization() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(2), new BlockVar(5m, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), null, this)).FirstOrDefault();
        bool gainBlock = false;
        if (selected != null)
        {
            bool isCrystal = CrystallizationRules.IsCrystallizationCard(selected);
            var exhausted = await CardCmd.Exhaust(context, selected);
            gainBlock = exhausted is { success: true, targetPile: PileType.Exhaust } && !isCrystal;
        }
        await PowerCmd.Apply<DrawCardsNextTurnPower>(context, Owner.Creature,
            DynamicVars.Cards.IntValue, Owner.Creature, this);
        if (gainBlock)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        }
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
