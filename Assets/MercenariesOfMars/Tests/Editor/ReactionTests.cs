using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class ReactionTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction, int hp = 30, int accuracy = 100)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
            d.faction = faction; d.attack = 0; d.maxHp = hp; d.accuracy = accuracy; return d;
        }
        SkillDefinition Skill(int power = 10, int cost = 4, int min = 1, int max = 1)
        {
            var s = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(s);
            s.power = power; s.energyCost = cost; s.minRange = min; s.maxRange = max; return s;
        }
        BattleSession Battle(out BattleUnit ally, out BattleUnit enemy, int power = 10, int allyHp = 30,
            int enemyHp = 30, int accuracy = 100, int allyAccuracy = 100, Func<int> roll = null)
        {
            var a = Character(Faction.Ally,allyHp,allyAccuracy); a.skills.Add(Skill());
            var e = Character(Faction.Enemy,enemyHp,accuracy); e.skills.Add(Skill(power,3,1,3));
            ally = new BattleUnit("아군",a,Vector2Int.zero); enemy = new BattleUnit("적군",e,Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(6,6),new[] { ally,enemy },roll);
            battle.EndTurn(); Assert.That(battle.TryAttack(enemy,ally,enemy.Skills[0]), Is.True); return battle;
        }
        [TearDown] public void Cleanup()
        { foreach(var o in created) if(o != null) Object.DestroyImmediate(o); created.Clear(); }

        [Test] public void 적의공격은_반응선택전까지_피해와_소모없이_대기한다()
        {
            var battle = Battle(out var ally,out var enemy);
            Assert.That(battle.PendingAttack, Is.Not.Null); Assert.That(battle.PendingAttack.Target, Is.SameAs(ally));
            Assert.That(ally.Hp, Is.EqualTo(30)); Assert.That(enemy.Energy, Is.EqualTo(10)); Assert.That(enemy.HasActed, Is.False);
            Assert.That(battle.LastAttack, Is.Null); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
        }
        [Test] public void 반응대기중에는_이동공격대기턴종료와_체력변경을_막는다()
        {
            var battle = Battle(out var ally,out var enemy);
            Assert.That(battle.TryMove(enemy,new Vector2Int(2,0)), Is.False);
            Assert.That(battle.TryAttack(enemy,ally), Is.False); Assert.That(battle.TryWait(enemy), Is.False);
            Assert.That(battle.ChangeHp(ally.Id,-5), Is.False); battle.EndTurn();
            Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy)); Assert.That(ally.Hp, Is.EqualTo(30));
        }
        [TestCase(10,4)] [TestCase(11,4)] [TestCase(12,5)] [TestCase(0,1)]
        public void 방어는_피해의_절반을_올림한다(int power, int expected)
        {
            var battle = Battle(out var ally,out var enemy,power:power);
            Assert.That(battle.GetReactionDamage(ReactionChoice.Defend), Is.EqualTo(expected));
            Assert.That(battle.ResolveReaction(ReactionChoice.Defend), Is.True);
            Assert.That(ally.Hp, Is.EqualTo(30-expected)); Assert.That(ally.Energy, Is.EqualTo(10));
            Assert.That(enemy.Energy, Is.EqualTo(7)); Assert.That(battle.LastCounterattack, Is.Null);
            Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.Round, Is.EqualTo(2));
        }
        [TestCase(100,49,true)] [TestCase(100,50,false)] [TestCase(65,32,true)] [TestCase(65,33,false)]
        public void 회피는_최종명중률을_정확히_절반으로_한다(int accuracy, int roll, bool hit)
        {
            var battle = Battle(out var ally,out _,accuracy:accuracy,roll:() => roll);
            Assert.That(battle.GetReactionHitChance(ReactionChoice.Evade), Is.EqualTo(accuracy*.5f));
            battle.ResolveReaction(ReactionChoice.Evade);
            Assert.That(battle.LastAttack.Hit, Is.EqualTo(hit)); Assert.That(battle.LastAttack.HitChance, Is.EqualTo(accuracy*.5f));
            Assert.That(ally.Hp, Is.EqualTo(hit ? 23 : 30)); Assert.That(ally.Energy, Is.EqualTo(10));
        }
        [Test] public void 반격은_선택스킬로_한번만_공격하고_각자의_에너지를_소모한다()
        {
            var battle = Battle(out var ally,out var enemy);
            Assert.That(ally.HasActed, Is.True); Assert.That(battle.CanCounterattack(ally.Skills[0]), Is.True);
            battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(ally.Hp, Is.EqualTo(23)); Assert.That(enemy.Hp, Is.EqualTo(23));
            Assert.That(ally.Energy, Is.EqualTo(6)); Assert.That(enemy.Energy, Is.EqualTo(7));
            Assert.That(battle.LastAttack.IsCounterattack, Is.False); Assert.That(battle.LastCounterattack.IsCounterattack, Is.True);
            Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Ally));
            Assert.That(ally.HasActed, Is.False);
        }
        [Test] public void 선택한_다른_반격스킬의_공격력과_소모량을_적용한다()
        {
            var a = Character(Faction.Ally); a.skills.Add(Skill()); a.skills.Add(Skill(18,6));
            var e = Character(Faction.Enemy); e.skills.Add(Skill(10,3));
            var ally = new BattleUnit("아군",a,Vector2Int.zero); var enemy = new BattleUnit("적군",e,Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy });
            battle.EndTurn(); battle.TryAttack(enemy,ally); battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[1]);
            Assert.That(enemy.Hp, Is.EqualTo(15)); Assert.That(ally.Energy, Is.EqualTo(4));
        }
        [Test] public void 사거리부족과_에너지부족과_미보유_반격은_거절한다()
        {
            var a = Character(Faction.Ally); a.skills.Add(Skill(min:2,max:3)); a.skills.Add(Skill(cost:11));
            var e = Character(Faction.Enemy); e.skills.Add(Skill());
            var ally = new BattleUnit("아군",a,Vector2Int.zero); var enemy = new BattleUnit("적군",e,Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy }); battle.EndTurn(); battle.TryAttack(enemy,ally);
            foreach(var skill in new[] { ally.Skills[0],ally.Skills[1],new BattleSkill(Skill()) })
            { Assert.That(battle.CanCounterattack(skill), Is.False); Assert.That(battle.ResolveReaction(ReactionChoice.Counterattack,skill), Is.False); }
            Assert.That(battle.ResolveReaction(ReactionChoice.Counterattack), Is.False);
            Assert.That(battle.PendingAttack, Is.Not.Null); Assert.That(ally.Energy, Is.EqualTo(10));
            Assert.That(battle.ResolveReaction(ReactionChoice.Defend), Is.True);
        }
        [Test] public void 피격으로_전투불능이면_반격하거나_에너지를_소모하지_않는다()
        {
            var battle = Battle(out var ally,out var enemy,allyHp:7);
            battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(ally.Hp, Is.Zero); Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(enemy.Hp, Is.EqualTo(30));
            Assert.That(battle.LastCounterattack, Is.Null); Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }
        [Test] public void 반격으로_적군전멸시_승리를_판정한다()
        {
            var battle = Battle(out var ally,out var enemy,enemyHp:7); battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(enemy.Hp, Is.Zero); Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory)); Assert.That(battle.PendingAttack, Is.Null);
        }
        [Test] public void 방어는_남은체력이_아닌_원래피해를_절반으로_한다()
        {
            var battle = Battle(out var ally,out _,power:100,allyHp:10);
            Assert.That(battle.GetReactionDamage(ReactionChoice.Defend), Is.EqualTo(49));
            battle.ResolveReaction(ReactionChoice.Defend); Assert.That(ally.Hp, Is.Zero);
        }
        [Test] public void 적공격이_빗나가도_반격할수_있다()
        {
            var battle = Battle(out var ally,out var enemy,accuracy:0); battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(battle.LastAttack.Hit, Is.False); Assert.That(ally.Hp, Is.EqualTo(30)); Assert.That(enemy.Hp, Is.EqualTo(23));
            Assert.That(ally.Energy, Is.EqualTo(6)); Assert.That(enemy.Energy, Is.EqualTo(7));
        }
        [Test] public void 반격이_빗나가도_에너지는_소모한다()
        {
            var battle = Battle(out var ally,out var enemy,allyAccuracy:0); battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(battle.LastCounterattack.Hit, Is.False); Assert.That(enemy.Hp, Is.EqualTo(30)); Assert.That(ally.Energy, Is.EqualTo(6));
        }
        [Test] public void 이전_자기턴에_이동하고_행동했어도_정지전용스킬로_반격한다()
        {
            var a = Character(Faction.Ally); var skill = Skill(max:3); skill.canUseAfterMoving = false; a.skills.Add(skill);
            var e = Character(Faction.Enemy); e.skills.Add(Skill(max:3));
            var ally = new BattleUnit("아군",a,Vector2Int.zero); var enemy = new BattleUnit("적군",e,Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,enemy });
            battle.TryMove(ally,Vector2Int.up); battle.TryWait(ally); battle.TryAttack(enemy,ally);
            Assert.That(ally.HasMoved && ally.HasActed, Is.True); Assert.That(battle.CanCounterattack(ally.Skills[0]), Is.True);
            Assert.That(battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]), Is.True);
        }
        [Test] public void 잘못된선택과_중복선택은_부작용없이_거절한다()
        {
            var battle = Battle(out var ally,out var enemy); Assert.That(battle.ResolveReaction((ReactionChoice)99), Is.False);
            Assert.That(battle.PendingAttack, Is.Not.Null); battle.ResolveReaction(ReactionChoice.Defend);
            int hp = ally.Hp; Assert.That(battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]), Is.False);
            Assert.That(ally.Hp, Is.EqualTo(hp)); Assert.That(enemy.Energy, Is.EqualTo(7));
        }
        [Test] public void 두번째판정이_실패해도_공격을_부분적용하지_않는다()
        {
            int calls = 0; var battle = Battle(out var ally,out var enemy,accuracy:50,allyAccuracy:50,roll:() => ++calls == 1 ? 0 : 100);
            Assert.Throws<InvalidOperationException>(() => battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]));
            Assert.That(ally.Hp, Is.EqualTo(30)); Assert.That(enemy.Hp, Is.EqualTo(30));
            Assert.That(ally.Energy, Is.EqualTo(10)); Assert.That(enemy.Energy, Is.EqualTo(10)); Assert.That(battle.PendingAttack, Is.Not.Null);
        }
        [Test] public void 연속으로_공격받을때_에너지부족한_반격을_막는다()
        {
            var a = Character(Faction.Ally); a.maxEnergy = 4; a.skills.Add(Skill());
            var e = Character(Faction.Enemy); e.skills.Add(Skill(10,3));
            var ally = new BattleUnit("아군",a,Vector2Int.zero);
            var first = new BattleUnit("적군1",e,Vector2Int.right); var second = new BattleUnit("적군2",e,Vector2Int.up);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { ally,first,second }); battle.EndTurn();
            battle.TryAttack(first,ally); battle.ResolveReaction(ReactionChoice.Counterattack,ally.Skills[0]);
            Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy)); Assert.That(ally.Energy, Is.Zero);
            battle.TryAttack(second,ally); Assert.That(battle.CanCounterattack(ally.Skills[0]), Is.False);
            Assert.That(battle.ResolveReaction(ReactionChoice.Defend), Is.True); Assert.That(ally.Energy, Is.Zero);
        }
    }
}
