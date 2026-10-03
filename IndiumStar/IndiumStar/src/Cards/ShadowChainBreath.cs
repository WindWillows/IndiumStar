using IndiumStar.CardPools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace IndiumStar.Cards;

[RegisterCard(typeof(IndiumStarCardPool))]
public sealed class ShadowChainBreath : ModCardTemplate
{
    public ShadowChainBreath() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://images/packed/card_portraits/ironclad/defend_ironclad.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3m, ValueProp.Move)];
    public override async Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            // 生成到手牌不会触发抽牌事件；其他同牌监听者不能代替本牌生成复制。
            await CardPileCmd.AddGeneratedCardToCombat(CreateClone(), PileType.Hand, Owner);
        }
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        int count = PileType.Hand.GetPile(Owner).Cards.OfType<ShadowChainBreath>().Count();
        for (int i = 0; i < count; i++)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(1m);
}
