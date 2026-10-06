using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace IndiumStar.Patches;

/// <summary>在本体OnPlay完成、ExecutionFinished/附魔/侵蚀/AfterCardPlayed之前延长系列。</summary>
[HarmonyPatch]
public static class CrystalReplaySeriesPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        MethodInfo wrapper = typeof(CardModel).GetMethod("OnPlayWrapper",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw Unsupported("missing OnPlayWrapper");
        Type stateMachine = wrapper.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw Unsupported("OnPlayWrapper is not an async state machine");
        return stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw Unsupported("missing MoveNext");
    }

    [HarmonyTranspiler]
    // 先读取本体IL，再让其他模组包装OnPlay/结束通知。否则精确契约会误读被替换的调用。
    [HarmonyPriority(Priority.First)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        Type stateMachine = __originalMethod.DeclaringType!;
        FieldInfo cardField = Field(stateMachine, "<>4__this", typeof(CardModel));
        FieldInfo countField = Field(stateMachine, "<playCount>", typeof(int));
        FieldInfo playField = Field(stateMachine, "<cardPlay>", typeof(CardPlay));
        MethodInfo finished = typeof(AbstractModel).GetMethod(nameof(AbstractModel.InvokeExecutionFinished))!;
        MethodInfo onPlay = typeof(CardModel).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int anchor = code.FindIndex(instruction => instruction.Calls(finished));
        int main = code.FindIndex(instruction => instruction.Calls(onPlay));
        // 检查真正的主效果完成位置，避免游戏更新后静默挂到附魔或不同循环上。
        if (anchor < 0 || main < 0 || main >= anchor
            || !code.Take(anchor).Any(instruction => instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, countField))
            || !code.Take(main).Any(instruction => instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, playField))
            || !code.Skip(main + 1).Take(anchor - main - 1).Any(instruction =>
                instruction.operand is MethodInfo method && method.Name == "GetResult"
                && method.DeclaringType == typeof(TaskAwaiter))
            || !code.Skip(anchor + 1).Any(instruction => instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, countField)))
            throw Unsupported("native series loop / awaited OnPlay completion contract changed");

        var first = new CodeInstruction(OpCodes.Ldarg_0);
        first.labels.AddRange(code[anchor].labels);
        first.blocks.AddRange(code[anchor].blocks);
        code[anchor].labels.Clear();
        code[anchor].blocks.Clear();
        code.InsertRange(anchor, new[] {
            first, new CodeInstruction(OpCodes.Ldfld, cardField),
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, playField),
            // 必须取正在运行的状态机字段地址；对结构体装箱后SetValue只会修改副本。
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldflda, countField),
            new CodeInstruction(OpCodes.Call, typeof(CrystalReplayRules).GetMethod(nameof(CrystalReplayRules.Apply))!)
        });
        return code;
    }

    private static FieldInfo Field(Type stateMachine, string prefix, Type type)
    {
        FieldInfo[] matches = stateMachine.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == type && field.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw Unsupported("captured field " + prefix);
    }

    private static InvalidOperationException Unsupported(string reason) =>
        new("IndiumStar crystal replay is incompatible with this CardModel.OnPlayWrapper: " + reason);
}
