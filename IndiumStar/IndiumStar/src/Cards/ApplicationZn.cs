using IndiumStar.Mechanics;
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
/// 运用-Zn：获得 12 点格挡。
/// <para>晶抗（结算后能量不为 0）：失去 1 点能量。</para>
/// <para>晶逆（结算后能量为 0）：消耗这张牌。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级只加格挡（12 → 15），费用、晶抗、晶逆都不变。
/// </para>
/// <para>
/// <b>晶抗的失去能量</b>用 <c>PlayerCmd.LoseEnergy</c>（和「分析」的晶抗同一写法）。
/// 注意它不会反过来触发晶逆：分支选择在读能量时已经完成，只判一次。
/// </para>
/// <para>
/// <b>晶逆</b>是<em>消耗这张牌本身</em>，直接调 <c>CardCmd.Exhaust</c>，见
/// <see cref="CrystalReversal" /> 的说明。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationZn : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationZn()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        return null;
    }

    /// <summary>晶抗：失去 1 点能量。</summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PlayerCmd.LoseEnergy(1m, Owner);
    }

    /// <summary>晶逆：消耗这张牌本身。</summary>
    /// <remarks>
    /// 直接 <c>CardCmd.Exhaust</c> 把牌移进消耗堆，而不是用 <c>AddKeyword(CardKeyword.Exhaust)</c>。
    /// 原因是「该进哪个牌堆」在出牌流程里是<strong>效果执行之前</strong>就定好的
    /// （<c>CardModel</c> 里先调 <c>GetResultLocationForCardPlay()</c>，之后才跑效果），
    /// 所以中途加关键字对本次出牌没有影响。而效果里就把牌移走是安全的：
    /// 收尾阶段会先判断牌是否还在出牌区（<c>pile.Type == PileType.Play</c>），
    /// 已经移走就跳过，不会重复移动。
    /// </remarks>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await CardCmd.Exhaust(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
