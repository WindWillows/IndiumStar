using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Pb：造成 9 点伤害。将至多 1 张状态牌变化为「晶化-Pb」。
/// <para>晶抗（结算后能量不为 0）：晶化 1。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：伤害 9 → 11，变化张数 1 → 2，晶化张数不变。
/// </para>
/// <para>
/// <b>「至多 N 张」= 可以不选</b>，所以选择界面用 <c>min = 0, max = N</c>
/// （<c>CardSelectorPrefs</c> 的三参数重载）。这一点和「离析」的「打出 N 张」不同
/// ——那种是 <c>min = max = N</c>，必须选满。
/// </para>
/// <para>
/// <b>「变化」用 <c>CardCmd.TransformTo&lt;T&gt;</c></b>，原版「降神」（<c>Seance</c>）
/// 就是这么把牌变成「灵魂」的。
/// </para>
/// <para>
/// 状态牌从<strong>手牌</strong>里选。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationPb : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Resistance;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move),
        // 最多把几张状态牌变化为晶化-Pb。
        new DynamicVar("TransformCount", 1m),
        // 晶抗：加入几张晶化-Pb。
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationPb()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 先造成伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 「至多 N 张」→ min = 0，可以不选。
        int maxCount = DynamicVars["TransformCount"].IntValue;

        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, 0, maxCount),
            filter: IsStatus,
            source: this);

        foreach (CardModel card in picked)
        {
            // 变化成「晶化-Pb」。
            await CardCmd.TransformTo<CrystallizationPb>(card);
        }

        return null;
    }

    /// <summary>
    /// 晶抗：晶化 1——括号内没有元素后缀，所以生成<strong>普通</strong>「晶化」，不是「晶化-Pb」。
    /// </summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);
    }

    /// <summary>只有状态牌能作为变化对象。</summary>
    private static bool IsStatus(CardModel card)
        => card.Type == CardType.Status;

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["TransformCount"].UpgradeValueBy(1m);
    }
}
