using System;
using System.Collections.Generic;
using System.Linq;

namespace MercenariesOfMars
{
    public sealed class NextDeploymentInfo
    {
        public int SelectableCount,AvailableCount,SlotCount,ForcedCount;
        public string Error;
    }
    public sealed partial class CampaignFlow
    {
        NextDeploymentInfo cachedDeployment;
        int cachedRevision;
        StageDefinition cachedNext;
        Dictionary<string,bool> cachedFlags;
        public NextDeploymentInfo NextDeployment()
        {
            var flags=owner.eventRunner!=null?owner.eventRunner.CaptureFlags():new Dictionary<string,bool>();
            if(cachedDeployment!=null && cachedRevision==revision && cachedNext==NextStage && cachedFlags.Count==flags.Count && cachedFlags.All(p=>flags.TryGetValue(p.Key,out var value)&&value==p.Value))return cachedDeployment;
            cachedFlags=new(flags);cachedRevision=revision;cachedNext=NextStage;
            var result=new NextDeploymentInfo();cachedDeployment=result;
            if(NextStage==null){result.Error="다음 스테이지가 없습니다.";return result;}
            var draft=new CampaignFlow(owner){previewMode=true,Definition=Definition,CurrentStage=NextStage,Phase=CampaignPhase.Intro,Funds=Funds,MaxUpgradeStage=MaxUpgradeStage};
            draft.recruitedAllies.AddRange(recruitedAllies);draft.reserveMechs.AddRange(reserveMechs);draft.reservePilots.AddRange(reservePilots);
            try
            {
                var issues=NextStage.ValidateStage();if(issues.Count>0)throw new InvalidOperationException(string.Join("\n",issues));
                if(!DeploymentSetupInterpreter.Run(NextStage.deploymentSetup,draft,flags,owner.eventRunner!=null?owner.eventRunner.instructionLimit:1000,out var error))throw new InvalidOperationException(error);
                using var resolved=draft.ResolveDeployment(NextStage.map,key=>flags.TryGetValue(key,out var value)&&value);
                result.SlotCount=NextStage.map.deploymentSlots.Count;
                result.ForcedCount=resolved.Placements.Count(p=>p.character.faction==Faction.Ally);
                result.AvailableCount=resolved.Roster.Count(a=>!draft.IsDeploymentBlocked(a) && !resolved.Placements.Any(p=>p.character.faction==Faction.Ally && RosterDeployment.SharesParts(p.character,a)));
                result.SelectableCount=Math.Min(result.SlotCount,result.AvailableCount);
            }
            catch(Exception e){result.Error=e.Message;}
            finally{draft.ReleaseFormations();}
            return result;
        }
    }
    public static class DeploymentSetupInterpreter
    {
        public static bool CampaignCommand(EventCommand c,CampaignFlow campaign)=>campaign!=null && (c.kind switch
        {
            EventCommandKind.IncreaseMaxUpgradeStage=>campaign.IncreaseMaxUpgradeStage(c.upgradeStageIncrease),
            EventCommandKind.RecruitAlly=>campaign.IsRecruited(c.rosterAlly) || campaign.RecruitAlly(c.rosterAlly),
            EventCommandKind.RemoveAlly=>!campaign.IsRecruited(c.rosterAlly) || campaign.RemoveAlly(c.rosterAlly),
            EventCommandKind.SetDeploymentBlocked=>campaign.SetDeploymentBlocked(c.rosterAlly,c.deploymentBlocked),_=>false
        });
        public static bool Run(EventSequence sequence,CampaignFlow campaign,Dictionary<string,bool> flags,int limit,out string error)
        {
            error=null;if(sequence==null)return true;
            var errors=sequence.ValidateDeploymentSetup();if(errors.Count>0){error=string.Join("\n",errors);return false;}
            int steps=0;
            for(int i=0;i<sequence.commands.Count;i++)
            {
                if(++steps>Math.Max(1,limit)){error="출격 준비 실행 한도를 초과했습니다. 순환 분기를 확인하세요.";return false;}
                var c=sequence.commands[i];if(c.kind==EventCommandKind.Stop)return true;
                if(c.kind==EventCommandKind.SetFlag)flags[c.flag]=c.flagValue;
                else if(c.kind==EventCommandKind.BranchFlag){bool value=flags.TryGetValue(c.flag,out var current)&&current;if(value==c.flagValue)i=c.jumpTo-1;}
                else if(!CampaignCommand(c,campaign)){error=$"출격 준비 명령 {i}: 아군 편성과 영입 상태를 확인하세요.";return false;}
            }
            return true;
        }
    }
}
