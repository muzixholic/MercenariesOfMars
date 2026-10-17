using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/파일럿 성장 설정",fileName="성장설정")]
    public sealed class PilotProgressionSettings : ScriptableObject
    {
        public List<PilotGrowthPreset> growthPresets=new();
        public List<EnemyLevelPreset> enemyLevelPresets=new();
        public PilotGrowthPreset newPilotGrowth;
        public EnemyLevelPreset defaultEnemyLevel;
        public static PilotProgressionSettings LoadDefault()=>Resources.Load<PilotProgressionSettings>("MercenariesOfMars/성장설정");
    }
    public static class BattleUnitFactory
    {
        public static List<BattleUnit> Create(IReadOnlyList<UnitPlacement> placements,PilotProgressionSettings settings=null)
        {
            settings=settings!=null?settings:PilotProgressionSettings.LoadDefault();
            double sum=0; int count=0;
            foreach(var p in placements)
                if(p?.character!=null && p.character.faction==Faction.Ally)
                { sum+=Mathf.Clamp(p.character.pilot!=null?p.character.pilot.level:p.character.level,1,99); count++; }
            var units=new List<BattleUnit>();
            foreach(var p in placements)
            {
                if(p?.character==null) throw new System.ArgumentException("유닛 편성을 지정하세요.");
                int? level=null;
                var preset=p.character.faction==Faction.Enemy
                    ?(p.enemyLevelPreset!=null?p.enemyLevelPreset:settings!=null?settings.defaultEnemyLevel:null):null;
                if(count>0 && p.character.faction==Faction.Enemy && p.character.pilot!=null && p.autoEnemyLevel)
                {
                    if(preset!=null) level=preset.Calculate(sum/count);
                }
                units.Add(new BattleUnit(p.id,p.character,p.cell,level,preset));
            }
            return units;
        }
    }
}
