using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class CombatConfirmationTests
    {
        readonly List<Object> created = new();
        SkillDefinition Skill(int power = 10, int cost = 3, int min = 1, int max = 1)
        {
            var s = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(s);
            s.power = power; s.energyCost = cost; s.minRange = min; s.maxRange = max; return s;
        }
        CharacterDefinition Character(Faction faction, int hp = 30, int accuracy = 100)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
            d.attack = 0; d.faction = faction; d.maxHp = hp; d.accuracy = accuracy; return d;
        }
        BattleSession Battle(out BattleUnit ally, out BattleUnit enemy, int enemyHp = 30, int allyPower = 10,
            SkillDefinition counter = null, Func<int> roll = null, int accuracy = 100)
        {
            var a = Character(Faction.Ally,accuracy:accuracy); a.skills.Add(Skill(allyPower));
            var e = Character(Faction.Enemy,enemyHp); if(counter != null) e.skills.Add(counter);
            ally = new BattleUnit("아군",a,Vector2Int.zero); enemy = new BattleUnit("적군",e,Vector2Int.right);
            return new BattleSession(new BattleGrid(5,5),new[] { ally,enemy },roll,requireConfirmation:true);
        }
        [TearDown] public void Cleanup()
        { foreach(var item in created) if(item != null) Object.DestroyImmediate(item); created.Clear(); }
        [Test] public void 적은_생존을_보장할수_있으면_방어를_선택한다()
        {
            var battle = Battle(out var ally,out var enemy,enemyHp:5,counter:Skill(30));
            battle.TryAttack(ally,enemy,ally.Skills[0]);
            Assert.That(battle.PendingAttack.Decision.Choice, Is.EqualTo(ReactionChoice.Defend));
            Assert.That(battle.PendingAttack.Decision.Reason, Is.Not.Empty);
            Assert.That(battle.ConfirmCombat(), Is.True); Assert.That(enemy.Hp, Is.EqualTo(1));
        }
        [Test] public void 방어해도_치명적이면_회피로_사망위험을_줄인다()
        {
            var battle = Battle(out var ally,out var enemy,enemyHp:3,counter:Skill(30)); battle.TryAttack(ally,enemy);
            Assert.That(battle.PendingAttack.Decision.Choice, Is.EqualTo(ReactionChoice.Evade));
        }
        [Test] public void 적은_생존가능하고_반격기대효과가_좋으면_스킬로_반격한다()
        {
            var skill = Skill(18); var battle = Battle(out var ally,out var enemy,counter:skill); battle.TryAttack(ally,enemy);
            Assert.That(battle.PendingAttack.Decision.Choice, Is.EqualTo(ReactionChoice.Counterattack));
            Assert.That(battle.PendingAttack.Decision.CounterSkill.Definition, Is.SameAs(skill));
            battle.ConfirmCombat(); Assert.That(enemy.Hp, Is.EqualTo(23)); Assert.That(ally.Hp, Is.EqualTo(15));
            Assert.That(enemy.Energy, Is.EqualTo(7)); Assert.That(battle.LastCounterattack, Is.Not.Null);
        }
        [TestCase(2,3,3)] [TestCase(1,1,11)]
        public void 적은_사거리나_에너지가_부족한_반격을_선택하지_않는다(int min, int max, int cost)
        {
            var battle = Battle(out var ally,out var enemy,counter:Skill(30,cost,min,max)); battle.TryAttack(ally,enemy);
            Assert.That(battle.PendingAttack.Decision.Choice, Is.Not.EqualTo(ReactionChoice.Counterattack));
        }
        [Test] public void 공격이_약할때도_방어와_회피의_기대피해를_비교한다()
        {
            var battle = Battle(out var ally,out var enemy,allyPower:11); battle.TryAttack(ally,enemy);
            // 원래 피해 8: 방어 4와 회피의 기대 피해 4가 같으면 확정 감소를 우선한다.
            Assert.That(battle.PendingAttack.Decision.Choice, Is.EqualTo(ReactionChoice.Defend));
        }
        [Test] public void 확인전에는_피해에너지행동과_난수를_소모하지_않는다()
        {
            int rolls = 0; var battle = Battle(out var ally,out var enemy,roll:() => { rolls++; return 0; },accuracy:60);
            battle.TryAttack(ally,enemy);
            Assert.That(battle.IsAwaitingConfirmation, Is.True); Assert.That(rolls, Is.Zero);
            Assert.That(ally.Hp, Is.EqualTo(30)); Assert.That(enemy.Hp, Is.EqualTo(30));
            Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(ally.HasActed, Is.False);
            battle.ConfirmCombat(); Assert.That(rolls, Is.EqualTo(1)); Assert.That(ally.Energy, Is.EqualTo(7));
        }
        [Test] public void 확인창이_열린동안_다른_전투행동과_턴진행을_막는다()
        {
            var battle = Battle(out var ally,out var enemy); battle.TryAttack(ally,enemy);
            Assert.That(battle.TryMove(ally,Vector2Int.up), Is.False); Assert.That(battle.TryWait(ally), Is.False);
            Assert.That(battle.TryAttack(ally,enemy), Is.False); Assert.That(battle.ChangeHp(enemy.Id,-1), Is.False);
            battle.EndTurn(); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Ally));
        }
        [Test] public void 아군공격_취소는_자원과_선택전_이동기록을_그대로_유지한다()
        {
            var a = Character(Faction.Ally); a.skills.Add(Skill(max:3));
            var ally = new BattleUnit("이동 아군",a,Vector2Int.zero);
            var enemy = new BattleUnit("대상",Character(Faction.Enemy),new Vector2Int(2,0));
            var battle = new BattleSession(new BattleGrid(5,5),new[] { ally,enemy },requireConfirmation:true);
            battle.TryMove(ally,Vector2Int.right); battle.TryAttack(ally,enemy); var request = battle.PendingAttack;
            Assert.That(battle.CancelCombat(), Is.True); Assert.That(request.Cancelled, Is.True);
            Assert.That(battle.PendingAttack, Is.Null); Assert.That(ally.Cell, Is.EqualTo(Vector2Int.right));
            Assert.That(ally.HasMoved, Is.True); Assert.That(ally.HasActed, Is.False); Assert.That(ally.Energy, Is.EqualTo(10));
            Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(battle.TryAttack(ally,enemy), Is.True);
        }
        [Test] public void 적공격은_내대응선택후_확인하고_취소시_대응으로_돌아간다()
        {
            var a = Character(Faction.Ally); a.skills.Add(Skill()); var e = Character(Faction.Enemy); e.skills.Add(Skill());
            var ally = new BattleUnit("아군",a,Vector2Int.zero); var enemy = new BattleUnit("적군",e,Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy },requireConfirmation:true);
            battle.EndTurn(); battle.TryAttack(enemy,ally); var request = battle.PendingAttack;
            Assert.That(battle.IsAwaitingConfirmation, Is.False); Assert.That(battle.ConfirmCombat(), Is.False);
            battle.ResolveReaction(ReactionChoice.Defend); Assert.That(battle.IsAwaitingConfirmation, Is.True);
            Assert.That(ally.Hp, Is.EqualTo(30)); Assert.That(enemy.Energy, Is.EqualTo(10));
            battle.CancelCombat(); Assert.That(battle.PendingAttack, Is.SameAs(request)); Assert.That(request.Cancelled, Is.False);
            Assert.That(battle.IsAwaitingConfirmation, Is.False); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
            battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]); Assert.That(battle.ConfirmCombat(), Is.True);
            Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.LastCounterattack, Is.Not.Null);
        }
        [Test] public void 플레이어는_적의_대응결정을_덮어쓰지_못한다()
        {
            var battle = Battle(out var ally,out var enemy); battle.TryAttack(ally,enemy); var decision = battle.PendingAttack.Decision;
            Assert.That(battle.ResolveReaction(ReactionChoice.Defend), Is.False); Assert.That(battle.PendingAttack.Decision, Is.SameAs(decision));
        }
        [Test] public void 취소후에는_확정하지_못하고_확정도_한번만_된다()
        {
            var battle = Battle(out var ally,out var enemy); battle.TryAttack(ally,enemy); battle.CancelCombat();
            Assert.That(battle.ConfirmCombat(), Is.False); Assert.That(ally.Energy, Is.EqualTo(10));
            battle.TryAttack(ally,enemy); battle.ConfirmCombat(); var hp = enemy.Hp; var energy = ally.Energy;
            Assert.That(battle.ConfirmCombat(), Is.False); Assert.That(enemy.Hp, Is.EqualTo(hp)); Assert.That(ally.Energy, Is.EqualTo(energy));
        }
        [Test] public void 취소하고_다시_공격해도_같은상태의_적_판단은_동일하다()
        {
            var battle = Battle(out var ally,out var enemy,counter:Skill(15)); battle.TryAttack(ally,enemy);
            var choice = battle.PendingAttack.Decision.Choice; var skill = battle.PendingAttack.Decision.CounterSkill;
            battle.CancelCombat(); battle.TryAttack(ally,enemy);
            Assert.That(battle.PendingAttack.Decision.Choice, Is.EqualTo(choice)); Assert.That(battle.PendingAttack.Decision.CounterSkill, Is.SameAs(skill));
        }
    }
}
