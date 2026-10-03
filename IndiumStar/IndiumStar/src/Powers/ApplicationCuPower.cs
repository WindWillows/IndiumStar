using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 「运用-Cu」给目标挂的能力：<strong>本回合内，这名敌人每受到 7 点攻击伤害，就获得 2 层中毒</strong>。
/// </summary>
/// <remarks>
/// <para>
/// <b>累计方式：</b>用一个字段 <see cref="_accumulatedDamage" /> 记住「已经累计了多少点还没触发的伤害」。
/// 每次受伤后累加，然后用 <c>while</c> 把够整份的都结算掉，<strong>余数保留</strong>到下一次累计。
/// </para>
/// <para>
/// 举例（阈值 7）：连续受到 4、4、4 点伤害 → 累计 4、8、12，
/// 在第 2 次时触发 1 次中毒（余 1），第 3 次累计到 13 时再触发 1 次（余 6）。
/// </para>
/// <para>
/// <b>只算实际掉血</b>（<c>UnblockedDamage</c>）：打在格挡上的部分不算进累计。
/// 阈值和中毒层数由施加时传入的 <c>Amount</c> 之外的字段决定，所以用两个字段分开存。
/// </para>
/// <para>
/// <b>为什么是 <c>AfterDamageGiven</c>：</b>这个钩子在伤害结算完成后触发，
/// 能拿到 <see cref="DamageResult" />（含实际掉血量）。原版「淬毒」也是用这个钩子。
/// </para>
/// <para>
/// <b>「本回合」的清理必须自己写。</b>原版只有「虚弱」「易伤」「脆弱」会调
/// <c>PowerCmd.TickDownDuration</c> 自动减层（见 <c>WeakPower.cs:53</c> 等），
/// 自定义能力<strong>不会</strong>自动消失，而且 <c>SkipNextDurationTick</c> 对它们毫无作用
/// （那个标记只在 <c>TickDownDuration</c> 里被读）。
/// 所以这里覆写 <see cref="AfterSideTurnEnd" />，在<strong>施加方（玩家）回合结束</strong>时
/// 把自己移除，这样它正好覆盖「打出这张牌的这一个回合」。
/// 不能按 <c>base.Owner.Side</c> 判断——这个能力挂在敌人身上，那样会晚一整个敌方回合才消失。
/// </para>
/// <para>
/// <b>重复施加共享一个实例</b>（<c>Counter</c> 叠加）：累计器共用，
/// 阈值和中毒层数以<strong>最后一次施加</strong>为准（<see cref="Configure" /> 覆盖）。
/// 普通版和升级版混用时同样按最后打出的那张结算。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationCuPower : ModPowerTemplate
{
    /// <summary>已经累计、但还不够触发一次的伤害点数。</summary>
    private int _accumulatedDamage;

    /// <summary>每累计多少点伤害触发一次中毒。</summary>
    private int _damageThreshold = 7;

    /// <summary>每次触发给几层中毒。</summary>
    private int _poisonPerTrigger = 2;

    /// <summary>施加方（打出「运用-Cu」的玩家）的阵营；为 null 时回退到单人游戏的玩家阵营。</summary>
    private CombatSide? _applierSide;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>暂时借用原版「中毒」的能力图标（这个能力的产物就是中毒）。</summary>
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("poison_power");

    /// <summary>
    /// 设置阈值和每次的中毒层数。
    /// <para>由「运用-Cu」在施加能力时调用（因为卡牌升级会改阈值）。</para>
    /// <para>重复施加共享同一实例，这里以最后一次施加的配置为准。</para>
    /// </summary>
    /// <param name="applier">打出「运用-Cu」的玩家；用于确定「本回合」的结束时机。</param>
    public void Configure(Creature applier, int damageThreshold, int poisonPerTrigger)
    {
        _applierSide = applier.Side;
        _damageThreshold = Math.Max(1, damageThreshold);
        _poisonPerTrigger = poisonPerTrigger;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // 只统计打在这名敌人身上的「吃力量的攻击」伤害。
        if (target != base.Owner || !props.IsPoweredAttack())
        {
            return;
        }

        // 只算实际掉血，被格挡的部分不算。
        int dealt = result.UnblockedDamage;
        if (dealt <= 0)
        {
            return;
        }

        _accumulatedDamage += dealt;

        // 够几份就触发几次，余数保留。
        while (_accumulatedDamage >= _damageThreshold)
        {
            _accumulatedDamage -= _damageThreshold;

            await PowerCmd.Apply<PoisonPower>(
                choiceContext,
                base.Owner,
                _poisonPerTrigger,
                base.Owner,
                null);
        }
    }

    /// <summary>
    /// 施加方（玩家）回合结束时移除自己，让效果正好持续「本回合」。
    /// <para>
    /// 自定义能力不会自动减层，所以要手动清理（见类文档的说明）。
    /// 判断依据是施加方的阵营，不是挂载对象（敌人）的阵营。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != (_applierSide ?? CombatSide.Player))
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
