using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

[RegisterPower]
public sealed class ProliferationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    // 原生关键词钩子同时作用于既有晶化、后续生成晶化以及复制；不写回永久牌组。
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        card.Owner?.Creature == Owner && CrystallizationRules.IsCrystallizationCard(card)
        && keywords.Remove(CardKeyword.Retain);

    public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw) =>
        card.Owner?.Creature == Owner && card.Type == CardType.Status
        && card.CombatState == Owner.CombatState && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding
            ? CardPileCmd.Draw(context, Amount, card.Owner) : Task.CompletedTask;
}
