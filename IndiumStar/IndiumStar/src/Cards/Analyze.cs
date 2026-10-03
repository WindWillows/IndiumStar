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
/// 分析：你每有一种效果，获得 2 点格挡；敌人每有一种效果，失去 2 点生命。
/// <para>晶抗：失去 1 点能量。晶逆：消耗。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后费用 1 → 0，效果数值不变。
/// </para>
/// <para>
/// <b>「效果」= 显示在角色下方的那一排能力图标</b>，用 <c>PowerModel.IsVisible</c> 判定
/// （<c>PowerModel.cs:150</c>）。隐藏的能力（例如本模组那些
/// <c>IsVisibleInternal =&gt; false</c> 的辅助能力）会被自动排除，
/// 不需要在卡里维护一份"哪些能力算数"的名单。
/// </para>
/// <para>
/// 敌人的效果只数<strong>这张牌的目标</strong>（卡是单体指向的）。
/// </para>
/// <para>
/// 「失去生命」用 <c>Unblockable | Unpowered | Move</c> 三个 flag，
/// 和「运用-Th」一样（原版「摄魂」也是这么做的）——无视格挡、不吃力量加成。
/// </para>
/// <para>
/// 格挡是<strong>自己算好数值再发</strong>（<c>CreatureCmd.GainBlock</c> 的 decimal 重载），
/// 因为层数取决于战斗状态，用不了静态的 <c>BlockVar</c>。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Analyze : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每种效果对应的格挡 / 失去生命。
        new DynamicVar("PerEffect", 2m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    /// <summary>会获得格挡，所以卡面要显示「格挡」的悬浮提示。</summary>
    public override bool GainsBlock => true;

    public Analyze()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        decimal perEffect = DynamicVars["PerEffect"].BaseValue;

        // 自己每有一种可见效果 → 2 点格挡。
        int selfEffects = CountVisibleEffects(Owner.Creature);
        if (selfEffects > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                selfEffects * perEffect,
                ValueProp.Move,
                cardPlay);
        }

        // 目标每有一种可见效果 → 失去 2 点生命。
        int targetEffects = CountVisibleEffects(cardPlay.Target);
        if (targetEffects > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                cardPlay.Target,
                new DamageVar(
                    targetEffects * perEffect,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move),
                this,
                cardPlay);
        }

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

    /// <summary>晶逆：消耗这张牌。</summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        await CardCmd.Exhaust(choiceContext, this);
    }

    /// <summary>
    /// 数一个角色身上<strong>可见</strong>的效果数量。
    /// <para>隐藏效果（<c>IsVisibleInternal =&gt; false</c>）不计入。</para>
    /// </summary>
    private static int CountVisibleEffects(Creature creature)
        => creature.Powers.Count(power => power.IsVisible);

    protected override void OnUpgrade()
    {
        // 升级只降费用：1 → 0。
        EnergyCost.UpgradeBy(-1);
    }
}
