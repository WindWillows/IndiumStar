using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「运用-U」的结算者：被赋予「晶化（U）1」的牌被打出时，向手牌加入等量「晶化U」。
/// </summary>
/// <remarks>
/// <para>
/// 与「运用-Co」同一套分工：结算状态在运行时的 <see cref="AttachedState{TKey,TValue}" />
/// 里（不占侵蚀槽，已有其它侵蚀的牌也照常生效），侵蚀只负责卡面显示。
/// 区别是这个效果是<strong>本场战斗永久</strong>的：打出后不清标记，回合结束也不清，
/// 能力本身整场战斗留存。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationUCrystallizePower : ModPowerTemplate
{
    /// <summary>「晶化（U）N」里 N 的最大值，防止异常数据。</summary>
    private const int MaxCrystallizationPerPlay = 99;

    /// <summary>结算标记：哪张牌被打出时加入几张「晶化U」（本场战斗有效）。</summary>
    private static readonly AttachedState<CardModel, int> CrystallizeUMarks = new(() => 0);

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>赋予一张牌「晶化（U）N」。重复赋予不叠加。</summary>
    public static void MarkForCrystallizeU(CardModel card, int count)
    {
        CrystallizeUMarks.Set(card, Math.Clamp(count, 0, MaxCrystallizationPerPlay));
    }

    public static void CopyMark(CardModel source, CardModel copy)
    {
        int count = CrystallizeUMarks.GetValueOrDefault(source, 0);
        if (count > 0) CrystallizeUMarks.Set(copy, count);
    }

    public static bool HasMark(CardModel card) => CrystallizeUMarks.GetValueOrDefault(card, 0) > 0;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;

        if (card.IsDupe || card.Owner.Creature != base.Owner)
        {
            return;
        }

        int count = CrystallizeUMarks.GetValueOrDefault(card, 0);
        if (count <= 0)
        {
            return;
        }

        ICombatState? combatState = base.Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        // `Owner.Player` 标了可空，但这个能力必然挂在玩家身上。
        await CrystallizationRules.AddToHand<CrystallizationU>(
            new ThrowingPlayerChoiceContext(),
            base.Owner.Player!,
            combatState,
            count,
            card);
    }
}
