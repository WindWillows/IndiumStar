using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「密铺形态」的能力：所有晶化牌具有「在手中时，你具有 2 点力量和 2 点敏捷」。
/// </summary>
/// <remarks>
/// <para>
/// 用伤害/格挡修正钩子实现，和「晶化Hs」的「在手中时」光环同一个思路：
/// 晶化离开手牌的那一刻加成自动消失，不需要监听牌堆变化或清理残留状态。
/// 与晶化Hs 的区别是这里只作用于<strong>自己</strong>（文本是「你具有」），
/// 格挡修正的口径与原版 <c>DexterityPower</c> 完全一致。
/// </para>
/// <para>
/// 代价与晶化Hs 相同：这是加值修正，不是真的力量/敏捷能力，
/// 所以能力栏不显示层数，按力量层数结算的效果也看不到它。
/// </para>
/// <para>
/// 图标暂时借用原版「力量」的能力图标。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class TilingFormPower : ModPowerTemplate
{
    /// <summary>每张手牌晶化提供的力量/敏捷。</summary>
    private const int AuraPerCrystal = 2;

    public override PowerType Type => PowerType.Buff;

    /// <summary>效果是开关而不是计数，用 Single 不显示层数。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    /// <summary>当前手牌中晶化的数量（自己玩家的手牌）。</summary>
    private int CrystalsInHand => base.Owner.Player == null
        ? 0
        : PileType.Hand.GetPile(base.Owner.Player).Cards.Count(CrystallizationRules.IsCrystallizationCard);

    /// <summary>「力量」部分：只加给自己的攻击。</summary>
    public override decimal ModifyDamageAdditive(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != base.Owner || !props.IsPoweredAttack())
        {
            return 0m;
        }

        return AuraPerCrystal * CrystalsInHand;
    }

    /// <summary>「敏捷」部分：口径与原版 <c>DexterityPower</c> 完全一致。</summary>
    public override decimal ModifyBlockAdditive(
        Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource != null)
        {
            if (cardSource.Owner.Creature != base.Owner)
            {
                return 0m;
            }
        }
        else if (base.Owner != target)
        {
            return 0m;
        }

        if (!props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 0m;
        }

        return AuraPerCrystal * CrystalsInHand;
    }
}
