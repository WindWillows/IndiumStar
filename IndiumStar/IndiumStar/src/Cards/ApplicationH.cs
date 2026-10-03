using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-H：失去 1 层易伤/虚弱/脆弱（升级 2 层）。
/// <para>晶抗：切换到下一个效果。晶逆：切换到上一个效果。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>「上下切换」原版没有，用<em>卡牌实例上的一个下标</em>实现。</b>
/// 卡牌的 <c>DynamicVars</c> 已经是每实例独立的，所以实例字段同样只影响这一张牌；
/// 而「每场战斗开始时处于『失去虚弱』」靠记录 <see cref="ICombatState" /> 实例来判定：
/// 战斗状态换了就是新的一场战斗，把下标重置为 0。
/// </para>
/// <para>
/// <b>为什么不用 <c>BeforeCombatStart</c>：</b>那个钩子走 <c>runState.IterateHookListeners</c>，
/// 只包含遗物/能力/药水等，<strong>不含牌组里的卡</strong>，卡牌收不到。
/// 而 <c>AfterCardChangedPiles</c> 走的是 <c>runState.IterateHookListeners(combatState)</c>，
/// 牌堆里的卡能收到（「君主之刃」就是这么读 <c>card.Pile.Type</c> 的），
/// 战斗开始时手牌/抽牌堆会发生变化，正好是个可靠的「战斗开始了」信号。
/// </para>
/// <para>
/// 三个状态是<strong>循环</strong>的：下一个 = (i+1) % 3，上一个 = (i+2) % 3。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationH : ApplicationCard, IApplicationCard
{
    public override CrystalBranch AvailableCrystalBranches => CrystalBranch.Both;

    /// <summary>
    /// 要失去的三种减益，顺序即切换顺序。
    /// <para>
    /// 每项同时存了「类型」（出牌时按类型找减益）和「取模型的方法」
    /// （拼卡面描述时要拿它的本地化标题）。
    /// </para>
    /// <para>
    /// 用 <c>Func&lt;PowerModel&gt;</c> 而不是 <c>ModelDb.Get(Type)</c>：
    /// <c>ModelDb</c> 只公开了泛型的 <c>Power&lt;T&gt;()</c> 和 <c>DebugPower(Type)</c>，
    /// 前者需要编译期类型，这里直接存工厂方法最干净。
    /// </para>
    /// </summary>
    private static readonly (Type Type, Func<PowerModel> Model)[] DebuffOrder =
    [
        (typeof(VulnerablePower), () => ModelDb.Power<VulnerablePower>()),
        (typeof(WeakPower), () => ModelDb.Power<WeakPower>()),
        (typeof(FrailPower), () => ModelDb.Power<FrailPower>()),
    ];

    /// <summary>当前处于第几个效果（0 = 失去易伤，即每场战斗的初始状态）。</summary>
    private int _stateIndex;

    /// <summary>上一次见到的战斗状态，用来判断是不是换了一场战斗。</summary>
    private ICombatState? _lastCombatState;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每次失去几层。
        new DynamicVar("DebuffAmount", 1m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationH()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>
    /// 把当前效果、以及晶抗/晶逆会切换到的目标效果注入卡面描述。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是原版给描述补参数的标准钩子（<c>CardModel.cs:1376</c> 在
    /// <c>DynamicVars.AddTo</c> 之后调用它），原版「疯狂科学」用它注入卡牌类型/词条。
    /// </para>
    /// <para>
    /// 注入三个变量，本地化文案里直接用：
    /// <list type="bullet">
    /// <item><c>{CurrentEffect}</c> —— 主效果（这次会扣哪种减益）</item>
    /// <item><c>{NextEffect}</c> —— 晶抗会切换到的目标</item>
    /// <item><c>{PreviousEffect}</c> —— 晶逆会切换到的目标</item>
    /// </list>
    /// </para>
    /// <para>
    /// 注入的是 <see cref="LocString" />（而不是已经格式化好的字符串）：
    /// <c>LocString.Add(string, LocString)</c> 会走 <c>GetFormattedText()</c>，
    /// 所以标题能跟着语言走，而且能保留 <c>[gold]</c> 这类富文本标记。
    /// </para>
    /// <para>
    /// 下标用 <see cref="NextIndex" /> / <see cref="PreviousIndex" /> 算，
    /// 和真正切换时用的是同一套逻辑，所以卡面显示的必然就是实际会发生的结果。
    /// </para>
    /// <para>
    /// 卡面刷新时机：这个方法每次构建描述时都会调用，所以切换效果后卡面会自动更新。
    /// </para>
    /// </remarks>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("CurrentEffect", TitleAt(_stateIndex));
        description.Add("NextEffect", TitleAt(NextIndex));
        description.Add("PreviousEffect", TitleAt(PreviousIndex));
    }

    /// <summary>晶抗会切换到的下标。</summary>
    private int NextIndex => (_stateIndex + 1) % DebuffOrder.Length;

    /// <summary>晶逆会切换到的下标。</summary>
    private int PreviousIndex => (_stateIndex + DebuffOrder.Length - 1) % DebuffOrder.Length;

    /// <summary>取第 <paramref name="index" /> 个效果的本地化标题。</summary>
    private static LocString TitleAt(int index) => DebuffOrder[index].Model().Title;

    /// <summary>
    /// 每场战斗开始时重置为「失去虚弱」。
    /// </summary>
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPile, AbstractModel? clonedBy)
    {
        // 只管自己。
        if (card != this)
        {
            return Task.CompletedTask;
        }

        ICombatState? current = CombatState;

        // 战斗状态换了实例 → 新的一场战斗 → 回到初始效果。
        if (!ReferenceEquals(_lastCombatState, current))
        {
            _lastCombatState = current;
            _stateIndex = 0;
        }

        return Task.CompletedTask;
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RemoveDebuff(choiceContext);
        return null;
    }

    /// <summary>晶抗：切换到下一个效果。</summary>
    protected override Task CrystalResistance(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        _stateIndex = NextIndex;
        RefreshCardFace();
        return Task.CompletedTask;
    }

    /// <summary>晶逆：切换到上一个效果。</summary>
    protected override Task CrystalReversal(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        CardModel? playResult)
    {
        _stateIndex = PreviousIndex;
        RefreshCardFace();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 切换效果后立刻刷新卡面，让描述里的 <c>{CurrentEffect}</c> 马上变成新效果。
    /// </summary>
    /// <remarks>
    /// 卡面本来也会在下次 <c>UpdateVisuals</c>（进牌堆、手牌重排等）时自动重建描述，
    /// 但那时这张牌可能已经离开手牌了。这里显式刷一次，保证玩家看得见切换结果。
    /// 做法和原版 <c>CardCmd.ApplyKeyword</c> / <c>RemoveKeyword</c> 完全一致。
    /// </remarks>
    private void RefreshCardFace()
    {
        PileType pileType = Pile?.Type ?? PileType.None;
        NCard.FindOnTable(this, pileType)?.UpdateVisuals(pileType, CardPreviewMode.Normal);
    }

    /// <summary>失去当前效果对应的减益层数。</summary>
    private async Task RemoveDebuff(PlayerChoiceContext choiceContext)
    {
        Creature self = Owner.Creature;
        int amount = DynamicVars["DebuffAmount"].IntValue;
        Type debuffType = DebuffOrder[_stateIndex].Type;

        PowerModel? debuff = self.Powers.FirstOrDefault(power => power.GetType() == debuffType);

        // 没有这个减益就什么都不做（不会报错、也不会切状态）。
        if (debuff == null)
        {
            return;
        }

        // 层数不够就直接移除，否则减层。
        // PowerCmd.Decrement 在减到 0 时会自己移除，所以统一用它就行。
        for (int i = 0; i < amount && debuff.Amount > 0; i++)
        {
            await PowerCmd.Decrement(debuff);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DebuffAmount"].UpgradeValueBy(1m);
    }
}
