// 执行实际卡牌源码，指令替身用于观察调用结果/时点；不等同于真实UI、存档或多人运行。
using IndiumStar.Cards;
using IndiumStar.Powers;
using IndiumStar.Mechanics;
using IndiumStar.Afflictions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

public static class ApplicationEffectTests
{
    private static void Check(bool value, string message = "Application effect assertion failed")
    { if (!value) throw new Exception(message); }
    private static void Put(Rig r, CardModel card, PileType pile = PileType.Hand)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    private static Creature Enemy(Rig r)
    { var enemy = new Creature { CombatState = r.State }; r.State.CreatureList.Add(enemy); return enemy; }
    private static Task Play(Rig r, CardModel card, Creature? target = null) =>
        card.Play(r.Choice, new() { Card = card, Target = target });
    private static Task Hit(Rig r, ApplicationCuPower power, Creature target, int damage, ValueProp props = ValueProp.Move,
        Creature? dealer = null) => power.AfterDamageGiven(r.Choice, dealer ?? r.Player.Creature,
            new DamageResult(target, props) { UnblockedDamage = damage, BlockedDamage = 20 }, props, target, null);

    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            foreach (bool magnesium in new[] { false, true }) foreach (int occupied in new[] { 0, 9, 10 })
            {
                bool mg = magnesium; int n = occupied;
                Test($"运用-{(mg ? "Mg" : "As")}伤害和晶化种类、满手溢出 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); CardModel card = mg ? r.Crystal<ApplicationMg>() : r.Crystal<ApplicationAs>();
                    if (up) card.Upgrade(); var enemy = Enemy(r);
                    for (int i = 0; i < n; i++) Put(r, new TypedProbe(CardType.Skill) { Owner = r.Player, CombatState = r.State });
                    r.State.Listeners.Add(new GeneratedObserver(c => Check(DamageCmd.Attacks.Count == 1, "Damage must finish before crystallization")));
                    await Play(r, card, enemy);
                    var attack = DamageCmd.Attacks.Single();
                    Check(attack.Damage == (mg ? (up ? 11 : 8) : (up ? 27 : 23)) && attack.Hits == 1
                        && attack.Source == card && attack.Target == enemy && attack.Choice == r.Choice);
                    var added = r.Player.PlayerCombatState!.AllCards.Where(c => mg ? c is CrystallizationMg : c is Crystallization).ToArray();
                    Check(added.Length == 2 && added.Count(c => c.Pile?.Type == PileType.Hand) == Math.Min(2, 10 - n));
                    Check(card.EnergyCost.Value == (mg ? 1 : 2) && card.Type == CardType.Attack);
                });
            }
            foreach (int slots in new[] { 0, 1, 2, 3 }) foreach (bool auto in new[] { false, true })
            {
                int free = slots; bool automatic = auto;
                Test($"运用-F先晶化再复制，手动/自动和溢出 slots={free} auto={automatic} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: 0); var card = r.Crystal<ApplicationF>(); if (up) card.Upgrade();
                    var enemy = Enemy(r);
                    for (int i = 0; i < 10 - free; i++) Put(r, new TypedProbe(CardType.Skill) { Owner = r.Player, CombatState = r.State });
                    await card.OnPlayWrapper(r.Choice, enemy, automatic);
                    Check(DamageCmd.Attacks.Single().Damage == (up ? 8 : 6));
                    var copies = r.Player.PlayerCombatState!.AllCards.OfType<ApplicationF>().Where(c => c != card).ToArray();
                    Check(copies.Length == 1 && copies[0].IsUpgraded == up && copies[0].EnergyCost.Value == 0
                        && copies[0].DeckVersion == null && copies[0].Keywords.Contains(CardKeyword.Exhaust));
                    Check(copies[0].Pile?.Type == (free >= 3 ? PileType.Hand : PileType.Discard));
                    Check(r.Player.PlayerCombatState.Hand.Cards.OfType<Crystallization>().Count() == Math.Min(2, free));
                    Check(r.Player.PlayerCombatState.AllCards.OfType<Crystallization>().Count() == 2);
                    Check(card.Pile?.Type == PileType.Exhaust && !r.Player.PlayerCombatState.PlayPile.Cards.Contains(card));
                    copies[0].DynamicVars.Damage.BaseValue = 99;
                    Check(card.DynamicVars.Damage.BaseValue == (up ? 8 : 6), "Clone must not share dynamic variables");
                });
            }
            Test($"运用-In先晶化再延拓，升级只添加保留 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationIn>(); if (up) card.Upgrade();
                r.State.Listeners.Add(new GeneratedObserver(c => Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 0)));
                await Play(r, card);
                Check(r.Player.PlayerCombatState!.Hand.Cards.Single() is Crystallization);
                Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 2 && card.EnergyCost.Value == 1);
                Check(card.Keywords.Contains(CardKeyword.Retain) == up && card.Keywords.Contains(IndiumStarKeywords.ExtensionKeyword));
                Check(PowerCmd.LastSource == card && PowerCmd.LastApplier == r.Player.Creature);
            });
            Test($"运用-Nb直接授予能力、升级活力及两张晶化Nb upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationNb>(); if (up) card.Upgrade(); await Play(r, card);
                Check(r.Player.Creature.GetPower<ApplicationNbPower>() != null && r.Player.Creature.GetPowerAmount<VigorPower>() == (up ? 2 : 0));
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 2 && r.Player.PlayerCombatState.Hand.Cards.All(c => c is CrystallizationNb));
                Check(card.Type == CardType.Power && card.EnergyCost.Value == 1);
            });
            foreach (int energy in new[] { 0, 1, 2 })
            {
                int e = energy;
                Test($"运用-Zn基础格挡之后只选择一次分支 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var card = r.Crystal<ApplicationZn>(); if (up) card.Upgrade(); Put(r, card);
                    await card.OnPlayWrapper(r.Choice);
                    Check(r.Player.Creature.Block == (up ? 15 : 12) && r.Energy == (e == 0 ? 0 : e - 1));
                    Check(card.Pile?.Type == (e == 0 ? PileType.Exhaust : PileType.Discard));
                    Check(!r.Player.PlayerCombatState!.PlayPile.Cards.Contains(card) && card.SeriesCleanupCount == 1);
                });
            }
            Test($"运用-H按易伤虚弱脆弱循环，升级去除上限 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 1); var card = r.Crystal<ApplicationH>(); if (up) card.Upgrade();
                await PowerCmd.Apply<VulnerablePower>(r.Choice, r.Player.Creature, 3, null, null);
                await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, 3, null, null);
                await PowerCmd.Apply<FrailPower>(r.Choice, r.Player.Creature, 3, null, null);
                await Play(r, card); Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == (up ? 1 : 2));
                r.Energy = 0; await Play(r, card); Check(r.Player.Creature.GetPowerAmount<WeakPower>() == (up ? 1 : 2));
                r.Energy = 1; await Play(r, card); Check(r.Player.Creature.GetPowerAmount<VulnerablePower>() == (up ? 0 : 1));
                await Play(r, card); await Play(r, card);
                Check(r.Player.Creature.GetPowerAmount<FrailPower>() == (up ? 1 : 2));
                Check(card.EnergyCost.Value == 0 && card.Type == CardType.Skill);
            });
            Test($"运用-Hs只削弱存活敌人，敌人回合末恢复 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationHs>(); if (up) card.Upgrade();
                var a = Enemy(r); var b = Enemy(r); var dead = Enemy(r); dead.IsAlive = false;
                await Play(r, card); int loss = up ? 10 : 7;
                Check(a.GetPowerAmount<StrengthPower>() == -loss + 2 && b.GetPowerAmount<StrengthPower>() == -loss + 2,
                    $"Hs initial strength: {a.GetPowerAmount<StrengthPower>()}, {b.GetPowerAmount<StrengthPower>()}");
                Check(dead.Powers.Count == 0 && r.Player.Creature.GetPowerAmount<StrengthPower>() == 2);
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 2 && r.Player.PlayerCombatState.Hand.Cards.All(c => c is CrystallizationHs));
                var power = a.GetPower<ApplicationHsStrengthDownPower>()!;
                Check(power.Amount == loss, "Temporary loss and hand aura are separate contributions");
                await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(a.GetPowerAmount<StrengthPower>() == -loss + 2, "Hs player turn cleanup");
                await power.AfterSideTurnEnd(r.Choice, CombatSide.Enemy, new[] { a, b }); Check(a.GetPowerAmount<StrengthPower>() == 2 && !a.Powers.Contains(power), $"Hs restoration: {a.GetPowerAmount<StrengthPower>()}");
                Check(card.Keywords.Contains(CardKeyword.Exhaust));
            });
            foreach (int available in new[] { 0, 1, 5 })
            {
                int n = available;
                Test($"运用-Co标记实际抽到的首牌并保留 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var card = r.Crystal<ApplicationCo>(); if (up) card.Upgrade();
                    for (int i = 0; i < n; i++) Put(r, new TypedProbe(CardType.Skill) { Owner = r.Player, CombatState = r.State }, PileType.Draw);
                    var expectedFirst = r.Player.PlayerCombatState!.DrawPile.Cards.FirstOrDefault(); await Play(r, card);
                    int drawn = Math.Min(n, up ? 4 : 3); Check(r.Player.PlayerCombatState.Hand.Cards.Count == drawn);
                    var power = r.Player.Creature.GetPower<ApplicationCoPower>()!; Check(power != null);
                    if (expectedFirst == null) return;
                    Check(expectedFirst.SingleTurnRetain && expectedFirst.Affliction is CrystallizeAffliction);
                    foreach (var other in r.Player.PlayerCombatState.Hand.Cards.Where(c => c != expectedFirst)) Check(!other.SingleTurnRetain && other.Affliction == null);
                    await power.BeforeCardPlayed(new() { Card = expectedFirst });
                    Check(r.Player.PlayerCombatState.Hand.Cards.OfType<Crystallization>().Count() == 2);
                });
            }
            foreach (int occupied in new[] { 0, 9, 10 })
            {
                int n = occupied;
                Test($"运用-U三张中子升级并各具有晶化U，溢出仍保留标记 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var card = r.Crystal<ApplicationU>(); if (up) card.Upgrade();
                    for (int i = 0; i < n; i++) Put(r, new TypedProbe(CardType.Skill) { Owner = r.Player, CombatState = r.State });
                    await Play(r, card);
                    var neutrons = r.Player.PlayerCombatState!.AllCards.OfType<ApplicationNeutron>().ToArray();
                    Check(neutrons.Length == 3 && neutrons.All(c => c.IsUpgraded && c.DynamicVars.Damage.BaseValue == 14 && c.EnergyCost.Value == 0 && c.Affliction is CrystallizeUAffliction));
                    Check(neutrons.Count(c => c.Pile?.Type == PileType.Hand) == Math.Min(3, 10 - n));
                    Check(card.EnergyCost.Value == (up ? 2 : 3) && card.Keywords.Contains(CardKeyword.Exhaust));
                    var power = r.Player.Creature.GetPower<ApplicationUCrystallizePower>()!;
                    foreach (var neutron in neutrons) await power.BeforeCardPlayed(new() { Card = neutron });
                    Check(r.Player.PlayerCombatState.AllCards.OfType<CrystallizationU>().Count() == 3);
                });
            }
            Test($"运用-Pb只变化状态牌并晶抗生成普通晶化 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationPb>(); if (up) card.Upgrade(); var enemy = Enemy(r);
                var attack = r.Crystal<TestAttack>(); Put(r, attack);
                var curse = new TypedProbe(CardType.Curse) { Owner = r.Player, CombatState = r.State }; Put(r, curse);
                var statuses = Enumerable.Range(0, 3).Select(_ => new TypedProbe(CardType.Status) { Owner = r.Player, CombatState = r.State }).ToArray();
                foreach (var status in statuses) Put(r, status); await Play(r, card, enemy);
                Check(DamageCmd.Attacks.Single().Damage == (up ? 11 : 9));
                Check(r.Player.PlayerCombatState!.Hand.Cards.OfType<CrystallizationPb>().Count() == (up ? 2 : 1));
                Check(r.Player.PlayerCombatState.Hand.Cards.OfType<Crystallization>().Count() == 1 && attack.Pile?.Type == PileType.Hand && curse.Pile?.Type == PileType.Hand);
                Check(statuses.Count(c => c.Pile == null) == (up ? 2 : 1));
            });
            Test($"运用-Cu伤害之后生效，累计余数及升级阈值 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationCu>(); if (up) card.Upgrade(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = _ => { Check(enemy.GetPower<ApplicationCuPower>() == null); return Task.CompletedTask; };
                await Play(r, card, enemy); var power = enemy.GetPower<ApplicationCuPower>()!;
                Check(DamageCmd.Attacks.Single().Damage == 13 && enemy.GetPowerAmount<PoisonPower>() == 0);
                await Hit(r, power, enemy, 4); await Hit(r, power, enemy, 4); await Hit(r, power, enemy, 4);
                Check(enemy.GetPowerAmount<PoisonPower>() == (up ? 4 : 2));
                Check(PowerCmd.LastApplier == r.Player.Creature, "Poison must be attributed to Cu's player");
                await Hit(r, power, enemy, up ? 6 : 7); Check(enemy.GetPowerAmount<PoisonPower>() == (up ? 6 : 4));
            });
            Test($"运用-Cu首次施加后的连锁攻击立即使用当前配置 upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<ApplicationCu>(); if (up) card.Upgrade(); var enemy = Enemy(r);
                r.State.Listeners.Add(new PowerChangeObserver(async power =>
                {
                    if (power is ApplicationCuPower cu) await Hit(r, cu, enemy, up ? 6 : 7);
                }));
                await Play(r, card, enemy); Check(enemy.GetPowerAmount<PoisonPower>() == 2);
            });
        }
        foreach (int finalEnergy in new[] { 0, 1 })
        {
            int final = finalEnergy;
            Test($"运用-F基础攻击钩子之后读取能量 final={final}", async () =>
            {
                var r = new Rig(energy: 1 - final); var card = r.Crystal<ApplicationF>(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = _ => { r.Energy = final; return Task.CompletedTask; };
                await Play(r, card, enemy); Check(r.Player.PlayerCombatState!.Hand.Cards.Count == (final == 0 ? 3 : 0));
            });
            Test($"运用-Zn基础格挡钩子之后读取能量 final={final}", async () =>
            {
                var r = new Rig(energy: 1 - final); var card = r.Crystal<ApplicationZn>(); Put(r, card);
                CreatureCmd.AfterBlockGained = (_, _, _) => { r.Energy = final; return Task.CompletedTask; };
                await card.OnPlayWrapper(r.Choice); Check(card.Pile?.Type == (final == 0 ? PileType.Exhaust : PileType.Discard));
            });
            Test($"运用-Pb选牌暂停恢复后读取能量且允许不选 final={final}", async () =>
            {
                var r = new Rig(energy: 1 - final); var card = r.Crystal<ApplicationPb>(); var enemy = Enemy(r);
                var gate = new TaskCompletionSource<IEnumerable<CardModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
                CardSelectCmd.SelectionOverride = (_, _, prefs, _, _) => { Check(prefs.MinCount == 0 && prefs.MaxCount == 1); return gate.Task; };
                var pending = Play(r, card, enemy); Check(!pending.IsCompleted && DamageCmd.Attacks.Count == 1);
                r.Energy = final; gate.SetResult(Array.Empty<CardModel>()); await pending;
                Check(r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().Count() == final);
            });
        }
        Test("运用-H无对应减益仍切换，克隆保持独立模式，新战斗重置", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationH>(); Put(r, card);
            await card.AfterCardChangedPiles(card, PileType.None, null); await Play(r, card); // 无易伤也转到虚弱
            var copy = (ApplicationH)card.CreateClone(); Put(r, copy); await copy.AfterCardChangedPiles(copy, PileType.None, card);
            await PowerCmd.Apply<WeakPower>(r.Choice, r.Player.Creature, 3, null, null);
            await Play(r, copy); await Play(r, card); Check(r.Player.Creature.GetPowerAmount<WeakPower>() == 1);
            var next = new FakeCombat(); card.CombatState = next; r.Player.Creature.CombatState = next;
            await card.AfterCardChangedPiles(card, PileType.Hand, null);
            await PowerCmd.Apply<VulnerablePower>(r.Choice, r.Player.Creature, 1, null, null); await Play(r, card);
            Check(r.Player.Creature.GetPower<VulnerablePower>() == null && r.Player.Creature.GetPowerAmount<WeakPower>() == 1);
        });
        Test("运用-Hs重复施加累计扣除并仅在敌人回合末返还", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationHs>(); var enemy = Enemy(r);
            await Play(r, card); await Play(r, card); Check(enemy.GetPowerAmount<StrengthPower>() == -10, $"Hs repeated strength: {enemy.GetPowerAmount<StrengthPower>()}");
            await enemy.GetPower<ApplicationHsStrengthDownPower>()!.AfterSideTurnEnd(r.Choice, CombatSide.Enemy, new[] { enemy });
            Check(enemy.GetPowerAmount<StrengthPower>() == 4, $"Hs repeated restoration: {enemy.GetPowerAmount<StrengthPower>()}");
        });
        Test("运用-Co首牌已侵蚀仍保留原侵蚀并获得晶化，满手/禁止抽牌不造假", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationCo>(); var first = new TypedProbe(CardType.Skill) { Owner = r.Player, CombatState = r.State, Affliction = new DummyAffliction() };
            Put(r, first, PileType.Draw); await Play(r, card); Check(first.Affliction is DummyAffliction && first.SingleTurnRetain);
            await r.Player.Creature.GetPower<ApplicationCoPower>()!.BeforeCardPlayed(new() { Card = first });
            Check(r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().Count() == 2);
            var blocked = new Rig(); CardPileCmd.DrawBlocked = true; var co = blocked.Crystal<ApplicationCo>(); var untouched = blocked.Crystal<TestAttack>(); Put(blocked, untouched, PileType.Draw);
            await Play(blocked, co); Check(untouched.Pile?.Type == PileType.Draw && !untouched.SingleTurnRetain && untouched.Affliction == null);
            var full = new Rig(); var fullCo = full.Crystal<ApplicationCo>();
            for (int i = 0; i < 10; i++) Put(full, new TypedProbe(CardType.Skill) { Owner = full.Player, CombatState = full.State });
            var waiting = full.Crystal<TestAttack>(); Put(full, waiting, PileType.Draw); await Play(full, fullCo);
            Check(waiting.Pile?.Type == PileType.Draw && !waiting.SingleTurnRetain && waiting.Affliction == null);
        });
        Test("运用-Cu过滤非攻击、格挡、其他目标，单击可以多次触发", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationCu>(); var enemy = Enemy(r); var other = Enemy(r);
            await Play(r, card, enemy); var power = enemy.GetPower<ApplicationCuPower>()!;
            await Hit(r, power, enemy, 50, ValueProp.Unpowered | ValueProp.Move); await Hit(r, power, enemy, 50, ValueProp.Unblockable);
            await Hit(r, power, enemy, 0); await Hit(r, power, other, 50); Check(enemy.GetPowerAmount<PoisonPower>() == 0);
            await Hit(r, power, enemy, 22); Check(enemy.GetPowerAmount<PoisonPower>() == 6);
            await Hit(r, power, enemy, 6); Check(enemy.GetPowerAmount<PoisonPower>() == 8);
        });
        Test("运用-Cu他人同阵营回合末不移除，本人回合末移除", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationCu>(); var enemy = Enemy(r); await Play(r, card, enemy);
            var power = enemy.GetPower<ApplicationCuPower>()!; var peer = new Player(); peer.Creature.CombatState = r.State;
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { peer.Creature }); Check(enemy.Powers.Contains(power));
            await Hit(r, power, enemy, 7, dealer: peer.Creature); Check(enemy.GetPowerAmount<PoisonPower>() == 2);
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Enemy, new[] { enemy }); Check(enemy.Powers.Contains(power));
            await power.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(!enemy.Powers.Contains(power));
        });
        Test("运用-Cu预约不跨新战斗或本人回合，目标不能获得能力时不强行配置", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationCu>(); var enemy = Enemy(r); await Play(r, card, enemy);
            var power = enemy.GetPower<ApplicationCuPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++; await Hit(r, power, enemy, 7); Check(enemy.GetPowerAmount<PoisonPower>() == 0);
            r.Player.PlayerCombatState.TurnNumber--; enemy.CombatState = new FakeCombat(); await Hit(r, power, enemy, 7); Check(enemy.GetPowerAmount<PoisonPower>() == 0);
            var rejected = Enemy(r); rejected.CanReceivePowers = false; await Play(r, card, rejected); Check(rejected.Powers.Count == 0);
        });
        Test("运用-Cu同本人回合重施保留余数，下一本人回合不带入旧余数", async () =>
        {
            var r = new Rig(); var card = r.Crystal<ApplicationCu>(); var enemy = Enemy(r); await Play(r, card, enemy);
            var power = enemy.GetPower<ApplicationCuPower>()!; await Hit(r, power, enemy, 5);
            power.Configure(r.Player.Creature, 7, 2); await Hit(r, power, enemy, 2); Check(enemy.GetPowerAmount<PoisonPower>() == 2);
            await Hit(r, power, enemy, 5); r.Player.PlayerCombatState!.TurnNumber++;
            await Play(r, card, enemy); await Hit(r, power, enemy, 2); Check(enemy.GetPowerAmount<PoisonPower>() == 2);
            await Hit(r, power, enemy, 5); Check(enemy.GetPowerAmount<PoisonPower>() == 4);
        });
        Test("运用-Cu混合升级重施的连锁攻击在施加返回前使用新阈值", async () =>
        {
            var r = new Rig(); var normal = r.Crystal<ApplicationCu>(); var upgraded = r.Crystal<ApplicationCu>(); upgraded.Upgrade(); var enemy = Enemy(r);
            await Play(r, normal, enemy); var power = enemy.GetPower<ApplicationCuPower>()!; await Hit(r, power, enemy, 5);
            r.State.Listeners.Add(new PowerChangeObserver(async changed => { if (changed == power) await Hit(r, power, enemy, 1); }));
            await Play(r, upgraded, enemy); Check(enemy.GetPowerAmount<PoisonPower>() == 2);
        });
        Test("运用-Cu嵌套打出升级牌后外层返回不能回滚最新配置", async () =>
        {
            var r = new Rig(); var normal = r.Crystal<ApplicationCu>(); var upgraded = r.Crystal<ApplicationCu>(); upgraded.Upgrade(); var enemy = Enemy(r);
            bool nested = false;
            r.State.Listeners.Add(new PowerChangeObserver(async changed =>
            {
                if (changed is ApplicationCuPower && !nested) { nested = true; await Play(r, upgraded, enemy); }
            }));
            DamageCmd.AfterAttackExecuted = async command =>
            {
                if (command.ModelSource is ApplicationCu && enemy.GetPower<ApplicationCuPower>() is { } existing)
                    await Hit(r, existing, enemy, 13);
            };
            await Play(r, normal, enemy); Check(nested && enemy.GetPowerAmount<PoisonPower>() == 2);
            await Hit(r, enemy.GetPower<ApplicationCuPower>()!, enemy, 6); Check(enemy.GetPowerAmount<PoisonPower>() == 6);
        });
        foreach (bool ended in new[] { false, true })
        {
            bool over = ended;
            Test($"运用-F基础伤害后结束阶段或主人死亡时分支牌不进入战斗牌堆 ended={over}", async () =>
            {
                var r = new Rig(energy: 0); var card = r.Crystal<ApplicationF>(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = _ => { if (over) CombatManager.Instance.IsOverOrEnding = true; else r.Player.Creature.IsAlive = false; return Task.CompletedTask; };
                await Play(r, card, enemy); Check(r.Player.PlayerCombatState!.AllCards.Count() == 0);
            });
        }
        Test("晶化Mg实际衍生牌0费保留消耗并获得4格挡", async () =>
        {
            var r = new Rig(); var card = r.Crystal<CrystallizationMg>(); await Play(r, card);
            Check(card.EnergyCost.Value == 0 && card.Type == CardType.Status && r.Player.Creature.Block == 4
                && card.Keywords.Contains(CardKeyword.Retain) && card.Keywords.Contains(CardKeyword.Exhaust));
        });
        Test("运用-Zn双分支固定先失能量再消耗，不重新读取分支", async () =>
        {
            var r = new Rig(energy: 1); var card = r.Crystal<ApplicationZn>(); Put(r, card);
            r.Modify((_, _) => CrystalBranch.Both); await card.OnPlayWrapper(r.Choice);
            Check(r.Energy == 0 && card.Pile?.Type == PileType.Exhaust && CreatureCmd.BlockGains.SequenceEqual(new[] { 12 }));
        });
        foreach (bool magnesium in new[] { false, true })
        {
            bool mg = magnesium;
            Test($"运用-{(mg ? "Mg" : "As")}基础攻击失败不生成后续晶化", async () =>
            {
                var r = new Rig(); CardModel card = mg ? r.Crystal<ApplicationMg>() : r.Crystal<ApplicationAs>(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = _ => throw new InvalidOperationException("attack hook failure");
                try { await Play(r, card, enemy); throw new Exception("must propagate attack failure"); }
                catch (InvalidOperationException) { }
                Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 0);
            });
        }
    }
    private sealed class GeneratedObserver(Action<CardModel> observe) : AbstractModel
    {
        public override Task AfterCardChangedPiles(CardModel card, PileType oldPile, AbstractModel? clonedBy)
        { observe(card); return Task.CompletedTask; }
    }
    private sealed class DummyAffliction : AfflictionModel { }
    private sealed class PowerChangeObserver(Func<PowerModel, Task> observe) : AbstractModel
    {
        public override Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            PowerModel power, decimal amount, Creature? applier, CardModel? source) => observe(power);
    }
}
