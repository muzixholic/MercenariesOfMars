using System;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public sealed partial class BattleController
    {
        MechDefinition workshopMech;
        PilotDefinition workshopPilot;
        string workshopMessage;
        Action workshopConfirmation;
        string workshopConfirmationText;
        bool titleLoadOpen,preferencesLoaded;
        void AskWorkshop(string text,Action action){workshopConfirmationText=text;workshopConfirmation=action;}
        void DrawWorkshopConfirmation()
        {
            TacticalGuiTheme.DimScreen();uiTheme.BeginPanel(TacticalGuiTheme.Centered(610,340),"작업 확인");
            GUILayout.Label(workshopConfirmationText,uiTheme.Heading);GUILayout.FlexibleSpace();
            bool ok=GUILayout.Button("확인",uiTheme.Primary,GUILayout.Height(40));bool cancel=GUILayout.Button("취소",GUILayout.Height(36));TacticalGuiTheme.EndPanel();
            if(cancel)workshopConfirmation=null;
            if(ok){var action=workshopConfirmation;workshopConfirmation=null;action?.Invoke();}
        }
        void WorkshopResult(bool success,string message){workshopMessage=success?"완료: "+message:message;}
        MechDefinition DrawWorkshopMechs()
        {
            var mechs=Campaign.OwnedMechs;
            if(mechs.Count==0){GUILayout.Label("보유 기체가 없습니다.",uiTheme.Muted);return null;}
            workshopMech=Campaign.ResolveMech(workshopMech)??mechs[0];int index=mechs.IndexOf(workshopMech);
            var labels=mechs.Select(m=>m.displayName+"\n"+(Campaign.MechCrew(m)?.PilotName??"미탑승")).ToArray();
            index=GUILayout.SelectionGrid(Mathf.Max(0,index),labels,Mathf.Min(3,mechs.Count));workshopMech=mechs[index];uiTheme.Separator();return workshopMech;
        }
        void DrawMechDetails(MechDefinition mech)
        {
            GUILayout.Label(mech.displayName,uiTheme.Heading);GUILayout.Label("파일럿  "+(Campaign.MechCrew(mech)?.PilotName??"미탑승"));
            GUILayout.Label($"HP {mech.MaxHpAtLimit(MaxUpgradeStage):N0} · EN {mech.MaxEnergyAtLimit(MaxUpgradeStage):N0} · 이동력 {mech.movement}");
            GUILayout.Label($"장갑치 {mech.ArmorAtLimit(MaxUpgradeStage):N0} · 조준치 {mech.TargetingAtLimit(MaxUpgradeStage):N0} · 운동성 {mech.MobilityAtLimit(MaxUpgradeStage):N0}");
            GUILayout.Label($"무장 중량 {mech.TotalWeaponWeight}/{mech.maxWeaponWeight} · 가격 {mech.price:N0}",uiTheme.Muted);
            GUILayout.Label($"개조  HP {mech.hpUpgrade} / EN {mech.energyUpgrade} / 장갑 {mech.armorUpgrade} / 조준 {mech.targetingUpgrade} / 운동 {mech.mobilityUpgrade} / 무장 {mech.weaponUpgrade}",uiTheme.Muted);
            if(!string.IsNullOrEmpty(mech.description))GUILayout.Label(mech.description);
        }
        void DrawUpgradeMenu(MechDefinition mech)
        {
            DrawMechDetails(mech);uiTheme.Separator();
            var types=(MechUpgradeType[])Enum.GetValues(typeof(MechUpgradeType));var names=new[]{"HP","EN","장갑치","조준치","운동성","무장"};
            foreach(var type in types)
            {
                int stage=mech.UpgradeStage(type);string reason=Campaign.UpgradeError(mech,type);
                GUILayout.BeginHorizontal();GUILayout.Label($"{names[(int)type]}  {stage} / {MaxUpgradeStage}",GUILayout.Width(170*TacticalGuiTheme.LayoutScale));
                GUILayout.Label(stage<int.MaxValue?$"다음 단계 {MechUpgradeCost.NextStage(type,stage):N0}":"최대 단계");
                bool before=GUI.enabled;GUI.enabled=before && reason==null;
                if(GUILayout.Button("1단계 개조",GUILayout.Width(140*TacticalGuiTheme.LayoutScale)))
                    AskWorkshop($"{mech.displayName} · {names[(int)type]} {stage} → {stage+1}\n{MechUpgradeCost.NextStage(type,stage):N0}을 사용합니다.",()=>{var ok=Campaign.Upgrade(mech,type,out var message);WorkshopResult(ok,message??"개조했습니다.");});
                GUI.enabled=before;GUILayout.EndHorizontal();if(reason!=null)GUILayout.Label(reason,uiTheme.Muted);
            }
        }
        void DrawWeaponChange(MechDefinition mech)
        {
            DrawMechDetails(mech);GUILayout.Label("장착 무장",uiTheme.Section);
            foreach(var w in mech.weapons.Where(w=>w!=null).ToList())
            {
                GUILayout.BeginHorizontal();GUILayout.Label($"{w.displayName} · 공격 {MechDefinition.ApplyUpgrade(w.power,Mathf.Min(mech.weaponUpgrade,MaxUpgradeStage))} · 무게 {w.weight}");
                if(GUILayout.Button("해제",GUILayout.Width(90)))AskWorkshop(w.displayName+"을 창고로 돌려보냅니다.",()=>{var ok=Campaign.Unequip(mech,w,out var message);WorkshopResult(ok,message??"무장을 해제했습니다.");});GUILayout.EndHorizontal();
            }
            GUILayout.Space(12);GUILayout.Label("창고 무장",uiTheme.Section);
            if(Campaign.SpareWeapons.Count==0)GUILayout.Label("창고가 비어 있습니다. 상점에서 무장을 구매하거나 다른 기체에서 해제하세요.",uiTheme.Muted);
            foreach(var w in Campaign.SpareWeapons.Distinct().ToList())
            {
                string reason=Campaign.EquipError(mech,w);GUILayout.BeginHorizontal();GUILayout.Label($"{w.displayName} × {Campaign.SpareWeapons.Count(x=>x==w)} · 무게 {w.weight}");
                bool before=GUI.enabled;GUI.enabled=before && reason==null;
                if(GUILayout.Button("장착",GUILayout.Width(90)))AskWorkshop(w.displayName+"을 장착합니다.",()=>{var ok=Campaign.Equip(mech,w,out var message);WorkshopResult(ok,message??"무장을 장착했습니다.");});
                GUI.enabled=before;GUILayout.EndHorizontal();if(reason!=null)GUILayout.Label(reason,uiTheme.Muted);
            }
        }
        void DrawPilotChange(MechDefinition mech)
        {
            DrawMechDetails(mech);uiTheme.Separator();GUILayout.Label("탑승할 파일럿",uiTheme.Section);
            if(Campaign.OwnedPilots.Count==0)GUILayout.Label("영입한 파일럿이 없습니다.",uiTheme.Muted);
            foreach(var pilot in Campaign.OwnedPilots)
            {
                var current=Campaign.RecruitedAllies.FirstOrDefault(a=>DeploymentCondition.SamePilotDefinition(a.pilot,pilot));
                bool before=GUI.enabled;GUI.enabled=before && !DeploymentCondition.SameMech(current?.mech,mech);
                if(GUILayout.Button($"{pilot.displayName} · Lv.{pilot.level} · {(current!=null?current.UnitName:"대기 중")}"))
                    AskWorkshop($"{mech.displayName}에 {pilot.displayName}을 배정합니다.\n기존 탑승자가 있으면 서로 교환하거나 대기 상태로 돌립니다.",()=>{var ok=Campaign.ChangePilot(mech,pilot,out var message);WorkshopResult(ok,message??"파일럿을 변경했습니다.");});
                GUI.enabled=before;
            }
        }
        void DrawPilotList()
        {
            var pilots=Campaign.OwnedPilots;if(pilots.Count==0){GUILayout.Label("영입한 파일럿이 없습니다.");return;}
            workshopPilot=pilots.FirstOrDefault(p=>DeploymentCondition.SamePilotDefinition(p,workshopPilot))??pilots[0];
            int index=GUILayout.SelectionGrid(pilots.IndexOf(workshopPilot),pilots.Select(p=>p.displayName).ToArray(),Mathf.Min(3,pilots.Count));workshopPilot=pilots[index];
            var stats=workshopPilot.StatsAtLevel(workshopPilot.level);var crew=Campaign.RecruitedAllies.FirstOrDefault(a=>DeploymentCondition.SamePilotDefinition(a.pilot,workshopPilot));
            uiTheme.Separator();GUILayout.Label(workshopPilot.displayName,uiTheme.Heading);GUILayout.Label($"Lv.{workshopPilot.level} · 경험치 {workshopPilot.experience:N0} · {(crew!=null?crew.UnitName:"대기 중")}");
            GUILayout.Label($"공격력 {stats.Attack} · 방어력 {stats.Defense} · 명중력 {stats.Accuracy} · 회피력 {stats.Evasion}");GUILayout.Label(workshopPilot.description??"");
        }
        void DrawShop<T>(System.Collections.Generic.IEnumerable<T> items) where T:ScriptableObject
        {
            var list=items?.Where(x=>x!=null).Distinct().ToList();if(list==null || list.Count==0){GUILayout.Label("현재 판매 조건을 만족하는 등록 품목이 없습니다.",uiTheme.Muted);return;}
            foreach(var item in list)
            {
                string name=item.name;int price=0;string detail="";
                if(item is MechDefinition m){name=m.displayName;price=m.price;detail=$"HP {m.MaxHpAtLimit(MaxUpgradeStage):N0} · EN {m.MaxEnergyAtLimit(MaxUpgradeStage):N0} · 허용중량 {m.maxWeaponWeight}";}
                if(item is PilotDefinition p){name=p.displayName;price=p.price;detail=$"Lv.{p.level} · 영입 후 파일럿 변경에서 배정하세요.";}
                if(item is SkillDefinition w){name=w.displayName;price=w.price;detail=$"공격 {w.power} · 사거리 {w.minRange}~{w.maxRange} · EN {w.energyCost} · 무게 {w.weight}";}
                GUILayout.Label(name,uiTheme.Heading);GUILayout.Label(detail);GUILayout.BeginHorizontal();GUILayout.Label($"가격 {price:N0}");string reason=Campaign.PurchaseError(item);
                bool before=GUI.enabled;GUI.enabled=before && reason==null;
                if(GUILayout.Button("구매",GUILayout.Width(110)))AskWorkshop($"{name}을 {price:N0}에 구매합니다.",()=>{var ok=Campaign.Purchase(item,out var message);WorkshopResult(ok,message??"구매했습니다.");});
                GUI.enabled=before;GUILayout.EndHorizontal();if(reason!=null)GUILayout.Label(reason,uiTheme.Muted);uiTheme.Separator();
            }
        }
        void DrawSaveSlots(bool load)
        {
            GUILayout.Label(load?"인터미션 저장 데이터를 불러옵니다.":"현재 자금·편성·개조·장비·창고·다음 미션을 저장합니다.",uiTheme.Muted);
            for(int slot=1;slot<=3;slot++)
            {
                int selected=slot;
                if(GUILayout.Button(CampaignSaveFiles.Label(slot)+(load?" · 불러오기":" · 저장"),GUILayout.Height(42)))
                    AskWorkshop(load?"현재 진행을 저장 데이터로 교체합니다.":$"슬롯 {slot}에 저장합니다. 기존 데이터가 있으면 덮어씁니다.",()=>
                    {
                        Campaign??=new CampaignFlow(this);string message;bool ok=load?CampaignSaveFiles.Load(Campaign,selected,out message):CampaignSaveFiles.Save(Campaign,selected,out message);
                        WorkshopResult(ok,message);if(ok && load){titleLoadOpen=false;intermissionPage="작전 대기";}
                    });
            }
        }
        void DrawTitleLoad()
        {
            uiTheme.BeginPanel(TacticalGuiTheme.Centered(650,450),"저장 데이터 불러오기");DrawSaveSlots(true);
            if(!string.IsNullOrEmpty(workshopMessage))GUILayout.Label(workshopMessage,uiTheme.Muted);GUILayout.FlexibleSpace();bool back=GUILayout.Button("돌아가기",GUILayout.Height(36));TacticalGuiTheme.EndPanel();if(back)titleLoadOpen=false;
        }
        internal void PrepareLoadedIntermission(CampaignFlow flow,CampaignDefinition definition)
        {
            if(Campaign!=null && Campaign!=flow)Campaign.Stop();
            eventRunner??=GetComponent<EventRunner>()??gameObject.AddComponent<EventRunner>();eventRunner.ResetState();
            ReleaseDeployment();Deployment=null;deploymentPlacements=null;ClearSelection();
            if(Session!=null)Session.Changed-=SyncViews;Session=null;
            foreach(var view in views.Values)if(view!=null){view.gameObject.SetActive(false);Destroy(view.gameObject);}views.Clear();
            foreach(var material in materials)if(material!=null)Destroy(material);materials.Clear();
            Campaign=flow;campaign=definition;IsTitleScreen=false;intermissionPage="작전 대기";workshopMech=null;workshopPilot=null;
        }
        void LoadPreferences()
        {
            if(preferencesLoaded)return;preferencesLoaded=true;
            if(PlayerPrefs.HasKey("MercenariesOfMars.음량"))AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat("MercenariesOfMars.음량"));
            if(board!=null && PlayerPrefs.HasKey("MercenariesOfMars.격자"))board.showGrid=PlayerPrefs.GetInt("MercenariesOfMars.격자")!=0;
            if(PlayerPrefs.HasKey("MercenariesOfMars.연출"))enableCombatPresentation=PlayerPrefs.GetInt("MercenariesOfMars.연출")!=0;
        }
        void DrawPersistentSettings()
        {
            bool grid=GUILayout.Toggle(board.showGrid,"격자 표시",GUI.skin.button);bool presentation=GUILayout.Toggle(enableCombatPresentation,"전투 연출",GUI.skin.button);
            GUILayout.Label($"음량 {Mathf.RoundToInt(AudioListener.volume*100)}%");float volume=GUILayout.HorizontalSlider(AudioListener.volume,0,1);
            if(grid!=board.showGrid || presentation!=enableCombatPresentation || !Mathf.Approximately(volume,AudioListener.volume))
            {board.showGrid=grid;enableCombatPresentation=presentation;AudioListener.volume=volume;PlayerPrefs.SetInt("MercenariesOfMars.격자",grid?1:0);PlayerPrefs.SetInt("MercenariesOfMars.연출",presentation?1:0);PlayerPrefs.SetFloat("MercenariesOfMars.음량",volume);PlayerPrefs.Save();}
        }
    }
}
