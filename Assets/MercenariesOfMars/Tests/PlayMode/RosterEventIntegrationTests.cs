using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class RosterEventIntegrationTests
    {
        readonly List<Object> owned=new();
        T New<T>() where T:ScriptableObject{var o=ScriptableObject.CreateInstance<T>();owned.Add(o);return o;}
        CharacterDefinition Ally(bool parts=false)
        {var a=New<CharacterDefinition>();if(parts){a.mech=New<MechDefinition>();a.pilot=New<PilotDefinition>();}return a;}
        StageDefinition Stage(params CharacterDefinition[] allies)
        {
            var stage=New<StageDefinition>();stage.defaultDestination.kind=StageDestinationKind.Ending;
            stage.map=New<BattleMapDefinition>();stage.map.width=5;stage.map.height=5;
            for(int i=0;i<allies.Length;i++)stage.map.units.Add(new UnitPlacement {id="아군"+i,character=allies[i],cell=new Vector2Int(i,0)});
            var enemy=New<CharacterDefinition>();enemy.faction=Faction.Enemy;
            stage.map.units.Add(new UnitPlacement {id="적",character=enemy,cell=new Vector2Int(4,4)});return stage;
        }
        EventSequence Script(params EventCommand[] commands){var s=New<EventSequence>();s.commands.AddRange(commands);return s;}
        BattleController Controller(StageDefinition stage,params CharacterDefinition[] roster)
        {
            var go=new GameObject("영입과 편성 검증");owned.Add(go);go.SetActive(false);
            var c=go.AddComponent<BattleController>();c.board=go.AddComponent<GridBoard>();c.eventRunner=go.AddComponent<EventRunner>();c.battleCamera=go.AddComponent<Camera>();
            c.automaticEnemy=false;c.showControls=false;c.enableCombatPresentation=false;
            c.campaign=New<CampaignDefinition>();c.campaign.firstStage=stage;c.campaign.initialAllies.AddRange(roster);go.SetActive(true);return c;
        }
        IEnumerator Phase(BattleController c,CampaignPhase phase)
        {for(int i=0;i<60 && c.Campaign.Phase!=phase;i++)yield return null;Assert.That(c.Campaign.Phase,Is.EqualTo(phase),c.Campaign.Error);}
        IEnumerator Finish(EventRunner r)
        {for(int i=0;i<60 && r.IsRunning;i++)yield return null;Assert.That(r.IsRunning,Is.False);Assert.That(r.LastError,Is.Null);}
        [UnityTearDown] public IEnumerator 정리(){foreach(var o in owned)if(o!=null)Object.Destroy(o);owned.Clear();yield return null;}
        [UnityTest] public IEnumerator 스크립트의_영입과_이탈은_즉시_영입목록과_전장에_반영된다()
        {
            var a=Ally();var b=Ally();var recruit=Ally();var stage=Stage(a,b);var c=Controller(stage,a,b);yield return null;c.RestartBattle();yield return Phase(c,CampaignPhase.Battle);
            var script=Script(new EventCommand {kind=EventCommandKind.RecruitAlly,rosterAlly=recruit},new EventCommand {kind=EventCommandKind.RecruitAlly,rosterAlly=recruit},
                new EventCommand {kind=EventCommandKind.RemoveAlly,rosterAlly=a},new EventCommand {kind=EventCommandKind.RemoveAlly,rosterAlly=a});
            Assert.That(c.eventRunner.Play(script,c.Session),Is.True);yield return Finish(c.eventRunner);
            Assert.That(c.Campaign.RecruitedAllies,Is.EquivalentTo(new[]{b,recruit}));Assert.That(c.Session.Find("아군0"),Is.Null);
            Assert.That(c.Session.Find("아군1").IsAlive,Is.True);Assert.That(c.Session.Outcome,Is.EqualTo(BattleOutcome.Ongoing));
            c.Session.ChangeHp("아군1",-999);yield return null;Assert.That(c.RestartBattle(),Is.True);yield return Phase(c,CampaignPhase.Battle);
            Assert.That(c.Campaign.RecruitedAllies,Is.EquivalentTo(new[]{a,b}));Assert.That(c.Session.Find("아군0"),Is.Not.Null);
        }
        [UnityTest] public IEnumerator 강제편성은_알림_확인후_영구교환하고_다음미션에도_유지한다()
        {
            var a=Ally(true);var b=Ally(true);var target=Ally(true);target.mech=a.mech;target.pilot=b.pilot;
            var stage=Stage(target);stage.map.deploymentSlots.Add(new AllyDeploymentSlot {id="선택",cell=Vector2Int.up});
            var next=Stage(target);stage.defaultDestination=new StageDestination {stage=next};var c=Controller(stage,a,b);yield return null;
            c.showControls=true;
            Assert.That(c.RestartBattle(),Is.True);Assert.That(c.HasFormationNotice,Is.True);Assert.That(c.Session,Is.Null);Assert.That(c.Campaign.RecruitedAllies,Is.EquivalentTo(new[]{a,b}));
            yield return null;
            Assert.That(c.ConfirmFormationNotice(),Is.True);Assert.That(c.IsDeploying,Is.True);
            var forced=c.Campaign.RecruitedAllies.Single(x=>x.pilot==b.pilot);Assert.That(forced.mech,Is.SameAs(a.mech));
            var remainder=c.Campaign.RecruitedAllies.Single(x=>x.pilot==a.pilot);Assert.That(remainder.mech,Is.SameAs(b.mech));
            Assert.That(c.Deployment.Assign("선택",forced),Is.False);Assert.That(c.Deployment.Assign("선택",remainder),Is.True);Assert.That(c.ConfirmDeployment(),Is.True);
            Assert.That(c.Session.Find("아군0").Definition,Is.SameAs(forced));yield return Phase(c,CampaignPhase.Battle);
            c.Session.ChangeHp("적",-999);yield return Phase(c,CampaignPhase.Intermission);Assert.That(c.Campaign.Depart(),Is.True);yield return Phase(c,CampaignPhase.Battle);
            Assert.That(c.HasFormationNotice,Is.False);Assert.That(c.Session.Find("아군0").Definition,Is.SameAs(forced));Assert.That(a.mech,Is.SameAs(target.mech));Assert.That(a.pilot,Is.Not.SameAs(target.pilot));
        }
        [UnityTest] public IEnumerator 아웃트로의_영입과_플래그는_다음맵의_조건부배치에_사용된다()
        {
            var a=Ally();var extra=Ally();var first=Stage(a);var next=Stage(a,extra);first.defaultDestination=new StageDestination {stage=next};
            next.map.units[1].conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.RecruitedAlly,ally=extra});
            next.map.units[1].conditions.Add(new DeploymentCondition {kind=DeploymentConditionKind.Flag,flag="추가출격"});
            first.outro=Script(new EventCommand {kind=EventCommandKind.RecruitAlly,rosterAlly=extra},new EventCommand {kind=EventCommandKind.SetFlag,flag="추가출격"});
            var c=Controller(first,a);yield return null;c.RestartBattle();yield return Phase(c,CampaignPhase.Battle);c.Session.ChangeHp("적",-999);yield return Phase(c,CampaignPhase.Intermission);
            Assert.That(c.Campaign.Depart(),Is.True);yield return Phase(c,CampaignPhase.Battle);Assert.That(c.Session.Find("아군1"),Is.Not.Null);
            Assert.That(c.Session.Find("아군1").Definition,Is.SameAs(extra));
        }
        [UnityTest] public IEnumerator 승리후_마지막아군이_이탈해도_엔딩을_진행한다()
        {
            var a=Ally();var stage=Stage(a);stage.outro=Script(new EventCommand {kind=EventCommandKind.RemoveAlly,rosterAlly=a});
            var c=Controller(stage,a);yield return null;c.RestartBattle();yield return Phase(c,CampaignPhase.Battle);c.Session.ChangeHp("적",-999);
            yield return Phase(c,CampaignPhase.Ending);Assert.That(c.Campaign.RecruitedAllies,Is.Empty);Assert.That(c.Session.Find("아군0"),Is.Null);Assert.That(c.IsGameOver,Is.False);
        }
        [UnityTest] public IEnumerator 전투중_마지막아군의_이탈은_즉시_패배가된다()
        {
            var a=Ally();var c=Controller(Stage(a),a);yield return null;c.RestartBattle();yield return Phase(c,CampaignPhase.Battle);
            Assert.That(c.eventRunner.Play(Script(new EventCommand {kind=EventCommandKind.RemoveAlly,rosterAlly=a}),c.Session),Is.True);
            yield return null;Assert.That(c.IsGameOver,Is.True);Assert.That(c.Campaign.RecruitedAllies,Is.Empty);
        }
    }
}
