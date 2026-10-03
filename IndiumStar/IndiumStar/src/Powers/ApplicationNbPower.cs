using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 「运用-Nb」的能力：<strong>活力改为在打出攻击牌后只减少 2 层</strong>。
/// </summary>
/// <remarks>
/// <para>
/// 原版 <see cref="VigorPower" /> 的行为是：攻击把活力的全部层数加进伤害，
/// 然后 <c>AfterAttack</c> 把攻击开始时的层数<strong>全部</strong>扣掉。
/// 本能力不改动原版能力，而是在出牌层面做差额补偿：
/// </para>
/// <list type="number">
/// <item><description>
/// <c>BeforeCardPlayed</c>：自己人打出攻击牌时，先记下当前活力层数。
/// </description></item>
/// <item><description>
/// <c>AfterCardPlayed</c>：把层数校准到「打出前 − 2」。
/// 原版已经全扣的就补回差额；这次攻击没有造成伤害（原版没扣）的就直接扣 2。
/// </description></item>
/// </list>
/// <para>
/// 这样一次出牌系列里每个攻击段都会各扣 2 层（每个段都触发一次这对钩子），
/// 与「打出攻击牌」的口径一致。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationNbPower : ModPowerTemplate
{
    /// <summary>每次打出攻击牌后活力减少的层数。</summary>
    private const int VigorLossPerAttack = 2;

    /// <summary>本次攻击牌打出前记录下的活力层数。</summary>
    private int _vigorBeforePlay;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    private bool IsOwnAttack(CardPlay cardPlay) =>
        cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card.Type == CardType.Attack;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (IsOwnAttack(cardPlay))
        {
            _vigorBeforePlay = base.Owner.GetPowerAmount<VigorPower>();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsOwnAttack(cardPlay))
        {
            return;
        }

        int expected = Math.Max(0, _vigorBeforePlay - VigorLossPerAttack);
        int current = base.Owner.GetPowerAmount<VigorPower>();
        int delta = current - expected;
        if (delta != 0)
        {
            await PowerCmd.Apply<VigorPower>(choiceContext, base.Owner, -delta, base.Owner, null);
        }
    }
}
