using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IndiumStar.Mechanics;

/// <summary>
/// 在任何意图副作用发生前，排除依赖玩家牌组、敌方槽位和怪物身体的动作。
/// 这是特殊意图尚未适配时的边界检查，不代表所有通过检查的动作已完成实机验证。
/// </summary>
public static class BorrowedIntentCompatibility
{
    private static readonly FieldInfo? ActionField = typeof(MoveState).GetField("_onPerform",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly ConcurrentDictionary<MethodInfo, string?> Results = new();
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields()
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);
    private static readonly HashSet<string> TransferablePowers =
    [
        "StrengthPower", "DexterityPower", "WeakPower", "VulnerablePower", "FrailPower",
        "RitualPower", "ArtifactPower", "PlatedArmorPower", "PoisonPower", "ThornsPower",
        "RegenerationPower", "IntangiblePower", "MetallicizePower", "VigorPower",
    ];

    public static bool CanExecute(MoveState move, out string? reason)
    {
        if (ActionField?.GetValue(move) is not Delegate action)
        {
            reason = "MoveState action contract unavailable";
            return false;
        }
        return CanExecuteAction(action.Method, out reason);
    }

    /// <summary>可用于对当前安装的游戏程序集生成动作兼容性清单，不执行怪物动作。</summary>
    public static bool CanExecuteAction(MethodInfo action, out string? reason)
    {
        reason = Results.GetOrAdd(action, method => Inspect(method, new HashSet<MethodBase>()));
        return reason == null;
    }

    private static string? Inspect(MethodInfo method, HashSet<MethodBase> visited)
    {
        if (!visited.Add(method)) return null;
        if (visited.Count > 128) return "Action dependency graph exceeds supported limit";
        var asyncType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (asyncType?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) is MethodInfo next)
        {
            string? nested = Inspect(next, visited);
            if (nested != null) return nested;
        }
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) return "Action IL unavailable: " + method.Name;
        try
        {
            for (int offset = 0; offset < il.Length;)
            {
                short code = il[offset++];
                if (code == 0xfe) code = (short)(0xfe00 | il[offset++]);
                OpCode op = Codes[code];
                int size = op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                    _ => 4,
                };
                if (op.OperandType == OperandType.InlineMethod)
                {
                    MethodBase? called = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset),
                        method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                    string? unsupported = CheckCall(called);
                    if (unsupported != null) return unsupported;
                    // 跟踪本怪物动作、辅助方法、委托和异步状态机；不进入通用引擎指令内部。
                    Type? type = called?.DeclaringType;
                    bool isMonsterCode = type != null && (typeof(MonsterModel).IsAssignableFrom(type)
                        || type.GetCustomAttribute<CompilerGeneratedAttribute>() != null);
                    if (called is MethodInfo info && isMonsterCode && info.DeclaringType != typeof(MonsterModel))
                    {
                        string? nested = Inspect(info, visited);
                        if (nested != null) return nested;
                    }
                }
                offset += size;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or KeyNotFoundException or BadImageFormatException)
        {
            return "Cannot inspect action IL: " + method.Name;
        }
        return null;
    }

    private static string? CheckCall(MethodBase? method)
    {
        if (method == null) return "Unresolved action dependency";
        string type = method.DeclaringType?.FullName ?? "";
        string name = method.Name;
        if (type.EndsWith(".MonsterModel") && name == "get_RunRng")
            return "Action consumes run-scoped RNG and needs a dedicated adapter";
        if (type.EndsWith(".Creature") && name is "get_Player" or "get_PetOwner" or "get_Monster")
            return "Action depends on a player/monster-specific target: " + name;
        if (type.EndsWith(".CreatureCmd") && name is "Add" or "Kill" or "Escape" or "SetMaxHp" or "SetCurrentHp" or "Revive")
            return "Body/summon action needs a dedicated adapter: " + name;
        if ((type.EndsWith(".ICombatState") || type.EndsWith(".CombatState")) && name is "get_Enemies" or "get_PlayerCreatures" or "get_Players")
            return "Action hard-codes a combat side: " + name;
        if (type.EndsWith(".NCreature") && name is "GetSpecialNode" or "get_SpineAnimation" or "SetDefaultScaleTo")
            return "Action requires a monster-specific visual: " + name;
        if (type.EndsWith(".Creature") && name is "GetPower" or "GetPowerAmount"
            || type.EndsWith(".PowerCmd") && name == "Apply")
        {
            if (method is MethodInfo info && info.IsGenericMethod
                && info.GetGenericArguments().Any(power => !TransferablePowers.Contains(power.Name)))
                return "Monster-specific power needs an adapter: " + string.Join(",", info.GetGenericArguments().Select(t => t.Name));
        }
        return null;
    }
}
