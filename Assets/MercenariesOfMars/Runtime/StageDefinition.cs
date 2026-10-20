using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum StageDestinationKind { [InspectorName("다음 스테이지")] Stage, [InspectorName("엔딩")] Ending }
    public enum StageConditionKind
    {
        [InspectorName("플래그 값")] Flag, [InspectorName("클리어 턴 수 이하")] ClearTurnsAtMost,
        [InspectorName("적 격추 수 이상")] DefeatedEnemiesAtLeast, [InspectorName("아군 유닛 생존")] AllySurvived
    }
    [Serializable] public sealed class StageCondition
    {
        [InspectorName("조건 종류")] public StageConditionKind kind;
        [InspectorName("플래그 / 유닛 ID")] public string key;
        [InspectorName("플래그 값")] public bool flagValue=true;
        [Min(0), InspectorName("기준 수치")] public int value=1;
        public bool Matches(StageBattleResult result,Func<string,bool> flag)
        {
            if(result==null) return false;
            return kind switch
            {
                StageConditionKind.Flag=>!string.IsNullOrWhiteSpace(key) && flag(key)==flagValue,
                StageConditionKind.ClearTurnsAtMost=>result.ClearTurns<=value,
                StageConditionKind.DefeatedEnemiesAtLeast=>result.DefeatedEnemies>=value,
                StageConditionKind.AllySurvived=>result.SurvivingAllies.Contains(key),
                _=>false
            };
        }
    }
    [Serializable] public sealed class StageDestination
    {
        [InspectorName("목적지 종류")] public StageDestinationKind kind;
        [InspectorName("다음 스테이지")] public StageDefinition stage;
        [InspectorName("엔딩 제목")] public string endingTitle="작전 완료";
        [TextArea, InspectorName("엔딩 문구")] public string endingText="모든 작전을 완료했습니다.";
        public string Error=>!Enum.IsDefined(typeof(StageDestinationKind),kind)?"목적지 종류가 잘못되었습니다.":
            kind==StageDestinationKind.Stage && stage==null?"다음 스테이지를 지정하거나 엔딩을 명시적으로 선택하세요.":null;
    }
    [Serializable] public sealed class StageBranch
    {
        [InspectorName("분기 이름")] public string label="새 분기";
        [InspectorName("모든 조건 만족 시 선택")] public List<StageCondition> conditions=new();
        [InspectorName("목적지")] public StageDestination destination=new();
        public bool Matches(StageBattleResult result,Func<string,bool> flag)
            =>conditions!=null && conditions.Count>0 && conditions.All(c=>c!=null && c.Matches(result,flag));
    }
    // 결과는 아웃트로의 체력 변경이나 다음 전투의 상태와 독립적으로 보관한다.
    public sealed class StageBattleResult
    {
        internal StageBattleResult(int turns,int defeated,IEnumerable<string> survivors)
        {ClearTurns=turns;DefeatedEnemies=defeated;SurvivingAllies=Array.AsReadOnly(survivors.ToArray());}
        public int ClearTurns { get; }
        public int DefeatedEnemies { get; }
        public IReadOnlyList<string> SurvivingAllies { get; }
        public StageBattleResult(BattleSession battle)
        {
            ClearTurns=battle.Round;
            DefeatedEnemies=battle.Units.Count(u=>u.Faction==Faction.Enemy && !u.IsAlive);
            SurvivingAllies=Array.AsReadOnly(battle.Units.Where(u=>u.Faction==Faction.Ally && u.IsAlive).Select(u=>u.Id).ToArray());
        }
    }
    [CreateAssetMenu(menuName="화성의 용병단/스테이지",fileName="새 스테이지")]
    public sealed class StageDefinition : ScriptableObject
    {
        [InspectorName("스테이지 이름")] public string displayName="새 스테이지";
        [InspectorName("출격 준비 스크립트（배치 선택 전에 실행）")] public EventSequence deploymentSetup;
        [InspectorName("인트로 스크립트")] public EventSequence intro;
        [InspectorName("맵")] public BattleMapDefinition map;
        [InspectorName("아웃트로 스크립트")] public EventSequence outro;
        [InspectorName("클리어 후 인터미션 상점")] public StageShopSettings shop=new();
        [InspectorName("조건별 다음 경로（위에서부터 우선）")] public List<StageBranch> branches=new();
        [InspectorName("기본 진행 경로")] public StageDestination defaultDestination=new();
        public StageDestination Resolve(StageBattleResult result,Func<string,bool> flag)
        {
            var branch=branches?.FirstOrDefault(b=>b!=null && b.Matches(result,flag));
            return branch!=null?branch.destination:defaultDestination;
        }
        public List<string> ValidateStage()
        {
            var errors=new List<string>();
            if(string.IsNullOrWhiteSpace(displayName)) errors.Add("스테이지 이름을 입력하세요.");
            if(shop!=null)errors.AddRange(shop.Validate().Select(e=>"인터미션 상점: "+e));
            if(deploymentSetup!=null)errors.AddRange(deploymentSetup.ValidateDeploymentSetup().Select(e=>"출격 준비: "+e));
            if(map==null) errors.Add("맵을 지정하세요."); else errors.AddRange(map.ValidateMap());
            if(intro!=null) errors.AddRange(intro.ValidateCommands().Select(e=>"인트로: "+e));
            if(outro!=null)
            {
                errors.AddRange(outro.ValidateCommands().Select(e=>"아웃트로: "+e));
                if(outro.commands!=null && outro.commands.Any(c=>c!=null && (c.kind==EventCommandKind.Attack || c.kind==EventCommandKind.Move || c.kind==EventCommandKind.EndTurn || c.kind==EventCommandKind.ChangeHp)))
                    errors.Add("아웃트로에서는 전투 상태 변경（공격·이동·턴 종료·체력 변경）을 사용할 수 없습니다.");
            }
            CheckDestination(defaultDestination,"기본 경로",errors);
            if(branches==null) errors.Add("분기 목록이 없습니다.");
            else foreach(var branch in branches)
            {
                if(branch==null) {errors.Add("비어 있는 분기가 있습니다.");continue;}
                if(branch.conditions==null || branch.conditions.Count==0) errors.Add(branch.label+": 조건을 추가하세요. 무조건 진행은 기본 경로를 사용하세요.");
                else foreach(var c in branch.conditions)
                {
                    if(c==null || !Enum.IsDefined(typeof(StageConditionKind),c.kind)) errors.Add(branch.label+": 잘못된 조건입니다.");
                    else if((c.kind==StageConditionKind.Flag || c.kind==StageConditionKind.AllySurvived) && string.IsNullOrWhiteSpace(c.key)) errors.Add(branch.label+": 플래그 또는 유닛 ID를 입력하세요.");
                    else if(c.value<0) errors.Add(branch.label+": 기준 수치는 0 이상이어야 합니다.");
                }
                CheckDestination(branch.destination,branch.label,errors);
            }
            return errors.Distinct().ToList();
        }
        static void CheckDestination(StageDestination destination,string label,List<string> errors)
        { if(destination==null) errors.Add(label+": 목적지가 없습니다."); else if(destination.Error!=null) errors.Add(label+": "+destination.Error); }
    }
}
