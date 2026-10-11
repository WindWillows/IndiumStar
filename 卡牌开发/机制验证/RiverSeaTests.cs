using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

// Source effects and command/history fixtures; no native live combat or complete run-save validation.
public static class RiverSeaTests
{
    private static void Check(bool value) { if (!value) throw new Exception("River/Sea assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    private static void Finish(Rig r, CardModel card, bool auto = false, int index = 0)
    { CombatManager.Instance.History.CardPlaysFinished.Add(new(r.State, r.State.Turn, new() { Card = card, IsAutoPlay = auto, PlayIndex = index })); }
    private static StellarSea Deck(Rig r, int level = 0)
    { var card = r.Crystal<StellarSea>(); for (int i = 0; i < level; i++) card.Upgrade(); Put(r, card, PileType.Deck); return card; }
    private static StellarSea Combat(StellarSea deck)
    { var card = (StellarSea)deck.Clone(); card.DeckVersion = deck; card.Pile = null; return card; }

    public static void Register(List<(string Name, Func<Task> Run)> tests)
    {
        void Test(string name, Func<Task> run) => tests.Add(("渊河辰海：" + name, run));
        foreach (PileType value in new[] { PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play })
        foreach (bool automatic in new[] { false, true })
        {
            PileType pile = value; bool auto = automatic;
            Test($"渊河普通或自动晶化从{pile}唤回且不重复入手 auto={auto}", async () => {
                var r = new Rig(); var river = r.Crystal<AbyssalRiver>(); Put(r, river, pile);
                var crystal = r.Crystal<Crystallization>(); var play = new CardPlay { Card = crystal, IsAutoPlay = auto };
                await river.AfterCardPlayedLate(r.Choice, play); await river.AfterCardPlayedLate(r.Choice, play);
                Check(river.Pile?.Type == PileType.Hand && r.Player.PlayerCombatState!.Hand.Cards.Count(c => c == river) == 1
                    && river.EnergyCost.Value == 0 && river.IsUpgradable && river.DynamicVars.Damage.BaseValue == 5);
            });
        }
        Test("渊河排除他人、非晶化、异战斗及结束状态", async () => {
            var r = new Rig(); var river = r.Crystal<AbyssalRiver>(); Put(r, river, PileType.Discard);
            await river.AfterCardPlayedLate(r.Choice, new() { Card = r.Crystal<TestAttack>() });
            var other = r.Crystal<Crystallization>(); other.Owner = new Player();
            await river.AfterCardPlayedLate(r.Choice, new() { Card = other });
            other.Owner = r.Player; other.CombatState = new FakeCombat();
            await river.AfterCardPlayedLate(r.Choice, new() { Card = other });
            CombatManager.Instance.IsOverOrEnding = true;
            await river.AfterCardPlayedLate(r.Choice, new() { Card = r.Crystal<Crystallization>() });
            Check(river.Pile?.Type == PileType.Discard);
        });
        Test("渊河满手沿原生Add溢出，不复制或移除卡牌", async () => {
            var r = new Rig(); var river = r.Crystal<AbyssalRiver>(); Put(r, river, PileType.Exhaust);
            for (int i = 0; i < 10; i++) Put(r, r.Crystal<TestAttack>(), PileType.Hand);
            await river.AfterCardPlayedLate(r.Choice, new() { Card = r.Crystal<Crystallization>() });
            Check(river.Pile?.Type == PileType.Discard && r.Player.PlayerCombatState!.AllCards.Count(c => c == river) == 1);
        });
        Test("渊河自身攻击的晶化连锁唤回后，最终不再丢入弃牌堆", async () => {
            var r = new Rig(); var river = r.Crystal<AbyssalRiver>(); Put(r, river, PileType.Hand);
            var crystal = r.Crystal<Crystallization>(); Put(r, crystal, PileType.Hand);
            DamageCmd.AfterAttackExecuted = _ => crystal.OnPlayWrapper(r.Choice, isAutoPlay: true);
            await river.OnPlayWrapper(r.Choice, new() { CombatState = r.State });
            Check(DamageCmd.Attacks.Single().Damage == 7 && river.Pile?.Type == PileType.Hand && crystal.Pile?.Type == PileType.Exhaust);
        });
        foreach (var value in new[] { (Level: 0, Plays: 0), (Level: 0, Plays: 1), (Level: 2, Plays: 2), (Level: 2, Plays: 3) })
        {
            var data = value;
            Test($"辰海严格大于升级等级才永久升级一次 level={data.Level} plays={data.Plays}", async () => {
                var r = new Rig(); var deck = Deck(r, data.Level); var card = Combat(deck);
                for (int i = 0; i < data.Plays; i++) Finish(r, card);
                await deck.AfterCombatEnd(new() { CombatState = r.State });
                int expected = data.Level + (data.Plays > data.Level ? 1 : 0);
                Check(deck.CurrentUpgradeLevel == expected && deck.DynamicVars.Damage.BaseValue == 10 + 2 * expected && card.CurrentUpgradeLevel == data.Level);
            });
        }
        Test("辰海自动和重放计入完成历史，原件与副本回调不重复升级", async () => {
            var r = new Rig(); var deck = Deck(r); var card = Combat(deck); Put(r, card, PileType.Discard);
            Finish(r, card, auto: true); Finish(r, card, auto: true, index: 1);
            var room = new CombatRoom { CombatState = r.State };
            await deck.AfterCombatEnd(room); await card.AfterCombatEnd(room); await deck.AfterCombatEnd(room);
            Check(deck.CurrentUpgradeLevel == 1 && card.CurrentUpgradeLevel == 0);
        });
        Test("辰海独立原件、战斗复制、未完成和他人出牌不混计", async () => {
            var r = new Rig(); var a = Deck(r, 1); var b = Deck(r, 1); var ac = Combat(a); var bc = Combat(b);
            Finish(r, ac); Finish(r, bc); Finish(r, bc);
            var independent = ac.CreateClone(); Finish(r, independent); Finish(r, independent);
            var foreign = Combat(a); foreign.Owner = new Player(); Finish(r, foreign);
            r.Record(ac); // 未完成不计入。
            var room = new CombatRoom { CombatState = r.State }; await a.AfterCombatEnd(room); await b.AfterCombatEnd(room);
            Check(a.CurrentUpgradeLevel == 1 && b.CurrentUpgradeLevel == 2);
        });
        Test("辰海每场计数重置且新战斗可再次升级", async () => {
            var r = new Rig(); var deck = Deck(r); var card = Combat(deck); var room = new CombatRoom { CombatState = r.State };
            Finish(r, card); await deck.AfterCombatEnd(room);
            CombatManager.Instance.History.CardPlaysFinished.Clear(); await deck.BeforeCombatStart();
            await deck.AfterCombatEnd(room); Check(deck.CurrentUpgradeLevel == 1);
            await deck.BeforeCombatStart(); Finish(r, card); Finish(r, card); await deck.AfterCombatEnd(room);
            Check(deck.CurrentUpgradeLevel == 2 && deck.DynamicVars.Damage.BaseValue == 14);
        });
        Test("辰海多次升级累加2伤害、保持1费和独立克隆", async () => {
            var r = new Rig(); var card = r.Crystal<StellarSea>(); for (int i = 0; i < 6; i++) card.Upgrade();
            var copy = card.CreateClone(); copy.Upgrade();
            await card.Play(r.Choice, new() { Card = card, Target = new() { CombatState = r.State } });
            Check(card.MaxUpgradeLevel == int.MaxValue && card.CurrentUpgradeLevel == 6 && card.EnergyCost.Value == 1
                && DamageCmd.Attacks.Single().Damage == 22 && copy.CurrentUpgradeLevel == 7 && copy.DynamicVars.Damage.BaseValue == 24);
        });
    }
}

namespace MegaCrit.Sts2.Core.Rooms
{
    public class CombatRoom { public ICombatState CombatState { get; set; } = null!; }
}
