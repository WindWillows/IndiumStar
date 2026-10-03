using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「运用-Hs」造成的<strong>临时力量下降</strong>：本回合结束前失去力量。
/// </summary>
/// <remarks>
/// <para>
/// 直接继承 <see cref="TemporaryStrengthPower" />，只把 <c>IsPositive</c> 改成 false，
/// 这样它就会<strong>扣</strong>力量而不是加，并在本回合结束时把扣掉的力量还回去。
/// 原版「君王之凝视」的 <c>MonarchsGazeStrengthDownPower</c> 就是这么写的。
/// </para>
/// <para>
/// 基类会自动选对文案（<c>TEMPORARY_STRENGTH_DOWN</c> 而不是 <c>TEMPORARY_STRENGTH_POWER</c>），
/// 所以不需要自己写 powers.json。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ApplicationHsStrengthDownPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<ApplicationHs>();

    /// <summary>false = 这个能力是<strong>扣</strong>力量的。</summary>
    protected override bool IsPositive => false;
}
