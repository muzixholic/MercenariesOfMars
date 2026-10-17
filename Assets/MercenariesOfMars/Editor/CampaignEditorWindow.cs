using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public sealed class CampaignEditorWindow : EditorWindow
    {
        [SerializeField] CampaignDefinition campaign;
        [SerializeField] Vector2 pan=new(30,30);
        [SerializeField] float zoom=1;
        [SerializeField] int tab;
        CampaignGraph graph;
        int selectedId;
        bool dragging,fitRequested;
        double nextCheck;
        string signature;
        Vector2 detailScroll,settingsScroll;
        UnityEditor.Editor inspector;
        static readonly Color Background=new(.10f,.12f,.15f),StageColor=new(.13f,.25f,.39f),EndingColor=new(.12f,.35f,.27f),ErrorColor=new(.48f,.18f,.19f),CycleColor=new(.94f,.66f,.22f);
        [MenuItem("화성의 용병단/캠페인 에디터")]
        static void Open()=>Edit(Selection.activeObject as CampaignDefinition);
        public static void Edit(CampaignDefinition data)
        {
            var w=GetWindow<CampaignEditorWindow>("캠페인 에디터");w.minSize=new Vector2(780,420);
            if(data!=null) w.campaign=data;
            w.tab=0;w.Rebuild(true);w.Show();
        }
        public CampaignGraph Graph=>graph;
        public void ShowCampaign(CampaignDefinition data) {campaign=data;Rebuild(true);}
        public void FitGraph() {fitRequested=true;Repaint();}
        void OnEnable() {Undo.undoRedoPerformed+=Changed;EditorApplication.projectChanged+=Changed;Rebuild(true);}
        void OnDisable()
        {Undo.undoRedoPerformed-=Changed;EditorApplication.projectChanged-=Changed;if(inspector!=null) DestroyImmediate(inspector);}
        void Changed()=>Rebuild(false);
        string Signature()
        {
            var s=new StringBuilder(campaign!=null?EditorJsonUtility.ToJson(campaign):"");
            if(graph!=null) foreach(var node in graph.nodes.Where(n=>n.stage!=null)) s.Append(EditorJsonUtility.ToJson(node.stage));
            return s.ToString();
        }
        void OnInspectorUpdate()
        {
            if(EditorApplication.timeSinceStartup<nextCheck) return;
            nextCheck=EditorApplication.timeSinceStartup+.5;
            if(signature!=Signature()) Rebuild(false);
        }
        void Rebuild(bool fit)
        {
            var previous=graph?.nodes.FirstOrDefault(n=>n.id==selectedId)?.stage;
            graph=CampaignGraph.Build(campaign);
            selectedId=previous!=null?graph.nodes.FirstOrDefault(n=>n.stage==previous)?.id??0:0;
            signature=Signature();fitRequested|=fit;Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var next=(CampaignDefinition)EditorGUILayout.ObjectField(campaign,typeof(CampaignDefinition),false,GUILayout.MinWidth(150));
            if(next!=campaign) {campaign=next;Rebuild(true);}
            if(GUILayout.Button("새로고침",EditorStyles.toolbarButton,GUILayout.Width(70))) Rebuild(false);
            if(GUILayout.Button("전체 보기",EditorStyles.toolbarButton,GUILayout.Width(70))) FitGraph();
            EditorGUILayout.EndHorizontal();
            tab=GUILayout.Toolbar(tab,new[]{"플로우 차트","캠페인 설정"});
            if(campaign==null) {EditorGUILayout.HelpBox("캠페인 에셋을 지정하세요. 첫 스테이지부터 연결된 모든 분기와 엔딩을 표시합니다.",MessageType.Info);return;}
            if(tab==1)
            {
                UnityEditor.Editor.CreateCachedEditor(campaign,null,ref inspector);
                settingsScroll=EditorGUILayout.BeginScrollView(settingsScroll);inspector.OnInspectorGUI();EditorGUILayout.EndScrollView();return;
            }
            graph??=CampaignGraph.Build(campaign);
            // Layout 이벤트의 임시 GetRect 값으로 상세 영역이 잘리지 않도록 고정 창 영역을 사용한다.
            var area=new Rect(0,46,position.width,Mathf.Max(1,position.height-46));
            var viewport=new Rect(area.x,area.y,Mathf.Max(1,area.width-300),area.height-26);
            var detail=new Rect(viewport.xMax+8,area.y,292,area.height-26);
            if(fitRequested && Event.current.type==EventType.Repaint) {Fit(viewport);fitRequested=false;}
            HandleInput(viewport);DrawGraph(viewport);DrawDetail(detail);
            GUI.Label(new Rect(area.x+6,area.yMax-24,area.width-12,22),$"스테이지 {graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Stage)} · 엔딩 {graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Ending)} · 연결 오류 {graph.nodes.Count(n=>n.kind==CampaignGraphNodeKind.Missing)}   |   드래그 이동 · 휠 확대/축소 · F 전체 보기 · 더블 클릭 편집   {zoom*100:0}%",EditorStyles.miniLabel);
        }
        void Fit(Rect viewport)
        {
            zoom=Mathf.Clamp(Mathf.Min((viewport.width-36)/graph.Size.x,(viewport.height-36)/graph.Size.y),.001f,1.3f);
            pan=(viewport.size-graph.Size*zoom)/2;
        }
        void HandleInput(Rect viewport)
        {
            var e=Event.current;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.F) {Fit(viewport);e.Use();Repaint();}
            if(e.type==EventType.MouseUp) dragging=false;
            if(!viewport.Contains(e.mousePosition)) return;
            var local=e.mousePosition-viewport.position;
            if(e.type==EventType.ScrollWheel)
            {
                var point=(local-pan)/zoom;zoom=Mathf.Clamp(zoom*Mathf.Exp(-e.delta.y*.06f),.001f,2);
                pan=local-point*zoom;e.Use();Repaint();
            }
            if(e.type==EventType.MouseDown && (e.button==0 || e.button==2))
            {
                dragging=false;
                var hit=graph.nodes.LastOrDefault(n=>n.rect.Contains((local-pan)/zoom));
                if(e.button==0 && hit!=null && !e.alt)
                {
                    selectedId=hit.id;
                    if(hit.stage!=null) {Selection.activeObject=hit.stage;if(e.clickCount==2) StageEditorWindow.Edit(hit.stage);}
                }
                else dragging=true;
                e.Use();Repaint();
            }
            if(e.type==EventType.MouseDrag && dragging) {pan+=e.delta;e.Use();Repaint();}
        }
        void DrawGraph(Rect viewport)
        {
            GUI.BeginGroup(viewport);EditorGUI.DrawRect(new Rect(Vector2.zero,viewport.size),Background);
            if(Event.current.type==EventType.Repaint)
            {
                float step=Mathf.Max(12,32*zoom);
                for(float x=pan.x%step;x<viewport.width;x+=step) EditorGUI.DrawRect(new Rect(x,0,1,viewport.height),new Color(.16f,.18f,.21f));
                for(float y=pan.y%step;y<viewport.height;y+=step) EditorGUI.DrawRect(new Rect(0,y,viewport.width,1),new Color(.16f,.18f,.21f));
            }
            var handlesColor=Handles.color;
            Handles.BeginGUI();
            foreach(var edge in graph.edges) DrawEdge(edge);
            Handles.EndGUI();Handles.color=handlesColor;
            foreach(var node in graph.nodes) DrawNode(node);
            GUI.EndGroup();
        }
        Vector3 View(Vector3 point)=>new(pan.x+point.x*zoom,pan.y+point.y*zoom);
        Rect View(Rect rect)=>new(pan+rect.position*zoom,rect.size*zoom);
        void DrawEdge(CampaignGraph.Edge edge)
        {
            var from=View(new Vector3(edge.from.rect.xMax,edge.from.rect.center.y));
            var to=View(new Vector3(edge.to.rect.xMin,edge.to.rect.center.y));
            Color color=edge.to.kind==CampaignGraphNodeKind.Missing?new Color(.96f,.43f,.40f):edge.isCycle?CycleColor:edge.isDefault?new Color(.43f,.78f,.61f):new Color(.40f,.68f,.96f);
            Vector2 label;
            if(edge.returnLane>=0)
            {
                float y=pan.y+(graph.nodes.Max(n=>n.rect.yMax)+30+edge.returnLane*44)*zoom;
                Handles.color=color;Handles.DrawAAPolyLine(2,from,new Vector3(from.x+24*zoom,from.y),new Vector3(from.x+24*zoom,y),new Vector3(to.x-24*zoom,y),new Vector3(to.x-24*zoom,to.y),to);
                label=new Vector2((from.x+to.x)/2-100*zoom,y-20*zoom);
            }
            else
            {
                float offset=(edge.parallelIndex-(edge.parallelCount-1)/2f)*60*zoom;
                Handles.color=Color.white;
                Handles.DrawBezier(from,to,from+new Vector3(100*zoom,offset),to+new Vector3(-100*zoom,offset),color,null,2);
                label=new Vector2((from.x+to.x)/2-110*zoom,(from.y+to.y)/2-36*zoom+offset*.75f);
            }
            Handles.color=color;Handles.DrawAAConvexPolygon(to,to+new Vector3(-10,-5)*zoom,to+new Vector3(-10,5)*zoom);
            var style=new GUIStyle(EditorStyles.miniLabel) {wordWrap=true,fontSize=Mathf.Max(1,Mathf.RoundToInt(11*zoom)),clipping=TextClipping.Clip};style.normal.textColor=color;
            string text=edge.label+(edge.isCycle?" · 순환":"")+"\n"+(edge.isDefault?"나머지 조건 불일치":edge.conditions);
            GUI.Label(new Rect(label.x,label.y,220*zoom,42*zoom),new GUIContent(text,edge.label+"\n"+edge.conditions),style);
        }
        void DrawNode(CampaignGraph.Node node)
        {
            bool selected=node.id==selectedId;var rect=View(node.rect);
            Color header=node.kind==CampaignGraphNodeKind.Stage?StageColor:node.kind==CampaignGraphNodeKind.Ending?EndingColor:ErrorColor;
            EditorGUI.DrawRect(rect,selected?new Color(.48f,.76f,1):new Color(.33f,.39f,.45f));
            var inner=new Rect(rect.x+2*zoom,rect.y+2*zoom,rect.width-4*zoom,rect.height-4*zoom);EditorGUI.DrawRect(inner,new Color(.18f,.21f,.25f));
            EditorGUI.DrawRect(new Rect(inner.x,inner.y,inner.width,34*zoom),header);
            var title=new GUIStyle(EditorStyles.boldLabel) {fontSize=Mathf.Max(1,Mathf.RoundToInt(13*zoom)),clipping=TextClipping.Clip};title.normal.textColor=Color.white;
            GUI.Label(new Rect(inner.x+10*zoom,inner.y+7*zoom,inner.width-20*zoom,22*zoom),new GUIContent(node.title,node.title),title);
            string body=node.stage!=null?(node.depth==0?"첫 스테이지\n":"스테이지\n")+"맵: "+(node.stage.map!=null?node.stage.map.displayName:"미지정"):
                node.kind==CampaignGraphNodeKind.Ending?"엔딩\n"+node.detail:node.detail;
            var style=new GUIStyle(EditorStyles.label) {wordWrap=true,fontSize=Mathf.Max(1,Mathf.RoundToInt(11*zoom)),clipping=TextClipping.Clip};style.normal.textColor=new Color(.82f,.87f,.92f);
            GUI.Label(new Rect(inner.x+10*zoom,inner.y+43*zoom,inner.width-20*zoom,60*zoom),new GUIContent(body,body),style);
        }
        void DrawDetail(Rect rect)
        {
            GUILayout.BeginArea(rect);detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
            var node=graph.nodes.FirstOrDefault(n=>n.id==selectedId);
            if(node!=null)
            {
                GUILayout.Label(node.title,EditorStyles.boldLabel);
                if(node.stage!=null)
                {
                    if(GUILayout.Button("스테이지 편집")) StageEditorWindow.Edit(node.stage);
                    EditorGUILayout.ObjectField("스테이지",node.stage,typeof(StageDefinition),false);
                    EditorGUILayout.ObjectField("맵",node.stage.map,typeof(BattleMapDefinition),false);
                    foreach(var issue in node.stage.ValidateStage()) EditorGUILayout.HelpBox(issue,MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(node.detail??"",node.kind==CampaignGraphNodeKind.Missing?MessageType.Error:MessageType.Info);
                    if(node.source!=null && GUILayout.Button("연결한 스테이지 편집")) StageEditorWindow.Edit(node.source);
                }
                foreach(var edge in graph.edges.Where(e=>e.from==node))
                {
                    GUILayout.Space(14);GUILayout.Label(edge.label,EditorStyles.boldLabel);
                    GUILayout.Label(edge.conditions,EditorStyles.wordWrappedLabel);
                    GUILayout.Label("→ "+edge.to.title,EditorStyles.wordWrappedLabel);
                    if(edge.isCycle) EditorGUILayout.HelpBox("이 경로는 이전 스테이지로 순환합니다.",MessageType.Warning);
                    if(GUILayout.Button("목적지 보기")) selectedId=edge.to.id;
                }
            }
            EditorGUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
