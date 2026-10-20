using System;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class MechUpgradeCostTests
    {
        [TestCase(0,5000)] [TestCase(1,10000)] [TestCase(4,25000)] [TestCase(9,50000)] [TestCase(14,75000)]
        public void 무장은_목표단계당_오천을_지불한다(int current,long expected)
        {Assert.That(MechUpgradeCost.NextStage(MechUpgradeType.Weapon,current),Is.EqualTo(expected));}

        [TestCase(MechUpgradeType.Hp)] [TestCase(MechUpgradeType.Energy)] [TestCase(MechUpgradeType.Armor)]
        [TestCase(MechUpgradeType.Targeting)] [TestCase(MechUpgradeType.Mobility)]
        public void 다른_다섯항목은_각각_자신의_목표단계당_삼천을_지불한다(MechUpgradeType type)
        {
            Assert.That(MechUpgradeCost.NextStage(type,0),Is.EqualTo(3000));
            Assert.That(MechUpgradeCost.NextStage(type,1),Is.EqualTo(6000));
            Assert.That(MechUpgradeCost.NextStage(type,4),Is.EqualTo(15000));
            Assert.That(MechUpgradeCost.NextStage(type,14),Is.EqualTo(45000));
        }
        [Test] public void 여러단계의_비용은_각단계_비용의_합계다()
        {
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Weapon,0,10),Is.EqualTo(275000));
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Hp,0,10),Is.EqualTo(165000));
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Weapon,3,5),Is.EqualTo(45000));
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Energy,3,5),Is.EqualTo(27000));
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Weapon,10,10),Is.Zero);
            foreach(MechUpgradeType type in Enum.GetValues(typeof(MechUpgradeType)))
            {
                long sum=0;for(int i=0;i<20;i++)sum+=MechUpgradeCost.NextStage(type,i);
                Assert.That(MechUpgradeCost.Total(type,0,20),Is.EqualTo(sum));
            }
        }
        [Test] public void 항목별_단계를_독립적으로_읽으며_견적은_원본을_바꾸지않는다()
        {
            var m=ScriptableObject.CreateInstance<MechDefinition>();
            try
            {
                m.hpUpgrade=4;m.energyUpgrade=1;m.armorUpgrade=2;m.targetingUpgrade=3;m.mobilityUpgrade=5;m.weaponUpgrade=9;
                Assert.That(MechUpgradeCost.NextStage(MechUpgradeType.Hp,m.UpgradeStage(MechUpgradeType.Hp)),Is.EqualTo(15000));
                Assert.That(MechUpgradeCost.NextStage(MechUpgradeType.Energy,m.UpgradeStage(MechUpgradeType.Energy)),Is.EqualTo(6000));
                Assert.That(m.UpgradeStage(MechUpgradeType.Armor),Is.EqualTo(2));Assert.That(m.UpgradeStage(MechUpgradeType.Targeting),Is.EqualTo(3));
                Assert.That(m.UpgradeStage(MechUpgradeType.Mobility),Is.EqualTo(5));
                Assert.That(MechUpgradeCost.NextStage(MechUpgradeType.Weapon,m.UpgradeStage(MechUpgradeType.Weapon)),Is.EqualTo(50000));
                Assert.That(m.hpUpgrade,Is.EqualTo(4));Assert.That(m.weaponUpgrade,Is.EqualTo(9));Assert.That(m.maxHp,Is.EqualTo(1000));
            }
            finally{UnityEngine.Object.DestroyImmediate(m);}
        }
        [Test] public void 잘못된_항목과_음수단계_역방향견적은_거절한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>MechUpgradeCost.NextStage((MechUpgradeType)99,0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>MechUpgradeCost.NextStage(MechUpgradeType.Weapon,-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>MechUpgradeCost.NextStage(MechUpgradeType.Weapon,int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(()=>MechUpgradeCost.Total(MechUpgradeType.Weapon,-1,3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>MechUpgradeCost.Total(MechUpgradeType.Weapon,3,2));
        }
        [Test] public void 큰금액은_64비트로_계산하고_표현불가능한_합계는_오류로_반환한다()
        {
            Assert.That(MechUpgradeCost.NextStage(MechUpgradeType.Weapon,int.MaxValue-1),Is.EqualTo(10737418235000L));
            Assert.That(MechUpgradeCost.Total(MechUpgradeType.Weapon,0,100000),Is.EqualTo(25000250000000L));
            Assert.Throws<OverflowException>(()=>MechUpgradeCost.Total(MechUpgradeType.Weapon,0,int.MaxValue));
        }
    }
}
