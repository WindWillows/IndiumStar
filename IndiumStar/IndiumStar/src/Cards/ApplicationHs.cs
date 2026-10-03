using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-Hs：使所有敌人失去 7 点临时力量，晶化（Hs）2。消耗。
/// <para>升级后：失去的力量 7 → 10，晶化张数不变。</para>
/// </summary>
/// <remarks>
/// <para>
/// 「失去临时力量」用 <see cref="ApplicationHsStrengthDownPower" />
/// （<c>TemporaryStrengthPower</c> 的负值子类）：它在本回合结束前扣力量，
/// 回合结束时把扣掉的力量还回去。
/// </para>
/// <para>
/// 因为是<strong>全体敌人</strong>，所以要逐个 <c>PowerCmd.Apply</c>
/// （<c>PowerCmd</c> 一次只作用于一个目标）。
/// </para>
/// <para>
/// 它属于「运用」系列，但<strong>没有晶抗/晶逆</strong>——所以不重写
/// <see cref="ApplicationCard.CrystalResistance" /> 与 <see cref="ApplicationCard.CrystalReversal" />，
/// 基类默认什么都不做（同「运用-In」「运用-Mg」「运用-As」）。
/// </para>
/// <para>
/// 「消耗」写在 <see cref="CanonicalKeywords" /> 里，升级前后都会消耗。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationHs : ApplicationCard, IApplicationCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat([CardKeyword.Exhaust]);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 失去多少点临时力量。
        // 用普通 DynamicVar 而不是 PowerVar<T>：PowerVar 的名字取自类型名
        // （会变成一长串 {ApplicationHsStrengthDownPower}），
        // 显式命名成 StrengthLoss 文案更干净。原版「君王之凝视」就是这么做的。
        new DynamicVar("StrengthLoss", 7m),
        // 晶化（Hs）：向手牌加入几张「晶化Hs」。
        new CardsVar(2),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationHs()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 使所有敌人失去临时力量。
        decimal strengthLoss = DynamicVars["StrengthLoss"].BaseValue;

        // CombatState 的 `!`：出牌时卡牌必然在战斗牌堆里，它必然非空。
        foreach (Creature enemy in CombatState!.Enemies)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            await PowerCmd.Apply<ApplicationHsStrengthDownPower>(
                choiceContext,
                enemy,
                strengthLoss,
                Owner.Creature,
                this);
        }

        // 晶化（Hs）2：向手牌加入 2 张「晶化Hs」。
        await CrystallizationRules.AddToHand<CrystallizationHs>(
            choiceContext,
            Owner,
            CombatState!,
            DynamicVars.Cards.IntValue,
            this);

        return null;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthLoss"].UpgradeValueBy(3m);
    }
}
