using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public sealed class CharacterEditorWindow : EditorWindow
    {
        [SerializeField] CharacterDefinition character;
        [SerializeField] MechDefinition draftMech;
        [SerializeField] PilotDefinition draftPilot;
        [SerializeField] Faction draftFaction;
        [SerializeField] bool creating=true;
        UnityEditor.Editor inspector;
        UnityEditor.Editor draftMechInspector,draftPilotInspector;
        bool showDraftMech,showDraftPilot;
        string creationError;
        Vector2 scroll;
        [MenuItem("화성의 용병단/캐릭터 능력치 에디터")]
        static void Open() => OpenUnit();
        [MenuItem("화성의 용병단/유닛 편성 에디터")]
        public static void OpenUnit()=>GetWindow<CharacterEditorWindow>("유닛 편성");
        public static void Edit(CharacterDefinition target)
        {
            var window = GetWindow<CharacterEditorWindow>("유닛 편성"); window.character = target;window.creating=false;window.scroll=Vector2.zero; window.Focus();
        }
        void OnEnable()=>minSize=new Vector2(440,380);
        void OnDisable()
        {if(inspector!=null)DestroyImmediate(inspector);if(draftMechInspector!=null)DestroyImmediate(draftMechInspector);if(draftPilotInspector!=null)DestroyImmediate(draftPilotInspector);}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("기체와 파일럿을 조합하고 진영을 지정합니다. 기체·파일럿 에셋은 여러 편성에서 공유할 수 있습니다. 변경값은 다음 전투부터 반영됩니다.", MessageType.Info);
            var next = (CharacterDefinition)EditorGUILayout.ObjectField("전투 유닛 편성", character, typeof(CharacterDefinition), false);
            if (next != character) {character = next;creating=false;scroll=Vector2.zero;}
            if (GUILayout.Button("새 유닛 편성 만들기"))
            {
                creating=true;draftMech=null;draftPilot=null;draftFaction=Faction.Ally;creationError=null;scroll=Vector2.zero;
            }
            if(creating) {DrawCreation();return;}
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
        void DrawCreation()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("1. 기체와 파일럿 선택",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            draftMech=UnitFormationCreation.MechSelector(draftMech);
            draftPilot=UnitFormationCreation.PilotSelector(draftPilot);
            if(EditorGUI.EndChangeCheck())creationError=null;
            draftFaction=(Faction)EditorGUILayout.EnumPopup("기본 진영",draftFaction);
            EditorGUILayout.Space();EditorGUILayout.LabelField("2. 편성 이름 확인",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.TextField("자동 편성 이름",UnitFormationCreation.SuggestedName(draftMech,draftPilot));
            EditorGUILayout.HelpBox("기체 이름 (파일럿 이름)으로 편성 이름과 기본 파일명을 제안합니다. 신규 버튼으로 필요한 기체·파일럿만 만들 수 있습니다.",MessageType.Info);
            if(draftMech!=null)
            {
                showDraftMech=EditorGUILayout.Foldout(showDraftMech,"선택 기체 능력치",true);
                if(showDraftMech){UnityEditor.Editor.CreateCachedEditor(draftMech,null,ref draftMechInspector);draftMechInspector.OnInspectorGUI();}
            }
            if(draftPilot!=null)
            {
                showDraftPilot=EditorGUILayout.Foldout(showDraftPilot,"선택 파일럿 능력치",true);
                if(showDraftPilot){UnityEditor.Editor.CreateCachedEditor(draftPilot,null,ref draftPilotInspector);draftPilotInspector.OnInspectorGUI();}
            }
            var errors=UnitFormationCreation.SelectionErrors(draftMech,draftPilot);
            foreach(var error in errors)EditorGUILayout.HelpBox(error,draftMech==null || draftPilot==null?MessageType.Info:MessageType.Error);
            if(!string.IsNullOrEmpty(creationError))EditorGUILayout.HelpBox(creationError,MessageType.Error);
            EditorGUILayout.EndScrollView();
            using(new EditorGUI.DisabledScope(errors.Count>0))
                if(GUILayout.Button("3. 편성 저장"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("유닛 편성 저장",UnitFormationCreation.SuggestedFileName(draftMech,draftPilot),"asset","선택한 기체·파일럿을 연결한 편성 하나를 저장합니다.");
                    if(!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            character=UnitFormationCreation.Create(path,draftMech,draftPilot,draftFaction);
                            AssetDatabase.SaveAssetIfDirty(draftMech);AssetDatabase.SaveAssetIfDirty(draftPilot);
                            creating=false;scroll=Vector2.zero;Selection.activeObject=character;
                        }
                        catch(System.Exception e){creationError=e.Message;}
                    }
                }
            if(GUILayout.Button("취소")){creating=false;creationError=null;scroll=Vector2.zero;}
        }
    }
}
