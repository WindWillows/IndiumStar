using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「温压领域」获得的<strong>临时力量</strong>：本回合结束前获得力量。
/// </summary>
/// <remarks>
/// <para>
/// 直接继承 <see cref="TemporaryStrengthPower" />（默认就是加力量），
/// 所以本回合结束时自动还回去，和原版「屈伸」用的 <c>SetupStrikePower</c> 同构。
/// </para>
/// <para>
/// 基类会自动选对文案（<c>TEMPORARY_STRENGTH_POWER</c>）并显示力量图标，不需要自己写 powers.json。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class ThermobaricFieldStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<ThermobaricField>();
}
