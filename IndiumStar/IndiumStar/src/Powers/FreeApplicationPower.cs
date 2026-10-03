using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「溯源猛击」给的能力：<strong>下一张「运用」牌可以免费打出</strong>。
/// </summary>
/// <remarks>
/// <para>
/// 做法和原版 <c>FreeSkillPower</c> / <c>FreeAttackPower</c> 完全一致：
/// </para>
/// <list type="number">
/// <item><description>
/// <see cref="TryModifyEnergyCostInCombatLate" /> 把符合条件的牌的费用改成 0
/// （用 Late 是因为它在费用修正链的<strong>最后</strong>跑，能盖过前面所有加成）。
/// </description></item>
/// <item><description>
/// <see cref="BeforeCardPlayed" /> 在真正打出那张牌时<strong>减一层</strong>，
/// 所以只对「下一张」生效。
/// </description></item>
/// </list>
/// <para>
/// 「运用」牌按 IApplicationCard 标记判定，与显示名称和语言无关。
/// </para>
/// <para>
/// 牌堆判定只认 <c>Hand</c> 和 <c>Play</c>：前者是正常从手里打，后者是被自动打出
/// （原版这两个 Power 也是这么判的）。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class FreeApplicationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 暂时借用原版「力量」的能力图标（这个能力是"牌变便宜"，没有更贴切的原版图标）。
    /// </summary>
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        if (!Applies(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!Applies(cardPlay.Card))
        {
            return;
        }

        // 用掉一次免费机会。
        await PowerCmd.Decrement(this);
    }

    /// <summary>
    /// 这张牌是否属于「本能力该免费的那一张」：必须是自己的能力持有者的牌、
    /// 必须是「运用」牌、且必须真的在手牌里或正在被打出。
    /// </summary>
    private bool Applies(CardModel card)
        => card.Owner.Creature == base.Owner
            && ApplicationRules.IsApplicationCard(card)
            && card.Pile?.Type is PileType.Hand or PileType.Play;
}
