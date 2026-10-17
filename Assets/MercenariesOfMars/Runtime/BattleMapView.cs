using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    // 저장된 씬을 건드리지 않고 실행 중에만 지형과 구조물을 만든다.
    public sealed class BattleMapView : MonoBehaviour
    {
        readonly List<Object> owned = new();
        public void Build(BattleMapDefinition map)
        {
            foreach (var group in Enumerable.Range(0, map.width * map.height)
                .Select(i => new Vector2Int(i % map.width, i / map.width)).GroupBy(map.TerrainAt))
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
                foreach (var c in group)
                {
                    int n = vertices.Count; float x = c.x * map.cellSize, z = c.y * map.cellSize, s = map.cellSize;
                    vertices.AddRange(new[] { new Vector3(x, .005f, z), new Vector3(x, .005f, z+s), new Vector3(x+s, .005f, z+s), new Vector3(x+s, .005f, z) });
                    triangles.AddRange(new[] { n, n+1, n+2, n, n+2, n+3 });
                    uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                }
                var mesh = new Mesh { name = "맵 표면" }; owned.Add(mesh);
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var tile = new GameObject(group.Key != null ? group.Key.displayName : "평지"); tile.transform.SetParent(transform, false);
                tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                tile.AddComponent<MeshRenderer>().sharedMaterial = group.Key != null && group.Key.material != null ? group.Key.material
                    : NewMaterial(group.Key != null ? group.Key.color : new Color(.45f, .65f, .35f));
            }
            foreach (var placement in map.structures.Where(s => s?.structure != null))
            {
                var d = placement.structure;
                var view = d.prefab != null ? Instantiate(d.prefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.name = d.displayName; view.transform.SetParent(transform, false);
                view.transform.localPosition = new Vector3((placement.cell.x+d.size.x*.5f)*map.cellSize, d.prefab == null ? d.height*.5f : 0,
                    (placement.cell.y+d.size.y*.5f)*map.cellSize);
                if (d.prefab == null)
                {
                    view.transform.localScale = new Vector3(d.size.x*map.cellSize*.9f, d.height, d.size.y*map.cellSize*.9f);
                    view.GetComponent<Renderer>().sharedMaterial = NewMaterial(d.color);
                }
            }
        }
        Material NewMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            material.color = color; owned.Add(material); return material;
        }
        void OnDestroy() { foreach (var item in owned) if (item != null) Destroy(item); }
    }
}
