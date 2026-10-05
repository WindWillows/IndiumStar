using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Patches;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

internal static class CrystalReplayTests
{
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        void Check(bool value) { if (!value) throw new Exception("crystal replay assertion failed"); }
        CardPlay[] Started(CardModel card) => CombatManager.Instance.History.CardPlaysStarted
            .Where(entry => ReferenceEquals(entry.CardPlay.Card, card)).Select(entry => entry.CardPlay).ToArray();
        Creature Enemy(Rig r) { var enemy = new Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy); return enemy; }
        void Series(CardModel card, int count) {
            var plays = Started(card);
            Check(plays.Length == count && plays.Select(play => play.PlayIndex).SequenceEqual(Enumerable.Range(0, count)));
            Check(plays.All(play => play.PlayCount == count) && plays.Count(play => play.IsLastInSeries) == 1);
            Check(card.FinishedCount == count && card.SeriesCleanupCount == 1 && card.Pile?.Type == PileType.Discard);
            Check(CombatManager.Instance.History.CardPlaysFinished.Count(entry => entry.CardPlay.Card == card) == count);
        }
        foreach (int energy in new[] { 0, 1, -1 }) {
            int initial = energy;
            Test($"晶流按基础效果后能量 {initial} 延长实际系列", async () => {
                var r = new Rig(energy: initial); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
                await card.OnPlayWrapper(r.Choice, enemy);
                Series(card, initial == 0 ? 2 : 1);
                Check(CreatureCmd.BlockGains.Sum() == (initial == 0 ? 10 : 5));
                Check(enemy.GetPowerAmount<WeakPower>() == (initial == 0 ? 0 : 1));
            });
        }
        foreach (int energy in new[] { 0, 1 }) {
            int initial = energy;
            Test($"晶流升级系列格挡和虚弱 {initial}", async () => {
                var r = new Rig(energy: initial); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r); card.Upgrade();
                await card.OnPlayWrapper(r.Choice, enemy);
                Series(card, initial == 0 ? 2 : 1);
                Check(CreatureCmd.BlockGains.Sum() == (initial == 0 ? 12 : 6));
                Check(enemy.GetPowerAmount<WeakPower>() == (initial == 0 ? 0 : 2));
            });
        }
        foreach (bool gain in new[] { false, true }) {
            bool gaining = gain;
            Test($"晶流等待格挡异步钩子后选择 {(gaining ? "晶抗" : "晶逆")}", async () => {
                var r = new Rig(energy: gaining ? 0 : 1); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
                CreatureCmd.AfterBlockGained = async (_, _, _) => { await Task.Yield(); r.Energy = gaining ? 1 : 0; };
                await card.OnPlayWrapper(r.Choice, enemy);
                Series(card, gaining ? 1 : 2);
                Check(enemy.GetPowerAmount<WeakPower>() == (gaining ? 1 : 0));
            });
        }
        Test("晶流格挡为零时也等待钩子改变能量", async () => {
            var r = new Rig(energy: 1); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
            await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, -10, null, null);
            CreatureCmd.AfterBlockGained = (_, _, _) => { r.Energy = 0; return Task.CompletedTask; };
            await card.OnPlayWrapper(r.Choice, enemy); Series(card, 2); Check(CreatureCmd.BlockGains.Count == 0);
        });
        foreach (bool gain in new[] { false, true }) {
            bool gaining = gain;
            Test($"运用Th等待生命伤害钩子后选择 {(gaining ? "晶抗" : "晶逆")}", async () => {
                var r = new Rig(energy: gaining ? 0 : 1); var card = r.Crystal<ApplicationTh>(); var enemy = Enemy(r);
                CreatureCmd.AfterDamage = async (_, _) => { await Task.Yield(); r.Energy = gaining ? 1 : 0; };
                await card.OnPlayWrapper(r.Choice, enemy); Series(card, gaining ? 1 : 3);
                Check(CreatureCmd.DirectDamages.Count == (gaining ? 1 : 3));
                Check(r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().Count() == (gaining ? 1 : 3));
                Check(DamageCmd.Attacks.Count == (gaining ? 1 : 0));
                if (gaining) Check(DamageCmd.Attacks[0].Damage == 4 && DamageCmd.Attacks[0].Hits == 2);
                Check(CreatureCmd.DirectDamages.All(hit => hit.Props == (ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move)));
            });
        }
        Test("运用Th晶化入堆钩子参与基础效果后判定", async () => {
            var r = new Rig(energy: 1); var card = r.Crystal<ApplicationTh>(); var enemy = Enemy(r);
            r.State.Listeners.Add(new PileEnergyHook(() => r.Energy = 0));
            await card.OnPlayWrapper(r.Choice, enemy); Series(card, 3);
            Check(DamageCmd.Attacks.Count == 0 && CreatureCmd.DirectDamages.Count == 3);
        });
        Test("运用Th手牌满时晶化改入弃牌仍完成三段", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<ApplicationTh>(); var enemy = Enemy(r);
            for (int i = 0; i < 10; i++) r.Player.PlayerCombatState!.Hand.Cards.Add(r.Crystal<TestAttack>());
            await card.OnPlayWrapper(r.Choice, enemy); Series(card, 3);
            Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 10);
            Check(r.Player.PlayerCombatState.DiscardPile.Cards.OfType<Crystallization>().Count() == 3);
        });
        Test("运用Th升级后每段生命伤害五点，晶抗独立普通攻击", async () => {
            var r = new Rig(); var card = r.Crystal<ApplicationTh>(); card.Upgrade();
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 1);
            Check(CreatureCmd.DirectDamages.Single().Amount == 5);
            Check(DamageCmd.Attacks.Single().Damage == 5 && DamageCmd.Attacks.Single().Hits == 2);
        });
        Test("运用Th先生成晶化后造成生命损失，再以最终能量选择分支", async () => {
            var r = new Rig(); var card = r.Crystal<ApplicationTh>(); var events = new List<string>();
            r.State.Listeners.Add(new PileEnergyHook(() => { events.Add("crystal"); r.Energy = 0; }));
            CreatureCmd.AfterDamage = (_, _) => {
                Check(r.Energy == 0 && r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().Count() == 1);
                events.Add("hp"); r.Energy = 1; return Task.CompletedTask;
            };
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 1);
            Check(events.SequenceEqual(new[] { "crystal", "hp" }) && DamageCmd.Attacks.Single().Hits == 2);
        });
        Test("运用Th后续段能量变化时正常触发晶抗", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<ApplicationTh>(); int mainCount = 0;
            CreatureCmd.AfterDamage = (_, _) => { if (++mainCount == 2) r.Energy = 1; return Task.CompletedTask; };
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 3);
            Check(mainCount == 3 && DamageCmd.Attacks.Count == 2 && DamageCmd.Attacks.All(attack => attack.Hits == 2));
        });
        Test("运用Th量子点重放每段保留双分支，但只首段扩张系列", async () => {
            var r = new Rig(); var card = r.Crystal<ApplicationTh>(); int resistance = 0, reversal = 0;
            r.Player.Creature.Powers.Add(new QuantumDotPower { Owner = r.Player.Creature });
            r.Listen((_, branch) => { if (branch == CrystalBranch.Resistance) resistance++; else reversal++; return Task.CompletedTask; });
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 3);
            Check(resistance == 3 && reversal == 3 && DamageCmd.Attacks.Count == 3);
        });
        Test("晶流重放明确禁止分支，运用Th遵循普通重放规则", () => {
            var r = new Rig(); var flow = r.Crystal<CrystalFlow>(); var th = r.Crystal<ApplicationTh>();
            var property = typeof(ApplicationCard).GetProperty("SuppressCrystalBranchesOnReplay", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check((bool)property.GetValue(flow)! && !(bool)property.GetValue(th)!); return Task.CompletedTask;
        });
        Test("系列保留目标资源自动标记，末段钩子恰好一次", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r); var audit = new SeriesAudit();
            r.State.Listeners.Add(audit);
            var resources = new ResourceInfo { EnergySpent = 1, EnergyValue = 3, StarsSpent = 2, StarValue = 5 };
            await card.OnPlayWrapper(r.Choice, enemy, true, resources); Series(card, 2);
            Check(Started(card).All(play => play.Target == enemy && play.IsAutoPlay && play.ResultPile == PileType.Discard
                && play.Resources.EnergySpent == 1 && play.Resources.EnergyValue == 3
                && play.Resources.StarsSpent == 2 && play.Resources.StarValue == 5));
            Check(audit.Before.Count == 2 && audit.After.Count == 2 && audit.After.Count(play => play.IsLastInSeries) == 1);
            // 条件重放尚未判定时Before看到当前计划；OnPlay完成后同一历史对象已更新。
            Check(audit.CountAtBefore.SequenceEqual(new[] { 1, 2 }) && audit.After.All(play => play.PlayCount == 2));
        });
        Test("本体外部次数修改只执行一次，晶逆追加而非替代已有重放", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>(); var modifier = new NativeReplayModifier();
            r.State.Listeners.Add(modifier); await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 3);
            Check(modifier.Calls == 1 && CreatureCmd.BlockGains.SequenceEqual(new[] { 5, 5, 5 }));
        });
        Test("双分支通知改变能量不重新选择或重复预约", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r); int reversals = 0;
            r.Modify((_, _) => CrystalBranch.Both);
            r.Listen((_, branch) => { if (branch == CrystalBranch.Resistance) r.Energy = 1; else reversals++; return Task.CompletedTask; });
            await card.OnPlayWrapper(r.Choice, enemy); Series(card, 2);
            Check(reversals == 1 && enemy.GetPowerAmount<WeakPower>() == 1);
        });
        Test("分支被禁用不会预约重放，修改器只在实际主效果后调用", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>(); int calls = 0;
            r.Modify((context, _) => { calls++; Check(context.CardPlay != null); return CrystalBranch.None; });
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 1); Check(calls == 1);
        });
        foreach (int position in new[] { 1, 2, 3, 4 }) {
            int ordinal = position;
            Test($"量子点第{ordinal}个系列的实际分支与重放一致", async () => {
                var r = new Rig(); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
                r.Player.Creature.Powers.Add(new QuantumDotPower { Owner = r.Player.Creature });
                for (int i = 1; i < ordinal; i++) r.Record(r.Crystal<TestAttack>());
                await card.OnPlayWrapper(r.Choice, enemy); Series(card, ordinal <= 2 ? 2 : 1);
                Check(enemy.GetPowerAmount<WeakPower>() == 1);
            });
        }
        Test("嵌套自动出牌的预约与外层系列隔离", async () => {
            var r = new Rig(energy: 0); var child = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
            r.Card.ReplayCount = 1; r.Card.SuppressReplayBranches = true;
            bool nested = false; r.Card.Main = async () => {
                if (!nested) { nested = true; await CardCmd.AutoPlay(r.Choice, child, enemy); }
                return null;
            };
            await r.Card.OnPlayWrapper(r.Choice); Series(r.Card, 2); Series(child, 2);
            Check(Started(child).All(play => play.IsAutoPlay) && Started(r.Card).All(play => !play.IsAutoPlay));
        });
        Test("重放未抑制分支时仍只在系列第一段追加次数", async () => {
            var r = new Rig(energy: 0); r.Card.ReplayCount = 2; r.Listen();
            await r.Card.OnPlayWrapper(r.Choice); Series(r.Card, 3);
            Check(r.Log.Count(value => value == "reversal") == 3 && r.Log.Count(value => value == "notify:reversal") == 3);
        });
        Test("没有晶逆重放声明的原版额外段继续完整分支", async () => {
            var r = new Rig(); r.Card.InitialPlayCount = 2;
            await r.Card.OnPlayWrapper(r.Choice); Series(r.Card, 2); Check(r.Log.Count(value => value == "resistance") == 2);
        });
        Test("下一次系列不继承已消费预约或上一轮总次数", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>(); var enemy = Enemy(r);
            await card.OnPlayWrapper(r.Choice, enemy); r.Energy = 1; await card.OnPlayWrapper(r.Choice, enemy);
            Check(Started(card).Select(play => play.PlayCount).SequenceEqual(new[] { 2, 2, 1 }));
            Check(card.SeriesCleanupCount == 2 && card.FinishedCount == 3);
        });
        Test("基础效果失败不延长系列，后续出牌没有遗留预约", async () => {
            var r = new Rig(energy: 0); r.Card.ReplayCount = 1;
            r.Card.Main = () => throw new InvalidOperationException("expected main failure");
            bool thrown = false; try { await r.Card.OnPlayWrapper(r.Choice); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown && Started(r.Card).Single().PlayCount == 1 && r.Card.FinishedCount == 0);
            r.Card.Main = null; r.Energy = 1; await r.Card.OnPlayWrapper(r.Choice);
            Check(Started(r.Card).Length == 2 && Started(r.Card).Last().PlayCount == 1);
        });
        Test("晶逆通知失败不预约或清理未完成的系列", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>();
            var failing = new Listener((_, _) => throw new InvalidOperationException("expected notification failure")); r.State.Listeners.Add(failing);
            bool thrown = false; try { await card.OnPlayWrapper(r.Choice, Enemy(r)); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown && Started(card).Single().PlayCount == 1 && card.SeriesCleanupCount == 0);
            r.State.Listeners.Remove(failing); r.Energy = 1; await card.OnPlayWrapper(r.Choice, Enemy(r));
            Check(Started(card).Length == 2 && Started(card).Last().PlayCount == 1);
        });
        Test("首段玩家死亡后不执行补丁、完成钩子或额外段", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>();
            CreatureCmd.AfterBlockGained = (_, _, _) => { r.Player.Creature.IsAlive = false; return Task.CompletedTask; };
            await card.OnPlayWrapper(r.Choice, Enemy(r));
            Check(Started(card).Length == 1 && card.FinishedCount == 0 && card.SeriesCleanupCount == 0);
            r.Player.Creature.IsAlive = true; r.Energy = 1; CreatureCmd.AfterBlockGained = null;
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Check(Started(card).Last().PlayCount == 1);
        });
        foreach (bool duringMain in new[] { false, true }) {
            bool main = duringMain;
            Test($"战斗在{(main ? "主效果" : "后钩子")}结束时停止额外段", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>();
                if (main) CreatureCmd.AfterBlockGained = (_, _, _) => { CombatManager.Instance.IsOverOrEnding = true; return Task.CompletedTask; };
                else r.State.Listeners.Add(new SeriesAudit { OnAfter = _ => CombatManager.Instance.IsOverOrEnding = true });
                await card.OnPlayWrapper(r.Choice, Enemy(r));
                Check(Started(card).Length == 1 && Started(card)[0].PlayCount == 2 && card.FinishedCount == 1);
            });
        }
        Test("普通自动出牌同样交付重放且不消耗能量", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<CrystalFlow>();
            await CardCmd.AutoPlay(r.Choice, card, Enemy(r)); Series(card, 2);
            Check(Started(card).All(play => play.IsAutoPlay) && r.Energy == 0);
        });
        Test("他人系列不获得自己的量子点分支", async () => {
            var r = new Rig(); r.Player.Creature.Powers.Add(new QuantumDotPower { Owner = r.Player.Creature });
            var other = new Player(); other.Creature.CombatState = r.State; other.PlayerCombatState!.Energy = 1; r.State.CreatureList.Add(other.Creature);
            var card = new CrystalFlow { Owner = other, CombatState = r.State };
            await card.OnPlayWrapper(r.Choice, Enemy(r)); Series(card, 1);
        });
        Test("预约消费幂等且同一牌的不同CardPlay相互独立", () => {
            var r = new Rig(); var first = r.PlayInfo; var second = r.PlayInfo;
            CrystalReplayRules.Request(r.Card, first, 1); CrystalReplayRules.Request(r.Card, second, 2);
            int a = 1, b = 1; CrystalReplayRules.Apply(r.Card, first, ref a); CrystalReplayRules.Apply(r.Card, first, ref a);
            CrystalReplayRules.Apply(r.Card, second, ref b); Check(a == 2 && b == 3 && first.PlayCount == 2 && second.PlayCount == 3);
            var copy = r.Card.Clone(); var clonedPlay = new CardPlay { Card = copy }; int c = 1;
            CrystalReplayRules.Apply(copy, clonedPlay, ref c); Check(c == 1);
            return Task.CompletedTask;
        });
        foreach (string invalid in new[] { "zero", "negative", "foreign", "replay", "overflow", "duplicate" }) {
            string reason = invalid;
            Test($"预约拒绝非法输入 {reason}", () => {
                var r = new Rig(); var play = reason == "replay" ? new CardPlay { Card = r.Card, PlayIndex = 1 }
                    : reason == "overflow" ? new CardPlay { Card = r.Card, PlayCount = int.MaxValue } : r.PlayInfo;
                if (reason == "duplicate") CrystalReplayRules.Request(r.Card, play, 1);
                bool thrown = false;
                try { CrystalReplayRules.Request(reason == "foreign" ? r.Crystal<CrystalA>() : r.Card, play,
                    reason == "zero" ? 0 : reason == "negative" ? -1 : 1); }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or OverflowException or ArgumentException) { thrown = true; }
                Check(thrown); return Task.CompletedTask;
            });
        }
        Test("系列元数据不一致时明确失败且不消费预约", () => {
            var r = new Rig(); var play = r.PlayInfo; CrystalReplayRules.Request(r.Card, play, 1); int count = 2;
            bool thrown = false; try { CrystalReplayRules.Apply(r.Card, play, ref count); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown && count == 2 && play.PlayCount == 1); count = 1; CrystalReplayRules.Apply(r.Card, play, ref count); Check(count == 2);
            return Task.CompletedTask;
        });
        Test("实际回归使用结构体异步状态机，验证字段地址修改原循环", () => {
            Check(CrystalReplaySeriesPatch.TargetMethod().DeclaringType!.IsValueType); return Task.CompletedTask;
        });
        Test("本体循环契约不匹配时补丁拒绝静默降级", () => {
            var target = CrystalReplaySeriesPatch.TargetMethod();
            var instructions = PatchProcessor.GetOriginalInstructions(target).Where(instruction =>
                !instruction.Calls(typeof(AbstractModel).GetMethod(nameof(AbstractModel.InvokeExecutionFinished))!));
            bool thrown = false;
            try { CrystalReplaySeriesPatch.Transpiler(instructions, target).ToArray(); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown); return Task.CompletedTask;
        });
        Test("双语文本只给晶流添加重放分支限制，保持Th的文档语义", () => {
            foreach (string language in new[] { "zhs", "eng" }) {
                using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(args[1], "IndiumStar", "localization", language, "cards.json")));
                string flow = document.RootElement.GetProperty("INDIUM_STAR_CARD_CRYSTAL_FLOW.description").GetString()!;
                string th = document.RootElement.GetProperty("INDIUM_STAR_CARD_APPLICATION_TH.description").GetString()!;
                Check(language == "zhs" ? flow.Contains("不会触发") && !th.Contains("不会触发")
                    : flow.Contains("Does not trigger") && !th.Contains("without triggering"));
            }
            return Task.CompletedTask;
        });
        Test("重放测试资源与伤害属性枚举契约匹配实际游戏", () => {
            var directory = System.IO.Path.Combine(args[0], "data_sts2_windows_x86_64");
            var context = new System.Runtime.Loader.AssemblyLoadContext("CrystalReplayContract", true);
            context.Resolving += (_, name) => {
                string path = System.IO.Path.Combine(directory, name.Name + ".dll");
                return System.IO.File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            try {
                var assembly = context.LoadFromAssemblyPath(System.IO.Path.Combine(directory, "sts2.dll"));
                var nativeEnum = assembly.GetType(typeof(ValueProp).FullName!, true)!;
                Check(Enum.GetNames(nativeEnum).SequenceEqual(Enum.GetNames<ValueProp>()));
                Check(Enum.GetValues(nativeEnum).Cast<object>().Select(Convert.ToInt32)
                    .SequenceEqual(Enum.GetValues<ValueProp>().Select(value => (int)value)));
                var nativePlay = assembly.GetType(typeof(CardPlay).FullName!, true)!;
                Check(nativePlay.GetProperty("PlayCount")!.SetMethod!.IsPublic && nativePlay.GetProperty("IsLastInSeries") != null);
                var resources = assembly.GetType(typeof(ResourceInfo).FullName!, true)!;
                Check(resources.IsValueType && resources.GetProperties().Select(property => property.Name)
                    .SequenceEqual(typeof(ResourceInfo).GetProperties().Select(property => property.Name)));
            } finally { context.Unload(); }
            return Task.CompletedTask;
        });
    }
}

internal sealed class SeriesAudit : AbstractModel
{
    public List<CardPlay> Before { get; } = [];
    public List<int> CountAtBefore { get; } = [];
    public List<CardPlay> After { get; } = [];
    public Action<CardPlay>? OnAfter { get; init; }
    public override Task BeforeCardPlayed(CardPlay play) { Before.Add(play); CountAtBefore.Add(play.PlayCount); return Task.CompletedTask; }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play) {
        After.Add(play); OnAfter?.Invoke(play); return Task.CompletedTask;
    }
}
internal sealed class NativeReplayModifier : AbstractModel
{
    public int Calls { get; private set; }
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int count) { Calls++; return count + 1; }
}
internal sealed class PileEnergyHook(Action action) : AbstractModel
{
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPile, AbstractModel? clonedBy) {
        if (card is Crystallization) action(); return Task.CompletedTask;
    }
}
