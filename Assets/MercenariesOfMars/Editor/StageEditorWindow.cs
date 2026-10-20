using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(StageDefinition))]
    public sealed class StageDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("출격 준비 스크립트 → 출격 배치 → 인트로 → 맵 전투 → 아웃트로 → 분기 확정 → 인터미션 → 다음 미션 순서입니다. 출격 준비에서는 영입·이탈·출격 선택 제한·플래그·개조 해금과 플래그 분기를 사용할 수 있습니다. 대사와 전투 명령은 인트로에 지정하세요. 조건별 다음 경로는 위에서부터 첫 일치 항목을 선택합니다. 엔딩은 목적지 종류에서 명시적으로 선택하세요.",MessageType.Info);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,"shop");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("클리어 후 인터미션 상점",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이 스테이지를 클리어한 직후 인터미션에서 사용하는 목록입니다. 기본 목록은 항상 판매하고, 조건부 추가 목록은 모든 조건을 만족할 때 합산합니다. 플래그·클리어 결과·영입·보유 조건을 사용할 수 있습니다. 이전 스테이지의 추가 목록은 자동 누적되지 않으므로 영구 해금은 유지되는 플래그를 조건으로 사용하세요.",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shop"),true);
            serializedObject.ApplyModifiedProperties();
            var stage=(StageDefinition)target;
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || stage.ValidateStage().Count>0))
                if(GUILayout.Button("스테이지 테스트 플레이")) StageBattlePreview.Start(stage);
            if(StageBattlePreview.IsTesting && GUILayout.Button("스테이지 테스트 종료")) EditorApplication.isPlaying=false;
            EditorGUILayout.HelpBox("테스트는 선택한 스테이지의 인트로부터 실행합니다. 전투 후 아웃트로와 분기 결과를 확인하고 종료하며 다음 스테이지로 자동 출격하지 않습니다. 이전 스테이지의 플래그·자금은 이월하지 않습니다.",MessageType.Info);
            foreach(var issue in ((StageDefinition)target).ValidateStage()) EditorGUILayout.HelpBox(issue,MessageType.Error);
        }
    }
    [CustomEditor(typeof(CampaignDefinition))]
    public sealed class CampaignDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(GUILayout.Button("캠페인 플로우 차트")) CampaignEditorWindow.Edit((CampaignDefinition)target);
            if(((CampaignDefinition)target).firstStage==null) EditorGUILayout.HelpBox("첫 스테이지를 지정하세요.",MessageType.Error);
            EditorGUILayout.HelpBox("전투 진행기의 캠페인 항목에 이 에셋을 지정하면 타이틀의 전투 시작부터 캠페인을 진행합니다. 맵 에디터 테스트는 캠페인에 진입하지 않습니다.",MessageType.Info);
            EditorGUILayout.HelpBox("초기 영입 아군에는 기체와 파일럿을 연결한 아군 유닛 편성을 등록하세요. 아군 출격 칸이 있는 맵에서는 이 영입 목록에서 선택해 출격합니다. 파일럿 에셋만으로는 출격할 수 없습니다.",MessageType.Info);
        }
    }
    public sealed class StageEditorWindow : EditorWindow
    {
        [SerializeField] Object selected;
        UnityEditor.Editor inspector;
        Vector2 scroll;
        [MenuItem("화성의 용병단/스테이지 에디터")]
        static void Open()=>GetWindow<StageEditorWindow>("스테이지 에디터");
        public static void Edit(StageDefinition stage)
        { var window=GetWindow<StageEditorWindow>("스테이지 에디터");window.selected=stage;window.Repaint(); }
        void OnDisable() { if(inspector!=null) DestroyImmediate(inspector); }
        void OnGUI()
        {
            var next=EditorGUILayout.ObjectField("스테이지 / 캠페인",selected,typeof(ScriptableObject),false);
            if(next!=selected && (next==null || next is StageDefinition || next is CampaignDefinition))
            { selected=next; if(inspector!=null) DestroyImmediate(inspector); inspector=null; }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("새 스테이지")) Create<StageDefinition>("새 스테이지");
            if(GUILayout.Button("새 캠페인")) Create<CampaignDefinition>("새 캠페인");
            EditorGUILayout.EndHorizontal();
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("현재 씬 전투를 파일로 추출")) StageSnapshotExporter.ExportCurrent();
            if(selected==null) return;
            UnityEditor.Editor.CreateCachedEditor(selected,null,ref inspector);
            scroll=EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            if(GUILayout.Button("데이터 저장")) AssetDatabase.SaveAssetIfDirty(selected);
        }
        void Create<T>(string label) where T:ScriptableObject
        {
            string path=EditorUtility.SaveFilePanelInProject(label+" 저장",label,"asset","저장 위치를 선택하세요.");
            if(string.IsNullOrEmpty(path)) return;
            selected=CreateInstance<T>(); AssetDatabase.CreateAsset(selected,path);
            if(inspector!=null) DestroyImmediate(inspector); inspector=null; Selection.activeObject=selected;
        }
    }
}
