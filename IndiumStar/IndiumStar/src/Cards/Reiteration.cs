using IndiumStar.CardPools;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Reiteration : ModCardTemplate
{
    public Reiteration() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CardPileCmd.Draw(context, 1m, Owner);
        // 此次BeforeCardPlayed已经结束；新能力不会误认刚打出的复沓为下一张牌。
        await PowerCmd.Apply<ReiterationPower>(context, Owner.Creature, 1m, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
