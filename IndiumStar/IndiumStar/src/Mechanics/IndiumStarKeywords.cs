using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Keywords;

namespace IndiumStar.Mechanics;

/// <summary>
/// 本模组的四个规则关键字：晶化、延拓、晶抗、晶逆。
/// </summary>
/// <remarks>
/// <para>
/// 注册走 RitsuLib 的 <see cref="ModKeywordRegistry" />（卡牌关键字形式，
/// 标题与描述读取 card_keywords.json，键 = INDIUM_STAR_KEYWORD_大写词干 + .title/.description）。
/// 注册后卡牌把它们加进 <c>CanonicalKeywords</c>，悬浮提示由 RitsuLib 对
/// <c>CardModel.HoverTips</c> 的补丁自动提供；卡面文字里的金色仍由描述里的
/// <c>[gold]</c> 标记负责，两者互不影响。
/// </para>
/// <para>
/// 描述位置用 <c>None</c>：这四个词已经在卡牌正文里内联出现
/// （「晶抗：……」），不再额外追加一行关键字说明。
/// </para>
/// </remarks>
public static class IndiumStarKeywords
{
    public const string ModId = "IndiumStar";

    public static ModKeywordDefinition Crystallization { get; private set; } = null!;
    public static ModKeywordDefinition Extension { get; private set; } = null!;
    public static ModKeywordDefinition CrystalResistance { get; private set; } = null!;
    public static ModKeywordDefinition CrystalReversal { get; private set; } = null!;

    public static CardKeyword CrystallizationKeyword => Crystallization.CardKeywordValue;
    public static CardKeyword ExtensionKeyword => Extension.CardKeywordValue;
    public static CardKeyword CrystalResistanceKeyword => CrystalResistance.CardKeywordValue;
    public static CardKeyword CrystalReversalKeyword => CrystalReversal.CardKeywordValue;

    /// <summary>在 Entry.Init 中、读取卡牌关键字前注册；重复调用保留已有定义。</summary>
    public static void RegisterAll()
    {
        if (CrystalReversal != null)
        {
            return;
        }
        var registry = ModKeywordRegistry.For(ModId);
        Crystallization = Register(registry, "crystallization");
        Extension = Register(registry, "extension");
        CrystalResistance = Register(registry, "crystal_resistance");
        CrystalReversal = Register(registry, "crystal_reversal");
    }

    private static ModKeywordDefinition Register(ModKeywordRegistry registry, string stem) =>
        registry.RegisterCardKeywordOwnedByLocNamespace(
            stem, null, ModKeywordCardDescriptionPlacement.None, includeInCardHoverTip: true);
}
