using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Cards;

/// <summary>按稳定标记识别运用牌族；与通用效果基类 ApplicationCard 分离。</summary>
public static class ApplicationRules
{
    public static bool IsApplicationCard(CardModel card) => card is IApplicationCard;
}
