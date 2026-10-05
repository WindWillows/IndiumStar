// 在托管测试模式下直接运行游戏DLL的模型注册、升级、深克隆与Harmony补丁。
// 不启动游戏循环，不调用UI、伤害指令或存档；不能代替实机战斗验证。
using System.Reflection;
using System.Reflection.Emit;
using IndiumStar.Mechanics;
using System.Text.Json;
using HarmonyLib;
using IndiumStar.Cards;
using IndiumStar.Patches;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Keywords;

TestMode.IsOn = true;
// 关键字注册会启动Godot日志器；此托管探针仅注入定义，不测试注册器或引擎启动。
// 卡牌仍通过实际CardModel.DeepCloneFields复制这些枚举值。
int keywordValue = 100000;
foreach (string name in new[] { "Crystallization", "Extension", "CrystalResistance", "CrystalReversal" }) {
    var definition = new ModKeywordDefinition("IndiumStar", name, "card_keywords", name,
        "card_keywords", name);
    typeof(ModKeywordDefinition).GetProperty("CardKeywordValue")!.SetValue(definition, (CardKeyword)keywordValue++);
    typeof(IndiumStar.Mechanics.IndiumStarKeywords).GetProperty(name)!.SetValue(null, definition);
}
var cardTypes = typeof(MacroDendrite).Assembly.GetTypes().Where(t =>
    typeof(CardModel).IsAssignableFrom(t) && !t.IsAbstract &&
    t.CustomAttributes.Any(a => a.AttributeType.Name == "RegisterCardAttribute"))
    .OrderBy(t => t.Name).ToArray();
ModelDb.Init(cardTypes);
var harmony = new Harmony("IndiumStar.NativeModelSmoke");
harmony.CreateClassProcessor(typeof(CrystallizeMarkClonePatch)).Patch();
var replayTarget = CrystalReplaySeriesPatch.TargetMethod();
var replayInstructions = CrystalReplaySeriesPatch.Transpiler(PatchProcessor.GetOriginalInstructions(replayTarget), replayTarget).ToArray();
if (replayInstructions.Count(instruction => instruction.Calls(typeof(CrystalReplayRules).GetMethod(nameof(CrystalReplayRules.Apply))!)) != 1
    || !replayInstructions.Any(instruction => instruction.opcode == OpCodes.Ldflda
        && instruction.operand is FieldInfo field && field.Name.StartsWith("<playCount>") && field.FieldType == typeof(int)))
    throw new Exception("Native replay injection contract failed");
harmony.CreateClassProcessor(typeof(CrystalReplaySeriesPatch)).Patch();
if (!Harmony.GetPatchInfo(replayTarget)!.Transpilers.Any(patch => patch.owner == harmony.Id))
    throw new Exception("Native replay transpiler not installed");

var rows = new List<object>();
int passed = 0;
foreach (Type type in cardTypes)
{
    CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
    CardModel mutable = canonical.ToMutable();
    bool upgraded = mutable.Type != CardType.Status;
    if (upgraded) { mutable.UpgradeInternal(); mutable.FinalizeUpgradeInternal(); }
    var copy = (CardModel)mutable.MutableClone();
    if (!copy.IsMutable || ReferenceEquals(copy.DynamicVars, mutable.DynamicVars)
        || ReferenceEquals(copy.EnergyCost, mutable.EnergyCost)
        || copy.IsUpgraded != mutable.IsUpgraded || copy.DeckVersion != null)
        throw new Exception("Native clone contract failed: " + type.Name);
    if (type == typeof(BondingTripleBond)
        && (canonical.DynamicVars.Block.BaseValue != 12 || canonical.DynamicVars["CrystalBlock"].BaseValue != 4
            || canonical.DynamicVars["ExtensionBlock"].BaseValue != 6 || copy.DynamicVars.Block.BaseValue != 15
            || copy.DynamicVars["CrystalBlock"].BaseValue != 6 || copy.DynamicVars["ExtensionBlock"].BaseValue != 8))
        throw new Exception("Native updated triple-bond specification failed");
    if (type == typeof(Inversion) && (canonical.DynamicVars.Damage.BaseValue != 7 || copy.DynamicVars.Damage.BaseValue != 7
        || !canonical.Keywords.Contains(CardKeyword.Exhaust) || !copy.Keywords.Contains(CardKeyword.Retain)
        || canonical.Keywords.Contains(CardKeyword.Retain) || mutable is not IExtensionCard
        || canonical.Rarity != CardRarity.Rare || canonical.Type != CardType.Attack))
        throw new Exception("Native updated Inversion damage failed");
    if (type == typeof(DeferredRelease) && (!copy.Keywords.Contains(CardKeyword.Retain)
        || canonical.Keywords.Contains(CardKeyword.Retain)))
        throw new Exception("Native deferred release upgrade keyword failed");
    if (type == typeof(ApplicationNeutron) && (canonical.Type != CardType.Attack || copy.Type != CardType.Attack
        || canonical.Rarity != CardRarity.Basic || canonical.Id.Entry != "APPLICATION_NEUTRON"
        || canonical.DynamicVars.Damage.BaseValue != 10 || copy.DynamicVars.Damage.BaseValue != 14))
        throw new Exception("Native renamed Electron type, stable ID or damage contract failed");
    rows.Add(new { type = type.Name, id = canonical.Id.Entry, upgraded, mutable_clone = "pass",
        card_type = canonical.Type.ToString(), rarity = canonical.Rarity.ToString() });
    passed++;
}
var macro = (MacroDendrite)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(MacroDendrite))).MutableClone();
var coin = (CrystalCoin)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(CrystalCoin))).MutableClone();
var decay = typeof(CrystalCoin).GetProperty("PlaysThisRun", BindingFlags.Instance | BindingFlags.NonPublic)!;
decay.SetValue(coin, 4);
var coinCopy = (CrystalCoin)coin.MutableClone();
if ((int)decay.GetValue(coinCopy)! != 4) throw new Exception("Native coin clone did not inherit decay");
decay.SetValue(coinCopy, 5);
if ((int)decay.GetValue(coin)! != 4) throw new Exception("Native coin clone decay not independent");
macro.DynamicVars.Damage.BaseValue += 2;
var macroCopy = (MacroDendrite)macro.MutableClone();
macroCopy.DynamicVars.Damage.BaseValue++;
if (macro.DynamicVars.Damage.IntValue != 5 || macroCopy.DynamicVars.Damage.IntValue != 6)
    throw new Exception("Native macro growth independence failed");
ApplicationCoPower.MarkForCrystallize(macro, 2);
ApplicationUCrystallizePower.MarkForCrystallizeU(macro, 1);
var markedCopy = (CardModel)macro.MutableClone();
int Mark(Type power, string field) => ((AttachedState<CardModel, int>)power.GetField(field,
    BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).GetValueOrDefault(markedCopy, 0);
if (Mark(typeof(ApplicationCoPower), "CrystallizeMarks") != 2
    || Mark(typeof(ApplicationUCrystallizePower), "CrystallizeUMarks") != 1)
    throw new Exception("Native Harmony copy-mark contract failed");
var replayPlay = new CardPlay {
    Card = macro, Player = null!, Target = null, ResultPile = PileType.Discard,
    Resources = default, IsAutoPlay = false, PlayIndex = 0, PlayCount = 2
};
CrystalReplayRules.Request(macro, replayPlay, 1);
int nativeSeriesCount = 2;
CrystalReplayRules.Apply(macro, replayPlay, ref nativeSeriesCount);
CrystalReplayRules.Apply(macro, replayPlay, ref nativeSeriesCount);
if (nativeSeriesCount != 3 || replayPlay.PlayCount != 3 || replayPlay.IsLastInSeries)
    throw new Exception("Native CardPlay init setter / replay consumption failed");
harmony.UnpatchAll(harmony.Id);
Console.WriteLine($"PASS native model smoke: {passed} registrations/upgrades/deep clones; macro independence; Co/U Harmony marks; native replay transpiler and CardPlay metadata");
File.WriteAllText(args[0], JsonSerializer.Serialize(new {
    scope = "Actual game DLL managed TestMode: model constructors/upgrades/MutableClone/Harmony; keyword definitions seeded without registry startup; no live combat, UI, CreateClone card-scope path, or save test",
    generated_at_utc = DateTime.UtcNow, passed, macro_independence = "pass", co_u_clone_marks = "pass",
    coin_decay_clone = "pass; actual SavedAttachedState and patched MutableClone; no save roundtrip",
    replay_patch = new { status = "pass", target = replayTarget.DeclaringType!.FullName,
        state_machine_is_value_type = replayTarget.DeclaringType.IsValueType,
        injection = "one Apply call before main ExecutionFinished, native loop field by reference",
        native_card_play_setter_and_idempotent_consumption = "pass",
        scope = "actual Harmony patch application and helper metadata; no live OnPlayWrapper combat invocation" }, cards = rows
}, new JsonSerializerOptions { WriteIndented = true }));
