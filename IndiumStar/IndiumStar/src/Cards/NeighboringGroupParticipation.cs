using IndiumStar.CardPools;
using IndiumStar.Powers;
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
public sealed class NeighboringGroupParticipation : ModCardTemplate
{
    public NeighboringGroupParticipation() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(3m, ValueProp.Move), new PowerVar<StrengthPower>(3m)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var drawn = (await CardPileCmd.Draw(context, 1m, Owner)).FirstOrDefault();
        switch (drawn?.Type)
        {
            case CardType.Power:
                await CardPileCmd.Draw(context, 2m, Owner);
                break;
            case CardType.Skill:
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
                break;
            case CardType.Attack:
                await PowerCmd.Apply<NeighboringGroupStrengthPower>(context, Owner.Creature,
                    DynamicVars.Strength.BaseValue, Owner.Creature, this);
                break;
        }
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Strength.UpgradeValueBy(1m);
    }
}
