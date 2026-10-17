using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class ReactionIntegrationTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
            d.faction = faction; d.attack = 0;
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            skill.energyCost = 3; d.skills.Add(skill); return d;
        }
        BattleController Controller(CharacterDefinition ally)
        {
            var go = new GameObject("반응 검증 전투"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>(); controller.showControls = false; controller.RequireConfirmation = false;
            controller.placements.Add(new UnitPlacement { id = "아군", character = ally, cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = Character(Faction.Enemy), cell = Vector2Int.right });
            go.SetActive(true); return controller;
        }
        IEnumerator AwaitReaction(BattleController controller)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while(controller.Session.PendingAttack == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.Session.PendingAttack, Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var item in created) if(item != null) Object.Destroy(item);
            created.Clear(); yield return null;
        }
        [UnityTest] public IEnumerator AI는_반응선택동안_멈추고_방어선택후_턴을_진행한다()
        {
            var controller = Controller(Character(Faction.Ally)); yield return null; controller.RestartBattle(); controller.Session.EndTurn();
            yield return AwaitReaction(controller);
            var incoming = controller.Session.PendingAttack;
            for(int i=0;i<10;i++) yield return null;
            Assert.That(controller.Session.PendingAttack, Is.SameAs(incoming));
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(30)); Assert.That(controller.Session.Round, Is.EqualTo(1));
            Assert.That(controller.SelectUnit("적군"), Is.False);
            Assert.That(controller.ChooseReaction(ReactionChoice.Defend), Is.True); yield return null;
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(26));
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally)); Assert.That(controller.Session.PendingAttack, Is.Null);
        }
        [UnityTest] public IEnumerator 반격은_선택한_유효스킬만_사용하고_재반격하지_않는다()
        {
            var ally = Character(Faction.Ally); ally.skills[0].minRange = 2; ally.skills[0].maxRange = 3;
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            skill.power = 18; skill.energyCost = 4; skill.canUseAfterMoving = false; ally.skills.Add(skill);
            var controller = Controller(ally); yield return null; controller.RestartBattle(); controller.Session.EndTurn(); yield return AwaitReaction(controller);
            Assert.That(controller.ChooseReaction(ReactionChoice.Counterattack,0), Is.False);
            Assert.That(controller.Session.PendingAttack, Is.Not.Null);
            Assert.That(controller.ChooseReaction(ReactionChoice.Counterattack,1), Is.True); yield return null;
            Assert.That(controller.Session.Find("적군").Hp, Is.EqualTo(15)); Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(23));
            Assert.That(controller.Session.Find("아군").Energy, Is.EqualTo(6));
            Assert.That(controller.Session.LastCounterattack.Skill.Definition, Is.SameAs(skill));
            Assert.That(controller.Session.PendingAttack, Is.Null); Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
        }
        [UnityTest] public IEnumerator 회피선택은_피격률을_절반으로_하고_에너지를_쓰지_않는다()
        {
            var controller = Controller(Character(Faction.Ally)); yield return null; controller.RestartBattle();
            controller.Session.EndTurn(); yield return AwaitReaction(controller);
            Assert.That(controller.ChooseReaction(ReactionChoice.Evade), Is.True); yield return null;
            Assert.That(controller.Session.LastAttack.HitChance, Is.EqualTo(50));
            Assert.That(controller.Session.Find("아군").Energy, Is.EqualTo(10));
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(23).Or.EqualTo(30));
        }
        [UnityTest] public IEnumerator 이벤트는_반응선택이_끝날때까지_후속명령을_기다린다()
        {
            var ally = Character(Faction.Ally); var enemy = Character(Faction.Enemy);
            var battle = new BattleSession(new BattleGrid(4,4),new[] {
                new BattleUnit("아군",ally,Vector2Int.zero),new BattleUnit("적군",enemy,Vector2Int.right) });
            battle.EndTurn();
            var sequence = ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Attack, unitId = "적군", targetId = "아군", skill = enemy.skills[0] });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.SetFlag, flag = "대응 완료", flagValue = true });
            var go = new GameObject("이벤트 반응 검증"); created.Add(go); var runner = go.AddComponent<EventRunner>();
            Assert.That(runner.Play(sequence,battle), Is.True);
            for(int i=0;i<5;i++) yield return null;
            Assert.That(runner.IsRunning, Is.True); Assert.That(runner.GetFlag("대응 완료"), Is.False);
            Assert.That(battle.PendingAttack, Is.Not.Null); Assert.That(battle.Find("아군").Hp, Is.EqualTo(30));
            battle.ResolveReaction(ReactionChoice.Defend);
            for(int i=0;i<30 && runner.IsRunning;i++) yield return null;
            Assert.That(runner.IsRunning, Is.False); Assert.That(runner.GetFlag("대응 완료"), Is.True);
            Assert.That(runner.LastError, Is.Null); Assert.That(battle.Find("아군").Hp, Is.EqualTo(26));
        }
    }
}
