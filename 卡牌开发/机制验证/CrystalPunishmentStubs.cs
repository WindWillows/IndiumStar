// 只用于编译与检查卡牌边界；存档使用实际游戏DLL探针，提示不模拟真实UI。
namespace MegaCrit.Sts2.Core.Saves.Runs {
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SavedPropertyAttribute : Attribute { }
}
namespace MegaCrit.Sts2.Core.HoverTips {
    public interface IHoverTip { }
    public sealed record TestPowerHoverTip(Type Type) : IHoverTip;
    public static class HoverTipFactory {
        public static IHoverTip FromPower<T>() => new TestPowerHoverTip(typeof(T));
    }
}
