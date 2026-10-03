// 仅供独立机制测试。使用小型本体替身链接真实公共机制源码，不进入模组编译。
namespace MegaCrit.Sts2.Core.Models
{
    public abstract class AbstractModel
    {
        public int FinishedCount { get; private set; }
        public void InvokeExecutionFinished() => FinishedCount++;
        public virtual int ModifyCardPlayCount(CardModel card,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, int playCount) => playCount;
        public virtual Task AfterCardChangedPiles(CardModel card, MegaCrit.Sts2.Core.Entities.Cards.PileType oldPile,
            AbstractModel? clonedBy) => Task.CompletedTask;
        public virtual Task AfterCreatureAddedToCombat(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => Task.CompletedTask;
        public virtual Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
        public virtual Task AfterCardPlayed(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => Task.CompletedTask;
        public virtual Task BeforeCardPlayed(MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => Task.CompletedTask;
        public virtual Task AfterCardDrawn(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            CardModel card, bool fromHandDraw) => Task.CompletedTask;
        public virtual Task AfterApplied(MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
        public virtual bool ShouldPlay(CardModel card, MegaCrit.Sts2.Core.Entities.Cards.AutoPlayType type) => true;
    }

    public abstract class CardModel : AbstractModel
    {
        private MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet? _dynamicVars;
        public MegaCrit.Sts2.Core.Entities.Players.Player Owner { get; set; } = null!;
        public MegaCrit.Sts2.Core.Combat.ICombatState? CombatState { get; set; }
        public CardModel? DeckVersion { get; set; }
        public string Title { get; set; } = "";
        public MegaCrit.Sts2.Core.Entities.Cards.CardType Type { get; protected set; }
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile? Pile { get; set; }
        public bool IsUpgraded { get; private set; }
        public virtual bool GainsBlock => false;
        public TestEnergyCost EnergyCost { get; } = new();
        public virtual IEnumerable<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> CanonicalKeywords => [];
        protected virtual IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars => [];
        public MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet DynamicVars =>
            _dynamicVars ??= new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet(CanonicalVars);
        public virtual STS2RitsuLib.Scaffolding.Content.CardAssetProfile AssetProfile => new("");
        protected virtual bool IsPlayable => true;
        public bool CanPlayManually => IsPlayable;
        public virtual bool HasTurnEndInHandEffect => false;
        protected virtual Task OnTurnEndInHand(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context) => Task.CompletedTask;
        public void GiveSingleTurnRetain() { }
        protected virtual void OnUpgrade() { }
        public void Upgrade() { IsUpgraded = true; OnUpgrade(); }
        public CardModel Clone() => (CardModel)MemberwiseClone();
        public CardModel CreateClone() {
            var clone = Clone(); clone.DeckVersion = null; clone.Pile = null;
            clone._dynamicVars = DynamicVars.Clone(); return clone;
        }
        protected virtual void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description) { }
        protected abstract Task OnPlay(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play);
        public Task Play(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => OnPlay(context, play);
    }

    public class PowerModel : AbstractModel
    {
        private object? _internalData;
        public PowerModel() { _internalData = InitInternalData(); }
        protected virtual object? InitInternalData() => null;
        protected T GetInternalData<T>() => (T)_internalData!;
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Owner { get; set; } = null!;
        public int Amount { get; set; }
        public virtual int DisplayAmount => Amount;
        protected void InvokeDisplayAmountChanged() { }
        public virtual STS2RitsuLib.Scaffolding.Content.PowerAssetProfile AssetProfile => new("");
        protected virtual bool IsVisibleInternal => true;
        protected virtual void DeepCloneFields() { }
        public PowerModel ToMutable() { var clone = (PowerModel)MemberwiseClone(); clone.DeepCloneFields(); return clone; }
        public void ApplyInternal(MegaCrit.Sts2.Core.Entities.Creatures.Creature owner, decimal amount, bool silent = false)
        { Owner = owner; Amount = (int)amount; owner.Powers.Add(this); }
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType => MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        public void SetAmount(int amount, bool silent = false) => Amount = amount;
        public virtual Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side,
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants,
            MegaCrit.Sts2.Core.Combat.ICombatState combatState) => Task.CompletedTask;
        public virtual Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
            MegaCrit.Sts2.Core.Combat.CombatSide side,
            IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) => Task.CompletedTask;
        public virtual Task BeforeApplied(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, CardModel? source) => Task.CompletedTask;
    }

    public static class ModelDb
    {
        public static T Power<T>() where T : PowerModel => Activator.CreateInstance<T>();
        public static T Card<T>() where T : CardModel => Activator.CreateInstance<T>();
    }
    public class TestEnergyCost { public int Value { get; set; } public void UpgradeBy(int amount) => Value += amount; }
}

namespace MegaCrit.Sts2.Core.Models.Powers
{
    public class EnergyNextTurnPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class StrengthPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class DexterityPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class DrawCardsNextTurnPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    // 按本体TemporaryStrengthPower的授予、追加与本回合回收顺序模拟。
    public abstract class TemporaryStrengthPower : MegaCrit.Sts2.Core.Models.PowerModel {
        public abstract MegaCrit.Sts2.Core.Models.AbstractModel OriginModel { get; }
        public override Task BeforeApplied(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source) =>
            MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(context, Owner, amount, applier, source);
        public override Task AfterPowerAmountChanged(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.PowerModel power, decimal amount, MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier,
            MegaCrit.Sts2.Core.Models.CardModel? source) => power == this && amount != Amount
                ? MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(context, Owner, amount, applier, source) : Task.CompletedTask;
        public override async Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) {
            if (!participants.Contains(Owner)) return;
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(this);
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<StrengthPower>(context, Owner, -Amount, Owner, null);
        }
    }
    public class VigorPower : MegaCrit.Sts2.Core.Models.PowerModel
    {
        private class Data { public object? Command; public int AmountAtStart; }
        protected override object InitInternalData() => new Data();
        public bool HasAttackCache => GetInternalData<Data>().Command != null;
        public Task BeforeAttack(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
        { var data = GetInternalData<Data>(); if (data.Command == null) { data.Command = command; data.AmountAtStart = Amount; } return Task.CompletedTask; }
        public async Task AfterAttack(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
        { var data = GetInternalData<Data>(); if (ReferenceEquals(data.Command, command))
            await MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(context, this, -data.AmountAtStart, null, null); }
    }
}

namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public class Creature
    {
        public MegaCrit.Sts2.Core.Combat.ICombatState? CombatState { get; set; }
        public MegaCrit.Sts2.Core.Entities.Players.Player? Player { get; set; }
        public bool IsAlive { get; set; } = true;
        public MegaCrit.Sts2.Core.Models.MonsterModel? Monster { get; set; }
        public int Block { get; set; }
        public bool IsMonster => Player == null;
        public bool CanReceivePowers { get; set; } = true;
        public List<MegaCrit.Sts2.Core.Models.PowerModel> Powers { get; } = [];
        public T? GetPower<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => Powers.OfType<T>().FirstOrDefault();
        public int GetPowerAmount<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => GetPower<T>()?.Amount ?? 0;
    }
}

namespace MegaCrit.Sts2.Core.Entities.Players
{
    public class Player
    {
        public Player() { Creature.Player = this; }
        public PlayerCombatState? PlayerCombatState { get; set; } = new();
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Creature { get; } = new();
        public FakeRunState RunState { get; } = new();
    }

    public class FakeRunState { public FakeRng Rng { get; } = new(); }
    public class FakeRng { public object CombatCardSelection { get; } = new(); }

    public class PlayerCombatState
    {
        public int Energy { get; set; }
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile Hand { get; } = new();
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile DrawPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Draw };
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile DiscardPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard };
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile ExhaustPile { get; } = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust };
    }
}

namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum CardType { Skill, Status, Attack, Power }
    public enum CardRarity { Common, Basic, Uncommon, Rare }
    public enum TargetType { Self, AnyEnemy }
    public enum CardKeyword { Retain, Exhaust, Ethereal }
    public enum PileType { Hand, Play, Exhaust, Discard, None, Draw }
    public enum CardPilePosition { Top, Bottom }
    public struct CardPileAddResult { public bool success; public PileType targetPile; }
    public enum AutoPlayType { Default }
    public class CardPile { public PileType Type { get; set; } = PileType.Hand; public List<MegaCrit.Sts2.Core.Models.CardModel> Cards { get; } = []; }
    public static class PileExtensions
    {
        public static CardPile GetPile(this PileType type, MegaCrit.Sts2.Core.Entities.Players.Player player) =>
            type switch {
                PileType.Draw => player.PlayerCombatState!.DrawPile,
                PileType.Discard => player.PlayerCombatState!.DiscardPile,
                PileType.Exhaust => player.PlayerCombatState!.ExhaustPile,
                _ => player.PlayerCombatState!.Hand
            };
    }

    public class CardPlay
    {
        public MegaCrit.Sts2.Core.Models.CardModel Card { get; init; } = null!;
        public MegaCrit.Sts2.Core.Entities.Players.Player Player => Card.Owner;
        public bool IsAutoPlay { get; init; }
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target { get; init; }
        public int PlayIndex { get; init; }
        public bool IsFirstInSeries => PlayIndex == 0;
    }
}

namespace MegaCrit.Sts2.Core.GameActions.Multiplayer
{
    public class PlayerChoiceContext { }
    public class ThrowingPlayerChoiceContext : PlayerChoiceContext { }
}

namespace MegaCrit.Sts2.Core.Entities.Powers
{
    public enum PowerType { Buff, Debuff }
    public enum PowerStackType { Counter, Single }
}

namespace MegaCrit.Sts2.Core.Combat
{
    public enum CombatSide { Player, Enemy }

    public interface ICombatState
    {
        int Turn { get; }
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Creatures { get; }
        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> Allies { get; }
        IEnumerable<MegaCrit.Sts2.Core.Models.AbstractModel> IterateHookListeners();
        T CreateCard<T>(MegaCrit.Sts2.Core.Entities.Players.Player owner) where T : MegaCrit.Sts2.Core.Models.CardModel;
    }

    public class CombatManager
    {
        public static CombatManager Instance { get; } = new();
        public bool IsOverOrEnding { get; set; }
        public bool IsStarting { get; set; }
        public History History { get; } = new();
    }

    public class History { public List<HistoryEntry> CardPlaysStarted { get; } = []; }
    public record HistoryEntry(ICombatState State, int Turn, MegaCrit.Sts2.Core.Entities.Cards.CardPlay CardPlay)
    {
        public bool HappenedThisTurn(ICombatState state) => ReferenceEquals(state, State) && state.Turn == Turn;
    }
}

namespace MegaCrit.Sts2.Core.Commands
{
    public static class CardPileCmd
    {
        public static int DrawCount { get; set; }
        public static int HandCapacity { get; set; } = 10;
        public static bool DrawBlocked { get; set; }
        public static async Task<IEnumerable<MegaCrit.Sts2.Core.Models.CardModel>> Draw(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, decimal count,
            MegaCrit.Sts2.Core.Entities.Players.Player owner) {
            DrawCount += (int)count;
            var drawn = new List<MegaCrit.Sts2.Core.Models.CardModel>();
            if (DrawBlocked) return drawn;
            for (int i = 0; i < count; i++) {
                var state = owner.PlayerCombatState!;
                if (state.DrawPile.Cards.Count == 0 || state.Hand.Cards.Count >= HandCapacity) break;
                var card = state.DrawPile.Cards[0]; state.DrawPile.Cards.RemoveAt(0);
                state.Hand.Cards.Add(card); card.Pile = state.Hand; drawn.Add(card);
                foreach (var listener in card.CombatState!.IterateHookListeners().ToArray())
                    await listener.AfterCardDrawn(context, card, false);
            }
            return drawn;
        }
        public static async Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult> AddGeneratedCardToCombat(MegaCrit.Sts2.Core.Models.CardModel card,
            MegaCrit.Sts2.Core.Entities.Cards.PileType pile, MegaCrit.Sts2.Core.Entities.Players.Player owner,
            MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition position = MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Bottom)
        {
            if (pile == MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand && owner.PlayerCombatState!.Hand.Cards.Count >= HandCapacity)
                pile = MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard;
            var destination = MegaCrit.Sts2.Core.Entities.Cards.PileExtensions.GetPile(pile, owner);
            if (position == MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Top) destination.Cards.Insert(0, card);
            else destination.Cards.Add(card);
            card.Pile = destination;
            foreach (var listener in card.CombatState!.IterateHookListeners().ToArray())
                await listener.AfterCardChangedPiles(card, MegaCrit.Sts2.Core.Entities.Cards.PileType.None, null);
            return new() { success = true, targetPile = pile };
        }
    }

    public static class PowerCmd
    {
        public static MegaCrit.Sts2.Core.Models.CardModel? LastSource { get; private set; }
        public static MegaCrit.Sts2.Core.Entities.Creatures.Creature? LastApplier { get; private set; }
        public static int ApplyCount { get; set; }
        public static async Task<T> Apply<T>(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature recipient, decimal amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source,
            bool silent = false)
            where T : MegaCrit.Sts2.Core.Models.PowerModel, new()
        {
            ApplyCount++;
            LastSource = source;
            LastApplier = applier;
            T? power = recipient.GetPower<T>();
            bool newlyApplied = power == null;
            if (power == null) { power = new T { Owner = recipient }; await power.BeforeApplied(context, amount, applier, source); recipient.Powers.Add(power); }
            power.Amount += (int)amount;
            if (newlyApplied) await power.AfterApplied(applier, source);
            if (recipient.CombatState != null) foreach (var listener in recipient.CombatState.IterateHookListeners().ToArray())
                await listener.AfterPowerAmountChanged(context, power, amount, applier, source);
            return power;
        }
        public static async Task<int> ModifyAmount(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.PowerModel power, decimal amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier, MegaCrit.Sts2.Core.Models.CardModel? source)
        { power.SetAmount(power.Amount + (int)amount); if (power.Amount == 0) await Remove(power); return power.Amount; }
        public static Task Remove(MegaCrit.Sts2.Core.Models.PowerModel power)
        {
            power.Owner.Powers.Remove(power);
            return Task.CompletedTask;
        }
    }

    public static class PlayerCmd
    {
        public static List<decimal> GoldGranted { get; } = [];
        public static Task GainGold(decimal amount, MegaCrit.Sts2.Core.Entities.Players.Player player)
        {
            GoldGranted.Add(amount);
            return Task.CompletedTask;
        }
    }
}

namespace STS2RitsuLib.Scaffolding.Content
{
    public record CardAssetProfile(string PortraitPath);
    public abstract class ModCardTemplate : MegaCrit.Sts2.Core.Models.CardModel
    {
        public int BaseCost { get; }
        protected ModCardTemplate(int cost, MegaCrit.Sts2.Core.Entities.Cards.CardType type,
            MegaCrit.Sts2.Core.Entities.Cards.CardRarity rarity, MegaCrit.Sts2.Core.Entities.Cards.TargetType target)
        { BaseCost = cost; Type = type; EnergyCost.Value = cost; }
    }
    public record PowerAssetProfile(string Path);
    public abstract class ModPowerTemplate : MegaCrit.Sts2.Core.Models.PowerModel { }
    public static class ContentAssetProfiles { public static PowerAssetProfile Power(string path) => new(path); }
}

namespace MegaCrit.Sts2.Core.Localization
{
    public class LocString { }
}

namespace MegaCrit.Sts2.Core.Localization.DynamicVars
{
    public class DynamicVar
    {
        public string Name { get; }
        public decimal BaseValue { get; set; }
        public int IntValue => (int)BaseValue;
        public decimal PreviewValue { get; set; }
        public DynamicVar(string name, decimal baseValue)
        {
            Name = name;
            BaseValue = baseValue;
            PreviewValue = baseValue;
        }
        public void UpgradeValueBy(decimal amount) => BaseValue += amount;
        public DynamicVar Clone() => (DynamicVar)MemberwiseClone();
    }

    public class DynamicVarSet
    {
        private readonly Dictionary<string, DynamicVar> _vars;
        public DynamicVarSet(IEnumerable<DynamicVar> vars) { _vars = vars.ToDictionary(v => v.Name); }
        public DynamicVar this[string name] => _vars[name];
        public DamageVar Damage => (DamageVar)_vars["Damage"];
        public BlockVar Block => (BlockVar)_vars["Block"];
        public CardsVar Cards => (CardsVar)_vars["Cards"];
        public DynamicVar Strength => _vars["StrengthPower"];
        public DynamicVarSet Clone() => new(_vars.Values.Select(v => v.Clone()));
    }
    public class DamageVar(decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props) : DynamicVar("Damage", amount)
    { public MegaCrit.Sts2.Core.ValueProps.ValueProp Props { get; } = props; }
    public class PowerVar<T>(decimal amount) : DynamicVar(typeof(T).Name, amount) { }
    public class BlockVar(decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props) : DynamicVar("Block", amount) { }
    public class CardsVar(decimal amount) : DynamicVar("Cards", amount) { }
}

namespace STS2RitsuLib.Utils
{
    public class SavedAttachedState<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _values = [];
        public SavedAttachedState(string name, Func<TValue>? defaultValueFactory = null, int order = 0) { }
        public TValue GetValueOrDefault(TKey key, TValue defaultValue) =>
            _values.TryGetValue(key, out var value) ? value : defaultValue;
        public TValue Set(TKey key, TValue value) { _values[key] = value; return value; }
    }
}

namespace STS2RitsuLib.Keywords
{
    public enum ModKeywordCardDescriptionPlacement { None, BeforeCardDescription, AfterCardDescription }

    public class ModKeywordDefinition
    {
        public string Id { get; set; } = "";
        public MegaCrit.Sts2.Core.Entities.Cards.CardKeyword CardKeywordValue { get; set; }
    }

    public class ModKeywordRegistry
    {
        private static readonly Dictionary<string, ModKeywordRegistry> Registries = [];
        private static int _nextValue = 100;
        private readonly Dictionary<string, ModKeywordDefinition> _definitions = [];
        private string _modId = "";
        public static ModKeywordRegistry For(string modId) {
            if (!Registries.TryGetValue(modId, out var registry)) {
                registry = new() { _modId = modId }; Registries[modId] = registry;
            }
            return registry;
        }
        public ModKeywordDefinition RegisterCardKeywordOwnedByLocNamespace(string localKeywordStem,
            string? iconPath, ModKeywordCardDescriptionPlacement cardDescriptionPlacement, bool includeInCardHoverTip) {
            if (_definitions.TryGetValue(localKeywordStem, out var existing)) return existing;
            var prefix = System.Text.RegularExpressions.Regex.Replace(_modId, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
            var definition = new ModKeywordDefinition { Id = prefix + "_KEYWORD_" + localKeywordStem.ToUpperInvariant(),
                CardKeywordValue = (MegaCrit.Sts2.Core.Entities.Cards.CardKeyword)_nextValue++ };
            _definitions.Add(localKeywordStem, definition); return definition;
        }
    }
}

namespace MegaCrit.Sts2.Core.Extensions
{
    public static class SelectionExtensions
    {
        // Pb的关键字回归不执行随机选牌；这里仅提供其源码需要的签名。
        public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> values, int count, object rng) => values.Take(count);
    }
}

namespace STS2RitsuLib.Interop.AutoRegistration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterCardAttribute(Type pool) : Attribute
    {
        public Type Pool { get; } = pool;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterPowerAttribute : Attribute { }
}

namespace IndiumStar.CardPools { public class IndiumStarCardPool { } }

namespace MegaCrit.Sts2.Core.ValueProps
{
    public enum ValueProp { Move }
}
namespace MegaCrit.Sts2.Core.Commands
{
    public static class CardCmd
    {
        public static Func<MegaCrit.Sts2.Core.Models.CardModel, bool> ShouldAutoPlay { get; set; } = _ => true;
        public static async Task AutoPlay(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Models.CardModel card, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
        {
            card.Owner.PlayerCombatState!.Hand.Cards.Remove(card);
            card.Pile = new() { Type = MegaCrit.Sts2.Core.Entities.Cards.PileType.Play };
            if (!ShouldAutoPlay(card) || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding) return;
            var play = new MegaCrit.Sts2.Core.Entities.Cards.CardPlay { Card = card, Target = target, IsAutoPlay = true };
            var state = card.CombatState!;
            MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysStarted.Add(new(state, state.Turn, play));
            await card.Play(context, play);
            foreach (var power in card.Owner.Creature.Powers.ToArray()) await power.AfterCardPlayed(context, play);
        }
        public static bool ExhaustBlocked { get; set; }
        public static Task<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?> Exhaust(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, MegaCrit.Sts2.Core.Models.CardModel card)
        {
            if (ExhaustBlocked || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding)
                return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(null);
            card.Owner.PlayerCombatState!.Hand.Cards.Remove(card);
            card.Pile = card.Owner.PlayerCombatState.ExhaustPile; card.Pile.Cards.Add(card);
            return Task.FromResult<MegaCrit.Sts2.Core.Entities.Cards.CardPileAddResult?>(new() { success = true, targetPile = MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust });
        }
    }
    public static class DamageCmd
    {
        public static List<int> HitCounts { get; } = [];
        public static List<(decimal Damage, int Hits, MegaCrit.Sts2.Core.Models.AbstractModel? Source, MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target,
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext? Choice)> Attacks { get; } = [];
        public static Builders.AttackCommand Attack(decimal amount) => new(amount);
    }
}
namespace MegaCrit.Sts2.Core.Commands.Builders
{
    public class AttackCommand(decimal damage = 0m)
    {
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Attacker { get; private set; } = null!;
        private int _hits = 1;
        public MegaCrit.Sts2.Core.Models.AbstractModel? ModelSource { get; private set; }
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target { get; private set; }
        public AttackCommand WithHitCount(int count) { _hits = count; return this; }
        public AttackCommand FromCard(MegaCrit.Sts2.Core.Models.CardModel card, MegaCrit.Sts2.Core.Entities.Cards.CardPlay play)
        { Attacker = card.Owner.Creature; ModelSource = card; return this; }
        public AttackCommand FromMonster(MegaCrit.Sts2.Core.Models.MonsterModel monster)
        { Attacker = monster.Creature; ModelSource = monster; return this; }
        public AttackCommand Targeting(MegaCrit.Sts2.Core.Entities.Creatures.Creature target) { Target = target; return this; }
        public AttackCommand WithHitFx(string path) => this;
        public async Task Execute(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext? choiceContext)
        {
            MegaCrit.Sts2.Core.Commands.DamageCmd.HitCounts.Add(_hits);
            MegaCrit.Sts2.Core.Commands.DamageCmd.Attacks.Add((damage, _hits, ModelSource, Target, choiceContext));
            var vigor = Attacker.GetPower<MegaCrit.Sts2.Core.Models.Powers.VigorPower>();
            if (vigor != null) { await vigor.BeforeAttack(this); await vigor.AfterAttack(choiceContext!, this); }
        }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public class NCombatRoom
    {
        public static NCombatRoom? Instance { get; set; }
        public FakeCreatureNode Node { get; } = new();
        public FakeCreatureNode GetCreatureNode(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature) => Node;
    }
    public class FakeCreatureNode
    {
        public int RefreshCount { get; private set; }
        public Task UpdateIntent(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
        { RefreshCount++; return Task.CompletedTask; }
    }
}
