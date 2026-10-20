using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class AllyDeploymentIntegrationTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject {var x=ScriptableObject.CreateInstance<T>();owned.Add(x);return x;}
        StageDefinition Stage()
        {
            var stage=New<StageDefinition>();stage.defaultDestination.kind=StageDestinationKind.Ending;
            stage.map=New<BattleMapDefinition>();stage.map.width=4;stage.map.height=4;
            var enemy=New<CharacterDefinition>();enemy.faction=Faction.Enemy;
            stage.map.units.Add(new UnitPlacement {id="적",character=enemy,cell=new Vector2Int(3,3)});
            stage.map.deploymentSlots.Add(new AllyDeploymentSlot {id="출격1",cell=Vector2Int.zero});
            stage.map.deploymentSlots.Add(new AllyDeploymentSlot {id="출격2",cell=Vector2Int.right});return stage;
        }
        BattleController Controller(StageDefinition stage,params CharacterDefinition[] allies)
        {
            var go=new GameObject("출격 배치 검증");owned.Add(go);go.SetActive(false);
            var c=go.AddComponent<BattleController>();c.board=go.AddComponent<GridBoard>();c.eventRunner=go.AddComponent<EventRunner>();
            c.battleCamera=go.AddComponent<Camera>();c.battleCamera.transform.position=new Vector3(2,8,-4);c.battleCamera.transform.LookAt(new Vector3(2,0,2));
            c.showControls=false;c.automaticEnemy=false;c.enableCombatPresentation=false;
            c.campaign=New<CampaignDefinition>();c.campaign.firstStage=stage;c.campaign.initialAllies.AddRange(allies);
            go.SetActive(true);return c;
        }
        IEnumerator Phase(BattleController c,CampaignPhase phase)
        {for(int i=0;i<60 && c.Campaign.Phase!=phase;i++)yield return null;Assert.That(c.Campaign.Phase,Is.EqualTo(phase),c.Campaign.Error);}
        [UnityTearDown] public IEnumerator Cleanup(){foreach(var o in owned)if(o!=null)Object.Destroy(o);owned.Clear();yield return null;}
        EventSequence Setup(params EventCommand[] commands){var s=New<EventSequence>();s.commands.AddRange(commands);return s;}
        [UnityTest] public IEnumerator 출격준비의_제한플래그는_영입을_유지하며_선택과_자동배치를_막는다()
        {
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();var stage=Stage();
            stage.deploymentSetup=Setup(new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=b});
            var c=Controller(stage,a,b);yield return null;Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.True);Assert.That(c.Campaign.RecruitedAllies,Does.Contain(b));
            Assert.That(c.Deployment.Assign("출격1",b),Is.False);c.Deployment.AutoFill();Assert.That(c.Deployment.Assignments.Count,Is.EqualTo(1));
            Assert.That(c.Campaign.SetDeploymentBlocked(a,true),Is.True);Assert.That(c.Deployment.Assignments,Is.Empty);Assert.That(c.ConfirmDeployment(),Is.False);
            Assert.That(c.Campaign.SetDeploymentBlocked(b,false),Is.True);Assert.That(c.Deployment.Assign("출격1",b),Is.True);
            Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Find("출격1").Definition,Is.SameAs(b));
        }
        [UnityTest] public IEnumerator 제한은_재시작시_준비스크립트를_재적용하고_다음미션과_타이틀에서_해제된다()
        {
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();var stage=Stage();var next=Stage();stage.defaultDestination=new StageDestination {stage=next};
            stage.deploymentSetup=Setup(new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=b});
            var c=Controller(stage,a,b);yield return null;c.RestartBattle();c.Campaign.SetDeploymentBlocked(b,false);c.Deployment.AutoFill();c.ConfirmDeployment();yield return Phase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("출격1",-999);c.Session.ChangeHp("출격2",-999);yield return null;Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.True);c.Deployment.AutoFill();c.ConfirmDeployment();yield return Phase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999);yield return Phase(c,CampaignPhase.Intermission);Assert.That(c.Campaign.Depart(),Is.True);
            Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.False);Assert.That(c.Deployment.Assign("출격1",b),Is.True);
            c.Campaign.SetDeploymentBlocked(b,true);Assert.That(c.ReturnToTitle(),Is.True);Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.False);
        }
        [UnityTest] public IEnumerator 선택제한은_고정강제출격을_제거하지않는다()
        {
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();var stage=Stage();
            stage.map.units.Add(new UnitPlacement {id="강제",character=a,cell=new Vector2Int(2,0)});
            stage.deploymentSetup=Setup(new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=a});
            var c=Controller(stage,a,b);yield return null;c.RestartBattle();Assert.That(c.Deployment.Assign("출격1",a),Is.False);
            c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Find("강제").IsAlive,Is.True);
            Assert.That(c.Campaign.RecruitedAllies,Does.Contain(a));
        }
        [UnityTest] public IEnumerator 출격준비의_영입과_플래그는_같은스테이지의_배치조건에_반영된다()
        {
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();var stage=Stage();
            stage.map.units.Add(new UnitPlacement {id="추가",character=b,cell=new Vector2Int(2,0),conditions=new List<DeploymentCondition>{new() {kind=DeploymentConditionKind.Flag,flag="추가"}}});
            stage.deploymentSetup=Setup(new EventCommand {kind=EventCommandKind.RecruitAlly,rosterAlly=b},new EventCommand {kind=EventCommandKind.SetFlag,flag="추가"},
                new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=b},new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=b,deploymentBlocked=false});
            var c=Controller(stage,a);yield return null;Assert.That(c.RestartBattle(),Is.True);Assert.That(c.Session,Is.Null);Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.False);
            c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Find("추가").Definition,Is.SameAs(b));
        }
        [UnityTest] public IEnumerator 순환하는_출격준비는_한도로_중단하고_오류를_표시한다()
        {
            var a=New<CharacterDefinition>();var stage=Stage();stage.deploymentSetup=Setup(new EventCommand {kind=EventCommandKind.SetFlag,flag="반복"},
                new EventCommand {kind=EventCommandKind.BranchFlag,flag="반복",jumpTo=0});
            var c=Controller(stage,a);c.eventRunner.instructionLimit=3;yield return null;
            Assert.That(c.RestartBattle(),Is.False);Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Error));Assert.That(c.Campaign.Error,Does.Contain("한도"));Assert.That(c.Session,Is.Null);
        }
        [UnityTest] public IEnumerator 출격을_확정하기전에는_인트로와_전투가_시작되지않는다()
        {
            var stage=Stage();stage.intro=New<EventSequence>();stage.intro.commands.Add(new EventCommand {kind=EventCommandKind.Dialogue,text="출격 완료"});
            var a=New<CharacterDefinition>();var c=Controller(stage,a);yield return null;
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.IsDeploying,Is.True);Assert.That(c.Session,Is.Null);
            Assert.That(c.eventRunner.IsRunning,Is.False);Assert.That(c.ConfirmDeployment(),Is.False);
            c.Deployment.Assign("출격2",a);Assert.That(c.ConfirmDeployment(),Is.True);
            Assert.That(c.IsDeploying,Is.False);Assert.That(c.Session.Find("출격2").Cell,Is.EqualTo(Vector2Int.right));
            Assert.That(c.eventRunner.Dialogue,Is.EqualTo("출격 완료"));Assert.That(stage.map.units.Count,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator 영입목록은_다음미션에_이월하고_테스트명단은_실제캠페인에_섞이지않는다()
        {
            var stage=Stage();var next=Stage();stage.defaultDestination=new StageDestination {stage=next};
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();next.map.testRecruitedAllies.Add(New<CharacterDefinition>());
            var c=Controller(stage,a);yield return null;c.RestartBattle();c.Deployment.AutoFill();c.ConfirmDeployment();
            Assert.That(c.Campaign.RecruitAlly(b),Is.True);Assert.That(c.Campaign.RecruitAlly(b),Is.False);
            yield return Phase(c,CampaignPhase.Battle);c.Session.ChangeHp("적",-999);yield return Phase(c,CampaignPhase.Intermission);
            Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.IsDeploying,Is.True);
            Assert.That(c.Deployment.Candidates,Is.EquivalentTo(new[]{a,b}));
            c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Units.Count,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator 패배후_재시작은_영입목록을_유지하고_배치를_다시_선택한다()
        {
            var stage=Stage();var a=New<CharacterDefinition>();var c=Controller(stage,a);yield return null;
            c.RestartBattle();c.Deployment.AutoFill();c.ConfirmDeployment();yield return Phase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("출격1",-999);yield return null;Assert.That(c.IsGameOver,Is.True);
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.IsDeploying,Is.True);Assert.That(c.Session,Is.Null);
            Assert.That(c.Deployment.Assignments.Count,Is.Zero);Assert.That(c.Deployment.Candidates,Does.Contain(a));
        }
        [UnityTest] public IEnumerator 맵단독테스트는_테스트명단을_사용하고_배치중에도_종료할수있다()
        {
            var stage=Stage();var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();stage.map.testRecruitedAllies.Add(b);
            var c=Controller(stage,a);c.board.map=stage.map;int exits=0;c.ConfigureMapTest(()=>exits++);yield return null;
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.Deployment.Candidates,Is.EquivalentTo(new[]{b}));
            Assert.That(c.ExitMapTest(),Is.True);Assert.That(exits,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator 배치중_타이틀로_돌아가면_새캠페인의_초기영입목록으로_시작한다()
        {
            var a=New<CharacterDefinition>();var b=New<CharacterDefinition>();var c=Controller(Stage(),a);yield return null;
            c.RestartBattle();c.Campaign.RecruitAlly(b);Assert.That(c.ReturnToTitle(),Is.True);
            Assert.That(c.IsDeploying,Is.False);Assert.That(c.RestartBattle(),Is.True);
            Assert.That(c.Deployment.Candidates,Is.EquivalentTo(new[]{a}));
        }
    }
}
