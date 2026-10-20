using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public sealed partial class CampaignFlow
    {
        public CampaignDefinition Definition {get;private set;}
        readonly List<MechDefinition> reserveMechs=new();
        readonly List<PilotDefinition> reservePilots=new();
        readonly List<SkillDefinition> spareWeapons=new();
        readonly List<ScriptableObject> workshopObjects=new();
        readonly Dictionary<MechDefinition,MechDefinition> localMechs=new();
        List<MechDefinition> stageReserveMechs=new();
        List<PilotDefinition> stageReservePilots=new();
        List<SkillDefinition> stageSpareWeapons=new();
        bool previewMode;
        int revision;
        public IReadOnlyList<SkillDefinition> SpareWeapons=>spareWeapons.AsReadOnly();
        public List<MechDefinition> OwnedMechs=>recruitedAllies.Where(a=>a.mech!=null).Select(a=>a.mech).Concat(reserveMechs).Distinct().ToList();
        public List<PilotDefinition> OwnedPilots=>recruitedAllies.Where(a=>a.pilot!=null).Select(a=>a.pilot).Concat(reservePilots).Distinct().ToList();
        void Touch(){revision++;cachedDeployment=null;}
        static void DestroyRuntime(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        void ReleaseWorkshop()
        {foreach(var o in workshopObjects)DestroyRuntime(o);workshopObjects.Clear();reserveMechs.Clear();reservePilots.Clear();spareWeapons.Clear();localMechs.Clear();stageReserveMechs.Clear();stageReservePilots.Clear();stageSpareWeapons.Clear();Touch();}
        void CaptureWorkshopStart(){stageReserveMechs=new(reserveMechs);stageReservePilots=new(reservePilots);stageSpareWeapons=new(spareWeapons);}
        void RestoreWorkshopStart(){reserveMechs.Clear();reserveMechs.AddRange(stageReserveMechs);reservePilots.Clear();reservePilots.AddRange(stageReservePilots);spareWeapons.Clear();spareWeapons.AddRange(stageSpareWeapons);}
        CharacterDefinition Pair(CharacterDefinition source,MechDefinition mech,PilotDefinition pilot)
        {
            var unit=source!=null?UnityEngine.Object.Instantiate(source):ScriptableObject.CreateInstance<CharacterDefinition>();
            unit.hideFlags=HideFlags.DontSave;unit.mech=mech;unit.pilot=pilot;unit.faction=Faction.Ally;unit.displayName=$"{mech.displayName} ({pilot.displayName})";ownedFormations.Add(unit);return unit;
        }
        CharacterDefinition AdoptReserves(CharacterDefinition unit)
        {
            if(unit.mech==null || unit.pilot==null)return unit;
            var mech=reserveMechs.FirstOrDefault(m=>DeploymentCondition.SameMech(m,unit.mech));
            if(mech==null && localMechs.TryGetValue(unit.mech.SourceDefinition,out var local))mech=local;
            var pilot=reservePilots.FirstOrDefault(p=>DeploymentCondition.SamePilotDefinition(p,unit.pilot));
            if(mech!=null)reserveMechs.Remove(mech);if(pilot!=null)reservePilots.Remove(pilot);
            return mech!=null || pilot!=null?Pair(unit,mech??unit.mech,pilot??unit.pilot):unit;
        }
        public MechDefinition ResolveMech(MechDefinition mech)=>mech!=null?OwnedMechs.FirstOrDefault(m=>DeploymentCondition.SameMech(m,mech)):null;
        public CharacterDefinition MechCrew(MechDefinition mech)=>recruitedAllies.FirstOrDefault(a=>DeploymentCondition.SameMech(a.mech,mech));
        internal RosterDeployment ResolveDeployment(BattleMapDefinition map,Func<string,bool> flags)
            =>new RosterDeployment(map,recruitedAllies,flags,true,reserveMechs,reservePilots);
        void RebindMech(MechDefinition previous,MechDefinition replacement)
        {
            for(int i=0;i<recruitedAllies.Count;i++)if(DeploymentCondition.SameMech(recruitedAllies[i].mech,previous))recruitedAllies[i]=Pair(recruitedAllies[i],replacement,recruitedAllies[i].pilot);
            int reserve=reserveMechs.IndexOf(previous);if(reserve>=0)reserveMechs[reserve]=replacement;
        }
        MechDefinition WritableMech(MechDefinition mech)
        {
            mech=ResolveMech(mech);if(mech==null)throw new InvalidOperationException("보유 기체를 선택하세요.");
            if(localMechs.TryGetValue(mech.SourceDefinition,out var existing)){if(mech!=existing)RebindMech(mech,existing);return existing;}
            var copy=UnityEngine.Object.Instantiate(mech);copy.hideFlags=HideFlags.DontSave;copy.sourceDefinition=mech.SourceDefinition;
            copy.weapons=new List<SkillDefinition>(mech.weapons??new());workshopObjects.Add(copy);localMechs[copy.SourceDefinition]=copy;
            RebindMech(mech,copy);return copy;
        }
        public string UpgradeError(MechDefinition mech,MechUpgradeType type)
        {
            mech=ResolveMech(mech);
            if(Phase!=CampaignPhase.Intermission)return "개조는 인터미션에서 할 수 있습니다.";
            if(mech==null || !Enum.IsDefined(typeof(MechUpgradeType),type))return "보유 기체와 개조 항목을 선택하세요.";
            int stage=mech.UpgradeStage(type);if(stage<0 || stage>=MaxUpgradeStage || stage==int.MaxValue)return "현재 최대 개조 단계에 도달했습니다.";
            return Funds<MechUpgradeCost.NextStage(type,stage)?"자금이 부족합니다.":null;
        }
        public bool Upgrade(MechDefinition mech,MechUpgradeType type,out string error)
        {
            error=UpgradeError(mech,type);if(error!=null)return false;
            mech=ResolveMech(mech);int next=mech.UpgradeStage(type)+1;long cost=MechUpgradeCost.NextStage(type,next-1);mech=WritableMech(mech);
            switch(type){case MechUpgradeType.Hp:mech.hpUpgrade=next;break;case MechUpgradeType.Energy:mech.energyUpgrade=next;break;
                case MechUpgradeType.Armor:mech.armorUpgrade=next;break;case MechUpgradeType.Targeting:mech.targetingUpgrade=next;break;case MechUpgradeType.Mobility:mech.mobilityUpgrade=next;break;case MechUpgradeType.Weapon:mech.weaponUpgrade=next;break;}
            Funds-=cost;Touch();return true;
        }
        public bool ChangePilot(MechDefinition mech,PilotDefinition pilot,out string error)
        {
            error=null;mech=ResolveMech(mech);pilot=OwnedPilots.FirstOrDefault(p=>DeploymentCondition.SamePilotDefinition(p,pilot));
            if(Phase!=CampaignPhase.Intermission || mech==null || pilot==null){error="인터미션에서 보유 기체와 파일럿을 선택하세요.";return false;}
            var crew=MechCrew(mech);var other=recruitedAllies.FirstOrDefault(a=>DeploymentCondition.SamePilotDefinition(a.pilot,pilot));
            if(crew==other && crew!=null)return true;
            if(crew!=null)
            {
                recruitedAllies[recruitedAllies.IndexOf(crew)]=Pair(crew,mech,pilot);
                if(other!=null)recruitedAllies[recruitedAllies.IndexOf(other)]=Pair(other,other.mech,crew.pilot);
                else{reservePilots.Remove(pilot);reservePilots.Add(crew.pilot);}
            }
            else
            {
                reserveMechs.Remove(mech);
                if(other!=null){recruitedAllies[recruitedAllies.IndexOf(other)]=Pair(other,mech,pilot);reserveMechs.Add(other.mech);}
                else{reservePilots.Remove(pilot);recruitedAllies.Add(Pair(null,mech,pilot));}
            }
            Touch();return true;
        }
        public string EquipError(MechDefinition mech,SkillDefinition weapon)
        {
            mech=ResolveMech(mech);
            if(Phase!=CampaignPhase.Intermission || mech==null || weapon==null)return "보유 기체와 무장을 선택하세요.";
            if(!spareWeapons.Contains(weapon))return "창고에 해당 무장이 없습니다.";
            if(mech.ValidateEquipment().Count>0)return "현재 기체 장비 설정을 확인하세요.";
            if(weapon.ValidateSkill().Count>0)return "무장 설정이 올바르지 않습니다.";
            if(mech.weapons.Contains(weapon))return "같은 무장은 중복 장착할 수 없습니다.";
            return mech.TotalWeaponWeight+weapon.weight>mech.maxWeaponWeight?"기체 허용중량을 초과합니다.":null;
        }
        public bool Equip(MechDefinition mech,SkillDefinition weapon,out string error)
        {error=EquipError(mech,weapon);if(error!=null)return false;WritableMech(mech).weapons.Add(weapon);spareWeapons.Remove(weapon);Touch();return true;}
        public bool Unequip(MechDefinition mech,SkillDefinition weapon,out string error)
        {
            error=null;mech=ResolveMech(mech);
            if(Phase!=CampaignPhase.Intermission || mech==null || weapon==null || !mech.weapons.Contains(weapon)){error="장착한 무장을 선택하세요.";return false;}
            WritableMech(mech).weapons.Remove(weapon);spareWeapons.Add(weapon);Touch();return true;
        }
        public string PurchaseError(ScriptableObject item)
        {
            if(Phase!=CampaignPhase.Intermission || Definition==null)return "구매는 인터미션에서 할 수 있습니다.";
            int price;
            switch(item)
            {
                case MechDefinition m:
                    if(!ShopMechs.Contains(m.SourceDefinition))return "판매 목록에 없는 기체입니다.";
                    if(ResolveMech(m)!=null)return "이미 보유한 기체입니다.";if(m.ValidateEquipment().Count>0)return "기체의 무장 설정을 확인하세요.";price=m.price;break;
                case PilotDefinition p:
                    if(!ShopPilots.Contains(p.SourceDefinition))return "영입 목록에 없는 파일럿입니다.";
                    if(OwnedPilots.Any(x=>DeploymentCondition.SamePilotDefinition(x,p)))return "이미 영입한 파일럿입니다.";
                    if(p.ValidateGrowth().Count>0)return "파일럿 성장 설정을 확인하세요.";price=p.price;break;
                case SkillDefinition w:
                    if(!ShopWeapons.Contains(w))return "판매 목록에 없는 무장입니다.";
                    if(w.ValidateSkill().Count>0)return "무장 설정을 확인하세요.";price=w.price;break;
                default:return "판매 항목을 선택하세요.";
            }
            return price<0?"가격은 0 이상이어야 합니다.":Funds<price?"자금이 부족합니다.":null;
        }
        public bool Purchase(ScriptableObject item,out string error)
        {
            error=PurchaseError(item);if(error!=null)return false;
            switch(item){case MechDefinition m:reserveMechs.Add(m);Funds-=m.price;break;case PilotDefinition p:reservePilots.Add(p);Funds-=p.price;break;case SkillDefinition w:spareWeapons.Add(w);Funds-=w.price;break;}
            Touch();return true;
        }
    }
}
