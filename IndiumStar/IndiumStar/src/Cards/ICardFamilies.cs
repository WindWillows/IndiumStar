namespace IndiumStar.Cards;

/// <summary>晶化牌族的稳定标记，独立于卡面名称和语言。</summary>
public interface ICrystallizationCard { }

/// <summary>运用牌族的稳定标记。ApplicationCard 本身不是运用牌族标记。</summary>
public interface IApplicationCard { }

/// <summary>
/// 延拓牌的稳定标记：卡面效果里直接包含「延拓 N」的牌。
/// 回忆-锡疑的降费以此识别；只引用延拓层数而不获得的牌（坍缩、构象打击）不算。
/// </summary>
public interface IExtensionCard { }
