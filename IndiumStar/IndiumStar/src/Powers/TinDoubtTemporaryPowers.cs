using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using IndiumStar.Cards;

namespace IndiumStar.Powers;

/// <summary>「回忆-锡疑」给的临时力量（本回合结束消失，逻辑在基类）。</summary>
[RegisterPower]
public sealed class TinDoubtStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<ReminiscenceTinDoubt>();
}

/// <summary>「回忆-锡疑」给的临时敏捷（本回合结束消失，逻辑在基类）。</summary>
[RegisterPower]
public sealed class TinDoubtDexterityPower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<ReminiscenceTinDoubt>();
}
