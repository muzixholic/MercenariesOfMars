using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class AllyDeploymentTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var x=ScriptableObject.CreateInstance<T>();owned.Add(x);return x;}
        CharacterDefinition Ally()=>New<CharacterDefinition>();
        BattleMapDefinition Map()
        {
            var map=New<BattleMapDefinition>();map.width=5;map.height=4;
            var enemy=New<CharacterDefinition>();enemy.faction=Faction.Enemy;
            map.units.Add(new UnitPlacement {id="적",character=enemy,cell=new Vector2Int(4,3)});
            map.deploymentSlots.Add(new AllyDeploymentSlot {id="출격1",cell=Vector2Int.zero});
            map.deploymentSlots.Add(new AllyDeploymentSlot {id="출격2",cell=Vector2Int.right});return map;
        }
        [TearDown] public void Cleanup() {foreach(var o in owned)if(o!=null)Object.DestroyImmediate(o);owned.Clear();}
        [Test] public void 고정아군이_없어도_출격칸이_있으면_맵과_도달성을_검증한다()
        {var map=Map();Assert.That(map.ValidateMap(),Is.Empty);Assert.That(map.ReachabilityWarnings(),Is.Empty);}
        [Test] public void 출격칸은_막힌지형_구조물_유닛과_겹치지_못한다()
        {
            var map=Map();Assert.That(map.CanPlaceDeploymentSlot(Vector2Int.zero),Is.False);
            Assert.That(map.CanPlaceDeploymentSlot(new Vector2Int(4,3)),Is.False);
            Assert.That(map.CanPlaceDeploymentSlot(new Vector2Int(-1,0)),Is.False);
            var wall=New<StructureDefinition>();Assert.That(map.CanPlaceStructure(wall,Vector2Int.zero),Is.False);
            map.structures.Add(new StructurePlacement {structure=wall,cell=Vector2Int.up});
            Assert.That(map.CanPlaceDeploymentSlot(Vector2Int.up),Is.False);
            var sea=New<TerrainDefinition>();sea.walkable=false;map.Paint(Vector2Int.one,sea);
            Assert.That(map.CanPlaceDeploymentSlot(Vector2Int.one),Is.False);
        }
        [Test] public void 출격칸의_중복ID_겹침_맵밖_항목을_검출한다()
        {
            var map=Map();map.deploymentSlots[0].id="적";map.deploymentSlots[1].cell=Vector2Int.zero;
            Assert.That(map.ValidateMap().Count,Is.EqualTo(2));
            map.deploymentSlots[0].cell=new Vector2Int(-1,0);Assert.That(map.ValidateMap(),Is.Not.Empty);
        }
        [Test] public void 영입되지않은_아군은_배치할수없고_최소한명은_선택해야한다()
        {
            var a=Ally();var plan=new AllyDeploymentPlan(Map(),new[]{a});
            Assert.That(plan.CanConfirm,Is.False);Assert.That(plan.Assign("출격1",Ally()),Is.False);
            Assert.That(plan.Assign("없는 칸",a),Is.False);Assert.Throws<InvalidOperationException>(()=>plan.CreatePlacements());
            Assert.That(plan.Assign("출격1",a),Is.True);Assert.That(plan.CanConfirm,Is.True);
            Assert.That(plan.Remove("출격1"),Is.True);Assert.That(plan.CanConfirm,Is.False);
        }
        [Test] public void 출격제한은_수동선택과_자동배치와_오래된확정을_막는다()
        {
            var a=Ally();bool blocked=true;using var plan=new AllyDeploymentPlan(Map(),new[]{a},isBlocked:_=>blocked);
            Assert.That(plan.Assign("출격1",a),Is.False);plan.AutoFill();Assert.That(plan.CanConfirm,Is.False);
            Assert.That(plan.SelectionRestriction(a),Does.Contain("선택 제한"));blocked=false;
            Assert.That(plan.Assign("출격1",a),Is.True);blocked=true;
            Assert.Throws<InvalidOperationException>(()=>plan.CreatePlacements());
            plan.RemoveAssignmentsFor(a);Assert.That(plan.Assignments,Is.Empty);Assert.That(plan.CanConfirm,Is.False);
        }
        [Test] public void 자동배치와_중복방지_결과는_맵과_영입목록을_변경하지않는다()
        {
            var map=Map();var a=Ally();var b=Ally();var roster=new List<CharacterDefinition> {a,b,a};
            var plan=new AllyDeploymentPlan(map,roster);Assert.That(plan.Candidates.Count,Is.EqualTo(2));
            plan.Assign("출격1",a);Assert.That(plan.Assign("출격2",a),Is.False);plan.AutoFill();
            var units=plan.CreatePlacements();Assert.That(units.Count,Is.EqualTo(3));
            Assert.That(units[1].id,Is.EqualTo("출격1"));Assert.That(units[2].character,Is.SameAs(b));
            Assert.That(map.units.Count,Is.EqualTo(1));Assert.That(roster.Count,Is.EqualTo(3));
            units[0].id="다른 ID";Assert.That(map.units[0].id,Is.EqualTo("적"));
        }
        [Test] public void 같은파일럿은_다른편성이나_고정아군으로도_중복출격할수없다()
        {
            var map=Map();var a=Ally();a.mech=New<MechDefinition>();a.pilot=New<PilotDefinition>();
            var b=Ally();b.mech=New<MechDefinition>();b.pilot=a.pilot;
            var plan=new AllyDeploymentPlan(map,new[]{a,b});Assert.That(plan.Candidates.Count,Is.EqualTo(1));
            map.units.Add(new UnitPlacement {id="고정",cell=Vector2Int.up,character=a});
            plan=new AllyDeploymentPlan(map,new[]{a});Assert.That(plan.CanConfirm,Is.True);
            Assert.That(plan.Assign("출격1",a),Is.False);
        }
        [Test] public void 선택후_변경된_잘못된편성은_확정하지않는다()
        {
            var a=Ally();var plan=new AllyDeploymentPlan(Map(),new[]{a});plan.Assign("출격1",a);a.faction=Faction.Enemy;
            Assert.Throws<InvalidOperationException>(()=>plan.CreatePlacements());
        }
        [Test] public void 배치중_맵의_출격칸이_바뀌면_오래된위치로_출격하지않는다()
        {
            var map=Map();var a=Ally();var plan=new AllyDeploymentPlan(map,new[]{a});plan.Assign("출격1",a);
            map.deploymentSlots[0].cell=Vector2Int.up;
            Assert.Throws<InvalidOperationException>(()=>plan.CreatePlacements());
        }
    }
}
