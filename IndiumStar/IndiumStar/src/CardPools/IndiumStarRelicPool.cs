using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.CardPools;

/// <summary>
/// 铟星的遗物池。目前是空的（初始遗物借用原版遗物），
/// 以后写自己的遗物时加上 <c>[RegisterRelic(typeof(IndiumStarRelicPool))]</c> 即可。
/// </summary>
[RegisterSharedRelicPool]
public sealed class IndiumStarRelicPool : TypeListRelicPoolModel
{
    /// <summary>沿用 <c>ironclad</c>，直接复用原版的能量图标。</summary>
    public override string EnergyColorName => "ironclad";

    /// <summary>「遗物实验室」里该池的描边色。</summary>
    public override Color LabOutlineColor => new("7FD4E8");
}
