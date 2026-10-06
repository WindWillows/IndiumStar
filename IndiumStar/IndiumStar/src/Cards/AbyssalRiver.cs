using IndiumStar.CardPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>渊河：打出晶化后，从非手牌的战斗牌堆返回手牌。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class AbyssalRiver : ModCardTemplate
{
    public AbyssalRiver() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    // 未指定升级效果，不生成没有效果变化的升级版本。
    public override int MaxUpgradeLevel => 0;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move)];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player != Owner || !CrystallizationRules.IsCrystallizationCard(play.Card)
            || CombatState == null || play.Card.CombatState != CombatState
            || Owner.Creature.CombatState != CombatState || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding || Pile is not { IsCombatPile: true } pile
            || pile.Type == PileType.Hand)
            return;

        // 沿原生Add处理满手溢出。若在Play堆中被晶化连锁唤回，本体最终移牌不会再次弃掉它。
        await CardPileCmd.Add(this, PileType.Hand);
    }
}
