using IndiumStar.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace IndiumStar.Powers;

[RegisterPower]
public sealed class NeighboringGroupStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<NeighboringGroupParticipation>();
}
