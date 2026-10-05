using IndiumStar.Cards;
using IndiumStar.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

public static class DeferredReleaseTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Deferred release assertion failed"); }
    private static void Put(Rig r, CardModel card, PileType pile)
    {
        card.Pile?.Cards.Remove(card); card.Pile = pile.GetPile(r.Player); card.Pile.Cards.Add(card);
    }
    private static async Task<DeferredReleasePower> Queue(Rig r, params CardModel[] cards)
    {
        var power = await PowerCmd.Apply<DeferredReleasePower>(r.Choice, r.Player.Creature, cards.Length, null, null);
        foreach (var card in cards) { Put(r, card, PileType.Exhaust); power!.Schedule(card, 2, r.State); }
        return power!;
    }
    public static void Register(List<(string Name, Func<Task> Run)> tests, string[] args)
    {
        void Test(string name, Func<Task> body) => tests.Add((name, body));
        foreach (string mode in new[] { "success", "blocked", "redirected", "empty" }) foreach (bool upgraded in new[] { false, true })
        {
            string m = mode; bool up = upgraded;
            Test($"未命名技能只为成功消耗创建下回合预约 {m} upgraded={up}", async () =>
            {
                var r = new Rig(); var card = r.Crystal<DeferredRelease>(); if (up) card.Upgrade();
                var selected = r.Crystal<DeferredProbe>(); if (m != "empty") Put(r, selected, PileType.Hand);
                CardCmd.ExhaustBlocked = m == "blocked";
                if (m == "redirected") CardCmd.ExhaustDestinationOverride = PileType.Discard;
                await card.Play(r.Choice, new() { Card = card });
                Check(selected.Plays == 0 && card.EnergyCost.Value == 1 && CardPileCmd.DrawCount == 0);
                Check(r.Player.Creature.GetPowerAmount<DeferredReleasePower>() == (m == "success" ? 1 : 0));
                if (m == "success") Check(selected.Pile?.Type == PileType.Exhaust);
                if (m == "blocked") Check(selected.Pile?.Type == PileType.Hand);
            });
        }
        Test("预约只在本人下一回合兑现并传递真实选择上下文", async () =>
        {
            var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
            r.State.Turn = 100; // 全局战斗回合变化不能代替本人回合推进。
            await power.BeforeHandDraw(r.Player, r.Choice, r.State); Check(selected.Plays == 0);
            var other = new Player(); other.PlayerCombatState!.TurnNumber = 2;
            await power.BeforeHandDraw(other, r.Choice, r.State); Check(selected.Plays == 0);
            r.Player.PlayerCombatState!.TurnNumber = 2; r.Energy = 5;
            await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(selected.Plays == 1 && selected.LastContext == r.Choice && selected.LastWasAuto && selected.EnergyAtPlay == 5);
            Check(selected.Pile?.Type == PileType.Discard && r.Player.PlayerCombatState.ExhaustPile.Cards.Count == 0);
            Check(r.Player.Creature.GetPower<DeferredReleasePower>() == null && CardPileCmd.DrawCount == 0);
            await power.BeforeHandDraw(r.Player, r.Choice, r.State); Check(selected.Plays == 1);
        });
        foreach (PileType pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard })
        {
            var destination = pile;
            Test($"预约跟随原牌身份，移入{destination}后仍能兑现", async () =>
            {
                var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
                Put(r, selected, destination); r.Player.PlayerCombatState!.TurnNumber = 2;
                await power.BeforeHandDraw(r.Player, r.Choice, r.State);
                Check(selected.Plays == 1 && CombatCard(r) == selected);
            });
        }
        foreach (string reason in new[] { "removed", "foreignOwner", "foreignCombat", "playing" })
        {
            string why = reason;
            Test($"预约不会拉回已失效或正在打出的原牌 {why}", async () =>
            {
                var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
                if (why == "removed") selected.Pile = null;
                if (why == "foreignOwner") selected.Owner = new Player();
                if (why == "foreignCombat") selected.CombatState = new FakeCombat();
                if (why == "playing") selected.Pile = new() { Type = PileType.Play };
                r.Player.PlayerCombatState!.TurnNumber = 2; await power.BeforeHandDraw(r.Player, r.Choice, r.State);
                Check(selected.Plays == 0 && r.Player.Creature.GetPower<DeferredReleasePower>() == null);
            });
        }
        Test("兑现中新增预约及递归回调不会提前执行", async () =>
        {
            var r = new Rig(); var first = r.Crystal<DeferredProbe>(); var later = r.Crystal<DeferredProbe>();
            var power = await Queue(r, first); Put(r, later, PileType.Exhaust);
            first.Effect = async () =>
            {
                power.Schedule(later, 3, r.State);
                await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            };
            r.Player.PlayerCombatState!.TurnNumber = 2; await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(first.Plays == 1 && later.Plays == 0 && power.Amount == 1 && power.DisplayAmount == 1);
            r.Player.PlayerCombatState.TurnNumber = 3; await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(later.Plays == 1 && r.Player.Creature.GetPower<DeferredReleasePower>() == null);
        });
        Test("多份预约按消费顺序兑现，异常不重试已尝试牌且保留未尝试牌", async () =>
        {
            var r = new Rig(); var first = r.Crystal<DeferredProbe>(); var second = r.Crystal<DeferredProbe>();
            first.Effect = () => throw new InvalidOperationException("test failure after partial effects");
            var power = await Queue(r, first, second); r.Player.PlayerCombatState!.TurnNumber = 2;
            bool thrown = false; try { await power.BeforeHandDraw(r.Player, r.Choice, r.State); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown && first.Plays == 1 && second.Plays == 0 && power.DisplayAmount == 1);
            await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(first.Plays == 1 && second.Plays == 1 && r.Player.Creature.GetPower<DeferredReleasePower>() == null);
        });
        Test("阻止自动出牌时预约不会在下一回合重试", async () =>
        {
            var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
            CardCmd.ShouldAutoPlay = _ => false; r.Player.PlayerCombatState!.TurnNumber = 2;
            await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(selected.Plays == 0 && r.Player.Creature.GetPower<DeferredReleasePower>() == null);
            CardCmd.ShouldAutoPlay = _ => true; r.Player.PlayerCombatState.TurnNumber = 3;
            await power.BeforeHandDraw(r.Player, r.Choice, r.State); Check(selected.Plays == 0);
        });
        Test("自动兑现晶化不受手动额度限制也不会消耗额外额度", async () =>
        {
            var r = new Rig(); var crystal = r.Crystal<Crystallization>(); var power = await Queue(r, crystal);
            r.Player.PlayerCombatState!.TurnNumber = 2; r.State.Turn = 2; r.Record(r.Crystal<CrystalA>());
            await power.BeforeHandDraw(r.Player, r.Choice, r.State);
            Check(CombatCard(r) == crystal && crystal.Pile?.Type == PileType.Exhaust);
            Check(MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysStarted.Count == 2);
            Check(CrystallizationRules.HasPlayedCrystallizationThisTurn(crystal));
        });
        Test("旧战斗的预约不会在新战斗兑现", async () =>
        {
            var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
            var newCombat = new FakeCombat(); r.Player.Creature.CombatState = newCombat;
            r.Player.PlayerCombatState!.TurnNumber = 2;
            await power.BeforeHandDraw(r.Player, r.Choice, newCombat);
            Check(selected.Plays == 0 && r.Player.Creature.GetPower<DeferredReleasePower>() == null);
        });
        Test("队列克隆不共享容器，非法到期回合不改变预约", async () =>
        {
            var r = new Rig(); var first = r.Crystal<DeferredProbe>(); var power = await Queue(r, first);
            var copy = (DeferredReleasePower)power.ToMutable(); copy.Schedule(r.Crystal<DeferredProbe>(), 2, r.State);
            Check(power.DisplayAmount == 1 && copy.DisplayAmount == 2);
            bool thrown = false; try { power.Schedule(first, 0, r.State); } catch (ArgumentOutOfRangeException) { thrown = true; }
            Check(thrown && power.DisplayAmount == 1);
        });
        Test("战斗结束或玩家死亡时不兑现预约", async () =>
        {
            foreach (bool dead in new[] { false, true })
            {
                var r = new Rig(); var selected = r.Crystal<DeferredProbe>(); var power = await Queue(r, selected);
                r.Player.PlayerCombatState!.TurnNumber = 2;
                r.Player.Creature.IsAlive = !dead; MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding = !dead;
                await power.BeforeHandDraw(r.Player, r.Choice, r.State); Check(selected.Plays == 0);
            }
        });
        Test("未命名技能及预约能力双语文本齐备", () =>
        {
            foreach (string language in new[] { "zhs", "eng" }) foreach (string table in new[] { "cards", "powers" })
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(
                    args[1], "IndiumStar", "localization", language, table + ".json")));
                string prefix = table == "cards" ? "INDIUM_STAR_CARD_DEFERRED_RELEASE" : "INDIUM_STAR_POWER_DEFERRED_RELEASE_POWER";
                Check(doc.RootElement.TryGetProperty(prefix + ".title", out _) && doc.RootElement.TryGetProperty(prefix + ".description", out _));
            }
            return Task.CompletedTask;
        });
    }
    private static CardModel CombatCard(Rig r) => MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysStarted.Last().CardPlay.Card;
}
public sealed class DeferredProbe : ModCardTemplate
{
    public DeferredProbe() : base(3, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public int Plays { get; private set; }
    public int EnergyAtPlay { get; private set; }
    public bool LastWasAuto { get; private set; }
    public PlayerChoiceContext? LastContext { get; private set; }
    public Func<Task>? Effect { get; set; }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        Plays++; LastContext = context; LastWasAuto = play.IsAutoPlay; EnergyAtPlay = Owner.PlayerCombatState!.Energy;
        if (Effect != null) await Effect();
    }
}
