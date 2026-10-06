using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

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

    public const string Version = "0.1.2";

    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 模型发现或第三方补丁可能提前读取卡牌关键字，先完成定义注册。
        IndiumStar.Mechanics.IndiumStarKeywords.RegisterAll();

        new Harmony(ModId).PatchAll(assembly);

        // 1) 让 RitsuLib 接管本程序集里的 Godot 脚本（本模组没有自定义场景，调用只是保险）。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 2) 注册本程序集，[RegisterCharacter] / [RegisterCard] / [RegisterCharacterStarterCard] 等
        //    特性才会被自动处理。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 缺陷晶胞通过遗物类上的RegisterCharacterStarterRelic自动注册。

        Logger.Info($"IndiumStar mod {Version} initialized.");
    }
}
