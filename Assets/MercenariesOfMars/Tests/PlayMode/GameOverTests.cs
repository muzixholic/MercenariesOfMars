using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class GameOverTests
    {
        readonly List<Object> created = new();
        BattleController Create(bool animate = false)
        {
            var go = new GameObject("게임오버 검증");
            go.SetActive(false); created.Add(go);
            var c = go.AddComponent<BattleController>(); c.board = go.AddComponent<GridBoard>();
            c.battleCamera = go.AddComponent<Camera>(); c.eventRunner = go.AddComponent<EventRunner>();
            c.automaticEnemy = false; c.showControls = false; c.enableCombatPresentation = animate;
            for (int i = 0; i < 3; i++)
            {
                var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
                d.faction = i < 2 ? Faction.Ally : Faction.Enemy; d.attack = 0; d.defense = 0;
                var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill); skill.power = 100; skill.energyCost = 2;
                d.skills.Add(skill); c.placements.Add(new UnitPlacement { id = "유닛" + i, character = d, cell = new Vector2Int(i,0) });
            }
            go.SetActive(true); return c;
        }
        void Defeat(BattleController c)
        { c.Session.ChangeHp("유닛0", -100); c.Session.ChangeHp("유닛1", -100); }
        [UnityTearDown] public IEnumerator Cleanup()
        { foreach (var item in created) if (item != null) Object.Destroy(item); created.Clear(); yield return null; }
        [UnityTest] public IEnumerator 첫실행은_타이틀에서_기다리고_전투시작후에만_전투와_이벤트를_만든다()
        {
            var c = Create();
            var opening = ScriptableObject.CreateInstance<EventSequence>(); created.Add(opening);
            opening.commands.Add(new EventCommand { kind = EventCommandKind.Dialogue, speaker = "안내", text = "전투를 시작합니다." });
            c.openingEvent = opening;
            yield return null;
            Assert.That(c.IsTitleScreen, Is.True); Assert.That(c.Session, Is.Null);
            Assert.That(c.IsGameOver, Is.False); Assert.That(c.eventRunner.IsRunning, Is.False);
            Assert.That(c.SelectUnit("유닛0"), Is.False); Assert.That(c.TrySelectedAttack("유닛2"), Is.False);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(c.Session, Is.Null); Assert.That(c.eventRunner.Dialogue, Is.Null);
            Assert.That(c.RestartBattle(), Is.True);
            Assert.That(c.IsTitleScreen, Is.False); Assert.That(c.Session.Round, Is.EqualTo(1));
            Assert.That(c.eventRunner.Dialogue, Is.EqualTo("전투를 시작합니다."));
            Assert.That(c.RestartBattle(), Is.False);
        }
        [UnityTest] public IEnumerator 아군전멸때만_게임오버이고_다시시작은_초기상태를_복원한다()
        {
            var c = Create(); yield return null; c.RestartBattle();
            Assert.That(c.RestartBattle(), Is.False); Assert.That(c.ReturnToTitle(), Is.False);
            c.SelectUnit("유닛0"); c.Session.TryMove(c.SelectedUnit,new Vector2Int(0,1));
            c.Session.ChangeHp("유닛0",-100); Assert.That(c.IsGameOver, Is.False);
            c.Session.ChangeHp("유닛1",-100); Assert.That(c.IsGameOver, Is.True);
            var old = c.Session; Assert.That(c.RestartBattle(), Is.True); yield return null;
            Assert.That(c.Session, Is.Not.SameAs(old)); Assert.That(c.IsGameOver, Is.False);
            Assert.That(c.Session.Round, Is.EqualTo(1)); Assert.That(c.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
            Assert.That(c.Session.PendingAttack, Is.Null); Assert.That(c.SelectedUnit, Is.Null);
            foreach(var unit in c.Session.Units)
            {
                Assert.That(unit.Hp, Is.EqualTo(unit.MaxHp)); Assert.That(unit.Energy, Is.EqualTo(unit.MaxEnergy));
                Assert.That(unit.HasMoved || unit.HasActed, Is.False);
            }
            Assert.That(c.Session.Find("유닛0").Cell, Is.EqualTo(Vector2Int.zero));
            Assert.That(c.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator 타이틀로_돌아가서_새전투를_시작할수_있다()
        {
            var c = Create(); yield return null; c.RestartBattle(); Defeat(c);
            Assert.That(c.ReturnToTitle(), Is.True); Assert.That(c.IsTitleScreen, Is.True); Assert.That(c.IsGameOver, Is.False);
            Assert.That(c.SelectUnit("유닛0"), Is.False); Assert.That(c.TrySelectedAttack("유닛2"), Is.False);
            Assert.That(c.ReturnToTitle(), Is.False); Assert.That(c.RestartBattle(), Is.True); yield return null;
            Assert.That(c.IsTitleScreen, Is.False); Assert.That(c.Session.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            Assert.That(c.SelectUnit("유닛0"), Is.True);
        }
        [UnityTest] public IEnumerator 마지막공격_연출이_끝난뒤에_게임오버를_표시한다()
        {
            var c = Create(true); yield return null; c.RestartBattle();
            c.Session.ChangeHp("유닛0", -100); c.Session.EndTurn();
            Assert.That(c.Session.TryAttack(c.Session.Find("유닛2"),c.Session.Find("유닛1")), Is.True);
            c.ChooseReaction(ReactionChoice.Defend); c.ConfirmCombat();
            Assert.That(c.Session.Find("유닛1").IsAlive, Is.False); Assert.That(c.IsGameOver, Is.False);
            Assert.That(c.RestartBattle(), Is.False); Assert.That(c.ReturnToTitle(), Is.False);
            c.Presenter.Finish(); Assert.That(c.IsGameOver, Is.True);
            Assert.That(c.RestartBattle(), Is.True); yield return null;
            Assert.That(c.battleCamera.enabled, Is.True); Assert.That(c.Session.IsPresenting, Is.False);
            Assert.That(c.Session.LastAttack, Is.Null);
        }
        [UnityTest] public IEnumerator 재시작은_이벤트플래그를_지우고_시작이벤트를_다시실행한다()
        {
            var c = Create(); yield return null; c.RestartBattle();
            var flag = ScriptableObject.CreateInstance<EventSequence>(); created.Add(flag);
            flag.commands.Add(new EventCommand { kind=EventCommandKind.SetFlag,flag="이전 전투",flagValue=true });
            c.eventRunner.Play(flag,c.Session); yield return null; yield return null;
            Assert.That(c.eventRunner.GetFlag("이전 전투"), Is.True);
            var opening = ScriptableObject.CreateInstance<EventSequence>(); created.Add(opening);
            opening.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speaker="안내",text="새 전투" }); c.openingEvent=opening;
            Defeat(c); yield return null; c.RestartBattle();
            Assert.That(c.eventRunner.GetFlag("이전 전투"), Is.False); Assert.That(c.eventRunner.Dialogue, Is.EqualTo("새 전투"));
            Defeat(c); yield return null;
            Assert.That(c.eventRunner.IsRunning, Is.False); Assert.That(c.eventRunner.Dialogue, Is.Null);
            c.ReturnToTitle(); Assert.That(c.eventRunner.LastError, Is.Null);
        }
        [UnityTest] public IEnumerator 적군전멸은_게임오버화면을_표시하지_않는다()
        {
            var c = Create(); yield return null; c.RestartBattle(); c.Session.ChangeHp("유닛2",-100);
            Assert.That(c.Session.Outcome, Is.EqualTo(BattleOutcome.Victory)); Assert.That(c.IsGameOver, Is.False);
            Assert.That(c.IsVictory, Is.True); Assert.That(c.ReturnToTitle(), Is.True);
            Assert.That(c.IsTitleScreen, Is.True); Assert.That(c.IsVictory, Is.False);
        }
        [UnityTest] public IEnumerator 적군전멸은_반격연출이_끝난뒤_승리화면으로_넘어간다()
        {
            var c=Create(true); c.placements[2].character.skills[0].power=0;
            yield return null; c.RestartBattle(); c.Session.EndTurn();
            Assert.That(c.Session.TryAttack(c.Session.Find("유닛2"),c.Session.Find("유닛1")), Is.True);
            c.ChooseReaction(ReactionChoice.Counterattack,0); c.ConfirmCombat();
            Assert.That(c.Session.Find("유닛2").IsAlive, Is.False); Assert.That(c.IsVictory, Is.False);
            Assert.That(c.ReturnToTitle(), Is.False);
            c.Presenter.Finish(); Assert.That(c.IsVictory, Is.True); Assert.That(c.IsGameOver, Is.False);
            Assert.That(c.RestartBattle(), Is.True); Assert.That(c.IsVictory, Is.False);
            Assert.That(c.Session.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
        }
        [UnityTest] public IEnumerator 맵테스트는_승리와_패배후_종료만_표시하고_한번만_종료요청한다()
        {
            var c=Create(); int exits=0; c.ConfigureMapTest(()=>exits++);
            yield return null;
            Assert.That(c.ShowsMapTestExit,Is.False); Assert.That(c.ExitMapTest(),Is.False);
            c.RestartBattle(); Assert.That(c.ExitMapTest(),Is.False);
            c.Session.ChangeHp("유닛2",-100);
            Assert.That(c.ShowsMapTestExit,Is.True); Assert.That(c.ExitMapTest(),Is.True);
            Assert.That(c.ExitMapTest(),Is.False); Assert.That(exits,Is.EqualTo(1));
            c.ConfigureMapTest(()=>exits++); c.RestartBattle(); Defeat(c);
            Assert.That(c.ShowsMapTestExit,Is.True); Assert.That(c.ExitMapTest(),Is.True);
            Assert.That(exits,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator 일반전투는_테스트종료를_제공하지_않는다()
        {
            var c=Create(); yield return null; c.RestartBattle(); Defeat(c);
            Assert.That(c.IsMapTest,Is.False); Assert.That(c.ShowsMapTestExit,Is.False); Assert.That(c.ExitMapTest(),Is.False);
            Assert.That(c.ReturnToTitle(),Is.True);
        }
    }
}
