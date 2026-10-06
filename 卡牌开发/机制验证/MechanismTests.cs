using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using IndiumStar.Patches;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

var tests = new List<(string Name, Func<Task> Run)>();
new Harmony("IndiumStar.RegressionTests").PatchAll(typeof(VigorRetentionPatch).Assembly);
IndiumStarKeywords.RegisterAll();
void Test(string name, Func<Task> run) => tests.Add((name, run));
void Sync(string name, Action run) => Test(name, () => { run(); return Task.CompletedTask; });
void Check(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
void LogIs(Rig rig, params string[] expected) => Check(rig.Log.SequenceEqual(expected), string.Join(",", rig.Log));

Sync("晶化身份不依赖名称或语言", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>();
    foreach (string title in new[] { "晶化Mg", "Crystallization Mg", "水晶", "", "Renamed" }) {
        c.Title = title; Check(CrystallizationRules.IsCrystallizationCard(c));
    }
    r.Card.Title = "晶化 application";
    Check(!CrystallizationRules.IsCrystallizationCard(r.Card));
    Check(!ApplicationRules.IsApplicationCard(r.Card));
});
Sync("通用基类不会被当成运用牌族", () => {
    var r = new Rig(); var application = new MarkedApplication { Owner = r.Player, CombatState = r.State };
    application.Title = "Renamed"; Check(ApplicationRules.IsApplicationCard(application));
    Check(!ApplicationRules.IsApplicationCard(r.Card));
});
Sync("晶化基类提供默认属性", () => {
    var c = new CrystalA(); Check(c.BaseCost == 0 && c.Type == CardType.Status);
    Check(c.CanonicalKeywords.Take(2).SequenceEqual(new[] { CardKeyword.Retain, CardKeyword.Exhaust }));
    Check(c.CanonicalKeywords.Contains(IndiumStarKeywords.CrystallizationKeyword));
});
Sync("四个规则词条独立且重复初始化不替换定义", () => {
    var definitions = new[] { IndiumStarKeywords.Crystallization, IndiumStarKeywords.Extension,
        IndiumStarKeywords.CrystalResistance, IndiumStarKeywords.CrystalReversal };
    Check(definitions.Select(d => d.CardKeywordValue).Distinct().Count() == 4);
    Check(definitions.All(d => !Enum.IsDefined(d.CardKeywordValue)));
    IndiumStarKeywords.RegisterAll();
    Check(ReferenceEquals(definitions[0], IndiumStarKeywords.Crystallization));
    Check(ReferenceEquals(definitions[3], IndiumStarKeywords.CrystalReversal));
});
foreach (var branch in new[] { CrystalBranch.None, CrystalBranch.Resistance, CrystalBranch.Reversal, CrystalBranch.Both }) {
    var captured = branch;
    Sync($"卡牌词条与声明分支一致：{captured}", () => {
        var keywords = new ProbeCard(captured, []).CanonicalKeywords.ToArray();
        Check(keywords.Contains(IndiumStarKeywords.CrystalResistanceKeyword) == captured.HasFlag(CrystalBranch.Resistance));
        Check(keywords.Contains(IndiumStarKeywords.CrystalReversalKeyword) == captured.HasFlag(CrystalBranch.Reversal));
        Check(!keywords.Contains(IndiumStarKeywords.ExtensionKeyword));
    });
}
Sync("延拓牌族具有延拓词条", () => {
    Check(new MarkedExtension().CanonicalKeywords.Contains(IndiumStarKeywords.ExtensionKeyword));
});
Sync("晶化Pb保留公共晶化属性并额外具有虚无", () => {
    var card = new CrystallizationPb(); var keywords = card.CanonicalKeywords.ToArray();
    Check(CrystallizationRules.IsCrystallizationCard(card));
    Check(keywords.Contains(CardKeyword.Exhaust) && keywords.Contains(CardKeyword.Ethereal));
    Check(keywords.Contains(IndiumStarKeywords.CrystallizationKeyword) && keywords.Contains(CardKeyword.Retain));
});
foreach (string language in new[] { "eng", "zhs" }) {
    var captured = language;
    Sync($"注册词条在{captured}本地化中均可查到标题与描述", () => {
        Check(args.Length >= 2, "ProjectDir required");
        string path = System.IO.Path.Combine(args[1], "IndiumStar", "localization", captured, "card_keywords.json");
        using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
        foreach (var definition in new[] { IndiumStarKeywords.Crystallization, IndiumStarKeywords.Extension,
            IndiumStarKeywords.CrystalResistance, IndiumStarKeywords.CrystalReversal }) {
            foreach (string suffix in new[] { ".title", ".description" }) {
                Check(document.RootElement.TryGetProperty(definition.Id + suffix, out var value), definition.Id + suffix);
                Check(!string.IsNullOrWhiteSpace(value.GetString()));
            }
        }
    });
}
Sync("尚未手动打出时额度可用", () => { var r = new Rig(); Check(r.Crystal<CrystalA>().CanPlayManually); });
Sync("自动打出多张后仍有一次手动额度", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>();
    r.Record(c, auto: true); r.Record(r.Crystal<CrystalB>(), auto: true);
    Check(c.CanPlayManually);
});
Sync("不同晶化共用手动额度", () => {
    var r = new Rig(); r.Record(r.Crystal<CrystalA>()); Check(!r.Crystal<CrystalB>().CanPlayManually);
});
Sync("手动之后的自动记录不改变额度", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>(); r.Record(c); r.Record(c, auto: true);
    Check(!c.CanPlayManually);
});
Sync("系列重放不作为新的手动出牌", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>(); r.Record(c, index: 1); Check(c.CanPlayManually);
});
Sync("新回合恢复手动额度", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>(); r.Record(c); r.State.Turn++; Check(c.CanPlayManually);
});
Sync("不同玩家额度独立", () => {
    var r = new Rig(); var other = r.Crystal<CrystalA>(); other.Owner = new Player(); r.Record(other);
    Check(r.Crystal<CrystalB>().CanPlayManually);
});
Sync("另一场战斗历史不占额度", () => {
    var r = new Rig(); var c = r.Crystal<CrystalA>(); r.Record(c); c.CombatState = new FakeCombat(); Check(c.CanPlayManually);
});
Sync("手牌计数只包含稳定标记", () => {
    var r = new Rig(); var a = r.Crystal<CrystalA>(); a.Title = "renamed";
    r.Card.Title = "晶化"; r.Player.PlayerCombatState!.Hand.Cards.AddRange(new CardModel[] { a, r.Crystal<CrystalB>(), r.Card });
    Check(CrystallizationRules.CountCrystallizationInHand(r.Card) == 2);
});
Test("晶化生成普通牌和指定变体", async () => {
    var r = new Rig(); await CrystallizationRules.AddToHand(r.Choice, r.Player, r.State, 2, r.Card);
    await CrystallizationRules.AddToHand<CrystalB>(r.Choice, r.Player, r.State, 1, r.Card);
    var hand = r.Player.PlayerCombatState!.Hand.Cards;
    Check(hand.Count == 3 && hand[0] is Crystallization && hand[1] is Crystallization && hand[2] is CrystalB);
    Check(hand.All(c => c.Owner == r.Player && c.CombatState == r.State));
});
Test("零数量不生成晶化", async () => {
    var r = new Rig(); await CrystallizationRules.AddToHand(r.Choice, r.Player, r.State, 0, r.Card);
    Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 0);
});

foreach (int energy in new[] { 0, 1, 3, -1 }) {
    int captured = energy;
    Test($"基础效果结束后能量{captured}选择正确分支", async () => {
        var r = new Rig(energy: captured); r.Listen(); await r.Play();
        string branch = captured == 0 ? "reversal" : "resistance";
        LogIs(r, "main", branch, "notify:" + branch);
    });
}
Test("扣费后0能量但基础效果返还1能量触发晶抗", async () => {
    var r = new Rig(energy: 0); r.Card.Main = async () => { await Task.Yield(); r.Energy = 1; return null; };
    r.Listen(); await r.Play(); LogIs(r, "main", "resistance", "notify:resistance");
});
Test("基础效果花掉最后能量触发晶逆", async () => {
    var r = new Rig(energy: 1); r.Card.Main = () => { r.Energy = 0; return Task.FromResult<CardModel?>(null); };
    await r.Play(); LogIs(r, "main", "reversal");
});
Test("晶抗花掉最后能量不追加晶逆", async () => {
    var r = new Rig(energy: 1); r.Card.Resistance = _ => { r.Energy = 0; return Task.CompletedTask; };
    r.Listen(); await r.Play(); LogIs(r, "main", "resistance", "notify:resistance");
});
Test("双分支固定顺序并保持能量快照", async () => {
    var r = new Rig(energy: 1); r.Modify((_, _) => CrystalBranch.Both);
    r.Card.Resistance = _ => { r.Energy = 0; return Task.CompletedTask; };
    r.Listen((context, _) => { Check(context.EnergyAfterMainEffect == 1); r.Energy = 9; return Task.CompletedTask; });
    await r.Play(); LogIs(r, "main", "resistance", "notify:resistance", "reversal", "notify:reversal");
});
Test("无分支卡牌不产生空触发", async () => {
    var r = new Rig(CrystalBranch.None); int modifierCalls = 0;
    r.Modify((_, _) => { modifierCalls++; return CrystalBranch.Both; }); r.Listen(); await r.Play();
    LogIs(r, "main"); Check(modifierCalls == 0);
});
Test("双分支规则只执行卡牌实际具备的分支", async () => {
    var r = new Rig(CrystalBranch.Resistance, energy: 0); r.Modify((_, _) => CrystalBranch.Both);
    r.Listen(); await r.Play(); LogIs(r, "main", "resistance", "notify:resistance");
});
Test("未满足唯一分支条件时没有触发", async () => {
    var r = new Rig(CrystalBranch.Resistance, energy: 0); r.Listen(); await r.Play(); LogIs(r, "main");
});
Test("修改规则按本体监听顺序组合", async () => {
    var r = new Rig(); r.Modify((_, _) => CrystalBranch.Both);
    r.Modify((_, current) => current & CrystalBranch.Reversal); await r.Play(); LogIs(r, "main", "reversal");
});
Test("修改规则可抑制全部分支", async () => {
    var r = new Rig(); r.Modify((_, _) => CrystalBranch.None); r.Listen(); await r.Play(); LogIs(r, "main");
});
Test("异步监听完成后才执行第二分支", async () => {
    var r = new Rig(); bool finished = false; r.Modify((_, _) => CrystalBranch.Both);
    r.Listen(async (_, branch) => { if (branch == CrystalBranch.Resistance) { await Task.Yield(); finished = true; } });
    r.Card.Reversal = _ => { Check(finished); return Task.CompletedTask; };
    await r.Play(); Check(finished);
});
Test("分支选择不接受未定义标记", async () => {
    var r = new Rig(); r.Modify((_, _) => (CrystalBranch)128); r.Listen(); await r.Play(); LogIs(r, "main");
});
Test("基础效果中的嵌套出牌完成后再判断能量", async () => {
    var r = new Rig(energy: 0);
    var child = new ProbeCard(CrystalBranch.None, []) { Owner = r.Player, CombatState = r.State,
        Main = () => { r.Energy++; return Task.FromResult<CardModel?>(null); } };
    r.Card.Main = async () => { await child.Play(r.Choice, new CardPlay { Card = child, IsAutoPlay = true }); return null; };
    await r.Play(); LogIs(r, "main", "resistance");
});
Test("基础效果返回值传给两分支及通知", async () => {
    var r = new Rig(); var result = r.Crystal<CrystalA>(); r.Modify((_, _) => CrystalBranch.Both);
    r.Card.Main = () => Task.FromResult<CardModel?>(result);
    r.Card.Resistance = c => { Check(ReferenceEquals(c, result)); return Task.CompletedTask; };
    r.Card.Reversal = c => { Check(ReferenceEquals(c, result)); return Task.CompletedTask; };
    r.Listen((context, _) => { Check(ReferenceEquals(context.MainEffectResult, result)); return Task.CompletedTask; });
    await r.Play();
});
Test("普通本体重放段仍触发晶抗晶逆和通知", async () => {
    var r = new Rig(energy: 0); r.Listen();
    await r.Card.Play(r.Choice, new CardPlay { Card = r.Card, PlayIndex = 1 });
    LogIs(r, "main", "reversal", "notify:reversal");
});
Test("卡牌显式限制时重放段只执行主效果", async () => {
    var r = new Rig(energy: 0); r.Card.SuppressReplayBranches = true; r.Listen();
    await r.Card.Play(r.Choice, new CardPlay { Card = r.Card, PlayIndex = 1 });
    LogIs(r, "main");
});
Sync("主效果前不预测晶逆重放", () => {
    var r = new Rig(energy: 0); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("非晶逆能量不延长出牌系列", () => {
    var r = new Rig(energy: 1); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("公共卡牌基类不会修改其它牌的出牌次数", () => {
    var r = new Rig(energy: 0); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Crystal<CrystalA>(), null, 1) == 1);
});
Sync("主效果前不调用修改规则预测重放", () => {
    var r = new Rig(energy: 1); r.Card.ReplayCount = 1;
    r.Modify((_, _) => CrystalBranch.Both);
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("未声明重放的牌不延长出牌系列", () => {
    var r = new Rig(energy: 0);
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Test("每次完整结算重新读取能量", async () => {
    var r = new Rig(energy: 1); r.Listen(); await r.Play(); r.Energy = 0;
    // 第二次完整结算（新的一次出牌，PlayIndex 回到 0）重新读取能量并重新选择分支。
    await r.Card.Play(r.Choice, new CardPlay { Card = r.Card, PlayIndex = 0 });
    LogIs(r, "main", "resistance", "notify:resistance", "main", "reversal", "notify:reversal");
});
Test("自动出牌同样可以触发已声明分支", async () => {
    var r = new Rig(); r.Listen(); await r.Card.Play(r.Choice, new CardPlay { Card = r.Card, IsAutoPlay = true });
    LogIs(r, "main", "resistance", "notify:resistance");
});
Test("基础效果失败不选择分支或通知", async () => {
    var r = new Rig(); r.Card.Main = () => throw new InvalidOperationException("expected"); r.Listen();
    try { await r.Play(); throw new Exception("must throw"); } catch (InvalidOperationException) { }
    LogIs(r, "main");
});
Test("分支失败不发送完成通知", async () => {
    var r = new Rig(); r.Card.Resistance = _ => throw new InvalidOperationException("expected"); r.Listen();
    try { await r.Play(); throw new Exception("must throw"); } catch (InvalidOperationException) { }
    LogIs(r, "main", "resistance");
});
Test("战斗结束后停止附加规则及通知", async () => {
    var r = new Rig(); r.Modify((_, _) => CrystalBranch.Both); r.Listen(); CombatManager.Instance.IsOverOrEnding = true;
    await r.Play(); LogIs(r, "main", "resistance");
});
Test("监听者只从当前战斗发现", async () => {
    var r = new Rig(); var oldState = r.State; r.Listen();
    r.Card.CombatState = new FakeCombat(); await r.Play(); LogIs(r, "main", "resistance");
});
Test("监听者可以正确过滤适用玩家", async () => {
    var r = new Rig(); var other = new Player(); int count = 0;
    r.Listen((context, _) => { if (context.Card.Owner == other) count++; return Task.CompletedTask; });
    await r.Play(); Check(count == 0);
});

Test("延拓获得走原版能力且保留来源", async () => {
    var r = new Rig(); await ExtensionRules.Gain(r.Choice, r.Player, 2, r.Card);
    Check(ExtensionRules.GetAmount(r.Player) == 2);
    Check(ReferenceEquals(PowerCmd.LastSource, r.Card) && ReferenceEquals(PowerCmd.LastApplier, r.Player.Creature));
});
Test("多次延拓通过同一原版计数能力累加", async () => {
    var r = new Rig(); await ExtensionRules.Gain(r.Choice, r.Player, 2, r.Card); await ExtensionRules.Gain(r.Choice, r.Player, 3, r.Card);
    Check(ExtensionRules.GetAmount(r.Player) == 5 && r.Player.Creature.Powers.Count == 1);
});
Test("零延拓不创建能力", async () => {
    var r = new Rig(); await ExtensionRules.Gain(r.Choice, r.Player, 0, r.Card);
    Check(PowerCmd.ApplyCount == 0 && ExtensionRules.GetAmount(r.Player) == 0);
});
Test("延拓不足阈值时不消耗", async () => {
    var r = new Rig(); await ExtensionRules.Gain(r.Choice, r.Player, 2, r.Card);
    Check(!await ExtensionRules.TryConsumeAll(r.Player, 3) && ExtensionRules.GetAmount(r.Player) == 2);
});
Test("达到阈值时消耗全部延拓", async () => {
    var r = new Rig(); await ExtensionRules.Gain(r.Choice, r.Player, 5, r.Card);
    Check(await ExtensionRules.TryConsumeAll(r.Player, 3)); Check(ExtensionRules.GetAmount(r.Player) == 0);
    Check(!await ExtensionRules.TryConsumeAll(r.Player, 1));
});
Test("清空延拓可重复调用且不影响其他玩家", async () => {
    var r = new Rig(); var other = new Player(); await ExtensionRules.Gain(r.Choice, other, 4, r.Card);
    await ExtensionRules.RemoveAll(r.Player); await ExtensionRules.RemoveAll(r.Player);
    Check(ExtensionRules.GetAmount(other) == 4 && ReferenceEquals(PowerCmd.LastApplier, r.Player.Creature));
});
Test("非法延拓数量及阈值不会改变状态", async () => {
    var r = new Rig();
    try { await ExtensionRules.Gain(r.Choice, r.Player, -1, r.Card); throw new Exception("must throw"); } catch (ArgumentOutOfRangeException) { }
    try { await ExtensionRules.TryConsumeAll(r.Player, 0); throw new Exception("must throw"); } catch (ArgumentOutOfRangeException) { }
    Check(ExtensionRules.GetAmount(r.Player) == 0 && PowerCmd.ApplyCount == 0);
});

// F02 回归：表征的延迟力量必须区分「已发放」和「待发放」。
Test("表征打出回合结束不发放也不收回", async () => {
    var r = new Rig();
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == power && power.Amount == 3);
});
Test("表征下回合开始发放并在结束时收回", async () => {
    var r = new Rig();
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    r.Player.PlayerCombatState!.TurnNumber++;
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == null);
});
Test("发放回合再次预约只扣回已发放部分", async () => {
    var r = new Rig();
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    r.Player.PlayerCombatState!.TurnNumber++;
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    Check(power.Amount == 6);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == power && power.Amount == 3);
    r.Player.PlayerCombatState!.TurnNumber++;
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == null);
});
Test("同回合多张表征合并为一次预约", async () => {
    var r = new Rig();
    await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    r.Player.PlayerCombatState!.TurnNumber++;
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 6);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == null);
});
Test("非本人回合不触发表征发放与收回", async () => {
    var r = new Rig();
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    await power.AfterSideTurnStart(CombatSide.Player, Array.Empty<MegaCrit.Sts2.Core.Entities.Creatures.Creature>(), r.State);
    r.Player.PlayerCombatState!.TurnNumber++;
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Enemy, Array.Empty<MegaCrit.Sts2.Core.Entities.Creatures.Creature>());
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == power);
});

// F03 回归：晶币的永久递减要落在牌组原件上，跨战斗保留，且各张独立。
// 战斗克隆体用 MemberwiseClone 复刻（和本体 CloneCard 一致），再指回牌组原件。
CrystalCoin CombatClone(CrystalCoin deck, Player owner) {
    var clone = (CrystalCoin)deck.Clone();
    clone.Owner = owner;
    clone.DeckVersion = deck;
    return clone;
}

Test("晶币跨战斗递减写回牌组原件", async () => {
    PlayerCmd.GoldGranted.Clear();
    var r = new Rig(); var deck = new CrystalCoin { Owner = r.Player };
    await CombatClone(deck, r.Player).Play(r.Choice, new CardPlay { Card = deck });
    await CombatClone(deck, r.Player).Play(r.Choice, new CardPlay { Card = deck });
    Check(PlayerCmd.GoldGranted.SequenceEqual(new[] { 18m, 15m }));
});
Test("晶币收益下限为0", async () => {
    PlayerCmd.GoldGranted.Clear();
    var r = new Rig(); var deck = new CrystalCoin { Owner = r.Player };
    for (int i = 0; i < 7; i++) {
        await CombatClone(deck, r.Player).Play(r.Choice, new CardPlay { Card = deck });
    }
    Check(PlayerCmd.GoldGranted.SequenceEqual(new[] { 18m, 15m, 12m, 9m, 6m, 3m }));
});
Test("晶币升级抬高基础值且不清除递减", async () => {
    PlayerCmd.GoldGranted.Clear();
    var r = new Rig(); var deck = new CrystalCoin { Owner = r.Player }; deck.Upgrade();
    await CombatClone(deck, r.Player).Play(r.Choice, new CardPlay { Card = deck });
    await CombatClone(deck, r.Player).Play(r.Choice, new CardPlay { Card = deck });
    Check(PlayerCmd.GoldGranted.SequenceEqual(new[] { 24m, 21m }));
});
Test("多张晶币各自独立递减", async () => {
    PlayerCmd.GoldGranted.Clear();
    var r = new Rig();
    var deckA = new CrystalCoin { Owner = r.Player };
    var deckB = new CrystalCoin { Owner = r.Player };
    await CombatClone(deckA, r.Player).Play(r.Choice, new CardPlay { Card = deckA });
    await CombatClone(deckA, r.Player).Play(r.Choice, new CardPlay { Card = deckA });
    await CombatClone(deckB, r.Player).Play(r.Choice, new CardPlay { Card = deckB });
    Check(PlayerCmd.GoldGranted.SequenceEqual(new[] { 18m, 15m, 18m }));
});

foreach (int ordinal in new[] { 1, 2, 3, 4 }) {
    int position = ordinal;
    Sync($"量子点第{position}张牌预测与实际结算一致", () => {
        var r = new Rig(); var quantum = new QuantumDotPower { Owner = r.Player.Creature, Amount = 1 };
        for (int i = 1; i < position; i++) r.Record(new ProbeCard(CrystalBranch.None, []) { Owner = r.Player, CombatState = r.State });
        var prediction = new CrystalBranchContext(r.Card, null, null, 1, CrystalBranch.Both);
        CrystalBranch predicted = quantum.ModifyCrystalBranches(prediction, CrystalBranch.Resistance);
        var current = r.PlayInfo; CombatManager.Instance.History.CardPlaysStarted.Add(new(r.State, r.State.Turn, current));
        var actual = prediction with { CardPlay = current };
        Check(predicted == quantum.ModifyCrystalBranches(actual, CrystalBranch.Resistance));
        Check(predicted == (position <= 2 ? CrystalBranch.Both : CrystalBranch.Resistance));
    });
}
Sync("量子点不把基础效果的嵌套出牌计入外层序号", () => {
    var r = new Rig(); var quantum = new QuantumDotPower { Owner = r.Player.Creature };
    r.Record(r.Crystal<CrystalA>());
    var current = r.PlayInfo; CombatManager.Instance.History.CardPlaysStarted.Add(new(r.State, r.State.Turn, current));
    r.Record(r.Crystal<CrystalA>(), auto: true); r.Record(r.Crystal<CrystalB>(), auto: true);
    Check(quantum.ModifyCrystalBranches(new(r.Card, current, null, 1, CrystalBranch.Both), CrystalBranch.Resistance) == CrystalBranch.Both);
});
Sync("量子点第三张牌的预测不会额外生成晶逆重放", () => {
    var r = new Rig(); r.Card.ReplayCount = 1;
    r.Player.Creature.Powers.Add(new QuantumDotPower { Owner = r.Player.Creature });
    r.Record(r.Crystal<CrystalA>()); r.Record(r.Crystal<CrystalA>());
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("量子点第二张牌也等到基础效果后预约重放", () => {
    var r = new Rig(); r.Card.ReplayCount = 1;
    r.Player.Creature.Powers.Add(new QuantumDotPower { Owner = r.Player.Creature }); r.Record(r.Crystal<CrystalA>());
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("量子点原生重放保持该系列的序号", () => {
    var r = new Rig(); var quantum = new QuantumDotPower { Owner = r.Player.Creature };
    r.Record(r.Card); var replay = new CardPlay { Card = r.Card, PlayIndex = 1 };
    CombatManager.Instance.History.CardPlaysStarted.Add(new(r.State, r.State.Turn, replay));
    r.Record(r.Crystal<CrystalA>(), auto: true); r.Record(r.Crystal<CrystalB>(), auto: true);
    Check(quantum.ModifyCrystalBranches(new(r.Card, replay, null, 1, CrystalBranch.Both), CrystalBranch.Resistance) == CrystalBranch.Both);
});
Sync("量子点过滤其他玩家并在新回合重新计数", () => {
    var r = new Rig(); var quantum = new QuantumDotPower { Owner = r.Player.Creature };
    var foreign = r.Crystal<CrystalA>(); foreign.Owner = new Player(); r.Record(foreign);
    r.Record(r.Card); r.Record(r.Card); r.State.Turn++;
    Check(quantum.ModifyCrystalBranches(new(r.Card, null, null, 1, CrystalBranch.Both), CrystalBranch.Resistance) == CrystalBranch.Both);
});
foreach (bool upgraded in new[] { false, true }) {
    bool upgrade = upgraded;
    Test($"污染H{(upgrade ? "升级版" : "普通版")}从零进度到准确阈值才奖励", async () => {
        var r = new Rig(); var card = new ReminiscencePollutionH { Owner = r.Player, CombatState = r.State };
        if (upgrade) card.Upgrade(); await card.Play(r.Choice, new CardPlay { Card = card });
        var power = r.Player.Creature.GetPower<ReminiscencePollutionHPower>()!;
        Check(power.Progress == 0 && power.DisplayAmount == 0);
        int threshold = upgrade ? 3 : 4;
        for (int i = 1; i < threshold; i++) await power.AfterCrystalBranchTriggered(r.Choice,
            new(r.Card, r.PlayInfo, null, 1, CrystalBranch.Both), CrystalBranch.Resistance);
        Check(CardPileCmd.DrawCount == 0 && ExtensionRules.GetAmount(r.Player) == 0);
        await power.AfterCrystalBranchTriggered(r.Choice, new(r.Card, r.PlayInfo, null, 1, CrystalBranch.Both), CrystalBranch.Resistance);
        Check(power.Progress == 0 && CardPileCmd.DrawCount == 1 && ExtensionRules.GetAmount(r.Player) == 1);
    });
}
Test("污染H重复施加不赠送进度且普通版不覆盖升级阈值", async () => {
    var r = new Rig(); var card = new ReminiscencePollutionH { Owner = r.Player, CombatState = r.State }; card.Upgrade();
    await card.Play(r.Choice, new CardPlay { Card = card }); var power = r.Player.Creature.GetPower<ReminiscencePollutionHPower>()!;
    await power.AfterCrystalBranchTriggered(r.Choice, new(r.Card, r.PlayInfo, null, 1, CrystalBranch.Both), CrystalBranch.Resistance);
    var normal = new ReminiscencePollutionH { Owner = r.Player, CombatState = r.State };
    await normal.Play(r.Choice, new CardPlay { Card = normal }); Check(power.Progress == 1);
    for (int i = 0; i < 2; i++) await power.AfterCrystalBranchTriggered(r.Choice,
        new(r.Card, r.PlayInfo, null, 1, CrystalBranch.Both), CrystalBranch.Resistance);
    Check(CardPileCmd.DrawCount == 1 && power.Progress == 0);
});
Test("污染H忽略晶逆和其他玩家的晶抗", async () => {
    var r = new Rig(); var power = new ReminiscencePollutionHPower { Owner = r.Player.Creature, Amount = 1 };
    await power.AfterCrystalBranchTriggered(r.Choice, new(r.Card, r.PlayInfo, null, 0, CrystalBranch.Both), CrystalBranch.Reversal);
    var foreign = new ProbeCard(CrystalBranch.Both, []) { Owner = new Player(), CombatState = r.State };
    await power.AfterCrystalBranchTriggered(r.Choice, new(foreign, null, null, 1, CrystalBranch.Both), CrystalBranch.Resistance);
    Check(power.Progress == 0);
});
Test("Nb与崩碎之歌加的自动晶化Nb组合保留2活力", async () => {
    var r = new Rig(); var nb = new ApplicationNbPower { Owner = r.Player.Creature, Amount = 1 }; r.Player.Creature.Powers.Add(nb);
    var song = new ShatterSong { Owner = r.Player, CombatState = r.State }; song.Upgrade();
    var crystal = r.Crystal<CrystallizationNb>(); r.Player.PlayerCombatState!.Hand.Cards.Add(crystal);
    var play = new CardPlay { Card = song, Target = new() };
    await song.Play(r.Choice, play); await nb.AfterCardPlayed(r.Choice, play);
    Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 2); Check(DamageCmd.HitCounts.SequenceEqual(new[] { 1 }));
});
Test("Nb保留同张牌后续伤害段的活力并清理原版攻击缓存", async () => {
    var r = new Rig(); var nb = new ApplicationNbPower { Owner = r.Player.Creature }; r.Player.Creature.Powers.Add(nb);
    var vigor = await PowerCmd.Apply<VigorPower>(r.Choice, r.Player.Creature, 6, r.Player.Creature, null);
    var attack = new TestAttack { Owner = r.Player, CombatState = r.State }; var play = new CardPlay { Card = attack };
    for (int i = 0; i < 2; i++) {
        await DamageCmd.Attack(1).FromCard(attack, play).Execute(r.Choice);
        Check(vigor.Amount == 6 && !vigor.HasAttackCache);
    }
    await nb.AfterCardPlayed(r.Choice, play); Check(vigor.Amount == 4);
    await PowerCmd.Apply<VigorPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, null);
    await nb.AfterCardPlayed(r.Choice, play); Check(vigor.Amount == 5);
});
Test("没有Nb时保持本体攻击后消耗全部活力", async () => {
    var r = new Rig(); await PowerCmd.Apply<VigorPower>(r.Choice, r.Player.Creature, 4, r.Player.Creature, null);
    var attack = new TestAttack { Owner = r.Player, CombatState = r.State };
    await DamageCmd.Attack(1).FromCard(attack, new() { Card = attack }).Execute(r.Choice);
    Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 0);
});
Test("Nb只扣本玩家攻击且不会把活力扣为负数", async () => {
    var r = new Rig(); var nb = new ApplicationNbPower { Owner = r.Player.Creature };
    await PowerCmd.Apply<VigorPower>(r.Choice, r.Player.Creature, 1, r.Player.Creature, null);
    await nb.AfterCardPlayed(r.Choice, r.PlayInfo); Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 1);
    var foreign = new TestAttack { Owner = new Player() }; await nb.AfterCardPlayed(r.Choice, new() { Card = foreign });
    Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 1);
    await nb.AfterCardPlayed(r.Choice, new() { Card = new TestAttack { Owner = r.Player } });
    Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 0);
});
Test("待发禁止自动晶化时崩碎之歌加不产生伤害", async () => {
    var r = new Rig(); var onlyAttacks = new OnlyAttacksThisTurnPower { Owner = r.Player.Creature };
    CardCmd.ShouldAutoPlay = c => onlyAttacks.ShouldPlay(c, AutoPlayType.Default);
    var song = new ShatterSong { Owner = r.Player, CombatState = r.State }; song.Upgrade();
    r.Player.PlayerCombatState!.Hand.Cards.Add(r.Crystal<CrystalA>());
    await song.Play(r.Choice, new() { Card = song, Target = new() }); Check(DamageCmd.HitCounts.Count == 0);
});
Test("崩碎之歌加只计算实际打出的晶化", async () => {
    var r = new Rig(); var allowed = r.Crystal<CrystalA>(); var blocked = r.Crystal<CrystalB>();
    r.Player.PlayerCombatState!.Hand.Cards.AddRange(new CardModel[] { allowed, blocked });
    CardCmd.ShouldAutoPlay = c => c == allowed;
    var song = new ShatterSong { Owner = r.Player, CombatState = r.State }; song.Upgrade();
    await song.Play(r.Choice, new() { Card = song, Target = new() }); Check(DamageCmd.HitCounts.SequenceEqual(new[] { 1 }));
});
Test("崩碎之歌普通版按实际消耗数量造成伤害", async () => {
    var r = new Rig(); r.Player.PlayerCombatState!.Hand.Cards.AddRange(new CardModel[] { r.Crystal<CrystalA>(), r.Crystal<CrystalB>() });
    var song = new ShatterSong { Owner = r.Player, CombatState = r.State };
    await song.Play(r.Choice, new() { Card = song, Target = new() }); Check(DamageCmd.HitCounts.SequenceEqual(new[] { 2 }));
});
Test("晶化Hs提供真正的全体力量并在离手后保留其他来源", async () => {
    var r = new Rig(); var enemy = new MegaCrit.Sts2.Core.Entities.Creatures.Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy);
    await PowerCmd.Apply<StrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, null);
    var hs = r.Crystal<CrystallizationHs>(); await CardPileCmd.AddGeneratedCardToCombat(hs, PileType.Hand, r.Player);
    Check(r.Player.Creature.GetPower<StrengthPower>()!.Amount == 4 && enemy.GetPowerAmount<StrengthPower>() == 1);
    await PowerCmd.Apply<StrengthPower>(r.Choice, r.Player.Creature, 2, r.Player.Creature, null);
    r.Player.PlayerCombatState!.Hand.Cards.Remove(hs);
    await r.Player.Creature.GetPower<CrystalHandAuraPower>()!.AfterCardChangedPiles(hs, PileType.Hand, null);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 5 && enemy.GetPowerAmount<StrengthPower>() == 0);
});
Test("密铺形态真实力量敏捷随晶化离手变化且不会重复叠加", async () => {
    var r = new Rig(); await PowerCmd.Apply<TilingFormPower>(r.Choice, r.Player.Creature, 1, r.Player.Creature, null);
    var crystal = r.Crystal<CrystalA>(); await CardPileCmd.AddGeneratedCardToCombat(crystal, PileType.Hand, r.Player);
    var controller = r.Player.Creature.GetPower<CrystalHandAuraPower>()!;
    await controller.Sync(); await controller.Sync();
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 2 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 2);
    r.Player.PlayerCombatState!.Hand.Cards.Clear(); await controller.Sync();
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 0);
});
Test("Hs与密铺形态相加并覆盖后加入战斗的敌人", async () => {
    var r = new Rig(); await PowerCmd.Apply<TilingFormPower>(r.Choice, r.Player.Creature, 1, r.Player.Creature, null);
    await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
    var controller = r.Player.Creature.GetPower<CrystalHandAuraPower>()!;
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 2);
    var enemy = new MegaCrit.Sts2.Core.Entities.Creatures.Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy);
    await controller.AfterCreatureAddedToCombat(enemy); Check(enemy.GetPowerAmount<StrengthPower>() == 1);
});
Test("光环抵消负力量为零时离手会恢复原有负力量", async () => {
    var r = new Rig(); await PowerCmd.Apply<StrengthPower>(r.Choice, r.Player.Creature, -1, r.Player.Creature, null);
    await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    r.Player.PlayerCombatState!.Hand.Cards.Clear(); await r.Player.Creature.GetPower<CrystalHandAuraPower>()!.Sync();
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == -1);
});
Test("光环控制器克隆后不会共享贡献字典", async () => {
    var r = new Rig(); await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
    var controller = r.Player.Creature.GetPower<CrystalHandAuraPower>()!;
    var other = new Player(); other.Creature.CombatState = r.State; r.State.CreatureList.Add(other.Creature);
    var clone = (CrystalHandAuraPower)controller.ToMutable(); clone.Owner = other.Creature; other.Creature.Powers.Add(clone);
    await clone.Sync(); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 1);
    await controller.Sync(); Check(other.Creature.GetPowerAmount<StrengthPower>() == 1);
});
Sync("实际游戏程序集的Nb补丁目标与内部缓存字段兼容", () => {
    if (args.Length == 0) throw new Exception("GameDir required");
    var directory = System.IO.Path.Combine(args[0], "data_sts2_windows_x86_64");
    var loadContext = new System.Runtime.Loader.AssemblyLoadContext("GameApiContract", isCollectible: true);
    loadContext.Resolving += (_, name) => {
        string path = System.IO.Path.Combine(directory, name.Name + ".dll");
        return System.IO.File.Exists(path) ? loadContext.LoadFromAssemblyPath(path) : null;
    };
    try {
        var assembly = loadContext.LoadFromAssemblyPath(System.IO.Path.Combine(directory, "sts2.dll"));
        var vigor = assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.VigorPower", throwOnError: true)!;
        var power = assembly.GetType("MegaCrit.Sts2.Core.Models.PowerModel", throwOnError: true)!;
        Check(vigor.GetMethod("AfterAttack")?.ReturnType == typeof(Task));
        Check(power.GetField("_internalData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.FieldType == typeof(object));
        var data = vigor.GetNestedType("Data", System.Reflection.BindingFlags.NonPublic)!;
        Check(data != null && Activator.CreateInstance(data, nonPublic: true) != null);
    } finally { loadContext.Unload(); }
});

Test("Hs光环改变敌人力量时同步刷新伤害意图", async () => {
    var r = new Rig(); var enemy = new MegaCrit.Sts2.Core.Entities.Creatures.Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy);
    var room = new MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom(); MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance = room;
    await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
    Check(room.Node.RefreshCount == 1);
    r.Player.PlayerCombatState!.Hand.Cards.Clear(); await r.Player.Creature.GetPower<CrystalHandAuraPower>()!.Sync();
    Check(room.Node.RefreshCount == 2);
});

NewCardTests.Register(tests, args);
SemanticAuditTests.Register(tests);
UpdatedSpecificationTests.Register(tests, args);
DerivedNeutronTests.Register(tests, args);
CrystalPunishmentTests.Register(tests, args);
DeferredReleaseTests.Register(tests, args);
CrystalReplayTests.Register(tests, args);
TurnSchedulingTests.Register(tests, args);
ApplicationEffectTests.Register(tests, args);
RemainingCardEffectTests.Register(tests, args);
RuleDecisionTests.Register(tests, args);
int failed = 0;
foreach (var test in tests) {
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed; scope: real mod source, command/history stubs, real Harmony patching and game API contract.");
return failed == 0 ? 0 : 1;

public class FakeCombat : ICombatState
{
    public int Turn { get; set; } = 1;
    public List<AbstractModel> Listeners { get; } = [];
    public List<MegaCrit.Sts2.Core.Entities.Creatures.Creature> CreatureList { get; } = [];
    public IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Creatures => CreatureList;
    public IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Allies => CreatureList.Where(c => c.Player != null).ToList();
    public IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Enemies => CreatureList.Where(c => c.Player == null).ToList();
    public IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> HittableEnemies => Enemies.Where(c => c.IsAlive);
    public CardModel CloneCard(CardModel card) => card.CreateClone();
    public IEnumerable<AbstractModel> IterateHookListeners() => Listeners
        .Concat(Creatures.SelectMany(c => c.Powers))
        .Concat(Creatures.Where(c => c.Player != null).SelectMany(c => c.Player!.PlayerCombatState!.AllCards))
        .Distinct().ToArray();
    public T CreateCard<T>(Player owner) where T : CardModel {
        var card = Activator.CreateInstance<T>(); card.Owner = owner; card.CombatState = this; return card;
    }
}
public class ProbeCard(CrystalBranch branches, List<string> log) : ApplicationCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override CrystalBranch AvailableCrystalBranches => branches;
    public int ReplayCount { get; set; }
    public bool SuppressReplayBranches { get; set; }
    protected override bool SuppressCrystalBranchesOnReplay => SuppressReplayBranches;
    protected override int CrystalReversalReplayCount => ReplayCount;
    public Func<Task<CardModel?>>? Main { get; set; }
    public Func<CardModel?, Task>? Resistance { get; set; }
    public Func<CardModel?, Task>? Reversal { get; set; }
    protected override async Task<CardModel?> PlayEffect(PlayerChoiceContext context, CardPlay play) {
        log.Add("main"); return Main == null ? null : await Main();
    }
    protected override Task CrystalResistance(PlayerChoiceContext context, CardPlay play, CardModel? result) {
        log.Add("resistance"); return Resistance?.Invoke(result) ?? Task.CompletedTask;
    }
    protected override Task CrystalReversal(PlayerChoiceContext context, CardPlay play, CardModel? result) {
        log.Add("reversal"); return Reversal?.Invoke(result) ?? Task.CompletedTask;
    }
}
public class CrystalA : CrystallizationCard { }
public class TestAttack() : STS2RitsuLib.Scaffolding.Content.ModCardTemplate(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
public class CrystalB : CrystallizationCard { }
public class MarkedApplication() : ProbeCard(CrystalBranch.None, []), IApplicationCard { }
public class MarkedExtension() : ProbeCard(CrystalBranch.None, []), IExtensionCard { }
public class Modifier(Func<CrystalBranchContext, CrystalBranch, CrystalBranch> modify) : AbstractModel, ICrystalBranchModifier
{
    public CrystalBranch ModifyCrystalBranches(CrystalBranchContext context, CrystalBranch selected) => modify(context, selected);
}
public class Listener(Func<CrystalBranchContext, CrystalBranch, Task> listen) : AbstractModel, ICrystalBranchListener
{
    public Task AfterCrystalBranchTriggered(PlayerChoiceContext context, CrystalBranchContext branchContext, CrystalBranch branch) => listen(branchContext, branch);
}
public class Rig
{
    public Player Player { get; } = new();
    public FakeCombat State { get; } = new();
    public List<string> Log { get; } = [];
    public PlayerChoiceContext Choice { get; } = new();
    public ProbeCard Card { get; }
    public CardPlay PlayInfo => new() { Card = Card };
    public int Energy { get => Player.PlayerCombatState!.Energy; set => Player.PlayerCombatState!.Energy = value; }
    public Rig(CrystalBranch branches = CrystalBranch.Both, int energy = 1) {
        CombatManager.Instance.History.CardPlaysStarted.Clear(); CombatManager.Instance.History.CardPlaysFinished.Clear(); CombatManager.Instance.IsOverOrEnding = false;
        CombatManager.Instance.IsStarting = false; PowerCmd.ApplyCount = 0;
        CardPileCmd.DrawCount = 0; CardCmd.ShouldAutoPlay = _ => true; DamageCmd.HitCounts.Clear();
        CardPileCmd.HandCapacity = 10; CardPileCmd.DrawBlocked = false; CardCmd.ExhaustBlocked = false; CardCmd.ExhaustDestinationOverride = null;
        DamageCmd.Attacks.Clear(); CreatureCmd.BlockGains.Clear(); CreatureCmd.LastAnimation = null; CreatureCmd.AfterBlockGained = null; CreatureCmd.AfterDamage = null; CreatureCmd.DirectDamages.Clear();
        DamageCmd.AfterAttackExecuted = null; CardSelectCmd.SelectionOverride = null;
        CardSelectCmd.PileSelectionOverride = null; PlayerCmd.AfterModifyingEnergyGain = null;
        PowerCmd.ModifyAmountReceived = null;
        CreatureCmd.AreaDamages.Clear(); CombatManager.Instance.History.Entries.Clear();
        MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance = null;
        Player.Creature.CombatState = State; State.CreatureList.Add(Player.Creature);
        Energy = energy; Card = new ProbeCard(branches, Log) { Owner = Player, CombatState = State };
    }
    public T Crystal<T>() where T : CardModel, new() => new() { Owner = Player, CombatState = State };
    public void Record(CardModel card, bool auto = false, int index = 0) =>
        CombatManager.Instance.History.CardPlaysStarted.Add(new(State, State.Turn, new CardPlay { Card = card, IsAutoPlay = auto, PlayIndex = index }));
    public Task Play() => Card.Play(Choice, PlayInfo);
    public void Modify(Func<CrystalBranchContext, CrystalBranch, CrystalBranch> modify) => State.Listeners.Add(new Modifier(modify));
    public void Listen(Func<CrystalBranchContext, CrystalBranch, Task>? extra = null) => State.Listeners.Add(new Listener(async (context, branch) => {
        Log.Add("notify:" + (branch == CrystalBranch.Resistance ? "resistance" : "reversal"));
        if (extra != null) await extra(context, branch);
    }));
}
