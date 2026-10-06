using System.Reflection;
using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Only the changed document specifications. Native commands/turn transitions are fixtures.
public static class DocumentSyncTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Document sync assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile = PileType.Hand)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    private static Creature Enemy(Rig r)
    { var enemy = new Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy); return enemy; }
    private static readonly PropertyInfo Decay = typeof(CrystalCoin).GetProperty("PlaysThisRun", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static void Register(List<(string Name, Func<Task> Run)> tests)
    {
        void Test(string name, Func<Task> run) => tests.Add(("文档同步：" + name, run));
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            Test($"电子1费、11/15伤害与分支 upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<ApplicationNeutron>(); if (up) card.Upgrade();
                await card.Play(r.Choice, new() { Card = card, Target = Enemy(r) });
                Check(card.EnergyCost.Value == 1 && card.Type == CardType.Attack
                    && DamageCmd.Attacks.Single().Damage == (up ? 15 : 11)
                    && r.Player.Creature.GetPowerAmount<WeakPower>() == 1);
            });
            Test($"碎晶雨所有段2伤害，晶抗只按手牌追加 upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<ShatterCrystalRain>(); if (up) card.Upgrade();
                Put(r, r.Crystal<Crystallization>()); Put(r, r.Crystal<CrystallizationNb>());
                Put(r, r.Crystal<Crystallization>(), PileType.Discard);
                await card.Play(r.Choice, new() { Card = card, Target = Enemy(r) });
                Check(DamageCmd.Attacks.Count == 2 && DamageCmd.Attacks.All(a => a.Damage == 2)
                    && DamageCmd.HitCounts.SequenceEqual(new[] { up ? 4 : 3, 2 }));
            });
            Test($"碎晶雨晶逆其他敌人保持2伤害多段 upgraded={up}", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<ShatterCrystalRain>(); if (up) card.Upgrade();
                var first = Enemy(r); var second = Enemy(r);
                await card.Play(r.Choice, new() { Card = card, Target = first });
                Check(DamageCmd.Attacks.Count == 2 && DamageCmd.Attacks.All(a => a.Damage == 2 && a.Hits == (up ? 4 : 3))
                    && DamageCmd.Attacks[1].Target == second);
            });
            Test($"Cu先挂中毒累计器，自身13伤害计入并保留余数 upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<ApplicationCu>(); if (up) card.Upgrade(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = async attack => {
                    var power = enemy.GetPower<ApplicationCuPower>(); Check(power != null);
                    await power!.AfterDamageGiven(r.Choice, r.Player.Creature,
                        new DamageResult(enemy, ValueProp.Move) { UnblockedDamage = (int)attack.Damage }, ValueProp.Move, enemy, card);
                };
                await card.Play(r.Choice, new() { Card = card, Target = enemy });
                Check(DamageCmd.Attacks.Single().Damage == 13 && enemy.GetPowerAmount<PoisonPower>() == (up ? 4 : 2));
                await enemy.GetPower<ApplicationCuPower>()!.AfterDamageGiven(r.Choice, r.Player.Creature,
                    new DamageResult(enemy, ValueProp.Move) { UnblockedDamage = 1, BlockedDamage = 99 }, ValueProp.Move, enemy, null);
                Check(enemy.GetPowerAmount<PoisonPower>() == 4);
            });
            Test($"晶币最后3金币后移出原件，当前战斗牌消耗 upgraded={up}", async () => {
                var r = new Rig(); PlayerCmd.GoldGranted.Clear(); var deck = r.Crystal<CrystalCoin>(); if (up) deck.Upgrade(); Put(r, deck, PileType.Deck);
                int plays = up ? 8 : 6;
                for (int i = 0; i < plays; i++) {
                    var combat = (CrystalCoin)deck.Clone(); combat.DeckVersion = deck; Put(r, combat);
                    await combat.OnPlayWrapper(r.Choice);
                    Check(combat.Pile?.Type == PileType.Exhaust && combat.Keywords.Contains(CardKeyword.Ethereal));
                    Check(r.Player.Deck.Cards.Contains(deck) == (i < plays - 1));
                }
                Check(deck.Pile == null && PlayerCmd.GoldGranted.SequenceEqual(Enumerable.Range(0, plays).Select(i => (decimal)((up ? 24 : 18) - 3 * i))));
            });
            Test($"晶币独立复制归零不移除来源，原件继续衰减 upgraded={up}", async () => {
                var r = new Rig(); PlayerCmd.GoldGranted.Clear(); var deck = r.Crystal<CrystalCoin>(); if (up) deck.Upgrade();
                Put(r, deck, PileType.Deck); int decay = up ? 7 : 5; Decay.SetValue(deck, decay);
                var combat = (CrystalCoin)deck.Clone(); combat.DeckVersion = deck;
                var copy = (CrystalCoin)combat.CreateClone(); Put(r, copy); await copy.OnPlayWrapper(r.Choice);
                Check(copy.DeckVersion == null && r.Player.Deck.Cards.Contains(deck) && (int)Decay.GetValue(deck)! == decay);
                Put(r, combat); await combat.OnPlayWrapper(r.Choice);
                Check(!r.Player.Deck.Cards.Contains(deck) && PlayerCmd.GoldGranted.SequenceEqual(new decimal[] { 3, 3 }));
            });
            Test($"钒永先抽牌，自身首次抽取不预约 upgraded={up}", async () => {
                var r = new Rig(); for (int i = 0; i < 6; i++) Put(r, r.Crystal<TestAttack>(), PileType.Draw);
                var card = r.Crystal<ReminiscenceVanadiumEternal>(); if (up) card.Upgrade();
                await card.Play(r.Choice, new() { Card = card }); var power = r.Player.Creature.GetPower<ReminiscenceDrawPower>()!;
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == (up ? 2 : 1) && power.Amount == 0);
                await CardPileCmd.Draw(r.Choice, 2, r.Player); await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
                r.Player.PlayerCombatState.TurnNumber++; await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, [r.Player.Creature], r.State);
                Check(power.ModifyHandDraw(r.Player, 5) == 7);
            });
            Test($"钒永已有统计器仍统计再次打出的抽牌，不叠加或清空 upgraded={up}", async () => {
                var r = new Rig(); for (int i = 0; i < 8; i++) Put(r, r.Crystal<TestAttack>(), PileType.Draw);
                var first = r.Crystal<ReminiscenceVanadiumEternal>(); await first.Play(r.Choice, new() { Card = first });
                await CardPileCmd.Draw(r.Choice, 1, r.Player);
                var second = r.Crystal<ReminiscenceVanadiumEternal>(); if (up) second.Upgrade(); await second.Play(r.Choice, new() { Card = second });
                Check(r.Player.Creature.Powers.OfType<ReminiscenceDrawPower>().Count() == 1
                    && r.Player.Creature.GetPower<ReminiscenceDrawPower>()!.Amount == (up ? 3 : 2));
            });
            foreach (int energy in new[] { 0, 1 })
            {
                int e = energy;
                Test($"三键基础格挡与3/5、5/7分支系数 upgraded={up} energy={e}", async () => {
                    var r = new Rig(energy: e); var card = r.Crystal<BondingTripleBond>(); if (up) card.Upgrade();
                    Put(r, r.Crystal<Crystallization>()); Put(r, r.Crystal<CrystallizationNb>()); Put(r, r.Crystal<Crystallization>(), PileType.Discard);
                    await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 3, null, null);
                    await card.Play(r.Choice, new() { Card = card });
                    Check(r.Player.Creature.Block == (up ? 15 : 12) + (e == 0 ? 3 * (up ? 7 : 5) : 2 * (up ? 5 : 3)));
                });
            }
            Test($"晶髓升级同时增加延拓和抽牌，并消耗 upgraded={up}", async () => {
                var r = new Rig(energy: 0); var card = r.Crystal<CrystalMarrow>(); if (up) card.Upgrade();
                card.EnergyCost.CapturedXValue = 2; card.EnergyCost.XModifier = 1; Put(r, card); await card.OnPlayWrapper(r.Choice);
                int amount = up ? 4 : 3;
                Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == amount
                    && r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == amount
                    && card.Pile?.Type == PileType.Exhaust && r.Player.Creature.GetPower<CrystalMarrowPower>()!.PendingTurns.Length == 1);
            });
            Test($"密铺每张手牌晶化1力1敏，与Hs光环独立 upgraded={up}", async () => {
                var r = new Rig(); var enemy = Enemy(r); await CardPileCmd.AddGeneratedCardToCombat(r.Crystal<CrystallizationHs>(), PileType.Hand, r.Player);
                var card = r.Crystal<TilingForm>(); if (up) card.Upgrade(); await card.Play(r.Choice, new() { Card = card });
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 5 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 4
                    && enemy.GetPowerAmount<StrengthPower>() == 1 && card.Keywords.Contains(CardKeyword.Retain) == up);
                var ordinary = r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().First();
                await CardCmd.Exhaust(r.Choice, ordinary);
                // The Exhaust fixture only moves piles; explicitly deliver the native pile-change hook.
                await r.Player.Creature.GetPower<CrystalHandAuraPower>()!.AfterCardChangedPiles(ordinary, PileType.Hand, null);
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 4 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 3);
            });
        }
        Test("晶化Nb给予3活力并保留公共晶化属性", async () => {
            var r = new Rig(); var card = r.Crystal<CrystallizationNb>(); Put(r, card); await card.OnPlayWrapper(r.Choice);
            Check(r.Player.Creature.GetPowerAmount<VigorPower>() == 3 && card.Pile?.Type == PileType.Exhaust && card.Keywords.Contains(CardKeyword.Retain));
        });
    }
}
