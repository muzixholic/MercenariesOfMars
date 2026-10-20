using System;
using System.Collections.Generic;
using System.Linq;

namespace MercenariesOfMars
{
    // 선택 결과만 보관하고 맵·영입 목록 에셋은 변경하지 않는다.
    public sealed class AllyDeploymentPlan : IDisposable
    {
        readonly BattleMapDefinition map;
        readonly RosterDeployment resolution;
        readonly bool ownsResolution;
        readonly Func<CharacterDefinition,bool> isBlocked;
        public IReadOnlyList<UnitPlacement> FixedPlacements=>resolution.Placements.AsReadOnly();
        readonly Dictionary<string,CharacterDefinition> assignments=new();
        public IReadOnlyList<CharacterDefinition> Candidates { get; }
        public IReadOnlyList<AllyDeploymentSlot> Slots { get; }
        public IReadOnlyDictionary<string,CharacterDefinition> Assignments {get;}
        public static bool SamePilotOrUnit(CharacterDefinition a,CharacterDefinition b)
            =>RosterDeployment.SharesParts(a,b);
        public AllyDeploymentPlan(BattleMapDefinition map,IEnumerable<CharacterDefinition> recruited,Func<string,bool> getFlag=null,RosterDeployment resolved=null,Func<CharacterDefinition,bool> isBlocked=null)
        {
            this.map=map??throw new ArgumentNullException(nameof(map));
            this.isBlocked=isBlocked;
            ownsResolution=resolved==null;resolution=resolved??new RosterDeployment(map,recruited,getFlag);
            Assignments=new System.Collections.ObjectModel.ReadOnlyDictionary<string,CharacterDefinition>(assignments);
            var errors=map.ValidateMap();if(errors.Count>0) throw new ArgumentException(string.Join("\n",errors));
            Slots=map.deploymentSlots.Select(s=>new AllyDeploymentSlot {id=s.id,cell=s.cell}).ToList().AsReadOnly();
            var allies=new List<CharacterDefinition>();
            foreach(var ally in resolution.Roster)
            {
                if(ally==null || ally.faction!=Faction.Ally || ally.ValidateAssignment().Count>0) throw new ArgumentException("영입 목록의 아군 편성을 확인하세요.");
                if(!allies.Any(a=>SamePilotOrUnit(a,ally))) allies.Add(ally);
            }
            Candidates=allies.AsReadOnly();
        }
        public void Dispose(){if(ownsResolution)resolution.Dispose();}
        public bool CanAssign(string id,CharacterDefinition ally)
            =>Slots.Any(s=>s.id==id) && ally!=null && Candidates.Contains(ally) && ally.faction==Faction.Ally && ally.ValidateAssignment().Count==0 && isBlocked?.Invoke(ally)!=true
                && !resolution.Placements.Any(u=>u?.character!=null && SamePilotOrUnit(u.character,ally))
                && !assignments.Any(p=>p.Key!=id && SamePilotOrUnit(p.Value,ally));
        public bool Assign(string id,CharacterDefinition ally)
        {if(!CanAssign(id,ally)) return false;assignments[id]=ally;return true;}
        public bool Remove(string id)=>id!=null && assignments.Remove(id);
        public void RemoveAssignmentsFor(CharacterDefinition ally)
        {foreach(var id in assignments.Where(p=>DeploymentCondition.SamePilot(p.Value,ally)).Select(p=>p.Key).ToList())assignments.Remove(id);}
        public string SelectionRestriction(CharacterDefinition ally)
            =>isBlocked?.Invoke(ally)==true?"이번 스테이지 출격 선택 제한":resolution.Placements.Any(p=>p.character.faction==Faction.Ally && SamePilotOrUnit(p.character,ally))?"고정·강제 출격":null;
        public void AutoFill()
        {
            foreach(var slot in Slots.Where(s=>!assignments.ContainsKey(s.id)))
            {var ally=Candidates.FirstOrDefault(a=>CanAssign(slot.id,a));if(ally!=null) Assign(slot.id,ally);}
        }
        public bool CanConfirm=>assignments.Count>0 || resolution.Placements.Any(u=>u?.character!=null && u.character.faction==Faction.Ally);
        public List<UnitPlacement> CreatePlacements()
        {
            if(!CanConfirm) throw new InvalidOperationException("아군을 한 명 이상 배치하세요.");
            var errors=map.ValidateMap();if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            if(!Slots.Select(s=>(s.id,s.cell)).SequenceEqual(map.deploymentSlots.Select(s=>(s.id,s.cell))))
                throw new InvalidOperationException("출격 칸이 변경되었습니다. 다시 시작해 배치 정보를 불러오세요.");
            foreach(var pair in assignments) if(!CanAssign(pair.Key,pair.Value)) throw new InvalidOperationException("배치한 아군의 편성이 변경되었습니다. 다시 선택하세요.");
            var result=resolution.Placements.Select(u=>u.Copy()).ToList();
            result.AddRange(Slots.Where(s=>assignments.ContainsKey(s.id)).Select(s=>new UnitPlacement {id=s.id,cell=s.cell,character=assignments[s.id],autoEnemyLevel=false}));
            return result;
        }
    }
}
