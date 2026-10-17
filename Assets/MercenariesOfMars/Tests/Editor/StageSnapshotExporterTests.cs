using System;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class StageSnapshotExporterTests
    {
        string folder;
        GameObject root;
        T Asset<T>(string name) where T:ScriptableObject
        {var a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,folder+"/"+name+".asset");return a;}
        [SetUp] public void Setup()
        {folder="Assets/임시스테이지추출검증_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));}
        [TearDown] public void Cleanup()
        {if(root!=null) UnityEngine.Object.DestroyImmediate(root);AssetDatabase.DeleteAsset(folder);}
        BattleController Source()
        {
            root=new GameObject("원본 전투");root.SetActive(false);var c=root.AddComponent<BattleController>();c.board=root.AddComponent<GridBoard>();
            // 스크립트 Inspector 기본 참조와 무관하게 단일 전투 추출을 검증한다.
            c.campaign=null;c.board.map=null;
            c.board.width=6;c.board.height=4;c.board.cellSize=2;c.board.blockedCells.Add(new Vector2Int(2,2));
            for(int i=0;i<2;i++)
            {
                var d=Asset<CharacterDefinition>("유닛"+i);d.faction=i==0?Faction.Ally:Faction.Enemy;
                c.placements.Add(new UnitPlacement {id="유닛"+i,character=d,cell=new Vector2Int(i,0),autoEnemyLevel=false});
            }
            c.openingEvent=Asset<EventSequence>("인트로");return c;
        }
        [Test] public void 씬전투를_별도파일로_추출하고_원본과_기존파일을_변경하지않는다()
        {
            var c=Source();string before=EditorJsonUtility.ToJson(c);bool dirty=c.gameObject.scene.isDirty;
            var stage=StageSnapshotExporter.Export(c,folder+"/추출.asset");
            Assert.That(stage.ValidateStage(),Is.Empty);Assert.That(stage.map.width,Is.EqualTo(6));Assert.That(stage.map.height,Is.EqualTo(4));
            Assert.That(stage.map.cellSize,Is.EqualTo(2));Assert.That(stage.map.CreateGrid().IsWalkable(new Vector2Int(2,2)),Is.False);
            Assert.That(stage.map.units[0].character,Is.SameAs(c.placements[0].character));
            Assert.That(stage.map.units[0].autoEnemyLevel,Is.False);Assert.That(stage.intro,Is.SameAs(c.openingEvent));
            Assert.That(stage.map.openingEvent,Is.SameAs(c.openingEvent));Assert.That(stage.defaultDestination.kind,Is.EqualTo(StageDestinationKind.Ending));
            Assert.That(EditorUtility.IsPersistent(stage.map),Is.True);Assert.That(c.board.map,Is.Null);
            Assert.That(EditorJsonUtility.ToJson(c),Is.EqualTo(before));Assert.That(c.gameObject.scene.isDirty,Is.EqualTo(dirty));
            stage.map.units[0].cell=Vector2Int.up;Assert.That(c.placements[0].cell,Is.EqualTo(Vector2Int.zero));
            var second=StageSnapshotExporter.Export(c,folder+"/추출.asset");
            Assert.That(AssetDatabase.GetAssetPath(second),Is.Not.EqualTo(AssetDatabase.GetAssetPath(stage)));
            Assert.That(stage.map.units[0].cell,Is.EqualTo(Vector2Int.up));
        }
        [Test] public void 맵이_연결된_캠페인은_지형_이벤트_아웃트로_분기를_보존한다()
        {
            var c=Source();var sourceMap=Asset<BattleMapDefinition>("원본맵");sourceMap.units.AddRange(c.placements);
            var terrain=Asset<TerrainDefinition>("숲");terrain.movementCost=3;sourceMap.Paint(Vector2Int.up,terrain);
            var sourceStage=Asset<StageDefinition>("원본스테이지");sourceStage.map=sourceMap;sourceStage.intro=c.openingEvent;
            sourceStage.outro=Asset<EventSequence>("아웃트로");sourceStage.defaultDestination.kind=StageDestinationKind.Ending;
            sourceStage.branches.Add(new StageBranch {conditions=new() {new StageCondition {key="분기"}},destination=new StageDestination {kind=StageDestinationKind.Ending,endingTitle="분기 엔딩"}});
            c.campaign=Asset<CampaignDefinition>("캠페인");c.campaign.firstStage=sourceStage;
            var exported=StageSnapshotExporter.Export(c,folder+"/추출.asset");
            Assert.That(exported.map,Is.Not.SameAs(sourceMap));Assert.That(exported.intro,Is.SameAs(sourceStage.intro));
            Assert.That(exported.outro,Is.SameAs(sourceStage.outro));Assert.That(exported.branches[0].destination.endingTitle,Is.EqualTo("분기 엔딩"));
            Assert.That(exported.map.TerrainAt(Vector2Int.up),Is.SameAs(terrain));
            exported.branches[0].conditions[0].key="변경";Assert.That(sourceStage.branches[0].conditions[0].key,Is.EqualTo("분기"));
        }
        [Test] public void 잘못된_배치는_추출파일을_남기지않는다()
        {
            var c=Source();c.placements[1].cell=c.placements[0].cell;
            int count=AssetDatabase.FindAssets("",new[]{folder}).Length;
            Assert.Throws<InvalidOperationException>(()=>StageSnapshotExporter.Export(c,folder+"/오류.asset"));
            Assert.That(AssetDatabase.FindAssets("",new[]{folder}).Length,Is.EqualTo(count));
        }
    }
}
