using IndiumStar.CardPools;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class Inversion : ModCardTemplate, IExtensionCard
{
    public Inversion() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, IndiumStarKeywords.ExtensionKeyword];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive) return;
        // 历史在每场战斗开始清空；仅数本人已经完成的延拓牌系列，自动打出也计入。
        // 本次反演尚未进入Finished历史；此前打出的反演可以计入。重放不重复计数。
        int played = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.CardPlay.Player == Owner && entry.CardPlay.IsFirstInSeries
            && entry.CardPlay.Card is IExtensionCard);
        await ExtensionRules.Gain(context, Owner, played, this);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
