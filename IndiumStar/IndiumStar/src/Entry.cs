using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using IndiumStar.Characters;

namespace IndiumStar;

/// <summary>
/// 模组入口。游戏的 <c>ModManager</c> 会寻找带 <see cref="ModInitializerAttribute" /> 的类型，
/// 并调用其中指定的静态方法。
/// </summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    /// <summary>必须与 <c>IndiumStar.json</c> 的 <c>id</c> 完全一致，也决定了本地化文件夹名。</summary>
    public const string ModId = "IndiumStar";

    public const string Version = "0.1.0";

    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 1) 让 RitsuLib 接管本程序集里的 Godot 脚本（本模组没有自定义场景，调用只是保险）。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 2) 注册本程序集，[RegisterCharacter] / [RegisterCard] / [RegisterCharacterStarterCard] 等
        //    特性才会被自动处理。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 3) 初始遗物：暂时借用原版「铁甲战士」的初始遗物——燃烧之血（BurningBlood）。
        //    想换成别的原版初始遗物，只需替换下面 BurningBlood 的类型：
        //      RingOfTheSnake（静默猎手）/ CrackedCore（故障机器人）/
        //      DivineRight（储君）/ BoundPhylactery（缚骨者）
        //    注意：原版类型不能加 [RegisterCharacterStarterRelic] 特性（改不了原版代码），
        //    所以这里用内容包注册。
        RitsuLibFramework.CreateContentPack(ModId)
            .CharacterStarterRelic<IndiumStarCharacter, BurningBlood>(1)
            .Apply();

        Logger.Info($"IndiumStar mod {Version} initialized.");
    }
}
