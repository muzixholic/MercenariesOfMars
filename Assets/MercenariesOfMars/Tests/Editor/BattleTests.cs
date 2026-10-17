using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class BattleTests
    {
        readonly List<UnityEngine.Object> created = new();
        CharacterDefinition Definition(Faction faction, int movement = 3, int attack = 10)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(definition);
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill); skill.power = attack;
            definition.attack = 0; definition.faction = faction; definition.movement = movement; definition.skills.Add(skill); return definition;
        }
        BattleSession Battle(out BattleUnit ally, out BattleUnit enemy)
        {
            ally = new BattleUnit("아군", Definition(Faction.Ally), Vector2Int.zero);
            enemy = new BattleUnit("적군", Definition(Faction.Enemy), new Vector2Int(3, 0));
            return new BattleSession(new BattleGrid(6, 6), new[] { ally, enemy });
        }
        EventSequence Sequence(params EventCommand[] commands)
        {
            var sequence = ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.AddRange(commands); return sequence;
        }
        EventRunner Runner()
        {
            var go = new GameObject("이벤트 검증"); created.Add(go); return go.AddComponent<EventRunner>();
        }
        [TearDown] public void Cleanup()
        { foreach (var item in created) if (item != null) UnityEngine.Object.DestroyImmediate(item); created.Clear(); }

        [Test] public void 장애물과_점유칸을_피해_최단경로를_찾는다()
        {
            var grid = new BattleGrid(4, 3, new[] { new Vector2Int(1, 0) });
            var paths = grid.Reachable(Vector2Int.zero, 4, c => c == new Vector2Int(0, 2));
            Assert.That(paths[new Vector2Int(2, 0)].Count, Is.EqualTo(4));
            Assert.That(paths.ContainsKey(new Vector2Int(1, 0)), Is.False);
            Assert.That(paths.ContainsKey(new Vector2Int(0, 2)), Is.False);
            Assert.That(paths.ContainsKey(new Vector2Int(3, 0)), Is.False);
        }
        [Test] public void 이동후_재이동과_점유칸_이동은_거절한다()
        {
            var battle = Battle(out var ally, out var enemy);
            Assert.That(battle.TryMove(ally, enemy.Cell), Is.False);
            Assert.That(battle.TryMove(ally, new Vector2Int(2, 0)), Is.True);
            Assert.That(battle.TryMove(ally, new Vector2Int(1, 0)), Is.False);
            Assert.That(battle.TryAttack(ally, enemy), Is.True);
            Assert.That(enemy.Hp, Is.EqualTo(23));
            Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
        }
        [Test] public void 아군_전원_행동후_적군턴으로_넘어간다()
        {
            var first = new BattleUnit("아군1", Definition(Faction.Ally), Vector2Int.zero);
            var second = new BattleUnit("아군2", Definition(Faction.Ally), Vector2Int.up);
            var enemy = new BattleUnit("적군", Definition(Faction.Enemy), new Vector2Int(3, 0));
            var battle = new BattleSession(new BattleGrid(5, 5), new[] { first, second, enemy });
            Assert.That(battle.TryWait(enemy), Is.False);
            battle.TryWait(first); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Ally));
            battle.TryWait(second); Assert.That(battle.ActiveFaction, Is.EqualTo(Faction.Enemy));
            battle.TryWait(enemy); Assert.That(battle.Round, Is.EqualTo(2));
            Assert.That(first.HasActed || first.HasMoved, Is.False);
        }
        [Test] public void 사거리밖과_같은진영_공격을_거절한다()
        {
            var battle = Battle(out var ally, out var enemy);
            Assert.That(battle.TryAttack(ally, enemy), Is.False);
            Assert.That(battle.TryAttack(ally, ally), Is.False);
            Assert.That(ally.HasActed, Is.False);
        }
        [Test] public void 전투불능과_승리후에는_행동하지_못한다()
        {
            var battle = Battle(out var ally, out var enemy);
            Assert.That(battle.ChangeHp(enemy.Id, -100), Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.At(enemy.Cell), Is.Null);
            Assert.That(battle.TryWait(ally), Is.False);
            Assert.That(battle.ChangeHp(enemy.Id, 30), Is.False);
        }
        [Test] public void 아군전멸은_패배다()
        {
            var battle = Battle(out var ally, out _); battle.ChangeHp(ally.Id, -100);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }
        [Test] public void 중복ID와_겹친배치를_거절한다()
        {
            var a = Definition(Faction.Ally); var e = Definition(Faction.Enemy);
            Assert.Throws<ArgumentException>(() => new BattleSession(new BattleGrid(4, 4), new[] {
                new BattleUnit("중복", a, Vector2Int.zero), new BattleUnit("중복", e, Vector2Int.one) }));
            Assert.Throws<ArgumentException>(() => new BattleSession(new BattleGrid(4, 4), new[] {
                new BattleUnit("아군", a, Vector2Int.zero), new BattleUnit("적군", e, Vector2Int.zero) }));
        }
        [Test] public void 능력치는_전투시작_값으로_복사한다()
        {
            var definition = Definition(Faction.Ally); var unit = new BattleUnit("아군", definition, Vector2Int.zero);
            definition.skills[0].power = 999; definition.maxEnergy = 999;
            Assert.That(unit.Skills[0].Power, Is.EqualTo(10)); Assert.That(unit.MaxEnergy, Is.EqualTo(10));
        }
        [Test] public void 체력변경은_최대치를_넘지_않는다()
        {
            var battle = Battle(out var ally, out _); battle.ChangeHp(ally.Id, int.MaxValue);
            Assert.That(ally.Hp, Is.EqualTo(ally.MaxHp));
        }
        [Test] public void 잘못된_분기와_대상ID를_검증한다()
        {
            var sequence = Sequence(new EventCommand { kind = EventCommandKind.BranchHp, jumpTo = 8 });
            Assert.That(sequence.ValidateCommands().Count, Is.EqualTo(2));
        }
        [Test] public void 보드좌표는_회전과_크기변경에도_왕복된다()
        {
            var go = new GameObject("격자 검증"); created.Add(go); var board = go.AddComponent<GridBoard>();
            board.cellSize = 2; go.transform.position = new Vector3(3, 2, 5);
            go.transform.rotation = Quaternion.Euler(0, 35, 0); go.transform.localScale = new Vector3(2, 1, 3);
            Assert.That(board.WorldToCell(board.CellToWorld(new Vector2Int(2, 3))), Is.EqualTo(new Vector2Int(2, 3)));
        }
    }
}
