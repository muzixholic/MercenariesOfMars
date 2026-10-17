using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.Tests
{
    public sealed class UnitCommandTests
    {
        readonly List<Object> created = new();
        BattleController Create()
        {
            var go = new GameObject("유닛 행동 메뉴 검증"); go.SetActive(false); created.Add(go);
            var c = go.AddComponent<BattleController>(); c.board = go.AddComponent<GridBoard>();
            c.board.width = 6; c.board.height = 6; c.board.blockedCells.Add(new Vector2Int(1,1));
            c.battleCamera = go.AddComponent<Camera>(); c.showControls = false; c.automaticEnemy = false;
            c.battleCamera.transform.position = new Vector3(2,10,-3); c.battleCamera.transform.LookAt(new Vector3(2,0,2));
            for (int i = 0; i < 3; i++)
            {
                var d = ScriptableObject.CreateInstance<CharacterDefinition>(); created.Add(d);
                d.faction = i < 2 ? Faction.Ally : Faction.Enemy; d.movement = 2;
                var skill = ScriptableObject.CreateInstance<SkillDefinition>(); created.Add(skill); d.skills.Add(skill);
                c.placements.Add(new UnitPlacement { id = "유닛" + i, character = d, cell = i == 0 ? Vector2Int.zero : i == 1 ? Vector2Int.up : Vector2Int.right });
            }
            go.SetActive(true); return c;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { foreach (var item in created) if (item != null) Object.Destroy(item); created.Clear(); yield return null; }
        Color UnitColor(BattleController c, string id)
        {
            var unit=c.Session.Find(id);
            return c.transform.Find(unit.Name+" ("+id+")").GetComponent<Renderer>().sharedMaterial.color;
        }
        [UnityTest] public IEnumerator 행동완료는_회색이며_다음_자기턴에_진영색으로_복원된다()
        {
            var c=Create(); yield return null; c.RestartBattle();
            c.Session.TryWait(c.Session.Find("유닛1"));
            var gray=UnitColor(c,"유닛1"); Assert.That(gray.r, Is.EqualTo(gray.g).Within(.001f)); Assert.That(gray.g, Is.EqualTo(gray.b).Within(.001f));
            Assert.That(UnitColor(c,"유닛0").b, Is.GreaterThan(UnitColor(c,"유닛0").r));
            c.Session.EndTurn(); Assert.That(UnitColor(c,"유닛1"), Is.EqualTo(gray));
            c.Session.TryWait(c.Session.Find("유닛2"));
            Assert.That(UnitColor(c,"유닛1").b, Is.GreaterThan(UnitColor(c,"유닛1").r));
            var enemyGray=UnitColor(c,"유닛2"); Assert.That(enemyGray.r, Is.EqualTo(enemyGray.b).Within(.001f));
            c.Session.EndTurn(); Assert.That(UnitColor(c,"유닛2").r, Is.GreaterThan(UnitColor(c,"유닛2").b));
        }
        [UnityTest] public IEnumerator 이동만하면_진영색을_유지하고_공격이_빗나가도_행동완료는_회색이다()
        {
            var c=Create(); c.RequireConfirmation=false; c.placements[2].character.evasion=100;
            yield return null; c.RestartBattle();
            c.Session.TryMove(c.Session.Find("유닛1"),new Vector2Int(0,2));
            Assert.That(UnitColor(c,"유닛1").b, Is.GreaterThan(UnitColor(c,"유닛1").r));
            c.Session.TryAttack(c.Session.Find("유닛0"),c.Session.Find("유닛2"));
            Assert.That(c.Session.LastAttack.Hit, Is.False);
            var gray=UnitColor(c,"유닛0"); Assert.That(gray.r, Is.EqualTo(gray.g).Within(.001f)); Assert.That(gray.g, Is.EqualTo(gray.b).Within(.001f));
        }
        [UnityTest] public IEnumerator 대사넘기기는_대사대기중에만_동작하고_한페이지씩_진행한다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.eventRunner=c.gameObject.AddComponent<EventRunner>();
            var sequence=ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.Add(new EventCommand {kind=EventCommandKind.Dialogue,speaker="안내",text="첫 페이지"});
            sequence.commands.Add(new EventCommand {kind=EventCommandKind.Dialogue,speaker="안내",text="둘째 페이지"});
            sequence.commands.Add(new EventCommand {kind=EventCommandKind.Wait,seconds=.05f});
            Assert.That(c.TryAdvanceDialogue(), Is.False); c.eventRunner.Play(sequence,c.Session);
            Assert.That(c.TryAdvanceDialogue(), Is.True);
            for(int i=0;i<8 && c.eventRunner.Dialogue!="둘째 페이지";i++) yield return null;
            Assert.That(c.eventRunner.Dialogue, Is.EqualTo("둘째 페이지"));
            for(int i=0;i<3;i++) yield return null;
            Assert.That(c.eventRunner.Dialogue, Is.EqualTo("둘째 페이지"));
            c.TryAdvanceDialogue();
            for(int i=0;i<8 && c.eventRunner.Dialogue!=null;i++) yield return null;
            Assert.That(c.TryAdvanceDialogue(), Is.False);
            yield return new WaitForSecondsRealtime(.1f); Assert.That(c.eventRunner.IsRunning, Is.False);
        }
        [UnityTest] public IEnumerator 적공격범위와_공격자_피격대상은_대응과_확인동안_유지되고_연출전에_지워진다()
        {
            var c=Create(); var skill=c.placements[2].character.skills[0]; skill.minRange=2; skill.maxRange=3;
            yield return null; c.RestartBattle(); c.Session.EndTurn();
            var enemy=c.Session.Find("유닛2"); var target=c.Session.Find("유닛1");
            Assert.That(c.Session.TryAttack(enemy,target), Is.True);
            var expected=new List<Vector2Int>();
            for(int y=0;y<c.Session.Grid.Height;y++) for(int x=0;x<c.Session.Grid.Width;x++)
                if(enemy.Skills[0].IsInRange(enemy.Cell,new Vector2Int(x,y))) expected.Add(new Vector2Int(x,y));
            CollectionAssert.AreEquivalent(expected,c.board.HighlightedCells);
            Assert.That(c.board.AttackerMarkerCell, Is.EqualTo(enemy.Cell)); Assert.That(c.board.TargetMarkerCell, Is.EqualTo(target.Cell));
            for(int i=0;i<3;i++) yield return null;
            CollectionAssert.AreEquivalent(expected,c.board.HighlightedCells);
            c.ChooseReaction(ReactionChoice.Defend); c.CancelCombat();
            CollectionAssert.AreEquivalent(expected,c.board.HighlightedCells);
            c.ChooseReaction(ReactionChoice.Defend); c.ConfirmCombat();
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.board.AttackerMarkerCell, Is.Null); Assert.That(c.board.TargetMarkerCell, Is.Null);
            c.Presenter.Finish(); Assert.That(c.TurnBannerText, Does.Contain("아군 턴")); Assert.That(c.IsTurnBannerVisible, Is.True);
        }
        [UnityTest] public IEnumerator 턴전환표시는_아군과_적군및_라운드를_반영하고_시간후_사라진다()
        {
            var c=Create(); c.turnBannerSeconds=.15f; yield return null; c.RestartBattle();
            Assert.That(c.IsTurnBannerVisible, Is.True); Assert.That(c.TurnBannerText, Does.Contain("아군 턴"));
            c.Session.EndTurn(); Assert.That(c.IsTurnBannerVisible, Is.True); Assert.That(c.TurnBannerText, Does.Contain("적군 턴"));
            c.Session.EndTurn(); Assert.That(c.TurnBannerText, Does.Contain("2 라운드"));
            yield return new WaitForSecondsRealtime(.2f); Assert.That(c.IsTurnBannerVisible, Is.False);
            c.Session.ChangeHp("유닛0",-1); Assert.That(c.IsTurnBannerVisible, Is.False);
        }
        [UnityTest] public IEnumerator 아군능력과_적군재클릭은_상세화면을_열고_현재수치를_유지한다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1");
            c.Session.ChangeHp("유닛1",-5);
            Assert.That(c.OpenUnitDetails(), Is.True); Assert.That(c.DetailsUnit.Id, Is.EqualTo("유닛1"));
            Assert.That(c.DetailsUnit.Hp, Is.EqualTo(25)); Assert.That(c.DetailsUnit.Skills.Count, Is.EqualTo(1));
            Assert.That(c.board.HighlightedCells, Is.Empty);
            c.ClearSelection(); Assert.That(c.DetailsUnit, Is.Null);
            c.HandleCellClick(Vector2Int.right); Assert.That(c.InspectedEnemy, Is.Not.Null);
            Assert.That(c.board.HighlightedCells, Is.Not.Empty);
            c.HandleCellClick(Vector2Int.right); Assert.That(c.DetailsUnit.Id, Is.EqualTo("유닛2"));
            Assert.That(c.board.HighlightedCells, Is.Empty);
            Assert.That(c.DetailsUnit.HasActed || c.DetailsUnit.HasMoved, Is.False);
            Assert.That(c.HandleCellClick(new Vector2Int(4,4)), Is.False); Assert.That(c.DetailsUnit, Is.Null);
        }
        [UnityTest] public IEnumerator 빈칸_우클릭에서만_설정과_턴종료_메뉴가_열린다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            Assert.That(c.HandleRightClick(Vector2Int.right,Vector2.zero), Is.False);
            Assert.That(c.IsContextMenuOpen, Is.False); Assert.That(c.SelectedUnit, Is.Null);
            Assert.That(c.HandleRightClick(new Vector2Int(-1,0),Vector2.zero), Is.False);
            Assert.That(c.HandleRightClick(new Vector2Int(4,4),new Vector2(100,100)), Is.True);
            Assert.That(c.ContextMenuRect.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(c.Session.Round, Is.EqualTo(1)); Assert.That(c.board.HighlightedCells, Is.Empty);
            Assert.That(c.OpenSettings(), Is.True); Assert.That(c.IsSettingsOpen, Is.True); Assert.That(c.IsContextMenuOpen, Is.False);
            c.ClearSelection(); Assert.That(c.IsSettingsOpen, Is.False);
            c.HandleRightClick(new Vector2Int(4,4),Vector2.zero);
            Assert.That(c.EndTurnFromMenu(), Is.True); Assert.That(c.Session.ActiveFaction, Is.EqualTo(Faction.Enemy));
            Assert.That(c.IsContextMenuOpen, Is.False); Assert.That(c.EndTurnFromMenu(), Is.False);
            c.Session.EndTurn(); Assert.That(c.Session.Round, Is.EqualTo(2));
            c.HandleRightClick(new Vector2Int(4,4),Vector2.zero); c.HandleCellClick(new Vector2Int(5,5));
            Assert.That(c.IsContextMenuOpen, Is.False);
            c.HandleRightClick(new Vector2Int(4,4),new Vector2(Screen.width+1000,Screen.height+1000));
            Assert.That(c.ContextMenuRect.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(c.ContextMenuRect.yMax, Is.LessThanOrEqualTo(Screen.height));
        }
        [UnityTest] public IEnumerator 메뉴밖_클릭과_선택해제는_메뉴와_막대와_범위를_닫는다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1");
            c.HandleCellClick(new Vector2Int(5,5));
            Assert.That(c.SelectedUnit, Is.Null); Assert.That(c.CanShowUnitMenu, Is.False);
            c.SelectUnit("유닛1"); c.HandleCellClick(Vector2Int.up); Assert.That(c.SelectedUnit, Is.Null);
            c.SelectUnit("유닛1"); c.OpenSkillSelection(); c.HandleCellClick(new Vector2Int(-1,-1));
            Assert.That(c.SelectedUnit, Is.Null); Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.None));
            c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move); c.ClearSelection();
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.SelectedStatusRect.width, Is.Zero);
            c.ClearSelection(); Assert.That(c.SelectedUnit, Is.Null);
        }
        [UnityTest] public IEnumerator 적조회는_행동없이_체력과_에너지와_이동범위를_표시하고_다른클릭으로_닫는다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1");
            Assert.That(c.HandleCellClick(Vector2Int.right), Is.True);
            Assert.That(c.InspectedEnemy.Id, Is.EqualTo("유닛2")); Assert.That(c.SelectedUnit, Is.Null);
            Assert.That(c.CanShowUnitMenu, Is.False); Assert.That(c.SelectedHpFraction, Is.EqualTo(1));
            Assert.That(c.SelectedEnergyFraction, Is.EqualTo(1)); Assert.That(c.SelectedStatusRect.width, Is.GreaterThan(0));
            var enemy=c.InspectedEnemy;
            var expected=c.Session.Grid.Reachable(enemy.Cell,enemy.Movement,cell=>c.Session.At(cell)!=null).Keys.Where(cell=>cell!=enemy.Cell);
            CollectionAssert.AreEquivalent(expected,c.board.HighlightedCells);
            Assert.That(c.SetCommandMode(UnitCommandMode.Move), Is.False);
            Assert.That(c.WaitSelectedUnit(), Is.False); Assert.That(enemy.HasMoved || enemy.HasActed, Is.False);
            c.Session.ChangeHp(enemy.Id,-5); Assert.That(c.SelectedHpFraction, Is.EqualTo(25f/30).Within(.001f));
            c.HandleCellClick(new Vector2Int(5,5));
            Assert.That(c.InspectedEnemy, Is.Null); Assert.That(c.board.HighlightedCells, Is.Empty);
            c.InspectEnemy(enemy.Id); c.ClearSelection(); Assert.That(c.InspectedEnemy, Is.Null);
            c.InspectEnemy(enemy.Id); c.Session.EndTurn(); Assert.That(c.InspectedEnemy, Is.Null);
            Assert.That(c.board.HighlightedCells, Is.Empty);
        }
        [UnityTest] public IEnumerator 공격창은_중앙에_열리고_선택후_최소최대사거리를_표시한다()
        {
            var c=Create();
            var skill=c.placements[0].character.skills[0]; skill.minRange=2; skill.maxRange=3;
            c.placements[2].cell=new Vector2Int(2,0);
            yield return null; c.RestartBattle(); c.SelectUnit("유닛0");
            Assert.That(c.OpenSkillSelection(), Is.True);
            Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.SkillSelection));
            Assert.That(c.SkillSelectionRect.center.x,Is.EqualTo(Screen.width/2f).Within(1));
            Assert.That(c.SkillSelectionRect.center.y,Is.EqualTo(Screen.height/2f).Within(1));
            Assert.That(c.board.HighlightedCells, Is.Empty);
            Assert.That(c.HandleCellClick(new Vector2Int(2,0)), Is.True);
            Assert.That(c.Session.PendingAttack, Is.Null);
            c.SelectUnit("유닛0"); c.OpenSkillSelection();
            Assert.That(c.SelectSkill(0), Is.True);
            Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.Attack));
            Assert.That(c.board.HighlightedCells.Contains(Vector2Int.zero), Is.False);
            Assert.That(c.board.HighlightedCells.Contains(Vector2Int.right), Is.False);
            Assert.That(c.board.HighlightedCells.Contains(new Vector2Int(2,0)), Is.True);
            Assert.That(c.board.HighlightedCells.Contains(new Vector2Int(1,1)), Is.True);
            foreach(var cell in c.board.HighlightedCells)
                Assert.That(BattleGrid.Distance(Vector2Int.zero,cell), Is.InRange(2,3));
            Assert.That(c.HandleCellClick(Vector2Int.right), Is.False);
            c.SetCommandMode(UnitCommandMode.None); Assert.That(c.board.HighlightedCells, Is.Empty);
            c.SetCommandMode(UnitCommandMode.Move); Assert.That(c.board.HighlightedCells, Is.Empty);
        }
        [UnityTest] public IEnumerator 사용할수없는_스킬은_선택되지않고_막대는_체력과_소모에너지를_반영한다()
        {
            var c=Create(); c.RequireConfirmation=false;
            c.placements[0].character.skills[0].energyCost=3;
            yield return null; c.RestartBattle(); c.SelectUnit("유닛0");
            Assert.That(c.SelectedHpFraction, Is.EqualTo(1)); Assert.That(c.SelectedEnergyFraction, Is.EqualTo(1));
            c.Session.ChangeHp("유닛0",-12); Assert.That(c.SelectedHpFraction, Is.EqualTo(.6f).Within(.001f));
            c.SelectSkill(0); c.TrySelectedAttack("유닛2"); c.Session.EndTurn(); c.Session.EndTurn(); c.SelectUnit("유닛0");
            Assert.That(c.SelectedEnergyFraction, Is.EqualTo(.7f).Within(.001f));
            Assert.That(c.SelectedStatusRect.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.SelectedStatusRect.xMax, Is.LessThanOrEqualTo(Screen.width));
            c.Session.ChangeHp("유닛0",-100); c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            Assert.That(c.HandleCellClick(new Vector2Int(0,2)), Is.True);
            // 전투 시작 시 수치를 복사하므로 별도 전투에서 에너지 부족을 검증한다.
            var d=Create(); d.placements[0].character.maxEnergy=0; d.placements[0].character.skills[0].energyCost=1;
            yield return null; d.RestartBattle(); d.SelectUnit("유닛0"); Assert.That(d.OpenSkillSelection(),Is.False);
            Assert.That(d.SelectedEnergyFraction, Is.Zero); Assert.That(d.SelectSkill(0), Is.False);
            Assert.That(d.CommandMode, Is.EqualTo(UnitCommandMode.None)); Assert.That(d.board.HighlightedCells, Is.Empty);
        }
        [UnityTest] public IEnumerator 아군선택은_메뉴를열고_모드선택전에는_이동하거나_공격하지않는다()
        {
            var c=Create(); yield return null; c.RestartBattle();
            Assert.That(c.HandleCellClick(Vector2Int.zero), Is.True); Assert.That(c.CanShowUnitMenu, Is.True);
            Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.None));
            Assert.That(c.HandleCellClick(Vector2Int.right), Is.True);
            Assert.That(c.HandleCellClick(new Vector2Int(0,2)), Is.False);
            Assert.That(c.Session.PendingAttack, Is.Null); Assert.That(c.Session.Find("유닛0").HasMoved, Is.False);
            Assert.That(c.InspectedEnemy, Is.Null); c.SelectUnit("유닛0");
            Assert.That(c.UnitMenuRect.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(c.UnitMenuRect.xMax, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(c.UnitMenuRect.yMax, Is.LessThanOrEqualTo(Screen.height));
            c.SetCommandMode(UnitCommandMode.Attack);
            Assert.That(c.HandleCellClick(Vector2Int.right), Is.True);
            Assert.That(c.CanShowUnitMenu, Is.False); Assert.That(c.board.HighlightedCells, Is.Empty);
        }
        [UnityTest] public IEnumerator 이동영역은_장애물과_점유칸을_제외하며_이동성공시에만_소모된다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1");
            Assert.That(c.SetCommandMode(UnitCommandMode.Move), Is.True);
            var expected=c.Session.MovementOptions(c.SelectedUnit).Keys.Where(cell=>cell!=c.SelectedUnit.Cell).ToArray();
            CollectionAssert.AreEquivalent(expected,c.board.HighlightedCells);
            Assert.That(c.board.HighlightedCells.Contains(Vector2Int.zero), Is.False);
            Assert.That(c.board.HighlightedCells.Contains(Vector2Int.right), Is.False);
            Assert.That(c.board.HighlightedCells.Contains(new Vector2Int(1,1)), Is.False);
            Assert.That(c.HandleCellClick(new Vector2Int(1,1)), Is.False);
            Assert.That(c.HandleCellClick(new Vector2Int(5,5)), Is.False);
            Assert.That(c.SelectedUnit.HasMoved, Is.False); Assert.That(c.board.HighlightedCells, Is.Not.Empty);
            c.board.ToggleGrid(); yield return null;
            Assert.That(c.board.HighlightedCells, Is.Not.Empty);
            Assert.That(c.HandleCellClick(new Vector2Int(0,2)), Is.True);
            Assert.That(c.SelectedUnit.Cell, Is.EqualTo(new Vector2Int(0,2))); Assert.That(c.SelectedUnit.HasMoved, Is.True);
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.None));
            Assert.That(c.SetCommandMode(UnitCommandMode.Move), Is.False);
            Assert.That(c.SetCommandMode(UnitCommandMode.Attack), Is.True);
        }
        [UnityTest] public IEnumerator 선택변경과_취소와_대기는_이동영역을_정리한다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            c.SetCommandMode(UnitCommandMode.None); Assert.That(c.board.HighlightedCells, Is.Empty);
            c.SetCommandMode(UnitCommandMode.Move); c.SelectUnit("유닛0");
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.CommandMode, Is.EqualTo(UnitCommandMode.None));
            Assert.That(c.SetCommandMode(UnitCommandMode.Move), Is.False);
            c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            Assert.That(c.WaitSelectedUnit(), Is.True); Assert.That(c.CanShowUnitMenu, Is.False);
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.Session.Find("유닛1").HasActed, Is.True);
        }
        [UnityTest] public IEnumerator 이벤트와_턴종료와_비활성화시_메뉴와_영역을_숨긴다()
        {
            var c=Create(); yield return null; c.RestartBattle(); c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            var runner=c.gameObject.AddComponent<EventRunner>(); c.eventRunner=runner;
            var sequence=ScriptableObject.CreateInstance<EventSequence>(); created.Add(sequence);
            sequence.commands.Add(new EventCommand {kind=EventCommandKind.Dialogue,speaker="안내",text="대기"}); runner.Play(sequence,c.Session);
            yield return null;
            Assert.That(c.CanShowUnitMenu, Is.False); Assert.That(c.board.HighlightedCells, Is.Empty);
            runner.ResetState(); c.SetCommandMode(UnitCommandMode.Move); c.Session.EndTurn();
            Assert.That(c.board.HighlightedCells, Is.Empty); Assert.That(c.CanShowUnitMenu, Is.False);
            c.Session.EndTurn(); c.SelectUnit("유닛1"); c.SetCommandMode(UnitCommandMode.Move);
            c.enabled=false; yield return null; Assert.That(c.board.HighlightedCells, Is.Empty);
        }
    }
}
