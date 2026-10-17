using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/캠페인",fileName="새 캠페인")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [InspectorName("캠페인 이름")] public string displayName="화성의 용병단";
        [InspectorName("첫 스테이지")] public StageDefinition firstStage;
        [Min(0), InspectorName("시작 자금")] public long initialFunds;
    }
}
