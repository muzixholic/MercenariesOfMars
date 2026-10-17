using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum CampaignPhase { Inactive, Intro, Battle, Outro, Intermission, Ending, Error }
    // 스테이지 에셋을 변경하지 않고 한 번의 플레이 진행 상태만 보유한다.
    public sealed class CampaignFlow
    {
        readonly BattleController owner;
        Dictionary<string,bool> stageStartFlags=new();
        bool outroStarted;
        public CampaignPhase Phase { get; private set; }
        public StageDefinition CurrentStage { get; private set; }
        public StageDefinition NextStage { get; private set; }
        public StageBattleResult LastResult { get; private set; }
        public long Funds { get; private set; }
        public string EndingTitle { get; private set; }
        public string EndingText { get; private set; }
        public string Error { get; private set; }
        public bool UsesFullScreen=>Phase==CampaignPhase.Intermission || Phase==CampaignPhase.Ending || Phase==CampaignPhase.Error;
        public CampaignFlow(BattleController controller) { owner=controller; }
        public bool Begin(CampaignDefinition campaign)
        {
            if(campaign==null || campaign.firstStage==null) return Fail("캠페인의 첫 스테이지를 지정하세요.");
            Funds=System.Math.Max(0,campaign.initialFunds); LastResult=null;
            return Enter(campaign.firstStage,new Dictionary<string,bool>());
        }
        bool Enter(StageDefinition stage,Dictionary<string,bool> flags)
        {
            if(stage==null) return Fail("진행할 스테이지가 없습니다.");
            // 검증 실패도 선택한 목적지의 실패다. 재시도에 목적지와 진입 당시 플래그를 유지한다.
            CurrentStage=stage; NextStage=null; stageStartFlags=new Dictionary<string,bool>(flags);
            var errors=stage.ValidateStage();
            if(errors.Count>0) return Fail(string.Join("\n",errors));
            Error=null; EndingTitle=null; EndingText=null; outroStarted=false; Phase=CampaignPhase.Intro;
            if(!owner.LoadStageBattle(stage,flags)) return Fail("스테이지 전투를 초기화하지 못했습니다.");
            return true;
        }
        public bool RestartStage()=>CurrentStage!=null && Enter(CurrentStage,stageStartFlags);
        public bool Depart()
        {
            if(owner.IsStageTest || Phase!=CampaignPhase.Intermission || NextStage==null) return false;
            // 확정된 목적지만 사용한다. 인터미션에서 조건을 다시 판정하지 않는다.
            return Enter(NextStage,owner.eventRunner.CaptureFlags());
        }
        public void Stop() { Phase=CampaignPhase.Inactive; CurrentStage=null; NextStage=null; LastResult=null; Error=null; }
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
                    if(!runner.Play(CurrentStage.outro,battle)) Fail(runner.LastError);
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
    }
}
