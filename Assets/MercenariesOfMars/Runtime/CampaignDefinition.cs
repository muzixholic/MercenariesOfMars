using UnityEngine;
using System.Collections.Generic;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/캠페인",fileName="새 캠페인")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        [InspectorName("캠페인 이름")] public string displayName="화성의 용병단";
        [InspectorName("첫 스테이지")] public StageDefinition firstStage;
        [Min(0), InspectorName("시작 자금")] public long initialFunds;
        [Min(0), InspectorName("시작 최대 개조 단계")] public int initialMaxUpgradeStage=10;
        [InspectorName("초기 영입 아군（유닛 편성）")] public List<CharacterDefinition> initialAllies=new();
        [InspectorName("상점 판매 기체")] public List<MechDefinition> shopMechs=new();
        [InspectorName("상점 판매 무장")] public List<SkillDefinition> shopWeapons=new();
        [InspectorName("상점 영입 파일럿")] public List<PilotDefinition> shopPilots=new();
        void OnValidate()=>initialMaxUpgradeStage=Mathf.Max(0,initialMaxUpgradeStage);
    }
}
