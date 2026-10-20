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
            sequence.commands[2].rosterAlly=character;
            sequence.commands[2].upgradeStageIncrease=7;
            sequence.commands[2].deploymentBlocked=false;
            editor.serializedObject.Update(); list.onAddCallback(list);
            editor.serializedObject.ApplyModifiedProperties();
            Assert.That(sequence.commands.Count, Is.EqualTo(4));
            Assert.That(sequence.commands[3].kind, Is.EqualTo(EventCommandKind.Stop));
            Assert.That(sequence.commands[3].unitId, Is.Empty);
            Assert.That(sequence.commands[3].rosterAlly,Is.Null);
            Assert.That(sequence.commands[3].deploymentBlocked,Is.True);
            Assert.That(sequence.commands[3].upgradeStageIncrease,Is.EqualTo(1));
            Assert.That(sequence.commands[3].speakerCharacter, Is.Null); Assert.That(sequence.commands[3].portraitId, Is.Empty);
            Object.DestroyImmediate(character);
        }
        [TestCase(0)] [TestCase(-1)]
        public void 최대개조단계_증가는_양수만_허용한다(int increase)
        {
            sequence.commands.Clear();sequence.commands.Add(new EventCommand {kind=EventCommandKind.IncreaseMaxUpgradeStage,upgradeStageIncrease=increase});
            Assert.That(sequence.ValidateCommands(),Has.Count.EqualTo(1));
            sequence.commands[0].upgradeStageIncrease=1;Assert.That(sequence.ValidateCommands(),Is.Empty);
        }
        [Test] public void 기존명령의_직렬화번호는_유지한다()
        {
            Assert.That((int)EventCommandKind.Stop,Is.EqualTo(9));Assert.That((int)EventCommandKind.IncreaseMaxUpgradeStage,Is.EqualTo(10));
            var campaign=ScriptableObject.CreateInstance<CampaignDefinition>();
            try{Assert.That(campaign.initialMaxUpgradeStage,Is.EqualTo(10));}
            finally{Object.DestroyImmediate(campaign);}
        }
        [TestCase(EventCommandKind.RecruitAlly)] [TestCase(EventCommandKind.RemoveAlly)] [TestCase(EventCommandKind.SetDeploymentBlocked)]
        public void 영입과이탈은_아군_편성을_필수로_요구한다(EventCommandKind kind)
        {
            sequence.commands.Clear();sequence.commands.Add(new EventCommand {kind=kind});Assert.That(sequence.ValidateCommands(),Is.Not.Empty);
            var ally=ScriptableObject.CreateInstance<CharacterDefinition>();
            try
            {
                sequence.commands[0].rosterAlly=ally;Assert.That(sequence.ValidateCommands(),Is.Empty);
                ally.faction=Faction.Enemy;Assert.That(sequence.ValidateCommands(),Is.Not.Empty);
            }
            finally{Object.DestroyImmediate(ally);}
        }
        [TestCase(EventCommandKind.Dialogue)] [TestCase(EventCommandKind.Wait)] [TestCase(EventCommandKind.ChangeHp)]
        public void 출격준비는_대사와_시간대기와_전투명령을_거절한다(EventCommandKind kind)
        {
            sequence.commands.Clear();sequence.commands.Add(new EventCommand {kind=kind,unitId="아군"});
            Assert.That(sequence.ValidateDeploymentSetup(),Has.Some.Contains("출격 준비"));
            sequence.commands[0]=new EventCommand {kind=EventCommandKind.SetFlag,flag="준비"};Assert.That(sequence.ValidateDeploymentSetup(),Is.Empty);
            Assert.That((int)EventCommandKind.SetDeploymentBlocked,Is.EqualTo(13));
        }
    }
}
