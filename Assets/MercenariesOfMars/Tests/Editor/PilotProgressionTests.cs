using System;
using System.Collections.Generic;
using System.Reflection;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class PilotProgressionTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        CharacterDefinition Unit(Faction faction,int level)
        {
            var c=New<CharacterDefinition>(); c.faction=faction; c.mech=New<MechDefinition>(); c.pilot=New<PilotDefinition>(); c.pilot.level=level; return c;
        }
        [TearDown] public void Cleanup() { foreach(var o in owned) if(o!=null) Object.DestroyImmediate(o); owned.Clear(); }
        [Test] public void 레벨과_기본값과_목표값은_설정범위로_제한한다()
        {
            var p=New<PilotDefinition>(); p.level=200; p.attack=0; p.defense=2000; p.accuracy=-1; p.evasion=1000;
            typeof(PilotDefinition).GetMethod("OnValidate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,null);
            Assert.That(p.level,Is.EqualTo(99)); Assert.That(p.attack,Is.EqualTo(1)); Assert.That(p.defense,Is.EqualTo(999)); Assert.That(p.accuracy,Is.EqualTo(1)); Assert.That(p.evasion,Is.EqualTo(999));
            Assert.That(p.defenseAt99,Is.EqualTo(999)); Assert.That(p.accuracyAt99,Is.GreaterThanOrEqualTo(p.accuracy));
        }
        [Test] public void 성장값은_레벨1과99_끝점에_일치하고_항상_단조증가한다()
        {
            var p=New<PilotDefinition>(); p.attackGrowth=New<PilotGrowthPreset>(); p.attack=20; p.attackAt99=216;
            Assert.That(p.StatsAtLevel(0).Attack,Is.EqualTo(20)); Assert.That(p.StatsAtLevel(100).Attack,Is.EqualTo(216));
            int previous=0;
            for(int lv=1;lv<=99;lv++) { int value=p.StatsAtLevel(lv).Attack; Assert.That(value,Is.InRange(previous,999)); previous=value; }
            Assert.That(p.StatsAtLevel(50).Attack,Is.EqualTo(118));
        }
        [Test] public void 초반형과_후반형은_중간성장이_다르고_시작과_최종은_같다()
        {
            var p=New<PilotDefinition>(); p.attack=10; p.attackAt99=990; var preset=New<PilotGrowthPreset>(); p.attackGrowth=preset;
            int linear=p.StatsAtLevel(50).Attack;
            preset.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.7f),new Keyframe(1,1)); int early=p.StatsAtLevel(50).Attack;
            preset.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.3f),new Keyframe(1,1)); int late=p.StatsAtLevel(50).Attack;
            Assert.That(early,Is.GreaterThan(linear)); Assert.That(late,Is.LessThan(linear));
            Assert.That(p.StatsAtLevel(1).Attack,Is.EqualTo(10)); Assert.That(p.StatsAtLevel(99).Attack,Is.EqualTo(990)); Assert.That(preset.ValidatePreset(),Is.Empty);
        }
        [Test] public void 감소커브와_잘못된_끝점은_전투시작_검증에서_거절한다()
        {
            var c=Unit(Faction.Ally,10); c.pilot.attackGrowth=New<PilotGrowthPreset>();
            c.pilot.attackGrowth.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.3f,.8f),new Keyframe(.7f,.2f),new Keyframe(1,1));
            Assert.That(c.ValidateAssignment(),Is.Not.Empty); Assert.Throws<ArgumentException>(()=>new BattleUnit("오류",c,Vector2Int.zero));
            c.pilot.attackGrowth.curve=AnimationCurve.Linear(0,.1f,1,1); Assert.That(c.ValidateAssignment(),Is.Not.Empty);
        }
        [Test] public void 능력별_커브와_상한을_독립적으로_적용한다()
        {
            var p=New<PilotDefinition>(); p.attackGrowth=New<PilotGrowthPreset>(); p.attack=999; p.attackAt99=2000;
            p.defense=1; p.defenseAt99=100; p.defenseGrowth=New<PilotGrowthPreset>(); p.defenseGrowth.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.8f),new Keyframe(1,1));
            var s=p.StatsAtLevel(50); Assert.That(s.Attack,Is.EqualTo(999)); Assert.That(s.Defense,Is.EqualTo(80));
            p.defenseGrowth=null; Assert.That(p.StatsAtLevel(99).Defense,Is.EqualTo(1));
        }
        [TestCase(20,1,0,20)] [TestCase(20,1.05f,1,22)] [TestCase(20,1.10f,2,24)]
        [TestCase(10,1.05f,1,12)] [TestCase(1.5,1,0,2)] [TestCase(99,10,999,99)] [TestCase(1,0,-99,1)]
        public void 적군레벨은_배율과보정을_반올림하고_전역범위를_지킨다(double average,float ratio,int offset,int expected)
        {
            var p=New<EnemyLevelPreset>(); p.multiplier=ratio; p.offset=offset; Assert.That(p.Calculate(average),Is.EqualTo(expected));
        }
        [Test] public void 적군별_최소최대레벨도_전역범위안에서_제한한다()
        {
            var p=New<EnemyLevelPreset>(); p.minimumLevel=10; p.maximumLevel=30;
            Assert.That(p.Calculate(1),Is.EqualTo(10)); Assert.That(p.Calculate(99),Is.EqualTo(30));
            p.minimumLevel=200; p.maximumLevel=-1; Assert.That(p.Calculate(20),Is.EqualTo(99));
        }
        [Test] public void 출격아군평균은_순서와_적의원본레벨에_영향받지_않는다()
        {
            var settings=New<PilotProgressionSettings>(); settings.defaultEnemyLevel=New<EnemyLevelPreset>(); settings.defaultEnemyLevel.multiplier=1.1f; settings.defaultEnemyLevel.offset=2;
            var first=Unit(Faction.Ally,10); var second=Unit(Faction.Ally,20); var enemy=Unit(Faction.Enemy,7); enemy.pilot.experience=777;
            enemy.pilot.attackGrowth=New<PilotGrowthPreset>(); enemy.pilot.attack=20; enemy.pilot.attackAt99=216;
            var placements=new List<UnitPlacement> { new() {id="적",character=enemy,cell=new(2,0)},new() {id="아군1",character=first},new() {id="아군2",character=second,cell=Vector2Int.up} };
            var units=BattleUnitFactory.Create(placements,settings);
            Assert.That(units[0].Level,Is.EqualTo(19)); Assert.That(units[0].Attack,Is.EqualTo(56)); Assert.That(units[0].Experience,Is.EqualTo(777));
            Assert.That(enemy.pilot.level,Is.EqualTo(7)); Assert.That(enemy.pilot.experience,Is.EqualTo(777)); Assert.That(first.pilot.level,Is.EqualTo(10));
            settings.defaultEnemyLevel.multiplier=2; Assert.That(units[0].Level,Is.EqualTo(19));
            Assert.That(BattleUnitFactory.Create(placements,settings)[0].Level,Is.EqualTo(32));
        }
        [Test] public void 수동레벨과_개별보정과_아군없는_경우를_구분한다()
        {
            var settings=New<PilotProgressionSettings>(); settings.defaultEnemyLevel=New<EnemyLevelPreset>();
            var ally=Unit(Faction.Ally,20); var enemy=Unit(Faction.Enemy,7);
            var placement=new UnitPlacement { id="적",character=enemy,cell=Vector2Int.right,autoEnemyLevel=false };
            var placements=new List<UnitPlacement> {new() {id="아군",character=ally},placement};
            Assert.That(BattleUnitFactory.Create(placements,settings)[1].Level,Is.EqualTo(7));
            placement.autoEnemyLevel=true; placement.enemyLevelPreset=New<EnemyLevelPreset>(); placement.enemyLevelPreset.offset=4;
            Assert.That(BattleUnitFactory.Create(placements,settings)[1].Level,Is.EqualTo(24));
            Assert.That(BattleUnitFactory.Create(new[]{placement},settings)[0].Level,Is.EqualTo(7));
        }
        [Test] public void 기존파일럿의_성장연결은_현재레벨_능력치를_보존한다()
        {
            foreach(int level in new[]{1,14,50,99}) foreach(int value in new[]{1,4,35,700,999})
            {
                var p=New<PilotDefinition>(); p.level=level; p.attack=value;
                PilotProgressionSetup.InitializePilot(p,true);
                Assert.That(p.StatsAtLevel(level).Attack,Is.EqualTo(value),$"레벨 {level}, 이전 공격 {value}");
            }
        }
        [Test] public void 네_능력치는_서로다른_프리셋을_선택하고_미지정은_기본값을_유지한다()
        {
            var p=New<PilotDefinition>(); p.attack=p.defense=p.accuracy=p.evasion=10;
            p.attackAt99=p.defenseAt99=p.accuracyAt99=p.evasionAt99=110;
            p.attackGrowth=New<PilotGrowthPreset>(); p.defenseGrowth=New<PilotGrowthPreset>(); p.accuracyGrowth=New<PilotGrowthPreset>();
            p.defenseGrowth.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.7f),new Keyframe(1,1));
            p.accuracyGrowth.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.3f),new Keyframe(1,1));
            var s=p.StatsAtLevel(50);
            Assert.That(s.Attack,Is.EqualTo(60)); Assert.That(s.Defense,Is.EqualTo(80)); Assert.That(s.Accuracy,Is.EqualTo(40)); Assert.That(s.Evasion,Is.EqualTo(10));
            p.attackGrowth=p.defenseGrowth; Assert.That(p.StatsAtLevel(50).Attack,Is.EqualTo(80));
        }
        [Test] public void 기존_다중커브를_이전하면_모든레벨과_개별선택이_보존된다()
        {
            var p=New<PilotDefinition>(); var old=New<PilotGrowthPreset>(); p.growthPreset=old;
            old.defense=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,.7f),new Keyframe(1,1));
            old.accuracy=AnimationCurve.Linear(0,0,1,1); old.evasion=AnimationCurve.Linear(0,0,1,1);
            p.evasionGrowth=New<PilotGrowthPreset>(); var selected=p.evasionGrowth;
            var before=new PilotStats[99]; for(int lv=1;lv<=99;lv++) before[lv-1]=p.StatsAtLevel(lv);
            PilotGrowthMigration.ConvertPilot(p,(source,curve,stat)=> { var g=New<PilotGrowthPreset>(); g.curve=curve??AnimationCurve.Linear(0,0,1,1); return g; });
            Assert.That(p.growthPreset,Is.Null); Assert.That(p.evasionGrowth,Is.SameAs(selected));
            for(int lv=1;lv<=99;lv++) Assert.That(p.StatsAtLevel(lv),Is.EqualTo(before[lv-1]));
        }
        [TestCase(101,1.5f,2,154)] [TestCase(100,0,0,0)] [TestCase(100,1,-200,0)]
        [TestCase(-5,1,10,10)] [TestCase(int.MaxValue,100,100,int.MaxValue)]
        public void 경험치보정은_반올림과_음수및_오버플로_제한을_적용한다(int original,float ratio,int offset,int expected)
        {
            var p=New<EnemyLevelPreset>(); p.experienceMultiplier=ratio; p.experienceOffset=offset;
            Assert.That(p.CalculateExperience(original),Is.EqualTo(expected));
        }
        [Test] public void 경험치보정은_자동레벨과_독립적이고_배치별_선택을_전투시작에_고정한다()
        {
            var settings=New<PilotProgressionSettings>(); settings.defaultEnemyLevel=New<EnemyLevelPreset>(); settings.defaultEnemyLevel.experienceMultiplier=2;
            var enemy=Unit(Faction.Enemy,7); var special=New<EnemyLevelPreset>(); special.experienceMultiplier=3; special.experienceOffset=5;
            var placements=new[]{new UnitPlacement {id="일반",character=enemy,autoEnemyLevel=false},new UnitPlacement {id="특수",character=enemy,enemyLevelPreset=special}};
            var units=BattleUnitFactory.Create(placements,settings);
            Assert.That(units[0].Level,Is.EqualTo(7)); Assert.That(units[0].CalculateDefeatExperience(100),Is.EqualTo(200));
            Assert.That(units[1].CalculateDefeatExperience(100),Is.EqualTo(305));
            special.experienceMultiplier=9; Assert.That(units[1].CalculateDefeatExperience(100),Is.EqualTo(305));
            Assert.That(new BattleUnit("아군",Unit(Faction.Ally,1),Vector2Int.zero,null,special).CalculateDefeatExperience(100),Is.EqualTo(100));
        }
    }
}
