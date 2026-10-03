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
using IndiumStar.Characters;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-中子：造成 10 点伤害。
/// <para>晶抗（结算后能量不为 0）：<strong>自己</strong>获得 1 层虚弱。</para>
/// <para>晶逆（结算后能量为 0）：<strong>自己</strong>获得 1 层易伤。</para>
/// </summary>
/// <remarks>
/// <para>
/// 晶抗/晶逆的负面效果都是上给<strong>铟星自己</strong>的（打出这张牌的代价），
/// 不是给被打的敌人。所以两边都用 <c>Owner.Creature</c>。
/// </para>
/// <para>
/// 按设计文档它是<strong>技能</strong>牌（不是攻击牌）：升级后费用 1 → 0，伤害 10 → 14。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
[RegisterCharacterStarterCard(typeof(IndiumStarCharacter), 1, Order = 3)]
public sealed class ApplicationNeutron : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationNeutron()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        return null;
    }

    /// <summary>
    /// 晶抗：给<strong>自己</strong> 1 层虚弱，并挂上隐藏效果
    /// <see cref="LoseWeakAtTurnEndPower" />（本回合结束时失去 1 层虚弱）。
    /// </summary>
    /// <remarks>
    /// 虚弱是「减少自己造成的伤害」，而它只在<strong>敌人回合</strong>才真正生效。
    /// 原版的虚弱是在敌人回合结束时递减的，那样会多持续一整个回合；
    /// 挂上隐藏效果后，本回合结束时就把虚弱摘掉。
    /// </remarks>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<LoseWeakAtTurnEndPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 晶逆：给<strong>自己</strong> 1 层易伤，并挂上隐藏效果
    /// <see cref="LoseVulnerableAtTurnStartPower" />（下个回合开始时失去 1 层易伤）。
    /// </summary>
    /// <remarks>
    /// 易伤是「受到的伤害增加」，要留到敌人行动时才产生影响，
    /// 所以等到你的下个回合开始才摘掉。
    /// </remarks>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Vulnerable.BaseValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<LoseVulnerableAtTurnStartPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 升级：费用 1 → 0，伤害 10 → 14（设计文档：1（0）c，造成 10（14）点伤害）。
    /// 晶抗/晶逆的层数保持 1 层不变。
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        EnergyCost.UpgradeBy(-1);
    }
}
