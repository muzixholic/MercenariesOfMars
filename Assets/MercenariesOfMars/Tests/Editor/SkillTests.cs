using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class SkillTests
    {
        readonly List<Object> created = new();
        SkillDefinition Skill(int min = 1, int max = 3, int cost = 4, int power = 12)
        {
            var s = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(s);
            s.minRange = min; s.maxRange = max; s.energyCost = cost; s.power = power; return s;
        }
        CharacterDefinition Character(Faction faction, params SkillDefinition[] skills)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
            d.attack = 0; d.faction = faction; d.maxEnergy = 10; d.skills.AddRange(skills); return d;
        }
        BattleSession Battle(SkillDefinition skill, int distance, out BattleUnit ally, out BattleUnit enemy)
        {
            ally = new BattleUnit("아군", Character(Faction.Ally, skill), Vector2Int.zero);
            enemy = new BattleUnit("적군", Character(Faction.Enemy), new Vector2Int(distance,0));
            return new BattleSession(new BattleGrid(8,8), new[] { ally, enemy });
        }
        [TearDown] public void Cleanup()
        { foreach (var o in created) if (o != null) Object.DestroyImmediate(o); created.Clear(); }
        [TestCase(1,1,1,true)] [TestCase(1,1,2,false)]
        [TestCase(1,3,1,true)] [TestCase(1,3,3,true)] [TestCase(1,3,4,false)]
        [TestCase(2,3,1,false)] [TestCase(2,3,2,true)] [TestCase(2,3,3,true)] [TestCase(2,3,4,false)]
        public void 최소최대_사거리_경계를_포함한다(int min, int max, int distance, bool valid)
        {
            var battle = Battle(Skill(min,max), distance, out var ally, out var enemy);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.EqualTo(valid));
            Assert.That(ally.Energy, Is.EqualTo(valid ? 6 : 10));
            Assert.That(ally.HasActed, Is.EqualTo(valid));
        }
        [Test] public void 에너지부족은_피해와_행동을_소모하지_않는다()
        {
            var battle = Battle(Skill(cost:11),1,out var ally,out var enemy);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.False);
            Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(ally.HasActed, Is.False);
        }
        [Test] public void 남은에너지와_소비량이_같으면_사용한다()
        {
            var battle = Battle(Skill(cost:10),1,out var ally,out var enemy);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True); Assert.That(ally.Energy, Is.Zero);
        }
        [Test] public void 소모한에너지는_다음턴에도_유지한다()
        {
            var battle = Battle(Skill(cost:6),1,out var ally,out var enemy);
            battle.TryAttack(ally,enemy,ally.Skills[0]); battle.TryWait(enemy);
            Assert.That(ally.Energy, Is.EqualTo(4));
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.False);
        }
        [Test] public void 다른캐릭터의_스킬과_미보유스킬을_거절한다()
        {
            var skill = Skill(); var battle = Battle(skill,1,out var ally,out var enemy);
            var other = new BattleUnit("외부",Character(Faction.Ally,skill),Vector2Int.up);
            Assert.That(battle.TryAttack(ally,enemy,other.Skills[0]), Is.False);
            Assert.That(battle.TryAttack(ally,enemy,new BattleSkill(Skill())), Is.False);
            Assert.That(ally.Energy, Is.EqualTo(10));
        }
        [Test] public void 캐릭터별_스킬목록과_에너지를_분리한다()
        {
            var shared = Skill(); var exclusive = Skill(power:20);
            var a = new BattleUnit("기사",Character(Faction.Ally,shared),Vector2Int.zero);
            var b = new BattleUnit("궁수",Character(Faction.Ally,shared,exclusive),Vector2Int.one);
            Assert.That(a.Skills.Count, Is.EqualTo(1)); Assert.That(b.Skills.Count, Is.EqualTo(2));
            shared.power = 999; Assert.That(a.Skills[0].Power, Is.EqualTo(12));
            Assert.That(b.Skills[0].Power, Is.EqualTo(12));
        }
        [Test] public void 스킬이_없으면_공격할수_없다()
        {
            var battle = Battle(null,1,out var ally,out var enemy);
            Assert.That(ally.Skills, Is.Empty); Assert.That(battle.TryAttack(ally,enemy), Is.False);
        }
        [Test] public void 중복과_빈스킬을_전투목록에서_제외한다()
        {
            var skill = Skill(); var unit = new BattleUnit("아군",Character(Faction.Ally,skill,null,skill),Vector2Int.zero);
            Assert.That(unit.Skills.Count, Is.EqualTo(1));
        }
        [Test] public void 방어력이_높아도_최소피해는_1이다()
        {
            var battle = Battle(Skill(power:0,cost:0),1,out var ally,out var enemy);
            battle.TryAttack(ally,enemy,ally.Skills[0]); Assert.That(enemy.Hp, Is.EqualTo(29)); Assert.That(ally.Energy, Is.EqualTo(10));
        }
        [Test] public void 잘못된_스킬수치를_검출한다()
        {
            var s = Skill(3,1,-1,-1); Assert.That(s.ValidateSkill().Count, Is.EqualTo(3));
        }
    }
}
