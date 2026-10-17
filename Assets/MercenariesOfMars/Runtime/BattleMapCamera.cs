using UnityEngine;

namespace MercenariesOfMars
{
    // 맵 조작과 클릭 판정을 분리해 드래그가 전투 행동을 실행하지 않게 한다.
    public sealed class BattleMapCamera : MonoBehaviour
    {
        [Min(.1f), InspectorName("최소 직교 크기")] public float minimumSize = 2;
        [Min(.1f), InspectorName("최대 직교 크기")] public float maximumSize = 20;
        [Min(.01f), InspectorName("휠 확대 감도")] public float zoomSensitivity = .15f;
        [Min(1), InspectorName("드래그 판정 거리（픽셀）")] public float dragThreshold = 8;
        Camera targetCamera;
        GridBoard board;
        Vector2 pointerStart, pointerPrevious;
        public bool IsConfigured { get; private set; }
        public bool IsTrackingPointer { get; private set; }
        public bool IsDragging { get; private set; }
        public bool Configure(Camera camera, GridBoard grid)
        {
            CancelPointer(); targetCamera = camera; board = grid;
            IsConfigured = camera != null && grid != null && camera.transform != grid.transform && !grid.transform.IsChildOf(camera.transform);
            if (!IsConfigured) return false;
            camera.rect = new Rect(0, 0, 1, 1);
            var center = grid.transform.TransformPoint(new Vector3(grid.width * grid.cellSize / 2, 0, grid.height * grid.cellSize / 2));
            if (PlanePoint(camera.ViewportPointToRay(new Vector3(.5f,.5f,0)), out var current)) camera.transform.position += center - current;
            else camera.transform.LookAt(center, grid.transform.up);
            if (camera.orthographic)
            {
                float size = minimumSize;
                foreach (float x in new[] { 0f, grid.width * grid.cellSize })
                    foreach (float z in new[] { 0f, grid.height * grid.cellSize })
                        foreach (float y in new[] { 0f, grid.cellSize * 1.6f })
                        {
                            var local = camera.transform.InverseTransformPoint(grid.transform.TransformPoint(new Vector3(x,y,z)));
                            size = Mathf.Max(size, Mathf.Abs(local.y) * 1.15f, Mathf.Abs(local.x) / Mathf.Max(.1f,camera.aspect) * 1.15f);
                        }
                camera.orthographicSize = Mathf.Clamp(size, Mathf.Max(.1f,minimumSize), Mathf.Max(minimumSize,maximumSize));
            }
            return true;
        }
        bool PlanePoint(Ray ray, out Vector3 point)
        {
            point = default;
            if (board == null || !new Plane(board.transform.up,board.transform.position).Raycast(ray,out float distance)) return false;
            point = ray.GetPoint(distance); return true;
        }
        public void Zoom(float wheelDelta)
        {
            if (!IsConfigured || Mathf.Approximately(wheelDelta,0) || IsTrackingPointer) return;
            float factor = Mathf.Exp(-Mathf.Clamp(wheelDelta / 120f,-10,10) * Mathf.Max(.01f,zoomSensitivity));
            if (targetCamera.orthographic)
                targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize * factor, Mathf.Max(.1f,minimumSize), Mathf.Max(minimumSize,maximumSize));
            else targetCamera.fieldOfView = Mathf.Clamp(targetCamera.fieldOfView * factor,20,75);
        }
        public void BeginPointer(Vector2 position)
        { pointerStart = pointerPrevious = position; IsTrackingPointer = true; IsDragging = false; }
        public void DragPointer(Vector2 position)
        {
            if (!IsTrackingPointer) return;
            if (!IsDragging && Vector2.Distance(pointerStart,position) < Mathf.Max(1,dragThreshold)) return;
            var previous = IsDragging ? pointerPrevious : pointerStart;
            IsDragging = true;
            if (IsConfigured && PlanePoint(targetCamera.ScreenPointToRay(previous),out var from)
                && PlanePoint(targetCamera.ScreenPointToRay(position),out var to))
            {
                targetCamera.transform.position += from - to;
                // 맵을 잃어버리지 않도록 보드 주변에서만 이동한다.
                if (PlanePoint(targetCamera.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var center))
                {
                    var local = board.transform.InverseTransformPoint(center);
                    var bounded = new Vector3(Mathf.Clamp(local.x,-board.width*board.cellSize*.5f,board.width*board.cellSize*1.5f),local.y,
                        Mathf.Clamp(local.z,-board.height*board.cellSize*.5f,board.height*board.cellSize*1.5f));
                    targetCamera.transform.position += board.transform.TransformPoint(bounded) - center;
                }
            }
            pointerPrevious = position;
        }
        public bool EndPointer(Vector2 position)
        {
            if (!IsTrackingPointer) return false;
            DragPointer(position); bool click = !IsDragging; CancelPointer(); return click;
        }
        public void CancelPointer() { IsTrackingPointer = false; IsDragging = false; }
        void OnDisable() => CancelPointer();
    }
}
