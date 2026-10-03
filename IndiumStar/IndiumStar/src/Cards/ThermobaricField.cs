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
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 温压领域：获得等同于晶化数量的临时力量。造成 7 点伤害 X 次。
/// <para>升级后：每段伤害 7 → 9。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>X 费怎么写：</b>覆写 <c>HasEnergyCostX => true</c>，基础费用传 0，
/// 然后在效果里调 <c>ResolveEnergyXValue()</c> 取 X。
/// 它会自动把「化学 X」之类修改 X 的遗物算进去。原版「旋风斩」「串刺」就是这么写的。
/// </para>
/// <para>
/// <b>顺序：先加力量、再打伤害</b>，这样这次的 X 段伤害自己就吃到力量加成。
/// 临时力量用 <see cref="ThermobaricFieldStrengthPower" />（原版 <c>TemporaryStrengthPower</c> 的子类），
/// 本回合结束时自动还回去。
/// </para>
/// <para>
/// <b>「晶化数量」= 手牌中的晶化张数</b>，和「碎晶雨」「构象打击」用的是同一个
/// <see cref="CrystallizationRules.CountCrystallizationInHand" />。
/// </para>
/// <para>
/// 注意 X 费会把能量<strong>全部花光</strong>，所以这张牌结算后能量一定是 0——
/// 如果以后要给它加晶抗/晶逆，那它永远只会走晶逆。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ThermobaricField : ApplicationCard
{
    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每一段造成的伤害。
        new DamageVar(7m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ThermobaricField()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 手牌里的晶化张数。
        int crystalCount = CrystallizationRules.CountCrystallizationInHand(this);

        // 先获得临时力量，这样下面的伤害自己就能吃到。
        if (crystalCount > 0)
        {
            await PowerCmd.Apply<ThermobaricFieldStrengthPower>(
                choiceContext,
                Owner.Creature,
                crystalCount,
                Owner.Creature,
                this);
        }

        // X = 出牌时花掉的能量。为 0 时 WithHitCount(0) 不会打出任何一段。
        int hits = ResolveEnergyXValue();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
