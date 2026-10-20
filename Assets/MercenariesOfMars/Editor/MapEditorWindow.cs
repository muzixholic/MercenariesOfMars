using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        static readonly string[] Tools = { "지형 칠하기", "구조물 배치", "유닛 배치", "이벤트 구역", "목표 칸", "이동 미리보기", "아군 출격 칸" };
        static readonly string[] Displays = { "지형", "이동 비용", "명중·방어 보정", "이동 불가" };
        [SerializeField] BattleMapDefinition map;
        TerrainDefinition terrain;
        StructureDefinition structure;
        CharacterDefinition character;
        EventSequence sequence;
        UnityEditor.Editor definitionInspector;
        Vector2 panelScroll, canvasScroll;
        int tool, display, tab;
        float zoom = 48;
        int previewMovement = 4;
        string unitId = "유닛";
        string deploymentId="출격";
        Vector2Int? regionStart;
        Vector2Int? hover;
        readonly HashSet<Vector2Int> painted = new();
        HashSet<Vector2Int> reachable = new();
        Vector2Int? previewStart;
        List<string> errors = new(), warnings = new();
        BattleGrid cachedGrid;
        bool stroke;
        int undoGroup;

        [MenuItem("화성의 용병단/맵 에디터")]
        public static void Open() => GetWindow<MapEditorWindow>("맵 에디터");
        public static void Edit(BattleMapDefinition target)
        {
            var window = GetWindow<MapEditorWindow>("맵 에디터"); window.map = target; window.RefreshMap(); window.Focus();
        }
        void OnEnable() { titleContent = new GUIContent("맵 에디터"); minSize = new Vector2(850, 600); wantsMouseMove = true; Undo.undoRedoPerformed += UndoChanged; }
        void OnDisable()
        {
            EndStroke(); Undo.undoRedoPerformed -= UndoChanged;
            if (definitionInspector != null) DestroyImmediate(definitionInspector);
        }
        void UndoChanged() { RefreshMap(); Repaint(); }
        void RefreshMap()
        {
            cachedGrid = map != null && map.width > 0 && map.height > 0 && map.width <= 64 && map.height <= 64 ? map.CreateGrid() : null;
            reachable.Clear(); previewStart = null; errors.Clear(); warnings.Clear();
        }
        void Changed()
        {
            EditorUtility.SetDirty(map); RefreshMap();
        }
        void Record(string label) => Undo.RecordObject(map, label);
        void EndStroke()
        {
            if (stroke) Undo.CollapseUndoOperations(undoGroup);
            stroke = false; painted.Clear(); regionStart = null;
        }
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = (BattleMapDefinition)EditorGUILayout.ObjectField(map, typeof(BattleMapDefinition), false, GUILayout.Width(260));
                if (next != map) { EndStroke(); map = next; RefreshMap(); }
                if (GUILayout.Button("새 맵", EditorStyles.toolbarButton)) CreateMap();
                if (map != null && GUILayout.Button("검증 후 저장", EditorStyles.toolbarButton)) SaveValidated();
                if (map != null && GUILayout.Button("초안 저장", EditorStyles.toolbarButton)) AssetDatabase.SaveAssetIfDirty(map);
                GUI.enabled = map != null && !EditorApplication.isPlayingOrWillChangePlaymode;
                if (GUILayout.Button("테스트 전투", EditorStyles.toolbarButton))
                {
                    Validate();
                    if (errors.Count == 0) { AssetDatabase.SaveAssetIfDirty(map); MapBattlePreview.Start(map); }
                    else EditorUtility.DisplayDialog("테스트 전투", "검증 오류를 먼저 해결하세요. 검증 탭에서 확인할 수 있습니다.", "확인");
                }
                GUI.enabled = true;
                if (MapBattlePreview.IsTesting && GUILayout.Button("테스트 종료", EditorStyles.toolbarButton)) EditorApplication.isPlaying = false;
            }
            if (map == null)
            {
                EditorGUILayout.HelpBox("새 맵을 만들거나 전투 맵 에셋을 지정하세요. 지형 정의 → 지형 칠하기 → 구조물·유닛·이벤트 배치 → 검증 → 테스트 전투 순서로 작업합니다.", MessageType.Info); return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(310)))
                {
                    panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
                    DrawPanel(); EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    tool = GUILayout.SelectionGrid(tool, Tools,4);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        display = EditorGUILayout.Popup("표시", display, Displays);
                        zoom = EditorGUILayout.Slider("칸 확대", zoom, 24, 90);
                    }
                    EditorGUILayout.LabelField("좌클릭·드래그: 현재 도구 / 우클릭: 해당 층 삭제 / Ctrl 또는 Cmd+Z: 실행 취소", EditorStyles.miniLabel);
                    DrawCanvas();
                    string cellInfo = "칸 위에 마우스를 올리면 지형 정보를 표시합니다.";
                    if (hover.HasValue && cachedGrid != null)
                    {
                        var c = hover.Value; var t = map.TerrainAt(c);
                        cellInfo = $"칸 {c.x}, {c.y} · {(t != null ? t.displayName : "평지")} · 비용 {cachedGrid.MovementCost(c)} · 명중 {cachedGrid.AccuracyModifier(c):+0;-0;0}%p · 방어 {cachedGrid.DefenseModifier(c):+0;-0;0}";
                    }
                    EditorGUILayout.LabelField(cellInfo);
                }
            }
            if (Event.current.type == EventType.MouseUp && stroke) EndStroke();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { EndStroke(); reachable.Clear(); previewStart = null; Event.current.Use(); Repaint(); }
        }
        void DrawPanel()
        {
            tab = GUILayout.Toolbar(tab, new[] { "맵 설정", "도구", "배치", "검증" });
            if (tab == 0)
            {
                var so = new SerializedObject(map); so.Update();
                foreach (string name in new[] { "displayName", "width", "height", "cellSize", "defaultTerrain", "openingEvent", "victoryCondition" })
                    MapPropertyLabels.Field(so, name);
                if (map.victoryCondition == MapVictoryCondition.ReachCell) MapPropertyLabels.Field(so, "goalCell");
                if (so.ApplyModifiedProperties()) RefreshMap();
                EditorGUILayout.HelpBox("기본 지형 미지정 칸은 비용 1의 평지입니다. 크기를 줄여도 기존 배치는 자동 삭제되지 않습니다. 검증에서 맵 밖 항목을 확인하세요.", MessageType.Info);
                if (GUILayout.Button("기본 지형 세트 만들기")) CreateTerrainSet();
            }
            else if (tab == 1)
            {
                if (tool == 0)
                {
                    terrain = (TerrainDefinition)EditorGUILayout.ObjectField("칠할 지형", terrain, typeof(TerrainDefinition), false);
                    if (GUILayout.Button("새 지형 만들기")) terrain = CreateData<TerrainDefinition>("새 지형");
                    DrawDefinition(terrain);
                    EditorGUILayout.HelpBox("우클릭하면 칠한 지형만 지우고 기본 지형으로 되돌립니다. 구조물과 유닛은 유지됩니다.", MessageType.None);
                }
                else if (tool == 1)
                {
                    structure = (StructureDefinition)EditorGUILayout.ObjectField("구조물", structure, typeof(StructureDefinition), false);
                    if (GUILayout.Button("새 구조물 만들기")) structure = CreateData<StructureDefinition>("새 구조물");
                    DrawDefinition(structure);
                    EditorGUILayout.HelpBox("기준 칸에서 오른쪽·위쪽으로 크기만큼 배치합니다. 겹침·맵 밖 배치는 거절합니다. 우클릭은 구조물 전체를 삭제합니다.", MessageType.None);
                }
                else if (tool == 2)
                {
                    character = (CharacterDefinition)EditorGUILayout.ObjectField("유닛 편성", character, typeof(CharacterDefinition), false);
                    unitId = EditorGUILayout.TextField("유닛 ID 접두어", unitId);
                    if (character != null && GUILayout.Button("유닛 편성 에디터 열기")) CharacterEditorWindow.Edit(character);
                    EditorGUILayout.HelpBox("빈 칸을 클릭하면 배치합니다. 같은 ID는 숫자를 붙여 구분합니다. 배치 탭에서 영입 여부·기체·파일럿 보유·플래그 조건（AND/OR）과 적군 레벨 보정을 지정할 수 있습니다. 캠페인의 고정 아군은 영입된 기체·파일럿으로 출격하며 서로 다른 편성이면 알림 후 영구 교환합니다. 미지정 시 공통 기본값을 사용합니다.", MessageType.None);
                }
                else if (tool == 3)
                {
                    sequence = (EventSequence)EditorGUILayout.ObjectField("진입 이벤트", sequence, typeof(EventSequence), false);
                    EditorGUILayout.HelpBox("시작 칸에서 끝 칸까지 드래그해 직사각형 구역을 만듭니다. 아군이 구역에 들어오면 전투당 한 번 실행됩니다. 시작부터 구역 안에 있는 아군도 발동합니다.", MessageType.Info);
                }
                else if (tool == 4) EditorGUILayout.HelpBox("칸을 클릭하면 승리 조건을 목표 칸 도달로 설정합니다. 아군 전멸 시에는 패배합니다.", MessageType.Info);
                else if(tool==5)
                {
                    previewMovement = EditorGUILayout.IntSlider("임시 이동력", previewMovement, 0, 30);
                    EditorGUILayout.HelpBox("유닛을 클릭하면 해당 캐릭터 이동력으로, 빈 칸을 클릭하면 임시 이동력으로 범위를 계산합니다. 다른 유닛은 통과하지 않습니다. Esc로 해제합니다.", MessageType.Info);
                    if (previewStart.HasValue) EditorGUILayout.LabelField($"시작 {previewStart} · 도달 가능 {Mathf.Max(0, reachable.Count-1)}칸");
                }
                else
                {
                    deploymentId=EditorGUILayout.TextField("출격 ID 접두어",deploymentId);
                    EditorGUILayout.HelpBox("좌클릭·드래그로 파란 출격 칸을 추가하고 우클릭으로 제거합니다. 고정 유닛·구조물·이동 불가 칸과 겹칠 수 없습니다. 플레이 시 영입 아군을 선택해 배치합니다.",MessageType.Info);
                    var so=new SerializedObject(map);so.Update();MapPropertyLabels.Field(so,"testRecruitedAllies");if(so.ApplyModifiedProperties())RefreshMap();
                    EditorGUILayout.HelpBox("위 명단은 맵·스테이지 단독 테스트용입니다. 실제 캠페인에서는 캠페인의 초기 영입 아군과 플레이 중 영입한 아군만 표시됩니다. 파일럿 단독 에셋이 아닌 기체+파일럿 유닛 편성을 등록하세요.",MessageType.Info);
                }
            }
            else if (tab == 2)
            {
                var so = new SerializedObject(map); so.Update();
                foreach (string name in new[] { "units", "deploymentSlots", "testRecruitedAllies", "structures", "eventRegions", "terrainCells" }) MapPropertyLabels.Field(so, name);
                if (so.ApplyModifiedProperties()) RefreshMap();
            }
            else
            {
                if (GUILayout.Button("맵 검증 실행")) Validate();
                foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                EditorGUILayout.HelpBox("도달성은 유닛 점유를 제외한 지형·구조물 기준입니다. 공격의 장애물 차단과 고저차는 이번 버전에 포함하지 않습니다.", MessageType.Info);
            }
        }
        void DrawDefinition(UnityEngine.Object target)
        {
            if (target == null) return;
            UnityEditor.Editor.CreateCachedEditor(target, null, ref definitionInspector);
            EditorGUI.BeginChangeCheck(); definitionInspector.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) RefreshMap();
            if (GUILayout.Button("정의 저장")) AssetDatabase.SaveAssetIfDirty(target);
        }
        void DrawCanvas()
        {
            canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll);
            var rect = GUILayoutUtility.GetRect(Mathf.Max(1, map.width)*zoom+20, Mathf.Max(1, map.height)*zoom+20, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            var e = Event.current;
            Vector2Int CellAt(Vector2 p) => new(Mathf.FloorToInt((p.x-rect.x)/zoom), map.height-1-Mathf.FloorToInt((p.y-rect.y)/zoom));
            var mouseCell = CellAt(e.mousePosition); hover = map.Contains(mouseCell) && rect.Contains(e.mousePosition) ? mouseCell : null;
            if (cachedGrid == null) RefreshMap();
            for (int y = 0; y < Mathf.Clamp(map.height, 1, 64); y++) for (int x = 0; x < Mathf.Clamp(map.width, 1, 64); x++)
            {
                var c = new Vector2Int(x, y); var r = CellRect(rect, c); var t = map.TerrainAt(c);
                Color color = t != null ? t.color : new Color(.45f,.65f,.35f);
                if (display == 3) color = cachedGrid != null && cachedGrid.IsWalkable(c) ? new Color(.3f,.6f,.35f) : new Color(.7f,.2f,.2f);
                if (reachable.Contains(c)) color = Color.Lerp(color, Color.cyan, .65f);
                var slot=map.deploymentSlots.FirstOrDefault(s=>s!=null && s.cell==c);
                if(slot!=null) color=Color.Lerp(color,new Color(.1f,.4f,1),.75f);
                EditorGUI.DrawRect(r, color);
                var s = map.StructureAt(c); var u = map.units.FirstOrDefault(v => v != null && v.cell == c);
                string text = display == 1 ? (cachedGrid != null && cachedGrid.IsWalkable(c) ? cachedGrid.MovementCost(c).ToString() : "×")
                    : display == 2 ? $"명{cachedGrid?.AccuracyModifier(c):+0;-0;0}\n방{cachedGrid?.DefenseModifier(c):+0;-0;0}" : "";
                if (s != null) text += "\n구조";
                if(slot!=null) text="출격\n"+slot.id;
                if (u != null) text = (u.character != null && u.character.faction == Faction.Enemy ? "적" : "아군") + "\n" + u.id;
                if (map.eventRegions.Any(v => v != null && v.area.Contains(c))) text += "\n사건";
                if (map.victoryCondition == MapVictoryCondition.ReachCell && map.goalCell == c) text += "\n목표";
                var style = new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                GUI.Label(r, new GUIContent(text.Trim(), $"{x}, {y} · {(t != null ? t.displayName : "평지")}"), style);
            }
            if (hover.HasValue && tool == 1 && structure != null)
            {
                var c = hover.Value; var r = CellRect(rect, c); r.y -= (Mathf.Clamp(structure.size.y, 1, 64)-1)*zoom;
                r.width = Mathf.Clamp(structure.size.x, 1, 64)*zoom-2; r.height = Mathf.Clamp(structure.size.y, 1, 64)*zoom-2;
                EditorGUI.DrawRect(r, map.CanPlaceStructure(structure, c) ? new Color(0,1,1,.35f) : new Color(1,0,0,.45f));
            }
            if (hover.HasValue && regionStart.HasValue)
            {
                var a = CellRect(rect, Vector2Int.Min(regionStart.Value, hover.Value));
                var b = CellRect(rect, Vector2Int.Max(regionStart.Value, hover.Value));
                EditorGUI.DrawRect(Rect.MinMaxRect(a.x,b.y,b.xMax,a.yMax), new Color(1,1,0,.3f));
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && hover.HasValue)
            {
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    EndStroke(); Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("맵 그리기"); stroke = true;
                    if (tool == 3 && e.button == 0) regionStart = hover;
                    else ApplyBrush(hover.Value, e.button == 1);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && stroke && (e.button == 0 || e.button == 1))
                {
                    if (tool != 3) ApplyBrush(hover.Value, e.button == 1); e.Use();
                }
                else if (e.type == EventType.MouseUp && stroke)
                {
                    if (regionStart.HasValue && tool == 3 && sequence != null)
                    {
                        Record("이벤트 구역 배치"); var min = Vector2Int.Min(regionStart.Value, hover.Value); var max = Vector2Int.Max(regionStart.Value, hover.Value);
                        map.eventRegions.Add(new MapEventRegion { label = $"구역 {map.eventRegions.Count+1}", area = new RectInt(min, max-min+Vector2Int.one), sequence = sequence }); Changed();
                    }
                    EndStroke(); e.Use();
                }
            }
            EditorGUILayout.EndScrollView();
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) Repaint();
        }
        Rect CellRect(Rect origin, Vector2Int c) => new(origin.x+c.x*zoom, origin.y+(map.height-1-c.y)*zoom, zoom-2, zoom-2);
        void ApplyBrush(Vector2Int cell, bool erase)
        {
            if (!painted.Add(cell)) return;
            if (tool == 5)
            {
                previewStart = cell; var unit = map.units.FirstOrDefault(u => u?.character != null && u.cell == cell);
                reachable = cachedGrid == null ? new() : new(cachedGrid.Reachable(cell, unit != null ? unit.character.UnitMovement : previewMovement,
                    c => map.units.Any(u => u != null && u.cell == c && c != cell)).Keys); return;
            }
            Record("맵 그리기");
            if (tool == 0) map.Paint(cell, erase ? null : terrain);
            else if (tool == 1)
            {
                if (erase) map.structures.Remove(map.StructureAt(cell));
                else if (map.CanPlaceStructure(structure, cell)) map.structures.Add(new StructurePlacement { cell = cell, structure = structure });
            }
            else if (tool == 2)
            {
                if (erase) map.units.RemoveAll(u => u != null && u.cell == cell);
                else if (character != null && cachedGrid != null && cachedGrid.IsWalkable(cell) && map.StructureAt(cell) == null && !map.units.Any(u => u != null && u.cell == cell)
                    && !map.deploymentSlots.Any(s=>s!=null && s.cell==cell))
                {
                    string prefix = string.IsNullOrWhiteSpace(unitId) ? "유닛" : unitId.Trim(), id = prefix; int n = 2;
                    while (map.units.Any(u => u != null && u.id == id)) id = prefix + n++;
                    map.units.Add(new UnitPlacement { id = id, character = character, cell = cell });
                }
            }
            else if (tool == 3 && erase) map.eventRegions.RemoveAll(r => r != null && r.area.Contains(cell));
            else if (tool == 4) { map.victoryCondition = erase ? MapVictoryCondition.EliminateEnemies : MapVictoryCondition.ReachCell; map.goalCell = cell; }
            else if(tool==6)
            {
                if(erase) map.deploymentSlots.RemoveAll(s=>s!=null && s.cell==cell);
                else if(map.CanPlaceDeploymentSlot(cell))
                {
                    string prefix=string.IsNullOrWhiteSpace(deploymentId)?"출격":deploymentId.Trim(),id=prefix;int n=2;
                    while(map.units.Any(u=>u!=null && u.id==id) || map.deploymentSlots.Any(s=>s!=null && s.id==id))id=prefix+n++;
                    map.deploymentSlots.Add(new AllyDeploymentSlot {id=id,cell=cell});
                }
            }
            Changed();
        }
        void Validate() { errors = map.ValidateMap(); warnings = map.ReachabilityWarnings(); tab = 3; }
        void SaveValidated()
        {
            Validate();
            if (errors.Count > 0) { EditorUtility.DisplayDialog("맵 검증", "오류를 먼저 해결하거나 초안 저장을 사용하세요.", "확인"); return; }
            AssetDatabase.SaveAssetIfDirty(map); ShowNotification(new GUIContent(warnings.Count == 0 ? "검증 후 저장했습니다." : "경고가 있는 맵을 저장했습니다. 검증 탭을 확인하세요."));
        }
        void CreateMap()
        {
            var created = CreateData<BattleMapDefinition>("새 맵"); if (created == null) return;
            map = created; RefreshMap(); tab = 0;
        }
        static T CreateData<T>(string name) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject(name+" 저장", name, "asset", "저장 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return null;
            var data = CreateInstance<T>(); AssetDatabase.CreateAsset(data, path); Undo.RegisterCreatedObjectUndo(data, name+" 만들기"); Selection.activeObject = data; return data;
        }
        void CreateTerrainSet()
        {
            string path = EditorUtility.SaveFilePanelInProject("기본 지형 세트", "평지", "asset", "평지를 저장할 위치를 선택하세요. 같은 폴더에 숲·늪·바다도 만듭니다.");
            if (string.IsNullOrEmpty(path)) return;
            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            var names = new[] { "평지", "숲", "늪", "바다" }; var colors = new[] { new Color(.45f,.65f,.35f), new Color(.15f,.4f,.2f), new Color(.4f,.35f,.2f), new Color(.15f,.4f,.75f) };
            for (int i = 0; i < 4; i++)
            {
                var t = CreateInstance<TerrainDefinition>(); t.displayName = names[i]; t.color = colors[i]; t.walkable = i != 3;
                t.movementCost = i == 1 ? 2 : i == 2 ? 3 : 1; t.accuracyModifier = i == 1 ? -5 : 0; t.defenseModifier = i == 1 ? 2 : 0;
                AssetDatabase.CreateAsset(t, AssetDatabase.GenerateUniqueAssetPath(i == 0 ? path : folder+"/"+names[i]+".asset"));
                if (i == 0) { Record("기본 지형 지정"); map.defaultTerrain = t; terrain = t; Changed(); }
            }
            AssetDatabase.SaveAssetIfDirty(map);
        }
    }
}
