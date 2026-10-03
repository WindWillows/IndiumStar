using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 键合-亚胺基：消耗手牌中至多 2 张「晶化」，获得 3 点临时敏捷，延拓 2。
/// <para>升级后：获得<strong>保留</strong>，并且改成「打出」手牌中至多 2 张「晶化」。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「消耗」用 <c>CardCmd.Exhaust</c>（同原版「煤渣」的写法）；
/// 「打出」用 <c>CardCmd.AutoPlay</c>，不花费用、会触发卡牌自身效果
/// （「晶化」本身没有效果，所以实际差别只是「直接进消耗堆」与「先打出去再进消耗堆」）。
/// </para>
/// <para>
/// 临时敏捷用 <see cref="BondingImidogenDexterityPower" />，它在<strong>本回合结束时</strong>消失。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class BondingImidogen : ModCardTemplate, IExtensionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 至多消耗/打出几张「晶化」。
        new CardsVar(2),
        // 延拓：下回合的能量。
        new EnergyVar(2),
        // 临时敏捷层数。
        new PowerVar<DexterityPower>(3m),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public BondingImidogen()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 让玩家自己选，至多 N 张，允许 0 张。
        // 提示文案跟着模式走：未升级是「消耗」，升级后是「打出」。
        IEnumerable<CardModel> picked = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: Owner,
            prefs: new CardSelectorPrefs(
                _playInsteadOfExhaust
                    ? new LocString("cards", Id.Entry + ".playSelectionScreenPrompt")
                    : new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
                0,
                DynamicVars.Cards.IntValue),
            filter: CrystallizationRules.IsCrystallizationCard,
            source: this);

        foreach (CardModel crystal in picked)
        {
            if (_playInsteadOfExhaust)
            {
                await CardCmd.AutoPlay(choiceContext, crystal, null);
            }
            else
            {
                await CardCmd.Exhaust(choiceContext, crystal);
            }
        }

        // 获得 3 点临时敏捷（本回合结束时消失）。
        // 注意 DynamicVarSet 没有 Dexterity 这个便捷访问器（只有 Strength/Vulnerable/Weak 等），
        // 所以走索引器。
        await PowerCmd.Apply<BondingImidogenDexterityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DexterityPower"].BaseValue,
            Owner.Creature,
            this);

        // 延拓 2：用原版的「下回合能量」能力，Counter 叠加。
        await ExtensionRules.Gain(choiceContext, Owner, DynamicVars.Energy.IntValue, this);
    }

    /// <summary>
    /// 升级后改成「打出」而不是「消耗」。
    /// <para>
    /// 用 <c>IsUpgraded</c> 而不是实例字段：升级状态是卡牌本身的数据，
    /// 这样克隆/复用实例时也不会残留上一次的状态。
    /// </para>
    /// </summary>
    private bool _playInsteadOfExhaust => IsUpgraded;

    protected override void OnUpgrade()
    {
        // 升级：获得保留，并把「消耗」改成「打出」。
        AddKeyword(CardKeyword.Retain);
    }
}
