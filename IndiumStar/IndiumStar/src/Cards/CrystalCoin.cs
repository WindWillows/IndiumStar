using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 晶币：获得 18 金币（升级 24）。此牌在本场游戏中给予的金币永久 -3。虚无。
/// </summary>
/// <remarks>
/// <para>
/// <b>「永久」是本场游戏（整个 run），并且要跨过保存/读档。</b>
/// 计数存在 RitsuLib 的 <see cref="SavedAttachedState{TKey,TValue}" /> 里：
/// 它挂在模型对象上，并通过 RitsuLib 对本体 <c>SavedProperties</c> 序列化的补丁
/// 随牌组原件一起写入跑局存档，读档后恢复。普通实例字段不走存档，
/// 只靠写回 <c>DeckVersion</c> 无法抗住保存退出。
/// </para>
/// <para>
/// <b>读写始终落在牌组原件上。</b>每场战斗开始时卡牌会被克隆
/// （<c>Player.PopulateCombatState</c>，<c>Player.cs:806-815</c>），战斗中打的是克隆体，
/// <c>DeckVersion</c> 指向牌组里的原件。<see cref="PersistentSelf" /> 统一解析到原件，
/// 所以克隆体的显示和结算读到的都是同一个计数，也不需要再分别写两份。
/// </para>
/// <para>
/// <b>多张晶币分别递减：</b>附加状态按牌实例区分，战斗中复制出来的新牌
/// 从完整收益重新开始计数。
/// </para>
/// <para>
/// <b>升级不会碰它：</b>升级改的是 <c>DynamicVars</c>，和这个计数完全无关。
/// 降级时 <c>DowngradeInternal</c> 会把 <c>DynamicVars</c> 重置回 18，
/// 但计数不受影响。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystalCoin : ModCardTemplate
{
    /// <summary>每次打出后，本场游戏的金币收益永久减少多少。</summary>
    private const decimal DecayPerPlay = 3m;

    /// <summary>
    /// 每张牌在本场游戏里已经打出过几次。随牌组原件的 <c>SavedProperties</c> 进存档。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, int> PlaysThisRunState =
        new("crystal_coin_plays_this_run", () => 0);

    /// <summary>牌组原件：战斗中打在克隆体上时解析到 <c>DeckVersion</c>，否则就是自己。</summary>
    private CrystalCoin PersistentSelf => DeckVersion is CrystalCoin deckVersion ? deckVersion : this;

    /// <summary>这张牌在本场游戏里已经打出过几次（跨战斗、跨读档保留）。</summary>
    private int PlaysThisRun
    {
        get => PlaysThisRunState.GetValueOrDefault(PersistentSelf, 0);
        set => PlaysThisRunState.Set(PersistentSelf, value);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 基础金币值（18），升级时 +6。
        new DynamicVar("Gold", 18m),
    ];

    /// <summary>虚无：回合结束时若在手中就被消耗掉。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public CrystalCoin()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>本次实际能获得的金币（基础值扣掉已打出的次数，下限 0）。</summary>
    private decimal CurrentGold
        => Math.Max(0m, DynamicVars["Gold"].BaseValue - DecayPerPlay * PlaysThisRun);

    /// <summary>
    /// 把当前实际金币写进 <c>Gold</c> 这个 <see cref="DynamicVar" /> 的显示值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>必须改 <c>PreviewValue</c>，不能另外注入一个数字。</b>
    /// 因为 <c>:diff()</c> 这个格式化器只认 <see cref="DynamicVar" /> 对象——
    /// <c>HighlightDifferencesFormatter.TryEvaluateFormat</c>
    /// 第一件事就是 <c>if (!(formattingInfo.CurrentValue is DynamicVar)) return false;</c>，
    /// 拿到普通数字会直接放弃格式化，于是卡面就显示成原文 <c>[CurrentGold:diff()]</c>。
    /// </para>
    /// <para>
    /// 而 <c>DynamicVars.AddTo(description)</c> 是把 <see cref="DynamicVar" />
    /// <strong>对象本身</strong>放进描述变量里的（<c>DynamicVarSet.cs:122</c>），
    /// 不是当时的值。所以在这里改 <c>PreviewValue</c>，随后
    /// <c>GetFormattedText()</c>（<c>CardModel.cs:1403</c>）读到的就是新值。
    /// </para>
    /// <para>
    /// <c>PreviewValue</c> 也正是 <c>:diff()</c> 会读的那个值
    /// （<c>DynamicVar.ToHighlightedString</c> 用的是 <c>PreviewValue</c>），
    /// 所以顺带还能拿到正确的变色。
    /// </para>
    /// </remarks>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        DynamicVars["Gold"].PreviewValue = CurrentGold;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先取这次的金币，再增加计数——这次拿到的还是扣之前的值。
        // 顺序很关键：CardPlayStarted 是在 OnPlay 之前记录的（CardModel.cs:1929），
        // 所以如果按"历史里的条数"算，这次出牌会被算进去，第一次就变成 15 而不是 18。
        decimal gold = CurrentGold;

        // 计数通过 PersistentSelf 落在牌组原件上，随 SavedProperties 进存档，
        // 下一场战斗和读档之后都有效。
        PlaysThisRun++;

        if (gold > 0m)
        {
            await PlayerCmd.GainGold(gold, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 只加基础值。降级时 DowngradeInternal 会把 DynamicVars 重置回 18，
        // 所以不需要写 AfterDowngraded 去还原。
        // 打出计数和 DynamicVars 完全无关，所以升级/降级都不会把它清掉。
        DynamicVars["Gold"].UpgradeValueBy(6m);
    }
}
