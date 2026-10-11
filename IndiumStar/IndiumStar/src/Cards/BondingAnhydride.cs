using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class BondingAnhydride : ModCardTemplate
{
    // 文档未给升级效果。
    public override int MaxUpgradeLevel => 0;
    public BondingAnhydride() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1), card => card is CrystallizationCard, this))
            .OfType<CrystallizationCard>().FirstOrDefault();
        if (selected?.Owner == Owner && selected.CombatState == CombatState && selected.Pile?.Type == PileType.Hand)
            selected.AddCrystalExtension(1);

        if (Owner.PlayerCombatState == null || CombatState == null) return;
        // 开始时快照，每张仅尝试一次。新生成/回收的牌不会反复进入此轮自动打出。
        var cards = Owner.PlayerCombatState.AllCards.Where(card => card != this
            && card.Pile?.IsCombatPile == true && card.Pile.Type != PileType.Play
            && AddedCardTextRules.HasAddedText(card)).Distinct().ToArray();
        foreach (var card in cards)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) break;
            if (card.Owner != Owner || card.CombatState != CombatState
                || card.Pile?.IsCombatPile != true || card.Pile.Type == PileType.Play
                || !AddedCardTextRules.HasAddedText(card)) continue;
            await CardCmd.AutoPlay(context, card, null);
        }
    }
}
