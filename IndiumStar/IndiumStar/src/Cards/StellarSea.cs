using IndiumStar.CardPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

/// <summary>旧版辰海：仅保留旧存档模型；Token稀有度排除正常奖励和商店，不再属于当前设计卡池。</summary>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class StellarSea : ModCardTemplate
{
    public override bool CanBeGeneratedInCombat => false;
    public override MegaCrit.Sts2.Core.Models.CardPoolModel Pool =>
        MegaCrit.Sts2.Core.Models.ModelDb.CardPool<IndiumStarCardPool>();

    private bool _settled;

    public StellarSea() : base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy) { }
    public override int MaxUpgradeLevel => int.MaxValue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10m, ValueProp.Move)];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    public override Task BeforeCombatStart()
    {
        _settled = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        // RunState同时通知原件与战斗副本；只由仍在本人永久牌组中的原件结算。
        if (_settled || Pile?.Type != PileType.Deck || DeckVersion != null
            || !Owner.Deck.Cards.Contains(this) || Owner.Creature.CombatState != room.CombatState)
            return Task.CompletedTask;
        _settled = true;

        // 本体在此钩子结束后才清空本场战斗历史。精确匹配DeckVersion，复制和其他同名原件不混计。
        int plays = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.CardPlay.Player == Owner
            && (entry.CardPlay.Card == this || entry.CardPlay.Card.DeckVersion == this));
        if (plays > CurrentUpgradeLevel && IsUpgradable)
            CardCmd.Upgrade(this, CardPreviewStyle.None);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
