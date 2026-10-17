using System;
using System.Collections.Generic;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class WeaponResourceTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject { var o=ScriptableObject.CreateInstance<T>(); owned.Add(o); return o; }
        SkillDefinition Weapon(int max=2,int current=2,int weight=10)
        { var s=New<SkillDefinition>(); s.maxAmmo=max; s.currentAmmo=current; s.weight=weight; return s; }
        CharacterDefinition Definition(Faction faction,params SkillDefinition[] weapons)
        {
            var d=New<CharacterDefinition>(); d.faction=faction; d.mech=New<MechDefinition>(); d.mech.maxHp=1000; d.mech.maxEnergy=100;
            d.mech.weapons.AddRange(weapons); d.pilot=New<PilotDefinition>(); d.pilot.accuracy=999; return d;
        }
        BattleSession Battle(SkillDefinition weapon,out BattleUnit ally,out BattleUnit enemy,bool confirm=false,SkillDefinition enemyWeapon=null)
        {
            ally=new BattleUnit("아군",Definition(Faction.Ally,weapon),Vector2Int.zero);
            enemy=new BattleUnit("적군",Definition(Faction.Enemy,enemyWeapon),Vector2Int.right);
            return new BattleSession(new BattleGrid(5,5),new[]{ally,enemy},()=>99,confirm);
        }
        [TearDown] public void Cleanup() { foreach(var o in owned) if(o!=null) Object.DestroyImmediate(o); owned.Clear(); }
        [Test] public void 마지막탄을_쓰면_다음공격은_금지하고_턴이_바뀌어도_복구하지_않는다()
        {
            var b=Battle(Weapon(3,1),out var a,out var e); var s=a.Skills[0];
            Assert.That(b.TryAttack(a,e,s),Is.True); Assert.That(s.CurrentAmmo,Is.Zero);
            b.TryWait(e); Assert.That(b.CanSelectSkill(a,s),Is.False); Assert.That(b.TryAttack(a,e,s),Is.False); Assert.That(a.HasActed,Is.False);
        }
        [Test] public void 최대탄수0은_현재탄수와_무관하게_탄수를_소모하지_않는다()
        {
            var b=Battle(Weapon(0,50),out var a,out var e); var s=a.Skills[0];
            for(int i=0;i<4;i++) { Assert.That(b.TryAttack(a,e,s),Is.True); b.TryWait(e); }
            Assert.That(s.HasAmmo,Is.True); Assert.That(s.CurrentAmmo,Is.Zero); Assert.That(s.AmmoText,Is.EqualTo("탄수 제한 없음"));
        }
        [Test] public void 잔탄은_공유에셋을_쓴_유닛끼리_독립적이며_재생성시_초기값을_사용한다()
        {
            var source=Weapon(5,2); var b=Battle(source,out var a,out var e,enemyWeapon:source);
            b.TryAttack(a,e,a.Skills[0]);
            Assert.That(a.Skills[0].CurrentAmmo,Is.EqualTo(1)); Assert.That(e.Skills[0].CurrentAmmo,Is.EqualTo(2)); Assert.That(source.currentAmmo,Is.EqualTo(2));
            source.maxAmmo=99; source.weight=50;
            Assert.That(a.Skills[0].MaxAmmo,Is.EqualTo(5)); Assert.That(a.Skills[0].Weight,Is.EqualTo(10));
            Assert.That(new BattleUnit("재시작",Definition(Faction.Ally,source),Vector2Int.zero).Skills[0].CurrentAmmo,Is.EqualTo(2));
        }
        [Test] public void 전투취소와_선택은_소모하지_않고_확정하면_소모한다()
        {
            var b=Battle(Weapon(),out var a,out var e,true); var s=a.Skills[0];
            Assert.That(b.TryAttack(a,e,s),Is.True); Assert.That(s.CurrentAmmo,Is.EqualTo(2));
            Assert.That(b.CancelCombat(),Is.True); Assert.That(s.CurrentAmmo,Is.EqualTo(2));
            b.TryAttack(a,e,s); Assert.That(b.ConfirmCombat(),Is.True); Assert.That(s.CurrentAmmo,Is.EqualTo(1));
        }
        [Test] public void 빗나간_공격도_탄수를_소모한다()
        {
            var ad=Definition(Faction.Ally,Weapon()); ad.pilot.accuracy=1;
            var ed=Definition(Faction.Enemy); ed.pilot.evasion=999;
            var a=new BattleUnit("아군",ad,Vector2Int.zero); var e=new BattleUnit("적군",ed,Vector2Int.right);
            var b=new BattleSession(new BattleGrid(4,4),new[]{a,e}); b.TryAttack(a,e,a.Skills[0]);
            Assert.That(b.LastAttack.Hit,Is.False); Assert.That(a.Skills[0].CurrentAmmo,Is.EqualTo(1));
        }
        [Test] public void 반격도_탄수를_소모하고_잔탄0_반격은_선택할_수_없다()
        {
            var b=Battle(Weapon(1,1),out var a,out var e,enemyWeapon:Weapon()); b.EndTurn();
            Assert.That(b.TryAttack(e,a,e.Skills[0]),Is.True); Assert.That(b.CanCounterattack(a.Skills[0]),Is.True);
            Assert.That(b.ResolveReaction(ReactionChoice.Counterattack,a.Skills[0]),Is.True);
            Assert.That(a.Skills[0].CurrentAmmo,Is.Zero); Assert.That(e.Skills[0].CurrentAmmo,Is.EqualTo(1));
            b.EndTurn(); b.TryAttack(e,a,e.Skills[0]); Assert.That(b.CanCounterattack(a.Skills[0]),Is.False);
            Assert.That(b.ResolveReaction(ReactionChoice.Counterattack,a.Skills[0]),Is.False);
            Assert.That(b.ResolveReaction(ReactionChoice.Defend),Is.True);
        }
        [Test] public void 공격에_격추된_유닛은_반격탄을_소모하지_않는다()
        {
            var main=Weapon(); main.power=9999;
            var b=Battle(Weapon(),out var a,out var e,enemyWeapon:main); b.EndTurn(); b.TryAttack(e,a,e.Skills[0]);
            b.ResolveReaction(ReactionChoice.Counterattack,a.Skills[0]); Assert.That(a.IsAlive,Is.False);
            Assert.That(a.Skills[0].CurrentAmmo,Is.EqualTo(2)); Assert.That(b.LastCounterattack,Is.Null);
        }
        [Test] public void 에너지나_사거리부족으로_실패하면_잔탄을_소모하지_않는다()
        {
            var source=Weapon(); source.minRange=source.maxRange=2;
            var b=Battle(source,out var a,out var e); Assert.That(b.TryAttack(a,e,a.Skills[0]),Is.False); Assert.That(a.Skills[0].CurrentAmmo,Is.EqualTo(2));
            source.minRange=1; source.energyCost=101;
            b=Battle(source,out a,out e); Assert.That(b.TryAttack(a,e,a.Skills[0]),Is.False); Assert.That(a.Skills[0].CurrentAmmo,Is.EqualTo(2));
        }
        [Test] public void 허용중량과_같으면_추가하고_초과하면_목록을_보존한다()
        {
            var m=New<MechDefinition>(); m.maxWeaponWeight=10;
            Assert.That(m.TryAddWeapon(Weapon(weight:10)),Is.True); Assert.That(m.TryAddWeapon(Weapon(weight:1)),Is.False);
            Assert.That(m.weapons.Count,Is.EqualTo(1)); Assert.That(m.TotalWeaponWeight,Is.EqualTo(10));
            m.maxWeaponWeight=0; m.weapons.Clear(); Assert.That(m.TryAddWeapon(Weapon(weight:0)),Is.True);
        }
        [Test] public void 같은무장은_중복추가를_거절하고_목록과_중량을_보존한다()
        {
            var m=New<MechDefinition>(); var s=Weapon(weight:10);
            Assert.That(m.TryAddWeapon(s),Is.True); Assert.That(m.TryAddWeapon(s),Is.False);
            Assert.That(m.weapons.Count,Is.EqualTo(1)); Assert.That(m.TotalWeaponWeight,Is.EqualTo(10));
            Assert.That(m.ValidateEquipment(),Is.Empty);
            Assert.That(m.TryAddWeapon(Weapon(weight:10)),Is.True);
        }
        [Test] public void 직접등록된_중복무장도_편성검증과_전투생성에서_막는다()
        {
            var s=Weapon(); var d=Definition(Faction.Ally,s,s);
            Assert.That(d.mech.ValidateEquipment(),Has.Some.Contains("같은 무장"));
            Assert.That(d.ValidateAssignment(),Is.Not.Empty);
            Assert.Throws<ArgumentException>(()=>new BattleUnit("중복",d,Vector2Int.zero));
        }
        [Test] public void 편집기는_저장전_중복을_막고_자기슬롯의_재선택과_해제를_허용한다()
        {
            var m=New<MechDefinition>(); var first=Weapon(); var second=Weapon();
            m.weapons.Add(first); m.weapons.Add(second);
            using var so=new SerializedObject(m);
            Assert.That(MechPilotLists.CanAssignWeapon(so,0,first),Is.True);
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,first),Is.False);
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,null),Is.True);
            // 아직 에셋에 적용하지 않은 다른 슬롯 변경도 검사한다.
            so.FindProperty("weapons").GetArrayElementAtIndex(0).objectReferenceValue=second;
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,second),Is.False);
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,first),Is.True);
        }
        [Test] public void 공유무장_무게변경으로_초과하면_편성검증과_전투생성에서_막는다()
        {
            var s=Weapon(weight:10); var d=Definition(Faction.Ally,s); d.mech.maxWeaponWeight=10; s.weight=11;
            Assert.That(d.ValidateAssignment(),Is.Not.Empty); Assert.Throws<ArgumentException>(()=>new BattleUnit("초과",d,Vector2Int.zero));
            d.mech.weapons.Clear(); Assert.That(d.ValidateAssignment(),Is.Empty);
        }
        [Test] public void 총중량은_정수범위를_넘어도_안전하게_합산한다()
        {
            var m=New<MechDefinition>(); m.maxWeaponWeight=int.MaxValue;
            m.weapons.Add(Weapon(weight:int.MaxValue)); m.weapons.Add(Weapon(weight:int.MaxValue));
            Assert.That(m.TotalWeaponWeight,Is.EqualTo(2L*int.MaxValue)); Assert.That(m.ValidateEquipment(),Is.Not.Empty);
        }
        [Test] public void 편집기는_저장전_허용중량과_교체대상_무게를_사용한다()
        {
            var m=New<MechDefinition>(); m.weapons.Add(Weapon(weight:6)); m.weapons.Add(Weapon(weight:4));
            using var so=new SerializedObject(m); so.FindProperty("maxWeaponWeight").intValue=10;
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,Weapon(weight:4)),Is.True);
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,Weapon(weight:5)),Is.False);
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,null),Is.True);
            so.FindProperty("maxWeaponWeight").intValue=11;
            Assert.That(MechPilotLists.CanAssignWeapon(so,1,Weapon(weight:5)),Is.True);
        }
        [Test] public void 탄수와_무게의_음수및_잔탄초과를_검출한다()
        {
            var s=Weapon(3,4,-1); Assert.That(s.ValidateSkill().Count,Is.EqualTo(2));
            s.maxAmmo=0; s.currentAmmo=999; s.weight=0; Assert.That(s.ValidateSkill(),Is.Empty);
        }
    }
}
