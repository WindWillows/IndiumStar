using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 基团迁移：延拓 2（升级 3）。
/// <para>晶抗：打出 1 张「晶化」。</para>
/// <para>晶逆：将手牌中的 1 张牌放在抽牌堆顶（升级：任意张）。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>「延拓」= 原版 <c>EnergyNextTurnPower</c></b>，和「键合-亚胺基」等牌一致。
/// </para>
/// <para>
/// <b>晶逆的「任意张」= 上限为手牌张数、下限 0</b>，所以升级后
/// <c>min = 0, max = 手牌张数</c>（<c>CardSelectorPrefs</c> 三参数重载）。
/// 基础版是「1 张」，必须选满 1 张，所以 <c>min = max = 1</c>。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class GroupMigration : ApplicationCard, IExtensionCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 延拓几层。
        new EnergyVar(2),
        // 晶逆要放几张牌到抽牌堆顶。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public GroupMigration()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>升级后晶逆变成「任意张」。</summary>
    private bool IsAnyNumberOfCards => IsUpgraded;

    /// <summary>
    /// 晶抗的选牌提示：「打出 1 张晶化」。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>这里不用基类的 <c>SelectionScreenPrompt</c>，而是自己建三个键</b>，
    /// 因为三种提示的<strong>措辞本来就不同</strong>：
    /// </para>
    /// <list type="bullet">
    /// <item>晶抗是「选择 1 张晶化打出」（选的是晶化牌，不是任意牌）</item>
    /// <item>晶逆基础版是「选择 1 张牌放在抽牌堆顶」</item>
    /// <item>晶逆升级版是「选择至多 N 张牌放在抽牌堆顶」</item>
    /// </list>
    /// <para>
    /// 共用 <c>selectionScreenPrompt</c> 一个键的话，文案里只能写死的"1 张"或
    /// 带 <c>{Amount}</c> 的"至多 N 张"，没法同时表达这三句话。
    /// </para>
    /// <para>
    /// 顺带说明：<c>CardSelectorPrefs</c> 会把 <c>Amount</c> / <c>MinCount</c> / <c>MaxCount</c>
    /// 写进传进来的 <see cref="LocString" />（<c>CardSelectorPrefs.cs:62-77</c>）。
    /// 但基类的 <c>SelectionScreenPrompt</c> 每次调用都会新建对象
    /// （<c>CardModel.cs:133</c>），所以不存在跨调用互相污染的问题。
    /// </para>
    /// </remarks>
    private LocString CrystalPlayPrompt =>
        new("cards", $"{Id.Entry.ToUpperInvariant()}.crystalPlayPrompt");

    /// <summary>晶逆的选牌提示（基础版：1 张）。</summary>
    private LocString PlaceOnePrompt =>
        new("cards", $"{Id.Entry.ToUpperInvariant()}.placeOnePrompt");

    /// <summary>晶逆的选牌提示（升级版：任意张）。</summary>
    private LocString PlaceAnyPrompt =>
        new("cards", $"{Id.Entry.ToUpperInvariant()}.placeAnyPrompt");

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 延拓：下回合能量。
        await ExtensionRules.Gain(choiceContext, Owner, DynamicVars.Energy.IntValue, this);

        return null;
    }

    /// <summary>晶抗：打出 1 张「晶化」。</summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            // 「打出 1 张」= 恰好 1 张，必须选满。
            prefs: new CardSelectorPrefs(CrystalPlayPrompt, 1),
            filter: CrystallizationRules.IsCrystallizationCard,
            source: this);

        foreach (CardModel crystal in picked)
        {
            await CardCmd.AutoPlay(choiceContext, crystal, null);
        }
    }

    /// <summary>晶逆：将手牌中的牌放在抽牌堆顶。</summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        int maxCount = IsAnyNumberOfCards
            ? PileType.Hand.GetPile(Owner).Cards.Count
            : DynamicVars.Cards.IntValue;

        if (maxCount <= 0)
        {
            return;
        }

        // 基础版是「1 张」（必须选满），升级版是「任意张」（下限 0）。
        CardSelectorPrefs prefs = IsAnyNumberOfCards
            ? new CardSelectorPrefs(PlaceAnyPrompt, 0, maxCount)
            : new CardSelectorPrefs(PlaceOnePrompt, maxCount);

        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            prefs: prefs,
            filter: null,
            source: this);

        foreach (CardModel card in picked)
        {
            // 放在抽牌堆顶。
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
