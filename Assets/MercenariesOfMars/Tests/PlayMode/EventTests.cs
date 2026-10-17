using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class EventTests
    {
        readonly List<Object> created = new();
        CharacterDefinition Character(Faction faction)
        {
            var value = ScriptableObject.CreateInstance<CharacterDefinition>();
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            value.attack = 0; value.skills.Add(skill); value.faction = faction; created.Add(value); return value;
        }
        BattleSession Battle()
        {
            return new BattleSession(new BattleGrid(6, 6), new[] {
                new BattleUnit("아군", Character(Faction.Ally), Vector2Int.zero),
                new BattleUnit("적군", Character(Faction.Enemy), new Vector2Int(3, 0)) });
        }
        EventSequence Sequence(params EventCommand[] commands)
        {
            var value = ScriptableObject.CreateInstance<EventSequence>(); created.Add(value);
            value.commands.AddRange(commands); return value;
        }
        EventRunner Runner()
        {
            var go = new GameObject("이벤트 테스트"); created.Add(go); return go.AddComponent<EventRunner>();
        }
        IEnumerator Finish(EventRunner runner)
        {
            for (int i = 0; i < 60 && runner.IsRunning; i++) yield return null;
            Assert.That(runner.IsRunning, Is.False, "이벤트가 정해진 프레임 안에 끝나야 합니다.");
        }
        IEnumerator DefendUntilAllyTurn(BattleController controller)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (controller.Session.ActiveFaction == Faction.Enemy && Time.realtimeSinceStartup < deadline)
            {
                if (controller.Session.PendingAttack != null) Assert.That(controller.ChooseReaction(ReactionChoice.Defend), Is.True);
                yield return null;
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var value in created) if (value != null) Object.Destroy(value);
            created.Clear(); yield return null;
        }
        [UnityTest] public IEnumerator 플래그분기로_피해명령을_건너뛴다()
        {
            var battle = Battle(); var runner = Runner();
            Assert.That(runner.Play(Sequence(
                new EventCommand { kind = EventCommandKind.SetFlag, flag = "구출", flagValue = true },
                new EventCommand { kind = EventCommandKind.BranchFlag, flag = "구출", flagValue = true, jumpTo = 3 },
                new EventCommand { kind = EventCommandKind.ChangeHp, unitId = "아군", amount = -10 },
                new EventCommand { kind = EventCommandKind.Stop }), battle), Is.True);
            yield return Finish(runner);
            Assert.That(battle.Find("아군").Hp, Is.EqualTo(30)); Assert.That(runner.LastError, Is.Null);
        }
        [UnityTest] public IEnumerator 대사확인_전에는_다음명령을_실행하지_않는다()
        {
            var battle = Battle(); var runner = Runner();
            runner.Play(Sequence(new EventCommand { kind = EventCommandKind.Dialogue, speaker = "기사", text = "출발하자." },
                new EventCommand { kind = EventCommandKind.ChangeHp, unitId = "아군", amount = -5 }), battle);
            yield return null;
            Assert.That(runner.IsRunning, Is.True); Assert.That(runner.Dialogue, Is.EqualTo("출발하자."));
            Assert.That(battle.Find("아군").Hp, Is.EqualTo(30));
            runner.ContinueDialogue(); yield return Finish(runner);
            Assert.That(battle.Find("아군").Hp, Is.EqualTo(25));
        }
        [UnityTest] public IEnumerator 순환분기를_실행한도로_중단한다()
        {
            var runner = Runner(); runner.instructionLimit = 3;
            runner.Play(Sequence(new EventCommand { kind = EventCommandKind.BranchFlag, flag = "반복", flagValue = false, jumpTo = 0 }), Battle());
            yield return Finish(runner);
            Assert.That(runner.LastError, Does.Contain("실행 한도"));
        }
        [UnityTest] public IEnumerator 없는유닛_명령은_실패하고_후속명령을_막는다()
        {
            var battle = Battle(); var runner = Runner();
            runner.Play(Sequence(new EventCommand { kind = EventCommandKind.ChangeHp, unitId = "없음", amount = -1 },
                new EventCommand { kind = EventCommandKind.EndTurn }), battle);
            yield return Finish(runner);
            Assert.That(runner.LastError, Does.Contain("실행할 수 없습니다"));
            Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Ally));
        }
        [UnityTest] public IEnumerator 이벤트_이동공격과_체력분기가_실행된다()
        {
            var battle = Battle(); var runner = Runner();
            runner.Play(Sequence(
                new EventCommand { kind = EventCommandKind.Move, unitId = "아군", cell = new Vector2Int(2, 0) },
                new EventCommand { kind = EventCommandKind.Attack, unitId = "아군", targetId = "적군" },
                new EventCommand { kind = EventCommandKind.BranchHp, unitId = "적군", amount = 23, jumpTo = 4 },
                new EventCommand { kind = EventCommandKind.ChangeHp, unitId = "아군", amount = -10 },
                new EventCommand { kind = EventCommandKind.Stop }), battle);
            yield return Finish(runner);
            Assert.That(battle.Find("적군").Hp, Is.EqualTo(23));
            Assert.That(battle.Find("아군").Hp, Is.EqualTo(30)); Assert.That(runner.LastError, Is.Null);
        }
        [UnityTest] public IEnumerator 기본AI는_공격하고_아군턴을_돌려준다()
        {
            var go = new GameObject("전투 연결 테스트"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("테스트 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>();
            controller.showControls = false; controller.RequireConfirmation = false;
            controller.placements.Add(new UnitPlacement { id = "아군", character = Character(Faction.Ally), cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = Character(Faction.Enemy), cell = Vector2Int.right });
            go.SetActive(true); yield return null; controller.RestartBattle();
            Assert.That(controller.Session, Is.Not.Null);
            controller.Session.EndTurn();
            yield return DefendUntilAllyTurn(controller);
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
            Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(26));
            board.ToggleGrid(); yield return null;
            Assert.That(go.transform.Find("전투 격자").gameObject.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator 적AI는_최소사거리를_지키고_스킬에너지를_소모한다()
        {
            var go = new GameObject("스킬 AI 검증"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>(); controller.showControls = false; controller.RequireConfirmation = false;
            var enemy = Character(Faction.Enemy); enemy.skills[0].minRange = 2; enemy.skills[0].maxRange = 3; enemy.skills[0].energyCost = 3;
            controller.placements.Add(new UnitPlacement { id = "아군", character = Character(Faction.Ally), cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = enemy, cell = Vector2Int.right });
            go.SetActive(true); yield return null; controller.RestartBattle(); controller.Session.EndTurn();
            yield return DefendUntilAllyTurn(controller);
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
            var actor = controller.Session.Find("적군");
            Assert.That(actor.Energy, Is.EqualTo(7)); Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(26));
            Assert.That(BattleGrid.Distance(actor.Cell, controller.Session.Find("아군").Cell), Is.InRange(2, 3));
        }
        [UnityTest] public IEnumerator 선택한_캐릭터의_스킬만_선택하고_사용한다()
        {
            var go = new GameObject("스킬 선택 검증"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>(); controller.showControls = false; controller.RequireConfirmation = false;
            var ally = Character(Faction.Ally); var special = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(special);
            special.power = 18; special.energyCost = 4; ally.skills.Add(special);
            controller.placements.Add(new UnitPlacement { id = "아군", character = ally, cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = Character(Faction.Enemy), cell = Vector2Int.right });
            go.SetActive(true); yield return null; controller.RestartBattle();
            Assert.That(controller.SelectUnit("아군"), Is.True); Assert.That(controller.SelectSkill(1), Is.True);
            Assert.That(controller.SelectSkill(2), Is.False); Assert.That(controller.SelectedSkill.Power, Is.EqualTo(18));
            Assert.That(controller.TrySelectedAttack("적군"), Is.True);
            Assert.That(controller.Session.Find("적군").Hp, Is.EqualTo(15));
            Assert.That(controller.Session.Find("아군").Energy, Is.EqualTo(6)); Assert.That(controller.SelectedSkill, Is.Null);
        }
        [UnityTest] public IEnumerator 이벤트는_지정된_보유스킬로_공격한다()
        {
            var ally = Character(Faction.Ally); var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill);
            skill.power = 18; skill.energyCost = 4; ally.skills.Add(skill);
            var battle = new BattleSession(new BattleGrid(4,4), new[] {
                new BattleUnit("아군", ally, Vector2Int.zero), new BattleUnit("적군", Character(Faction.Enemy), Vector2Int.right) });
            var runner = Runner(); runner.Play(Sequence(new EventCommand { kind = EventCommandKind.Attack, unitId = "아군", targetId = "적군", skill = skill }), battle);
            yield return Finish(runner);
            Assert.That(battle.Find("적군").Hp, Is.EqualTo(15)); Assert.That(battle.Find("아군").Energy, Is.EqualTo(6));
            Assert.That(runner.LastError, Is.Null);
        }
        [UnityTest] public IEnumerator 이벤트_공격이_빗나가도_행동은_성공하고_다음명령으로_진행한다()
        {
            var ally = Character(Faction.Ally); ally.skills[0].energyCost = 3;
            var enemy = Character(Faction.Enemy); enemy.evasion = 100;
            var battle = new BattleSession(new BattleGrid(4,4),new[] {
                new BattleUnit("아군",ally,Vector2Int.zero), new BattleUnit("적군",enemy,Vector2Int.right) });
            var runner = Runner(); runner.Play(Sequence(
                new EventCommand { kind = EventCommandKind.Attack, unitId = "아군", targetId = "적군", skill = ally.skills[0] },
                new EventCommand { kind = EventCommandKind.SetFlag, flag = "공격 완료", flagValue = true }),battle);
            yield return Finish(runner);
            Assert.That(battle.LastAttack.Hit, Is.False); Assert.That(battle.Find("적군").Hp, Is.EqualTo(30));
            Assert.That(battle.Find("아군").Energy, Is.EqualTo(7)); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
            Assert.That(runner.GetFlag("공격 완료"), Is.True); Assert.That(runner.LastError, Is.Null);
        }
        [UnityTest] public IEnumerator 이동하면_선택된_정지전용스킬을_해제하고_재선택을_막는다()
        {
            var go = new GameObject("이동 제한 선택 검증"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>(); controller.showControls = false; controller.RequireConfirmation = false;
            var ally = Character(Faction.Ally); ally.skills[0].canUseAfterMoving = false;
            var mobile = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(mobile); ally.skills.Add(mobile);
            controller.placements.Add(new UnitPlacement { id = "아군", character = ally, cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = Character(Faction.Enemy), cell = new Vector2Int(2,0) });
            go.SetActive(true); yield return null; controller.RestartBattle();
            Assert.That(controller.SelectUnit("아군"), Is.True); Assert.That(controller.SelectedSkill.CanUseAfterMoving, Is.False);
            controller.Session.TryMove(controller.SelectedUnit,Vector2Int.right);
            Assert.That(controller.SelectedSkill, Is.Null); Assert.That(controller.SelectSkill(0), Is.False);
            Assert.That(controller.SelectSkill(1), Is.True); Assert.That(controller.TrySelectedAttack("적군"), Is.True);
        }
        [UnityTest] public IEnumerator 정지전용스킬의_적AI는_접근후_대기하고_다음턴에_공격한다()
        {
            var go = new GameObject("정지 전용 AI 검증"); go.SetActive(false); created.Add(go);
            var board = go.AddComponent<GridBoard>(); var controller = go.AddComponent<BattleController>();
            var cameraObject = new GameObject("검증 카메라"); created.Add(cameraObject);
            controller.board = board; controller.battleCamera = cameraObject.AddComponent<Camera>(); controller.showControls = false; controller.RequireConfirmation = false;
            var enemy = Character(Faction.Enemy); enemy.skills[0].canUseAfterMoving = false; enemy.skills[0].energyCost = 3;
            controller.placements.Add(new UnitPlacement { id = "아군", character = Character(Faction.Ally), cell = Vector2Int.zero });
            controller.placements.Add(new UnitPlacement { id = "적군", character = enemy, cell = new Vector2Int(3,0) });
            go.SetActive(true); yield return null; controller.RestartBattle();
            controller.Session.EndTurn();
            yield return DefendUntilAllyTurn(controller);
            var actor = controller.Session.Find("적군");
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
            Assert.That(actor.Cell, Is.Not.EqualTo(new Vector2Int(3,0)));
            Assert.That(actor.Energy, Is.EqualTo(10)); Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(30));
            controller.Session.EndTurn();
            yield return DefendUntilAllyTurn(controller);
            Assert.That(controller.Session.ActiveFaction, Is.EqualTo(Faction.Ally));
            Assert.That(actor.Energy, Is.EqualTo(7)); Assert.That(controller.Session.Find("아군").Hp, Is.EqualTo(26));
        }
        [UnityTest] public IEnumerator 이벤트도_이동후불가_스킬의_사용을_거절한다()
        {
            var ally = Character(Faction.Ally); ally.skills[0].canUseAfterMoving = false;
            var battle = new BattleSession(new BattleGrid(6,6),new[] {
                new BattleUnit("아군",ally,Vector2Int.zero),new BattleUnit("적군",Character(Faction.Enemy),new Vector2Int(2,0)) });
            var runner = Runner(); runner.Play(Sequence(
                new EventCommand { kind = EventCommandKind.Move, unitId = "아군", cell = Vector2Int.right },
                new EventCommand { kind = EventCommandKind.Attack, unitId = "아군", targetId = "적군", skill = ally.skills[0] }),battle);
            yield return Finish(runner);
            Assert.That(runner.LastError, Is.Not.Null); Assert.That(battle.Find("적군").Hp, Is.EqualTo(30));
            Assert.That(battle.Find("아군").Energy, Is.EqualTo(10)); Assert.That(battle.Find("아군").HasActed, Is.False);
        }
    }
}
