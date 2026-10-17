using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(PilotGrowthPreset))]
    public sealed class PilotGrowthPresetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"),new GUIContent("프리셋 이름"));
            EditorGUILayout.HelpBox("X는 레벨 위치(0=1레벨, 1=99레벨), Y는 기본값에서 최종값까지의 성장 비율입니다. 프리셋 하나에 커브 하나를 설정하고 파일럿의 능력치마다 선택합니다.",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("curve"),new GUIContent("성장 커브"));
            serializedObject.ApplyModifiedProperties();
            foreach(var error in ((PilotGrowthPreset)target).ValidatePreset()) EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
    [CustomEditor(typeof(EnemyLevelPreset))]
    public sealed class EnemyLevelPresetEditor : UnityEditor.Editor
    {
        float average=20;
        int baseExperience=100;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MechPilotLists.Fields(serializedObject,new[]{"displayName","multiplier","offset","minimumLevel","maximumLevel","experienceMultiplier","experienceOffset"},
                new[]{"프리셋 이름","아군 평균 레벨 배율","레벨 보정","최소 레벨","최대 레벨","격추 경험치 배율","격추 경험치 가산값"});
            serializedObject.ApplyModifiedProperties();
            baseExperience=Mathf.Max(0,EditorGUILayout.IntField("기본 격추 경험치 미리보기",baseExperience));
            EditorGUILayout.LabelField("보정 후 격추 경험치",((EnemyLevelPreset)target).CalculateExperience(baseExperience).ToString());
            EditorGUILayout.HelpBox("격추 경험치 = 반올림(기본 경험치 × 경험치 배율 + 가산값), 최소 0. 레벨 자동 보정 여부와 독립적입니다. 경험치 지급·자동 레벨업은 별도 기능입니다.",MessageType.Info);
            average=EditorGUILayout.Slider("출격 아군 평균 미리보기",average,1,99);
            EditorGUILayout.LabelField("계산된 적군 레벨",((EnemyLevelPreset)target).Calculate(average).ToString());
            EditorGUILayout.HelpBox("반올림(출격 아군 평균 × 배율 + 보정)을 최소·최대 레벨과 1~99로 제한합니다. 전투 시작 시 한 번 계산합니다.",MessageType.Info);
        }
    }
    [CustomEditor(typeof(PilotProgressionSettings))]
    public sealed class PilotProgressionSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MechPilotLists.Fields(serializedObject,new[]{"newPilotGrowth","defaultEnemyLevel","growthPresets","enemyLevelPresets"},
                new[]{"새 파일럿 기본 성장","기본 적군 레벨 보정","성장 프리셋 목록","적군 보정 프리셋 목록"});
            serializedObject.ApplyModifiedProperties();
        }
    }
    public sealed class PilotProgressionWindow : EditorWindow
    {
        [SerializeField] PilotProgressionSettings settings;
        UnityEditor.Editor inspector;
        Vector2 scroll;
        int tab;
        Object selected;
        [MenuItem("화성의 용병단/설정/파일럿 성장 프리셋")]
        public static void OpenGrowth()=>Open(0);
        [MenuItem("화성의 용병단/설정/적군 레벨 보정 프리셋")]
        public static void OpenEnemies()=>Open(1);
        static void Open(int tab)
        {
            var w=GetWindow<PilotProgressionWindow>("성장·적군 레벨 설정");
            w.settings=PilotProgressionSetup.EnsureDefaults(); w.tab=tab; w.selected=null; w.Focus();
        }
        void OnDisable() { if(inspector!=null) DestroyImmediate(inspector); }
        void OnGUI()
        {
            if(settings==null) { if(GUILayout.Button("기본 설정 만들기·열기")) settings=PilotProgressionSetup.EnsureDefaults(); return; }
            settings=(PilotProgressionSettings)EditorGUILayout.ObjectField("설정 에셋",settings,typeof(PilotProgressionSettings),false);
            if(settings==null) return;
            int nextTab=GUILayout.Toolbar(tab,new[]{"파일럿 성장","적군 레벨 보정","기본값·등록 목록"});
            if(nextTab!=tab) { tab=nextTab; selected=null; GUIUtility.ExitGUI(); }
            if(tab==2)
            {
                UnityEditor.Editor.CreateCachedEditor(settings,null,ref inspector);
                scroll=EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            }
            else
            {
                var entries=tab==0?settings.growthPresets.Where(p=>p!=null).Cast<Object>().ToArray():settings.enemyLevelPresets.Where(p=>p!=null).Cast<Object>().ToArray();
                if(!entries.Contains(selected)) selected=entries.FirstOrDefault();
                if(entries.Length>0)
                {
                    var labels=entries.Select(p=>p is PilotGrowthPreset g?g.displayName:((EnemyLevelPreset)p).displayName).ToArray();
                    selected=entries[EditorGUILayout.Popup("편집할 프리셋",System.Array.IndexOf(entries,selected),labels)];
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("새 프리셋 추가")) CreatePreset(false);
                    using(new EditorGUI.DisabledScope(selected==null))
                    {
                        if(GUILayout.Button("선택 복제")) CreatePreset(true);
                        if(GUILayout.Button("등록 해제"))
                        {
                            Undo.RecordObject(settings,"프리셋 등록 해제");
                            if(tab==0) settings.growthPresets.Remove((PilotGrowthPreset)selected); else settings.enemyLevelPresets.Remove((EnemyLevelPreset)selected);
                            selected=null; EditorUtility.SetDirty(settings); GUIUtility.ExitGUI();
                        }
                    }
                }
                if(selected!=null)
                {
                    UnityEditor.Editor.CreateCachedEditor(selected,null,ref inspector);
                    scroll=EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.HelpBox("등록 해제는 목록에서만 제외합니다. 이미 파일럿·맵에 연결된 프리셋 에셋은 유지됩니다.",MessageType.None);
            }
            if(GUILayout.Button("설정과 현재 프리셋 저장"))
            { AssetDatabase.SaveAssetIfDirty(settings); if(selected!=null) AssetDatabase.SaveAssetIfDirty(selected); }
        }
        void CreatePreset(bool duplicate)
        {
            string path=EditorUtility.SaveFilePanelInProject("프리셋 저장",tab==0?"새 성장 프리셋":"새 적군 보정","asset","저장 위치를 선택하세요.");
            if(string.IsNullOrEmpty(path)) return;
            var data=duplicate?Instantiate(selected):tab==0?(Object)CreateInstance<PilotGrowthPreset>():CreateInstance<EnemyLevelPreset>();
            if(data is PilotGrowthPreset g) g.displayName=duplicate?g.displayName+" 복사":"새 성장 프리셋";
            if(data is EnemyLevelPreset e) e.displayName=duplicate?e.displayName+" 복사":"새 적군 보정";
            AssetDatabase.CreateAsset(data,path); Undo.RegisterCreatedObjectUndo(data,"프리셋 추가"); Undo.RecordObject(settings,"프리셋 등록");
            if(tab==0) settings.growthPresets.Add((PilotGrowthPreset)data); else settings.enemyLevelPresets.Add((EnemyLevelPreset)data);
            EditorUtility.SetDirty(settings); selected=data; AssetDatabase.SaveAssetIfDirty(settings); AssetDatabase.SaveAssetIfDirty(data);
            GUIUtility.ExitGUI();
        }
    }
}
