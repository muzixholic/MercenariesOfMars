using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(SkillDefinition))]
    public sealed class SkillDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MechPilotLists.Fields(serializedObject,new[]{"displayName","icon","price","power","accuracyModifier","minRange","maxRange","energyCost","maxAmmo"},
                new[]{"무장 이름","무장 이미지","구매 가격","공격력","명중 보정（%p）","최소 사거리","최대 사거리","소비 에너지","최대 탄수（0: 제한 없음）"});
            using(new EditorGUI.DisabledScope(serializedObject.FindProperty("maxAmmo").intValue==0))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentAmmo"),new GUIContent("현재 탄수（시작 잔탄）"));
            MechPilotLists.Fields(serializedObject,new[]{"weight","canUseAfterMoving","presentation","description"},
                new[]{"무게","이동 후 사용 가능","전투 연출","설명"});
            serializedObject.ApplyModifiedProperties();
            foreach(var error in ((SkillDefinition)target).ValidateSkill()) EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
    public sealed class SkillEditorWindow : EditorWindow
    {
        SkillDefinition skill;
        UnityEditor.Editor inspector;
        Vector2 scroll;
        [MenuItem("화성의 용병단/스킬 에디터")]
        public static void Open() => GetWindow<SkillEditorWindow>("스킬 에디터");
        [MenuItem("화성의 용병단/무장 에디터")]
        public static void OpenWeapon()=>Open();
        public static void Edit(SkillDefinition target)
        {
            var window = GetWindow<SkillEditorWindow>("스킬 에디터");
            window.skill = target; window.Focus();
        }
        void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("피해 = 최대(1, 캐릭터 공격력 + 스킬 공격력 − 방어력). 명중률 = 명중 + 스킬 보정 − 회피(0~100%). 이동 후 사용 가능을 끄면 이동한 턴에는 쓸 수 없습니다.", MessageType.Info);
            EditorGUILayout.HelpBox("최대 탄수 0은 탄수 제한 없음입니다. 최대 탄수가 있으면 현재 탄수를 전투 시작 잔탄으로 사용하고 공격·반격마다 1발을 소비합니다. 무게는 기체 허용중량을 제한합니다.",MessageType.Info);
            skill = (SkillDefinition)EditorGUILayout.ObjectField("스킬 데이터", skill, typeof(SkillDefinition), false);
            if (GUILayout.Button("새 스킬 만들기"))
            {
                string path = EditorUtility.SaveFilePanelInProject("스킬 저장", "새 스킬", "asset", "저장 위치를 선택하세요.");
                if (!string.IsNullOrEmpty(path))
                {
                    skill = CreateInstance<SkillDefinition>(); AssetDatabase.CreateAsset(skill, path); Selection.activeObject = skill;
                }
            }
            if (skill == null) return;
            UnityEditor.Editor.CreateCachedEditor(skill, null, ref inspector);
            scroll = EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            if (GUILayout.Button("스킬 저장")) AssetDatabase.SaveAssetIfDirty(skill);
        }
    }
}
