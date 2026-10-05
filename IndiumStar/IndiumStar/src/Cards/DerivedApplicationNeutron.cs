using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>运用-中子：随机自动打出弃牌堆中一张当前固定费用为0或1的牌。</summary>
/// <remarks>
/// 独立衍生技能；旧ApplicationNeutron模型仍是初始攻击「运用-电子」。
/// 自动打出不额外支付费用；嵌套出牌及其钩子全部结束后，才判断晶抗/晶逆。
/// X费用与不可打出的牌不作为候选，目标及出牌限制交由本体AutoPlay处理。
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class DerivedApplicationNeutron : ApplicationCard, IApplicationCard
{
    public DerivedApplicationNeutron() : base(1, CardType.Skill, CardRarity.Token, TargetType.Self) { }

    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    internal static bool CanAutoPlayFromDiscard(CardModel card) =>
        !card.EnergyCost.CostsX && !card.Keywords.Contains(CardKeyword.Unplayable)
        && card.EnergyCost.GetWithModifiers(CostModifiers.All) is >= 0 and <= 1;

    private bool CanResolve => CombatState != null && Owner.Creature.IsAlive
        && !CombatManager.Instance.IsOverOrEnding;

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext context, CardPlay play)
    {
        if (!CanResolve) return null;
        var picked = PileType.Discard.GetPile(Owner).Cards
            .Where(CanAutoPlayFromDiscard)
            .TakeRandom(1, Owner.RunState.Rng.CombatCardSelection)
            .FirstOrDefault();
        if (picked != null) await CardCmd.AutoPlay(context, picked, null);
        return picked;
    }

    protected override async Task CrystalResistance(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        if (CanResolve) await CardPileCmd.Draw(context, 1, Owner);
    }

    protected override async Task CrystalReversal(PlayerChoiceContext context, CardPlay play, CardModel? result)
    {
        if (!CanResolve) return;
        var selected = (await CardSelectCmd.FromHandForDiscard(context, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, this)).FirstOrDefault();
        if (selected != null && CanResolve && selected.Pile?.Type == PileType.Hand)
            await CardCmd.Discard(context, selected);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
