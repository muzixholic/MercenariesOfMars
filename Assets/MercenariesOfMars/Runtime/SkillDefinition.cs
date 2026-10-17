using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName = "화성의 용병단/공격 스킬", fileName = "새 스킬")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [InspectorName("스킬 이름")] public string displayName = "새 스킬";
        [InspectorName("무장 이미지（선택 사항）")] public Sprite icon;
        [Min(0), InspectorName("공격력")] public int power = 10;
        [Range(-100, 100), InspectorName("명중 보정 (%p)")] public int accuracyModifier;
        [Min(1), InspectorName("최소 사거리")] public int minRange = 1;
        [Min(1), InspectorName("최대 사거리")] public int maxRange = 1;
        [Min(0), InspectorName("소비 에너지")] public int energyCost;
        [Min(0), InspectorName("최대 탄수"), Tooltip("0이면 탄수 제한 없이 사용합니다.")] public int maxAmmo;
        [Min(0), InspectorName("현재 탄수"), Tooltip("전투 시작 시 잔탄입니다. 전투 중 잔탄은 유닛별로 관리하며 이 에셋은 변경하지 않습니다.")] public int currentAmmo;
        [Min(0), InspectorName("무게")] public int weight;
        [InspectorName("이동 후 사용 가능"), Tooltip("끄면 해당 턴에 이동한 유닛은 이 스킬을 사용할 수 없습니다.")]
        public bool canUseAfterMoving = true;
        [InspectorName("전투 연출（미지정 시 기본 효과）")] public CombatPresentationProfile presentation;
        [TextArea, InspectorName("설명")] public string description;
        public List<string> ValidateSkill()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(displayName)) errors.Add("스킬 이름을 입력하세요.");
            if (accuracyModifier < -100 || accuracyModifier > 100) errors.Add("명중 보정은 -100~100 사이여야 합니다.");
            if (power < 0) errors.Add("공격력은 0 이상이어야 합니다.");
            if (minRange < 1 || maxRange < minRange) errors.Add("사거리는 1 이상이고 최대값은 최소값 이상이어야 합니다.");
            if (energyCost < 0) errors.Add("소비 에너지는 0 이상이어야 합니다.");
            if(maxAmmo<0 || currentAmmo<0 || (maxAmmo>0 && currentAmmo>maxAmmo)) errors.Add("탄수는 0 이상이고 현재 탄수는 최대 탄수 이하여야 합니다.");
            if(weight<0) errors.Add("무장 무게는 0 이상이어야 합니다.");
            return errors;
        }
        void OnValidate()
        {
            maxAmmo=Mathf.Max(0,maxAmmo); currentAmmo=maxAmmo>0?Mathf.Clamp(currentAmmo,0,maxAmmo):0; weight=Mathf.Max(0,weight);
            accuracyModifier = Mathf.Clamp(accuracyModifier, -100, 100);
            power = Mathf.Max(0, power); minRange = Mathf.Max(1, minRange);
            maxRange = Mathf.Max(minRange, maxRange); energyCost = Mathf.Max(0, energyCost);
        }
    }
    // 전투 시작 시 스킬 수치를 복사하여 원본 에셋과 전투 상태를 분리한다.
    public sealed class BattleSkill
    {
        public SkillDefinition Definition { get; }
        public string Name { get; }
        public Sprite Icon { get; }
        public int Power { get; }
        public int AccuracyModifier { get; }
        public int MinRange { get; }
        public int MaxRange { get; }
        public int EnergyCost { get; }
        public int MaxAmmo { get; }
        public int CurrentAmmo { get; private set; }
        public int Weight { get; }
        public bool HasAmmo=>MaxAmmo==0 || CurrentAmmo>0;
        public string AmmoText=>MaxAmmo==0?"탄수 제한 없음":$"잔탄 {CurrentAmmo}/{MaxAmmo}";
        internal void ConsumeAmmo() { if(MaxAmmo>0 && CurrentAmmo>0) CurrentAmmo--; }
        public bool CanUseAfterMoving { get; }
        public BattleSkill(SkillDefinition definition)
        {
            if (definition == null) throw new System.ArgumentNullException(nameof(definition));
            MaxAmmo=Mathf.Max(0,definition.maxAmmo); CurrentAmmo=MaxAmmo>0?Mathf.Clamp(definition.currentAmmo,0,MaxAmmo):0;
            Weight=Mathf.Max(0,definition.weight);
            Definition = definition; Name = definition.displayName; Icon=definition.icon;
            CanUseAfterMoving = definition.canUseAfterMoving;
            AccuracyModifier = Mathf.Clamp(definition.accuracyModifier, -100, 100);
            Power = Mathf.Max(0, definition.power); MinRange = Mathf.Max(1, definition.minRange);
            MaxRange = Mathf.Max(MinRange, definition.maxRange); EnergyCost = Mathf.Max(0, definition.energyCost);
        }
        public bool IsInRange(Vector2Int from, Vector2Int to)
        {
            int distance = BattleGrid.Distance(from, to);
            return distance >= MinRange && distance <= MaxRange;
        }
    }
}
