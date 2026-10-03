using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化Hs：继承晶化的公共属性和额度。
/// 在手中时给所有角色提供1点真正的力量，离手后回收该光环的贡献。
/// </summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystallizationHs : CrystallizationCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        card == this && Pile?.Type == PileType.Hand
            ? CrystalHandAuraPower.EnsureAndSync(Owner.Creature)
            : Task.CompletedTask;
}
