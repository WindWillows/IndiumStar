using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Mechanics;

/// <summary>酸酐只识别额外效果文字；数值、费用变化和通常升级均不算。</summary>
public static class AddedCardTextRules
{
    public static bool HasAddedText(CardModel card) =>
        card is CrystallizationCard { HasAddedEffectText: true }
        || ApplicationCoPower.HasMark(card) || ApplicationUCrystallizePower.HasMark(card)
        || card.Affliction?.DynamicExtraCardText != null
        || card.Enchantment?.DynamicExtraCardText != null;
}
