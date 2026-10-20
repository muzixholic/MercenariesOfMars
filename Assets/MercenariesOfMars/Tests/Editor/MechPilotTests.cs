using System;
using System.Collections.Generic;
using System.Reflection;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MercenariesOfMars.Tests
{
    public sealed class MechPilotTests
    {
        readonly List<Object> owned=new();
        string folder;
        T New<T>() where T:ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        CharacterDefinition Assignment()
        {
            var c=New<CharacterDefinition>(); c.mech=New<MechDefinition>(); c.pilot=New<PilotDefinition>();
            c.mech.displayName="시험 기체"; c.mech.maxHp=1200; c.mech.maxEnergy=45; c.mech.movement=7;
            c.mech.armor=30; c.mech.targeting=40; c.mech.mobility=50; c.mech.price=12000;
            c.mech.movementEnvironments=MovementEnvironment.Ground|MovementEnvironment.Space;
            c.pilot.displayName="시험 파일럿"; c.pilot.level=8; c.pilot.attack=21; c.pilot.defense=11; c.pilot.accuracy=150; c.pilot.evasion=130;
            c.mech.weapons.Add(New<SkillDefinition>()); return c;
        }
        [TearDown] public void Cleanup()
        {
            if(!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            foreach(var obj in owned) if(obj!=null) Object.DestroyImmediate(obj); owned.Clear(); folder=null;
        }
        [Test] public void 기체와_파일럿의_값을_분리해서_읽고_장갑과_조준을_합산하지_않는다()
        {
            var c=Assignment(); var u=new BattleUnit("유닛",c,Vector2Int.zero);
            Assert.That(u.Name,Is.EqualTo("시험 기체")); Assert.That(u.PilotName,Is.EqualTo("시험 파일럿")); Assert.That(u.Level,Is.EqualTo(8));
            Assert.That(u.MaxHp,Is.EqualTo(1200)); Assert.That(u.MaxEnergy,Is.EqualTo(45)); Assert.That(u.Movement,Is.EqualTo(7));
            Assert.That(u.Attack,Is.EqualTo(21)); Assert.That(u.Defense,Is.EqualTo(11)); Assert.That(u.Accuracy,Is.EqualTo(150)); Assert.That(u.Evasion,Is.EqualTo(130));
            Assert.That(u.Armor,Is.EqualTo(30)); Assert.That(u.Targeting,Is.EqualTo(40)); Assert.That(u.Mobility,Is.EqualTo(50)); Assert.That(u.Price,Is.EqualTo(12000));
            Assert.That(u.MovementEnvironments,Is.EqualTo(MovementEnvironment.Ground|MovementEnvironment.Space));
        }
        [Test] public void 동일파일럿을_다른기체에_태워도_시작한전투의_스냅샷은_변하지_않는다()
        {
            var c=Assignment(); var first=new BattleUnit("첫 편성",c,Vector2Int.zero);
            var other=New<MechDefinition>(); other.maxHp=3000; c.mech=other; c.pilot.level=20;
            var second=new BattleUnit("둘째 편성",c,Vector2Int.right);
            Assert.That(first.MaxHp,Is.EqualTo(1200)); Assert.That(first.Level,Is.EqualTo(8));
            Assert.That(second.MaxHp,Is.EqualTo(3000)); Assert.That(second.Level,Is.EqualTo(20)); Assert.That(first.Attack,Is.EqualTo(second.Attack));
        }
        [Test] public void 경험치는_누적값을_보존하고_레벨을_자동변경하지_않는다()
        {
            var c=Assignment(); c.pilot.experience=12345;
            var unit=new BattleUnit("경험치",c,Vector2Int.zero);
            c.pilot.experience=99999;
            Assert.That(unit.Experience,Is.EqualTo(12345)); Assert.That(unit.Level,Is.EqualTo(8)); Assert.That(c.pilot.level,Is.EqualTo(8));
            c.pilot.experience=-1; Assert.That(new BattleUnit("최솟값",c,Vector2Int.zero).Experience,Is.Zero);
            typeof(PilotDefinition).GetMethod("OnValidate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c.pilot,null);
            Assert.That(c.pilot.experience,Is.Zero);
        }
        [Test] public void 무장은_기체에서_가져오고_기존편성의_스킬목록과_섞지_않는다()
        {
            var c=Assignment(); var old=New<SkillDefinition>(); c.skills.Add(old);
            var u=new BattleUnit("유닛",c,Vector2Int.zero);
            Assert.That(u.Skills,Has.Count.EqualTo(1)); Assert.That(u.Skills[0].Definition,Is.SameAs(c.mech.weapons[0]));
            c.mech.weapons.Clear(); Assert.That(u.Skills,Has.Count.EqualTo(1));
            Assert.That(new BattleUnit("새 전투",c,Vector2Int.zero).Skills,Is.Empty);
        }
        [Test] public void 파일럿_화자는_전투배치없이_선택하고_이름과_표정을_유지한다()
        {
            var c=Assignment(); var p=new CharacterPortrait { label="웃음" }; c.pilot.portraits.Add(p);
            var command=new EventCommand { kind=EventCommandKind.Dialogue,speakerPilot=c.pilot,portraitId=p.id,speaker="안내" };
            var sequence=New<EventSequence>(); sequence.commands.Add(command);
            Assert.That(sequence.ValidateCommands(),Is.Empty); Assert.That(command.ResolvedSpeaker,Is.EqualTo(c.pilot.displayName));
            c.pilot.portraits.Insert(0,new CharacterPortrait()); Assert.That(command.SpeakerPortraits[1].id,Is.EqualTo(p.id));
            c.pilot.portraits.RemoveAt(1); Assert.That(sequence.ValidateCommands(),Has.Count.EqualTo(1));
            command.speakerPilot=null; Assert.That(command.ResolvedSpeaker,Is.EqualTo("안내")); Assert.That(sequence.ValidateCommands(),Is.Empty);
        }
        [Test] public void 기존캐릭터_화자연결도_분리된_파일럿을_참조한다()
        {
            var c=Assignment(); var p=new CharacterPortrait(); c.pilot.portraits.Add(p);
            var command=new EventCommand { speakerCharacter=c,portraitId=p.id };
            Assert.That(command.ResolvedSpeaker,Is.EqualTo(c.pilot.displayName)); Assert.That(command.SpeakerPortraits,Is.SameAs(c.pilot.portraits));
        }
        [Test] public void 기체와_파일럿_중_하나만_있는_전투편성은_검증에서_거절한다()
        {
            var c=New<CharacterDefinition>(); c.mech=New<MechDefinition>();
            Assert.That(c.ValidateAssignment(),Has.Count.EqualTo(1));
            Assert.Throws<ArgumentException>(()=>new BattleUnit("누락",c,Vector2Int.zero));
            var map=New<BattleMapDefinition>(); map.units.Add(new UnitPlacement { id="누락",character=c });
            Assert.That(map.ValidateMap(),Has.Some.Contains("파일럿"));
        }
        [Test] public void 무장_추가와_삭제는_기존_항목을_복제하거나_원본을_삭제하지_않는다()
        {
            var c=Assignment(); var weapon=c.mech.weapons[0]; var editor=UnityEditor.Editor.CreateEditor(c.mech,typeof(MechDefinitionEditor)); owned.Add(editor);
            var list=(ReorderableList)typeof(MechDefinitionEditor).GetField("weapons",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(editor);
            editor.serializedObject.Update(); list.onAddCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(c.mech.weapons[1],Is.Null);
            editor.serializedObject.Update(); list.index=0; list.onRemoveCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(c.mech.weapons,Has.Count.EqualTo(1)); Assert.That(weapon!=null,Is.True);
        }
        [Test] public void 파일럿_표정추가는_고유ID를_사용하고_이미지는_비운다()
        {
            var c=Assignment(); c.pilot.portraits.Add(new CharacterPortrait()); var id=c.pilot.portraits[0].id;
            var editor=UnityEditor.Editor.CreateEditor(c.pilot,typeof(PilotDefinitionEditor)); owned.Add(editor);
            var list=(ReorderableList)typeof(PilotDefinitionEditor).GetField("portraits",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(editor);
            editor.serializedObject.Update(); list.onAddCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(c.pilot.portraits[1].id,Is.Not.EqualTo(id)); Assert.That(c.pilot.portraits[1].sprite,Is.Null);
        }
        [Test] public void 기존에셋_분리는_값과_ID와_참조를_보존하고_재실행해도_중복생성하지_않는다()
        {
            folder="Assets/기체파일럿분리검증_"+Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var c=New<CharacterDefinition>(); c.displayName="보존 캐릭터"; c.maxHp=1087; c.maxEnergy=23; c.movement=6;
            c.level=14; c.attack=35; c.defense=16; c.accuracy=92; c.evasion=17; c.portraits.Add(new CharacterPortrait {label="기본"});
            var skill=New<SkillDefinition>(); AssetDatabase.CreateAsset(skill,folder+"/무장.asset"); c.skills.Add(skill); AssetDatabase.CreateAsset(c,folder+"/편성.asset");
            var before=new BattleUnit("이전",c,Vector2Int.zero); string portraitId=c.portraits[0].id;
            CharacterSplitMigration.Split(c); var mech=c.mech; var pilot=c.pilot; var after=new BattleUnit("이후",c,Vector2Int.zero);
            Assert.That(after.MaxHp,Is.EqualTo(before.MaxHp)); Assert.That(after.MaxEnergy,Is.EqualTo(before.MaxEnergy)); Assert.That(after.Movement,Is.EqualTo(before.Movement));
            Assert.That(after.Attack,Is.EqualTo(before.Attack)); Assert.That(after.Defense,Is.EqualTo(before.Defense)); Assert.That(after.Accuracy,Is.EqualTo(before.Accuracy)); Assert.That(after.Evasion,Is.EqualTo(before.Evasion));
            Assert.That(after.Level,Is.EqualTo(before.Level)); Assert.That(after.Skills[0].Definition,Is.SameAs(skill)); Assert.That(pilot.portraits[0].id,Is.EqualTo(portraitId));
            c.portraits[0].label="이전 표정"; Assert.That(pilot.portraits[0].label,Is.EqualTo("기본"));
            CharacterSplitMigration.Split(c); Assert.That(c.mech,Is.SameAs(mech)); Assert.That(c.pilot,Is.SameAs(pilot));
            Assert.That(AssetDatabase.FindAssets("t:MechDefinition",new[]{folder}),Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.FindAssets("t:PilotDefinition",new[]{folder}),Has.Length.EqualTo(1));
            Undo.ClearUndo(c); Undo.ClearUndo(mech); Undo.ClearUndo(pilot);
        }
    }
}
