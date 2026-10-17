using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    // 테스트 대상은 실행 중에만 생성한다. 기존 씬 파일과 배치를 저장하지 않는다.
    [InitializeOnLoad]
    public static class MapBattlePreview
    {
        const string MapKey = "MercenariesOfMars.맵테스트.경로";
        const string OptionsKey = "MercenariesOfMars.맵테스트.플레이옵션";
        public static bool IsTesting => !string.IsNullOrEmpty(SessionState.GetString(MapKey, ""));
        static MapBattlePreview() => EditorApplication.playModeStateChanged += StateChanged;
        public static void Start(BattleMapDefinition map)
        {
            if (map == null || EditorApplication.isPlayingOrWillChangePlaymode || map.ValidateMap().Count > 0) return;
            string path = AssetDatabase.GetAssetPath(map);
            if (string.IsNullOrEmpty(path)) return;
            SessionState.SetString(MapKey, path);
            SessionState.SetInt(OptionsKey, (int)EditorSettings.enterPlayModeOptions);
            // 테스트에 한해 씬 복원을 켜고 종료 후 원래 옵션으로 되돌린다.
            EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.isPlaying = true;
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if (!IsTesting) return;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(OptionsKey, 0);
                SessionState.EraseInt(OptionsKey); SessionState.EraseString(MapKey); MapEditorWindow.Open(); return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !IsTesting) return;
            var map = AssetDatabase.LoadAssetAtPath<BattleMapDefinition>(SessionState.GetString(MapKey, ""));
            if (map == null) { EditorApplication.isPlaying = false; return; }
            // 실행 중에만 기존 루트를 숨기므로 테스트 종료 시 원래 편집 상태로 돌아온다.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects()) root.SetActive(false);
            var battle=CreateRuntimeBattle(map,"맵 테스트 전투");
            battle.ConfigureMapTest(() => EditorApplication.isPlaying = false);
            battle.RestartBattle();
        }
        internal static BattleController CreateRuntimeBattle(BattleMapDefinition map,string name)
        {
            var test = new GameObject(name);
            var board = new GameObject("테스트 격자").AddComponent<GridBoard>(); board.transform.SetParent(test.transform); board.map = map;
            var camera = new GameObject("테스트 카메라").AddComponent<Camera>(); camera.transform.SetParent(test.transform);
            camera.orthographic = true; camera.farClipPlane = 200; camera.transform.position = new Vector3(0, 20, -12);
            camera.transform.rotation = Quaternion.Euler(55, 0, 0); camera.backgroundColor = new Color(.1f,.12f,.16f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("테스트 조명").AddComponent<Light>(); light.transform.SetParent(test.transform);
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50,-30,0); light.intensity = 1.2f;
            var battle = test.AddComponent<BattleController>(); battle.board = board; battle.battleCamera = camera;
            battle.eventRunner=test.AddComponent<EventRunner>();
            return battle;
        }
    }
}
