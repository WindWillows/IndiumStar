using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>普通晶化：0费状态牌，保留、消耗，打出无额外效果。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Crystallization : CrystallizationCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
}
