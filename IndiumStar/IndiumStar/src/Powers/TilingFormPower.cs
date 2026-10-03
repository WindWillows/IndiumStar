using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Powers;

/// <summary>密铺形态的开关；手牌光环由控制器同步到本体力量/敏捷。</summary>
[RegisterPower]
public sealed class TilingFormPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("strength_power");

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        CrystalHandAuraPower.EnsureAndSync(Owner);
}
