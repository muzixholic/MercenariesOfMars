using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class MechUpgradeTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var o=ScriptableObject.CreateInstance<T>();owned.Add(o);return o;}
        CharacterDefinition Formation(MechDefinition mech,Faction faction=Faction.Ally)
        {var d=New<CharacterDefinition>();d.mech=mech;d.pilot=New<PilotDefinition>();d.faction=faction;d.pilot.accuracy=999;return d;}
        [TearDown] public void 정리(){foreach(var o in owned)if(o!=null)Object.DestroyImmediate(o);owned.Clear();}

        [TestCase(0,1000)] [TestCase(1,1050)] [TestCase(2,1100)] [TestCase(3,1150)]
        [TestCase(4,1200)] [TestCase(5,1250)] [TestCase(6,1300)] [TestCase(7,1350)]
        [TestCase(8,1400)] [TestCase(9,1450)] [TestCase(10,1500)]
        public void 개조는_복리가_아닌_기본값의_5퍼센트씩_증가한다(int stage,int expected)
        {Assert.That(MechDefinition.ApplyUpgrade(1000,stage),Is.EqualTo(expected));}

        [Test] public void 기본능력치와_개조단계는_각각_허용범위로_제한한다()
        {
            var m=New<MechDefinition>();
            foreach(bool upper in new[]{false,true})
            {
                int value=upper?int.MaxValue:int.MinValue;
                m.maxHp=m.maxEnergy=m.armor=m.targeting=m.mobility=m.movement=m.price=m.maxWeaponWeight=value;
                m.hpUpgrade=m.energyUpgrade=m.armorUpgrade=m.targetingUpgrade=m.mobilityUpgrade=m.weaponUpgrade=value;
                typeof(MechDefinition).GetMethod("OnValidate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(m,null);
                Assert.That(m.maxHp,Is.EqualTo(upper?9999000:1000));Assert.That(m.maxEnergy,Is.EqualTo(upper?990:10));
                Assert.That(m.armor,Is.EqualTo(upper?9990:10));Assert.That(m.targeting,Is.EqualTo(upper?9990:10));Assert.That(m.mobility,Is.EqualTo(upper?9990:10));
                Assert.That(m.movement,Is.EqualTo(upper?99:1));Assert.That(m.price,Is.EqualTo(upper?9999000:0));Assert.That(m.maxWeaponWeight,Is.EqualTo(upper?990:0));
                foreach(var stage in new[]{m.hpUpgrade,m.energyUpgrade,m.armorUpgrade,m.targetingUpgrade,m.mobilityUpgrade,m.weaponUpgrade})Assert.That(stage,Is.EqualTo(upper?int.MaxValue:0));
            }
        }
        [Test] public void 여섯종의_개조는_독립적이며_소수점은_버리고_기본값을_보존한다()
        {
            var m=New<MechDefinition>();m.maxHp=1234;m.maxEnergy=23;m.armor=55;m.targeting=77;m.mobility=99;
            m.hpUpgrade=1;m.energyUpgrade=2;m.armorUpgrade=3;m.targetingUpgrade=4;m.mobilityUpgrade=5;m.weaponUpgrade=6;
            var u=new BattleUnit("개조",Formation(m),Vector2Int.zero);
            Assert.That(u.MaxHp,Is.EqualTo(1295));Assert.That(u.Hp,Is.EqualTo(1295));Assert.That(u.MaxEnergy,Is.EqualTo(25));Assert.That(u.Energy,Is.EqualTo(25));
            Assert.That(u.Armor,Is.EqualTo(63));Assert.That(u.Targeting,Is.EqualTo(92));Assert.That(u.Mobility,Is.EqualTo(123));
            Assert.That(m.maxHp,Is.EqualTo(1234));Assert.That(m.maxEnergy,Is.EqualTo(23));Assert.That(m.armor,Is.EqualTo(55));
            Assert.That(u.Movement,Is.EqualTo(4));Assert.That(u.MaxWeaponWeight,Is.EqualTo(100));Assert.That(u.Attack,Is.EqualTo(10));
        }
        [Test] public void 공유무장은_장착한기체의_개조단계를_사용하며_이전전투와_원본은_유지한다()
        {
            var weapon=New<SkillDefinition>();weapon.power=1000;weapon.maxAmmo=4;weapon.currentAmmo=3;weapon.energyCost=5;weapon.weight=10;
            var a=New<MechDefinition>();a.weapons.Add(weapon);var b=New<MechDefinition>();b.weaponUpgrade=5;
            var formation=Formation(a);var first=new BattleUnit("A",formation,Vector2Int.zero);
            a.weapons.Remove(weapon);b.weapons.Add(weapon);formation.mech=b;
            var second=new BattleUnit("B",formation,Vector2Int.zero);
            Assert.That(first.Skills[0].Power,Is.EqualTo(1000));Assert.That(second.Skills[0].Power,Is.EqualTo(1250));
            Assert.That(second.Skills[0].BasePower,Is.EqualTo(1000));Assert.That(second.Skills[0].UpgradeStage,Is.EqualTo(5));
            Assert.That(second.Skills[0].Definition,Is.SameAs(weapon));Assert.That(weapon.power,Is.EqualTo(1000));
            Assert.That(second.Skills[0].EnergyCost,Is.EqualTo(5));Assert.That(second.Skills[0].CurrentAmmo,Is.EqualTo(3));Assert.That(second.Skills[0].Weight,Is.EqualTo(10));
            b.weaponUpgrade=10;weapon.power=2000;
            Assert.That(second.Skills[0].Power,Is.EqualTo(1250));
            Assert.That(new BattleUnit("재출격",formation,Vector2Int.zero).Skills[0].Power,Is.EqualTo(3000));
        }
        [Test] public void 공격과_반격의_피해량은_各기체의_무장개조를_사용한다()
        {
            var weapon=New<SkillDefinition>();weapon.power=1000;
            var a=New<MechDefinition>();a.maxHp=10000;a.weaponUpgrade=5;a.weapons.Add(weapon);
            var b=New<MechDefinition>();b.maxHp=10000;b.weaponUpgrade=10;b.weapons.Add(weapon);
            var ally=new BattleUnit("아군",Formation(a),Vector2Int.zero);var enemy=new BattleUnit("적군",Formation(b,Faction.Enemy),Vector2Int.right);
            var battle=new BattleSession(new BattleGrid(3,3),new[]{ally,enemy},()=>0,true);
            Assert.That(battle.GetDamage(ally,enemy,ally.Skills[0]),Is.EqualTo(1257));
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]),Is.True);
            Assert.That(battle.ConfirmCombat(),Is.True);
            Assert.That(battle.LastAttack.Damage,Is.EqualTo(1257));Assert.That(battle.LastCounterattack.Damage,Is.EqualTo(1507));
            Assert.That(weapon.power,Is.EqualTo(1000));
        }
        [Test] public void 개조후상한은_기본값상한을_넘을수있고_큰무장공격력도_넘치지않는다()
        {
            var m=New<MechDefinition>();m.maxHp=9999000;m.maxEnergy=990;m.armor=m.targeting=m.mobility=9990;
            m.hpUpgrade=m.energyUpgrade=m.armorUpgrade=m.targetingUpgrade=m.mobilityUpgrade=10;
            Assert.That(m.EffectiveMaxHp,Is.EqualTo(14998500));Assert.That(m.EffectiveMaxEnergy,Is.EqualTo(1485));
            Assert.That(m.EffectiveArmor,Is.EqualTo(14985));Assert.That(m.EffectiveTargeting,Is.EqualTo(14985));Assert.That(m.EffectiveMobility,Is.EqualTo(14985));
            Assert.That(MechDefinition.ApplyUpgrade(int.MaxValue,10),Is.EqualTo(int.MaxValue));
            Assert.That(MechDefinition.ApplyUpgrade(1000,-1),Is.EqualTo(1000));Assert.That(MechDefinition.ApplyUpgrade(1000,11),Is.EqualTo(1550));
            Assert.That(MechDefinition.ApplyUpgrade(int.MaxValue,int.MaxValue),Is.EqualTo(int.MaxValue));
            Assert.That(MechDefinition.ApplyUpgrade(-1,10),Is.Zero);
        }
        [Test] public void 캠페인의_상한까지만_개조를_적용하고_10단계_초과도_해금할수있다()
        {
            var m=New<MechDefinition>();m.maxHp=1000;m.maxEnergy=100;m.armor=m.targeting=m.mobility=100;
            m.hpUpgrade=m.energyUpgrade=m.armorUpgrade=m.targetingUpgrade=m.mobilityUpgrade=m.weaponUpgrade=20;
            var skill=New<SkillDefinition>();skill.power=1000;m.weapons.Add(skill);var definition=Formation(m);
            foreach(int cap in new[]{0,5,10,15,20})
            {
                var u=new BattleUnit("상한",definition,Vector2Int.zero,maxUpgradeStage:cap);
                Assert.That(u.MaxHp,Is.EqualTo(1000+cap*50));Assert.That(u.Skills[0].Power,Is.EqualTo(1000+cap*50));
                Assert.That(u.MaxEnergy,Is.EqualTo(100+cap*5));Assert.That(u.Armor,Is.EqualTo(100+cap*5));Assert.That(u.Targeting,Is.EqualTo(100+cap*5));Assert.That(u.Mobility,Is.EqualTo(100+cap*5));
            }
            Assert.That(new BattleUnit("기본",definition,Vector2Int.zero).MaxHp,Is.EqualTo(1500));
            Assert.That(m.hpUpgrade,Is.EqualTo(20));Assert.That(m.maxHp,Is.EqualTo(1000));Assert.That(skill.power,Is.EqualTo(1000));
        }
    }
}
