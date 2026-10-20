using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum CampaignPhase { Inactive, Intro, Battle, Outro, Intermission, Ending, Error }
    // 스테이지 에셋을 변경하지 않고 한 번의 플레이 진행 상태만 보유한다.
    public sealed partial class CampaignFlow
    {
        readonly BattleController owner;
        Dictionary<string,bool> stageStartFlags=new();
        int stageStartMaxUpgradeStage=10;
        bool outroStarted;
        readonly List<CharacterDefinition> recruitedAllies=new();
        readonly List<CharacterDefinition> deploymentBlockedAllies=new();
        public bool IsDeploymentBlocked(CharacterDefinition ally)=>deploymentBlockedAllies.Exists(a=>DeploymentCondition.SamePilot(a,ally));
        public bool SetDeploymentBlocked(CharacterDefinition ally,bool blocked)
        {
            if(ally==null || ally.faction!=Faction.Ally || ally.ValidateAssignment().Count>0 || Phase==CampaignPhase.Inactive || Phase==CampaignPhase.Error)return false;
            deploymentBlockedAllies.RemoveAll(a=>DeploymentCondition.SamePilot(a,ally));
            if(blocked){deploymentBlockedAllies.Add(ally);if(!previewMode)owner.Deployment?.RemoveAssignmentsFor(ally);}
            Touch();
            return true;
        }
        List<CharacterDefinition> stageStartAllies=new();
        readonly List<CharacterDefinition> ownedFormations=new();
        public bool IsRecruited(CharacterDefinition ally)=>recruitedAllies.Exists(a=>DeploymentCondition.SamePilot(a,ally));
        public IReadOnlyList<CharacterDefinition> RecruitedAllies=>recruitedAllies.AsReadOnly();
        public bool RecruitAlly(CharacterDefinition ally)
        {
            if(ally==null || ally.faction!=Faction.Ally || ally.ValidateAssignment().Count>0
                || recruitedAllies.Exists(a=>RosterDeployment.SharesParts(a,ally))) return false;
            recruitedAllies.Add(AdoptReserves(ally));Touch();return true;
        }
        public bool RemoveAlly(CharacterDefinition ally)
        {
            var actual=recruitedAllies.Find(a=>DeploymentCondition.SamePilot(a,ally));
            if(actual==null || !previewMode && !owner.WithdrawAllyFromBattle(actual))return false;
            recruitedAllies.Remove(actual);Touch();return true;
        }
        internal void ApplyDeploymentRoster(RosterDeployment deployment)
        {recruitedAllies.Clear();recruitedAllies.AddRange(deployment.Roster);reserveMechs.Clear();reserveMechs.AddRange(deployment.ReserveMechs);reservePilots.Clear();reservePilots.AddRange(deployment.ReservePilots);ownedFormations.AddRange(deployment.TakeOwned());Touch();}
        void ReleaseFormations()
        {
            foreach(var unit in ownedFormations)if(unit!=null)
            {if(Application.isPlaying)UnityEngine.Object.Destroy(unit);else UnityEngine.Object.DestroyImmediate(unit);}
            ownedFormations.Clear();
            ReleaseWorkshop();
        }
        public CampaignPhase Phase { get; private set; }
        public StageDefinition CurrentStage { get; private set; }
        public StageDefinition NextStage { get; private set; }
        public StageBattleResult LastResult { get; private set; }
        public long Funds { get; private set; }
        public int MaxUpgradeStage { get; private set; }=10;
        public bool IncreaseMaxUpgradeStage(int amount)
        {
            if(amount<=0 || Phase==CampaignPhase.Inactive || Phase==CampaignPhase.Error)return false;
            MaxUpgradeStage=(int)System.Math.Min(int.MaxValue,(long)MaxUpgradeStage+amount);Touch();return true;
        }
        public string EndingTitle { get; private set; }
        public string EndingText { get; private set; }
        public string Error { get; private set; }
        public bool UsesFullScreen=>Phase==CampaignPhase.Intermission || Phase==CampaignPhase.Ending || Phase==CampaignPhase.Error;
        public CampaignFlow(BattleController controller) { owner=controller; }
        public bool Begin(CampaignDefinition campaign)
        {
            if(campaign==null || campaign.firstStage==null) return Fail("캠페인의 첫 스테이지를 지정하세요.");
            Funds=System.Math.Max(0,campaign.initialFunds); LastResult=null;
            MaxUpgradeStage=Mathf.Max(0,campaign.initialMaxUpgradeStage);
            CurrentStage=null;NextStage=null;stageStartAllies.Clear();
            recruitedAllies.Clear();ReleaseFormations();
            Definition=campaign;
            if(campaign.initialAllies==null)return Fail("초기 영입 아군 목록을 초기화하세요.");
            foreach(var ally in campaign.initialAllies)
            {
                if(ally==null || ally.faction!=Faction.Ally || ally.ValidateAssignment().Count>0) return Fail("초기 영입 아군 목록에 올바른 아군 편성을 지정하세요.");
                if(!RecruitAlly(ally) && !IsRecruited(ally))return Fail("초기 영입 목록에 같은 기체가 중복 지정되어 있습니다.");
            }
            return Enter(campaign.firstStage,new Dictionary<string,bool>());
        }
        bool Enter(StageDefinition stage,Dictionary<string,bool> flags)
        {
            if(stage==null) return Fail("진행할 스테이지가 없습니다.");
            // 검증 실패도 선택한 목적지의 실패다. 재시도에 목적지와 진입 당시 플래그를 유지한다.
            CurrentStage=stage; NextStage=null; stageStartFlags=new Dictionary<string,bool>(flags);
            deploymentBlockedAllies.Clear();
            stageStartMaxUpgradeStage=MaxUpgradeStage;
            stageStartAllies=new List<CharacterDefinition>(recruitedAllies);
            CaptureWorkshopStart();
            var errors=stage.ValidateStage();
            if(errors.Count>0) return Fail(string.Join("\n",errors));
            Error=null; EndingTitle=null; EndingText=null; outroStarted=false; Phase=CampaignPhase.Intro;
            if(!owner.LoadStageBattle(stage,flags)) return Fail(owner.eventRunner?.LastError??"스테이지 전투를 초기화하지 못했습니다.");
            return true;
        }
        public bool RestartStage()
        {
            if(CurrentStage==null)return false;
            recruitedAllies.Clear();recruitedAllies.AddRange(stageStartAllies);
            RestoreWorkshopStart();
            MaxUpgradeStage=stageStartMaxUpgradeStage;return Enter(CurrentStage,stageStartFlags);
        }
        public bool Depart()
        {
            if(owner.IsStageTest || Phase!=CampaignPhase.Intermission || NextStage==null) return false;
            // 확정된 목적지만 사용한다. 인터미션에서 조건을 다시 판정하지 않는다.
            return Enter(NextStage,owner.eventRunner.CaptureFlags());
        }
        public void Stop() { Phase=CampaignPhase.Inactive; CurrentStage=null; NextStage=null; LastResult=null; Error=null; recruitedAllies.Clear();deploymentBlockedAllies.Clear();stageStartAllies.Clear();ReleaseFormations(); MaxUpgradeStage=10;stageStartMaxUpgradeStage=10; }
        public void Tick()
        {
            if(Phase==CampaignPhase.Inactive || UsesFullScreen || owner.Session==null) return;
            var runner=owner.eventRunner; var battle=owner.Session;
            if(battle.IsPresenting || battle.PendingAttack!=null || battle.Outcome==BattleOutcome.Defeat) return;
            if(Phase==CampaignPhase.Intro)
            {
                if(runner.IsRunning) return;
                if(!string.IsNullOrEmpty(runner.LastError)) {Fail(runner.LastError);return;}
                Phase=CampaignPhase.Battle;
            }
            if(Phase==CampaignPhase.Battle && battle.Outcome==BattleOutcome.Victory)
            { LastResult=new StageBattleResult(battle); Phase=CampaignPhase.Outro; }
            if(Phase!=CampaignPhase.Outro || runner.IsRunning) return;
            if(!string.IsNullOrEmpty(runner.LastError)) {Fail(runner.LastError);return;}
            if(!outroStarted)
            {
                outroStarted=true;
                if(CurrentStage.outro!=null)
                {
                    if(!runner.Play(CurrentStage.outro,battle,this)) Fail(runner.LastError);
                    return;
                }
            }
            var destination=CurrentStage.Resolve(LastResult,runner.GetFlag);
            if(destination==null || destination.Error!=null) {Fail(destination?.Error??"진행 경로가 없습니다.");return;}
            if(destination.kind==StageDestinationKind.Ending)
            { EndingTitle=destination.endingTitle; EndingText=destination.endingText; Phase=CampaignPhase.Ending; }
            else { NextStage=destination.stage; Phase=CampaignPhase.Intermission; }
            owner.ClearSelection();
        }
        bool Fail(string message) { Error=message; Phase=CampaignPhase.Error; return false; }
        internal void DeploymentFailed(string message)=>Fail(message);
    }
}
