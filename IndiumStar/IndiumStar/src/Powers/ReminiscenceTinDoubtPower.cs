using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>
/// 「回忆-锡疑」的能力：<strong>本回合</strong>，你每打出一张牌，
/// 就交替获得 1 点临时力量 / 临时敏捷。
/// </summary>
/// <remarks>
/// <para>
/// 「临时」用原版的临时力量/敏捷基类（当回合结束自动消失）。
/// 交替状态是这个能力实例上的布尔值：第一张给力量，第二张给敏捷，如此往复。
/// </para>
/// <para>
/// 能力在「回忆-锡疑」自己的 <c>OnPlay</c> 里挂上，而 <c>AfterCardPlayed</c>
/// 会对那张牌再触发一次——用 <see cref="Configure" /> 记下那次出牌并跳过，
/// 锡疑自己不计入「每打出一张牌」。
/// </para>
/// <para>
/// 「本回合」的边界由能力自己管：自己的回合结束时移除。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ReminiscenceTinDoubtPower : ModPowerTemplate
{
    /// <summary>下一张牌给力量（true）还是敏捷（false）。</summary>
    private bool _nextIsStrength = true;

    /// <summary>挂上能力的这次出牌（回忆-锡疑自己），不计入交替计数。</summary>
    private CardPlay? _skipPlay;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>由卡牌在施加时调用：跳过挂上能力的这次出牌。</summary>
    public void Configure(CardPlay cardPlay)
    {
        _skipPlay = cardPlay;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != base.Owner || ReferenceEquals(cardPlay, _skipPlay))
        {
            return;
        }

        if (_nextIsStrength)
        {
            await PowerCmd.Apply<TinDoubtStrengthPower>(choiceContext, base.Owner, 1m, base.Owner, null);
        }
        else
        {
            await PowerCmd.Apply<TinDoubtDexterityPower>(choiceContext, base.Owner, 1m, base.Owner, null);
        }

        _nextIsStrength = !_nextIsStrength;
    }

    /// <summary>「本回合」结束：交替效果不再生效。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == base.Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
