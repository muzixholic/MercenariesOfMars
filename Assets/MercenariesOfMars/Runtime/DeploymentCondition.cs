using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum DeploymentConditionKind
    {
        [InspectorName("플래그")] Flag,
        [InspectorName("아군 영입 여부（파일럿 기준）")] RecruitedAlly,
        [InspectorName("기체 보유 여부")] OwnedMech,
        [InspectorName("파일럿 영입 여부")] RecruitedPilot
    }
    public enum DeploymentConditionMatch { [InspectorName("모두 만족（AND）")] All,[InspectorName("하나 이상 만족（OR）")] Any }
    [Serializable] public sealed class DeploymentCondition
    {
        [InspectorName("조건 종류")] public DeploymentConditionKind kind;
        [InspectorName("플래그 이름")] public string flag;
        [InspectorName("아군 편성")] public CharacterDefinition ally;
        [InspectorName("기체")] public MechDefinition mech;
        [InspectorName("파일럿")] public PilotDefinition pilot;
        [InspectorName("기대값（끄면 미영입·미보유·거짓）")] public bool expected=true;
        public static bool SamePilot(CharacterDefinition a,CharacterDefinition b)
            =>a!=null && b!=null && (a==b || SamePilotDefinition(a.pilot,b.pilot));
        public static bool SamePilotDefinition(PilotDefinition a,PilotDefinition b)=>a!=null && b!=null && a.SourceDefinition==b.SourceDefinition;
        public static bool SameMech(MechDefinition a,MechDefinition b)=>a!=null && b!=null && a.SourceDefinition==b.SourceDefinition;
        public bool Matches(IReadOnlyList<CharacterDefinition> roster,Func<string,bool> getFlag,IReadOnlyList<MechDefinition> ownedMechs=null,IReadOnlyList<PilotDefinition> ownedPilots=null)
        {
            bool value=kind switch
            {
                DeploymentConditionKind.Flag=>getFlag?.Invoke(flag)??false,
                DeploymentConditionKind.RecruitedAlly=>roster.Any(a=>SamePilot(a,ally)) || ally?.pilot!=null && ownedPilots!=null && ownedPilots.Any(p=>SamePilotDefinition(p,ally.pilot)),
                DeploymentConditionKind.OwnedMech=>ownedMechs!=null?ownedMechs.Any(m=>SameMech(m,mech)):roster.Any(a=>a!=null && SameMech(a.mech,mech)),
                DeploymentConditionKind.RecruitedPilot=>ownedPilots!=null?ownedPilots.Any(p=>SamePilotDefinition(p,pilot)):roster.Any(a=>a!=null && SamePilotDefinition(a.pilot,pilot)),
                _=>false
            };
            return value==expected;
        }
        public string Validate()=>!Enum.IsDefined(typeof(DeploymentConditionKind),kind)?"알 수 없는 배치 조건입니다.":kind switch
        {
            DeploymentConditionKind.Flag when string.IsNullOrWhiteSpace(flag)=>"배치 조건의 플래그 이름을 지정하세요.",
            DeploymentConditionKind.RecruitedAlly when ally==null || ally.faction!=Faction.Ally=>"배치 조건의 아군 편성을 지정하세요.",
            DeploymentConditionKind.OwnedMech when mech==null=>"배치 조건의 기체를 지정하세요.",
            DeploymentConditionKind.RecruitedPilot when pilot==null=>"배치 조건의 파일럿을 지정하세요.",
            _=>null
        };
    }
}
