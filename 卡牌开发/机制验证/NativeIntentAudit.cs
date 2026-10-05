// 读取当前游戏DLL的真实动作IL，生成预检清单；不执行动作、不加载游戏或修改存档。
using System.Reflection;
using System.Text.Json;
using IndiumStar.Mechanics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

// 使用实际Rng对象验证借用快照的构造契约，不用测试替身。
var originalRng = new Rng(34773);
originalRng.NextInt();
string rngBefore = JsonSerializer.Serialize(originalRng.ToSerializable());
var borrowedRng = new Rng(originalRng.ToSerializable());
var comparisonRng = new Rng(originalRng.ToSerializable());
for (int i = 0; i < 16; i++)
    if (borrowedRng.NextInt() != comparisonRng.NextInt()) throw new Exception("Native RNG snapshot diverged");
if (rngBefore != JsonSerializer.Serialize(originalRng.ToSerializable())) throw new Exception("Native RNG snapshot changed original");
Console.WriteLine("PASS native RNG snapshot: deterministic sequence, original unchanged");

var rows = new List<object>();
int accepted = 0, rejected = 0;
foreach (Type type in typeof(MonsterModel).Assembly.GetTypes().Where(t =>
    typeof(MonsterModel).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested).OrderBy(t => t.Name))
{
    foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        .Where(m => m.ReturnType == typeof(Task) && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType.ToString().Contains("Entities.Creatures.Creature")))
    {
        bool supported = BorrowedIntentCompatibility.CanExecuteAction(method, out string? reason);
        if (supported) accepted++; else rejected++;
        rows.Add(new { monster = type.Name, action = method.Name, passes_preflight = supported, reason });
    }
}
void Contract(string typeName, string methodName, bool expected) {
    Type type = typeof(MonsterModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Monsters." + typeName, true)!;
    MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
    bool actual = BorrowedIntentCompatibility.CanExecuteAction(method, out string? reason);
    if (actual != expected) throw new Exception($"{typeName}.{methodName}: {actual}, {reason}");
    Console.WriteLine($"PASS native action preflight {typeName}.{methodName}: {actual}");
}
Contract("DampCultist", "IncantationMove", true);
Contract("DampCultist", "DarkStrikeMove", true);
Contract("AxeRubyRaider", "SwingMove", true);
Contract("Fogmog", "IllusionMove", false);
Contract("GasBomb", "ExplodeMove", false);
string json = JsonSerializer.Serialize(new {
    scope = "Native game DLL method IL preflight only; candidate action methods, not an in-game execution test or guaranteed intent support",
    generated_at_utc = DateTime.UtcNow,
    game_dll_sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(MonsterModel).Assembly.Location))),
    accepted, rejected, candidates = rows
}, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(args[0], json);
Console.WriteLine($"Native action preflight: {accepted} accepted / {rejected} rejected; {args[0]}");
