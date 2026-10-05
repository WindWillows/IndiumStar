using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Th：晶化 1。使一名敌人失去 4 点生命。
/// <para>晶抗（结算后能量不为 0）：造成等量伤害 2 次。</para>
/// <para>晶逆（结算后能量为 0）：重放 2。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：失去的生命 4 → 5，晶化张数和重放次数不变。
/// </para>
/// <para>
/// <b>「失去生命」用伤害实现：</b>没有专门的「失去生命」命令，
/// 原版「摄魂」（<c>CaptureSpirit</c>）的做法是发一次
/// <c>Unblockable | Unpowered | Move</c> 的伤害——无视格挡、不吃力量加成。
/// </para>
/// <para>
/// <b>晶抗的「等量伤害」是<em>普通攻击伤害</em></b>（吃格挡和力量），
/// 和主效果的「失去生命」不是一回事，这是刻意区分的。
/// </para>
/// <para>
/// <b>重放沿用原版完整结算：</b>文档未给Th添加晶流的「不触发晶抗/晶逆」限制。
/// 每段在各自基础效果之后选择分支；首段晶逆完成后由
/// <see cref="CrystalReversalReplayCount" /> 延长本体系列2段，后续段不再扩张同一系列。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationTh : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    /// <summary>重放次数：主效果额外再执行这么多次，通过出牌系列延长交付。</summary>
    protected override int CrystalReversalReplayCount => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 主效果「失去的生命」= 晶抗「等量伤害」的那个「量」。
        new DamageVar(4m, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move),
        new CardsVar(1),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ApplicationTh()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 按文档顺序先晶化，再使目标失去生命。两步及其钩子全部完成后才选择分支。
        await CrystallizationRules.AddToHand(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        // 使一名敌人失去生命（无视格挡、不吃力量）。
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, this, cardPlay);
        return null;
    }

    /// <summary>
    /// 晶抗：造成等量伤害 2 次。
    /// <para>这是<strong>追加</strong>在主效果之外的 2 次普通攻击伤害。</para>
    /// </summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        // 「等量」= 主效果的那个数值。
        decimal amount = DynamicVars.Damage.BaseValue;

        // 普通攻击伤害：吃格挡和力量，所以用 ValueProp.Move 而不是主效果的那套 flag。
        await DamageCmd.Attack(amount)
            .WithHitCount(2)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 晶逆：首段完成后将本次出牌系列延长2段，每段正常结算主效果与分支。
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
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
