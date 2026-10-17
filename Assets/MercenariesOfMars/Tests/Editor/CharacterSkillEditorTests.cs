using System.Reflection;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class CharacterSkillEditorTests
    {
        CharacterDefinition character;
        SkillDefinition first, second;
        UnityEditor.Editor editor;
        ReorderableList list;
        [SetUp] public void Setup()
        {
            character = ScriptableObject.CreateInstance<CharacterDefinition>();
            first = ScriptableObject.CreateInstance<SkillDefinition>(); second = ScriptableObject.CreateInstance<SkillDefinition>();
            character.skills.Add(first); character.skills.Add(second);
            editor = UnityEditor.Editor.CreateEditor(character, typeof(CharacterDefinitionEditor));
            list = (ReorderableList)typeof(CharacterDefinitionEditor).GetField("skills", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(editor); Object.DestroyImmediate(character); Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        }
        [Test] public void 스킬추가는_비어있는_항목을_추가한다()
        {
            editor.serializedObject.Update(); list.onAddCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(character.skills.Count, Is.EqualTo(3)); Assert.That(character.skills[2], Is.Null);
            Assert.That(character.skills[0], Is.SameAs(first));
        }
        [Test] public void 지정된_스킬항목을_한번에_삭제하고_에셋은_유지한다()
        {
            editor.serializedObject.Update(); list.index = 0; list.onRemoveCallback(list); editor.serializedObject.ApplyModifiedProperties();
            Assert.That(character.skills.Count, Is.EqualTo(1)); Assert.That(character.skills[0], Is.SameAs(second));
            Assert.That(first != null, Is.True);
        }
        [Test] public void 스킬순서를_변경하면_전투의_선택순서도_변한다()
        {
            editor.serializedObject.Update(); list.serializedProperty.MoveArrayElement(1,0); editor.serializedObject.ApplyModifiedProperties();
            var unit = new BattleUnit("아군",character,Vector2Int.zero);
            Assert.That(unit.Skills[0].Definition, Is.SameAs(second)); Assert.That(unit.Skills[1].Definition, Is.SameAs(first));
        }
    }
}
