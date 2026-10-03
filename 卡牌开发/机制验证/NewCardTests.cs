using IndiumStar.Cards;
using IndiumStar.Mechanics;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public static class NewCardTests
{
    private static void Check(bool condition, string message = "assertion failed") {
        if (!condition) throw new Exception(message);
    }
    private static void Put(Rig r, CardModel card, PileType pile) {
        var destination = pile.GetPile(r.Player); destination.Cards.Add(card); card.Pile = destination;
    }
    // 仅为新卡提供本体出牌的关键顺序：离手→历史→Before→基础效果→After。
    private static async Task Play(Rig r, CardModel card, Creature? target = null, bool auto = false, int index = 0) {
        card.Pile?.Cards.Remove(card); card.Pile = new() { Type = PileType.Play };
        var play = new CardPlay { Card = card, Target = target, IsAutoPlay = auto, PlayIndex = index };
        r.Record(card, auto, index);
        foreach (var listener in r.State.IterateHookListeners().ToArray()) await listener.BeforeCardPlayed(play);
        await card.Play(r.Choice, play);
        foreach (var listener in r.State.IterateHookListeners().ToArray()) await listener.AfterCardPlayed(r.Choice, play);
    }
    private static Creature Enemy(Rig r, ProbeEnemy.Mode mode) {
        var creature = new Creature { CombatState = r.State };
        var monster = new ProbeEnemy { Creature = creature, Intent = mode }; creature.Monster = monster;
        monster.SetUpForCombat(); r.State.CreatureList.Add(creature); return creature;
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args) {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (int energy in new[] { 0, 1 }) foreach (bool upgraded in new[] { false, true }) {
            int e = energy; bool up = upgraded;
            Test($"宏枝晶先伤害后按能量成长 e={e} upgraded={up}", async () => {
                var r = new Rig(energy: e); var card = r.Crystal<MacroDendrite>(); if (up) card.Upgrade();
                await Play(r, card, new()); var first = DamageCmd.Attacks.Single();
                Check(first.Damage == 3 && first.Hits == 3);
                int growth = up ? 2 : 1;
                Check(card.DynamicVars.Damage.IntValue == (e == 0 ? 3 : 3 + growth));
                Check(card.DynamicVars["HitCount"].IntValue == (e == 0 ? 3 + growth : 3));
                Check(r.Player.PlayerCombatState!.Hand.Cards.Single() is Crystallization);
                await Play(r, card, new()); Check(DamageCmd.Attacks.Count == 2);
                Check(DamageCmd.Attacks[1].Damage == (e == 0 ? 3 : 3 + growth));
                Check(DamageCmd.Attacks[1].Hits == (e == 0 ? 3 + growth : 3));
            });
        }
        Test("宏枝晶量子点双分支各生成1晶化并各成长一次", async () => {
            var r = new Rig(); var card = r.Crystal<MacroDendrite>();
            await PowerCmd.Apply<QuantumDotPower>(r.Choice, r.Player.Creature, 1, r.Player.Creature, null);
            await Play(r, card, new());
            Check(card.DynamicVars.Damage.IntValue == 4 && card.DynamicVars["HitCount"].IntValue == 4);
            Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 2);
        });
        Test("宏枝晶成长复制继承当前值但不共享状态或写回牌组", async () => {
            var r = new Rig(); var card = r.Crystal<MacroDendrite>(); var deck = r.Crystal<MacroDendrite>(); card.DeckVersion = deck;
            await Play(r, card, new()); var clone = (MacroDendrite)card.CreateClone();
            Check(clone.DynamicVars.Damage.IntValue == 4 && clone.DeckVersion == null);
            await Play(r, clone, new());
            Check(clone.DynamicVars.Damage.IntValue == 5 && card.DynamicVars.Damage.IntValue == 4 && deck.DynamicVars.Damage.IntValue == 3);
        });
        foreach (string kind in new[] { "skill", "crystal", "empty", "blocked" }) foreach (bool upgraded in new[] { false, true }) {
            string k = kind; bool up = upgraded;
            Test($"成环消耗实际选择牌并延后抽牌 {k} upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<Cyclization>(); if (up) card.Upgrade();
                CardModel selected = k == "crystal" ? r.Crystal<CrystalA>() : r.Crystal<Reiteration>();
                if (k != "empty") Put(r, selected, PileType.Hand);
                CardCmd.ExhaustBlocked = k == "blocked";
                await Play(r, card);
                Check(r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == (up ? 3 : 2));
                Check(CardPileCmd.DrawCount == 0);
                Check(r.Player.Creature.Block == (k == "skill" ? (up ? 8 : 5) : 0));
                if (k is "skill" or "crystal") Check(selected.Pile?.Type == PileType.Exhaust);
            });
        }
        Test("分影连气仅真正抽到本牌时复制且保留升级", async () => {
            var r = new Rig(); var card = r.Crystal<ShadowChainBreath>(); card.Upgrade(); Put(r, card, PileType.Draw);
            var drawn = (await CardPileCmd.Draw(r.Choice, 1, r.Player)).ToArray();
            Check(drawn.Length == 1 && drawn[0] == card);
            var hand = r.Player.PlayerCombatState!.Hand.Cards;
            Check(hand.Count == 2 && hand[1] is ShadowChainBreath clone && clone != card && clone.IsUpgraded && clone.DynamicVars.Block.IntValue == 4);
            await card.AfterCardDrawn(r.Choice, r.Card, false); Check(hand.Count == 2);
        });
        Test("分影连气生成复制不会递归且满手溢出进入弃牌堆", async () => {
            var r = new Rig(); CardPileCmd.HandCapacity = 1;
            var card = r.Crystal<ShadowChainBreath>(); Put(r, card, PileType.Draw);
            await CardPileCmd.Draw(r.Choice, 1, r.Player);
            Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 1 && r.Player.PlayerCombatState.DiscardPile.Cards.Count == 1);
        });
        foreach (int remaining in new[] { 0, 1, 3 }) foreach (bool upgraded in new[] { false, true }) {
            int count = remaining; bool up = upgraded;
            Test($"分影连气按剩余手牌分次获得格挡 count={count} upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<ShadowChainBreath>(); if (up) card.Upgrade(); Put(r, card, PileType.Hand);
                for (int i = 0; i < count; i++) Put(r, r.Crystal<ShadowChainBreath>(), PileType.Hand);
                await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, 2, r.Player.Creature, null);
                await Play(r, card);
                Check(CreatureCmd.BlockGains.Count == count && r.Player.Creature.Block == count * (up ? 6 : 5));
                Check(card.CanonicalKeywords.Contains(CardKeyword.Exhaust));
            });
        }
        Test("复沓不复制自身而在下一牌结算前置顶独立升级复制", async () => {
            var r = new Rig(); var card = r.Crystal<Reiteration>(); card.Upgrade(); await Play(r, card);
            Check(card.EnergyCost.Value == 0 && r.Player.PlayerCombatState!.DrawPile.Cards.Count == 0);
            Check(r.Player.Creature.GetPowerAmount<ReiterationPower>() == 1);
            var next = r.Crystal<MacroDendrite>(); next.Upgrade(); await Play(r, next, new());
            var clone = (MacroDendrite)r.Player.PlayerCombatState.DrawPile.Cards.Single();
            Check(clone != next && clone.IsUpgraded && clone.DynamicVars.Damage.IntValue == 3 && next.DynamicVars.Damage.IntValue == 5);
            Check(r.Player.Creature.GetPower<ReiterationPower>() == null);
        });
        Test("复沓可复制自动打出牌但不再次复制系列重放", async () => {
            var r = new Rig(); await Play(r, r.Crystal<Reiteration>());
            var next = r.Crystal<TestAttack>(); await Play(r, next, new(), auto: true);
            await Play(r, next, new(), auto: true, index: 1);
            Check(r.Player.PlayerCombatState!.DrawPile.Cards.Count == 1);
        });
        Test("复沓叠加生成多个复制并在本人回合结束清除", async () => {
            var r = new Rig(); await PowerCmd.Apply<ReiterationPower>(r.Choice, r.Player.Creature, 2, r.Player.Creature, null);
            await Play(r, r.Crystal<TestAttack>(), new()); Check(r.Player.PlayerCombatState!.DrawPile.Cards.Count == 2);
            var power = await PowerCmd.Apply<ReiterationPower>(r.Choice, r.Player.Creature, 1, r.Player.Creature, null);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Enemy, [new()]); Check(r.Player.Creature.GetPower<ReiterationPower>() != null);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]); Check(r.Player.Creature.GetPower<ReiterationPower>() == null);
        });
        Test("复沓不响应另一玩家出牌和非首次重放", async () => {
            var r = new Rig(); var power = await PowerCmd.Apply<ReiterationPower>(r.Choice, r.Player.Creature, 1, null, null);
            await power.BeforeCardPlayed(new() { Card = new TestAttack { Owner = new() } });
            await power.BeforeCardPlayed(new() { Card = r.Card, PlayIndex = 1 });
            Check(r.Player.Creature.GetPowerAmount<ReiterationPower>() == 1 && r.Player.PlayerCombatState!.DrawPile.Cards.Count == 0);
        });
        foreach (CardType type in Enum.GetValues<CardType>()) foreach (bool upgraded in new[] { false, true }) {
            CardType t = type; bool up = upgraded;
            Test($"邻基参与按第一张实际抽到牌判断 {t} upgraded={up}", async () => {
                var r = new Rig(); var card = r.Crystal<NeighboringGroupParticipation>(); if (up) card.Upgrade();
                var first = new TypedProbe(t) { Owner = r.Player, CombatState = r.State }; Put(r, first, PileType.Draw);
                Put(r, r.Crystal<TestAttack>(), PileType.Draw); Put(r, r.Crystal<TestAttack>(), PileType.Draw);
                await Play(r, card);
                Check(CardPileCmd.DrawCount == (t == CardType.Power ? 3 : 1));
                Check(r.Player.Creature.Block == (t == CardType.Skill ? (up ? 5 : 3) : 0));
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == (t == CardType.Attack ? (up ? 4 : 3) : 0));
            });
        }
        Test("邻基参与抽牌受阻和空牌堆都不触发类型奖励", async () => {
            foreach (bool blocked in new[] { false, true }) {
                var r = new Rig(); CardPileCmd.DrawBlocked = blocked;
                if (blocked) Put(r, r.Crystal<TestAttack>(), PileType.Draw);
                await Play(r, r.Crystal<NeighboringGroupParticipation>());
                Check(r.Player.Creature.Block == 0 && r.Player.Creature.GetPower<NeighboringGroupStrengthPower>() == null);
            }
        });
        Test("邻基参与多次临时力量回收保留原有永久力量", async () => {
            var r = new Rig(); await PowerCmd.Apply<StrengthPower>(r.Choice, r.Player.Creature, 5, null, null);
            for (int i = 0; i < 2; i++) {
                Put(r, r.Crystal<TestAttack>(), PileType.Draw); await Play(r, r.Crystal<NeighboringGroupParticipation>());
            }
            Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 11);
            var temporary = r.Player.Creature.GetPower<NeighboringGroupStrengthPower>()!;
            await temporary.AfterSideTurnEnd(r.Choice, CombatSide.Player, [r.Player.Creature]);
            Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 5 && r.Player.Creature.GetPower<NeighboringGroupStrengthPower>() == null);
        });
        foreach (ProbeEnemy.Mode mode in Enum.GetValues<ProbeEnemy.Mode>()) {
            var m = mode;
            Test($"反演借用意图且不推进原敌人 {m}", async () => {
                var r = new Rig(); var enemy = Enemy(r, m); var original = (ProbeEnemy)enemy.Monster!;
                var originalMove = original.NextMove; var card = r.Crystal<Inversion>();
                bool failed = false; try { await Play(r, card, enemy); } catch (InvalidOperationException) { failed = true; }
                Check(failed == (m == ProbeEnemy.Mode.Fail));
                Check(original.ExecutionCount == 0 && !originalMove.Performed && original.NextMove == originalMove && original.Creature == enemy);
                Check(IntentExecutionRules.Current == null);
                Check(DamageCmd.Attacks[0].Damage == 6 && DamageCmd.Attacks[0].Target == enemy);
                if (m == ProbeEnemy.Mode.Attack) {
                    var attack = DamageCmd.Attacks[1]; Check(attack.Damage == 7 && attack.Hits == 2 && attack.Source == card && attack.Target == enemy && attack.Choice == r.Choice);
                }
                if (m == ProbeEnemy.Mode.Buff) Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 2 && enemy.GetPowerAmount<StrengthPower>() == 0);
                if (m == ProbeEnemy.Mode.Debuff) Check(enemy.GetPowerAmount<DexterityPower>() == -2 && r.Player.Creature.GetPowerAmount<DexterityPower>() == 0);
                if (m == ProbeEnemy.Mode.Animation) Check(CreatureCmd.LastAnimation == "Cast");
            });
        }
        Test("反演升级8伤且死亡目标不借用意图", async () => {
            var r = new Rig(); var enemy = Enemy(r, ProbeEnemy.Mode.Attack); enemy.IsAlive = false;
            var card = r.Crystal<Inversion>(); card.Upgrade(); await Play(r, card, enemy);
            Check(DamageCmd.Attacks.Count == 1 && DamageCmd.Attacks[0].Damage == 8);
        });
        Test("反演补丁在借用范围外不改原怪物攻击与动画", async () => {
            var r = new Rig(); var enemy = Enemy(r, ProbeEnemy.Mode.Attack);
            await DamageCmd.Attack(7m).FromMonster(enemy.Monster!).Execute(null);
            Check(DamageCmd.Attacks.Single().Source == enemy.Monster && DamageCmd.Attacks.Single().Choice == null);
            await CreatureCmd.TriggerAnim(enemy, "MonsterSpecial", 1f); Check(CreatureCmd.LastAnimation == "MonsterSpecial");
        });
        Test("六张新卡中英文标题描述均存在", () => {
            foreach (string language in new[] { "zhs", "eng" }) {
                string path = System.IO.Path.Combine(args[1], "IndiumStar", "localization", language, "cards.json");
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                foreach (string id in new[] { "MACRO_DENDRITE", "CYCLIZATION", "SHADOW_CHAIN_BREATH", "REITERATION", "INVERSION", "NEIGHBORING_GROUP_PARTICIPATION" })
                    foreach (string suffix in new[] { ".title", ".description" }) Check(doc.RootElement.TryGetProperty("INDIUM_STAR_CARD_" + id + suffix, out _));
            }
            return Task.CompletedTask;
        });
        Test("实际游戏反演反射字段和Harmony目标参数契约", () => {
            var directory = System.IO.Path.Combine(args[0], "data_sts2_windows_x86_64");
            var context = new System.Runtime.Loader.AssemblyLoadContext("IntentContract", isCollectible: true);
            context.Resolving += (_, name) => {
                string path = System.IO.Path.Combine(directory, name.Name + ".dll");
                return System.IO.File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            try {
                var assembly = context.LoadFromAssemblyPath(System.IO.Path.Combine(directory, "sts2.dll"));
                var monster = assembly.GetType("MegaCrit.Sts2.Core.Models.MonsterModel", true)!;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                Check(monster.GetField("_creature", flags)?.FieldType.FullName == typeof(Creature).FullName);
                var attack = assembly.GetType("MegaCrit.Sts2.Core.Commands.Builders.AttackCommand", true)!;
                Check(attack.GetMethod("FromMonster")!.GetParameters().Single().Name == "monster");
                Check(attack.GetMethod("Execute")!.GetParameters().Single().Name == "choiceContext");
                var creatureCmd = assembly.GetType("MegaCrit.Sts2.Core.Commands.CreatureCmd", true)!;
                Check(creatureCmd.GetMethod("TriggerAnim")!.GetParameters().Select(p => p.Name).SequenceEqual(new[] { "creature", "triggerName", "waitTime" }));
            } finally { context.Unload(); }
            return Task.CompletedTask;
        });
    }
}
public class TypedProbe(CardType type) : STS2RitsuLib.Scaffolding.Content.ModCardTemplate(0, type, CardRarity.Common, TargetType.Self) {
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
