using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>晶化-Pb：继承公共额度，额外具有虚无；回合结束随机保留1张非状态牌。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystallizationPb : CrystallizationCard
{
    private const int RetainCount = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain, CardKeyword.Exhaust, CardKeyword.Ethereal];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public override bool HasTurnEndInHandEffect => true;

    protected override Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var picked = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card.Type != CardType.Status)
            .TakeRandom(RetainCount, Owner.RunState.Rng.CombatCardSelection);

        foreach (CardModel card in picked)
        {
            card.GiveSingleTurnRetain();
        }

        return Task.CompletedTask;
    }
}
