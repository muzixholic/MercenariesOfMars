using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    public sealed class GridBoard : MonoBehaviour
    {
        [InspectorName("맵 데이터（선택）")] public BattleMapDefinition map;
        BattleMapDefinition loadedMap;
        GameObject mapView;
        public void LoadMap(bool rebuild = false)
        {
            if (map == null) return;
            width = map.width; height = map.height; cellSize = map.cellSize;
            if (!rebuild && loadedMap == map && mapView != null) return;
            if (mapView != null) { mapView.SetActive(false); Destroy(mapView); }
            if (lines != null) { lines.SetActive(false); Destroy(lines); lines = null; }
            if (lineMaterial != null) Destroy(lineMaterial);
            loadedMap = map;
            mapView = new GameObject("맵 지형과 구조물");
            mapView.transform.SetParent(transform, false); mapView.AddComponent<BattleMapView>().Build(map);
        }
        [Min(1), InspectorName("가로 칸 수")] public int width = 10;
        [Min(1), InspectorName("세로 칸 수")] public int height = 10;
        [Min(0.1f), InspectorName("칸 크기")] public float cellSize = 1;
        [InspectorName("격자 표시")] public bool showGrid = true;
        [InspectorName("격자 색상")] public Color gridColor = new(0.3f, 0.8f, 1f, 0.8f);
        [InspectorName("진입 불가 칸")] public List<Vector2Int> blockedCells = new();
        [InspectorName("이동 가능 영역 색상")] public Color movementColor = new(.08f, .4f, .7f, 1);
        [InspectorName("공격 범위 색상")] public Color attackColor = new(.8f, .25f, .08f, 1);
        [InspectorName("적 공격 범위 색상")] public Color incomingAttackColor = new(.65f, .12f, .12f, 1);
        GameObject incomingMarkers;
        readonly List<Material> markerMaterials = new();
        public Vector2Int? AttackerMarkerCell { get; private set; }
        public Vector2Int? TargetMarkerCell { get; private set; }
        public void SetIncomingAttackHighlights(IEnumerable<Vector2Int> cells, Vector2Int attacker, Vector2Int target)
        {
            SetHighlights(cells, incomingAttackColor, "적 공격 범위（임시）");
            if (incomingMarkers != null && AttackerMarkerCell == attacker && TargetMarkerCell == target) return;
            ClearIncomingMarkers();
            incomingMarkers = new GameObject("공격자와 피격 대상 표시"); incomingMarkers.transform.SetParent(transform, false);
            AttackerMarkerCell = attacker; TargetMarkerCell = target;
            AddMarker(attacker, new Color(1, .85f, .1f), "공격자");
            AddMarker(target, new Color(.15f, 1, 1), "피격 대상");
        }
        void AddMarker(Vector2Int cell, Color color, string label)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>(); line.transform.SetParent(incomingMarkers.transform, false);
            line.useWorldSpace = false; line.loop = true; line.positionCount = 4; line.startWidth = line.endWidth = cellSize * .07f;
            float x = cell.x * cellSize, z = cell.y * cellSize, inset = cellSize * .1f;
            line.SetPositions(new[] { new Vector3(x+inset,.09f,z+inset), new Vector3(x+inset,.09f,z+cellSize-inset),
                new Vector3(x+cellSize-inset,.09f,z+cellSize-inset), new Vector3(x+cellSize-inset,.09f,z+inset) });
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            material.color = color; markerMaterials.Add(material); line.sharedMaterial = material;
        }
        void ClearIncomingMarkers()
        {
            if (incomingMarkers != null) { incomingMarkers.SetActive(false); Destroy(incomingMarkers); }
            foreach (var material in markerMaterials) if (material != null) Destroy(material);
            markerMaterials.Clear(); incomingMarkers = null; AttackerMarkerCell = null; TargetMarkerCell = null;
        }
        readonly HashSet<Vector2Int> highlightedCells = new();
        public IReadOnlyCollection<Vector2Int> HighlightedCells => highlightedCells;
        GameObject highlights;
        Mesh highlightMesh;
        Material highlightMaterial;
        public void SetMovementHighlights(IEnumerable<Vector2Int> cells) => SetHighlights(cells, movementColor, "이동 가능 영역（임시）");
        public void SetAttackHighlights(IEnumerable<Vector2Int> cells) => SetHighlights(cells, attackColor, "공격 범위（임시）");
        void SetHighlights(IEnumerable<Vector2Int> cells, Color color, string label)
        {
            var next = new HashSet<Vector2Int>(cells);
            if (highlightedCells.SetEquals(next) && (highlightMaterial == null || highlightMaterial.color == color)) return;
            ClearMovementHighlights();
            highlightedCells.UnionWith(next);
            if (next.Count == 0) return;
            highlights = new GameObject(label); highlights.transform.SetParent(transform, false);
            highlightMesh = new Mesh { name = "범위 표시 칸", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var cell in next)
            {
                int start = vertices.Count;
                float x = cell.x * cellSize, z = cell.y * cellSize, margin = cellSize * .06f;
                vertices.Add(new Vector3(x + margin, .04f, z + margin));
                vertices.Add(new Vector3(x + margin, .04f, z + cellSize - margin));
                vertices.Add(new Vector3(x + cellSize - margin, .04f, z + cellSize - margin));
                vertices.Add(new Vector3(x + cellSize - margin, .04f, z + margin));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            highlightMesh.SetVertices(vertices); highlightMesh.SetTriangles(triangles, 0); highlightMesh.RecalculateBounds();
            highlights.AddComponent<MeshFilter>().sharedMesh = highlightMesh;
            highlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            highlightMaterial.color = color;
            highlights.AddComponent<MeshRenderer>().sharedMaterial = highlightMaterial;
        }
        public void ClearMovementHighlights()
        {
            ClearIncomingMarkers();
            highlightedCells.Clear();
            if (highlights != null) { highlights.SetActive(false); Destroy(highlights); }
            if (highlightMesh != null) Destroy(highlightMesh);
            if (highlightMaterial != null) Destroy(highlightMaterial);
            highlights = null; highlightMesh = null; highlightMaterial = null;
        }
        void OnDisable() => ClearMovementHighlights();
        GameObject lines;
        Material lineMaterial;
        public BattleGrid CreateGrid() => map != null ? map.CreateGrid() : new(width, height, blockedCells);
        public Vector3 CellToWorld(Vector2Int cell) => transform.TransformPoint(new Vector3((cell.x + .5f) * cellSize, 0, (cell.y + .5f) * cellSize));
        public Vector2Int WorldToCell(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
        }
        public bool RayToCell(Ray ray, out Vector2Int cell)
        {
            cell = default;
            if (!new Plane(transform.up, transform.position).Raycast(ray, out float distance)) return false;
            cell = WorldToCell(ray.GetPoint(distance));
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }
        void Start()
        {
            LoadMap(); BuildLines();
        }
        void BuildLines()
        {
            if (lines != null) return;
            lines = new GameObject("전투 격자"); lines.transform.SetParent(transform, false);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) { Debug.LogWarning("격자 표시용 셰이더를 찾지 못했습니다.", this); return; }
            lineMaterial = new Material(shader);
            lineMaterial.color = gridColor;
            for (int x = 0; x <= width; x++) AddLine(new(x * cellSize, .025f, 0), new(x * cellSize, .025f, height * cellSize));
            for (int z = 0; z <= height; z++) AddLine(new(0, .025f, z * cellSize), new(width * cellSize, .025f, z * cellSize));
        }
        void AddLine(Vector3 a, Vector3 b)
        {
            var line = new GameObject("격자 선").AddComponent<LineRenderer>();
            line.transform.SetParent(lines.transform, false); line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial; line.startWidth = line.endWidth = .025f;
            line.positionCount = 2; line.SetPosition(0, a); line.SetPosition(1, b);
        }
        void LateUpdate() { if (lines == null && loadedMap != null) BuildLines(); if (lines != null) lines.SetActive(showGrid); }
        public void ToggleGrid() => showGrid = !showGrid;
        void OnDestroy() { ClearMovementHighlights(); if (lineMaterial != null) Destroy(lineMaterial); }
        void OnValidate() { width = Mathf.Max(1, width); height = Mathf.Max(1, height); cellSize = Mathf.Max(.1f, cellSize); }
        void OnDrawGizmosSelected()
        {
            if (!showGrid) return;
            Gizmos.color = gridColor;
            for (int x = 0; x <= width; x++) Gizmos.DrawLine(transform.TransformPoint(new Vector3(x * cellSize, 0, 0)), transform.TransformPoint(new Vector3(x * cellSize, 0, height * cellSize)));
            for (int z = 0; z <= height; z++) Gizmos.DrawLine(transform.TransformPoint(new Vector3(0, 0, z * cellSize)), transform.TransformPoint(new Vector3(width * cellSize, 0, z * cellSize)));
        }
    }
}
