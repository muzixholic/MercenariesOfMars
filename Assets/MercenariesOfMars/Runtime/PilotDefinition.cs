using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/파일럿",fileName="새 파일럿")]
    public sealed class PilotDefinition : ScriptableObject
    {
        public string displayName="새 파일럿";
        [Range(1,99)] public int level=1;
        [Min(0)] public int experience;
        [Range(1,999)] public int attack=10;
        [Range(1,999)] public int defense=3;
        [Range(1,999)] public int accuracy=100;
        [Range(1,999)] public int evasion=1;
        [HideInInspector] public PilotGrowthPreset growthPreset;
        public PilotGrowthPreset attackGrowth;
        public PilotGrowthPreset defenseGrowth;
        public PilotGrowthPreset accuracyGrowth;
        public PilotGrowthPreset evasionGrowth;
        public List<string> ValidateGrowth()
        {
            var errors=new List<string>();
            var presets=new[]{attackGrowth??growthPreset,defenseGrowth??growthPreset,accuracyGrowth??growthPreset,evasionGrowth??growthPreset};
            var names=new[]{"공격력","방어력","명중력","회피력"};
            for(int i=0;i<presets.Length;i++)
                if(presets[i]!=null) foreach(var error in presets[i].ValidatePreset()) errors.Add(names[i]+": "+error);
            return errors;
        }
        [Range(1,999)] public int attackAt99=108;
        [Range(1,999)] public int defenseAt99=101;
        [Range(1,999)] public int accuracyAt99=198;
        [Range(1,999)] public int evasionAt99=99;
        public List<CharacterPortrait> portraits=new();
        [TextArea] public string description;
        public Sprite FindPortrait(string id)=>string.IsNullOrEmpty(id)?null:portraits?.FirstOrDefault(p=>p!=null && p.id==id)?.sprite;
        public PilotStats StatsAtLevel(int targetLevel)
        {
            int Value(int start,int end,PilotGrowthPreset preset,AnimationCurve legacyCurve)
            {
                start=Mathf.Clamp(start,1,999); end=Mathf.Clamp(end,start,999);
                float t=preset!=null?preset.Evaluate(targetLevel):growthPreset!=null?growthPreset.Evaluate(legacyCurve,targetLevel):0;
                return Mathf.Clamp(Mathf.FloorToInt(start+(end-start)*t+.5f),1,999);
            }
            return new PilotStats(Value(attack,attackAt99,attackGrowth,growthPreset!=null?growthPreset.curve:null),
                Value(defense,defenseAt99,defenseGrowth,growthPreset!=null?growthPreset.defense:null),
                Value(accuracy,accuracyAt99,accuracyGrowth,growthPreset!=null?growthPreset.accuracy:null),
                Value(evasion,evasionAt99,evasionGrowth,growthPreset!=null?growthPreset.evasion:null));
        }
        void OnValidate()
        {
            level=Mathf.Clamp(level,1,99); experience=Mathf.Max(0,experience);
            attack=Mathf.Clamp(attack,1,999); defense=Mathf.Clamp(defense,1,999); accuracy=Mathf.Clamp(accuracy,1,999); evasion=Mathf.Clamp(evasion,1,999);
            attackAt99=Mathf.Clamp(attackAt99,attack,999); defenseAt99=Mathf.Clamp(defenseAt99,defense,999);
            accuracyAt99=Mathf.Clamp(accuracyAt99,accuracy,999); evasionAt99=Mathf.Clamp(evasionAt99,evasion,999);
            portraits??=new List<CharacterPortrait>(); CharacterPortrait.EnsureIds(portraits);
        }
    }
    public readonly struct PilotStats
    {
        public readonly int Attack,Defense,Accuracy,Evasion;
        public PilotStats(int attack,int defense,int accuracy,int evasion)
        { Attack=attack; Defense=defense; Accuracy=accuracy; Evasion=evasion; }
    }
}
