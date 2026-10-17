using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    // 원본 씬의 참조와 편집 상태를 바꾸지 않고 새 데이터 에셋만 만든다.
    public static class StageSnapshotExporter
    {
        public static StageDefinition Export(BattleController source,string stagePath)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드를 종료한 뒤 추출하세요.");
            if(source==null || source.board==null) throw new ArgumentException("전투 진행기와 격자 보드가 필요합니다.");
            stagePath=stagePath.Replace('\\','/');
            string folder=Path.GetDirectoryName(stagePath)?.Replace('\\','/');
            if(!stagePath.StartsWith("Assets/",StringComparison.Ordinal) || !stagePath.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)
                || !AssetDatabase.IsValidFolder(folder)) throw new ArgumentException("프로젝트 Assets 안의 폴더와 .asset 경로를 선택하세요.");
            var original=source.campaign!=null?source.campaign.firstStage:null;
            var originalMap=original!=null?original.map:source.board.map;
            BattleMapDefinition map=originalMap!=null?UnityEngine.Object.Instantiate(originalMap):ScriptableObject.CreateInstance<BattleMapDefinition>();
            StageDefinition stage=original!=null?UnityEngine.Object.Instantiate(original):ScriptableObject.CreateInstance<StageDefinition>();
            TerrainDefinition blocked=null;
            var created=new List<string>();
            try
            {
                string stem=Path.GetFileNameWithoutExtension(stagePath);
                if(originalMap==null)
                {
                    map.displayName="현재 전투 맵";map.width=source.board.width;map.height=source.board.height;map.cellSize=source.board.cellSize;
                    map.openingEvent=source.openingEvent;
                    map.units=source.placements.Select(p=>p==null?null:new UnitPlacement {
                        id=p.id,character=p.character,cell=p.cell,autoEnemyLevel=p.autoEnemyLevel,enemyLevelPreset=p.enemyLevelPreset }).ToList();
                    if(source.board.blockedCells.Count>0)
                    {
                        blocked=ScriptableObject.CreateInstance<TerrainDefinition>();blocked.displayName="현재 전투 진입 불가";
                        blocked.walkable=false;blocked.color=new Color(.24f,.27f,.3f);
                        foreach(var cell in source.board.blockedCells.Distinct()) map.Paint(cell,blocked);
                    }
                }
                if(original==null)
                {
                    stage.displayName="현재 전투";stage.intro=map.openingEvent;stage.defaultDestination.kind=StageDestinationKind.Ending;
                    stage.defaultDestination.endingTitle="작전 완료";stage.defaultDestination.endingText="적군을 모두 격파했습니다.";
                }
                stage.map=map;
                var issues=stage.ValidateStage();
                if(issues.Count>0) throw new InvalidOperationException(string.Join("\n",issues));
                if(blocked!=null) Save(blocked,folder+"/"+stem+"_진입불가지형.asset",created);
                Save(map,folder+"/"+stem+"_맵.asset",created);
                Save(stage,stagePath,created);
                foreach(string path in created) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
                return stage;
            }
            catch
            {
                foreach(string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                if(stage!=null && !EditorUtility.IsPersistent(stage)) UnityEngine.Object.DestroyImmediate(stage);
                if(map!=null && !EditorUtility.IsPersistent(map)) UnityEngine.Object.DestroyImmediate(map);
                if(blocked!=null && !EditorUtility.IsPersistent(blocked)) UnityEngine.Object.DestroyImmediate(blocked);
                throw;
            }
        }
        static void Save(UnityEngine.Object data,string path,List<string> created)
        { path=AssetDatabase.GenerateUniqueAssetPath(path);data.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(data,path);created.Add(path); }
        [MenuItem("화성의 용병단/현재 전투를 스테이지로 추출")]
        public static void ExportCurrent()
        {
            var source=UnityEngine.Object.FindAnyObjectByType<BattleController>();
            if(source==null) {EditorUtility.DisplayDialog("추출 불가","현재 씬에 전투 진행기가 없습니다.","확인");return;}
            string path=EditorUtility.SaveFilePanelInProject("현재 전투 추출","현재전투_스테이지","asset","스테이지와 같은 폴더에 맵·진입 불가 지형도 함께 저장합니다. 기존 파일은 덮어쓰지 않습니다.");
            if(string.IsNullOrEmpty(path)) return;
            try {var stage=Export(source,path);Selection.activeObject=stage;StageEditorWindow.Edit(stage);}
            catch(Exception e) {EditorUtility.DisplayDialog("추출 불가",e.Message,"확인");}
        }
    }
}
