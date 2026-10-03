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
/// 坍缩：造成 27 点伤害。若有不少于 3 层延拓，失去所有延拓，获得 2 点能量。
/// <para>升级后：伤害 27 → 33，阈值 3 → 2。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>「延拓」就是原版的「下回合能量」能力</b>（<c>EnergyNextTurnPower</c>），
/// 它是 <c>Counter</c> 叠加，所以层数直接读 <c>Amount</c>，用 <c>PowerCmd.Remove</c> 一次清空。
/// </para>
/// <para>
/// <b>「获得 2 点能量」是本回合立即获得</b>（<c>PlayerCmd.GainEnergy</c>），
/// 不是延拓——所以它不能用来给自己"滚雪球"续延拓，但可以立刻拿去打别的牌。
/// </para>
/// <para>
/// 顺序是<strong>先打伤害、再检查延拓</strong>：这样伤害本身不会因为延拓被清空而受影响
/// （延拓本来也不影响伤害，但把结算顺序写清楚更不容易出错）。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Collapse : ApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(27m, ValueProp.Move),
        // 需要的延拓层数。
        new DynamicVar("ExtensionThreshold", 3m),
        // 满足条件时本回合获得的能量。
        new EnergyVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public Collapse()
        : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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

        // 达到阈值时消耗全部延拓；不足时不改变状态。
        if (!await ExtensionRules.TryConsumeAll(Owner, DynamicVars["ExtensionThreshold"].IntValue))
        {
            return null;
        }

        // 本回合立即获得 2 点能量。
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["ExtensionThreshold"].UpgradeValueBy(-1m);
    }
}
