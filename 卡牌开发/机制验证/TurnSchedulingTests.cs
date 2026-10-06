using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

public static class TurnSchedulingTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Turn scheduling assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile) {
        card.Pile?.Cards.Remove(card); card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card);
    }
    private static void Fill(Rig r, int count) { for (int i = 0; i < count; i++) Put(r, r.Crystal<TestAttack>(), PileType.Draw); }
    private static Creature Enemy(Rig r) { var enemy = new Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy); return enemy; }
    private static async Task<ReminiscenceDrawPower> Play(Rig r, bool upgrade = false) {
        var card = r.Crystal<ReminiscenceVanadiumEternal>(); if (upgrade) card.Upgrade();
        await card.Play(r.Choice, new() { Card = card }); return r.Player.Creature.GetPower<ReminiscenceDrawPower>()!;
    }
    private static Task End(Rig r, ReminiscenceDrawPower power) => power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
    private static async Task Begin(Rig r, ReminiscenceDrawPower power) {
        r.Player.PlayerCombatState!.TurnNumber++;
        await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (bool upgraded in new[] { false, true }) foreach (int available in new[] { 0, 1, 5 }) {
            bool up = upgraded; int count = available;
            Test($"钒永只统计实际抽到牌 available={count} upgraded={up}", async () => {
                var r = new Rig(); Fill(r, count); var power = await Play(r, up); await End(r, power);
                int actual = 0; Check(power.Amount == actual);
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == Math.Min(count, up ? 2 : 1));
                Check(power.ModifyHandDraw(r.Player, 5) == 5); // 尚未到本人下一回合。
                await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 5 + actual);
                if (actual > 0) await power.AfterModifyingHandDraw();
                else { await power.AfterModifyingHandDraw(); Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power); }
                await power.AfterPlayerTurnStart(r.Choice, r.Player);
                Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == null);
                Check(power.ModifyHandDraw(r.Player, 5) == 5);
            });
        }
        Test("钒永抽牌被阻止不会预约不存在的牌", async () => {
            var r = new Rig(); Fill(r, 8); CardPileCmd.DrawBlocked = true; var power = await Play(r, true); await End(r, power);
            Check(power.Amount == 0); await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 5);
            await power.AfterPlayerTurnStart(r.Choice, r.Player); Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == null);
        });
        Test("钒永满手不把抽牌指令请求数当作实际抽牌数", async () => {
            var r = new Rig(); Fill(r, 3); for (int i = 0; i < 10; i++) Put(r, r.Crystal<TestAttack>(), PileType.Hand);
            var power = await Play(r, true); await End(r, power); Check(power.Amount == 0);
        });
        Test("钒永首次自身和生效前抽牌不计入，只统计生效后来源", async () => {
            var r = new Rig(); Fill(r, 10); await CardPileCmd.Draw(r.Choice, 2, r.Player);
            var power = await Play(r); await CardPileCmd.Draw(r.Choice, 3, r.Player); await End(r, power);
            Check(power.Amount == 3); await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 8);
        });
        Test("钒永多份效果不叠加，每张实际抽取仅计一次", async () => {
            var r = new Rig(); Fill(r, 9); var power = await Play(r); Check(await Play(r) == power);
            await CardPileCmd.Draw(r.Choice, 2, r.Player); await End(r, power); Check(power.Amount == 3);
            await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 8);
            Check(r.Player.Creature.Powers.OfType<ReminiscenceDrawPower>().Count() == 1);
        });
        Test("钒永生成和入手牌不计为抽取，其他玩家抽取也不计入", async () => {
            var r = new Rig(); Fill(r, 6); var power = await Play(r);
            var generated = r.Crystal<TestAttack>(); await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, r.Player);
            var other = new Player(); var foreign = r.Crystal<TestAttack>(); foreign.Owner = other;
            await power.AfterCardDrawn(r.Choice, foreign, false); await End(r, power); Check(power.Amount == 0);
        });
        Test("钒永同阵营其他玩家结束或开始回合不覆盖本人预约", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); var other = new Player();
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { other.Creature });
            await CardPileCmd.Draw(r.Choice, 2, r.Player); await End(r, power);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { other.Creature });
            await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { other.Creature }, r.State);
            await power.AfterSideTurnStart(CombatSide.Player, new[] { other.Creature }, r.State);
            Check(power.Amount == 2 && r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
            await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 7 && power.ModifyHandDraw(other, 5) == 5);
        });
        Test("钒永重复结束回调不会把预约数清零", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 2, r.Player);
            await End(r, power); await End(r, power); Check(power.Amount == 2);
        });
        Test("钒永回合末晚抽牌连锁仍计入，下一阵营开始后封存", async () => {
            var r = new Rig(); Fill(r, 9); var power = await Play(r); await End(r, power);
            await CardPileCmd.Draw(r.Choice, 2, r.Player); Check(power.Amount == 2);
            await power.BeforeSideTurnStart(r.Choice, CombatSide.Enemy, new[] { Enemy(r) }, r.State);
            await CardPileCmd.Draw(r.Choice, 1, r.Player); await Begin(r, power);
            Check(power.ModifyHandDraw(r.Player, 5) == 7);
        });
        Test("钒永在普通结束钩子遍历之后生效仍能预约下回合", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player);
            // 新能力未收到已过去的End遍历，下一侧开始时必须封存。
            await power.BeforeSideTurnStart(r.Choice, CombatSide.Enemy, new[] { Enemy(r) }, r.State);
            await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 6);
        });
        Test("钒永全局回合推进不能替代本人回合推进", async () => {
            var r = new Rig(); Fill(r, 6); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); r.State.Turn = 100;
            Check(power.ModifyHandDraw(r.Player, 5) == 5); await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 6);
        });
        Test("钒永预览修改抽牌数是纯读，不提前消费预约", async () => {
            var r = new Rig(); Fill(r, 6); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            Check(power.ModifyHandDraw(r.Player, 5) == 6 && power.ModifyHandDraw(r.Player, 5) == 6);
            Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
            await power.AfterModifyingHandDraw(); Check(power.ModifyHandDraw(r.Player, 5) == 5);
        });
        Test("钒永本人开始阶段未完成时阵营开始钩子不会提前移除", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power && power.ModifyHandDraw(r.Player, 5) == 6);
        });
        Test("钒永零预约也等待本人开始完成，其他玩家开始不清理", async () => {
            var r = new Rig(); var power = await Play(r); await End(r, power); await Begin(r, power);
            await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            await power.AfterPlayerTurnStart(r.Choice, new Player()); Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
            await power.AfterPlayerTurnStart(r.Choice, r.Player); Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == null);
        });
        Test("钒永下回合抽牌前再次生效，旧预约消费保留新统计", async () => {
            var r = new Rig(); Fill(r, 15); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            Check(await Play(r) == power); decimal draw = power.ModifyHandDraw(r.Player, 5); Check(draw == 6);
            await power.AfterModifyingHandDraw(); Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
            await CardPileCmd.Draw(r.Choice, draw, r.Player, fromHandDraw: true);
            await power.AfterPlayerTurnStart(r.Choice, r.Player); await End(r, power);
            Check(power.Amount == 6); await Begin(r, power); Check(power.ModifyHandDraw(r.Player, 5) == 11);
        });
        Test("钒永普通开局抽牌不会自动开启下一份统计", async () => {
            var r = new Rig(); Fill(r, 15); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            decimal draw = power.ModifyHandDraw(r.Player, 5); await power.AfterModifyingHandDraw();
            await CardPileCmd.Draw(r.Choice, draw, r.Player, true); await power.AfterPlayerTurnStart(r.Choice, r.Player);
            Check(r.Player.Creature.GetPower<ReminiscenceDrawPower>() == null && power.ModifyHandDraw(r.Player, 5) == 5);
        });
        Test("钒永旧预约为零时的新统计不会被本人开始回调移除", async () => {
            var r = new Rig(); var power = await Play(r); await End(r, power); await Begin(r, power); Fill(r, 8);
            Check(await Play(r) == power); await CardPileCmd.Draw(r.Choice, 1, r.Player); await power.AfterPlayerTurnStart(r.Choice, r.Player); await End(r, power);
            Check(power.Amount == 1 && r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
        });
        Test("钒永与预约自动打出及抽牌选择暂停组合，不提前消费旧预约", async () => {
            var r = new Rig(); Fill(r, 15); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power);
            var card = r.Crystal<ReminiscenceVanadiumEternal>(); Put(r, card, PileType.Exhaust);
            var deferred = await PowerCmd.Apply<DeferredReleasePower>(r.Choice, r.Player.Creature, 1, null, null);
            deferred.Schedule(card, 2, r.State); await Begin(r, power);
            var pause = new PauseOnDraw(); r.State.Listeners.Add(pause);
            Task pending = deferred.BeforeHandDraw(r.Player, r.Choice, r.State);
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(!pending.IsCompleted); await power.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            Check(power.ModifyHandDraw(r.Player, 5) == 6 && r.Player.Creature.GetPower<ReminiscenceDrawPower>() == power);
            pause.Resume.SetResult(); await pending; r.State.Listeners.Remove(pause);
            decimal count = power.ModifyHandDraw(r.Player, 5); await power.AfterModifyingHandDraw();
            await CardPileCmd.Draw(r.Choice, count, r.Player, true); await power.AfterPlayerTurnStart(r.Choice, r.Player);
            await End(r, power); Check(power.Amount == 6 && card.Pile?.Type == PileType.Discard);
        });
        Test("钒永预约不跨入旧战斗或死亡玩家的开始结算", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            r.Player.Creature.IsAlive = false; Check(power.ModifyHandDraw(r.Player, 5) == 5);
            r.Player.Creature.IsAlive = true; var newCombat = new FakeCombat(); r.Player.Creature.CombatState = newCombat;
            Check(power.ModifyHandDraw(r.Player, 5) == 5); await power.AfterPlayerTurnStart(r.Choice, r.Player);
            Check(power.Amount == 1);
        });
        Test("钒永计数显示跟随实际抽牌，不把再次施加显示为旧预约加一", async () => {
            var r = new Rig(); Fill(r, 15); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 2, r.Player);
            Check(power.Amount == 2); await End(r, power); await Begin(r, power); Check(await Play(r) == power);
            Check(power.Amount == 2); await power.AfterModifyingHandDraw(); Check(power.Amount == 0);
            await CardPileCmd.Draw(r.Choice, 1, r.Player); Check(power.Amount == 1);
        });
        Test("钒永队列克隆后的消费不修改原能力字段", async () => {
            var r = new Rig(); Fill(r, 8); var power = await Play(r); await CardPileCmd.Draw(r.Choice, 1, r.Player); await End(r, power); await Begin(r, power);
            var copy = (ReminiscenceDrawPower)power.ToMutable(); await copy.AfterModifyingHandDraw();
            Check(copy.ModifyHandDraw(r.Player, 5) == 5 && power.ModifyHandDraw(r.Player, 5) == 6);
        });
        foreach (bool upgrade in new[] { false, true }) {
            bool up = upgrade;
            Test($"气相涌流实际保留才成长且每本人回合一次 upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<GasPhaseSurge>(); if (up) card.Upgrade();
                await card.AfterFlush(r.Choice, r.Player, new[] { card }, Array.Empty<CardModel>()); Check(card.DynamicVars.Damage.BaseValue == 6);
                await card.AfterFlush(r.Choice, new Player(), Array.Empty<CardModel>(), new[] { card }); Check(card.DynamicVars.Damage.BaseValue == 6);
                await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card });
                await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card });
                Check(card.DynamicVars.Damage.BaseValue == (up ? 15 : 12));
            });
        }
        Test("气相涌流他人同阵营开始不重置成长次数", async () => {
            var r = new Rig(); var card = r.Crystal<GasPhaseSurge>();
            await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card });
            await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { new Player().Creature }, r.State);
            await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card }); Check(card.DynamicVars.Damage.BaseValue == 12);
            await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
            await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card }); Check(card.DynamicVars.Damage.BaseValue == 18);
        });
        foreach (PileType pile in new[] { PileType.Discard, PileType.Draw, PileType.Exhaust }) {
            PileType location = pile;
            Test($"气相涌流晶抗预约只在本人开始从{location}返回一次", async () => {
                var r = new Rig(); var card = r.Crystal<GasPhaseSurge>(); await card.Play(r.Choice, new() { Card = card, Target = Enemy(r) });
                Put(r, card, location); await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { new Player().Creature }, r.State);
                Check(card.Pile?.Type == location);
                await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(card.Pile?.Type == PileType.Hand && r.Player.PlayerCombatState!.Hand.Cards.Count(value => value == card) == 1);
                Put(r, card, PileType.Discard); await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(card.Pile?.Type == PileType.Discard);
            });
        }
        Test("气相涌流拒绝敌方或其他战斗的开始回调", async () => {
            var r = new Rig(); var card = r.Crystal<GasPhaseSurge>(); await card.Play(r.Choice, new() { Card = card, Target = Enemy(r) }); Put(r, card, PileType.Discard);
            await card.BeforeSideTurnStart(r.Choice, CombatSide.Enemy, new[] { r.Player.Creature }, r.State);
            await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, new FakeCombat()); Check(card.Pile?.Type == PileType.Discard);
            await card.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State); Check(card.Pile?.Type == PileType.Hand);
        });
        Test("气相涌流晶逆获得战斗内保留而不是自带保留", async () => {
            var r = new Rig(energy: 0); var card = r.Crystal<GasPhaseSurge>(); Check(!card.Keywords.Contains(CardKeyword.Retain));
            await card.Play(r.Choice, new() { Card = card, Target = Enemy(r) }); Check(card.Keywords.Contains(CardKeyword.Retain));
        });
        Test("气相涌流成长克隆继承数值但不写回牌组或共享动态变量", async () => {
            var r = new Rig(); var deck = r.Crystal<GasPhaseSurge>(); var card = (GasPhaseSurge)deck.CreateClone(); card.DeckVersion = deck;
            await card.AfterFlush(r.Choice, r.Player, Array.Empty<CardModel>(), new[] { card }); var copy = card.CreateClone(); copy.DynamicVars.Damage.BaseValue++;
            Check(deck.DynamicVars.Damage.BaseValue == 6 && card.DynamicVars.Damage.BaseValue == 12 && copy.DynamicVars.Damage.BaseValue == 13);
        });
        Test("新增回合钩子与当前游戏DLL契约一致", () => {
            var directory = System.IO.Path.Combine(args[0], "data_sts2_windows_x86_64");
            var context = new System.Runtime.Loader.AssemblyLoadContext("TurnSchedulingContract", true);
            context.Resolving += (_, name) => { string path = System.IO.Path.Combine(directory, name.Name + ".dll"); return System.IO.File.Exists(path) ? context.LoadFromAssemblyPath(path) : null; };
            try {
                var assembly = context.LoadFromAssemblyPath(System.IO.Path.Combine(directory, "sts2.dll"));
                var model = assembly.GetType(typeof(AbstractModel).FullName!, true)!;
                Check(model.GetMethod("AfterModifyingHandDraw")!.ReturnType == typeof(Task) && model.GetMethod("AfterModifyingHandDraw")!.GetParameters().Length == 0);
                var start = model.GetMethod("AfterPlayerTurnStart")!; Check(start.ReturnType == typeof(Task)
                    && start.GetParameters().Select(parameter => parameter.ParameterType.Name).SequenceEqual(new[] { "PlayerChoiceContext", "Player" }));
                var draw = assembly.GetType("MegaCrit.Sts2.Core.Commands.CardPileCmd", true)!.GetMethods()
                    .Single(method => method.Name == "Draw" && method.GetParameters()[1].ParameterType == typeof(decimal));
                Check(draw.GetParameters().Any(parameter => parameter.Name == "fromHandDraw" && parameter.ParameterType == typeof(bool)));
            } finally { context.Unload(); }
            return Task.CompletedTask;
        });
    }
}
internal sealed class PauseOnDraw : AbstractModel
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _used;
    public override async Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw) {
        if (_used) return; _used = true; Started.TrySetResult(); await Resume.Task;
    }
}
