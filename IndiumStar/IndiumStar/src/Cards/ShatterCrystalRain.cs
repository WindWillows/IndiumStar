using IndiumStar.Mechanics;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
/// 碎晶雨：造成 3 点伤害 3 次。
/// <para>晶抗（结算后能量不为 0）：手牌中每有一张「晶化」，就再造成 3 点伤害一次。</para>
/// <para>晶逆（结算后能量为 0）：对其他敌人造成等量伤害。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：基础段数 3 → 4（每段仍是 3 点），晶抗追加段每段 3 → 4。
/// 基础每段伤害不随升级变化，所以基础段（<c>Damage</c>）和晶抗追加段
/// （<c>CrystalDamage</c>）是两个独立的变量。
/// </para>
/// <para>
/// <b>三个效果各自负责自己的段数，不要混用：</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>主效果</b>：只打 <c>Repeat</c> 段（3 段），<strong>不受</strong>手牌里的晶化影响。
/// </description></item>
/// <item><description>
/// <b>晶抗</b>：在主效果<strong>之外追加</strong>「手牌晶化张数」段。
/// 所以 1 张晶化时总共是 3 + 1 = <strong>4</strong> 段。
/// </description></item>
/// <item><description>
/// <b>晶逆</b>：对其他敌人造成「每段伤害 × <c>Repeat</c>」的等量伤害，
/// 段数<strong>不含</strong>晶化加成（晶抗和晶逆互斥）。
/// </description></item>
/// </list>
/// <para>
/// <b>踩过的坑：</b>最初把段数写成「<c>Repeat</c> + 手牌晶化张数」一个属性给三处共用，
/// 结果是晶抗把基础段数又打了一遍（1 张晶化变成 3+2=5 段），
/// 而且晶逆也带上了晶抗的加成。正确做法是每处只算自己那部分。
/// </para>
/// <para>
/// 「晶化」按稳定牌族标记统计（<see cref="CrystallizationRules.IsCrystallizationCard" />），
/// 所以「晶化」「晶化Mg」……都算，且共用「同一回合只能直接打出一次」的那次机会。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ShatterCrystalRain : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 基础段每段伤害（升级不变）。
        new DamageVar(3m, ValueProp.Move),
        // 基础段数。
        new RepeatVar(3),
        // 晶抗追加段每段伤害。
        new DamageVar("CrystalDamage", 3m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ShatterCrystalRain()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    /// <summary>
    /// 晶抗<strong>额外</strong>增加的段数 = 手牌里的晶化张数。
    /// <para>
    /// <b>只算额外部分</b>：基础段数由主效果负责，晶抗不该把基础段数再打一遍。
    /// 晶逆也不该用它（两者互斥）。
    /// </para>
    /// </summary>
    private int ExtraHitsFromHand => CrystallizationRules.CountCrystallizationInHand(this);

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 主效果：只打基础段数，不受手牌里的晶化影响。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        return null;
    }

    /// <summary>
    /// 晶抗：手牌中每有一张晶化，就<strong>额外</strong>造成一次伤害。
    /// <para>
    /// 这是主效果之外的追加段，所以段数只取 <see cref="ExtraHitsFromHand" />，
    /// 不叠加基础段数。
    /// </para>
    /// </summary>
    protected override async Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        int extraHits = ExtraHitsFromHand;
        if (extraHits <= 0 || cardPlay.Target == null)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars["CrystalDamage"].BaseValue)
            .WithHitCount(extraHits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 晶逆：对其他敌人造成等量伤害（= 每段伤害 × <strong>基础</strong>段数）。
    /// <para>
    /// 这里用 <c>Repeat</c> 而不是含晶化加成的段数：晶逆和晶抗是<strong>互斥</strong>的，
    /// 触发晶逆时晶抗的加段效果本来就不该生效。
    /// </para>
    /// </summary>
    protected override async Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        // 逐个打：AttackCommand 的 Targeting 只接受单个目标。
        // CombatState 的 `!`：出牌时卡牌必然在战斗牌堆里，它必然非空。
        var others = CombatState!.Enemies
            .Where(enemy => enemy != cardPlay.Target && enemy.IsAlive)
            .ToList();

        if (others.Count == 0)
        {
            return;
        }

        foreach (Creature enemy in others)
        {
            // 保留主伤害的段数，力量、活力、虚弱及受击钩子才会与原目标一致。
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(DynamicVars.Repeat.IntValue)
                .FromCard(this, cardPlay)
                .Targeting(enemy)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        // 基础每段伤害不变；段数 3 → 4，晶抗追加段每段 3 → 4。
        DynamicVars.Repeat.UpgradeValueBy(1m);
        DynamicVars["CrystalDamage"].UpgradeValueBy(1m);
    }
}
