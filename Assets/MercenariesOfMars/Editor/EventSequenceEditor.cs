using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(EventSequence))]
    public sealed class EventSequenceEditor : UnityEditor.Editor
    {
        ReorderableList list;
        void OnEnable()
        {
            list = new ReorderableList(serializedObject, serializedObject.FindProperty("commands"), true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "명령 순서 · 드래그로 정렬 / 번호는 0부터 시작");
            list.elementHeightCallback = index => EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index)) + 25;
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 2; EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, 18), $"명령 {index}", EditorStyles.boldLabel);
                rect.y += 21; rect.height -= 23;
                EditorGUI.PropertyField(rect, list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true);
            };
            list.onAddCallback = _ =>
            {
                // Unity의 배열 추가는 마지막 원소를 복제하므로 모든 필드를 명시적으로 초기화한다.
                var array = list.serializedProperty; int i = array.arraySize++; var item = array.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("kind").enumValueIndex = (int)EventCommandKind.Stop;
                foreach (string key in new[] { "unitId", "targetId", "speaker", "text", "flag", "portraitId" }) item.FindPropertyRelative(key).stringValue = "";
                item.FindPropertyRelative("speakerCharacter").objectReferenceValue = null;
                item.FindPropertyRelative("speakerPilot").objectReferenceValue = null;
                item.FindPropertyRelative("skill").objectReferenceValue = null;
                item.FindPropertyRelative("rosterAlly").objectReferenceValue=null;
                item.FindPropertyRelative("deploymentBlocked").boolValue=true;
                item.FindPropertyRelative("seconds").floatValue = 1;
                item.FindPropertyRelative("amount").intValue = 0;
                item.FindPropertyRelative("upgradeStageIncrease").intValue=1;
                item.FindPropertyRelative("cell").vector2IntValue = Vector2Int.zero;
                item.FindPropertyRelative("flagValue").boolValue = true;
                item.FindPropertyRelative("jumpTo").intValue = 0;
                list.index = i;
            };
            list.onReorderCallbackWithDetails = (_, oldIndex, newIndex) =>
            {
                RemapTargets(target => target == oldIndex ? newIndex
                    : oldIndex < newIndex && target > oldIndex && target <= newIndex ? target - 1
                    : oldIndex > newIndex && target >= newIndex && target < oldIndex ? target + 1 : target);
            };
            list.onRemoveCallback = _ =>
            {
                int removed = list.index;
                if (removed < 0 || removed >= list.serializedProperty.arraySize) return;
                list.serializedProperty.DeleteArrayElementAtIndex(removed);
                RemapTargets(target => target == removed ? -1 : target > removed ? target - 1 : target);
                list.index = Mathf.Min(removed, list.serializedProperty.arraySize - 1);
            };
        }
        void RemapTargets(System.Func<int, int> remap)
        {
            for (int i = 0; i < list.serializedProperty.arraySize; i++)
            {
                var item = list.serializedProperty.GetArrayElementAtIndex(i);
                var kind = (EventCommandKind)item.FindPropertyRelative("kind").enumValueIndex;
                if (kind != EventCommandKind.BranchFlag && kind != EventCommandKind.BranchHp) continue;
                var jump = item.FindPropertyRelative("jumpTo"); jump.intValue = remap(jump.intValue);
            }
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("명령은 위에서 아래로 실행됩니다. 대사는 창 클릭·Space·Enter로 넘깁니다. 화자 파일럿을 지정하면 이름과 포트레이트를 사용합니다. 파일럿이 없으면 이름을 직접 입력합니다.", MessageType.Info);
            list.DoLayoutList(); serializedObject.ApplyModifiedProperties();
            foreach (var error in ((EventSequence)target).ValidateCommands()) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
    public sealed class EventEditorWindow : EditorWindow
    {
        EventSequence sequence;
        UnityEditor.Editor inspector;
        Vector2 scroll;
        [MenuItem("화성의 용병단/조립형 스크립트 에디터")]
        static void Open() => GetWindow<EventEditorWindow>("조립형 스크립트");
        void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        void OnGUI()
        {
            var next = (EventSequence)EditorGUILayout.ObjectField("이벤트 데이터", sequence, typeof(EventSequence), false);
            if (next != sequence)
            { sequence = next; if (inspector != null) DestroyImmediate(inspector); inspector = null; }
            if (GUILayout.Button("새 이벤트 만들기"))
            {
                string path = EditorUtility.SaveFilePanelInProject("이벤트 저장", "새 이벤트", "asset", "저장 위치를 선택하세요.");
                if (!string.IsNullOrEmpty(path))
                {
                    sequence = CreateInstance<EventSequence>(); AssetDatabase.CreateAsset(sequence, path);
                    if (inspector != null) DestroyImmediate(inspector); inspector = null; Selection.activeObject = sequence;
                }
            }
            if (sequence == null) return;
            UnityEditor.Editor.CreateCachedEditor(sequence, null, ref inspector);
            scroll = EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            if (GUILayout.Button("데이터 저장")) AssetDatabase.SaveAssetIfDirty(sequence);
        }
    }
}
