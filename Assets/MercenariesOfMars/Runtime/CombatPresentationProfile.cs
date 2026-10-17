using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName = "화성의 용병단/전투 연출", fileName = "새 전투 연출")]
    public sealed class CombatPresentationProfile : ScriptableObject
    {
        [Min(.05f), InspectorName("공격 효과 시간")] public float attackSeconds = .65f;
        [Min(.05f), InspectorName("피해 표시 시간")] public float damageSeconds = .9f;
        [Min(.05f), InspectorName("공격 사이 간격")] public float pauseSeconds = .3f;
        [Min(.1f), InspectorName("효과 크기")] public float effectScale = 1;
        [InspectorName("공격 효과 색상")] public Color effectColor = new(1, .72f, .16f);
        [InspectorName("피해 표시 색상")] public Color damageColor = new(1, .35f, .25f);
        [InspectorName("공격 효과 프리팹（선택）"), Tooltip("대상 위치에 생성합니다. 해당 공격 연출이 끝나면 제거합니다.")]
        public GameObject attackEffectPrefab;
    }
}
