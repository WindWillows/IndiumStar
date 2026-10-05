using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Afflictions;

/// <summary>
/// 「晶化（U）1」：挂在「运用-电子+」上的<strong>纯显示</strong>标记（本场战斗永久）。
/// <para>表示"这张牌被打出时，向手牌加入 1 张「晶化U」"。</para>
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="CrystallizeAffliction" /> 一样只负责卡面显示；
/// 实际结算在 <see cref="Powers.ApplicationUCrystallizePower" /> 的运行时附加状态里，
/// 不占侵蚀槽的结算含义。被标记的牌已有别的侵蚀时显示挂不上，但效果照常生效。
/// </para>
/// <para>
/// 效果是本场战斗永久的，所以不在回合结束时清除。
/// </para>
/// </remarks>
[RegisterAffliction]
public sealed class CrystallizeUAffliction : ModAfflictionTemplate
{
    /// <summary>在卡面显示额外文字（「晶化（U）1」）。</summary>
    public override bool HasExtraCardText => true;
}
