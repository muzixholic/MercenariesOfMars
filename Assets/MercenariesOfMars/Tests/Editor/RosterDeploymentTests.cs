using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class RosterDeploymentTests
    {
        readonly List<UnityEngine.Object> owned=new();
        T New<T>() where T:ScriptableObject{var o=ScriptableObject.CreateInstance<T>();owned.Add(o);return o;}
        CharacterDefinition Ally(){var a=New<CharacterDefinition>();a.mech=New<MechDefinition>();a.pilot=New<PilotDefinition>();return a;}
        BattleMapDefinition Map(params CharacterDefinition[] fixedAllies)
        {
            var m=New<BattleMapDefinition>();m.width=5;m.height=5;
            for(int i=0;i<fixedAllies.Length;i++)m.units.Add(new UnitPlacement {id="고정"+i,character=fixedAllies[i],cell=new Vector2Int(i,0)});
            var enemy=New<CharacterDefinition>();enemy.faction=Faction.Enemy;m.units.Add(new UnitPlacement {id="적",character=enemy,cell=new Vector2Int(4,4)});
            m.deploymentSlots.Add(new AllyDeploymentSlot {id="선택",cell=Vector2Int.up});return m;
        }
        [TearDown] public void 정리(){foreach(var o in owned)if(o!=null)UnityEngine.Object.DestroyImmediate(o);owned.Clear();}
        [Test] public void 같은조합은_맵템플릿이_아닌_영입편성을_사용한다()
        {
            var a=Ally();var template=Ally();template.mech=a.mech;template.pilot=a.pilot;
            using var result=new RosterDeployment(Map(template),new[]{a});
            Assert.That(result.Placements[0].character,Is.SameAs(a));Assert.That(result.Notices,Is.Empty);
        }
        [Test] public void 기체와파일럿이_다른편성이면_두편성을_교환하고_원본은_보존한다()
        {
            var a=Ally();var b=Ally();var target=Ally();target.mech=a.mech;target.pilot=b.pilot;var map=Map(target);
            using var result=new RosterDeployment(map,new[]{a,b});
            Assert.That(result.Notices,Has.Count.EqualTo(1));var forced=result.Placements[0].character;
            Assert.That(forced.mech,Is.SameAs(a.mech));Assert.That(forced.pilot,Is.SameAs(b.pilot));
            var remainder=result.Roster.Single(x=>x!=forced);Assert.That(remainder.mech,Is.SameAs(b.mech));Assert.That(remainder.pilot,Is.SameAs(a.pilot));
            Assert.That(a.pilot,Is.Not.SameAs(b.pilot));Assert.That(map.units[0].character,Is.SameAs(target));
            using var plan=new AllyDeploymentPlan(map,result.Roster,resolved:result);
            Assert.That(plan.Assign("선택",forced),Is.False);Assert.That(plan.Assign("선택",remainder),Is.True);
        }
        [Test] public void 미보유_필수조합은_새기체나_파일럿을_생성하지않고_거절한다()
        {var a=Ally();Assert.Throws<InvalidOperationException>(()=>new RosterDeployment(Map(Ally()),new[]{a}));}
        [Test] public void 배치조건은_플래그_영입_기체_파일럿의_AND와_OR_및_반전을_지원한다()
        {
            var a=Ally();var map=Map(a);var p=map.units[0];
            p.conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.Flag,flag="증원"});
            p.conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.RecruitedAlly,ally=a});
            p.conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.OwnedMech,mech=a.mech});
            p.conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.RecruitedPilot,pilot=a.pilot});
            using(var result=new RosterDeployment(map,new[]{a},_=>false))Assert.That(result.Placements,Has.Count.EqualTo(1));
            using(var result=new RosterDeployment(map,new[]{a},_=>true))Assert.That(result.Placements,Has.Count.EqualTo(2));
            p.conditionMatch=DeploymentConditionMatch.Any;
            using(var result=new RosterDeployment(map,new[]{a},_=>false))Assert.That(result.Placements,Has.Count.EqualTo(2));
            p.conditionMatch=DeploymentConditionMatch.All;p.conditions[0].expected=false;
            using(var result=new RosterDeployment(map,new[]{a},_=>false))Assert.That(result.Placements,Has.Count.EqualTo(2));
        }
        [Test] public void 조건불충족_고정아군은_출격자원을_예약하지않는다()
        {
            var a=Ally();var map=Map(a);map.units[0].conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.Flag,flag="증원"});
            using var plan=new AllyDeploymentPlan(map,new[]{a},_=>false);
            Assert.That(plan.CanConfirm,Is.False);Assert.That(plan.Assign("선택",a),Is.True);Assert.That(plan.CreatePlacements(),Has.Count.EqualTo(2));
        }
        [Test] public void 영입되지않은_조건부아군은_안전하게_생략한다()
        {
            var a=Ally();var optional=Ally();var map=Map(a,optional);
            map.units[1].conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.RecruitedAlly,ally=optional});
            using var result=new RosterDeployment(map,new[]{a});Assert.That(result.Placements,Has.Count.EqualTo(2));
            Assert.That(result.Placements.Any(p=>p.character==optional),Is.False);
        }
        [Test] public void 조건입력누락과_동일자원_중복필수배치를_거절한다()
        {
            var a=Ally();var map=Map(a,a);
            Assert.Throws<InvalidOperationException>(()=>new RosterDeployment(map,new[]{a}));
            map.units.RemoveAt(1);map.units[0].conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.Flag});
            Assert.That(map.ValidateMap(),Has.Some.Contains("플래그"));
        }
        [Test] public void 배치복사는_조건리스트와_조건값을_독립적으로_보존한다()
        {
            var a=Ally();var p=Map(a).units[0];p.conditionMatch=DeploymentConditionMatch.Any;
            p.conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.Flag,flag="조건",expected=false});
            var copy=p.Copy();copy.conditions[0].flag="변경";
            Assert.That(p.conditions[0].flag,Is.EqualTo("조건"));Assert.That(copy.conditions[0].expected,Is.False);Assert.That(copy.conditionMatch,Is.EqualTo(DeploymentConditionMatch.Any));
        }
    }
}
