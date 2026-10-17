using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class CombatConfirmationIntegrationTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction, int power = 10)
        {
            var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d); d.faction = faction; d.attack = 0;
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill); skill.power = power; skill.energyCost = 3;
            d.skills.Add(skill); return d;
        }
        BattleController Controller(bool automaticEnemy = false)
        {
            var go = new GameObject("전투 확인 검증"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>();
            controller.enableCombatPresentation = false; controller.showControls = false; controller.automaticEnemy = automaticEnemy;
            controller.placements.Add(new UnitPlacement { id = "아군", character = Character(Faction.Ally), cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = Character(Faction.Enemy,18), cell = Vector2Int.right });
            go.SetActive(true); return controller;
        }
        EventRunner Runner()
        { var go = new GameObject("전투 확인 이벤트"); created.Add(go); return go.AddComponent<EventRunner>(); }
        EventSequence Sequence(string attacker, string target)
        {
            var sequence = ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Attack, unitId = attacker, targetId = target });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.SetFlag, flag = "진행 완료", flagValue = true }); return sequence;
        }
        IEnumerator Finish(EventRunner runner)
        { for(int i=0;i<60 && runner.IsRunning;i++) yield return null; Assert.That(runner.IsRunning, Is.False); }
        [UnityTearDown] public IEnumerator Cleanup()
        { foreach(var item in created) if(item != null) Object.Destroy(item); created.Clear(); yield return null; }
        [UnityTest] public IEnumerator 긴_기체명과_무장명에도_실제_확인카드폭은_동일하다()
        {
            var c=Controller(); c.showControls=true;
            c.placements[0].character.displayName="아주 긴 이름을 가진 아군 전술 지원 기체 및 추가 장비";
            c.placements[0].character.skills[0].displayName="장거리 전술 지원용 특수 고출력 에너지 무장";
            c.placements[1].character.displayName="적";
            yield return null; c.RestartBattle(); c.SelectUnit("아군"); c.SelectSkill(0);
            Assert.That(c.TrySelectedAttack("적군"),Is.True);
            for(int i=0;i<4;i++) yield return null;
            Assert.That(c.AttackerForecastRect.width,Is.GreaterThan(0));
            Assert.That(c.AttackerForecastRect.width,Is.EqualTo(c.DefenderForecastRect.width).Within(.1f));
            Assert.That(c.AttackerForecastRect.xMax,Is.LessThan(c.DefenderForecastRect.xMin));
            Assert.That(c.DefenderForecastRect.xMax,Is.LessThanOrEqualTo(c.CombatConfirmationRect.width-28));
        }
        [UnityTest] public IEnumerator 아군공격은_적의선택을_확인하고_취소하거나_확정한다()
        {
            var controller = Controller(); yield return null; controller.RestartBattle();
            Assert.That(controller.Session.RequireConfirmation, Is.True);
            controller.SelectUnit("아군"); controller.SelectSkill(0); var skill = controller.SelectedSkill;
            Assert.That(controller.TrySelectedAttack("적군"), Is.True);
            Assert.That(controller.Session.IsAwaitingConfirmation, Is.True);
            Assert.That(controller.Session.PendingAttack.Decision.Choice, Is.EqualTo(ReactionChoice.Counterattack));
            Assert.That(controller.Session.Find("적군").Hp, Is.EqualTo(30));
            Assert.That(controller.CancelCombat(), Is.True); Assert.That(controller.SelectedSkill, Is.SameAs(skill));
            Assert.That(controller.SelectedUnit.Id, Is.EqualTo("아군")); Assert.That(controller.SelectedUnit.HasActed, Is.False);
            Assert.That(controller.TrySelectedAttack("적군"), Is.True); Assert.That(controller.ConfirmCombat(), Is.True);
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(15)); Assert.That(controller.Session.Find("적군").Hp, Is.EqualTo(23));
            Assert.That(controller.Session.Find("아군").Energy, Is.EqualTo(7)); Assert.That(controller.Session.Find("적군").Energy, Is.EqualTo(7));
        }
        [UnityTest] public IEnumerator 적공격은_대응선택후에도_확인을_기다리며_대응을_바꿀수_있다()
        {
            var controller = Controller(true); yield return null; controller.RestartBattle(); controller.Session.EndTurn();
            float deadline = Time.realtimeSinceStartup + 3;
            while(controller.Session.PendingAttack == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.Session.PendingAttack, Is.Not.Null);
            controller.ChooseReaction(ReactionChoice.Defend); Assert.That(controller.Session.IsAwaitingConfirmation, Is.True);
            for(int i=0;i<5;i++) yield return null;
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(30));
            controller.CancelCombat(); Assert.That(controller.Session.PendingAttack, Is.Not.Null); Assert.That(controller.Session.IsAwaitingConfirmation, Is.False);
            controller.ChooseReaction(ReactionChoice.Defend); controller.ConfirmCombat(); yield return null;
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(22));
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally)); Assert.That(controller.Session.Round, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator 아군의_이벤트공격_취소는_이벤트를_명시적으로_중단한다()
        {
            var a = new BattleUnit("아군",Character(Faction.Ally),Vector2Int.zero);
            var e = new BattleUnit("적군",Character(Faction.Enemy),Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { a,e },requireConfirmation:true);
            var runner = Runner(); runner.Play(Sequence("아군","적군"),battle); yield return null;
            Assert.That(runner.GetFlag("진행 완료"), Is.False); Assert.That(battle.IsAwaitingConfirmation, Is.True);
            battle.CancelCombat(); yield return Finish(runner);
            Assert.That(runner.LastError, Does.Contain("취소")); Assert.That(runner.GetFlag("진행 완료"), Is.False);
            Assert.That(a.Energy, Is.EqualTo(10)); Assert.That(e.Hp, Is.EqualTo(30));
        }
        [UnityTest] public IEnumerator 적의_이벤트공격은_대응과_최종확인을_모두_기다린다()
        {
            var a = new BattleUnit("아군",Character(Faction.Ally),Vector2Int.zero);
            var e = new BattleUnit("적군",Character(Faction.Enemy),Vector2Int.right);
            var battle = new BattleSession(new BattleGrid(4,4),new[] { a,e },requireConfirmation:true); battle.EndTurn();
            var runner = Runner(); runner.Play(Sequence("적군","아군"),battle); yield return null;
            battle.ResolveReaction(ReactionChoice.Defend);
            for(int i=0;i<5;i++) yield return null;
            Assert.That(runner.IsRunning, Is.True); Assert.That(runner.GetFlag("진행 완료"), Is.False); Assert.That(a.Hp, Is.EqualTo(30));
            battle.ConfirmCombat(); yield return Finish(runner);
            Assert.That(runner.GetFlag("진행 완료"), Is.True); Assert.That(runner.LastError, Is.Null); Assert.That(a.Hp, Is.EqualTo(26));
        }
    }
}
