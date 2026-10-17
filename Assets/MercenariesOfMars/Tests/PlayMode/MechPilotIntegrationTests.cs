using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class MechPilotIntegrationTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        BattleController Create()
        {
            var root=new GameObject("기체 파일럿 통합 검증"); owned.Add(root); root.SetActive(false);
            var c=root.AddComponent<BattleController>(); c.board=root.AddComponent<GridBoard>(); c.battleCamera=root.AddComponent<Camera>();
            c.eventRunner=root.AddComponent<EventRunner>(); c.automaticEnemy=false; c.enableCombatPresentation=false; c.RequireConfirmation=false; c.showControls=false;
            for(int i=0;i<2;i++)
            {
                var assignment=New<CharacterDefinition>(); assignment.faction=i==0?Faction.Ally:Faction.Enemy;
                assignment.mech=New<MechDefinition>(); assignment.mech.displayName="기체"+i; assignment.mech.maxHp=100; assignment.mech.maxEnergy=40; assignment.mech.armor=999;
                assignment.pilot=New<PilotDefinition>(); assignment.pilot.displayName="파일럿"+i; assignment.pilot.level=9; assignment.pilot.attack=15; assignment.pilot.defense=7; assignment.pilot.accuracy=999;
                var weapon=New<SkillDefinition>(); weapon.power=20; weapon.energyCost=5; assignment.mech.weapons.Add(weapon);
                c.placements.Add(new UnitPlacement { id="유닛"+i,character=assignment,cell=new Vector2Int(i,0) });
            }
            root.SetActive(true); return c;
        }
        [UnityTearDown] public IEnumerator Cleanup() { foreach(var item in owned) if(item!=null) Object.Destroy(item); owned.Clear(); yield return null; }
        [UnityTest] public IEnumerator 전투는_기체의_HP와_EN과_무장_파일럿의_기존능력치를_사용한다()
        {
            var c=Create(); yield return null; Assert.That(c.RestartBattle(),Is.True);
            var ally=c.Session.Find("유닛0"); var enemy=c.Session.Find("유닛1");
            Assert.That(ally.Name,Is.EqualTo("기체0")); Assert.That(ally.PilotName,Is.EqualTo("파일럿0")); Assert.That(ally.Hp,Is.EqualTo(100)); Assert.That(ally.Energy,Is.EqualTo(40));
            Assert.That(c.Session.GetDamage(ally,enemy,ally.Skills[0]),Is.EqualTo(28),"장갑치 999를 새 계산식으로 합산하면 안 됩니다.");
            Assert.That(c.Session.TryAttack(ally,enemy),Is.True); Assert.That(enemy.Hp,Is.EqualTo(72)); Assert.That(ally.Energy,Is.EqualTo(35));
            c.Session.ChangeHp("유닛1",-999); var otherPilot=New<PilotDefinition>(); otherPilot.displayName="교체 파일럿"; otherPilot.level=20;
            c.placements[0].character.pilot=otherPilot; c.RestartBattle();
            Assert.That(c.Session.Find("유닛0").PilotName,Is.EqualTo("교체 파일럿")); Assert.That(c.Session.Find("유닛0").Level,Is.EqualTo(20)); Assert.That(c.Session.Find("유닛0").Hp,Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator 파일럿_단독화자를_재생하고_수동화자로_전환한다()
        {
            var c=Create(); yield return null; c.RestartBattle();
            var npc=New<PilotDefinition>(); npc.displayName="정비사";
            var sequence=New<EventSequence>(); sequence.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speakerPilot=npc,text="출격 준비 완료." });
            sequence.commands.Add(new EventCommand { kind=EventCommandKind.Dialogue,speaker="통신",text="발진합니다." });
            Assert.That(c.eventRunner.Play(sequence,c.Session),Is.True); Assert.That(c.eventRunner.Speaker,Is.EqualTo("정비사"));
            c.eventRunner.ContinueDialogue(); yield return null; yield return null;
            Assert.That(c.eventRunner.Speaker,Is.EqualTo("통신")); Assert.That(c.eventRunner.Portrait,Is.Null);
            c.eventRunner.StopPlayback();
        }
        [UnityTest] public IEnumerator 적군은_출격평균과_프리셋으로_레벨과_능력치를_계산하고_전투중에는_고정한다()
        {
            var c=Create(); var growth=New<PilotGrowthPreset>();
            foreach(var p in c.placements) { p.character.pilot.attackGrowth=growth; p.character.pilot.attackAt99=211; }
            c.placements[0].character.pilot.level=20; c.placements[1].character.pilot.level=7;
            var settings=New<PilotProgressionSettings>(); settings.defaultEnemyLevel=New<EnemyLevelPreset>(); settings.defaultEnemyLevel.multiplier=1.1f; settings.defaultEnemyLevel.offset=2; settings.defaultEnemyLevel.experienceMultiplier=1.5f; settings.defaultEnemyLevel.experienceOffset=2; c.progressionSettings=settings;
            yield return null; Assert.That(c.RestartBattle(),Is.True);
            var enemy=c.Session.Find("유닛1"); Assert.That(enemy.Level,Is.EqualTo(24)); Assert.That(enemy.Attack,Is.EqualTo(61));
            Assert.That(enemy.CalculateDefeatExperience(100),Is.EqualTo(152));
            Assert.That(enemy.Hp,Is.EqualTo(100)); Assert.That(enemy.MaxEnergy,Is.EqualTo(40)); Assert.That(c.placements[1].character.pilot.level,Is.EqualTo(7));
            settings.defaultEnemyLevel.experienceMultiplier=2;
            Assert.That(enemy.CalculateDefeatExperience(100),Is.EqualTo(152));
            c.placements[0].character.pilot.level=90; c.Session.EndTurn(); Assert.That(enemy.Level,Is.EqualTo(24));
            c.Session.ChangeHp("유닛1",-999); Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Session.Find("유닛1").CalculateDefeatExperience(100),Is.EqualTo(202));
            Assert.That(c.Session.Find("유닛1").Level,Is.EqualTo(99)); Assert.That(enemy.Level,Is.EqualTo(24));
        }
        [UnityTest] public IEnumerator 적군_자동레벨을_끄거나_개별프리셋으로_교체할_수_있다()
        {
            var c=Create(); c.placements[0].character.pilot.level=20; c.placements[1].character.pilot.level=7; c.placements[1].autoEnemyLevel=false;
            yield return null; c.RestartBattle(); Assert.That(c.Session.Find("유닛1").Level,Is.EqualTo(7));
            c.Session.ChangeHp("유닛1",-999); c.placements[1].autoEnemyLevel=true;
            c.placements[1].enemyLevelPreset=New<EnemyLevelPreset>(); c.placements[1].enemyLevelPreset.offset=5;
            c.RestartBattle(); Assert.That(c.Session.Find("유닛1").Level,Is.EqualTo(25));
        }
        [UnityTest] public IEnumerator 무장잔탄은_전투와_재시작에_반영되고_빈무장만_있으면_적이_대기한다()
        {
            var c=Create(); var definition=c.placements[0].character.mech.weapons[0]; definition.maxAmmo=3; definition.currentAmmo=1; definition.weight=10;
            c.placements[0].character.mech.maxWeaponWeight=10;
            var enemyWeapon=c.placements[1].character.mech.weapons[0]; enemyWeapon.maxAmmo=2; enemyWeapon.currentAmmo=0;
            yield return null; Assert.That(c.RestartBattle(),Is.True);
            var ally=c.Session.Find("유닛0"); var enemy=c.Session.Find("유닛1");
            Assert.That(c.Session.TryAttack(ally,enemy,ally.Skills[0]),Is.True);
            Assert.That(ally.Skills[0].CurrentAmmo,Is.Zero); Assert.That(ally.WeaponWeight,Is.EqualTo(10)); Assert.That(ally.MaxWeaponWeight,Is.EqualTo(10));
            typeof(BattleController).GetMethod("EnemyStep",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(c,null);
            Assert.That(c.Session.ActiveFaction,Is.EqualTo(Faction.Ally)); Assert.That(ally.Hp,Is.EqualTo(100));
            Assert.That(c.Session.CanSelectSkill(ally,ally.Skills[0]),Is.False);
            c.Session.ChangeHp("유닛1",-999); Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Session.Find("유닛0").Skills[0].CurrentAmmo,Is.EqualTo(1)); Assert.That(definition.currentAmmo,Is.EqualTo(1));
        }
    }
}
