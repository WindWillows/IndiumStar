using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class LiveBugfixTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Live bugfix assertion failed"); }
    private static void Hand(Rig r, CardModel card) {
        card.Pile = r.Player.PlayerCombatState!.Hand; card.Pile.Cards.Add(card);
    }
    private static Creature Enemy(Rig r) {
        var enemy = new Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy); return enemy;
    }
    private static async Task End(Rig r, CombatSide side, params Creature[] participants) {
        foreach (var listener in r.State.IterateHookListeners().ToArray())
            await listener.BeforeSideTurnEnd(r.Choice, side, participants);
        foreach (var listener in r.State.IterateHookListeners().OfType<PowerModel>().ToArray())
            await listener.AfterSideTurnEnd(r.Choice, side, participants);
    }
    private static async Task Electron(Rig r, Creature target, int times = 2) {
        var card = r.Crystal<ApplicationNeutron>(); Hand(r, card);
        for (int i = 0; i < times; i++) await card.OnPlayWrapper(r.Choice, target);
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests) {
        void Test(string name, Func<Task> run) => tests.Add(("实测修复 " + name, run));
        foreach (int remaining in new[] { 0, 2 }) foreach (bool upgraded in new[] { false, true }) {
            int count = remaining; bool up = upgraded;
            Test($"分影连气本体离手后包含自身 remaining={count} upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<ShadowChainBreath>(); if (up) card.Upgrade(); Hand(r, card);
                for (int i = 0; i < count; i++) Hand(r, r.Crystal<ShadowChainBreath>());
                await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, 2, null, null);
                await card.OnPlayWrapper(r.Choice, null);
                Check(CreatureCmd.BlockGains.Count == count + 1 && r.Player.Creature.Block == (count + 1) * (up ? 6 : 5)
                    && card.Pile?.Type == PileType.Exhaust);
            });
        }
        Test("分影连气直接结算仍在手牌时自身只计一次", async () => {
            var r = new Rig(); var card = r.Crystal<ShadowChainBreath>(); Hand(r, card);
            await card.Play(r.Choice, new() { Card = card }); Check(CreatureCmd.BlockGains.Count == 1);
        });
        foreach (bool upgraded in new[] { false, true }) {
            bool up = upgraded;
            Test($"晶罚全部晶化变体施加消亡粉末能力而非灾厄 upgraded={up}", async () => {
                foreach (Type type in new[] { typeof(Crystallization), typeof(CrystallizationMg), typeof(CrystallizationNb),
                    typeof(CrystallizationHs), typeof(CrystallizationPb), typeof(CrystallizationU) }) {
                    var r = new Rig(); var target = Enemy(r); var punish = r.Crystal<CrystalPunishment>(); if (up) punish.Upgrade();
                    var crystal = (CrystallizationCard)Activator.CreateInstance(type)!;
                    crystal.Owner = r.Player; crystal.CombatState = r.State; Hand(r, crystal);
                    await punish.OnPlayWrapper(r.Choice, target); await crystal.OnPlayWrapper(r.Choice, target);
                    Check(target.GetPowerAmount<DemisePower>() == (up ? 4 : 3) && target.GetPowerAmount<DoomPower>() == 0
                        && PowerCmd.LastSource == crystal && crystal.Pile?.Type == PileType.Exhaust);
                }
            });
        }
        foreach (int baseline in new[] { 0, 3 }) {
            int initial = baseline;
            Test($"电子重复虚弱仅持续本人本回合且保留其他来源 baseline={initial}", async () => {
                var r = new Rig(energy: 1); var target = Enemy(r);
                if (initial > 0) { var old = await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, initial, null, null); old.SkipNextDurationTick = false; }
                await Electron(r, target);
                Check(r.Player.Creature.GetPowerAmount<WeakPower>() == initial + 2
                    && r.Player.Creature.GetPower<LoseWeakAtTurnEndPower>() is { TemporaryAmount: 2, Type: PowerType.Buff, IsVisible: false });
                await End(r, CombatSide.Player, r.Player.Creature);
                Check(r.Player.Creature.GetPowerAmount<WeakPower>() == initial && r.Player.Creature.GetPower<LoseWeakAtTurnEndPower>() == null);
                if (r.Player.Creature.GetPower<WeakPower>() is { } weak) await PowerCmd.TickDownDuration(weak);
                Check(r.Player.Creature.GetPowerAmount<WeakPower>() == Math.Max(0, initial - 1));
            });
            Test($"电子重复易伤覆盖敌方行动但不进入下一回合或双扣 baseline={initial}", async () => {
                var r = new Rig(energy: 0); var target = Enemy(r);
                if (initial > 0) { var old = await PowerCmd.Apply<VulnerablePower>(r.Choice, r.Player.Creature, initial, null, null); old.SkipNextDurationTick = false; }
                await Electron(r, target); await End(r, CombatSide.Player, r.Player.Creature);
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == initial + 2);
                await End(r, CombatSide.Enemy, target);
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == initial && r.Player.Creature.GetPower<LoseVulnerableAtTurnStartPower>() == null);
                if (r.Player.Creature.GetPower<VulnerablePower>() is { } vulnerable) await PowerCmd.TickDownDuration(vulnerable);
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == Math.Max(0, initial - 1));
            });
        }
        Test("电子负面受阻不产生倒扣额度", async () => {
            foreach (int energy in new[] { 0, 1 }) {
                var r = new Rig(energy: energy); var target = Enemy(r);
                PowerCmd.ModifyAmountReceived = (power, amount) => power is WeakPower or VulnerablePower ? 0 : amount;
                await Electron(r, target, 1);
                Check(r.Player.Creature.GetPowerAmount<WeakPower>() == 0 && r.Player.Creature.GetPowerAmount<VulnerablePower>() == 0
                    && !r.Player.Creature.Powers.Any(p => p is LoseWeakAtTurnEndPower or LoseVulnerableAtTurnStartPower));
            }
        });
        Test("电子负面提前减层后不误删既有层数", async () => {
            var r = new Rig(energy: 1); var target = Enemy(r);
            await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, 3, null, null); await Electron(r, target);
            await PowerCmd.Decrement(r.Player.Creature.GetPower<WeakPower>()!);
            Check(r.Player.Creature.GetPower<LoseWeakAtTurnEndPower>()?.TemporaryAmount == 1);
            await End(r, CombatSide.Player, r.Player.Creature); Check(r.Player.Creature.GetPowerAmount<WeakPower>() == 3);
        });
        Test("电子负面被完全清除后不会误删外部新实例", async () => {
            var r = new Rig(energy: 1); var target = Enemy(r); await Electron(r, target, 1);
            await PowerCmd.Remove(r.Player.Creature.GetPower<WeakPower>()!);
            await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, 2, null, null);
            await End(r, CombatSide.Player, r.Player.Creature); Check(r.Player.Creature.GetPowerAmount<WeakPower>() == 2);
        });
        Test("电子他人回合不能清除本人的虚弱", async () => {
            var r = new Rig(energy: 1); var target = Enemy(r); await Electron(r, target, 1);
            await End(r, CombatSide.Player, new MegaCrit.Sts2.Core.Entities.Players.Player().Creature);
            Check(r.Player.Creature.GetPowerAmount<WeakPower>() == 1);
        });
        Test("电子特殊跳回合的开始兜底不清除同回合新效果", async () => {
            foreach (int energy in new[] { 0, 1 }) {
                var r = new Rig(energy: energy); var target = Enemy(r); await Electron(r, target, 1);
                var expiry = r.Player.Creature.Powers.Single(p => p is LoseVulnerableAtTurnStartPower or LoseWeakAtTurnEndPower);
                await expiry.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() + r.Player.Creature.GetPowerAmount<WeakPower>() == 1);
                r.Player.PlayerCombatState!.TurnNumber++;
                await expiry.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
                Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() + r.Player.Creature.GetPowerAmount<WeakPower>() == 0);
            }
        });
    }
}
