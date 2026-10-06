using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// A short check of the two newly implemented cards. Commands/turn transitions are fixtures, not a live battle.
public static class RemainingXCardTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("X card assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile = PileType.Hand)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    private static async Task<CrystalMarrowPower> MarrowTurn(Rig r)
    {
        var power = new CrystalMarrowPower { Owner = r.Player.Creature, Amount = 1 };
        r.Player.Creature.Powers.Add(power);
        power.Schedule(r.Player.PlayerCombatState!.TurnNumber + 1);
        r.Player.PlayerCombatState.TurnNumber++;
        await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
        return power;
    }

    public static void Register(List<(string Name, Func<Task> Run)> tests)
    {
        void Test(string name, Func<Task> run) => tests.Add(("X卡：" + name, run));
        foreach (int xValue in new[] { 0, 2 }) foreach (bool upgrade in new[] { false, true })
        {
            int x = xValue; bool up = upgrade;
            Test($"隧穿捕获X、升级与消耗 x={x} upgraded={up}", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<Tunneling>(); if (up) card.Upgrade();
                card.EnergyCost.CapturedXValue = x; PlayerCmd.LastTurnEnded = null; Put(r, card);
                await card.OnPlayWrapper(r.Choice);
                var power = r.Player.Creature.GetPower<TunnelingPower>()!;
                Check(power.PendingLimits.SequenceEqual(new[] { x + (up ? 1 : 0) }));
                Check(PlayerCmd.LastTurnEnded == r.Player && card.Pile?.Type == PileType.Exhaust);
                r.Player.PlayerCombatState!.TurnNumber++;
                await power.AfterTakingExtraTurn(r.Player);
                Check(power.CardLimit == x + (up ? 1 : 0)
                    && power.ShouldPlay(r.Card, AutoPlayType.Default) == (power.CardLimit > 0));
            });
            Test($"晶髓捕获X、延拓与额外抽牌 x={x} upgraded={up}", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<CrystalMarrow>(); if (up) card.Upgrade();
                card.EnergyCost.CapturedXValue = x; Put(r, card);
                await card.OnPlayWrapper(r.Choice);
                Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == x
                    && r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == x + (up ? 1 : 0));
                var power = r.Player.Creature.GetPower<CrystalMarrowPower>()!;
                Check(power.PendingTurns.SequenceEqual(new[] { r.Player.PlayerCombatState!.TurnNumber + 1 })
                    && power.ActiveTurn == -1 && card.Pile?.Type == PileType.Discard);
            });
        }
        Test("两张X牌采用X修正而非结算后能量", async () => {
            var r = new Rig(energy: 0); var tunnel = r.Crystal<Tunneling>();
            tunnel.EnergyCost.CapturedXValue = 1; tunnel.EnergyCost.XModifier = 2;
            await tunnel.Play(r.Choice, new() { Card = tunnel });
            Check(r.Player.Creature.GetPower<TunnelingPower>()!.PendingLimits.Single() == 3);
            var marrow = r.Crystal<CrystalMarrow>(); marrow.EnergyCost.CapturedXValue = 1; marrow.EnergyCost.XModifier = 2;
            await marrow.Play(r.Choice, new() { Card = marrow });
            Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 3);
        });
        Test("隧穿在效果前计数，自动首段计入而重放不另计", async () => {
            var r = new Rig(); var power = new TunnelingPower { Owner = r.Player.Creature };
            power.Enqueue(1); r.Player.PlayerCombatState!.TurnNumber++;
            await power.AfterTakingExtraTurn(r.Player);
            await power.BeforeCardPlayed(new() { Card = r.Card, IsAutoPlay = true });
            Check(!power.ShouldPlay(r.Card, AutoPlayType.Default));
            await power.BeforeCardPlayed(new() { Card = r.Card, PlayIndex = 1 });
            Check(power.CardsPlayed == 1);
            var other = new Player(); Check(power.ShouldPlay(new CrystalA { Owner = other }, AutoPlayType.Default));
        });
        Test("隧穿连续预约保持各回合独立上限，非本人回合不清除", async () => {
            var r = new Rig(); var power = new TunnelingPower { Owner = r.Player.Creature };
            r.Player.Creature.Powers.Add(power); power.Enqueue(1); power.Enqueue(3);
            var other = new Player(); await power.AfterTakingExtraTurn(other);
            Check(power.PendingLimits.Length == 2);
            r.Player.PlayerCombatState!.TurnNumber++; await power.AfterTakingExtraTurn(r.Player);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [other.Creature]);
            Check(power.CardLimit == 1 && power.PendingLimits.Single() == 3);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            Check(power.ShouldTakeExtraTurn(r.Player));
            r.Player.PlayerCombatState.TurnNumber++; await power.AfterTakingExtraTurn(r.Player);
            Check(power.CardLimit == 3 && power.CardsPlayed == 0);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            Check(r.Player.Creature.GetPower<TunnelingPower>() == null);
        });
        Test("晶髓只加费于本回合本人可打出的固定费手牌", async () => {
            var r = new Rig(); var power = await MarrowTurn(r); Put(r, r.Card);
            Check(power.TryModifyEnergyCostInCombat(r.Card, 0, out var cost) && cost == 1);
            r.Card.EnergyCost.CostsX = true;
            Check(!power.TryModifyEnergyCostInCombat(r.Card, 0, out _));
            r.Card.EnergyCost.CostsX = false; r.Card.AddKeyword(CardKeyword.Unplayable);
            Check(!power.TryModifyEnergyCostInCombat(r.Card, 0, out _));
            var outside = r.Crystal<CrystalA>(); Put(r, outside, PileType.Discard);
            Check(!power.TryModifyEnergyCostInCombat(outside, 1, out _));
            var foreign = new CrystalA { Owner = new Player() };
            Check(!power.TryModifyEnergyCostInCombat(foreign, 0, out _));
        });
        Test("晶髓重放采用原生系列，手牌自动打出适用，弃牌堆直接打出不适用", async () => {
            var r = new Rig(); await MarrowTurn(r);
            var hand = r.Crystal<CrystalA>(); Put(r, hand);
            await hand.OnPlayWrapper(r.Choice, isAutoPlay: true);
            Check(hand.FinishedCount == 2 && hand.SeriesCleanupCount == 1);
            var discard = r.Crystal<CrystalA>(); Put(r, discard, PileType.Discard);
            await discard.OnPlayWrapper(r.Choice, isAutoPlay: true);
            Check(discard.FinishedCount == 1);
        });
        Test("晶髓与实际晶逆条件重放相加，没有重复预约异常", async () => {
            var r = new Rig(energy: 0); await MarrowTurn(r);
            var flow = r.Crystal<CrystalFlow>(); Put(r, flow);
            var enemy = new MegaCrit.Sts2.Core.Entities.Creatures.Creature { CombatState = r.State };
            r.State.CreatureList.Add(enemy);
            await flow.OnPlayWrapper(r.Choice, enemy);
            Check(flow.FinishedCount == 3 && flow.SeriesCleanupCount == 1);
        });
        Test("晶髓本回合新预约留到下一回合，重复赋予只给予重放1", async () => {
            var r = new Rig(); var power = await MarrowTurn(r);
            int next = r.Player.PlayerCombatState!.TurnNumber + 1;
            power.Schedule(next); power.Schedule(next);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            Check(power.ActiveTurn == -1 && power.PendingTurns.SequenceEqual(new[] { next }));
            r.Player.PlayerCombatState.TurnNumber++;
            await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
            var card = r.Crystal<CrystalA>(); Put(r, card); await card.OnPlayWrapper(r.Choice);
            Check(card.FinishedCount == 2);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            Check(r.Player.Creature.GetPower<CrystalMarrowPower>() == null);
        });
        Test("晶髓预约不会在其他玩家的额外回合提前生效", async () => {
            var r = new Rig(); var power = new CrystalMarrowPower { Owner = r.Player.Creature };
            power.Schedule(r.Player.PlayerCombatState!.TurnNumber + 1);
            await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, [new Player().Creature], r.State);
            Check(power.ActiveTurn == -1 && power.PendingTurns.Length == 1);
            Put(r, r.Card); Check(!power.TryModifyEnergyCostInCombat(r.Card, 1, out _));
        });
    }
}
