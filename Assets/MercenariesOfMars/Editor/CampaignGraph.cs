using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public enum CampaignGraphNodeKind { Stage, Ending, Missing }
    // 조건의 참/거짓을 실행하지 않고 도달 가능한 모든 설정 경로를 열거한다.
    public sealed class CampaignGraph
    {
        public sealed class Node
        {
            public int id,depth;
            public CampaignGraphNodeKind kind;
            public StageDefinition stage,source;
            public string title,detail;
            public Rect rect;
        }
        public sealed class Edge
        {
            public Node from,to;
            public string label,conditions;
            public bool isDefault,isCycle;
            public int returnLane=-1,parallelIndex,parallelCount;
        }
        public readonly List<Node> nodes=new();
        public readonly List<Edge> edges=new();
        public Vector2 Size { get; private set; }
        public const float NodeWidth=260,NodeHeight=112,ColumnStep=520,RowStep=172,Margin=40;
        public static CampaignGraph Build(CampaignDefinition campaign)
        {
            var graph=new CampaignGraph();
            var stages=new Dictionary<StageDefinition,Node>();var queue=new Queue<Node>();
            Node AddStage(StageDefinition stage,int depth)
            {
                if(stages.TryGetValue(stage,out var found)) return found;
                var node=new Node {id=graph.nodes.Count,depth=depth,kind=CampaignGraphNodeKind.Stage,stage=stage,title=stage.displayName};
                graph.nodes.Add(node);stages.Add(stage,node);queue.Enqueue(node);return node;
            }
            void Connect(Node from,StageDestination route,string label,string conditions,bool isDefault)
            {
                Node to;
                if(route!=null && route.Error==null && route.kind==StageDestinationKind.Stage) to=AddStage(route.stage,from.depth+1);
                else
                {
                    bool ending=route!=null && route.Error==null && route.kind==StageDestinationKind.Ending;
                    to=new Node {id=graph.nodes.Count,depth=from.depth+1,source=from.stage,
                        kind=ending?CampaignGraphNodeKind.Ending:CampaignGraphNodeKind.Missing,
                        title=ending?route.endingTitle:"연결 오류",detail=ending?route.endingText:route?.Error??"목적지가 비어 있습니다."};
                    graph.nodes.Add(to);
                }
                graph.edges.Add(new Edge {from=from,to=to,label=label,conditions=conditions,isDefault=isDefault});
            }
            if(campaign==null || campaign.firstStage==null)
                graph.nodes.Add(new Node {id=0,kind=CampaignGraphNodeKind.Missing,title="첫 스테이지 미지정",detail="캠페인 설정에서 첫 스테이지를 지정하세요."});
            else AddStage(campaign.firstStage,0);
            while(queue.Count>0)
            {
                var node=queue.Dequeue();var stage=node.stage;
                if(stage.branches!=null) for(int i=0;i<stage.branches.Count;i++)
                {
                    var branch=stage.branches[i];
                    Connect(node,branch?.destination,$"{i+1}. {branch?.label??"비어 있는 분기"}",Conditions(branch?.conditions),false);
                }
                Connect(node,stage.defaultDestination,"기본 경로","위 조건이 모두 불일치할 때",true);
            }
            bool Reaches(Node start,Node goal)
            {
                var visited=new HashSet<Node>();var stack=new Stack<Node>();stack.Push(start);
                while(stack.Count>0)
                {
                    var node=stack.Pop();if(node==goal) return true;
                    if(!visited.Add(node)) continue;
                    foreach(var edge in graph.edges.Where(e=>e.from==node)) stack.Push(edge.to);
                }
                return false;
            }
            foreach(var group in graph.edges.GroupBy(e=>(e.from,e.to)))
            {
                int index=0,count=group.Count();
                foreach(var edge in group) {edge.parallelIndex=index++;edge.parallelCount=count;}
            }
            float parallelPadding=graph.edges.Count==0?0:(graph.edges.Max(e=>e.parallelCount)-1)*30;
            int lane=0;
            foreach(var edge in graph.edges)
                if(edge.to.depth<=edge.from.depth)
                {edge.returnLane=lane++;edge.isCycle=Reaches(edge.to,edge.from);}
            int maxRows=graph.nodes.GroupBy(n=>n.depth).Max(g=>g.Count());
            foreach(var column in graph.nodes.GroupBy(n=>n.depth))
            {
                int row=0;
                foreach(var node in column) node.rect=new Rect(Margin+column.Key*ColumnStep,Margin+parallelPadding+(maxRows-column.Count())*RowStep/2+row++*RowStep,NodeWidth,NodeHeight);
            }
            graph.Size=new Vector2(graph.nodes.Max(n=>n.rect.xMax)+Margin,graph.nodes.Max(n=>n.rect.yMax)+Margin+lane*44+parallelPadding);
            return graph;
        }
        public static string Conditions(IReadOnlyList<StageCondition> conditions)
        {
            if(conditions==null || conditions.Count==0) return "조건 없음 · 설정 확인 필요";
            return string.Join(" 그리고 ",conditions.Select(c=>c==null?"비어 있는 조건":c.kind switch
            {
                StageConditionKind.Flag=>$"플래그 ‘{c.key}’ = {(c.flagValue?"참":"거짓")}",
                StageConditionKind.ClearTurnsAtMost=>$"클리어 턴 ≤ {c.value}",
                StageConditionKind.DefeatedEnemiesAtLeast=>$"적 격추 ≥ {c.value}",
                StageConditionKind.AllySurvived=>$"아군 ‘{c.key}’ 생존",
                _=>"알 수 없는 조건"
            }));
        }
    }
}
