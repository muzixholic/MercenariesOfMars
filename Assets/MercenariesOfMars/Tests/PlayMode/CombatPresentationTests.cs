using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class CombatPresentationTests
    {
        readonly List<Object> created = new();
        BattleController Create(int power = 10, int accuracy = 100)
        {
            var go = new GameObject("연출 검증"); go.SetActive(false); created.Add(go);
            var controller = go.AddComponent<BattleController>(); controller.board = go.AddComponent<GridBoard>();
            controller.battleCamera = go.AddComponent<Camera>(); controller.automaticEnemy = false; controller.showControls = false;
            var profile = ScriptableObject.CreateInstance<CombatPresentationProfile>(); created.Add(profile);
            profile.attackSeconds = .08f; profile.damageSeconds = .08f; profile.pauseSeconds = .05f; controller.defaultPresentation = profile;
            for (int i = 0; i < 2; i++)
            {
                var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
                d.faction = i == 0 ? Faction.Ally : Faction.Enemy; d.attack = 0; d.defense = 0; d.accuracy = accuracy;
                var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill); skill.power = power; skill.energyCost = 2;
                d.skills.Add(skill); controller.placements.Add(new UnitPlacement { id = i == 0 ? "아군" : "적군", character = d, cell = new Vector2Int(i,0) });
            }
            go.SetActive(true); return controller;
        }
        void Attack(BattleController c, ReactionChoice choice = ReactionChoice.Counterattack)
        {
            c.Session.EndTurn();
            Assert.That(c.Session.TryAttack(c.Session.Find("적군"), c.Session.Find("아군")), Is.True);
            Assert.That(c.ChooseReaction(choice, choice == ReactionChoice.Counterattack ? 0 : -1), Is.True);
            Assert.That(c.ConfirmCombat(), Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { foreach (var item in created) if (item != null) Object.Destroy(item); created.Clear(); yield return null; }
        [UnityTest] public IEnumerator 공격과_반격이_순서대로_재생되고_체력과_턴이_복귀한다()
        {
            var c = Create(); yield return null; c.RestartBattle(); Attack(c);
            Assert.That(c.Session.IsPresenting, Is.True); Assert.That(c.battleCamera.enabled, Is.False);
            Assert.That(c.Presenter.DisplayedAttackerHp, Is.EqualTo(30)); Assert.That(c.Presenter.DisplayedTargetHp, Is.EqualTo(30));
            Assert.That(c.Session.ActiveFaction, Is.EqualTo(Faction.Enemy));
            Assert.That(c.Session.TryWait(c.Session.Find("아군")), Is.False); Assert.That(c.SelectUnit("아군"), Is.False);
            Assert.That(c.Session.ChangeHp("아군", -1), Is.False); c.Session.EndTurn();
            var phases = new List<string>(); float deadline = Time.realtimeSinceStartup + 5;
            while(c.Presenter.IsPlaying && Time.realtimeSinceStartup < deadline)
            { if (!phases.Contains(c.Presenter.Phase)) phases.Add(c.Presenter.Phase); yield return null; }
            Assert.That(c.Presenter.IsPlaying, Is.False); Assert.That(c.Session.IsPresenting, Is.False);
            Assert.That(phases.FindIndex(x => x.StartsWith("공격 ·")), Is.LessThan(phases.IndexOf("공격 피해")));
            Assert.That(phases.IndexOf("공격 피해"), Is.LessThan(phases.FindIndex(x => x.StartsWith("반격 ·"))));
            Assert.That(phases.FindIndex(x => x.StartsWith("반격 ·")), Is.LessThan(phases.IndexOf("반격 피해")));
            Assert.That(c.Presenter.DisplayedAttackerHp, Is.EqualTo(20)); Assert.That(c.Presenter.DisplayedTargetHp, Is.EqualTo(20));
            Assert.That(c.Session.Round, Is.EqualTo(2)); Assert.That(c.battleCamera.enabled, Is.True);
            Assert.That(c.Session.Find("아군").Energy, Is.EqualTo(8));
        }
        [UnityTest] public IEnumerator 전투불능이면_반격없이_종료하고_건너뛰어도_중복피해가_없다()
        {
            var c = Create(100); yield return null; c.RestartBattle(); Attack(c);
            Assert.That(c.Session.LastCounterattack, Is.Null); Assert.That(c.Session.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            c.Presenter.Finish(); c.Presenter.Finish(); yield return null;
            Assert.That(c.Session.Outcome, Is.EqualTo(BattleOutcome.Defeat)); Assert.That(c.Session.Find("아군").Hp, Is.Zero);
            Assert.That(c.Session.Find("적군").Energy, Is.EqualTo(8)); Assert.That(c.battleCamera.enabled, Is.True);
        }
        [UnityTest] public IEnumerator 비활성화시_카메라와_잠금이_복구되고_빗나감은_피해가_없다()
        {
            var c = Create(10,0); yield return null; c.RestartBattle(); Attack(c,ReactionChoice.Evade);
            Assert.That(c.Session.LastAttack.Hit, Is.False);
            c.enabled = false; yield return null;
            Assert.That(c.Session.IsPresenting, Is.False); Assert.That(c.battleCamera.enabled, Is.True);
            Assert.That(c.Session.Find("아군").Hp, Is.EqualTo(30));
        }
        [UnityTest] public IEnumerator 이벤트는_공격확정후에도_연출종료까지_기다린다()
        {
            var c = Create(); yield return null; c.RestartBattle();
            var runner = c.gameObject.AddComponent<EventRunner>();
            var sequence = ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Attack, unitId = "아군", targetId = "적군" });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.SetFlag, flag = "완료", flagValue = true });
            runner.Play(sequence,c.Session); yield return null; c.ConfirmCombat();
            for (int i=0;i<3;i++) yield return null;
            Assert.That(runner.GetFlag("완료"), Is.False); Assert.That(c.Session.IsPresenting, Is.True);
            c.Presenter.Finish();
            for (int i=0;i<5 && runner.IsRunning;i++) yield return null;
            Assert.That(runner.GetFlag("완료"), Is.True); Assert.That(runner.LastError, Is.Null);
        }
    }
}
