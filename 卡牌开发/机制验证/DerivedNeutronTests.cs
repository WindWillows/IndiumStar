using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

public static class DerivedNeutronTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Derived Neutron assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile) {
        card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card);
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (bool upgrade in new[] { false, true }) foreach (int energy in new[] { 0, 1 }) {
            bool up = upgrade; int e = energy;
            Test($"运用-中子随机牌结算后抽弃互斥 energy={e} upgraded={up}", async () => {
                var r = new Rig(energy: e); var neutron = r.Crystal<DerivedApplicationNeutron>(); if (up) neutron.Upgrade();
                Put(r, neutron, PileType.Hand);
                var selected = r.Crystal<CrystalA>(); Put(r, selected, PileType.Discard);
                var held = r.Crystal<CrystalA>(); Put(r, held, PileType.Hand);
                var draw = r.Crystal<CrystalA>(); Put(r, draw, PileType.Draw);
                bool selectedAfterPlay = false;
                CardSelectCmd.SelectionOverride = (context, owner, prefs, filter, source) => {
                    selectedAfterPlay = true;
                    Check(context == r.Choice && source == neutron && owner == r.Player && prefs.Count == 1);
                    Check(selected.Pile?.Type == PileType.Exhaust && !owner.PlayerCombatState!.Hand.Cards.Contains(neutron));
                    return Task.FromResult<IEnumerable<CardModel>>(new[] { held });
                };
                await neutron.OnPlayWrapper(r.Choice);
                Check(selected.Pile?.Type == PileType.Exhaust && neutron.Pile?.Type == PileType.Discard);
                Check(r.Energy == e && CardPileCmd.DrawCount == e && selectedAfterPlay == (e == 0));
                Check(held.Pile?.Type == (e == 0 ? PileType.Discard : PileType.Hand));
                Check(draw.Pile?.Type == (e == 0 ? PileType.Draw : PileType.Hand));
                Check(neutron.EnergyCost.Value == (up ? 0 : 1) && neutron.Type == CardType.Skill
                    && ApplicationRules.IsApplicationCard(neutron));
                Check(CombatManager.Instance.History.CardPlaysStarted.Count == 2
                    && CombatManager.Instance.History.CardPlaysStarted.Count(p => p.CardPlay.IsAutoPlay) == 1);
            });
        }
        foreach (int before in new[] { 0, 1 }) {
            int start = before;
            Test($"运用-中子包含嵌套出牌结束钩子的能量变化 initial={start}", async () => {
                var r = new Rig(energy: start); var neutron = r.Crystal<DerivedApplicationNeutron>();
                Put(r, neutron, PileType.Hand); var selected = r.Crystal<CrystalA>(); Put(r, selected, PileType.Discard);
                var held = r.Crystal<CrystalA>(); Put(r, held, PileType.Hand);
                r.State.Listeners.Add(new AfterPlayObserver(selected, () => r.Energy = 1 - start));
                await neutron.OnPlayWrapper(r.Choice);
                Check(CardPileCmd.DrawCount == 1 - start && r.Energy == 1 - start);
                Check(held.Pile?.Type == (start == 1 ? PileType.Discard : PileType.Hand));
            });
        }
        foreach (int cost in new[] { -1, 0, 1, 2 }) {
            int amount = cost;
            Test($"运用-中子只选择当前固定0或1费牌 cost={amount}", async () => {
                var r = new Rig(); var neutron = r.Crystal<DerivedApplicationNeutron>();
                var selected = r.Crystal<CrystalA>(); selected.EnergyCost.Value = amount; Put(r, selected, PileType.Discard);
                await neutron.OnPlayWrapper(r.Choice);
                Check(selected.Pile?.Type == (amount is 0 or 1 ? PileType.Exhaust : PileType.Discard));
                Check(CardPileCmd.DrawCount == 1);
            });
        }
        foreach (bool x in new[] { false, true }) {
            bool isX = x;
            Test($"运用-中子跳过X费用及不可打出牌 X={isX}", async () => {
                var r = new Rig(); var neutron = r.Crystal<DerivedApplicationNeutron>();
                var invalid = r.Crystal<CrystalA>(); invalid.EnergyCost.CostsX = isX;
                if (!isX) invalid.AddKeyword(CardKeyword.Unplayable);
                Put(r, invalid, PileType.Discard);
                var valid = r.Crystal<CrystalA>(); Put(r, valid, PileType.Discard);
                await neutron.OnPlayWrapper(r.Choice);
                Check(invalid.Pile?.Type == PileType.Discard && valid.Pile?.Type == PileType.Exhaust);
            });
        }
        foreach (int energy in new[] { 0, 1 }) {
            int e = energy;
            Test($"运用-中子弃牌堆为空仍结算分支 energy={e}", async () => {
                var r = new Rig(energy: e); var neutron = r.Crystal<DerivedApplicationNeutron>();
                var held = r.Crystal<CrystalA>(); Put(r, held, PileType.Hand);
                await neutron.OnPlayWrapper(r.Choice);
                Check(CardPileCmd.DrawCount == e && held.Pile?.Type == (e == 0 ? PileType.Discard : PileType.Hand));
            });
        }
        Test("运用-中子空手晶逆安全完成", async () => {
            var r = new Rig(energy: 0); var neutron = r.Crystal<DerivedApplicationNeutron>(); Put(r, neutron, PileType.Hand);
            await neutron.OnPlayWrapper(r.Choice);
            Check(neutron.Pile?.Type == PileType.Discard && CardPileCmd.DrawCount == 0);
        });
        Test("运用-中子遵循本体自动出牌限制不重新选牌", async () => {
            var r = new Rig(); var neutron = r.Crystal<DerivedApplicationNeutron>();
            var blocked = r.Crystal<CrystalA>(); Put(r, blocked, PileType.Discard);
            var other = r.Crystal<CrystalA>(); Put(r, other, PileType.Discard);
            CardCmd.ShouldAutoPlay = c => c != blocked;
            await neutron.OnPlayWrapper(r.Choice);
            Check(blocked.Pile?.Type == PileType.Discard && other.Pile?.Type == PileType.Discard
                && CardPileCmd.DrawCount == 1 && CombatManager.Instance.History.CardPlaysStarted.Count == 1);
        });
        foreach (bool death in new[] { false, true }) {
            bool dead = death;
            Test($"运用-中子嵌套效果结束战斗或本人死亡停止后续 dead={dead}", async () => {
                var r = new Rig(); var neutron = r.Crystal<DerivedApplicationNeutron>();
                var selected = r.Crystal<CrystalA>(); Put(r, selected, PileType.Discard);
                r.State.Listeners.Add(new AfterPlayObserver(selected, () => {
                    if (dead) r.Player.Creature.IsAlive = false; else CombatManager.Instance.IsOverOrEnding = true;
                }));
                await neutron.OnPlayWrapper(r.Choice);
                Check(CardPileCmd.DrawCount == 0);
            });
        }
        foreach (PileType pile in new[] { PileType.Draw, PileType.Discard, PileType.Exhaust }) {
            PileType location = pile;
            Test($"晶化U只回收独立衍生中子+ pile={location}", async () => {
                var r = new Rig(); var electron = r.Crystal<ApplicationNeutron>(); electron.Upgrade(); Put(r, electron, location);
                var ordinary = r.Crystal<DerivedApplicationNeutron>(); Put(r, ordinary, location);
                var upgraded = r.Crystal<DerivedApplicationNeutron>(); upgraded.Upgrade(); Put(r, upgraded, location);
                await r.Crystal<CrystallizationU>().Play(r.Choice, new() { Card = r.Card });
                Check(electron.Pile?.Type == location && ordinary.Pile?.Type == location
                    && upgraded.Pile?.Type == PileType.Hand && upgraded.Keywords.Contains(CardKeyword.Exhaust));
            });
        }
        Test("运用-中子与电子双语身份及U引用相互独立", () => {
            foreach (string lang in new[] { "zhs", "eng" }) {
                using var json = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(args[1], "IndiumStar", "localization", lang, "cards.json")));
                var root = json.RootElement;
                Check(root.GetProperty("INDIUM_STAR_CARD_DERIVED_APPLICATION_NEUTRON.title").GetString()
                    == (lang == "zhs" ? "运用-中子" : "Application-Neutron"));
                Check(root.GetProperty("INDIUM_STAR_CARD_APPLICATION_NEUTRON.title").GetString()
                    == (lang == "zhs" ? "近身斩" : "Melee Slash"));
                foreach (string key in new[] { "APPLICATION_U", "CRYSTALLIZATION_U" }) {
                    string description = root.GetProperty("INDIUM_STAR_CARD_" + key + ".description").GetString()!;
                    Check(description.Contains(lang == "zhs" ? "运用-中子+" : "Application-Neutron+")
                        && !description.Contains(lang == "zhs" ? "近身斩" : "Melee Slash"));
                }
            }
            return Task.CompletedTask;
        });
        Test("运用-U生成中子实际出牌会晶化U，自动晶化不占手动次数", async () => {
            var r = new Rig(energy: 0); var u = r.Crystal<ApplicationU>();
            await u.Play(r.Choice, new() { Card = u });
            var neutrons = r.Player.PlayerCombatState!.Hand.Cards.OfType<DerivedApplicationNeutron>().ToArray();
            var crystal = r.Crystal<CrystallizationMg>(); Put(r, crystal, PileType.Discard);
            CardSelectCmd.SelectionOverride = (_, _, _, _, _) =>
                Task.FromResult<IEnumerable<CardModel>>(new[] { neutrons[1] });
            await neutrons[0].OnPlayWrapper(r.Choice);
            var crystalU = r.Player.PlayerCombatState.Hand.Cards.OfType<CrystallizationU>().Single();
            Check(crystal.Pile?.Type == PileType.Exhaust && r.Player.Creature.Block == 4
                && neutrons[1].Pile?.Type == PileType.Discard && crystalU.CanPlayManually
                && !CrystallizationRules.HasPlayedCrystallizationThisTurn(crystalU));
            Check(!r.Player.PlayerCombatState.AllCards.OfType<ApplicationNeutron>().Any());
        });
    }
    private sealed class AfterPlayObserver(CardModel selected, Action act) : AbstractModel {
        public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play) {
            if (play.Card == selected) act(); return Task.CompletedTask;
        }
    }
}
