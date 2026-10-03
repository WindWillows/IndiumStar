using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化的公共基类：默认0费状态牌、保留、消耗，共享每玩家每回合的手动额度。
/// 自动打出沿用本体流程，不消耗手动额度。变体可以覆写关键字或基础效果。
/// </summary>
public abstract class CrystallizationCard : ApplicationCard, ICrystallizationCard
{
    protected CrystallizationCard()
        : base(0, CardType.Status, CardRarity.Basic, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain, CardKeyword.Exhaust];

    /// <summary>变体的额外手动出牌条件；本体自动出牌会绕过 IsPlayable。</summary>
    protected virtual bool CanPlayCrystallization => true;

    protected sealed override bool IsPlayable =>
        CanPlayCrystallization && !CrystallizationRules.HasPlayedCrystallizationThisTurn(this);

    protected override Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.FromResult<CardModel?>(null);
}
