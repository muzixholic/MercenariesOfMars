using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class WeaponSelectionViewTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        BattleController Create(int count=4)
        {
            var root=new GameObject("고전 전술 무장창 검증"); owned.Add(root); root.SetActive(false);
            var c=root.AddComponent<BattleController>(); c.board=root.AddComponent<GridBoard>(); c.battleCamera=root.AddComponent<Camera>();
            c.eventRunner=root.AddComponent<EventRunner>(); c.automaticEnemy=false; c.enableCombatPresentation=false; c.RequireConfirmation=false; c.showControls=true;
            for(int i=0;i<2;i++)
            {
                var d=New<CharacterDefinition>(); d.faction=i==0?Faction.Ally:Faction.Enemy; d.mech=New<MechDefinition>(); d.pilot=New<PilotDefinition>(); d.pilot.accuracy=999;
                d.mech.maxHp=100; d.mech.maxEnergy=40;
                if(i==0) for(int n=0;n<count;n++)
                {
                    var s=New<SkillDefinition>(); s.displayName="무장 "+n; s.power=3; s.energyCost=2; s.maxRange=3;
                    if(n==1) { s.maxAmmo=3; s.currentAmmo=0; }
                    if(n==2) s.energyCost=99;
                    if(n==3) s.canUseAfterMoving=false;
                    d.mech.weapons.Add(s);
                }
                c.placements.Add(new UnitPlacement {id="유닛"+i,character=d,cell=new Vector2Int(i,0)});
            }
            root.SetActive(true); return c;
        }
        [UnityTearDown] public IEnumerator Cleanup() { foreach(var o in owned) if(o!=null) Object.Destroy(o); owned.Clear(); yield return null; }
        void Open(BattleController c) { Assert.That(c.RestartBattle(),Is.True); Assert.That(c.SelectUnit("유닛0"),Is.True); Assert.That(c.OpenSkillSelection(),Is.True); c.WeaponMenu.Refresh(); }
        [UnityTest] public IEnumerator 행을_살펴보는_동안은_소모하지_않고_결정할때만_사거리를_표시한다()
        {
            var c=Create(); yield return null; Open(c); var ui=c.WeaponMenu;
            Assert.That(ui.IsRendered,Is.True); Assert.That(ui.RenderedRowCount,Is.EqualTo(4));
            Assert.That(ui.FocusWeapon(3),Is.True); Assert.That(c.SelectedUnit.Energy,Is.EqualTo(40)); Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.SkillSelection));
            Assert.That(c.board.HighlightedCells,Is.Empty); Assert.That(ui.ConfirmFocused(),Is.True);
            Assert.That(c.SelectedSkill.Name,Is.EqualTo("무장 3")); Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.Attack));
            Assert.That(ui.IsRendered,Is.False); Assert.That(c.board.HighlightedCells,Is.Not.Empty);
        }
        [UnityTest] public IEnumerator 잔탄부족과_EN부족과_이동후제한을_안내하며_잘못된_결정을_막는다()
        {
            var c=Create(); yield return null; Open(c); var ui=c.WeaponMenu;
            ui.FocusWeapon(1); Assert.That(ui.UnavailableReason,Does.Contain("잔탄 없음")); Assert.That(ui.ConfirmFocused(),Is.False);
            ui.FocusWeapon(2); Assert.That(ui.UnavailableReason,Does.Contain("EN 부족")); Assert.That(ui.ConfirmFocused(),Is.False);
            ui.Cancel(); c.SetCommandMode(UnitCommandMode.Move); Assert.That(c.Session.TryMove(c.SelectedUnit,new Vector2Int(0,1)),Is.True);
            c.OpenSkillSelection(); ui.FocusWeapon(3); Assert.That(ui.UnavailableReason,Does.Contain("이동 후 사용 불가")); Assert.That(ui.ConfirmFocused(),Is.False);
            ui.FocusWeapon(0); Assert.That(ui.CanConfirm,Is.True); Assert.That(ui.FocusedWeapon.MaxAmmo,Is.Zero);
        }
        [UnityTest] public IEnumerator 목록_스크롤과_방향이동은_무장을_선택하며_취소와_바깥클릭은_맵행동을_하지않는다()
        {
            var c=Create(12); yield return null; Open(c); var ui=c.WeaponMenu; var cell=c.SelectedUnit.Cell;
            ui.FocusWeapon(11); ui.MoveFocus(1); Assert.That(ui.FocusedIndex,Is.Zero); ui.MoveFocus(-1); Assert.That(ui.FocusedIndex,Is.EqualTo(11));
            var rect=ui.ScreenRect; var point=rect.position+new Vector2(500,250)*WeaponSelectionView.ScaleFor(Screen.width,Screen.height);
            Assert.That(ui.HandlePointer(point,false,-120),Is.True); Assert.That(c.SelectedUnit.Cell,Is.EqualTo(cell)); Assert.That(c.SelectedUnit.HasMoved,Is.False);
            ui.Cancel(); Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.None)); Assert.That(c.CanShowUnitMenu,Is.True);
            c.OpenSkillSelection(); ui.HandlePointer(Vector2.zero,true);
            Assert.That(ui.IsRendered,Is.False); Assert.That(c.SelectedUnit,Is.Null); Assert.That(c.Session.Find("유닛0").HasActed,Is.False);
        }
        [UnityTest] public IEnumerator 빈무장과_선택변경과_턴종료와_비활성화는_안전하게_처리한다()
        {
            var empty=Create(0); yield return null; empty.RestartBattle(); empty.SelectUnit("유닛0");
            Assert.That(empty.CanOpenAttackMenu,Is.False); Assert.That(empty.OpenSkillSelection(),Is.False);
            Assert.That(empty.WeaponMenu,Is.Null); Assert.That(empty.CommandMode,Is.EqualTo(UnitCommandMode.None));
            var c=Create(); yield return null; Open(c); var ui=c.WeaponMenu;
            ui.Cancel(); c.OpenSkillSelection(); Assert.That(c.GetComponents<WeaponSelectionView>().Length,Is.EqualTo(1));
            c.Session.EndTurn(); yield return null; Assert.That(ui.IsRendered,Is.False);
            c.Session.EndTurn(); c.SelectUnit("유닛0"); c.OpenSkillSelection(); c.enabled=false;
            Assert.That(ui.IsRendered,Is.False); yield return null; Assert.That(ui.IsRendered,Is.False);
        }
        [UnityTest] public IEnumerator 최소사거리보다_가까운_적은_대상에서_제외하고_가능한_무장을_처음선택한다()
        {
            var c=Create();
            c.placements[0].character.mech.weapons[0].minRange=2;
            yield return null; Open(c); var ui=c.WeaponMenu;
            Assert.That(ui.FocusedIndex,Is.EqualTo(3));
            ui.FocusWeapon(0); Assert.That(ui.UnavailableReason,Does.Contain("사거리 내 대상 없음"));
            Assert.That(ui.CanConfirm,Is.False); Assert.That(ui.ConfirmFocused(),Is.False);
            Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.SkillSelection));
            Assert.That(c.SelectedUnit.Energy,Is.EqualTo(40)); Assert.That(c.SelectedUnit.HasActed,Is.False);
            ui.FocusWeapon(3); Assert.That(ui.CanConfirm,Is.True);
        }
        [UnityTest] public IEnumerator 최대사거리_경계는_선택가능하고_이동후에는_현재위치로_대상을_다시판단한다()
        {
            var c=Create(); c.placements[1].cell=new Vector2Int(3,0);
            c.placements[0].character.mech.weapons[0].minRange=3;
            yield return null; Open(c); var ui=c.WeaponMenu;
            Assert.That(ui.FocusedIndex,Is.Zero); Assert.That(ui.CanConfirm,Is.True);
            ui.Cancel(); Assert.That(c.Session.TryMove(c.SelectedUnit,Vector2Int.right),Is.True);
            Assert.That(c.CanOpenAttackMenu,Is.False); Assert.That(c.OpenSkillSelection(),Is.False);
            Assert.That(ui.IsRendered,Is.False); Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.None));
        }
        [UnityTest] public IEnumerator 격추된_적과_아군만_사거리안에_있으면_모든무장의_결정을_막는다()
        {
            var c=Create(); c.placements[0].character.mech.weapons[0].power=999;
            c.placements.Add(new UnitPlacement {id="먼 적",character=c.placements[1].character,cell=new Vector2Int(7,7)});
            yield return null; Open(c); var ally=c.SelectedUnit; var enemy=c.Session.Find("유닛1");
            c.WeaponMenu.Cancel(); Assert.That(c.Session.TryAttack(ally,enemy,ally.Skills[0]),Is.True);
            Assert.That(enemy.IsAlive,Is.False);
            if(c.Session.ActiveFaction==Faction.Enemy) c.Session.EndTurn();
            Assert.That(c.SelectUnit("유닛0"),Is.True); Assert.That(c.CanOpenAttackMenu,Is.False);
            Assert.That(c.OpenSkillSelection(),Is.False); Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.None));
            Assert.That(c.WeaponMenu.IsRendered,Is.False); Assert.That(ally.HasActed,Is.False);
        }
        [UnityTest] public IEnumerator 사거리_EN_잔탄_이동제한을_모두_만족하는_무장이_있을때만_공격메뉴가_활성화된다()
        {
            var c=Create(); c.placements[0].character.mech.weapons[0].maxAmmo=1;
            c.placements[0].character.mech.weapons[0].currentAmmo=0;
            yield return null; Open(c); var ally=c.SelectedUnit;
            Assert.That(c.CanOpenAttackMenu,Is.True); // 이동 전에는 네 번째 무장만 사용 가능
            c.WeaponMenu.Cancel(); Assert.That(c.Session.TryMove(ally,new Vector2Int(0,1)),Is.True);
            Assert.That(c.CanShowUnitMenu,Is.True); Assert.That(c.CanOpenAttackMenu,Is.False);
            Assert.That(c.OpenSkillSelection(),Is.False);
            Assert.That(c.SetCommandMode(UnitCommandMode.SkillSelection),Is.False);
            Assert.That(c.CommandMode,Is.EqualTo(UnitCommandMode.None)); Assert.That(ally.HasActed,Is.False);
            Assert.That(c.SetCommandMode(UnitCommandMode.Move),Is.False);
            Assert.That(c.WaitSelectedUnit(),Is.True);
        }
        [UnityTest] public IEnumerator 반격도_같은창을_쓰며_취소는_대응을_보존하고_결정은_확인으로_넘긴다()
        {
            var c=Create();
            var attack=New<SkillDefinition>(); attack.maxRange=3; attack.power=1;
            c.placements[1].character.mech.weapons.Add(attack);
            c.placements[0].character.mech.weapons[0].maxRange=1;
            c.RequireConfirmation=true; yield return null;
            Assert.That(c.RestartBattle(),Is.True);
            var ally=c.Session.Find("유닛0"); var enemy=c.Session.Find("유닛1");
            Assert.That(c.Session.TryMove(ally,new Vector2Int(0,1)),Is.True);
            Assert.That(c.Session.TryWait(ally),Is.True);
            if(c.Session.ActiveFaction==Faction.Ally) c.Session.EndTurn();
            Assert.That(c.Session.TryAttack(enemy,ally,enemy.Skills[0]),Is.True);
            var incoming=c.Session.PendingAttack;
            Assert.That(c.SelectedUnit,Is.Null);
            Assert.That(c.OpenCounterWeaponSelection(),Is.True);
            var ui=c.WeaponMenu;
            Assert.That(ui.IsRendered,Is.True); Assert.That(ui.RenderedRowCount,Is.EqualTo(4));
            ui.FocusWeapon(0); Assert.That(ui.UnavailableReason,Does.Contain("반격 사거리 밖")); Assert.That(ui.CanConfirm,Is.False);
            ui.FocusWeapon(1); Assert.That(ui.UnavailableReason,Does.Contain("잔탄 없음")); Assert.That(ui.ConfirmFocused(),Is.False);
            ui.FocusWeapon(2); Assert.That(ui.UnavailableReason,Does.Contain("EN 부족")); Assert.That(ui.CanConfirm,Is.False);
            ui.FocusWeapon(3); Assert.That(ui.CanConfirm,Is.True); Assert.That(ui.UnavailableReason,Is.Empty);
            ui.Cancel(); Assert.That(c.Session.PendingAttack,Is.SameAs(incoming)); Assert.That(c.Session.IsAwaitingConfirmation,Is.False);
            Assert.That(ui.IsRendered,Is.False);
            c.OpenCounterWeaponSelection(); ui.HandlePointer(Vector2.zero,true);
            Assert.That(c.Session.PendingAttack,Is.SameAs(incoming)); Assert.That(c.IsCounterWeaponSelectionOpen,Is.False);
            c.OpenCounterWeaponSelection(); ui.FocusWeapon(3);
            Assert.That(ui.ConfirmFocused(),Is.True); Assert.That(ui.IsRendered,Is.False);
            Assert.That(c.Session.IsAwaitingConfirmation,Is.True);
            Assert.That(c.Session.PendingAttack.Decision.CounterSkill,Is.SameAs(ally.Skills[3]));
            Assert.That(ally.Energy,Is.EqualTo(40)); Assert.That(ally.Hp,Is.EqualTo(100));
            c.CancelCombat(); c.OpenCounterWeaponSelection();
            Assert.That(ui.IsRendered,Is.True); c.ChooseReaction(ReactionChoice.Defend);
            Assert.That(ui.IsRendered,Is.False);
        }
        [UnityTest] public IEnumerator 등록한_포트레이트와_무장이미지를_표시하고_HP와_EN색을_유지한다()
        {
            var c=Create(); var texture=new Texture2D(8,8); owned.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f); owned.Add(sprite);
            c.placements[0].character.pilot.portraits.Add(new CharacterPortrait {sprite=sprite});
            c.placements[0].character.mech.weapons[0].icon=sprite;
            yield return null; Open(c); var ui=c.WeaponMenu;
            Assert.That(ui.DisplayedPortrait,Is.SameAs(sprite)); Assert.That(ui.FocusedWeapon.Icon,Is.SameAs(sprite));
            Assert.That(ui.HpColor.r,Is.GreaterThan(ui.HpColor.b)); Assert.That(ui.EnergyColor.b,Is.GreaterThan(ui.EnergyColor.r));
            Assert.That(ui.ScreenRect.center.x,Is.EqualTo(Screen.width/2f).Within(1)); Assert.That(ui.ScreenRect.center.y,Is.EqualTo(Screen.height/2f).Within(1));
        }
    }
}
