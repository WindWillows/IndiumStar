using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// 最新用户定稿优先于DOCX。运行实际卡牌/能力源码，替身不代表实机验收。
public static class RuleDecisionTests
{
    private static void Check(bool condition, string message = "Latest rule decision failed")
    { if (!condition) throw new Exception(message); }
    private static Task Play(Rig r, CardModel c, Creature? target = null) =>
        c.Play(r.Choice, new() { Card = c, Target = target });
    private static Creature Enemy(Rig r)
    { var c = new Creature { CombatState = r.State }; r.State.CreatureList.Add(c); return c; }
    private static void Put(Rig r, CardModel c, PileType pile = PileType.Hand)
    { c.Pile = pile.GetPile(r.Player); c.Pile.Cards.Add(c); }
    private static void Finished(Rig r, CardModel c, int index = 0, bool auto = false, int turn = 1) =>
        CombatManager.Instance.History.CardPlaysFinished.Add(new(r.State, turn,
            new() { Card = c, PlayIndex = index, PlayCount = 2, IsAutoPlay = auto }));
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            foreach (int count in new[] { 0, 1, 5 })
            {
                int n = count;
                Test($"反演固定7伤、费用稀有度词条与历史延拓 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<Inversion>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    for (int i = 0; i < n; i++) Finished(r, r.Crystal<MarkedExtension>(), turn: i % 2);
                    DamageCmd.AfterAttackExecuted = _ => { Check(ExtensionRules.GetAmount(r.Player) == 0); return Task.CompletedTask; };
                    await Play(r, c, enemy);
                    Check(DamageCmd.Attacks.Single().Damage == 7 && ExtensionRules.GetAmount(r.Player) == n);
                    Check(c is IExtensionCard && c.EnergyCost.Value == 1 && c.Type == CardType.Attack
                        && c.Keywords.Contains(CardKeyword.Exhaust)
                        && c.Keywords.Contains(CardKeyword.Retain) == up);
                });
            }
            Test($"晶币复制继承已衰减收益，后续各自递减 upgraded={up}", async () =>
            {
                var r = new Rig(); PlayerCmd.GoldGranted.Clear(); var c = r.Crystal<CrystalCoin>(); if (up) c.Upgrade();
                await Play(r, c); await Play(r, c); var copy = (CrystalCoin)c.CreateClone();
                await Play(r, c); await Play(r, copy); await Play(r, copy);
                decimal b = up ? 24 : 18;
                Check(PlayerCmd.GoldGranted.SequenceEqual(new[] { b, b - 3, b - 6, b - 6, b - 9 }));
                await Play(r, c); Check(PlayerCmd.GoldGranted.Last() == b - 9);
            });
            foreach (bool secondUp in new[] { false, true })
            {
                bool nextUp = secondUp;
                Test($"磷语多份合并图标、次数叠加，各份效果不变 firstUp={up} secondUp={nextUp}", async () =>
                {
                    var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); var b = r.Crystal<ReminiscencePhosphorus>();
                    if (up) a.Upgrade(); if (nextUp) b.Upgrade(); await Play(r, a); await Play(r, b);
                    var p = r.Player.Creature.GetPower<ReminiscencePhosphorusPower>()!;
                    Check(p.Amount == 2 && p.StackType == PowerStackType.Counter && r.Player.Creature.Powers.OfType<ReminiscencePhosphorusPower>().Count() == 1);
                    await ExtensionRules.Gain(r.Choice, r.Player, 0, a);
                    await ExtensionRules.Gain(r.Choice, r.Player, 2, a); Check(r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0));
                    await ExtensionRules.Gain(r.Choice, r.Player, 3, a); Check(r.Energy == 5 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0) + (nextUp ? 3 : 0));
                    await ExtensionRules.Gain(r.Choice, r.Player, 4, a); Check(r.Energy == 5 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0) + (nextUp ? 3 : 0) + 4);
                    r.Player.PlayerCombatState!.TurnNumber++;
                    await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
                    await ExtensionRules.Gain(r.Choice, r.Player, 1, a);
                    await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
                    await ExtensionRules.Gain(r.Choice, r.Player, 1, a);
                    await ExtensionRules.Gain(r.Choice, r.Player, 1, a); Check(r.Energy == 7, "Repeated start callbacks must not refill quotas");
                });
                Test($"硬钙石多份合并图标并累计数值 firstUp={up} secondUp={nextUp}", async () =>
                {
                    var r = new Rig(); var a = r.Crystal<ReminiscenceHardCalcite>(); var b = r.Crystal<ReminiscenceHardCalcite>();
                    if (up) a.Upgrade(); if (nextUp) b.Upgrade(); await Play(r, a); await Play(r, b);
                    var p = r.Player.Creature.GetPower<ReminiscenceHardCalcitePower>()!; int coefficient = (up ? 4 : 3) + (nextUp ? 4 : 3);
                    Check(p.Amount == coefficient && p.StackType == PowerStackType.Counter && r.Player.Creature.Powers.OfType<ReminiscenceHardCalcitePower>().Count() == 1);
                    Enemy(r); Put(r, r.Crystal<Crystallization>()); Put(r, r.Crystal<Crystallization>());
                    Put(r, r.Crystal<Crystallization>(), PileType.Draw); Put(r, r.Crystal<Crystallization>(), PileType.Exhaust);
                    CombatManager.Instance.History.Entries.Add(new(r.State, r.State.Turn, r.Player.Creature));
                    await p.BeforeSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
                    Check(CreatureCmd.AreaDamages.Single().Amount == 2 * coefficient);
                });
                Test($"磷语Before施加钩子嵌套也保留内外模式 outerUp={up} innerUp={nextUp}", async () =>
                {
                    var r = new Rig(energy: 0); await Play(r, r.Crystal<ReminiscencePhosphorus>());
                    await ExtensionRules.Gain(r.Choice, r.Player, 1, null);
                    var outer = r.Crystal<ReminiscencePhosphorus>(); var inner = r.Crystal<ReminiscencePhosphorus>();
                    if (up) outer.Upgrade(); if (nextUp) inner.Upgrade(); bool nested = false;
                    r.Player.Creature.Powers.Add(new BeforeObserver(async p => {
                        if (p is ReminiscencePhosphorusPower && !nested) { nested = true; await Play(r, inner); }
                    }) { Owner = r.Player.Creature });
                    await Play(r, outer);
                    await ExtensionRules.Gain(r.Choice, r.Player, 2, outer);
                    Check(r.Energy == 3 && ExtensionRules.GetAmount(r.Player) == (nextUp ? 2 : 0));
                    await ExtensionRules.Gain(r.Choice, r.Player, 3, outer);
                    Check(r.Energy == 6 && ExtensionRules.GetAmount(r.Player) == (nextUp ? 2 : 0) + (up ? 3 : 0));
                });
            }
        }
        Test("反演只计本人完成系列，自动算一次，重放不重复，未完成不计", async () =>
        {
            var r = new Rig(); var counted = r.Crystal<MarkedExtension>(); Finished(r, counted, auto: true); Finished(r, counted, index: 1, auto: true);
            Finished(r, r.Crystal<TestAttack>()); var foreign = new MarkedExtension { Owner = new Player(), CombatState = r.State }; Finished(r, foreign);
            r.Record(r.Crystal<MarkedExtension>()); var c = r.Crystal<Inversion>(); await Play(r, c, Enemy(r)); Check(ExtensionRules.GetAmount(r.Player) == 1);
        });
        Test("反演当前牌不自计，已完成反演在后续计入，重放结算仍获得同数延拓", async () =>
        {
            var r = new Rig(); var enemy = Enemy(r); var a = r.Crystal<Inversion>(); var b = r.Crystal<Inversion>();
            await a.OnPlayWrapper(r.Choice, enemy); Check(ExtensionRules.GetAmount(r.Player) == 0 && a.Pile?.Type == PileType.Exhaust);
            await b.OnPlayWrapper(r.Choice, enemy); Check(ExtensionRules.GetAmount(r.Player) == 1);
            await a.Play(r.Choice, new() { Card = a, Target = enemy, PlayIndex = 1, PlayCount = 2 });
            Check(ExtensionRules.GetAmount(r.Player) == 3);
        });
        Test("反演攻击连锁完成的延拓牌在伤害之后计入", async () =>
        {
            var r = new Rig(); DamageCmd.AfterAttackExecuted = _ => { Finished(r, r.Crystal<MarkedExtension>()); return Task.CompletedTask; };
            await Play(r, r.Crystal<Inversion>(), Enemy(r)); Check(ExtensionRules.GetAmount(r.Player) == 1);
        });
        foreach (bool endCombat in new[] { false, true })
        {
            bool ended = endCombat;
            Test($"反演伤害后战斗结束或本人死亡不再加延拓 ended={ended}", async () =>
            {
                var r = new Rig(); Finished(r, r.Crystal<MarkedExtension>());
                DamageCmd.AfterAttackExecuted = _ => { if (ended) CombatManager.Instance.IsOverOrEnding = true; else r.Player.Creature.IsAlive = false; return Task.CompletedTask; };
                await Play(r, r.Crystal<Inversion>(), Enemy(r)); Check(ExtensionRules.GetAmount(r.Player) == 0);
            });
        }
        Test("反演新战斗重置历史，累计不跨战斗", async () =>
        {
            var r = new Rig(); Finished(r, r.Crystal<MarkedExtension>()); var next = new Rig();
            await Play(next, next.Crystal<Inversion>(), Enemy(next)); Check(ExtensionRules.GetAmount(next.Player) == 0);
        });
        Test("反演延拓使用公共入口，受磷语替换影响", async () =>
        {
            var r = new Rig(energy: 0); await Play(r, r.Crystal<ReminiscencePhosphorus>()); Finished(r, r.Crystal<MarkedExtension>());
            await Play(r, r.Crystal<Inversion>(), Enemy(r)); Check(r.Energy == 1 && ExtensionRules.GetAmount(r.Player) == 0);
        });
        foreach (int accepted in new[] { 0, 1, 2 })
        {
            int offset = accepted;
            Test($"磷语重复施加只增加本体接受的额度 offset={offset}", async () =>
            {
                var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); await Play(r, a);
                PowerCmd.ModifyAmountReceived = (p, n) => p is ReminiscencePhosphorusPower ? offset : n;
                var b = r.Crystal<ReminiscencePhosphorus>(); b.Upgrade(); await Play(r, b); PowerCmd.ModifyAmountReceived = null;
                for (int i = 0; i < offset + 2; i++) await ExtensionRules.Gain(r.Choice, r.Player, 1, a);
                Check(r.Energy == 1 + offset && ExtensionRules.GetAmount(r.Player) == offset + 1);
            });
        }
        Test("磷语被拒绝的升级施加不会改写下一份普通额度", async () =>
        {
            var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); await Play(r, a);
            PowerCmd.ModifyAmountReceived = (p, n) => p is ReminiscencePhosphorusPower ? 0 : n;
            var b = r.Crystal<ReminiscencePhosphorus>(); b.Upgrade(); await Play(r, b); PowerCmd.ModifyAmountReceived = null;
            await Play(r, a); await ExtensionRules.Gain(r.Choice, r.Player, 2, a); await ExtensionRules.Gain(r.Choice, r.Player, 3, a);
            Check(r.Energy == 5 && ExtensionRules.GetAmount(r.Player) == 0);
        });
        Test("磷语能量连锁重入先消费额度，两个额度均只触发一次", async () =>
        {
            var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); await Play(r, a); await Play(r, a);
            int calls = 0; PlayerCmd.AfterModifyingEnergyGain = async (_, _) => { if (++calls < 4) await ExtensionRules.Gain(r.Choice, r.Player, 1, a); };
            await ExtensionRules.Gain(r.Choice, r.Player, 2, a);
            Check(calls == 2 && r.Energy == 3 && ExtensionRules.GetAmount(r.Player) == 1);
        });
        Test("磷语能力克隆后的模式列表独立", async () =>
        {
            var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); await Play(r, a);
            var b = r.Crystal<ReminiscencePhosphorus>(); b.Upgrade(); await Play(r, b);
            var p = r.Player.Creature.GetPower<ReminiscencePhosphorusPower>()!; var clone = (ReminiscencePhosphorusPower)p.ToMutable();
            clone.SetAmount(0); Check(!await clone.TryConvertExtensionToEnergy(r.Choice, r.Player, 2));
            Check(await p.TryConvertExtensionToEnergy(r.Choice, r.Player, 3) && r.Energy == 3);
        });
        Test("磷语他人和其他战斗的回合开始不会刷新本人额度", async () =>
        {
            var r = new Rig(energy: 0); var a = r.Crystal<ReminiscencePhosphorus>(); await Play(r, a); await ExtensionRules.Gain(r.Choice, r.Player, 2, a);
            var p = r.Player.Creature.GetPower<ReminiscencePhosphorusPower>()!; r.Player.PlayerCombatState!.TurnNumber++;
            await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, [new Player().Creature], r.State);
            await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], new FakeCombat());
            Check(!await p.TryConvertExtensionToEnergy(r.Choice, new Player(), 10));
            await ExtensionRules.Gain(r.Choice, r.Player, 1, a); Check(r.Energy == 2);
        });
        Test("晶币从牌组战斗克隆复制时读取原件衰减，复制后独立", async () =>
        {
            var r = new Rig(); PlayerCmd.GoldGranted.Clear(); var deck = r.Crystal<CrystalCoin>(); await Play(r, deck); await Play(r, deck);
            var combat = (CrystalCoin)deck.Clone(); combat.DeckVersion = deck; var copy = (CrystalCoin)combat.CreateClone();
            Check(copy.DeckVersion == null); await Play(r, combat); await Play(r, copy); var secondCopy = (CrystalCoin)copy.CreateClone();
            await Play(r, secondCopy); await Play(r, copy);
            Check(PlayerCmd.GoldGranted.SequenceEqual(new decimal[] { 18, 15, 12, 12, 9, 9 }));
        });
        Test("晶币衰减到零再复制和升级仍继承次数", async () =>
        {
            var r = new Rig(); PlayerCmd.GoldGranted.Clear(); var c = r.Crystal<CrystalCoin>(); for (int i = 0; i < 7; i++) await Play(r, c);
            var copy = (CrystalCoin)c.CreateClone(); copy.Upgrade(); await Play(r, copy); Check(PlayerCmd.GoldGranted.Last() == 3);
            int grants = PlayerCmd.GoldGranted.Count;
            await Play(r, copy); Check(PlayerCmd.GoldGranted.Count == grants);
        });
        foreach (bool up in new[] { false, true })
        {
            bool upgraded = up;
            Test($"分析同类型多个实例只计一种、仍排除隐藏效果 upgraded={up}", async () =>
            {
                var r = new Rig(); var target = Enemy(r); var c = r.Crystal<Analyze>(); if (upgraded) c.Upgrade();
                r.Player.Creature.Powers.AddRange(new PowerModel[] { new StrengthPower(), new StrengthPower(), new WeakPower(), new HiddenPower() });
                target.Powers.AddRange(new PowerModel[] { new WeakPower(), new WeakPower(), new PoisonPower(), new HiddenPower() });
                await Play(r, c, target); Check(r.Player.Creature.Block == 4 && CreatureCmd.DirectDamages.Single().Amount == 4);
            });
            Test($"原波穿刺同类型负面状态只计一种 upgraded={up}", async () =>
            {
                var r = new Rig(); var target = Enemy(r); var c = r.Crystal<PrimordialPierce>(); if (upgraded) c.Upgrade();
                target.Powers.AddRange(new PowerModel[] { new WeakPower(), new WeakPower(), new PoisonPower(), new TemporaryDebuff(), new StrengthPower { Amount = -2 } });
                await Play(r, c, target); Check(DamageCmd.Attacks.Single().Damage == 11 + 3 * (upgraded ? 4 : 3));
            });
        }
        Test("气溶胶命名保留模型ID且中英卡面同步", () =>
        {
            foreach (string lang in new[] { "zhs", "eng" })
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(args[1], "IndiumStar", "localization", lang, "cards.json")));
                Check(doc.RootElement.GetProperty("INDIUM_STAR_CARD_DEFERRED_RELEASE.title").GetString() == (lang == "zhs" ? "气溶胶" : "Aerosol"));
            }
            return Task.CompletedTask;
        });
    }
    private sealed class HiddenPower : PowerModel { protected override bool IsVisibleInternal => false; }
    private sealed class TemporaryDebuff : PowerModel, ITemporaryPower { public override PowerType Type => PowerType.Debuff; }
    private sealed class BeforeObserver(Func<PowerModel, Task> observe) : PowerModel
    {
        public override Task BeforePowerAmountChanged(PowerModel p, decimal n, Creature target, Creature? applier, CardModel? source) => observe(p);
    }
}
