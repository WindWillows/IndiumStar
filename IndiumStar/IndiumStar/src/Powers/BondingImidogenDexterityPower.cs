using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>
/// 「键合-亚胺基」给的<strong>临时敏捷</strong>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TemporaryDexterityPower" /> 是<strong>抽象基类</strong>，不能直接注册或使用，
/// 原版的写法是每张牌配一个具体子类（如 <c>FadePower</c>、<c>AnticipatePower</c>），
/// 子类只负责告诉基类「我是被哪张牌施加的」。
/// </para>
/// <para>
/// 这个 <see cref="OriginModel" /> 决定了能力在界面上显示的名字与悬浮提示
/// （基类的 <c>Title</c> 直接取 <see cref="OriginModel" /> 的标题）。
/// </para>
/// <para>
/// 敏捷本身、以及「本回合结束时消失」的逻辑都在基类里，这里不用重复实现。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class BondingImidogenDexterityPower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<BondingImidogen>();
}
