using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using IndiumStar.Afflictions;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「运用-Co」的结算者：让被标记的牌在本回合每次被打出时，向手牌加入等量「晶化」。
/// </summary>
/// <remarks>
/// <para>
/// <b>结算状态与显示分离。</b>结算依据是 <see cref="CrystallizeMarks" />
/// （RitsuLib 的运行时附加状态，按牌实例区分，不占本体的侵蚀槽）；
/// <see cref="CrystallizeAffliction" /> 只负责在卡面显示「晶化 2」那行字。
/// 这样被标记的牌即使已经有别的侵蚀，效果也照常生效——侵蚀槽只有一格，
/// 拿它当结算依据会让已侵蚀的牌静默丢失效果。
/// </para>
/// <para>
/// <b>「本回合具有晶化 2」= 本回合每次打出都结算。</b>
/// 所以打出时<strong>不</strong>清标记；同一张牌被重放、或回收后再次打出，都会再次结算。
/// 标记在回合结束时统一清除（<see cref="BeforeSideTurnEnd" />）。
/// </para>
/// <para>
/// <b>这个能力必须由「运用-Co」主动挂上。</b>只标记不挂能力的话，
/// 卡面会显示「晶化 2」但打出时没有任何效果——标记自己不会结算。
/// </para>
/// <para>
/// <b>写法参照原版 <c>ChainsOfBindingPower</c>（<c>:48-65</c>）：</b>
/// 先挡掉 <c>IsDupe</c>，再比对主人。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationCoPower : ModPowerTemplate
{
    /// <summary>「晶化 N」里 N 的最大值，防止异常数据。</summary>
    private const int MaxCrystallizationPerPlay = 99;

    /// <summary>
    /// 结算标记：哪张牌本回合打出时加入几张「晶化」。运行时状态，战斗结束随克隆体消失；
    /// 不经过存档序列化（效果是「本回合」的，没有需要保存的内容）。
    /// </summary>
    private static readonly AttachedState<CardModel, int> CrystallizeMarks = new(() => 0);

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// <c>Single</c> 而不是 <c>Counter</c>：层数没有意义
    /// （<c>Single</c> 会把层数固定为 1 且不显示）。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>
    /// 标记一张牌：本回合它每次被打出时加入 <paramref name="count" /> 张「晶化」。
    /// 重复标记同一张牌不叠加（「具有晶化 2」是状态，不是次数）。
    /// </summary>
    public static void MarkForCrystallize(CardModel card, int count)
    {
        CrystallizeMarks.Set(card, Math.Clamp(count, 0, MaxCrystallizationPerPlay));
    }

    public static void CopyMark(CardModel source, CardModel copy)
    {
        int count = CrystallizeMarks.GetValueOrDefault(source, 0);
        if (count > 0) CrystallizeMarks.Set(copy, count);
    }

    public static bool HasMark(CardModel card) => CrystallizeMarks.GetValueOrDefault(card, 0) > 0;

    /// <summary>那张牌被打出时，结算「晶化 N」。</summary>
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;

        // 复制出来的牌不触发（参照 ChainsOfBindingPower.cs:51-54）。
        if (card.IsDupe)
        {
            return;
        }

        // 只认自己主人的牌。
        if (card.Owner.Creature != base.Owner)
        {
            return;
        }

        int count = CrystallizeMarks.GetValueOrDefault(card, 0);
        if (count <= 0)
        {
            return;
        }

        ICombatState? combatState = base.Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 本回合每次打出都结算，不清标记；回合结束统一清除。
        // `Owner.Player` 标了可空（Creature.cs:104），但这里的能力必然挂在玩家身上，
        // 所以用 `!` 标注这个不变量。
        await CrystallizationRules.AddToHand(
            new ThrowingPlayerChoiceContext(),
            base.Owner.Player!,
            combatState,
            count,
            card);
    }

    /// <summary>自己的回合结束时清掉所有还没过期的标记与卡面文字。</summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner))
        {
            return Task.CompletedTask;
        }

        IEnumerable<CardModel> allCards = base.Owner.Player?.PlayerCombatState?.AllCards
            ?? Array.Empty<CardModel>();

        foreach (CardModel card in allCards)
        {
            if (card.Affliction is CrystallizeAffliction)
            {
                CardCmd.ClearAffliction(card);
            }

            CrystallizeMarks.Remove(card);
        }

        return Task.CompletedTask;
    }

    /// <summary>「本回合」结束：这张能力不再生效。</summary>
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
