using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.CardPools;

/// <summary>
/// 铟星的卡牌池。使用 RitsuLib 的 <see cref="TypeListCardPoolModel" />，
/// 卡牌通过各卡类上的 <c>[RegisterCard(typeof(IndiumStarCardPool))]</c> 特性注入。
/// </summary>
/// <remarks>
/// <see cref="EnergyColorName" /> 沿用 <c>ironclad</c>，这样就直接复用原版的能量图标，
/// 不需要额外的美术资源。等你做好自己的能量图标后，把 <see cref="TextEnergyIconPath" /> 与
/// <see cref="BigEnergyIconPath" /> 指到 <c>res://IndiumStar/...</c> 即可。
/// </remarks>
[RegisterSharedCardPool]
public sealed class IndiumStarCardPool : TypeListCardPoolModel
{
    /// <summary>卡图路径里会用到的池标识（不是显示名）。</summary>
    public override string Title => "indiumstar";

    public override string EnergyColorName => "ironclad";

    /// <summary>沿用原版红色卡框材质，避免自制材质缺失。</summary>
    public override string CardFrameMaterialPath => "card_frame_red";

    public override Color DeckEntryCardColor => new("7FD4E8");

    public override Color EnergyOutlineColor => new("0E3B44");

    public override bool IsColorless => false;
}
