using IndiumStar.CardPools;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>文档中未命名的普通技能；内部标识固定，显示名称明确保留待命名状态。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class DeferredRelease : ModCardTemplate
{
    public DeferredRelease() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), null, this)).FirstOrDefault();
        if (selected == null) return;
        var exhausted = await CardCmd.Exhaust(context, selected);
        if (exhausted is not { success: true, targetPile: PileType.Exhaust }
            || CombatState is not { } combat || Owner.PlayerCombatState is not { } playerState
            || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding
            || selected.Owner != Owner || selected.CombatState != combat)
            return;
        int dueTurn = playerState.TurnNumber + 1;
        var power = await PowerCmd.Apply<DeferredReleasePower>(context, Owner.Creature, 1, Owner.Creature, this);
        power?.Schedule(selected, dueTurn, combat);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
