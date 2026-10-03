using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using IndiumStar.CardPools;

namespace IndiumStar.Characters;

/// <summary>
/// 角色：铟星。
/// <para>
/// 泛型参数依次是：卡池、遗物池、药水池。
/// 初始牌组由 <c>[RegisterCharacterStarterCard]</c> 声明（见 <c>Cards/StrikeIndiumStar.cs</c> 与
/// <c>Cards/DefendIndiumStar.cs</c>），初始遗物在 <c>Entry.Init</c> 里注册。
/// </para>
/// <para>
/// 本模组暂时没有任何自制美术资源：<see cref="AssetProfile" /> 直接复用原版铁甲战士的整套资源
/// （战斗立绘、角色选择界面、地图标记、音效等）。想换成自己的素材时，把
/// <see cref="AssetProfile" /> 改成
/// <c>CharacterAssetProfiles.Merge(CharacterAssetProfiles.Ironclad(), new CharacterAssetProfile(...))</c>，
/// 只填你要覆盖的那几项即可，其余仍会自动回退到铁甲战士。
/// </para>
/// </summary>
[RegisterCharacter]
public sealed class IndiumStarCharacter
    : ModCharacterTemplate<IndiumStarCardPool, IndiumStarRelicPool, IndiumStarPotionPool>
{
    /// <summary>铟的银蓝色。</summary>
    public override Color NameColor => new("7FD4E8");

    public override Color EnergyLabelOutlineColor => new("0E3B44");

    public override Color MapDrawingColor => new("7FD4E8");

    /// <summary>影响本地化里的代词选择（中性）。</summary>
    public override CharacterGender Gender => CharacterGender.Neutral;

    public override int StartingHp => 75;

    public override int StartingGold => 99;

    /// <summary>
    /// 不使用原版的历史节点 / 时间线进度流程，省去一整套 epochs 配置。
    /// </summary>
    public override bool RequiresEpochAndTimeline => false;

    /// <summary>
    /// 缺资源时用原版铁甲战士的资源补上。
    /// 注意：<see cref="PlaceholderCharacterId" /> 默认就是 <c>ironclad</c>，
    /// 所以这里即使返回 <c>CharacterAssetProfile.Empty</c> 效果也一样，写出来只是为了明确。
    /// </summary>
    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Ironclad();

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    /// <summary>
    /// 「建筑师」战斗里随机挑选的攻击特效，沿用铁甲战士那一套。
    /// </summary>
    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter",
    ];
}
