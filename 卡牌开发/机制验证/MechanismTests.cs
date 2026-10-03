using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

var tests = new List<(string Name, Func<Task> Run)>();
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
    Check(c.CanonicalKeywords.SequenceEqual(new[] { CardKeyword.Retain, CardKeyword.Exhaust }));
});
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
Test("重放段不触发晶抗晶逆或通知", async () => {
    var r = new Rig(energy: 0); r.Listen();
    await r.Card.Play(r.Choice, new CardPlay { Card = r.Card, PlayIndex = 1 });
    LogIs(r, "main");
});
Sync("晶逆重放预测延长出牌系列", () => {
    var r = new Rig(energy: 0); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 3);
});
Sync("非晶逆能量不延长出牌系列", () => {
    var r = new Rig(energy: 1); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 1);
});
Sync("重放预测只作用于牌自身", () => {
    var r = new Rig(energy: 0); r.Card.ReplayCount = 2;
    Check(r.Card.ModifyCardPlayCount(r.Crystal<CrystalA>(), null, 1) == 1);
});
Sync("修改规则参与重放预测", () => {
    var r = new Rig(energy: 1); r.Card.ReplayCount = 1;
    r.Modify((_, _) => CrystalBranch.Both);
    Check(r.Card.ModifyCardPlayCount(r.Card, null, 1) == 2);
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
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == null);
});
Test("发放回合再次预约只扣回已发放部分", async () => {
    var r = new Rig();
    var power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
    power = await PowerCmd.Apply<CharacterizationStrengthPower>(r.Choice, r.Player.Creature, 3, r.Player.Creature, r.Card);
    Check(power.Amount == 6);
    await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
    Check(r.Player.Creature.GetPower<CharacterizationStrengthPower>() == power && power.Amount == 3);
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

int failed = 0;
foreach (var test in tests) {
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed; scope: real shared mechanism source with game command/history stubs.");
return failed == 0 ? 0 : 1;

public class FakeCombat : ICombatState
{
    public int Turn { get; set; } = 1;
    public List<AbstractModel> Listeners { get; } = [];
    public IEnumerable<AbstractModel> IterateHookListeners() => Listeners.ToArray();
    public T CreateCard<T>(Player owner) where T : CardModel {
        var card = Activator.CreateInstance<T>(); card.Owner = owner; card.CombatState = this; return card;
    }
}
public class ProbeCard(CrystalBranch branches, List<string> log) : ApplicationCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override CrystalBranch AvailableCrystalBranches => branches;
    public int ReplayCount { get; set; }
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
public class CrystalB : CrystallizationCard { }
public class MarkedApplication() : ProbeCard(CrystalBranch.None, []), IApplicationCard { }
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
        CombatManager.Instance.History.CardPlaysStarted.Clear(); CombatManager.Instance.IsOverOrEnding = false;
        CombatManager.Instance.IsStarting = false; PowerCmd.ApplyCount = 0;
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
