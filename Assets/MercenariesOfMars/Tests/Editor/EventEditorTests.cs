using System.Reflection;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class EventEditorTests
    {
        EventSequence sequence;
        UnityEditor.Editor editor;
        ReorderableList list;
        [SetUp] public void Setup()
        {
            sequence = ScriptableObject.CreateInstance<EventSequence>();
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.BranchFlag, flag = "검증", jumpTo = 2 });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Wait });
            sequence.commands.Add(new EventCommand { kind = EventCommandKind.Stop });
            editor = UnityEditor.Editor.CreateEditor(sequence, typeof(EventSequenceEditor));
            list = (ReorderableList)typeof(EventSequenceEditor).GetField("list", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(editor); Object.DestroyImmediate(sequence);
        }
        [Test] public void 순서변경시_분기목적지가_따라간다()
        {
            editor.serializedObject.Update();
            list.serializedProperty.MoveArrayElement(2, 1);
            list.onReorderCallbackWithDetails(list, 2, 1);
            editor.serializedObject.ApplyModifiedProperties();
            Assert.That(sequence.commands[0].jumpTo, Is.EqualTo(1));
            Assert.That(sequence.commands[1].kind, Is.EqualTo(EventCommandKind.Stop));
        }
        [Test] public void 분기목적지를_삭제하면_오류로_표시한다()
        {
            editor.serializedObject.Update(); list.index = 2;
            list.onRemoveCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(sequence.commands[0].jumpTo, Is.EqualTo(-1));
            Assert.That(sequence.ValidateCommands(), Has.Count.EqualTo(1));
        }
        [Test] public void 명령추가는_이전명령을_복제하지_않는다()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            sequence.commands[2].speakerCharacter = character; sequence.commands[2].portraitId = "표정";
            editor.serializedObject.Update(); list.onAddCallback(list);
            editor.serializedObject.ApplyModifiedProperties();
            Assert.That(sequence.commands.Count, Is.EqualTo(4));
            Assert.That(sequence.commands[3].kind, Is.EqualTo(EventCommandKind.Stop));
            Assert.That(sequence.commands[3].unitId, Is.Empty);
            Assert.That(sequence.commands[3].speakerCharacter, Is.Null); Assert.That(sequence.commands[3].portraitId, Is.Empty);
            Object.DestroyImmediate(character);
        }
    }
}
