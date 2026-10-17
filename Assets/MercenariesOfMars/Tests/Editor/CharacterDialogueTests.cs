using System.Collections.Generic;
using System.Reflection;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class CharacterDialogueTests
    {
        readonly List<Object> owned = new();
        T New<T>() where T : ScriptableObject { var data=ScriptableObject.CreateInstance<T>(); owned.Add(data); return data; }
        Sprite Sprite()
        {
            var texture=new Texture2D(8,8); owned.Add(texture);
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(.5f,.5f)); owned.Add(sprite); return sprite;
        }
        [TearDown] public void Cleanup() { foreach (var obj in owned) if (obj != null) Object.DestroyImmediate(obj); owned.Clear(); }
        [Test] public void 캐릭터_화자는_최신이름을_사용하고_직접입력한_이름은_보존한다()
        {
            var character=New<CharacterDefinition>(); character.displayName="기사";
            var c=new EventCommand { speaker="직접 입력",speakerCharacter=character };
            Assert.That(c.ResolvedSpeaker,Is.EqualTo("기사"));
            character.displayName="성기사"; Assert.That(c.ResolvedSpeaker,Is.EqualTo("성기사"));
            c.speakerCharacter=null; Assert.That(c.ResolvedSpeaker,Is.EqualTo("직접 입력")); Assert.That(c.ResolvedPortrait,Is.Null);
        }
        [Test] public void 포트레이트의_순서와_이름을_바꿔도_선택을_유지한다()
        {
            var character=New<CharacterDefinition>(); var first=new CharacterPortrait { sprite=Sprite(),label="기본" }; var second=new CharacterPortrait { sprite=Sprite(),label="웃음" };
            character.portraits.Add(first); character.portraits.Add(second);
            var c=new EventCommand { speakerCharacter=character,portraitId=second.id };
            character.portraits.Reverse(); second.label="미소";
            Assert.That(c.ResolvedPortrait,Is.SameAs(second.sprite));
            c.portraitId=""; Assert.That(c.ResolvedPortrait,Is.Null);
        }
        [Test] public void 삭제된_선택은_검증하고_화자없는_기존대사는_허용한다()
        {
            var character=New<CharacterDefinition>(); var sequence=New<EventSequence>();
            var c=new EventCommand { kind=EventCommandKind.Dialogue,speakerCharacter=character,portraitId="삭제된 표정",speaker="안내" }; sequence.commands.Add(c);
            Assert.That(sequence.ValidateCommands(),Has.Count.EqualTo(1));
            c.speakerCharacter=null; Assert.That(sequence.ValidateCommands(),Is.Empty);
            Assert.That(c.ResolvedSpeaker,Is.EqualTo("안내"));
        }
        [Test] public void 레벨과_맵아이콘은_전투시작값을_복사한다()
        {
            var character=New<CharacterDefinition>(); character.level=12; character.mapIcon=Sprite();
            var unit=new BattleUnit("기사",character,Vector2Int.zero); character.level=99; character.mapIcon=null;
            Assert.That(unit.Level,Is.EqualTo(12)); Assert.That(unit.MapIcon,Is.Not.Null);
            character.level=0; Assert.That(new BattleUnit("최솟값",character,Vector2Int.zero).Level,Is.EqualTo(1));
        }
        [Test] public void 포트레이트_추가는_이전이미지와_ID를_복제하지_않는다()
        {
            var character=New<CharacterDefinition>(); character.portraits.Add(new CharacterPortrait { sprite=Sprite() }); string original=character.portraits[0].id;
            var editor=UnityEditor.Editor.CreateEditor(character,typeof(CharacterDefinitionEditor)); owned.Add(editor);
            var list=(ReorderableList)typeof(CharacterDefinitionEditor).GetField("portraits",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
            editor.serializedObject.Update(); list.onAddCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(character.portraits[1].id,Is.Not.EqualTo(original)); Assert.That(character.portraits[1].sprite,Is.Null);
            editor.serializedObject.Update(); list.serializedProperty.MoveArrayElement(1,0); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(character.portraits[1].id,Is.EqualTo(original));
            editor.serializedObject.Update(); list.index=0; ReorderableList.defaultBehaviours.DoRemoveButton(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(character.portraits,Has.Count.EqualTo(1)); Assert.That(character.portraits[0].id,Is.EqualTo(original));
        }
        [Test] public void 포트레이트_복제시_중복ID를_정리하고_첫선택은_보존한다()
        {
            var character=New<CharacterDefinition>(); var first=new CharacterPortrait(); character.portraits.Add(first);
            character.portraits.Add(new CharacterPortrait { id=first.id }); string id=first.id;
            typeof(CharacterDefinition).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(character,null);
            Assert.That(character.portraits[0].id,Is.EqualTo(id)); Assert.That(character.portraits[1].id,Is.Not.EqualTo(id));
        }
    }
}
