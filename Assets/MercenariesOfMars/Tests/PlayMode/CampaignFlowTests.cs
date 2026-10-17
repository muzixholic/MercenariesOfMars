using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class CampaignFlowTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var x=ScriptableObject.CreateInstance<T>();owned.Add(x);return x;}
        EventSequence Script(params EventCommand[] commands)
        {var s=New<EventSequence>();s.commands.AddRange(commands);return s;}
        StageDefinition Stage(string name)
        {
            var s=New<StageDefinition>();s.displayName=name;s.defaultDestination.kind=StageDestinationKind.Ending;
            s.map=New<BattleMapDefinition>();s.map.width=4;s.map.height=4;
            for(int i=0;i<2;i++)
            {
                var d=New<CharacterDefinition>();d.faction=i==0?Faction.Ally:Faction.Enemy;
                s.map.units.Add(new UnitPlacement {id=i==0?"아군":"적",character=d,cell=new Vector2Int(i,0)});
            }
            return s;
        }
        BattleController Controller(StageDefinition first)
        {
            var go=new GameObject("캠페인 진행 검증");owned.Add(go);go.SetActive(false);
            var c=go.AddComponent<BattleController>();c.board=go.AddComponent<GridBoard>();c.eventRunner=go.AddComponent<EventRunner>();
            var camera=new GameObject("캠페인 카메라");owned.Add(camera);c.battleCamera=camera.AddComponent<Camera>();
            camera.transform.position=new Vector3(2,8,-4);camera.transform.LookAt(new Vector3(2,0,2));
            c.showControls=false;c.automaticEnemy=false;c.enableCombatPresentation=false;
            c.campaign=New<CampaignDefinition>();c.campaign.firstStage=first;c.campaign.initialFunds=12345;
            go.SetActive(true);return c;
        }
        IEnumerator WaitPhase(BattleController c,CampaignPhase phase)
        {for(int i=0;i<60 && c.Campaign.Phase!=phase;i++) yield return null;Assert.That(c.Campaign.Phase,Is.EqualTo(phase),c.Campaign.Error);}
        [UnityTearDown] public IEnumerator Cleanup()
        {foreach(var o in owned) if(o!=null) Object.Destroy(o);owned.Clear();yield return null;}
        [UnityTest] public IEnumerator 인트로_전투_아웃트로_분기_인터미션_다음인트로를_순서대로_진행한다()
        {
            var first=Stage("첫 작전");var special=Stage("구출 작전");var fallback=Stage("기본 작전");
            first.intro=Script(new EventCommand {kind=EventCommandKind.Dialogue,speaker="통신",text="인트로"});
            first.map.openingEvent=Script(new EventCommand {kind=EventCommandKind.SetFlag,flag="맵 시작"});
            first.outro=Script(new EventCommand {kind=EventCommandKind.Dialogue,speaker="통신",text="아웃트로"},new EventCommand {kind=EventCommandKind.SetFlag,flag="구출 성공"});
            first.defaultDestination=new StageDestination {stage=fallback};
            first.branches.Add(new StageBranch {conditions=new List<StageCondition> {new() {key="구출 성공"}},destination=new StageDestination {stage=special}});
            special.intro=Script(new EventCommand {kind=EventCommandKind.Dialogue,text="다음 인트로"});
            var c=Controller(first);yield return null;Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Intro));Assert.That(c.eventRunner.Dialogue,Is.EqualTo("인트로"));
            Assert.That(c.TryAdvanceDialogue(),Is.True);yield return WaitPhase(c,CampaignPhase.Battle);
            Assert.That(c.eventRunner.GetFlag("맵 시작"),Is.False);
            c.Session.ChangeHp("적",-999);yield return WaitPhase(c,CampaignPhase.Outro);
            Assert.That(c.IsVictory,Is.False);Assert.That(c.eventRunner.Dialogue,Is.EqualTo("아웃트로"));
            Assert.That(c.Campaign.LastResult.DefeatedEnemies,Is.EqualTo(1));
            Assert.That(c.TryAdvanceDialogue(),Is.True);yield return WaitPhase(c,CampaignPhase.Intermission);
            Assert.That(c.Campaign.NextStage,Is.SameAs(special));Assert.That(c.Campaign.Funds,Is.EqualTo(12345));
            // 확정 후 플래그를 바꿔도 목적지는 바뀌지 않는다.
            c.eventRunner.RestoreFlags(new Dictionary<string,bool> { ["구출 성공"]=false,["이월 플래그"]=true });
            Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.Campaign.CurrentStage,Is.SameAs(special));
            Assert.That(c.board.map,Is.SameAs(special.map));Assert.That(c.eventRunner.GetFlag("이월 플래그"),Is.True);
            Assert.That(c.eventRunner.Dialogue,Is.EqualTo("다음 인트로"));Assert.That(c.Session.Round,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator 인트로_이벤트중_승리해도_남은대사를_넘기고_아웃트로로_진행한다()
        {
            var stage=Stage("이벤트 작전");
            stage.intro=Script(new EventCommand {kind=EventCommandKind.ChangeHp,unitId="적",amount=-999},
                new EventCommand {kind=EventCommandKind.Dialogue,text="목표 달성"});
            stage.outro=Script(new EventCommand {kind=EventCommandKind.SetFlag,flag="아웃트로 완료"});
            var c=Controller(stage);yield return null;c.RestartBattle();
            for(int i=0;i<10 && c.eventRunner.Dialogue==null;i++) yield return null;
            Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Victory));
            Assert.That(c.TryAdvanceDialogue(),Is.True);
            yield return WaitPhase(c,CampaignPhase.Ending);
            Assert.That(c.eventRunner.GetFlag("아웃트로 완료"),Is.True);
        }
        [UnityTest] public IEnumerator 마지막스테이지는_인터미션을_건너뛰고_엔딩후_타이틀에서_새로시작한다()
        {
            var final=Stage("마지막 작전");final.defaultDestination.endingTitle="귀환";
            var c=Controller(final);yield return null;c.RestartBattle();yield return WaitPhase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999);yield return WaitPhase(c,CampaignPhase.Ending);
            Assert.That(c.Campaign.NextStage,Is.Null);Assert.That(c.Campaign.EndingTitle,Is.EqualTo("귀환"));
            Assert.That(c.Campaign.Depart(),Is.False);Assert.That(c.ReturnToTitle(),Is.True);
            Assert.That(c.IsTitleScreen,Is.True);Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Inactive));
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
        }
        [UnityTest] public IEnumerator 패배는_아웃트로없이_게임오버로_가고_재시작하면_스테이지시작_플래그로_돌아간다()
        {
            var stage=Stage("방어 작전");stage.outro=Script(new EventCommand {kind=EventCommandKind.SetFlag,flag="아웃트로"});
            var c=Controller(stage);yield return null;c.RestartBattle();yield return WaitPhase(c,CampaignPhase.Battle);
            c.eventRunner.RestoreFlags(new Dictionary<string,bool> { ["실패한 시도"]=true });
            c.Session.ChangeHp("아군",-999);yield return null;
            Assert.That(c.IsGameOver,Is.True);Assert.That(c.eventRunner.GetFlag("아웃트로"),Is.False);
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.eventRunner.GetFlag("실패한 시도"),Is.False);
            Assert.That(c.Session.Find("아군").IsAlive,Is.True);
        }
        [UnityTest] public IEnumerator 연결누락과_스크립트오류는_진행을_멈추고_설정수정후_재시도할수있다()
        {
            var stage=Stage("연결 확인");stage.defaultDestination=new StageDestination();
            var c=Controller(stage);yield return null;Assert.That(c.RestartBattle(),Is.False);
            Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Error));Assert.That(c.IsTitleScreen,Is.False);
            Assert.That(c.Campaign.Error,Does.Contain("다음 스테이지"));
            stage.defaultDestination.kind=StageDestinationKind.Ending;
            Assert.That(c.RestartBattle(),Is.True);yield return WaitPhase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("아군",-999);yield return null;
            stage.intro=Script(new EventCommand {kind=EventCommandKind.Move,unitId="없는 유닛",cell=Vector2Int.up});
            c.RestartBattle();yield return WaitPhase(c,CampaignPhase.Error);
            Assert.That(c.Campaign.Error,Does.Contain("실행할 수 없습니다"));Assert.That(c.CanShowUnitMenu,Is.False);
            Assert.That(c.ReturnToTitle(),Is.True);
        }
        [UnityTest] public IEnumerator 다음스테이지_검증실패후_재시도는_목적지와_진입당시_플래그를_유지한다()
        {
            var first=Stage("완료한 작전"); var next=Stage("진입할 작전");
            first.defaultDestination=new StageDestination {stage=next};
            next.intro=Script(new EventCommand {kind=EventCommandKind.Dialogue,text="다음 작전 시작"});
            var c=Controller(first); yield return null; Assert.That(c.RestartBattle(),Is.True);
            yield return WaitPhase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999); yield return WaitPhase(c,CampaignPhase.Intermission);
            c.eventRunner.RestoreFlags(new Dictionary<string,bool> {["이월"]=true});
            var map=next.map; next.map=null;
            Assert.That(c.Campaign.Depart(),Is.False);
            Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Error));
            Assert.That(c.Campaign.CurrentStage,Is.SameAs(next));
            Assert.That(c.Campaign.Error,Does.Contain("맵"));
            Assert.That(c.RestartBattle(),Is.False); // 반복 실패도 목적지를 유지한다.
            c.eventRunner.RestoreFlags(new Dictionary<string,bool> {["이월"]=false,["실패 후 변경"]=true});
            next.map=map;
            Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Campaign.CurrentStage,Is.SameAs(next));
            Assert.That(c.board.map,Is.SameAs(map));
            Assert.That(c.eventRunner.GetFlag("이월"),Is.True);
            Assert.That(c.eventRunner.GetFlag("실패 후 변경"),Is.False);
            Assert.That(c.eventRunner.Dialogue,Is.EqualTo("다음 작전 시작"));
            Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
        }
        [UnityTest] public IEnumerator 다음스테이지_진입실패후_타이틀에서_시작하면_첫작전으로_돌아간다()
        {
            var first=Stage("첫 작전"); var next=Stage("다음 작전");
            first.defaultDestination=new StageDestination {stage=next};
            var c=Controller(first); yield return null; c.RestartBattle(); yield return WaitPhase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999); yield return WaitPhase(c,CampaignPhase.Intermission);
            next.map=null; Assert.That(c.Campaign.Depart(),Is.False);
            Assert.That(c.ReturnToTitle(),Is.True); Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Campaign.CurrentStage,Is.SameAs(first));
            Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
        }
        [UnityTest] public IEnumerator 스테이지테스트는_분기결과에서_멈추고_재시작과_종료를_제공한다()
        {
            var stage=Stage("선택 스테이지");var next=Stage("다음 스테이지");stage.defaultDestination=new StageDestination {stage=next};
            stage.outro=Script(new EventCommand {kind=EventCommandKind.SetFlag,flag="아웃트로 완료"});
            var c=Controller(stage);int exits=0;c.ConfigureStageTest(()=>exits++);yield return null;
            Assert.That(c.RestartBattle(),Is.True);yield return WaitPhase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999);yield return WaitPhase(c,CampaignPhase.Intermission);
            Assert.That(c.Campaign.CurrentStage,Is.SameAs(stage));Assert.That(c.Campaign.NextStage,Is.SameAs(next));
            Assert.That(c.eventRunner.GetFlag("아웃트로 완료"),Is.True);Assert.That(c.Campaign.Depart(),Is.False);
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.eventRunner.GetFlag("아웃트로 완료"),Is.False);
            Assert.That(c.Campaign.CurrentStage,Is.SameAs(stage));Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
            Assert.That(c.ExitStageTest(),Is.True);Assert.That(c.ExitStageTest(),Is.False);Assert.That(exits,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator 스테이지테스트는_패배와_엔딩에서도_재시작할수있다()
        {
            var c=Controller(Stage("단일 작전"));c.ConfigureStageTest(()=>{});yield return null;
            c.RestartBattle();yield return WaitPhase(c,CampaignPhase.Battle);c.Session.ChangeHp("아군",-999);yield return null;
            Assert.That(c.IsGameOver,Is.True);Assert.That(c.RestartBattle(),Is.True);
            yield return WaitPhase(c,CampaignPhase.Battle);c.Session.ChangeHp("적",-999);yield return WaitPhase(c,CampaignPhase.Ending);
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
        }
        [UnityTest] public IEnumerator 맵테스트는_캠페인이_지정되어도_기존_단일맵으로_진행한다()
        {
            var stage=Stage("캠페인용");var c=Controller(stage);c.board.map=stage.map;
            c.ConfigureMapTest(()=>{});yield return null;
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.Campaign,Is.Null);
            c.Session.ChangeHp("적",-999);yield return null;
            Assert.That(c.IsVictory,Is.True);Assert.That(c.ShowsMapTestExit,Is.True);
        }
    }
}
