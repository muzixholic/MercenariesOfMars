using System;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum MechUpgradeType
    {
        [InspectorName("HP")] Hp,
        [InspectorName("EN")] Energy,
        [InspectorName("장갑치")] Armor,
        [InspectorName("조준치")] Targeting,
        [InspectorName("운동성")] Mobility,
        [InspectorName("무장")] Weapon
    }

    public static class MechUpgradeCost
    {
        public static int StepPrice(MechUpgradeType type)=>type switch
        {
            MechUpgradeType.Weapon=>5000,
            MechUpgradeType.Hp or MechUpgradeType.Energy or MechUpgradeType.Armor
                or MechUpgradeType.Targeting or MechUpgradeType.Mobility=>3000,
            _=>throw new ArgumentOutOfRangeException(nameof(type),"올바른 개조 항목을 선택하세요.")
        };
        public static long NextStage(MechUpgradeType type,int currentStage)
        {
            int price=StepPrice(type);
            if(currentStage<0 || currentStage==int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(currentStage),"다음 단계가 있는 0 이상의 개조 단계를 지정하세요.");
            return ((long)currentStage+1)*price;
        }
        public static long Total(MechUpgradeType type,int fromStage,int toStage)
        {
            int price=StepPrice(type);
            if(fromStage<0 || toStage<fromStage)
                throw new ArgumentOutOfRangeException(nameof(toStage),"개조 목표 단계는 현재 단계 이상이어야 합니다.");
            // 해금 상한이 커져도 중간 계산이 넘치지 않으며 표현 불가능한 금액을 할인하지 않는다.
            decimal total=((decimal)fromStage+1+toStage)*(toStage-fromStage)/2*price;
            if(total>long.MaxValue)throw new OverflowException("개조 비용이 자금의 정수 범위를 초과합니다.");
            return (long)total;
        }
    }
}
