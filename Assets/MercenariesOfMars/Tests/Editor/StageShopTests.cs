using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MercenariesOfMars.Tests
{
    public sealed class StageShopTests
    {
        [Test] public void 추가조건이_없으면_항상판매로_처리하지않는다()
        {
            var item=new ConditionalShopAddition();
            Assert.That(item.Validate(),Has.Count.GreaterThan(0));
            Assert.That(item.Matches(null,_=>true,Array.Empty<CharacterDefinition>(),Array.Empty<MechDefinition>(),Array.Empty<PilotDefinition>()),Is.False);
        }
        [Test] public void 잘못된_조건과_빈품목은_에디터_검증에서_안내한다()
        {
            var shop=new StageShopSettings();shop.weapons.Add(null);
            shop.additions.Add(new ConditionalShopAddition {resultConditions=new(){new StageCondition {kind=StageConditionKind.Flag,key=""}}});
            shop.additions.Add(new ConditionalShopAddition {rosterConditions=new(){new DeploymentCondition {kind=DeploymentConditionKind.OwnedMech}}});
            Assert.That(shop.Validate(),Has.Count.EqualTo(3));
        }
        [Test] public void 빈조건목록과_잘못된조건종류를_안전하게_거절한다()
        {
            var item=new ConditionalShopAddition {resultConditions=null};Assert.That(item.Validate(),Has.Count.EqualTo(1));
            item.resultConditions=new List<StageCondition>{new StageCondition {kind=(StageConditionKind)999}};Assert.That(item.Validate(),Has.Count.EqualTo(1));
            item.resultConditions.Clear();item.rosterConditions.Add(null);Assert.That(item.Validate(),Has.Count.EqualTo(1));
        }
    }
}
