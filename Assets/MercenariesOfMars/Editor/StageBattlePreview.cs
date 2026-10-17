using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [InitializeOnLoad]
    public static class StageBattlePreview
    {
        const string StageKey="MercenariesOfMars.스테이지테스트.경로",OptionsKey="MercenariesOfMars.스테이지테스트.플레이옵션";
        public static bool IsTesting=>!string.IsNullOrEmpty(SessionState.GetString(StageKey,""));
        static StageBattlePreview()=>EditorApplication.playModeStateChanged+=StateChanged;
        public static void Start(StageDefinition stage)
        {
            if(stage==null || EditorApplication.isPlayingOrWillChangePlaymode || stage.ValidateStage().Count>0) return;
            string path=AssetDatabase.GetAssetPath(stage);if(string.IsNullOrEmpty(path)) return;
            SessionState.SetString(StageKey,path);SessionState.SetInt(OptionsKey,(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptions&=~EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.isPlaying=true;
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if(!IsTesting) return;
            string path=SessionState.GetString(StageKey,"");
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(OptionsKey,0);
                SessionState.EraseString(StageKey);SessionState.EraseInt(OptionsKey);
                StageEditorWindow.Edit(AssetDatabase.LoadAssetAtPath<StageDefinition>(path));return;
            }
            if(state!=PlayModeStateChange.EnteredPlayMode) return;
            var stage=AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if(stage==null) {EditorApplication.isPlaying=false;return;}
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                foreach(var root in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects()) root.SetActive(false);
            var battle=MapBattlePreview.CreateRuntimeBattle(stage.map,"스테이지 테스트 전투");
            var campaign=ScriptableObject.CreateInstance<CampaignDefinition>();campaign.firstStage=stage;
            battle.campaign=campaign;
            battle.ConfigureStageTest(()=>EditorApplication.isPlaying=false);
            battle.RestartBattle();
            EditorApplication.playModeStateChanged+=Cleanup;
            void Cleanup(PlayModeStateChange next)
            {
                if(next!=PlayModeStateChange.ExitingPlayMode) return;
                EditorApplication.playModeStateChanged-=Cleanup;
                if(campaign!=null) UnityEngine.Object.Destroy(campaign);
            }
        }
    }
}
