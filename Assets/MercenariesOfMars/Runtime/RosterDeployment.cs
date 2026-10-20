using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    // 전장용 배치를 계산하고 강제 편성은 확인 후 캠페인이 인수한다. 원본 에셋은 수정하지 않는다.
    public sealed class RosterDeployment : IDisposable
    {
        readonly List<CharacterDefinition> owned=new();
        public List<CharacterDefinition> Roster {get;}=new();
        public List<UnitPlacement> Placements {get;}=new();
        public List<string> Notices {get;}=new();
        public List<MechDefinition> ReserveMechs {get;}=new();
        public List<PilotDefinition> ReservePilots {get;}=new();
        public static bool SharesParts(CharacterDefinition a,CharacterDefinition b)
            =>DeploymentCondition.SamePilot(a,b) || a!=null && b!=null && DeploymentCondition.SameMech(a.mech,b.mech);
        CharacterDefinition Pair(CharacterDefinition source,MechDefinition mech,PilotDefinition pilot=null)
        {
            var unit=UnityEngine.Object.Instantiate(source);unit.hideFlags=HideFlags.DontSave;
            unit.mech=mech;if(pilot!=null)unit.pilot=pilot;unit.displayName=$"{mech.displayName} ({unit.PilotName})";owned.Add(unit);return unit;
        }
        public RosterDeployment(BattleMapDefinition map,IEnumerable<CharacterDefinition> roster,Func<string,bool> getFlag=null,bool requireRoster=true,IEnumerable<MechDefinition> reserveMechs=null,IEnumerable<PilotDefinition> reservePilots=null)
        {
            var errors=map.ValidateMap();if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            foreach(var ally in roster??Enumerable.Empty<CharacterDefinition>())
            {
                if(ally==null || ally.faction!=Faction.Ally || ally.ValidateAssignment().Count>0)throw new ArgumentException("영입된 아군 편성을 확인하세요.");
                if(!Roster.Contains(ally))Roster.Add(ally);
            }
            try
            {
                ReserveMechs.AddRange(reserveMechs??Enumerable.Empty<MechDefinition>());ReservePilots.AddRange(reservePilots??Enumerable.Empty<PilotDefinition>());
                var allMechs=Roster.Where(a=>a.mech!=null).Select(a=>a.mech).Concat(ReserveMechs).ToList();var allPilots=Roster.Where(a=>a.pilot!=null).Select(a=>a.pilot).Concat(ReservePilots).ToList();
                var active=map.units.Where(u=>u.MeetsConditions(Roster,getFlag,allMechs,allPilots)).ToList();
                var fixedAllies=new List<CharacterDefinition>();
                foreach(var placement in active)
                {
                    var requested=placement.character;var actual=requested;
                    if(requested.faction==Faction.Ally && requireRoster)
                    {
                        if(requested.mech==null && requested.pilot==null)actual=Roster.FirstOrDefault(a=>a==requested);
                        else actual=Roster.FirstOrDefault(a=>DeploymentCondition.SameMech(a.mech,requested.mech) && DeploymentCondition.SamePilotDefinition(a.pilot,requested.pilot));
                        if(actual==null && requested.mech!=null && requested.pilot!=null)
                        {
                            var mechOwner=Roster.FirstOrDefault(a=>DeploymentCondition.SameMech(a.mech,requested.mech));
                            var pilotOwner=Roster.FirstOrDefault(a=>DeploymentCondition.SamePilotDefinition(a.pilot,requested.pilot));
                            if(mechOwner!=null && pilotOwner!=null && mechOwner!=pilotOwner && mechOwner.pilot!=null && pilotOwner.mech!=null)
                            {
                                actual=Pair(pilotOwner,mechOwner.mech);var remaining=Pair(mechOwner,pilotOwner.mech);
                                Roster[Roster.IndexOf(pilotOwner)]=actual;Roster[Roster.IndexOf(mechOwner)]=remaining;
                                Notices.Add($"{placement.id}: {actual.PilotName} → {actual.UnitName} 강제 편성\n{remaining.PilotName} → {remaining.UnitName} 교환 편성\n이 편성은 캠페인 영입 목록에 유지됩니다.");
                            }
                            else
                            {
                                var spareMech=ReserveMechs.FirstOrDefault(m=>DeploymentCondition.SameMech(m,requested.mech));
                                var sparePilot=ReservePilots.FirstOrDefault(p=>DeploymentCondition.SamePilotDefinition(p,requested.pilot));
                                var mech=mechOwner?.mech??spareMech;var pilot=pilotOwner?.pilot??sparePilot;
                                if(mech!=null && pilot!=null)
                                {
                                    actual=Pair(pilotOwner??requested,mech,pilot);
                                    if(mechOwner!=null){Roster[Roster.IndexOf(mechOwner)]=actual;ReservePilots.Add(mechOwner.pilot);}else ReserveMechs.Remove(spareMech);
                                    if(pilotOwner!=null){Roster[Roster.IndexOf(pilotOwner)]=actual;ReserveMechs.Add(pilotOwner.mech);}else ReservePilots.Remove(sparePilot);
                                    if(mechOwner==null && pilotOwner==null)Roster.Add(actual);
                                    Notices.Add($"{placement.id}: {actual.PilotName} → {actual.UnitName} 강제 편성\n대기 자원을 사용하며 기존 탑승자·기체는 대기 상태로 보존합니다. 캠페인 편성에 유지됩니다.");
                                }
                            }
                        }
                        if(actual==null)throw new InvalidOperationException($"필수 아군 {placement.id}: {requested.UnitName} / {requested.PilotName}의 기체 또는 파일럿을 보유하지 않았습니다. 영입 목록이나 배치 조건을 확인하세요.");
                    }
                    if(actual.faction==Faction.Ally)
                    {
                        if(fixedAllies.Any(a=>SharesParts(a,actual)))throw new InvalidOperationException($"고정 배치 {placement.id}: 같은 기체 또는 파일럿은 중복 출격할 수 없습니다.");
                        fixedAllies.Add(actual);
                    }
                    Placements.Add(placement.Copy(actual));
                }
                // 뒤의 강제 편성이 앞에서 예약한 편성을 교환했다면 중복 자원을 만들지 않고 거절한다.
                foreach(var unit in fixedAllies)if(requireRoster && !Roster.Contains(unit))throw new InvalidOperationException("서로 충돌하는 강제 편성이 있습니다. 고정 배치의 기체·파일럿을 확인하세요.");
            }
            catch{Dispose();throw;}
        }
        public List<CharacterDefinition> TakeOwned(){var result=new List<CharacterDefinition>(owned);owned.Clear();return result;}
        public void Dispose()
        {
            foreach(var unit in owned)if(unit!=null)
            {if(Application.isPlaying)UnityEngine.Object.Destroy(unit);else UnityEngine.Object.DestroyImmediate(unit);}
            owned.Clear();
        }
    }
}
