using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Cards;

/// <summary>晶化的身份、手动额度、计数与生成入口。</summary>
public static class CrystallizationRules
{
    public const int MaxPerTurn = 1;

    /// <summary>按稳定的牌族标记识别；改名和切换语言不会改变规则身份。</summary>
    public static bool IsCrystallizationCard(CardModel card) => card is ICrystallizationCard;

    public static bool HasPlayedCrystallizationThisTurn(CardModel card)
    {
        var combatState = card.CombatState;
        if (combatState == null)
        {
            return false;
        }

        // 只查询同一玩家、本回合、一次手动出牌系列的第一段。
        // Take 上限后停止扫描；自动打出和同一系列的重放都不消耗额外额度。
        int playedThisTurn = CombatManager.Instance.History.CardPlaysStarted
            .Where(entry => entry.HappenedThisTurn(combatState)
                && entry.CardPlay.Player == card.Owner
                && !entry.CardPlay.IsAutoPlay
                && entry.CardPlay.IsFirstInSeries
                && IsCrystallizationCard(entry.CardPlay.Card))
            .Take(MaxPerTurn)
            .Count();

        return playedThisTurn >= MaxPerTurn;
    }

    public static int CountCrystallizationInHand(CardModel owner) =>
        PileType.Hand.GetPile(owner.Owner).Cards.Count(IsCrystallizationCard);

    /// <summary>空括号的晶化 n：生成普通晶化。</summary>
    public static Task AddToHand(
        PlayerChoiceContext choiceContext,
        Player owner,
        ICombatState combatState,
        int count,
        CardModel source) =>
        AddToHand<Crystallization>(choiceContext, owner, combatState, count, source);

    /// <summary>晶化（X）n：生成指定晶化变体，使用本体创建和入手牌流程。</summary>
    public static async Task AddToHand<TCard>(
        PlayerChoiceContext choiceContext,
        Player owner,
        ICombatState combatState,
        int count,
        CardModel source)
        where TCard : CardModel, ICrystallizationCard
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var crystal = combatState.CreateCard<TCard>(owner);
            await CardPileCmd.AddGeneratedCardToCombat(crystal, PileType.Hand, owner);
        }
    }
}
