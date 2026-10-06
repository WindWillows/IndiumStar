using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>晶化Nb：继承晶化的公共属性和额度；打出获得3层活力。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystallizationNb : CrystallizationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VigorPower>(3m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    protected override async Task<CardModel?> PlayCrystallizationEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["VigorPower"].BaseValue,
            Owner.Creature,
            this);
        return null;
    }
}
