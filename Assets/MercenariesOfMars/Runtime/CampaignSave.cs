using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    [Serializable] public sealed class SavedFlag {public string key;public bool value;}
    [Serializable] public sealed class SavedMech {public string source;public int hp,en,armor,targeting,mobility,weapon;public List<string> weapons=new();}
    [Serializable] public sealed class SavedPilot {public string source;public int level,experience;}
    [Serializable] public sealed class SavedFormation {public string legacy,mech,pilot;}
    [Serializable] public sealed class CampaignSaveData
    {
        public int version=1;public string campaign,current,next,savedAt;public long funds;public int maxUpgradeStage;
        public int clearTurns,defeatedEnemies;public List<string> survivors=new();
        public List<SavedFlag> flags=new();public List<SavedMech> mechs=new();public List<SavedPilot> pilots=new();
        public List<SavedFormation> roster=new();public List<string> spareWeapons=new();
    }
    public sealed partial class CampaignFlow
    {
        public string SerializeSave(CampaignAssetCatalog catalog)
        {
            if(Phase!=CampaignPhase.Intermission || Definition==null || CurrentStage==null || NextStage==null)throw new InvalidOperationException("저장은 인터미션에서 할 수 있습니다.");
            if(catalog==null)throw new InvalidOperationException("저장 참조 목록을 갱신하세요.");
            var d=new CampaignSaveData {campaign=catalog.Id(Definition),current=catalog.Id(CurrentStage),next=catalog.Id(NextStage),funds=Funds,maxUpgradeStage=MaxUpgradeStage,savedAt=DateTimeOffset.Now.ToString("O"),clearTurns=LastResult?.ClearTurns??0,defeatedEnemies=LastResult?.DefeatedEnemies??0};
            d.survivors.AddRange(LastResult?.SurvivingAllies??Array.Empty<string>());
            foreach(var pair in owner.eventRunner.CaptureFlags())d.flags.Add(new SavedFlag {key=pair.Key,value=pair.Value});
            foreach(var m in OwnedMechs)d.mechs.Add(new SavedMech {source=catalog.Id(m),hp=m.hpUpgrade,en=m.energyUpgrade,armor=m.armorUpgrade,targeting=m.targetingUpgrade,mobility=m.mobilityUpgrade,weapon=m.weaponUpgrade,weapons=m.weapons.Where(w=>w!=null).Select(w=>catalog.Id(w)).ToList()});
            foreach(var p in OwnedPilots)d.pilots.Add(new SavedPilot {source=catalog.Id(p),level=p.level,experience=p.experience});
            foreach(var a in recruitedAllies)d.roster.Add(a.mech==null && a.pilot==null?new SavedFormation {legacy=catalog.Id(a)}:new SavedFormation {mech=catalog.Id(a.mech),pilot=catalog.Id(a.pilot)});
            d.spareWeapons.AddRange(spareWeapons.Select(w=>catalog.Id(w)));return JsonUtility.ToJson(d,true);
        }
        public bool RestoreSave(string json,CampaignAssetCatalog catalog,out string error)
        {
            error=null;var created=new List<ScriptableObject>();
            try
            {
                if(owner.IsMapTest || owner.IsStageTest)throw new InvalidOperationException("테스트 모드에서는 캠페인 저장을 불러올 수 없습니다.");
                if(Phase!=CampaignPhase.Inactive && Phase!=CampaignPhase.Intermission)throw new InvalidOperationException("불러오기는 타이틀 또는 인터미션에서 할 수 있습니다.");
                if(catalog==null)throw new InvalidOperationException("저장 참조 목록이 없습니다.");
                if(string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))throw new InvalidOperationException("저장 파일 형식이 올바르지 않습니다.");
                var d=JsonUtility.FromJson<CampaignSaveData>(json);
                if(d==null || d.version!=1 || d.funds<0 || d.maxUpgradeStage<0 || d.clearTurns<0 || d.defeatedEnemies<0 || d.mechs==null || d.pilots==null || d.roster==null || d.flags==null || d.spareWeapons==null || d.survivors==null)throw new InvalidOperationException("저장 데이터 값 또는 버전을 확인하세요.");
                var definition=catalog.Asset<CampaignDefinition>(d.campaign);var current=catalog.Asset<StageDefinition>(d.current);var next=catalog.Asset<StageDefinition>(d.next);
                if(current.ValidateStage().Count>0 || next.ValidateStage().Count>0)throw new InvalidOperationException("저장된 스테이지의 연결을 확인하세요.");
                var mechs=new Dictionary<string,MechDefinition>();var pilots=new Dictionary<string,PilotDefinition>();var roster=new List<CharacterDefinition>();
                foreach(var item in d.mechs)
                {
                    if(item==null || item.weapons==null || new[]{item.hp,item.en,item.armor,item.targeting,item.mobility,item.weapon}.Any(n=>n<0))throw new InvalidOperationException("기체 저장값이 올바르지 않습니다.");
                    var source=catalog.Asset<MechDefinition>(item.source);var m=UnityEngine.Object.Instantiate(source);created.Add(m);m.hideFlags=HideFlags.DontSave;m.sourceDefinition=source;
                    m.hpUpgrade=item.hp;m.energyUpgrade=item.en;m.armorUpgrade=item.armor;m.targetingUpgrade=item.targeting;m.mobilityUpgrade=item.mobility;m.weaponUpgrade=item.weapon;
                    m.weapons=item.weapons.Select(id=>catalog.Asset<SkillDefinition>(id)).ToList();if(m.ValidateEquipment().Count>0)throw new InvalidOperationException("저장 기체의 무장·중량을 확인하세요.");mechs.Add(item.source,m);
                }
                foreach(var item in d.pilots)
                {
                    if(item==null || item.level<1 || item.level>99 || item.experience<0)throw new InvalidOperationException("파일럿 저장값을 확인하세요.");
                    var source=catalog.Asset<PilotDefinition>(item.source);var p=UnityEngine.Object.Instantiate(source);created.Add(p);p.hideFlags=HideFlags.DontSave;p.sourceDefinition=source;p.level=item.level;p.experience=item.experience;
                    if(p.ValidateGrowth().Count>0)throw new InvalidOperationException("파일럿 성장 설정을 확인하세요.");pilots.Add(item.source,p);
                }
                foreach(var item in d.roster)
                {
                    if(item==null)throw new InvalidOperationException("비어 있는 편성이 있습니다.");CharacterDefinition a;
                    if(!string.IsNullOrEmpty(item.legacy)){a=catalog.Asset<CharacterDefinition>(item.legacy);if(a.mech!=null || a.pilot!=null)throw new InvalidOperationException("이전 편성 데이터가 변경되었습니다.");}
                    else{a=ScriptableObject.CreateInstance<CharacterDefinition>();created.Add(a);a.hideFlags=HideFlags.DontSave;a.mech=mechs[item.mech];a.pilot=pilots[item.pilot];a.displayName=$"{a.mech.displayName} ({a.pilot.displayName})";}
                    if(a.faction!=Faction.Ally || a.ValidateAssignment().Count>0 || roster.Any(b=>RosterDeployment.SharesParts(a,b)))throw new InvalidOperationException("중복되거나 잘못된 아군 편성입니다.");roster.Add(a);
                }
                var spares=d.spareWeapons.Select(id=>catalog.Asset<SkillDefinition>(id)).ToList();if(spares.Any(w=>w.ValidateSkill().Count>0))throw new InvalidOperationException("창고 무장 설정을 확인하세요.");
                var flags=new Dictionary<string,bool>();foreach(var item in d.flags){if(item==null || string.IsNullOrWhiteSpace(item.key))throw new InvalidOperationException("플래그 저장값을 확인하세요.");flags.Add(item.key,item.value);}
                // 모든 참조·소유·중량 검증이 끝난 뒤 현재 진행 상태를 교체한다.
                owner.PrepareLoadedIntermission(this,definition);recruitedAllies.Clear();ReleaseFormations();Definition=definition;CurrentStage=current;NextStage=next;Funds=d.funds;MaxUpgradeStage=d.maxUpgradeStage;
                recruitedAllies.AddRange(roster);ownedFormations.AddRange(created.OfType<CharacterDefinition>());workshopObjects.AddRange(created.Where(o=>o is not CharacterDefinition));
                foreach(var m in mechs.Values){localMechs[m.SourceDefinition]=m;if(!roster.Any(a=>a.mech==m))reserveMechs.Add(m);}
                foreach(var p in pilots.Values)if(!roster.Any(a=>a.pilot==p))reservePilots.Add(p);
                spareWeapons.AddRange(spares);owner.eventRunner.RestoreFlags(flags);stageStartFlags=new(flags);stageStartAllies=new(roster);stageStartMaxUpgradeStage=MaxUpgradeStage;deploymentBlockedAllies.Clear();
                CaptureWorkshopStart();LastResult=new StageBattleResult(d.clearTurns,d.defeatedEnemies,d.survivors);EndingTitle=null;EndingText=null;Error=null;Phase=CampaignPhase.Intermission;Touch();created.Clear();return true;
            }
            catch(Exception e){foreach(var o in created)DestroyRuntime(o);error="불러오지 못했습니다. "+e.Message;return false;}
        }
    }
    public static class CampaignSaveFiles
    {
        public static string SlotPath(int slot)
        {if(slot<1 || slot>3)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(Application.persistentDataPath,"저장",$"슬롯{slot}.json");}
        public static bool Save(CampaignFlow campaign,int slot,out string message)
        {
            try{if(!SaveToPath(campaign,CampaignAssetCatalog.Load(),SlotPath(slot),out message))return false;message=$"슬롯 {slot} 저장 완료";return true;}
            catch(Exception e){message="저장하지 못했습니다. "+e.Message;return false;}
        }
        public static bool SaveToPath(CampaignFlow campaign,CampaignAssetCatalog catalog,string path,out string message)
        {
            try{string json=campaign.SerializeSave(catalog);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,json);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);message="저장 완료";return true;}
            catch(Exception e){message="저장하지 못했습니다. "+e.Message;return false;}
        }
        public static bool Load(CampaignFlow campaign,int slot,out string message)
        {
            try{var path=SlotPath(slot);if(!File.Exists(path)){message="저장 데이터가 없습니다.";return false;}if(new FileInfo(path).Length>5*1024*1024){message="저장 파일 크기가 올바르지 않습니다.";return false;}if(!campaign.RestoreSave(File.ReadAllText(path),CampaignAssetCatalog.Load(),out message))return false;message=$"슬롯 {slot} 불러오기 완료";return true;}
            catch(Exception e){message="불러오지 못했습니다. "+e.Message;return false;}
        }
        public static string Label(int slot)
        {try{return File.Exists(SlotPath(slot))?$"슬롯 {slot} · {File.GetLastWriteTime(SlotPath(slot)):yyyy-MM-dd HH:mm}":$"슬롯 {slot} · 비어 있음";}catch{return $"슬롯 {slot} · 읽기 오류";}}
    }
}
