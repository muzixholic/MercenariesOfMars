using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName = "화성의 용병단/지형", fileName = "새 지형")]
    public sealed class TerrainDefinition : ScriptableObject
    {
        [InspectorName("지형 이름")] public string displayName = "평지";
        [InspectorName("표시 색상")] public Color color = new(.45f, .65f, .35f);
        [InspectorName("이동 가능")] public bool walkable = true;
        [Min(1), InspectorName("진입 이동 비용")] public int movementCost = 1;
        [Range(-100, 100), InspectorName("이 칸에서 공격 시 명중 보정（%p）")] public int accuracyModifier;
        [InspectorName("이 칸에서 피격 시 방어력 보정")] public int defenseModifier;
        [InspectorName("표면 재질（선택）")] public Material material;
        [TextArea, InspectorName("설명")] public string description;
    }
}
