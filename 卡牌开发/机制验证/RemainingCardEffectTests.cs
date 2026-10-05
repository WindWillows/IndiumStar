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
using MegaCrit.Sts2.Core.ValueProps;

// 运行整张牌的实际源码；不以指令替身代替原生战斗验收。
public static class RemainingCardEffectTests
{
    private static void Check(bool condition, string message = "Remaining card effect assertion failed")
    { if (!condition) throw new Exception(message); }
    private static void Put(Rig r, CardModel card, PileType pile = PileType.Hand)
    { card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card); }
    private static Creature Enemy(Rig r)
    { var c = new Creature { CombatState = r.State }; r.State.CreatureList.Add(c); return c; }
    private static Task Play(Rig r, CardModel c, Creature? target = null) => c.Play(r.Choice, new() { Card = c, Target = target });
    private static CardModel Probe(Rig r, CardType type = CardType.Skill) => new TypedProbe(type) { Owner = r.Player, CombatState = r.State };
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> run) => tests.Add((name, run));
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            Test($"打击基础伤害与Strike标签 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<StrikeIndiumStar>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                await Play(r, c, enemy); var a = DamageCmd.Attacks.Single();
                Check(a.Damage == (up ? 9 : 6) && a.Hits == 1 && a.Source == c && a.Target == enemy
                    && c.Tags.Contains(CardTag.Strike) && c.EnergyCost.Value == 1);
            });
            Test($"防御基础格挡与Defend标签 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<DefendIndiumStar>(); if (up) c.Upgrade();
                await PowerCmd.Apply<DexterityPower>(r.Choice, r.Player.Creature, 2, null, null);
                await Play(r, c); Check(r.Player.Creature.Block == (up ? 10 : 7) && c.Tags.Contains(CardTag.Defend) && c.EnergyCost.Value == 1);
            });
            Test($"合斩一次指令攻击全部存活敌人 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<Slash>(); if (up) c.Upgrade(); var a = Enemy(r); var b = Enemy(r); Enemy(r).IsAlive = false;
                bool observed = false;
                DamageCmd.AfterAttackExecuted = command => { observed = true; Check(command.Targets.SequenceEqual(new[] { a, b })); return Task.CompletedTask; };
                await Play(r, c); Check(observed && DamageCmd.Attacks.Single().Damage == (up ? 7 : 5) && c.EnergyCost.Value == 0);
            });
            foreach (int energy in new[] { 0, 1 })
            {
                int e = energy;
                Test($"分析可见效果计数、无视格挡及固定分支 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<Analyze>(); if (up) c.Upgrade(); Put(r, c); var enemy = Enemy(r); enemy.Block = 100;
                    r.Player.Creature.Powers.Add(new StrengthPower { Owner = r.Player.Creature, Amount = 50 });
                    r.Player.Creature.Powers.Add(new HiddenPower { Owner = r.Player.Creature, Amount = 1 });
                    enemy.Powers.Add(new WeakPower { Owner = enemy, Amount = 20 }); enemy.Powers.Add(new PoisonPower { Owner = enemy, Amount = 10 });
                    enemy.Powers.Add(new HiddenPower { Owner = enemy });
                    await c.OnPlayWrapper(r.Choice, enemy);
                    Check(r.Player.Creature.Block == 2 && CreatureCmd.DirectDamages.Single().Amount == 4);
                    Check(CreatureCmd.DirectDamages.Single().Props == (ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move));
                    Check(c.Pile?.Type == (e == 0 ? PileType.Exhaust : PileType.Discard) && r.Energy == 0 && c.EnergyCost.Value == (up ? 0 : 1));
                });
                Test($"键合-酯基格挡后选择非攻击并保留，晶逆延拓 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<BondingEster>(); if (up) c.Upgrade(); Put(r, c);
                    var attack = Probe(r, CardType.Attack); var skill = Probe(r); var power = Probe(r, CardType.Power);
                    Put(r, attack); Put(r, skill); Put(r, power);
                    CardSelectCmd.SelectionOverride = (ctx, owner, prefs, filter, source) => {
                        Check(ctx == r.Choice && source == c && r.Player.Creature.Block == (up ? 8 : 6));
                        Check(prefs.MinCount == 1 && prefs.MaxCount == 1 && !filter!(attack) && filter(skill) && filter(power));
                        return Task.FromResult<IEnumerable<CardModel>>(new[] { power });
                    };
                    await c.OnPlayWrapper(r.Choice);
                    Check(power.Keywords.Contains(CardKeyword.Retain) && !skill.Keywords.Contains(CardKeyword.Retain)
                        && !attack.Keywords.Contains(CardKeyword.Retain) && r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == (e == 0 ? 1 : 0));
                });
                Test($"表征格挡后预约下回合力量或抽牌 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<Characterization>(); if (up) c.Upgrade(); await Play(r, c);
                    Check(r.Player.Creature.Block == (up ? 9 : 7) && r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
                    if (e == 0) Check(r.Player.Creature.GetPowerAmount<DrawCardsNextTurnPower>() == (up ? 3 : 2)
                        && r.Player.Creature.GetPower<CharacterizationStrengthPower>() == null);
                    else {
                        var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
                        await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
                        r.Player.PlayerCombatState!.TurnNumber++; r.State.Turn++;
                        await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                        Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == (up ? 4 : 3));
                        await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
                    }
                });
                Test($"因势利导额外攻击先于易伤，同一目标且不重复判断 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<ExploitAdvantage>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    DamageCmd.AfterAttackExecuted = command => { Check(enemy.GetPowerAmount<VulnerablePower>() == 0); return Task.CompletedTask; };
                    await Play(r, c, enemy);
                    Check(DamageCmd.Attacks.Select(a => a.Damage).SequenceEqual(e == 0 ? new decimal[] { up ? 10 : 7 } : new decimal[] { up ? 10 : 7, up ? 5 : 3 }));
                    Check(DamageCmd.Attacks.All(a => a.Target == enemy) && enemy.GetPowerAmount<WeakPower>() == (e == 0 ? 1 : 0)
                        && enemy.GetPowerAmount<VulnerablePower>() == (e == 0 ? 0 : 1));
                });
            }
            foreach (int crystals in new[] { 0, 1, 3 })
            {
                int n = crystals;
                Test($"键合-亚胺基至多两张，消耗/自动打出、临时敏捷及延拓 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<BondingImidogen>(); if (up) c.Upgrade(); var ignored = Probe(r); Put(r, ignored);
                    for (int i = 0; i < n; i++) Put(r, r.Crystal<CrystallizationMg>());
                    CardSelectCmd.SelectionOverride = (ctx, owner, prefs, filter, source) => {
                        Check(prefs.MinCount == 0 && prefs.MaxCount == 2 && !filter!(ignored));
                        return Task.FromResult<IEnumerable<CardModel>>(r.Player.PlayerCombatState!.Hand.Cards.Where(filter!).Take(2).ToArray());
                    };
                    await Play(r, c);
                    Check(r.Player.PlayerCombatState!.ExhaustPile.Cards.Count == Math.Min(2, n) && r.Player.Creature.GetPowerAmount<DexterityPower>() == 3
                        && r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 2 && c.Keywords.Contains(CardKeyword.Retain) == up);
                    Check(CombatManager.Instance.History.CardPlaysStarted.Count == (up ? Math.Min(2, n) : 0)
                        && r.Player.Creature.Block == (up ? 4 * Math.Min(2, n) : 0));
                    var p = r.Player.Creature.GetPower<BondingImidogenDexterityPower>()!;
                    await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { new Player().Creature }); Check(r.Player.Creature.GetPowerAmount<DexterityPower>() == 3);
                    await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.GetPowerAmount<DexterityPower>() == 0);
                });
                foreach (int xValue in new[] { 0, 1, 4 })
                {
                    int x = xValue;
                    Test($"温压领域按捕获X而非剩余能量，多段前获得临时力量 n={n} X={x} upgraded={up}", async () =>
                    {
                        var r = new Rig(energy: 99); var c = r.Crystal<ThermobaricField>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                        c.EnergyCost.CapturedXValue = x; c.EnergyCost.XModifier = 2;
                        for (int i = 0; i < n; i++) Put(r, r.Crystal<Crystallization>());
                        Put(r, r.Crystal<Crystallization>(), PileType.Discard);
                        DamageCmd.AfterAttackExecuted = command => { Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == n); return Task.CompletedTask; };
                        await Play(r, c, enemy); var a = DamageCmd.Attacks.Single();
                        Check(a.Damage == (up ? 9 : 7) && a.Hits == x + 2 && a.Target == enemy && r.Energy == 99);
                        if (n > 0) {
                            var p = r.Player.Creature.GetPower<ThermobaricFieldStrengthPower>()!;
                            await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
                        }
                    });
                }
            }
            foreach (int available in new[] { 0, 1, 5 })
            {
                int n = available;
                Test($"键合-硝基攻击、延拓、抽牌严格顺序与升级保留 available={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<BondingNitro>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    for (int i = 0; i < n; i++) Put(r, Probe(r), PileType.Draw);
                    DamageCmd.AfterAttackExecuted = command => { Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 0 && CardPileCmd.DrawCount == 0); return Task.CompletedTask; };
                    r.State.Listeners.Add(new DrawObserver(_ => { Check(r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 1); return Task.CompletedTask; }));
                    await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == 6 && r.Player.Creature.GetPowerAmount<EnergyNextTurnPower>() == 1
                        && r.Player.PlayerCombatState!.Hand.Cards.Count == Math.Min(1, n) && c.Keywords.Contains(CardKeyword.Retain) == up);
                });
                Test($"待发获得能量、抽牌后才禁止非攻击 available={n} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: 0); var c = r.Crystal<Primed>(); if (up) c.Upgrade();
                    for (int i = 0; i < n; i++) Put(r, Probe(r), PileType.Draw);
                    r.State.Listeners.Add(new DrawObserver(_ => { Check(r.Energy == 1 && r.Player.Creature.GetPower<OnlyAttacksThisTurnPower>() == null); return Task.CompletedTask; }));
                    await Play(r, c); var p = r.Player.Creature.GetPower<OnlyAttacksThisTurnPower>()!;
                    Check(r.Energy == 1 && r.Player.PlayerCombatState!.Hand.Cards.Count == Math.Min(n, up ? 2 : 1));
                    Check(p.ShouldPlay(Probe(r, CardType.Attack), AutoPlayType.Default) && !p.ShouldPlay(Probe(r), AutoPlayType.Default)
                        && !p.ShouldPlay(r.Crystal<Crystallization>(), AutoPlayType.Default));
                    await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { new Player().Creature }); Check(r.Player.Creature.Powers.Contains(p));
                    await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(!r.Player.Creature.Powers.Contains(p));
                });
            }
            foreach (int extension in new[] { 0, 1, 2, 3, 5 })
            {
                int n = extension;
                Test($"坍缩伤害后达阈值消耗全部延拓，仅获两能量 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: 0); var c = r.Crystal<Collapse>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    if (n > 0) await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, n, null, null);
                    DamageCmd.AfterAttackExecuted = command => { Check(r.Energy == 0 && ExtensionRules.GetAmount(r.Player) == n); return Task.CompletedTask; };
                    await Play(r, c, enemy); bool enough = n >= (up ? 2 : 3);
                    Check(DamageCmd.Attacks.Single().Damage == (up ? 33 : 27) && r.Energy == (enough ? 2 : 0)
                        && ExtensionRules.GetAmount(r.Player) == (enough ? 0 : n));
                });
            }
            foreach (int energy in new[] { 0, 1 }) foreach (bool both in new[] { false, true })
            {
                int e = energy; bool dual = both;
                Test($"构象打击基础4后读取延拓和手中晶化，分支不重判 energy={e} both={dual} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<ConformationBlow>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    await PowerCmd.Apply<EnergyNextTurnPower>(r.Choice, r.Player.Creature, 2, null, null);
                    Put(r, r.Crystal<Crystallization>()); Put(r, r.Crystal<CrystallizationMg>()); Put(r, r.Crystal<Crystallization>(), PileType.Discard);
                    if (dual) r.Modify((_, _) => CrystalBranch.Both);
                    await Play(r, c, enemy); var expected = new List<decimal> { 4 };
                    if (dual || e > 0) expected.Add(up ? 14 : 10); if (dual || e == 0) expected.Add(up ? 8 : 6);
                    Check(DamageCmd.Attacks.Select(a => a.Damage).SequenceEqual(expected) && r.Energy == 0);
                });
            }
            foreach (bool hasCrystal in new[] { false, true }) foreach (int occupied in new[] { 0, 9, 10 })
            {
                bool has = hasCrystal; int n = occupied;
                Test($"晶簇枪伤害后检查手中晶化，先生成后抽牌 has={has} occupied={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<CrystalClusterGun>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    for (int i = 0; i < n; i++) Put(r, Probe(r)); if (has) Put(r, r.Crystal<Crystallization>());
                    Put(r, r.Crystal<Crystallization>(), PileType.Exhaust); var top = Probe(r); Put(r, top, PileType.Draw);
                    await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == (up ? 8 : 6));
                    Check(CardPileCmd.DrawCount == (has ? 0 : 1) && r.Player.PlayerCombatState!.Hand.Cards.OfType<Crystallization>().Count() == (has ? 1 : n < 10 ? 1 : 0));
                    Check(top.Pile?.Type == (!has && n < 9 ? PileType.Hand : PileType.Draw));
                });
            }
            Test($"延拓展延按本人回合结束延拓层数获得格挡 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<ExtensionSpread>(); if (up) c.Upgrade(); await Play(r, c);
                var p = r.Player.Creature.GetPower<ExtensionSpreadPower>()!;
                await p.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.Block == 0);
                await ExtensionRules.Gain(r.Choice, r.Player, 4, c);
                await p.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { new Player().Creature }); Check(r.Player.Creature.Block == 0);
                await p.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.Block == 12 && c.EnergyCost.Value == (up ? 0 : 1));
            });
            foreach (int energy in new[] { 0, 1 })
            {
                int e = energy;
                Test($"基团迁移先延拓、晶抗自动晶化或晶逆置顶数量与顺序 energy={e} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<GroupMigration>(); if (up) c.Upgrade();
                    var crystal = r.Crystal<CrystallizationMg>(); var a = Probe(r); var b = Probe(r); Put(r, crystal); Put(r, a); Put(r, b);
                    CardSelectCmd.SelectionOverride = (ctx, owner, prefs, filter, source) => {
                        Check(ExtensionRules.GetAmount(owner) == (up ? 3 : 2));
                        if (e > 0) { Check(prefs.MinCount == 1 && prefs.MaxCount == 1 && filter!(crystal) && !filter(a)); return Task.FromResult<IEnumerable<CardModel>>(new[] { crystal }); }
                        Check(prefs.MinCount == (up ? 0 : 1) && prefs.MaxCount == (up ? 3 : 1));
                        return Task.FromResult<IEnumerable<CardModel>>(up ? new[] { a, b } : new[] { a });
                    };
                    await Play(r, c);
                    if (e > 0) Check(crystal.Pile?.Type == PileType.Exhaust && r.Player.Creature.Block == 4
                        && CombatManager.Instance.History.CardPlaysStarted.Single().CardPlay.IsAutoPlay);
                    else Check(r.Player.PlayerCombatState!.DrawPile.Cards.SequenceEqual(up ? new[] { b, a } : new[] { a }));
                });
            }
            Test($"溯源猛击伤害后发放下一运用免费且只消耗首段 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<OrigionStrike>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                DamageCmd.AfterAttackExecuted = _ => { Check(r.Player.Creature.GetPower<FreeApplicationPower>() == null); return Task.CompletedTask; };
                await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == (up ? 20 : 14));
                var p = r.Player.Creature.GetPower<FreeApplicationPower>()!; var application = r.Crystal<ApplicationAs>(); Put(r, application);
                Check(p.TryModifyEnergyCostInCombatLate(application, 4, out decimal cost) && cost == 0);
                await p.BeforeCardPlayed(new() { Card = application, PlayIndex = 1 }); Check(p.Amount == 1);
                await p.BeforeCardPlayed(new() { Card = application }); Check(!r.Player.Creature.Powers.Contains(p));
            });
            Test($"原波穿刺按负面种数、负力量及临时包装排除，克隆计算独立 upgraded={up}", async () =>
            {
                var r = new Rig(); var c = r.Crystal<PrimordialPierce>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                enemy.Powers.Add(new WeakPower { Owner = enemy, Amount = 8 }); enemy.Powers.Add(new VulnerablePower { Owner = enemy, Amount = 3 });
                enemy.Powers.Add(new StrengthPower { Owner = enemy, Amount = -1 }); enemy.Powers.Add(new DexterityPower { Owner = enemy, Amount = 4 });
                enemy.Powers.Add(new TemporaryDebuff { Owner = enemy, Amount = 1 });
                await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == 11 + 3 * (up ? 4 : 3));
                var clone = c.CreateClone(); clone.DynamicVars.ExtraDamage.BaseValue = 9;
                Check(clone.DynamicVars.CalculatedDamage.Calculate(enemy) == 38 && c.DynamicVars.CalculatedDamage.Calculate(enemy) == 11 + 3 * (up ? 4 : 3));
                Check(c.DynamicVars.CalculatedDamage.Calculate(null) == 11);
            });
            Test($"量子点真实卡牌施加后按全牌序号提供前两张双分支 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 1); var c = r.Crystal<QuantumDot>(); if (up) c.Upgrade(); Put(r, c); await c.OnPlayWrapper(r.Choice);
                Check(c.EnergyCost.Value == (up ? 1 : 2) && r.Player.Creature.GetPower<QuantumDotPower>() != null);
                var first = r.Crystal<ApplicationZn>(); Put(r, first); await first.OnPlayWrapper(r.Choice); Check(first.Pile?.Type == PileType.Exhaust);
                r.Energy = 1; var third = r.Crystal<ApplicationZn>(); Put(r, third); await third.OnPlayWrapper(r.Choice); Check(third.Pile?.Type == PileType.Discard);
            });
            foreach (int weakAmount in new[] { 0, 1, 5 })
            {
                int n = weakAmount;
                Test($"锈蚀之刃伤害先于虚弱翻倍，保留消耗 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<RustEdge>(); if (up) c.Upgrade(); var enemy = Enemy(r);
                    if (n > 0) await PowerCmd.Apply<WeakPower>(r.Choice, enemy, n, null, null);
                    DamageCmd.AfterAttackExecuted = _ => { Check(enemy.GetPowerAmount<WeakPower>() == n); return Task.CompletedTask; };
                    await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == (up ? 14 : 10)
                        && enemy.GetPowerAmount<WeakPower>() == 2 * n && c.Keywords.Contains(CardKeyword.Exhaust));
                });
            }
            foreach (int crystals in new[] { 0, 1, 3 })
            {
                int n = crystals;
                Test($"离析自动打出实际可选晶化后抽两张，不消耗手动额度 n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<Segregation>(); if (up) c.Upgrade();
                    for (int i = 0; i < n; i++) Put(r, r.Crystal<CrystallizationMg>());
                    for (int i = 0; i < 3; i++) Put(r, Probe(r), PileType.Draw);
                    r.State.Listeners.Add(new DrawObserver(_ => { Check(CombatManager.Instance.History.CardPlaysFinished.Count == Math.Min(n, up ? 2 : 1)); return Task.CompletedTask; }));
                    await Play(r, c); Check(r.Player.Creature.Block == 4 * Math.Min(n, up ? 2 : 1)
                        && r.Player.PlayerCombatState!.Hand.Cards.Count == n - Math.Min(n, up ? 2 : 1) + 2
                        && r.Crystal<Crystallization>().CanPlayManually);
                });
            }
            foreach (int energy in new[] { 0, 1 }) foreach (int discarded in new[] { 0, 2 })
            {
                int e = energy; int n = discarded;
                Test($"回身踢攻击后取弃牌、晶逆单回合保留及升级移除消耗 energy={e} discarded={n} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<SpinKick>(); if (up) c.Upgrade(); var enemy = Enemy(r); Put(r, c);
                    CardModel? selected = null; for (int i = 0; i < n; i++) { var p = Probe(r); Put(r, p, PileType.Discard); selected ??= p; }
                    CardSelectCmd.PileSelectionOverride = (ctx, pile, owner, prefs) => {
                        Check(DamageCmd.Attacks.Single().Hits == (up ? 3 : 2) && prefs.MinCount == 1 && ctx == r.Choice);
                        return Task.FromResult<IEnumerable<CardModel>>(selected == null ? Array.Empty<CardModel>() : new[] { selected });
                    };
                    await c.OnPlayWrapper(r.Choice, enemy); Check(DamageCmd.Attacks.Single().Damage == 2
                        && c.Keywords.Contains(CardKeyword.Exhaust) == !up && c.Pile?.Type == (up ? PileType.Discard : PileType.Exhaust));
                    if (selected != null) Check(selected.Pile?.Type == PileType.Hand && selected.SingleTurnRetain == (e == 0)
                        && !selected.Keywords.Contains(CardKeyword.Retain));
                });
            }
            foreach (int occupied in new[] { 0, 9, 10 })
            {
                int n = occupied;
                Test($"密铺形态先施加光环再晶化，只有手中晶化提供属性 occupied={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<TilingForm>(); if (up) c.Upgrade();
                    for (int i = 0; i < n; i++) Put(r, Probe(r)); Put(r, r.Crystal<Crystallization>(), PileType.Exhaust);
                    await Play(r, c); int handCrystals = Math.Min(3, 10 - n);
                    Check(r.Player.Creature.GetPower<TilingFormPower>() != null && r.Player.PlayerCombatState!.AllCards.OfType<Crystallization>().Count() == 4
                        && r.Player.Creature.GetPowerAmount<StrengthPower>() == 2 * handCrystals
                        && r.Player.Creature.GetPowerAmount<DexterityPower>() == 2 * handCrystals && c.Keywords.Contains(CardKeyword.Retain) == up);
                });
            }
            foreach (int energy in new[] { 0, 1 }) foreach (bool both in new[] { false, true })
            {
                int e = energy; bool dual = both;
                Test($"回忆-污染O打出时确定永久触发器，手动自动晶化均收益 energy={e} both={dual} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: e); var c = r.Crystal<ReminiscencePollutionO>(); if (up) c.Upgrade(); if (dual) r.Modify((_, _) => CrystalBranch.Both);
                    await Play(r, c); r.Energy = 10; var crystal = r.Crystal<Crystallization>(); Put(r, crystal);
                    await crystal.OnPlayWrapper(r.Choice); var second = r.Crystal<Crystallization>(); Put(r, second); await CardCmd.AutoPlay(r.Choice, second, null);
                    Check(r.Player.Creature.Block == (dual || e > 0 ? 8 : 0) && r.Player.Creature.GetPowerAmount<VigorPower>() == (dual || e == 0 ? 6 : 0)
                        && c.Keywords.Contains(CardKeyword.Innate) == up);
                    var power = r.Player.Creature.GetPower<ReminiscencePollutionOPower>()!; var peer = new Player(); peer.Creature.CombatState = r.State;
                    await power.AfterCardPlayed(r.Choice, new() { Card = new Crystallization { Owner = peer, CombatState = r.State } });
                    Check(r.Player.Creature.Block == (dual || e > 0 ? 8 : 0));
                });
            }
            foreach (bool hasCrystal in new[] { false, true }) foreach (bool hasDraw in new[] { false, true })
            {
                bool crystal = hasCrystal; bool draw = hasDraw;
                Test($"嬗变本人开始前变化原堆顶复制、空手空堆及升级固有 crystal={crystal} draw={draw} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<Transmutation>(); if (up) c.Upgrade(); await Play(r, c); var power = r.Player.Creature.GetPower<TransmutationPower>()!;
                    var original = r.Crystal<Crystallization>(); if (crystal) Put(r, original); var top = r.Crystal<StrikeIndiumStar>(); top.Upgrade(); if (draw) Put(r, top, PileType.Draw);
                    await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { new Player().Creature }, r.State); Check(original.Pile?.Type == (crystal ? PileType.Hand : null));
                    await power.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
                    Check(top.Pile?.Type == (draw ? PileType.Draw : null) && c.Keywords.Contains(CardKeyword.Innate) == up);
                    if (crystal && draw) {
                        var replacement = r.Player.PlayerCombatState!.Hand.Cards.Single(); Check(replacement is StrikeIndiumStar && replacement != top
                            && replacement.IsUpgraded && replacement.Owner == r.Player && replacement.DeckVersion == null && original.Pile == null);
                        replacement.DynamicVars.Damage.BaseValue = 100; Check(top.DynamicVars.Damage.BaseValue == 9);
                    } else Check(r.Player.PlayerCombatState!.Hand.Cards.Count == (crystal ? 1 : 0));
                });
            }
            foreach (int crystals in new[] { 0, 1, 3 }) foreach (bool exhausted in new[] { false, true })
            {
                int n = crystals; bool didExhaust = exhausted;
                Test($"回忆-硬钙石本人本回合消耗历史、手中晶化及全敌人 unblocked={didExhaust} n={n} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<ReminiscenceHardCalcite>(); if (up) c.Upgrade(); await Play(r, c);
                    var power = r.Player.Creature.GetPower<ReminiscenceHardCalcitePower>()!; var enemy = Enemy(r); Enemy(r).IsAlive = false;
                    for (int i = 0; i < n; i++) Put(r, r.Crystal<Crystallization>()); Put(r, r.Crystal<Crystallization>(), PileType.Discard);
                    var history = CombatManager.Instance.History.Entries;
                    history.Add(new(r.State, r.State.Turn - 1, r.Player.Creature)); history.Add(new(new FakeCombat(), r.State.Turn, r.Player.Creature));
                    history.Add(new(r.State, r.State.Turn, new Player().Creature));
                    if (didExhaust) history.Add(new(r.State, r.State.Turn, r.Player.Creature));
                    await power.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { new Player().Creature }); Check(CreatureCmd.AreaDamages.Count == 0);
                    await power.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
                    if (didExhaust && n > 0) {
                        var damage = CreatureCmd.AreaDamages.Single(); Check(damage.Amount == n * (up ? 4 : 3) && damage.Props == ValueProp.Unpowered
                            && damage.Source == r.Player.Creature && damage.Targets.SequenceEqual(new[] { enemy }));
                    } else Check(CreatureCmd.AreaDamages.Count == 0);
                });
            }
            Test($"回忆-磷语叠加次数延拓替换或并行、本人与他人重置 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 0); var c = r.Crystal<ReminiscencePhosphorus>(); if (up) c.Upgrade(); await Play(r, c);
                var p = r.Player.Creature.GetPower<ReminiscencePhosphorusPower>()!;
                await ExtensionRules.Gain(r.Choice, r.Player, 0, c); await ExtensionRules.Gain(r.Choice, r.Player, 2, c);
                Check(r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0));
                await Play(r, c); await ExtensionRules.Gain(r.Choice, r.Player, 3, c);
                Check(r.Energy == 5 && ExtensionRules.GetAmount(r.Player) == (up ? 5 : 0));
                await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { new Player().Creature }, r.State);
                await ExtensionRules.Gain(r.Choice, r.Player, 1, c); Check(r.Energy == 5);
                r.Player.PlayerCombatState!.TurnNumber++;
                await p.BeforeSideTurnStart(r.Choice, CombatSide.Player, new[] { r.Player.Creature }, r.State);
                await ExtensionRules.Gain(r.Choice, r.Player, 4, c);
                Check(r.Energy == 9 && ExtensionRules.GetAmount(r.Player) == (up ? 10 : 1));
            });
            foreach (bool repeated in new[] { false, true })
            {
                bool repeat = repeated;
                Test($"回忆-磷语施加链内额度保留施加顺序 repeat={repeat} upgraded={up}", async () =>
                {
                    var r = new Rig(energy: 0); var c = r.Crystal<ReminiscencePhosphorus>(); if (up) c.Upgrade();
                    if (repeat) { var previous = r.Crystal<ReminiscencePhosphorus>(); if (!up) previous.Upgrade(); await Play(r, previous); }
                    bool observed = false;
                    r.State.Listeners.Add(new PowerObserver(async p => { if (p is ReminiscencePhosphorusPower && !observed) {
                        observed = true; await ExtensionRules.Gain(r.Choice, r.Player, 2, c);
                    } }));
                    await Play(r, c); Check(observed && r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == ((repeat ? !up : up) ? 2 : 0), "Phosphorus slots must preserve application order");
                });
                Test($"回忆-硬钙石施加链内回合末读取累计伤害 repeat={repeat} upgraded={up}", async () =>
                {
                    var r = new Rig(); var c = r.Crystal<ReminiscenceHardCalcite>(); if (up) c.Upgrade();
                    if (repeat) { var previous = r.Crystal<ReminiscenceHardCalcite>(); if (!up) previous.Upgrade(); await Play(r, previous); }
                    Enemy(r); Put(r, r.Crystal<Crystallization>()); CombatManager.Instance.History.Entries.Add(new(r.State, r.State.Turn, r.Player.Creature));
                    bool observed = false;
                    r.State.Listeners.Add(new PowerObserver(async p => { if (p is ReminiscenceHardCalcitePower hard && !observed) {
                        observed = true; await hard.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
                    } }));
                    await Play(r, c); Check(observed && CreatureCmd.AreaDamages.Single().Amount == (repeat ? 7 : (up ? 4 : 3)), "Hard calcite must accumulate before apply chain");
                });
            }
            Test($"回忆-磷语嵌套重施后各份模式按顺序保留 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 0); var outer = r.Crystal<ReminiscencePhosphorus>(); var inner = r.Crystal<ReminiscencePhosphorus>();
                if (up) outer.Upgrade(); else inner.Upgrade(); bool nested = false;
                r.State.Listeners.Add(new PowerObserver(async p => { if (p is ReminiscencePhosphorusPower && !nested) { nested = true; await Play(r, inner); } }));
                await Play(r, outer); await ExtensionRules.Gain(r.Choice, r.Player, 2, outer);
                Check(nested && r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0));
                await ExtensionRules.Gain(r.Choice, r.Player, 3, outer);
                Check(r.Energy == 5 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 3), "Nested modes must remain independent");
            });
            Test($"回忆-硬钙石嵌套重施后总伤害正确累加 upgraded={up}", async () =>
            {
                var r = new Rig(); var outer = r.Crystal<ReminiscenceHardCalcite>(); var inner = r.Crystal<ReminiscenceHardCalcite>();
                if (up) outer.Upgrade(); else inner.Upgrade(); bool nested = false;
                r.State.Listeners.Add(new PowerObserver(async p => { if (p is ReminiscenceHardCalcitePower && !nested) { nested = true; await Play(r, inner); } }));
                await Play(r, outer); Enemy(r); Put(r, r.Crystal<Crystallization>()); CombatManager.Instance.History.Entries.Add(new(r.State, r.State.Turn, r.Player.Creature));
                await r.Player.Creature.GetPower<ReminiscenceHardCalcitePower>()!.BeforeSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature });
                Check(nested && CreatureCmd.AreaDamages.Single().Amount == 7, "Both calcite applications must contribute");
            });
            Test($"回忆-磷语能量钩子嵌套改配置，本次延拓模式取拦截开始快照 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 0); var initial = r.Crystal<ReminiscencePhosphorus>(); var nested = r.Crystal<ReminiscencePhosphorus>();
                if (up) initial.Upgrade(); else nested.Upgrade(); await Play(r, initial);
                PlayerCmd.AfterModifyingEnergyGain = async (_, _) => { PlayerCmd.AfterModifyingEnergyGain = null; await Play(r, nested); };
                await ExtensionRules.Gain(r.Choice, r.Player, 2, initial); Check(r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 0), "Current conversion mode must not change while energy hook awaits");
                await ExtensionRules.Gain(r.Choice, r.Player, 1, initial); Check(r.Energy == 3 && ExtensionRules.GetAmount(r.Player) == (up ? 2 : 1));
            });
        }
        Test("基团迁移磷语基础效果返能量后判断晶抗", async () =>
        {
            var r = new Rig(energy: 0); await Play(r, r.Crystal<ReminiscencePhosphorus>());
            var c = r.Crystal<GroupMigration>(); var crystal = r.Crystal<CrystallizationMg>(); Put(r, crystal);
            await Play(r, c); Check(r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == 0 && crystal.Pile?.Type == PileType.Exhaust && r.Player.Creature.Block == 4);
        });
        Test("基团迁移升级选择零张仍保留延拓，不默认强制置顶", async () =>
        {
            var r = new Rig(energy: 0); var c = r.Crystal<GroupMigration>(); c.Upgrade(); var untouched = Probe(r); Put(r, untouched);
            CardSelectCmd.SelectionOverride = (_, _, prefs, _, _) => { Check(prefs.MinCount == 0); return Task.FromResult<IEnumerable<CardModel>>([]); };
            await Play(r, c); Check(untouched.Pile?.Type == PileType.Hand && ExtensionRules.GetAmount(r.Player) == 3);
        });
        Test("键合-亚胺基选择暂停完成之前不授予敏捷或延拓", async () =>
        {
            var r = new Rig(); var c = r.Crystal<BondingImidogen>(); var tcs = new TaskCompletionSource<IEnumerable<CardModel>>();
            CardSelectCmd.SelectionOverride = (_, _, _, _, _) => tcs.Task;
            Task pending = Play(r, c); Check(!pending.IsCompleted && r.Player.Creature.GetPowerAmount<DexterityPower>() == 0 && ExtensionRules.GetAmount(r.Player) == 0);
            tcs.SetResult([]); await pending; Check(r.Player.Creature.GetPowerAmount<DexterityPower>() == 3 && ExtensionRules.GetAmount(r.Player) == 2);
        });
        Test("温压领域原始X零仍授予临时力量，攻击指令为零段", async () =>
        {
            var r = new Rig(); var c = r.Crystal<ThermobaricField>(); Put(r, r.Crystal<Crystallization>());
            await Play(r, c, Enemy(r)); Check(DamageCmd.Attacks.Single().Hits == 0 && r.Player.Creature.GetPowerAmount<StrengthPower>() == 1);
        });
        Test("晶簇枪伤害钩子已经生成晶化后不再重复生成或抽牌", async () =>
        {
            var r = new Rig(); var c = r.Crystal<CrystalClusterGun>(); DamageCmd.AfterAttackExecuted = _ => { Put(r, r.Crystal<Crystallization>()); return Task.CompletedTask; };
            await Play(r, c, Enemy(r)); Check(r.Player.PlayerCombatState!.Hand.Cards.Count == 1 && CardPileCmd.DrawCount == 0);
        });
        Test("回忆-磷语能量拦截器先占本回合名额防递归重复返能量", async () =>
        {
            var r = new Rig(energy: 0); var c = r.Crystal<ReminiscencePhosphorus>(); await Play(r, c);
            PlayerCmd.AfterModifyingEnergyGain = async (_, _) => { PlayerCmd.AfterModifyingEnergyGain = null; await ExtensionRules.Gain(r.Choice, r.Player, 3, c); };
            await ExtensionRules.Gain(r.Choice, r.Player, 2, c); Check(r.Energy == 2 && ExtensionRules.GetAmount(r.Player) == 3);
        });
        Test("回忆-污染O重复施加保持既有双收益，非晶化不触发", async () =>
        {
            var r = new Rig(energy: 1); var c = r.Crystal<ReminiscencePollutionO>(); await Play(r, c); r.Energy = 0; await Play(r, c); await Play(r, c);
            var p = r.Player.Creature.GetPower<ReminiscencePollutionOPower>()!;
            await p.AfterCardPlayed(r.Choice, new() { Card = Probe(r) }); Check(r.Player.Creature.Block == 0);
            await p.AfterCardPlayed(r.Choice, new() { Card = r.Crystal<Crystallization>() }); Check(r.Player.Creature.Block == 4 && r.Player.Creature.GetPowerAmount<VigorPower>() == 3);
        });
        Test("原波穿刺空效果目标只有基础11，正力量不计负面", async () =>
        {
            var r = new Rig(); var c = r.Crystal<PrimordialPierce>(); var enemy = Enemy(r); enemy.Powers.Add(new StrengthPower { Owner = enemy, Amount = 100 });
            await Play(r, c, enemy); Check(DamageCmd.Attacks.Single().Damage == 11);
        });
        foreach (bool upgraded in new[] { false, true })
        {
            bool up = upgraded;
            Test($"表征在本人开始阶段自动打出不能提前兑现当前回合 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 1); var c = r.Crystal<Characterization>(); if (up) c.Upgrade();
                Put(r, c, PileType.Exhaust); await CardCmd.AutoPlay(r.Choice, c, null);
                var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
                await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0, "Current start-phase play is due next own turn");
                r.Player.PlayerCombatState!.TurnNumber++; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == (up ? 4 : 3));
            });
            Test($"表征旧预约兑现前嵌套新预约，只发旧预约并保留下一回合 upgraded={up}", async () =>
            {
                var r = new Rig(energy: 1); var c = r.Crystal<Characterization>(); if (up) c.Upgrade(); await Play(r, c);
                var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!; r.Player.PlayerCombatState!.TurnNumber++;
                await Play(r, c); await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                int amount = up ? 4 : 3; Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == amount, "Fresh appointment is due one turn later");
                await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(p.Amount == amount && r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
                r.Player.PlayerCombatState.TurnNumber++; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == amount);
            });
        }
        Test("表征异战斗、他人和重复开始回调不能兑现或重复授予", async () =>
        {
            var r = new Rig(energy: 1); await Play(r, r.Crystal<Characterization>()); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++;
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, new FakeCombat());
            await p.AfterSideTurnStart(CombatSide.Player, new[] { new Player().Creature }, r.State); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
        });
        Test("表征实际力量授予受阻时不在回合末倒扣", async () =>
        {
            var r = new Rig(energy: 1); await Play(r, r.Crystal<Characterization>()); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++; r.Player.Creature.CanReceivePowers = false;
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); r.Player.Creature.CanReceivePowers = true;
            await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
        });
        Test("表征发放钩子暂停期间重施与重复开始，不扩大旧预约", async () =>
        {
            var r = new Rig(energy: 1); var c = r.Crystal<Characterization>(); await Play(r, c); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++; var tcs = new TaskCompletionSource(); bool waiting = false;
            r.State.Listeners.Add(new PowerObserver(async changed => { if (changed is StrengthPower && !waiting) { waiting = true; await tcs.Task; } }));
            Task pending = p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            Check(!pending.IsCompleted && r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
            await Play(r, c); await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3); tcs.SetResult(); await pending;
            await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(p.Amount == 3 && r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
        });
        Test("表征回收连锁重施的新预约与重复结束隔离", async () =>
        {
            var r = new Rig(energy: 1); var c = r.Crystal<Characterization>(); await Play(r, c); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            bool nested = false;
            r.State.Listeners.Add(new PowerObserver(async changed => { if (changed is StrengthPower && !nested) { nested = true;
                await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); await Play(r, c);
            } }));
            await p.AfterSideTurnEnd(r.Choice, CombatSide.Player, new[] { r.Player.Creature }); Check(nested && p.Amount == 3 && r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
            r.Player.PlayerCombatState.TurnNumber++; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
        });
        Test("表征死亡或结束阶段不发放，预约保留到有效回调", async () =>
        {
            var r = new Rig(energy: 1); await Play(r, r.Crystal<Characterization>()); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            r.Player.PlayerCombatState!.TurnNumber++; r.Player.Creature.IsAlive = false;
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); r.Player.Creature.IsAlive = true; CombatManager.Instance.IsOverOrEnding = true;
            await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 0);
            CombatManager.Instance.IsOverOrEnding = false; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3);
        });
        Test("表征能力克隆的预约列表独立，不消耗原件预约", async () =>
        {
            var r = new Rig(energy: 1); await Play(r, r.Crystal<Characterization>()); var original = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
            var clone = (CharacterizationStrengthPower)original.ToMutable(); r.Player.PlayerCombatState!.TurnNumber++;
            await clone.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
            await original.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State); Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 6);
        });
        foreach (int finalOffset in new[] { 0, 1, 5 })
        {
            int offset = finalOffset;
            Test($"表征重复施加按本体最终接受层数预约 offset={offset}", async () =>
            {
                var r = new Rig(energy: 1); var c = r.Crystal<Characterization>(); await Play(r, c); var p = r.Player.Creature.GetPower<CharacterizationStrengthPower>()!;
                PowerCmd.ModifyAmountReceived = (power, amount) => power is CharacterizationStrengthPower ? offset : amount;
                await Play(r, c); PowerCmd.ModifyAmountReceived = null; Check(p.Amount == 3 + offset);
                r.Player.PlayerCombatState!.TurnNumber++; await p.AfterSideTurnStart(CombatSide.Player, new[] { r.Player.Creature }, r.State);
                Check(r.Player.Creature.GetPowerAmount<StrengthPower>() == 3 + offset, "Rejected or modified increments must not queue the requested amount");
            });
        }
    }
    private sealed class HiddenPower : PowerModel { protected override bool IsVisibleInternal => false; }
    private sealed class TemporaryDebuff : PowerModel, ITemporaryPower { public override PowerType Type => PowerType.Debuff; }
    private sealed class DrawObserver(Func<CardModel, Task> observe) : AbstractModel
    { public override Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw) => observe(card); }
    private sealed class PowerObserver(Func<PowerModel, Task> observe) : AbstractModel
    { public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel p, decimal amount, Creature? applier, CardModel? source) => observe(p); }
}
