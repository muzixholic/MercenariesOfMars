using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public sealed class CharacterEditorWindow : EditorWindow
    {
        CharacterDefinition character;
        UnityEditor.Editor inspector;
        Vector2 scroll;
        [MenuItem("화성의 용병단/캐릭터 능력치 에디터")]
        static void Open() => OpenUnit();
        [MenuItem("화성의 용병단/유닛 편성 에디터")]
        public static void OpenUnit()=>GetWindow<CharacterEditorWindow>("유닛 편성");
        public static void Edit(CharacterDefinition target)
        {
            var window = GetWindow<CharacterEditorWindow>("유닛 편성"); window.character = target; window.Focus();
        }
        void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("기체와 파일럿을 조합하고 진영을 지정합니다. 기체·파일럿 에셋은 여러 편성에서 공유할 수 있습니다. 변경값은 다음 전투부터 반영됩니다.", MessageType.Info);
            var next = (CharacterDefinition)EditorGUILayout.ObjectField("전투 유닛 편성", character, typeof(CharacterDefinition), false);
            if (next != character) character = next;
            if (GUILayout.Button("새 유닛 편성 만들기"))
            {
                string path = EditorUtility.SaveFilePanelInProject("유닛 편성 저장", "새 유닛 편성", "asset", "저장 위치를 선택하세요.");
                if (!string.IsNullOrEmpty(path))
                {
                    character = CreateInstance<CharacterDefinition>(); AssetDatabase.CreateAsset(character, path);
                    CharacterSplitMigration.Split(character);
                    Selection.activeObject = character;
                }
            }
            if (character == null) return;
            UnityEditor.Editor.CreateCachedEditor(character, null, ref inspector);
            scroll = EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            if (GUILayout.Button("데이터 저장"))
            {
                AssetDatabase.SaveAssetIfDirty(character);
                if(character.mech!=null) AssetDatabase.SaveAssetIfDirty(character.mech);
                if(character.pilot!=null) AssetDatabase.SaveAssetIfDirty(character.pilot);
            }
        }
    }
}
