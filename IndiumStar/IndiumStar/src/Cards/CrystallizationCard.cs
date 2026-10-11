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
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace IndiumStar.Cards;

/// <summary>
/// 晶化的公共基类：默认0费状态牌、保留、消耗，共享每玩家每回合的手动额度。
/// 自动打出沿用本体流程，不消耗手动额度。变体可以覆写关键字或基础效果。
/// </summary>
public abstract class CrystallizationCard : ApplicationCard, ICrystallizationCard
{
    private int _crystalDoomAmount;
    private int _crystalDrawAmount;
    private int _crystalExtensionAmount;

    [SavedProperty]
    public int CrystalDrawAmount
    {
        get => _crystalDrawAmount;
        set { AssertMutable(); _crystalDrawAmount = Math.Max(0, value); }
    }

    [SavedProperty]
    public int CrystalExtensionAmount
    {
        get => _crystalExtensionAmount;
        set
        {
            AssertMutable();
            _crystalExtensionAmount = Math.Max(0, value);
            if (_crystalExtensionAmount > 0) AddKeyword(IndiumStarKeywords.ExtensionKeyword);
            else RemoveKeyword(IndiumStarKeywords.ExtensionKeyword);
        }
    }

    public void AddCrystalDraw(int amount)
    {
        if (amount <= 0) return;
        CrystalDrawAmount = (int)Math.Min(int.MaxValue, (long)CrystalDrawAmount + amount);
        RefreshAddedText();
    }

    public void AddCrystalExtension(int amount)
    {
        if (amount <= 0) return;
        CrystalExtensionAmount = (int)Math.Min(int.MaxValue, (long)CrystalExtensionAmount + amount);
        RefreshAddedText();
    }

    public bool HasAddedEffectText => CrystalDoomAmount > 0 || CrystalDrawAmount > 0 || CrystalExtensionAmount > 0;

    /// <summary>消亡粉末对应的消亡层数；保留旧属性名以兼容存档，实际施加DemisePower。</summary>
    [SavedProperty]
    public int CrystalDoomAmount
    {
        get => _crystalDoomAmount;
        set { AssertMutable(); _crystalDoomAmount = Math.Max(0, value); }
    }

    public void AddCrystalDoom(int amount)
    {
        if (amount <= 0) return;
        CrystalDoomAmount = (int)Math.Min(int.MaxValue, (long)CrystalDoomAmount + amount);
        RefreshAddedText();
    }

    private void RefreshAddedText()
    {
        var pile = Pile?.Type ?? PileType.None;
        NCard.FindOnTable(this, pile)?.UpdateVisuals(pile, CardPreviewMode.Normal);
    }

    public override TargetType TargetType => CrystalDoomAmount > 0 ? TargetType.AnyEnemy : base.TargetType;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => CrystalDoomAmount > 0
        ? base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<DemisePower>()) : base.AdditionalHoverTips;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CrystalDoomActive", CrystalDoomAmount > 0);
        description.Add("CrystalDoomAmount", CrystalDoomAmount);
        description.Add("CrystalDrawActive", CrystalDrawAmount > 0);
        description.Add("CrystalDrawAmount", CrystalDrawAmount);
        description.Add("CrystalExtensionActive", CrystalExtensionAmount > 0);
        description.Add("CrystalExtensionAmount", CrystalExtensionAmount);
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
        int demise = CrystalDoomAmount;
        int draw = CrystalDrawAmount;
        int extension = CrystalExtensionAmount;
        ExtensionRules.RecordAddedExtension(play, extension > 0);
        var result = await PlayCrystallizationEffect(context, play);
        if (demise > 0 && play.Target is { IsAlive: true } target && target.Side != Owner.Creature.Side
            && target.CombatState == CombatState && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<DemisePower>(context, target, demise, Owner.Creature, this);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            if (draw > 0) await CardPileCmd.Draw(context, draw, Owner);
            if (extension > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await ExtensionRules.Gain(context, Owner, extension, this);
        }
        return result;
    }

    protected virtual Task<CardModel?> PlayCrystallizationEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.FromResult<CardModel?>(null);
}
