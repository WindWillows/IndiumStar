using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 气相涌流：造成 6 点伤害。每当你保留这张牌时，在本场战斗中其伤害增加 6。
/// <para>晶抗（结算后能量不为 0）：回合开始时，返回手牌。</para>
/// <para>晶逆（结算后能量为 0）：获得保留。</para>
/// </summary>
/// <remarks>
/// <para>
/// 升级后：基础伤害仍 6，<strong>每次保留的成长</strong> 6 → 9。
/// </para>
/// <para>
/// <b>「每当你保留这张牌时」用 <c>AfterFlush</c> 钩子实现。</b>
/// 回合结束弃牌时，<c>Hook.AfterFlush</c> 会传出 <c>retainedCards</c>（被留下的牌）
/// 和 <c>flushedCards</c>（被弃掉的牌），只要这张牌出现在前者里就说明它被保留了。
/// 这个钩子走的是 <c>IterateCombatHookListeners</c>，<strong>包含牌堆里的卡</strong>，
/// 所以手牌里的这张牌自己就能收到通知。
/// </para>
/// <para>
/// <b>成长写进 <c>DynamicVars.Damage.BaseValue</c></b>：卡牌的 DynamicVars 是<strong>每个实例</strong>
/// 独立的（<c>CardModel.DynamicVars</c> 是按实例惰性创建的），所以改它只影响这一张牌，
/// 而且在本场战斗内一直保留。卡面数字也会跟着变。
/// </para>
/// <para>
/// <b>每回合只成长一次：</b>晶逆会给它「保留」，所以它有可能连续多个回合留在手里。
/// 如果每回合结束都无条件加伤就会失控，所以用 <c>_boostedThisTurn</c> 保证
/// 同一回合内只加一次，回合开始时重置。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class GasPhaseSurge : ApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    /// <summary>本次出牌的晶抗是否让它在回合开始时回到手牌。</summary>
    private bool _returnToHandAtTurnStart;

    /// <summary>本回合是否已经因为「被保留」而成长过。</summary>
    private bool _boostedThisTurn;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        // 每次被保留时伤害增加多少。
        new DynamicVar("RetainGrowth", 6m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public GasPhaseSurge()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        return null;
    }

    /// <summary>晶抗：标记成「回合开始时返回手牌」。</summary>
    protected override Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        _returnToHandAtTurnStart = true;
        return Task.CompletedTask;
    }

    /// <summary>晶逆：获得保留（这张牌自带保留，所以是确保它留下）。</summary>
    protected override Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        AddKeyword(CardKeyword.Retain);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束弃牌时被调用：如果这张牌被<strong>保留</strong>了，就永久提高它的伤害。
    /// </summary>
    public override Task AfterFlush(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards)
    {
        // 只有自己的回合结束才算。
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        // 只有真的被保留了才成长；同一回合只成长一次。
        if (_boostedThisTurn || !retainedCards.Contains(this))
        {
            return Task.CompletedTask;
        }

        _boostedThisTurn = true;
        DynamicVars.Damage.BaseValue += DynamicVars["RetainGrowth"].BaseValue;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家回合开始时：重置「本回合已成长」标记，并在晶抗生效时把这张牌拉回手牌。
    /// </summary>
    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Creature.Side)
        {
            return;
        }

        // 新回合，允许再成长一次。
        _boostedThisTurn = false;

        if (!_returnToHandAtTurnStart)
        {
            return;
        }

        _returnToHandAtTurnStart = false;

        // 已经在手牌里就不动它。
        if (Pile?.Type == PileType.Hand)
        {
            return;
        }

        await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        // 升级不加基础伤害，加的是每次保留的成长量。
        DynamicVars["RetainGrowth"].UpgradeValueBy(3m);
    }
}
