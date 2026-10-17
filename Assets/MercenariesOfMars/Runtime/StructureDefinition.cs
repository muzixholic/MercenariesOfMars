using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName = "화성의 용병단/구조물", fileName = "새 구조물")]
    public sealed class StructureDefinition : ScriptableObject
    {
        [InspectorName("구조물 이름")] public string displayName = "장애물";
        [InspectorName("차지하는 칸 수")] public Vector2Int size = Vector2Int.one;
        [InspectorName("이동 차단")] public bool blocksMovement = true;
        [InspectorName("표시 색상")] public Color color = new(.45f, .4f, .35f);
        [Min(.1f), InspectorName("임시 모델 높이")] public float height = 1;
        [InspectorName("모델 프리팹（선택, 원점은 바닥 중앙）")] public GameObject prefab;
    }
}
