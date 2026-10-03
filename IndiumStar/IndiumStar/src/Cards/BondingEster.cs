using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 键合-酯基：获得 6 点格挡，为手牌中的 1 张非攻击牌添加保留。
/// <para>晶逆（结算后能量为 0）：延拓 1。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：格挡 6 → 8，其余不变。
/// </para>
/// <para>
/// <b>「非攻击牌」</b>= <c>Type != CardType.Attack</c>，也就是技能 / 能力 / 状态 / 诅咒 / 任务
/// （<c>CardType</c> 里没有单独的「先古」，那是稀有度）。
/// </para>
/// <para>
/// <b>「添加保留」</b>用的是永久的 <see cref="CardModel.AddKeyword" />，
/// 不是 <c>CardCmd.ApplySingleTurnRetain</c>（那个只保留当前回合）。
/// </para>
/// <para>
/// 描述里写的是「为手牌中的 1 张」，所以<strong>恰好 1 张、必须选满</strong>
/// （<c>min = max = 1</c>），和「至多」不同。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class BondingEster : ApplicationCard, IExtensionCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Reversal;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        // 延拓：下回合的能量。
        new EnergyVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public BondingEster()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 6 点格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 恰好 1 张非攻击牌，必须选满。
        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
            filter: IsNonAttack,
            source: this);

        foreach (CardModel card in picked)
        {
            // 永久添加「保留」：回合结束时不被弃掉。
            card.AddKeyword(CardKeyword.Retain);
        }

        return null;
    }

    /// <summary>晶逆：延拓 1（下回合 +1 能量）。</summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await ExtensionRules.Gain(choiceContext, Owner, DynamicVars.Energy.IntValue, this);
    }

    /// <summary>
    /// 「非攻击牌」：除了攻击牌以外的都算。
    /// <para>
    /// 写成「不是攻击」而不是「是技能/能力/…」，这样以后游戏加新类型也自动覆盖。
    /// </para>
    /// </summary>
    private static bool IsNonAttack(CardModel card)
        => card.Type != CardType.Attack;

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
