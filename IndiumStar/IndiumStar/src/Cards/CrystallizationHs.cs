using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化Hs：继承晶化的公共属性和额度。
/// 当前保留原实现：在手中时，对所有角色的 powered attack 增加1伤害。
/// 这尚不是原版 StrengthPower 的完整语义，见审查报告 F11。
/// </summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystallizationHs : CrystallizationCard
{
    private const decimal StrengthWhileInHand = 1m;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Pile?.Type != PileType.Hand || dealer == null || !props.IsPoweredAttack())
        {
            return 0m;
        }

        return StrengthWhileInHand;
    }
}
