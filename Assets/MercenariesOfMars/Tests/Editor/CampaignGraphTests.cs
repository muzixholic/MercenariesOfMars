using System.Collections.Generic;
using System.Linq;
using MercenariesOfMars.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MercenariesOfMars.Tests
{
    public sealed class CampaignGraphTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var x=ScriptableObject.CreateInstance<T>();owned.Add(x);return x;}
        StageDefinition Stage(string title)
        {var s=New<StageDefinition>();s.displayName=title;s.defaultDestination.kind=StageDestinationKind.Ending;return s;}
        CampaignGraph Graph(StageDefinition first)
        {var c=New<CampaignDefinition>();c.firstStage=first;return CampaignGraph.Build(c);}
        StageBranch Branch(StageDefinition next,string key="조건")=>new() {label="구출 분기",conditions=new() {new StageCondition {key=key}},destination=new StageDestination {stage=next}};
        [TearDown] public void Cleanup() {foreach(var x in owned) Object.DestroyImmediate(x);owned.Clear();}
        [Test] public void 모든분기와_합류를_탐색하되_같은스테이지는_한번만_표시한다()
        {
            var first=Stage("시작");var left=Stage("동일 이름");var right=Stage("동일 이름");var merge=Stage("합류");
            first.branches.Add(Branch(left,"구출"));first.defaultDestination=new StageDestination {stage=right};
            left.defaultDestination=new StageDestination {stage=merge};right.defaultDestination=new StageDestination {stage=merge};
            var graph=Graph(first);
            Assert.That(graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Stage),Is.EqualTo(4));
            Assert.That(graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Ending),Is.EqualTo(1));
            Assert.That(graph.edges.Count(e=>e.to.stage==merge),Is.EqualTo(2));Assert.That(graph.edges.Any(e=>e.isCycle),Is.False);
            Assert.That(graph.edges[0].conditions,Does.Contain("구출"));Assert.That(graph.edges[1].isDefault,Is.True);
            foreach(var node in graph.nodes) foreach(var other in graph.nodes.Where(n=>n.id!=node.id)) Assert.That(node.rect.Overlaps(other.rect),Is.False);
        }
        [Test] public void 순환과_자기연결은_무한탐색없이_표시한다()
        {
            var first=Stage("처음");var second=Stage("두번째");first.defaultDestination=new StageDestination {stage=second};
            second.branches.Add(Branch(first));second.branches.Add(Branch(second));
            var graph=Graph(first);
            Assert.That(graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Stage),Is.EqualTo(2));
            Assert.That(graph.edges.Count(e=>e.isCycle),Is.EqualTo(2));
            Assert.That(graph.edges.Where(e=>e.isCycle).All(e=>e.returnLane>=0),Is.True);
            Assert.That(graph.Size.y,Is.GreaterThan(graph.nodes.Max(n=>n.rect.yMax)));
        }
        [Test] public void 비어있는_목적지와_알수없는_종류는_오류노드로_남긴다()
        {
            var first=Stage("처음");first.defaultDestination=null;first.branches.Add(null);
            first.branches.Add(new StageBranch {destination=new StageDestination {kind=(StageDestinationKind)999}});
            var graph=Graph(first);
            Assert.That(graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Missing),Is.EqualTo(3));
            Assert.That(graph.edges.Count,Is.EqualTo(3));Assert.That(Graph(null).nodes.Single().kind,Is.EqualTo(CampaignGraphNodeKind.Missing));
            Assert.That(CampaignGraph.Build(null).nodes.Single().kind,Is.EqualTo(CampaignGraphNodeKind.Missing));
        }
        [Test] public void 같은목적지의_여러분기는_모두보존하고_병렬연결로_구분한다()
        {
            var first=Stage("시작");var next=Stage("도착");first.branches.Add(Branch(next));first.defaultDestination=new StageDestination {stage=next};
            var graph=Graph(first);var connections=graph.edges.Where(e=>e.from.stage==first).ToList();
            Assert.That(connections.Count,Is.EqualTo(2));Assert.That(connections.All(e=>e.parallelCount==2),Is.True);
            Assert.That(connections.Select(e=>e.parallelIndex).Distinct().Count(),Is.EqualTo(2));
        }
        [Test] public void 모든조건과_동일제목의_서로다른엔딩을_표시한다()
        {
            var first=Stage("시작");first.branches.Add(new StageBranch {destination=new StageDestination {kind=StageDestinationKind.Ending},conditions=new() {
                new StageCondition {key="구출",flagValue=false},new StageCondition {kind=StageConditionKind.ClearTurnsAtMost,value=8},
                new StageCondition {kind=StageConditionKind.DefeatedEnemiesAtLeast,value=3},new StageCondition {kind=StageConditionKind.AllySurvived,key="아군_1"}}});
            var graph=Graph(first);
            Assert.That(graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Ending),Is.EqualTo(2));
            string text=graph.edges[0].conditions;
            foreach(string part in new[]{"거짓","≤ 8","≥ 3","아군_1","그리고"}) Assert.That(text,Does.Contain(part));
        }
    }
}
