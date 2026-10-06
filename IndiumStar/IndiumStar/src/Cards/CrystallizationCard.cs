using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化的公共基类：默认0费状态牌、保留、消耗，共享每玩家每回合的手动额度。
/// 自动打出沿用本体流程，不消耗手动额度。变体可以覆写关键字或基础效果。
/// </summary>
public abstract class CrystallizationCard : ApplicationCard, ICrystallizationCard
{
    private int _crystalDoomAmount;

    /// <summary>当前晶化的附加消亡层数；原生属性存档及深克隆保留，战斗实例不会写回牌组。</summary>
    [SavedProperty]
    public int CrystalDoomAmount
    {
        get => _crystalDoomAmount;
        set { AssertMutable(); _crystalDoomAmount = Math.Max(0, value); }
    }

    public void AddCrystalDoom(int amount)
    {
        if (amount > 0) CrystalDoomAmount = (int)Math.Min(int.MaxValue, (long)CrystalDoomAmount + amount);
    }

    public override TargetType TargetType => CrystalDoomAmount > 0 ? TargetType.AnyEnemy : base.TargetType;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => CrystalDoomAmount > 0
        ? base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<DoomPower>()) : base.AdditionalHoverTips;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CrystalDoomActive", CrystalDoomAmount > 0);
        description.Add("CrystalDoomAmount", CrystalDoomAmount);
    }

    protected CrystallizationCard()
        : base(0, CardType.Status, CardRarity.Basic, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Retain, CardKeyword.Exhaust, IndiumStarKeywords.CrystallizationKeyword]);

    /// <summary>变体的额外手动出牌条件；本体自动出牌会绕过 IsPlayable。</summary>
    protected virtual bool CanPlayCrystallization => true;

    protected sealed override bool IsPlayable =>
        CanPlayCrystallization && !CrystallizationRules.HasPlayedCrystallizationThisTurn(this);

    /// <summary>每段先执行晶化原效果，再执行附加消亡；自动打出和重放沿用同一入口。</summary>
    protected sealed override async Task<CardModel?> PlayEffect(PlayerChoiceContext context, CardPlay play)
    {
        int doom = CrystalDoomAmount;
        var result = await PlayCrystallizationEffect(context, play);
        if (doom > 0 && play.Target is { IsAlive: true } target && target.Side != Owner.Creature.Side
            && target.CombatState == CombatState && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<DoomPower>(context, target, doom, Owner.Creature, this);
        return result;
    }

    protected virtual Task<CardModel?> PlayCrystallizationEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.FromResult<CardModel?>(null);
}
