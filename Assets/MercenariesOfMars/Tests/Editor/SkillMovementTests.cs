using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class SkillMovementTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
            d.faction = faction; d.attack = 0; return d;
        }
        SkillDefinition Skill(bool allowed)
        {
            var s = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(s);
            s.canUseAfterMoving = allowed; s.maxRange = 3; s.energyCost = 3; return s;
        }
        BattleSession Battle(bool allowed, out BattleUnit ally, out BattleUnit enemy,
            System.Func<int> roll = null, int accuracy = 100)
        {
            var d = Character(Faction.Ally); d.skills.Add(Skill(allowed)); d.accuracy = accuracy;
            ally = new BattleUnit("아군",d,Vector2Int.zero);
            enemy = new BattleUnit("적군",Character(Faction.Enemy),new Vector2Int(2,0));
            return new BattleSession(new BattleGrid(6,6),new[] { ally,enemy },roll);
        }
        [TearDown] public void Cleanup()
        { foreach(var item in created) if(item != null) Object.DestroyImmediate(item); created.Clear(); }
        [Test] public void 이동하지_않으면_이동후불가_스킬을_사용한다()
        {
            var battle = Battle(false,out var ally,out var enemy);
            Assert.That(battle.CanSelectSkill(ally,ally.Skills[0]), Is.True);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True);
            Assert.That(ally.Energy, Is.EqualTo(7)); Assert.That(enemy.Hp, Is.EqualTo(23));
        }
        [Test] public void 이동후불가_스킬은_피해와_행동과_에너지를_소모하지_않는다()
        {
            var battle = Battle(false,out var ally,out var enemy,() => throw new System.Exception("명중 판정하면 안 됩니다."),50);
            Assert.That(battle.TryMove(ally,Vector2Int.right), Is.True);
            Assert.That(battle.CanSelectSkill(ally,ally.Skills[0]), Is.False);
            Assert.That(battle.CanUseSkill(ally,enemy,ally.Skills[0]), Is.False);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.False);
            Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(ally.HasActed, Is.False);
            Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(battle.LastAttack, Is.Null);
        }
        [Test] public void 이동후가능_스킬은_이동하고_사용한다()
        {
            var battle = Battle(true,out var ally,out var enemy); battle.TryMove(ally,Vector2Int.right);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True);
            Assert.That(ally.Energy, Is.EqualTo(7));
        }
        [Test] public void 사용불가_시도후_다른스킬로_공격할수_있다()
        {
            var d = Character(Faction.Ally); d.skills.Add(Skill(false)); d.skills.Add(Skill(true));
            var ally = new BattleUnit("아군",d,Vector2Int.zero);
            var enemy = new BattleUnit("적군",Character(Faction.Enemy),new Vector2Int(2,0));
            var battle = new BattleSession(new BattleGrid(6,6),new[] { ally,enemy });
            battle.TryMove(ally,Vector2Int.right);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.False);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[1]), Is.True);
        }
        [Test] public void 다음_자기진영턴에는_이동제한이_초기화된다()
        {
            var battle = Battle(false,out var ally,out var enemy);
            battle.TryMove(ally,Vector2Int.right); battle.TryWait(ally); battle.TryWait(enemy);
            Assert.That(ally.HasMoved, Is.False);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True);
        }
        [Test] public void 실패한이동은_이동으로_취급하지_않는다()
        {
            var battle = Battle(false,out var ally,out var enemy);
            Assert.That(battle.TryMove(ally,ally.Cell), Is.False);
            Assert.That(battle.TryMove(ally,enemy.Cell), Is.False);
            Assert.That(battle.TryMove(ally,new Vector2Int(-1,0)), Is.False);
            Assert.That(ally.HasMoved, Is.False);
            Assert.That(battle.TryAttack(ally,enemy,ally.Skills[0]), Is.True);
        }
        [Test] public void 전투시작후_원본이_바뀌어도_이동제한은_유지된다()
        {
            var definition = Character(Faction.Ally); var skill = Skill(false); definition.skills.Add(skill);
            var ally = new BattleUnit("아군",definition,Vector2Int.zero); skill.canUseAfterMoving = true;
            Assert.That(ally.Skills[0].CanUseAfterMoving, Is.False);
        }
        [Test] public void 새스킬은_기본적으로_이동후_사용가능하다()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            Assert.That(skill.canUseAfterMoving, Is.True); Assert.That(new BattleSkill(skill).CanUseAfterMoving, Is.True);
        }
    }
}
