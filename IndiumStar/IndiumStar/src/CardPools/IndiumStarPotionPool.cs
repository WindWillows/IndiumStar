using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.CardPools;

/// <summary>
/// 铟星的药水池。目前是空的，
/// 以后写自己的药水时加上 <c>[RegisterPotion(typeof(IndiumStarPotionPool))]</c> 即可。
/// </summary>
[RegisterSharedPotionPool]
public sealed class IndiumStarPotionPool : TypeListPotionPoolModel
{
    /// <summary>沿用 <c>ironclad</c>，直接复用原版的能量图标。</summary>
    public override string EnergyColorName => "ironclad";

    /// <summary>「药水实验室」里该池的描边色。</summary>
    public override Color LabOutlineColor => new("7FD4E8");
}
