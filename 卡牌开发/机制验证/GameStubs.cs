// 仅供独立机制测试。使用小型本体替身链接真实公共机制源码，不进入模组编译。
namespace MegaCrit.Sts2.Core.Models
{
    public abstract class AbstractModel
    {
        public int FinishedCount { get; private set; }
        public void InvokeExecutionFinished() => FinishedCount++;
        public virtual int ModifyCardPlayCount(CardModel card,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, int playCount) => playCount;
    }

    public abstract class CardModel : AbstractModel
    {
        private MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet? _dynamicVars;
        public MegaCrit.Sts2.Core.Entities.Players.Player Owner { get; set; } = null!;
        public MegaCrit.Sts2.Core.Combat.ICombatState? CombatState { get; set; }
        public CardModel? DeckVersion { get; set; }
        public string Title { get; set; } = "";
        public virtual IEnumerable<MegaCrit.Sts2.Core.Entities.Cards.CardKeyword> CanonicalKeywords => [];
        protected virtual IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars => [];
        public MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet DynamicVars =>
            _dynamicVars ??= new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet(CanonicalVars);
        public virtual STS2RitsuLib.Scaffolding.Content.CardAssetProfile AssetProfile => new("");
        protected virtual bool IsPlayable => true;
        public bool CanPlayManually => IsPlayable;
        protected virtual void OnUpgrade() { }
        public void Upgrade() => OnUpgrade();
        public CardModel Clone() => (CardModel)MemberwiseClone();
        protected virtual void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description) { }
        protected abstract Task OnPlay(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play);
        public Task Play(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => OnPlay(context, play);
    }

    public class PowerModel : AbstractModel
    {
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Owner { get; set; } = null!;
        public int Amount { get; set; }
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerType Type => MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public virtual MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType => MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        public void SetAmount(int amount, bool silent = false) => Amount = amount;
        public virtual Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side,
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants,
            MegaCrit.Sts2.Core.Combat.ICombatState combatState) => Task.CompletedTask;
        public virtual Task AfterSideTurnEnd(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
            MegaCrit.Sts2.Core.Combat.CombatSide side,
            IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants) => Task.CompletedTask;
    }
}

namespace MegaCrit.Sts2.Core.Models.Powers
{
    public class EnergyNextTurnPower : MegaCrit.Sts2.Core.Models.PowerModel { }
    public class StrengthPower : MegaCrit.Sts2.Core.Models.PowerModel { }
}

namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public class Creature
    {
        public List<MegaCrit.Sts2.Core.Models.PowerModel> Powers { get; } = [];
        public T? GetPower<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => Powers.OfType<T>().FirstOrDefault();
        public int GetPowerAmount<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel => GetPower<T>()?.Amount ?? 0;
    }
}

namespace MegaCrit.Sts2.Core.Entities.Players
{
    public class Player
    {
        public PlayerCombatState? PlayerCombatState { get; set; } = new();
        public MegaCrit.Sts2.Core.Entities.Creatures.Creature Creature { get; } = new();
    }

    public class PlayerCombatState
    {
        public int Energy { get; set; }
        public MegaCrit.Sts2.Core.Entities.Cards.CardPile Hand { get; } = new();
    }
}

namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum CardType { Skill, Status }
    public enum CardRarity { Common, Basic, Uncommon }
    public enum TargetType { Self }
    public enum CardKeyword { Retain, Exhaust, Ethereal }
    public enum PileType { Hand }
    public class CardPile { public List<MegaCrit.Sts2.Core.Models.CardModel> Cards { get; } = []; }
    public static class PileExtensions
    {
        public static CardPile GetPile(this PileType type, MegaCrit.Sts2.Core.Entities.Players.Player player) =>
            player.PlayerCombatState!.Hand;
    }

    public class CardPlay
    {
        public MegaCrit.Sts2.Core.Models.CardModel Card { get; init; } = null!;
        public MegaCrit.Sts2.Core.Entities.Players.Player Player => Card.Owner;
        public bool IsAutoPlay { get; init; }
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
        public static Task AddGeneratedCardToCombat(MegaCrit.Sts2.Core.Models.CardModel card,
            MegaCrit.Sts2.Core.Entities.Cards.PileType pile, MegaCrit.Sts2.Core.Entities.Players.Player owner)
        {
            owner.PlayerCombatState!.Hand.Cards.Add(card);
            return Task.CompletedTask;
        }
    }

    public static class PowerCmd
    {
        public static MegaCrit.Sts2.Core.Models.CardModel? LastSource { get; private set; }
        public static MegaCrit.Sts2.Core.Entities.Creatures.Creature? LastApplier { get; private set; }
        public static int ApplyCount { get; set; }
        public static Task<T> Apply<T>(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature recipient, int amount,
            MegaCrit.Sts2.Core.Entities.Creatures.Creature applier, MegaCrit.Sts2.Core.Models.CardModel? source,
            bool silent = false)
            where T : MegaCrit.Sts2.Core.Models.PowerModel, new()
        {
            ApplyCount++;
            LastSource = source;
            LastApplier = applier;
            T? power = recipient.GetPower<T>();
            if (power == null) { power = new T { Owner = recipient }; recipient.Powers.Add(power); }
            power.Amount += amount;
            return Task.FromResult(power);
        }
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
        public MegaCrit.Sts2.Core.Entities.Cards.CardType Type { get; }
        protected ModCardTemplate(int cost, MegaCrit.Sts2.Core.Entities.Cards.CardType type,
            MegaCrit.Sts2.Core.Entities.Cards.CardRarity rarity, MegaCrit.Sts2.Core.Entities.Cards.TargetType target)
        { BaseCost = cost; Type = type; }
    }
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
        public decimal PreviewValue { get; set; }
        public DynamicVar(string name, decimal baseValue)
        {
            Name = name;
            BaseValue = baseValue;
            PreviewValue = baseValue;
        }
        public void UpgradeValueBy(decimal amount) => BaseValue += amount;
    }

    public class DynamicVarSet
    {
        private readonly Dictionary<string, DynamicVar> _vars;
        public DynamicVarSet(IEnumerable<DynamicVar> vars) { _vars = vars.ToDictionary(v => v.Name); }
        public DynamicVar this[string name] => _vars[name];
    }
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
