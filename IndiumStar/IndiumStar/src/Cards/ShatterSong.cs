using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.CardPools;

namespace IndiumStar.Cards;

/// <summary>
/// 崩碎之歌：消耗手牌中所有晶化，每张造成 9 点伤害。消耗。
/// <para>升级后：以「打出」代替消耗，每张伤害 9 → 11。</para>
/// </summary>
/// <remarks>
/// <para>
/// <b>规格歧义的落地：</b>原文未升级版写「消耗所有晶化」，后半句却写「每张被<em>打出</em>的晶化」。
/// 按「每张被处理（消耗或打出）的晶化各造成一次伤害」实现：基础版消耗、每张 9 点；
/// 升级版自动打出（不占晶化的手动额度，晶化自身效果照常触发）、每张 11 点。
/// </para>
/// <para>
/// 自动打出用 <c>CardCmd.AutoPlay</c>（本体对「再打出一张牌」的标准流程，
/// 同原版「冰雹」）。伤害段数 = 处理的晶化张数，用 <c>WithHitCount</c> 多段攻击，
/// 力量等加成按段生效。
/// </para>
/// <para>
/// 卡图暂时借用原版铁甲战士的「打击」卡图。
/// </para>
/// </remarks>
[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ShatterSong : ModCardTemplate
{
    /// <summary>消耗：打出后进入消耗堆。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每张晶化造成的伤害。
        new DamageVar(9m, ValueProp.Move),
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/strike_ironclad.png");

    public ShatterSong()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var crystals = PileType.Hand.GetPile(Owner).Cards
            .Where(CrystallizationRules.IsCrystallizationCard)
            .ToList();

        foreach (CardModel crystal in crystals)
        {
            if (IsUpgraded)
            {
                // 升级版：自动打出（不占手动额度，晶化自身效果照常触发）。
                await CardCmd.AutoPlay(choiceContext, crystal, null);
            }
            else
            {
                await CardCmd.Exhaust(choiceContext, crystal);
            }
        }

        if (crystals.Count == 0)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(crystals.Count)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
