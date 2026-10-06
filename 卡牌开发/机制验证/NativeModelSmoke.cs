// 在托管测试模式下直接运行游戏DLL的模型注册、升级、深克隆与Harmony补丁。
// 不启动游戏循环，不调用UI或伤害指令；只检查单卡属性JSON恢复，不能代替完整战斗存档。
using System.Reflection;
using System.Reflection.Emit;
using IndiumStar.Mechanics;
using System.Text.Json;
using HarmonyLib;
using IndiumStar.Cards;
using IndiumStar.Patches;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
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
var newPowerTypes = new[] { typeof(TunnelingPower), typeof(CrystalMarrowPower) };
ModelDb.Init(cardTypes.Concat(newPowerTypes).ToArray());
// 全局Init会调用Godot日志器；此托管探针仅调用实际属性扫描并设置缓存就绪标志。
// 不测试真实初始化、模组加载、网络ID协商或完整战斗存档。
var propertyCacheType = typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache);
foreach (Type cardType in cardTypes.Concat(newPowerTypes))
    MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.CacheSavedPropertiesForTypeDebug(cardType);
propertyCacheType.GetField("_initialized", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, true);
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
    bool upgraded = mutable.Type != CardType.Status && mutable.IsUpgradable;
    if (upgraded) { mutable.UpgradeInternal(); mutable.FinalizeUpgradeInternal(); }
    var copy = (CardModel)mutable.MutableClone();
    if (!copy.IsMutable || ReferenceEquals(copy.DynamicVars, mutable.DynamicVars)
        || ReferenceEquals(copy.EnergyCost, mutable.EnergyCost)
        || copy.IsUpgraded != mutable.IsUpgraded || copy.DeckVersion != null)
        throw new Exception("Native clone contract failed: " + type.Name);
    if (type == typeof(BondingTripleBond)
        && (canonical.DynamicVars.Block.BaseValue != 12 || canonical.DynamicVars["CrystalBlock"].BaseValue != 3
            || canonical.DynamicVars["ExtensionBlock"].BaseValue != 5 || copy.DynamicVars.Block.BaseValue != 15
            || copy.DynamicVars["CrystalBlock"].BaseValue != 5 || copy.DynamicVars["ExtensionBlock"].BaseValue != 7))
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
        || canonical.DynamicVars.Damage.BaseValue != 11 || copy.DynamicVars.Damage.BaseValue != 15
        || canonical.EnergyCost.GetWithModifiers(CostModifiers.All) != 1 || copy.EnergyCost.GetWithModifiers(CostModifiers.All) != 1))
        throw new Exception("Native renamed Electron type, stable ID or damage contract failed");
    if (type == typeof(DerivedApplicationNeutron) && (canonical.Type != CardType.Skill
        || canonical.Rarity != CardRarity.Token || canonical.Id.Entry != "DERIVED_APPLICATION_NEUTRON"
        || canonical.EnergyCost.GetWithModifiers(CostModifiers.All) != 1
        || copy.EnergyCost.GetWithModifiers(CostModifiers.All) != 0
        || canonical.Keywords.Contains(CardKeyword.Exhaust) || canonical.Keywords.Contains(CardKeyword.Retain)
        || type.CustomAttributes.Any(a => a.AttributeType.Name == "RegisterCharacterStarterCardAttribute")))
        throw new Exception("Native derived Neutron metadata/upgrade contract failed");
    if (type == typeof(CrystalPunishment) && (canonical.Type != CardType.Attack
        || canonical.Rarity != CardRarity.Rare || canonical.TargetType != TargetType.AnyEnemy
        || canonical.EnergyCost.GetWithModifiers(CostModifiers.All) != 1
        || copy.EnergyCost.GetWithModifiers(CostModifiers.All) != 1
        || canonical.DynamicVars.Damage.BaseValue != 9 || copy.DynamicVars.Damage.BaseValue != 9
        || canonical.DynamicVars["DoomPower"].BaseValue != 3 || copy.DynamicVars["DoomPower"].BaseValue != 4))
        throw new Exception("Native Crystal Punishment upgrade contract failed");
    if (type == typeof(Tunneling) && (!canonical.EnergyCost.CostsX || !copy.EnergyCost.CostsX
        || canonical.Type != CardType.Skill || canonical.Rarity != CardRarity.Rare
        || !canonical.Keywords.Contains(CardKeyword.Exhaust)
        || canonical.DynamicVars["ExtraCards"].IntValue != 0 || copy.DynamicVars["ExtraCards"].IntValue != 1))
        throw new Exception("Native Tunneling X/upgrade contract failed");
    if (type == typeof(CrystalMarrow) && (!canonical.EnergyCost.CostsX || !copy.EnergyCost.CostsX
        || canonical.Type != CardType.Skill || canonical.Rarity != CardRarity.Rare
        || canonical is not IExtensionCard || !canonical.Keywords.Contains(CardKeyword.Exhaust) || !copy.Keywords.Contains(CardKeyword.Exhaust)
        || canonical.DynamicVars["ExtraDraw"].IntValue != 0 || copy.DynamicVars["ExtraDraw"].IntValue != 1))
        throw new Exception("Native Crystal Marrow X/upgrade contract failed");
    if (type == typeof(CrystalCoin) && (!canonical.Keywords.Contains(CardKeyword.Ethereal)
        || !canonical.Keywords.Contains(CardKeyword.Exhaust) || !copy.Keywords.Contains(CardKeyword.Exhaust)))
        throw new Exception("Native Crystal Coin keywords failed");
    if (type == typeof(CrystallizationNb) && canonical.DynamicVars["VigorPower"].BaseValue != 3)
        throw new Exception("Native Crystallization Nb Vigor failed");
    if (type == typeof(ShatterCrystalRain) && (canonical.DynamicVars.Damage.BaseValue != 2
        || copy.DynamicVars.Damage.BaseValue != 2 || canonical.DynamicVars.Repeat.IntValue != 3 || copy.DynamicVars.Repeat.IntValue != 4))
        throw new Exception("Native Shatter Crystal Rain damage/repeat failed");
    if (type == typeof(AbyssalRiver) && (canonical.DynamicVars.Damage.BaseValue != 7
        || canonical.MaxUpgradeLevel != 0 || mutable.IsUpgradable || copy.IsUpgraded
        || canonical.EnergyCost.GetWithModifiers(CostModifiers.All) != 0 || canonical.Rarity != CardRarity.Rare || canonical.Type != CardType.Attack))
        throw new Exception("Native Abyssal River metadata failed");
    if (type == typeof(StellarSea)) {
        for (int i = 1; i < 5; i++) { mutable.UpgradeInternal(); mutable.FinalizeUpgradeInternal(); }
        var save = mutable.ToSerializable();
        var restored = CardModel.FromSerializable(save);
        if (canonical.MaxUpgradeLevel != int.MaxValue || canonical.DynamicVars.Damage.BaseValue != 10
            || mutable.CurrentUpgradeLevel != 5 || mutable.DynamicVars.Damage.BaseValue != 20
            || restored.CurrentUpgradeLevel != 5 || restored.DynamicVars.Damage.BaseValue != 20
            || restored.EnergyCost.GetWithModifiers(CostModifiers.All) != 1)
            throw new Exception("Native Stellar Sea repeated upgrade / SerializableCard restoration failed");
    }
    if (mutable is CrystallizationCard crystal) {
        crystal.AddCrystalDoom(3); crystal.AddCrystalDoom(4);
        var crystalCopy = (CrystallizationCard)crystal.MutableClone();
        crystalCopy.AddCrystalDoom(4);
        if (crystal.CrystalDoomAmount != 7 || crystalCopy.CrystalDoomAmount != 11
            || crystal.TargetType != TargetType.AnyEnemy || canonical.TargetType != TargetType.Self
            || ((CrystallizationCard)canonical).CrystalDoomAmount != 0)
            throw new Exception("Native Crystal Doom clone/canonical isolation failed: " + type.Name);
        var properties = SavedProperties.From(crystal)!;
        if (properties.ints?.SingleOrDefault(p => p.name == nameof(CrystallizationCard.CrystalDoomAmount)).value != 7)
            throw new Exception("Native inherited Crystal Doom SavedProperty missing: " + type.Name);
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        var savedJson = JsonSerializer.Serialize(properties, jsonOptions);
        var restoredProperties = JsonSerializer.Deserialize<SavedProperties>(savedJson, jsonOptions)!;
        var restored = (CrystallizationCard)canonical.ToMutable();
        restoredProperties.Fill(restored);
        if (restored.CrystalDoomAmount != 7 || restored.TargetType != TargetType.AnyEnemy)
            throw new Exception("Native Crystal Doom JSON property roundtrip failed: " + type.Name);
    }
    rows.Add(new { type = type.Name, id = canonical.Id.Entry, upgraded, mutable_clone = "pass",
        card_type = canonical.Type.ToString(), rarity = canonical.Rarity.ToString() });
    passed++;
}
var macro = (MacroDendrite)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(MacroDendrite))).MutableClone();
var formatter = new SmartFormat.SmartFormatter();
formatter.AddExtensions(new SmartFormat.Extensions.DictionarySource(), new SmartFormat.Extensions.DefaultSource());
formatter.AddExtensions(new SmartFormat.Extensions.ConditionalFormatter(), new SmartFormat.Extensions.DefaultFormatter());
int doomDescriptions = 0;
foreach (string language in new[] { "eng", "zhs" }) {
    using var localization = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1], "IndiumStar", "localization", language, "cards.json")));
    foreach (Type crystalType in cardTypes.Where(t => typeof(CrystallizationCard).IsAssignableFrom(t))) {
        string description = localization.RootElement.GetProperty("INDIUM_STAR_CARD_" + ModelDb.GetId(crystalType).Entry + ".description").GetString()!;
        string suffix = description[description.IndexOf("{CrystalDoomActive:cond:", StringComparison.Ordinal)..];
        foreach (bool active in new[] { false, true }) {
            var crystal = (CrystallizationCard)ModelDb.GetById<CardModel>(ModelDb.GetId(crystalType)).ToMutable();
            if (active) crystal.AddCrystalDoom(7);
            var argsForDescription = new MegaCrit.Sts2.Core.Localization.LocString("cards", "probe");
            typeof(CrystallizationCard).GetMethod("AddExtraArgsToDescription", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(crystal, new object[] { argsForDescription });
            var vars = argsForDescription.Variables.ToDictionary(p => p.Key, p => p.Value);
            string text = formatter.Format(suffix, vars);
            if (active ? !text.Contains("7") || !text.Contains(language == "eng" ? "Doom" : "消亡") : text != "")
                throw new Exception("Native crystal conditional description failed: " + crystalType.Name + " / " + language);
        }
        doomDescriptions++;
    }
}
var neutronFilter = typeof(DerivedApplicationNeutron).GetMethod("CanAutoPlayFromDiscard",
    BindingFlags.Static | BindingFlags.NonPublic)!;
bool Eligible(CardModel card) => (bool)neutronFilter.Invoke(null, new object[] { card })!;
var electronCandidate = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(ApplicationNeutron))).ToMutable();
if (!Eligible(electronCandidate)) throw new Exception("Native Neutron excluded 1-cost card");
electronCandidate.EnergyCost.SetThisCombat(2);
if (Eligible(electronCandidate)) throw new Exception("Native Neutron ignored increased current cost");
electronCandidate.EnergyCost.SetThisCombat(0);
if (!Eligible(electronCandidate)) throw new Exception("Native Neutron ignored reduced current cost");
electronCandidate.AddKeyword(CardKeyword.Unplayable);
if (Eligible(electronCandidate)) throw new Exception("Native Neutron included unplayable card");
var xCandidate = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(ThermobaricField))).ToMutable();
if (Eligible(xCandidate)) throw new Exception("Native Neutron included X-cost card");
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
// Check only the new powers' scalar/array persistence, independently of a full combat save.
var tunnelState = (TunnelingPower)ModelDb.Power<TunnelingPower>().ToMutable();
tunnelState.PendingLimits = [0, 3]; tunnelState.ActiveTurn = 7;
tunnelState.CardLimit = 4; tunnelState.CardsPlayed = 2;
var tunnelCopy = (TunnelingPower)tunnelState.MutableClone();
tunnelCopy.PendingLimits[0] = 9;
if (tunnelState.PendingLimits[0] != 0) throw new Exception("Native Tunneling queue clone aliases");
var savedTunnel = SavedProperties.From(tunnelState)!;
var restoredTunnel = (TunnelingPower)ModelDb.Power<TunnelingPower>().ToMutable();
JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(savedTunnel,
    new JsonSerializerOptions { IncludeFields = true }), new JsonSerializerOptions { IncludeFields = true })!.Fill(restoredTunnel);
if (!restoredTunnel.PendingLimits.SequenceEqual(new[] { 0, 3 }) || restoredTunnel.ActiveTurn != 7
    || restoredTunnel.CardLimit != 4 || restoredTunnel.CardsPlayed != 2)
    throw new Exception("Native Tunneling state JSON roundtrip failed");
var marrowState = (CrystalMarrowPower)ModelDb.Power<CrystalMarrowPower>().ToMutable();
marrowState.PendingTurns = [8, 9]; marrowState.ActiveTurn = 7;
var marrowCopy = (CrystalMarrowPower)marrowState.MutableClone();
marrowCopy.PendingTurns[0] = 99;
if (marrowState.PendingTurns[0] != 8) throw new Exception("Native Marrow queue clone aliases");
var restoredMarrow = (CrystalMarrowPower)ModelDb.Power<CrystalMarrowPower>().ToMutable();
JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(marrowState),
    new JsonSerializerOptions { IncludeFields = true }), new JsonSerializerOptions { IncludeFields = true })!.Fill(restoredMarrow);
if (!restoredMarrow.PendingTurns.SequenceEqual(new[] { 8, 9 }) || restoredMarrow.ActiveTurn != 7)
    throw new Exception("Native Crystal Marrow state JSON roundtrip failed");
harmony.UnpatchAll(harmony.Id);
Console.WriteLine($"PASS native model smoke: {passed} registrations/upgrades/deep clones; macro independence; Co/U Harmony marks; native replay transpiler and CardPlay metadata");
File.WriteAllText(args[0], JsonSerializer.Serialize(new {
    scope = "Actual game DLL managed TestMode: model constructors/upgrades/MutableClone/Harmony, per-card SavedProperties JSON and conditional description formatting; keyword and serialization-cache readiness seeded without full registry/startup; no live combat, Godot UI, CreateClone card-scope path, full combat save or multiplayer negotiation",
    generated_at_utc = DateTime.UtcNow, passed, macro_independence = "pass", co_u_clone_marks = "pass",
    remaining_x_cards = "pass; native X metadata/upgrade; two powers' independent array clone and SavedProperties JSON -> Fill; no native live turn loop",
    coin_decay_clone = "pass; actual SavedAttachedState and patched MutableClone; no save roundtrip",
    derived_neutron_cost_filter = "pass; actual local cost modifiers, increased/reduced current cost, Unplayable and X exclusion; global combat hooks not invoked",
    crystal_doom_state = "pass; all 6 crystal variants inherit SavedProperty; actual SavedProperties -> JSON -> Fill; target change, canonical isolation and independent MutableClone; no full combat/run save",
    crystal_doom_descriptions = new { passed = doomDescriptions, scope = "actual card AddExtraArgsToDescription and game SmartFormat ConditionalFormatter, 6 variants x 2 languages, inactive hidden / active numeric text; no Godot card UI" },
    replay_patch = new { status = "pass", target = replayTarget.DeclaringType!.FullName,
        state_machine_is_value_type = replayTarget.DeclaringType.IsValueType,
        injection = "one Apply call before main ExecutionFinished, native loop field by reference",
        native_card_play_setter_and_idempotent_consumption = "pass",
        scope = "actual Harmony patch application and helper metadata; no live OnPlayWrapper combat invocation" }, cards = rows
}, new JsonSerializerOptions { WriteIndented = true }));
