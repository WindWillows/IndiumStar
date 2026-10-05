using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Afflictions;
using IndiumStar.CardPools;
using IndiumStar.Powers;

namespace IndiumStar.Cards;

/// <summary>
/// 运用-U：将 3 张具有「晶化（U）1」的「运用-电子+」加入手牌。消耗。
/// <para>升级后：费用 3 → 2。</para>
/// </summary>
/// <remarks>
/// <para>
/// 生成的电子是现场创建并升级的副本（<c>CreateCard</c> + <c>CardCmd.Upgrade</c>）。
/// 「晶化（U）1」的结算状态由 <see cref="ApplicationUCrystallizePower" /> 的
/// 运行时附加状态承载（不占侵蚀槽）；侵蚀槽空闲时再挂显示标记让卡面出现词条。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「防御」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ApplicationU : ModCardTemplate, IApplicationCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 加入几张「运用-电子+」。
        new CardsVar(3),
    ];

    /// <summary>消耗：打出后进入消耗堆。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");

    public ApplicationU()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先挂结算能力：生成的电子被打出时要靠它来结算「晶化（U）1」。
        await PowerCmd.Apply<ApplicationUCrystallizePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        for (int i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            var neutron = CombatState!.CreateCard<ApplicationNeutron>(Owner);
            CardCmd.Upgrade(neutron, CardPreviewStyle.None);

            ApplicationUCrystallizePower.MarkForCrystallizeU(neutron, 1);
            if (neutron.Affliction == null)
            {
                await CardCmd.Afflict<CrystallizeUAffliction>(neutron, 1m);
            }

            await CardPileCmd.AddGeneratedCardToCombat(neutron, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
