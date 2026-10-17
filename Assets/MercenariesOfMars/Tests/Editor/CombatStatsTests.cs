using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class CombatStatsTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction, int attack = 8, int accuracy = 80, int evasion = 0)
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(character);
            character.faction = faction; character.attack = attack; character.accuracy = accuracy; character.evasion = evasion;
            return character;
        }
        SkillDefinition Skill(int modifier = 0)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            skill.power = 12; skill.accuracyModifier = modifier; skill.energyCost = 4; return skill;
        }
        BattleSession Battle(int accuracy, int modifier, int evasion, System.Func<int> roll,
            out BattleUnit ally, out BattleUnit enemy)
        {
            var character = Character(Faction.Ally, accuracy: accuracy); character.skills.Add(Skill(modifier));
            ally = new BattleUnit("아군", character, Vector2Int.zero);
            enemy = new BattleUnit("적군", Character(Faction.Enemy, evasion: evasion), Vector2Int.right);
            return new BattleSession(new BattleGrid(6,6), new[] { ally, enemy }, roll);
        }
        [TearDown] public void Cleanup()
        { foreach (var value in created) if (value != null) Object.DestroyImmediate(value); created.Clear(); }

        [TestCase(80,10,20,70)] [TestCase(30,-40,0,0)] [TestCase(100,100,0,100)]
        [TestCase(0,-100,100,0)] [TestCase(100,-20,100,0)] [TestCase(65,0,15,50)]
        public void 명중과_스킬보정에서_회피를_빼고_범위를_제한한다(int accuracy, int modifier, int evasion, int expected)
        {
            var battle = Battle(accuracy,modifier,evasion,() => 0,out var ally,out var enemy);
            Assert.That(battle.GetHitChance(ally,enemy,ally.Skills[0]), Is.EqualTo(expected));
            Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(ally.Energy, Is.EqualTo(10));
        }
        [TestCase(0,true)] [TestCase(49,true)] [TestCase(50,false)] [TestCase(99,false)]
        public void 명중판정은_확률_미만일때만_명중한다(int roll, bool hit)
        {
            var battle = Battle(70,10,30,() => roll,out var ally,out var enemy);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True);
            Assert.That(battle.LastAttack.HitChance, Is.EqualTo(50)); Assert.That(battle.LastAttack.Hit, Is.EqualTo(hit));
            Assert.That(battle.LastAttack.Damage, Is.EqualTo(hit ? 17 : 0));
            Assert.That(enemy.Hp, Is.EqualTo(hit ? 13 : 30));
            Assert.That(ally.Energy, Is.EqualTo(6)); Assert.That(ally.HasActed, Is.True);
            Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
        }
        [TestCase(0,false)] [TestCase(100,true)]
        public void 영퍼센트와_백퍼센트는_난수없이_확정한다(int chance, bool hit)
        {
            var battle = Battle(chance,0,0,() => throw new System.Exception("확정 판정은 난수가 필요 없습니다."),out var ally,out var enemy);
            battle.TryAttack(ally,enemy,ally.Skills[0]); Assert.That(battle.LastAttack.Hit, Is.EqualTo(hit));
        }
        [Test] public void 캐릭터공격과_스킬공격을_더한뒤_방어력을_뺀다()
        {
            var battle = Battle(100,0,0,() => 0,out var ally,out var enemy);
            Assert.That(battle.GetDamage(ally,enemy,ally.Skills[0]), Is.EqualTo(17));
            battle.TryAttack(ally,enemy,ally.Skills[0]); Assert.That(enemy.Hp, Is.EqualTo(13));
        }
        [Test] public void 사거리밖이면_명중판정과_행동소모를_하지_않는다()
        {
            int rolls = 0; var battle = Battle(50,0,0,() => { rolls++; return 0; },out var ally,out var enemy);
            battle.TryMove(ally,new Vector2Int(0,2));
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.False);
            Assert.That(rolls, Is.Zero); Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(ally.HasActed, Is.False);
            Assert.That(battle.LastAttack, Is.Null);
        }
        [Test] public void 공격명중회피와_스킬보정은_전투시작_값을_유지한다()
        {
            var definition = Character(Faction.Ally,8,75,10); var skill = Skill(12); definition.skills.Add(skill);
            var ally = new BattleUnit("아군",definition,Vector2Int.zero);
            var enemy = new BattleUnit("적군",Character(Faction.Enemy,evasion:15),Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy });
            definition.attack = 999; definition.accuracy = 0; definition.evasion = 100; skill.accuracyModifier = -100;
            Assert.That(ally.Attack, Is.EqualTo(8)); Assert.That(ally.Accuracy, Is.EqualTo(75)); Assert.That(ally.Evasion, Is.EqualTo(10));
            Assert.That(ally.Skills[0].AccuracyModifier, Is.EqualTo(12));
            Assert.That(battle.GetHitChance(ally,enemy,ally.Skills[0]), Is.EqualTo(72));
        }
        [Test] public void 큰공격력도_오버플로없이_피해를_계산한다()
        {
            var d = Character(Faction.Ally,int.MaxValue,100); var s = Skill(); s.power = int.MaxValue; d.skills.Add(s);
            var ally = new BattleUnit("아군",d,Vector2Int.zero);
            var enemy = new BattleUnit("적군",Character(Faction.Enemy),Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy });
            Assert.That(battle.GetDamage(ally,enemy,ally.Skills[0]), Is.EqualTo(int.MaxValue));
            battle.TryAttack(ally,enemy,ally.Skills[0]);
            Assert.That(enemy.Hp, Is.Zero); Assert.That(battle.LastAttack.Damage, Is.EqualTo(30));
        }
        [Test] public void 잘못된_난수는_체력과_에너지를_변경하지_않는다()
        {
            var battle = Battle(50,0,0,() => 100,out var ally,out var enemy);
            Assert.Throws<System.InvalidOperationException>(() => battle.TryAttack(ally,enemy,ally.Skills[0]));
            Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(ally.HasActed, Is.False);
        }
        [Test] public void 스킬명중보정의_잘못된범위를_검출한다()
        {
            var skill = Skill(101); Assert.That(skill.ValidateSkill(), Has.Count.EqualTo(1));
        }
        [Test] public void 잘못된_원본값은_전투복사시_유효범위로_제한한다()
        {
            var d = Character(Faction.Ally,-10,150,-5); var s = Skill(-150); d.skills.Add(s);
            var unit = new BattleUnit("아군",d,Vector2Int.zero);
            Assert.That(unit.Attack, Is.Zero); Assert.That(unit.Accuracy, Is.EqualTo(100)); Assert.That(unit.Evasion, Is.Zero);
            Assert.That(unit.Skills[0].AccuracyModifier, Is.EqualTo(-100));
        }
    }
}
