using System;
using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/적군 레벨 보정 프리셋",fileName="새 적군 레벨 보정")]
    public sealed class EnemyLevelPreset : ScriptableObject
    {
        public string displayName="새 적군 레벨 보정";
        [Min(0)] public float multiplier=1;
        public int offset;
        [Min(0)] public float experienceMultiplier=1;
        public int experienceOffset;
        public int CalculateExperience(int baseExperience)=>CalculateExperience(baseExperience,experienceMultiplier,experienceOffset);
        public static int CalculateExperience(int baseExperience,float multiplier,int offset)
        {
            double ratio=float.IsFinite(multiplier)?Math.Round(Math.Max(0,multiplier),6):1;
            double value=Math.Floor(Math.Round(Math.Max(0,baseExperience)*ratio+offset,6)+.5);
            return (int)Math.Clamp(value,0,int.MaxValue);
        }
        [Range(1,99)] public int minimumLevel=1;
        [Range(1,99)] public int maximumLevel=99;
        public int Calculate(double allyAverage)
        {
            double average=double.IsFinite(allyAverage)?Math.Clamp(allyAverage,1,99):1;
            double ratio=float.IsFinite(multiplier)?Math.Round(Math.Max(0,multiplier),6):1;
            double value=Math.Floor(Math.Round(average*ratio+offset,6)+.5);
            int min=Mathf.Clamp(minimumLevel,1,99),max=Mathf.Clamp(maximumLevel,min,99);
            return (int)Math.Clamp(value,min,max);
        }
        void OnValidate()
        {
            multiplier=float.IsFinite(multiplier)?Mathf.Max(0,multiplier):1;
            experienceMultiplier=float.IsFinite(experienceMultiplier)?Mathf.Max(0,experienceMultiplier):1;
            minimumLevel=Mathf.Clamp(minimumLevel,1,99); maximumLevel=Mathf.Clamp(maximumLevel,minimumLevel,99);
        }
    }
}
