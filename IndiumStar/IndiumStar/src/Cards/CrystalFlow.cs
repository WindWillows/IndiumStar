using IndiumStar.Mechanics;
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
/// 晶流：获得 5 点格挡。
/// <para>晶抗（结算后能量不为 0）：给予 1 层虚弱。</para>
/// <para>晶逆（结算后能量为 0）：重放 1（<strong>不会</strong>再触发晶抗/晶逆）。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：格挡 5 → 6，虚弱 1 → 2，重放仍是 1。
/// </para>
/// <para>
/// <b>「重放」走本体出牌系列</b>（和复制药水同一条路径）：
/// <see cref="CrystalReversalReplayCount" /> 让 <c>ModifyCardPlayCount</c> 在预测到
/// 晶逆时把本次出牌系列延长 1 段；重放段经过完整的出牌钩子与出牌历史，
/// 由 <c>ResolveEffect</c> 按 <c>IsFirstInSeries</c> 抑制晶抗/晶逆。
/// 主效果（获得格挡）不改变能量，满足预测的约束。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class CrystalFlow : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    /// <summary>晶逆的重放次数：通过延长本次出牌系列交付，重放段不再触发分支。</summary>
    protected override int CrystalReversalReplayCount => 1;

    protected override bool SuppressCrystalBranchesOnReplay => true;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<WeakPower>(1m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public CrystalFlow()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        return null;
    }

    /// <summary>晶抗：给予 1 层虚弱。</summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 晶逆：重放 1——本次出牌系列延长 1 段，重放段只重复主效果，不会再触发晶抗/晶逆。
    /// <para>
    /// 方法体保持为空：重放已经由 <see cref="CrystalReversalReplayCount" /> 驱动的
    /// 系列延长交付；这里留空实现只是为了让晶逆分支正常触发并发送通知。
    /// </para>
    /// </summary>
    protected override Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
        => Task.CompletedTask;

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Weak.UpgradeValueBy(1m);
    }
}
