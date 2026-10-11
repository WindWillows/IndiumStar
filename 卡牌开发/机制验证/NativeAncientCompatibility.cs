// 仅验证实际游戏 DLL 的尘封魔典设置与 RitsuLib 对话解析，不启动游戏或改写跑局存档。
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using IndiumStar.CardPools;
using IndiumStar.Characters;
using IndiumStar.Patches;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Localization;

TestMode.IsOn = true;
const string characterEntry = "INDIUM_STAR_CHARACTER_INDIUM_STAR_CHARACTER";
string[] ancientEntries = ["DARV", "NEOW", "NONUPEIPE", "OROBAS", "PAEL", "TANX", "TEZCATARA", "THE_ARCHITECT", "VAKUU"];
string modDirectory = Path.GetFullPath(args[0]);
string baseLocalizationDirectory = Path.GetFullPath(args[1]);
string outputPath = Path.GetFullPath(args[2]);
var loc = (LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
typeof(LocManager).GetProperty(nameof(LocManager.Instance))!.SetValue(null, loc);
typeof(LocManager).GetProperty(nameof(LocManager.CultureInfo))!.SetValue(loc, CultureInfo.InvariantCulture);
typeof(LocManager).GetField("_smartFormatter", BindingFlags.Static | BindingFlags.NonPublic)!
    .SetValue(null, SmartFormat.Smart.CreateDefaultSmartFormat());

void LoadLanguage(string language)
{
    var tables = new Dictionary<string, LocTable>();
    foreach (string path in Directory.GetFiles(Path.Combine(baseLocalizationDirectory, language), "*.json"))
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        string name = Path.GetFileNameWithoutExtension(path);
        tables[name] = new LocTable(name, data);
    }
    foreach (string path in Directory.GetFiles(Path.Combine(modDirectory, "IndiumStar", "localization", language), "*.json"))
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        string name = Path.GetFileNameWithoutExtension(path);
        if (tables.TryGetValue(name, out var table)) table.MergeWith(data);
        else tables[name] = new LocTable(name, data);
    }
    typeof(LocManager).GetField("_tables", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(loc, tables);
    typeof(LocManager).GetProperty(nameof(LocManager.Language))!.SetValue(loc, language);
}

int dialoguesChecked = 0;
foreach (string language in new[] { "eng", "zhs" })
{
    LoadLanguage(language);
    foreach (string ancient in ancientEntries)
    {
        var dialogues = AncientDialogueLocalization.GetDialoguesForKey("ancients",
            AncientDialogueLocalization.BaseLocKey(ancient, characterEntry));
        if (dialogues.Count != 1 || dialogues[0].Lines.Count != 2)
            throw new Exception("Native dialogue scan failed: " + language + "/" + ancient + "; count=" + dialogues.Count);
        var set = new AncientDialogueSet {
            FirstVisitEverDialogue = null,
            CharacterDialogues = new() { [characterEntry] = dialogues },
            AgnosticDialogues = Array.Empty<AncientDialogue>()
        };
        set.PopulateLocKeys(ancient);
        if (!dialogues[0].IsRepeating)
            throw new Exception("Native repeating metadata failed after PopulateLocKeys: " + language + "/" + ancient);
        foreach (int visits in new[] { 0, 1, 2, 4, 100 })
            foreach (bool allowAny in new[] { false, true })
                if (!set.GetValidDialogues(new ModelId("character", characterEntry), visits, visits + 1, allowAny).Any())
                    throw new Exception("Native dialogue pool empty: " + language + "/" + ancient + "/" + visits);
        var lines = dialogues[0].Lines;
        if (lines[0].Speaker != AncientDialogueSpeaker.Ancient || lines[1].Speaker != AncientDialogueSpeaker.Character
            || lines.Any(line => line.LineText == null || string.IsNullOrWhiteSpace(line.LineText.GetRawText()))
            || lines[0].NextButtonText == null || string.IsNullOrWhiteSpace(lines[0].NextButtonText!.GetRawText())
            || lines[1].NextButtonText != null)
            throw new Exception("Native speaker/text/next-button contract failed: " + language + "/" + ancient);
        // 建筑师的攻击者标记由RitsuLib按原版流程补齐，沿用其默认结束动画。
        if (ancient == "THE_ARCHITECT" && (!Enum.IsDefined(dialogues[0].StartAttackers) || !Enum.IsDefined(dialogues[0].EndAttackers)))
            throw new Exception("Native Architect attacker metadata is invalid");
        dialoguesChecked++;
    }
}
Console.WriteLine("PASS native ancient dialogue parsing and visit selection: " + dialoguesChecked + " sets");

LoadLanguage("eng");
ModelDb.Init([
    typeof(IndiumStarCharacter), typeof(IndiumStarCardPool), typeof(IndiumStarRelicPool), typeof(IndiumStarPotionPool),
    typeof(Ironclad), typeof(IroncladCardPool), typeof(DustyTome), typeof(Corruption),
    typeof(Bash), typeof(Break), typeof(Neutralize), typeof(Suppress), typeof(Unleash), typeof(Protector),
    typeof(FallingStar), typeof(MeteorShower), typeof(Dualcast), typeof(Quadcast)
]);
var currentPool = ModelDb.CardPool<IndiumStarCardPool>();
var cardCache = typeof(CardPoolModel).GetField("_allCards", BindingFlags.Instance | BindingFlags.NonPublic)!;
cardCache.SetValue(currentPool, Array.Empty<CardModel>());
cardCache.SetValue(ModelDb.CardPool<IroncladCardPool>(), new CardModel[] { ModelDb.Card<Corruption>() });
// UnlockState 的静态全解锁项会查询遭遇；本探针未注册章节与遭遇，只注入空缓存。
typeof(ModelDb).GetField("_allEncounters", BindingFlags.Static | BindingFlags.NonPublic)!
    .SetValue(null, Array.Empty<EncounterModel>());

Player MakePlayer(CharacterModel character)
{
    var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
    void BackingField(string property, object value) => typeof(Player)
        .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, value);
    BackingField(nameof(Player.Character), character);
    BackingField(nameof(Player.UnlockState), UnlockState.none);
    typeof(Player).GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, NullRunState.Instance);
    typeof(Player).GetProperty(nameof(Player.PlayerRng))!.SetValue(player, new PlayerRngSet(0));
    return player;
}

var owner = MakePlayer(ModelDb.Character<IndiumStarCharacter>());
bool reproducedEmptyPool = false;
try { ((DustyTome)ModelDb.Relic<DustyTome>().ToMutable()).SetupForPlayer(owner); }
catch (NullReferenceException) { reproducedEmptyPool = true; }
if (!reproducedEmptyPool) throw new Exception("Expected empty-pool native DustyTome failure was not reproduced");

var harmony = new Harmony("IndiumStar.NativeAncientCompatibility");
harmony.CreateClassProcessor(typeof(AncientDustyTomeFallbackPatch)).Patch();
var target = typeof(DustyTome).GetMethod(nameof(DustyTome.SetupForPlayer))!;
if (!Harmony.GetPatchInfo(target)!.Prefixes.Any(patch => patch.owner == harmony.Id && patch.priority == Priority.First))
    throw new Exception("DustyTome prefix or priority was not installed");
var fixedTome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
fixedTome.SetupForPlayer(owner);
if (fixedTome.AncientCard != ModelDb.Card<Corruption>().Id)
    throw new Exception("Patched native DustyTome did not receive fallback reward");

var untouchedTome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
if (!AncientDustyTomeFallbackPatch.Prefix(untouchedTome, MakePlayer(ModelDb.Character<Ironclad>())) || untouchedTome.AncientCard != null)
    throw new Exception("Fallback altered a base-game character");
cardCache.SetValue(currentPool, new CardModel[] { ModelDb.Card<Corruption>() });
var futureTome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
if (!AncientDustyTomeFallbackPatch.Prefix(futureTome, owner) || futureTome.AncientCard != null)
    throw new Exception("Fallback overrides a nonempty native Ancient-card pool");
Console.WriteLine("PASS native empty-pool reproduction, patched DustyTome setup, character scope and future-pool guard");

File.WriteAllText(outputPath, JsonSerializer.Serialize(new {
    version = "0.1.5", ancient_dialogue_sets = dialoguesChecked,
    dialogue_languages = new[] { "eng", "zhs" }, ancient_entries = ancientEntries,
    visits_checked = new[] { 0, 1, 2, 4, 100 }, empty_tome_pool_reproduced = true,
    native_patched_tome_setup = "pass", fallback_card = "CORRUPTION",
    character_and_nonempty_pool_guards = "pass",
    scope = "Actual game DLL and RitsuLib 0.6.2, managed TestMode with seeded localization/model/pool caches. No Godot UI, full mod startup, real event selection or live run save."
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");
