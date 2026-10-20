using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class IntermissionIntegrationTests
    {
        readonly List<Object> owned=new();
        BattleController c;CharacterDefinition a,b;StageDefinition next;
        MechDefinition shopMech;PilotDefinition shopPilot;SkillDefinition shopWeapon;
        T New<T>() where T:ScriptableObject{var o=ScriptableObject.CreateInstance<T>();owned.Add(o);return o;}
        CharacterDefinition Ally(string name)
        {var u=New<CharacterDefinition>();u.mech=New<MechDefinition>();u.mech.displayName=name;u.pilot=New<PilotDefinition>();u.pilot.displayName=name+" 파일럿";return u;}
        StageDefinition Stage(CharacterDefinition ally)
        {
            var s=New<StageDefinition>();s.defaultDestination.kind=StageDestinationKind.Ending;s.map=New<BattleMapDefinition>();s.map.width=5;s.map.height=5;
            var enemy=New<CharacterDefinition>();enemy.faction=Faction.Enemy;s.map.units.Add(new UnitPlacement {id="고정",character=ally,cell=Vector2Int.zero});
            s.map.units.Add(new UnitPlacement {id="적",character=enemy,cell=new Vector2Int(4,4)});return s;
        }
        IEnumerator Phase(CampaignPhase phase)
        {for(int i=0;i<90 && c.Campaign.Phase!=phase;i++)yield return null;Assert.That(c.Campaign.Phase,Is.EqualTo(phase),c.Campaign.Error);}
        IEnumerator Open(long funds=1000000)
        {
            a=Ally("기체 A");b=Ally("기체 B");var initial=New<SkillDefinition>();initial.power=1000;initial.weight=5;a.mech.weapons.Add(initial);a.mech.maxWeaponWeight=25;
            var first=Stage(a);next=Stage(a);next.map.deploymentSlots.Add(new AllyDeploymentSlot {id="선택1",cell=Vector2Int.right});next.map.deploymentSlots.Add(new AllyDeploymentSlot {id="선택2",cell=new Vector2Int(2,0)});
            first.defaultDestination=new StageDestination {stage=next};shopMech=New<MechDefinition>();shopMech.price=20000;shopPilot=New<PilotDefinition>();shopPilot.price=30000;
            shopWeapon=New<SkillDefinition>();shopWeapon.price=1000;shopWeapon.weight=20;
            var go=new GameObject("인터미션 검증");owned.Add(go);go.SetActive(false);c=go.AddComponent<BattleController>();c.board=go.AddComponent<GridBoard>();c.eventRunner=go.AddComponent<EventRunner>();c.battleCamera=go.AddComponent<Camera>();
            c.showControls=false;c.automaticEnemy=false;c.enableCombatPresentation=false;c.campaign=New<CampaignDefinition>();c.campaign.firstStage=first;c.campaign.initialFunds=funds;c.campaign.initialAllies.AddRange(new[]{a,b});
            c.campaign.shopMechs.Add(shopMech);c.campaign.shopPilots.Add(shopPilot);c.campaign.shopWeapons.Add(shopWeapon);go.SetActive(true);yield return null;
            Assert.That(c.RestartBattle(),Is.True);yield return Phase(CampaignPhase.Battle);c.Session.ChangeHp("적",-999);yield return Phase(CampaignPhase.Intermission);
        }
        CampaignAssetCatalog Catalog()
        {
            var catalog=New<CampaignAssetCatalog>();foreach(var o in owned.OfType<ScriptableObject>().Where(o=>o!=catalog))catalog.entries.Add(new CampaignAssetEntry {id=Guid.NewGuid().ToString("N"),asset=o});return catalog;
        }
        [UnityTearDown] public IEnumerator Cleanup(){foreach(var o in owned)if(o!=null)Object.Destroy(o);owned.Clear();yield return null;}
        [UnityTest] public IEnumerator 개조는_비용을_차감하고_원본을_보존하며_다음_고정출격에_반영된다()
        {
            yield return Open();Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.True);Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.True);
            Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Weapon,out _),Is.True);Assert.That(c.Campaign.Funds,Is.EqualTo(986000));
            Assert.That(a.mech.hpUpgrade,Is.Zero);Assert.That(a.mech.weaponUpgrade,Is.Zero);Assert.That(a.mech.weapons[0].power,Is.EqualTo(1000));
            Assert.That(c.Campaign.NextDeployment().Error,Is.Null);Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.HasFormationNotice,Is.False);c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);
            Assert.That(c.Session.Find("고정").MaxHp,Is.EqualTo(1100));Assert.That(c.Session.Find("고정").Skills[0].Power,Is.EqualTo(1050));
        }
        [UnityTest] public IEnumerator 자금부족과_개조상한은_진행을_바꾸지않는다()
        {
            yield return Open(4999);Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Weapon,out var error),Is.False);Assert.That(error,Does.Contain("자금"));Assert.That(c.Campaign.Funds,Is.EqualTo(4999));
            Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.True);Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.False);Assert.That(c.Campaign.Funds,Is.EqualTo(1999));
        }
        [UnityTest] public IEnumerator 현재_개조상한을_넘어_결제하지않고_해금후에만_추가개조한다()
        {
            yield return Open();for(int i=0;i<10;i++)Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.True);
            long funds=c.Campaign.Funds;Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out var error),Is.False);Assert.That(error,Does.Contain("최대"));Assert.That(c.Campaign.Funds,Is.EqualTo(funds));
            c.Campaign.IncreaseMaxUpgradeStage(1);Assert.That(c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _),Is.True);Assert.That(c.Campaign.Funds,Is.EqualTo(funds-33000));
        }
        [UnityTest] public IEnumerator 창고와_장착은_중량과_중복을_검사하고_원본목록을_보존한다()
        {
            yield return Open();Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.True);Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.True);
            Assert.That(c.Campaign.Equip(a.mech,shopWeapon,out _),Is.True);Assert.That(c.Campaign.Equip(a.mech,shopWeapon,out var error),Is.False);Assert.That(error,Does.Contain("중복"));
            Assert.That(a.mech.weapons,Has.Count.EqualTo(1));Assert.That(c.Campaign.ResolveMech(a.mech).TotalWeaponWeight,Is.EqualTo(25));Assert.That(c.Campaign.SpareWeapons,Has.Count.EqualTo(1));
            Assert.That(c.Campaign.Unequip(a.mech,shopWeapon,out _),Is.True);Assert.That(c.Campaign.SpareWeapons,Has.Count.EqualTo(2));
            var heavy=New<SkillDefinition>();heavy.weight=26;c.campaign.shopWeapons.Add(heavy);c.Campaign.Purchase(heavy,out _);Assert.That(c.Campaign.Equip(a.mech,heavy,out error),Is.False);Assert.That(error,Does.Contain("중량"));
        }
        [UnityTest] public IEnumerator 파일럿교환은_양쪽_편성과_자원을_보존한다()
        {
            yield return Open();Assert.That(c.Campaign.ChangePilot(a.mech,b.pilot,out _),Is.True);
            Assert.That(c.Campaign.MechCrew(a.mech).pilot,Is.SameAs(b.pilot));Assert.That(c.Campaign.MechCrew(b.mech).pilot,Is.SameAs(a.pilot));
            Assert.That(c.Campaign.OwnedMechs,Has.Count.EqualTo(2));Assert.That(c.Campaign.OwnedPilots,Has.Count.EqualTo(2));Assert.That(a.pilot.displayName,Is.EqualTo("기체 A 파일럿"));
            var before=c.Campaign.RecruitedAllies.ToArray();c.Campaign.NextDeployment();Assert.That(c.Campaign.RecruitedAllies,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator 구매한_대기기체와_파일럿을_편성하고_중복구매를_막는다()
        {
            yield return Open();Assert.That(c.Campaign.Purchase(shopMech,out _),Is.True);Assert.That(c.Campaign.Purchase(shopPilot,out _),Is.True);Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(2));
            Assert.That(c.Campaign.Purchase(shopMech,out _),Is.False);Assert.That(c.Campaign.Purchase(shopPilot,out _),Is.False);Assert.That(c.Campaign.Funds,Is.EqualTo(950000));
            Assert.That(c.Campaign.ChangePilot(shopMech,shopPilot,out _),Is.True);Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(3));Assert.That(c.Campaign.NextDeployment().SelectableCount,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator 대기자원의_조건부_강제출격은_미리보기후_확인할때_편성에_반영한다()
        {
            yield return Open();c.Campaign.Purchase(shopMech,out _);c.Campaign.Purchase(shopPilot,out _);
            var target=New<CharacterDefinition>();target.mech=shopMech;target.pilot=shopPilot;
            next.map.units.Add(new UnitPlacement {id="구매증원",character=target,cell=new Vector2Int(3,0),conditions=new(){
                new DeploymentCondition {kind=DeploymentConditionKind.OwnedMech,mech=shopMech},
                new DeploymentCondition {kind=DeploymentConditionKind.RecruitedPilot,pilot=shopPilot}}});
            var preview=c.Campaign.NextDeployment();Assert.That(preview.Error,Is.Null);Assert.That(preview.ForcedCount,Is.EqualTo(2));Assert.That(preview.SelectableCount,Is.EqualTo(1));
            Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(2));Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.HasFormationNotice,Is.True);
            Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(2));Assert.That(c.ConfirmFormationNotice(),Is.True);Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(3));
            Assert.That(c.Campaign.OwnedMechs,Has.Count.EqualTo(3));Assert.That(c.Campaign.OwnedPilots,Has.Count.EqualTo(3));
            c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Find("구매증원").Definition.mech,Is.SameAs(shopMech));
        }
        [UnityTest] public IEnumerator 기체만_구매해_기존파일럿을_옮겨도_이전기체는_대기로_보존한다()
        {
            yield return Open();c.Campaign.Purchase(shopMech,out _);Assert.That(c.Campaign.ChangePilot(shopMech,a.pilot,out _),Is.True);
            Assert.That(c.Campaign.MechCrew(a.mech),Is.Null);Assert.That(c.Campaign.OwnedMechs,Has.Count.EqualTo(3));
            c.Campaign.Purchase(shopPilot,out _);Assert.That(c.Campaign.ChangePilot(a.mech,shopPilot,out _),Is.True);Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(3));
        }
        [UnityTest] public IEnumerator 다음미션_미리보기는_준비스크립트의_부작용없이_실제출격수를_예측한다()
        {
            yield return Open();var extra=Ally("추가");next.map.units.Add(new UnitPlacement {id="추가",character=extra,cell=new Vector2Int(3,0),conditions=new(){new DeploymentCondition {kind=DeploymentConditionKind.Flag,flag="증원"}}});
            next.deploymentSetup=New<EventSequence>();next.deploymentSetup.commands.AddRange(new[]{new EventCommand {kind=EventCommandKind.RecruitAlly,rosterAlly=extra},new EventCommand {kind=EventCommandKind.SetFlag,flag="증원"},
                new EventCommand {kind=EventCommandKind.SetDeploymentBlocked,rosterAlly=b},new EventCommand {kind=EventCommandKind.IncreaseMaxUpgradeStage,upgradeStageIncrease=5}});
            var result=c.Campaign.NextDeployment();Assert.That(result.Error,Is.Null);Assert.That(result.SelectableCount,Is.Zero);Assert.That(result.ForcedCount,Is.EqualTo(2));Assert.That(result.SlotCount,Is.EqualTo(2));
            Assert.That(c.Campaign.MaxUpgradeStage,Is.EqualTo(10));Assert.That(c.Campaign.IsRecruited(extra),Is.False);Assert.That(c.eventRunner.GetFlag("증원"),Is.False);Assert.That(c.Campaign.IsDeploymentBlocked(b),Is.False);
            Assert.That(c.Campaign.NextDeployment(),Is.SameAs(result));Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.Campaign.MaxUpgradeStage,Is.EqualTo(15));Assert.That(c.Campaign.IsRecruited(extra),Is.True);
            Assert.That(c.Deployment.Candidates.Count(x=>c.Deployment.CanAssign("선택1",x)),Is.EqualTo(result.AvailableCount));
        }
        [UnityTest] public IEnumerator 저장은_개조_창고_대기자원_자금_편성_분기를_복원하고_다음미션으로_진행한다()
        {
            yield return Open();c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _);c.Campaign.Purchase(shopWeapon,out _);c.Campaign.Purchase(shopMech,out _);c.Campaign.Purchase(shopPilot,out _);
            var catalog=Catalog();string save=c.Campaign.SerializeSave(catalog);long funds=c.Campaign.Funds;
            c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _);Assert.That(c.Campaign.RestoreSave(save,catalog,out var error),Is.True,error);
            Assert.That(c.Campaign.Funds,Is.EqualTo(funds));Assert.That(c.Campaign.ResolveMech(a.mech).hpUpgrade,Is.EqualTo(1));Assert.That(c.Campaign.SpareWeapons,Has.Count.EqualTo(1));
            Assert.That(c.Campaign.OwnedMechs,Has.Count.EqualTo(3));Assert.That(c.Campaign.OwnedPilots,Has.Count.EqualTo(3));Assert.That(c.Campaign.NextStage,Is.SameAs(next));
            Assert.That(c.Campaign.NextDeployment().Error,Is.Null);Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.HasFormationNotice,Is.False);c.Deployment.AutoFill();Assert.That(c.ConfirmDeployment(),Is.True);Assert.That(c.Session.Find("고정").MaxHp,Is.EqualTo(1050));
        }
        [UnityTest] public IEnumerator 손상되거나_참조가누락된_저장은_현재상태를_바꾸지않는다()
        {
            yield return Open();var catalog=Catalog();var original=c.Campaign.RecruitedAllies.ToArray();long funds=c.Campaign.Funds;var dto=JsonUtility.FromJson<CampaignSaveData>(c.Campaign.SerializeSave(catalog));
            dto.mechs[0].weapons.Add("없는 참조");Assert.That(c.Campaign.RestoreSave(JsonUtility.ToJson(dto),catalog,out _),Is.False);
            Assert.That(c.Campaign.RestoreSave("{}",catalog,out _),Is.False);Assert.That(c.Campaign.RestoreSave("잘못된 데이터",catalog,out _),Is.False);
            Assert.That(c.Campaign.Funds,Is.EqualTo(funds));Assert.That(c.Campaign.RecruitedAllies,Is.EqualTo(original));Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Intermission));
        }
        [UnityTest] public IEnumerator 타이틀에서_저장을_복원하고_파일덮어쓰기는_이전내용을_보관한다()
        {
            yield return Open();var catalog=Catalog();string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"인터미션저장검증_"+Guid.NewGuid().ToString("N"));string path=System.IO.Path.Combine(directory,"저장.json");
            try
            {
                Assert.That(CampaignSaveFiles.SaveToPath(c.Campaign,catalog,path,out var error),Is.True,error);string previous=System.IO.File.ReadAllText(path);
                c.Campaign.Upgrade(a.mech,MechUpgradeType.Hp,out _);Assert.That(CampaignSaveFiles.SaveToPath(c.Campaign,catalog,path,out error),Is.True,error);Assert.That(System.IO.File.ReadAllText(path+".bak"),Is.EqualTo(previous));
                string saved=System.IO.File.ReadAllText(path);Assert.That(c.ReturnToTitle(),Is.True);Assert.That(c.IsTitleScreen,Is.True);Assert.That(c.Campaign.RestoreSave(saved,catalog,out error),Is.True,error);
                Assert.That(c.IsTitleScreen,Is.False);Assert.That(c.Campaign.Phase,Is.EqualTo(CampaignPhase.Intermission));Assert.That(c.Campaign.ResolveMech(a.mech).hpUpgrade,Is.EqualTo(1));
            }
            finally{if(System.IO.Directory.Exists(directory))System.IO.Directory.Delete(directory,true);}
        }
        [UnityTest] public IEnumerator 다음미션_필수자원_누락은_출격수_대신_오류를_표시한다()
        {
            yield return Open();next.map.units[0].character=Ally("미영입");var result=c.Campaign.NextDeployment();
            Assert.That(result.Error,Does.Contain("필수 아군"));Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(2));Assert.That(c.Campaign.Funds,Is.EqualTo(1000000));
        }
        [UnityTest] public IEnumerator 스테이지_기본판매는_세품목에_적용하고_공통목록을_선택적으로_제외한다()
        {
            yield return Open();var shop=c.Campaign.CurrentStage.shop;shop.includeCampaignProducts=false;
            Assert.That(c.Campaign.Purchase(shopMech,out _),Is.False);Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.False);Assert.That(c.Campaign.Purchase(shopPilot,out _),Is.False);
            shop.mechs.AddRange(new[]{shopMech,shopMech});shop.weapons.Add(shopWeapon);shop.pilots.Add(shopPilot);
            Assert.That(c.Campaign.ShopMechs,Has.Count.EqualTo(1));Assert.That(c.Campaign.Purchase(shopMech,out _),Is.True);
            Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.True);Assert.That(c.Campaign.Purchase(shopPilot,out _),Is.True);Assert.That(c.Campaign.Funds,Is.EqualTo(949000));
        }
        [UnityTest] public IEnumerator 조건부판매는_플래그와_결과의_모든조건을_만족해야_구매할수있다()
        {
            yield return Open();var shop=c.Campaign.CurrentStage.shop;shop.includeCampaignProducts=false;
            shop.additions.Add(new ConditionalShopAddition {weapons=new(){shopWeapon},resultConditions=new(){
                new StageCondition {kind=StageConditionKind.Flag,key="특별상점"},
                new StageCondition {kind=StageConditionKind.ClearTurnsAtMost,value=1},
                new StageCondition {kind=StageConditionKind.DefeatedEnemiesAtLeast,value=1}}});
            Assert.That(c.Campaign.ShopWeapons,Is.Empty);Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.False);
            c.eventRunner.RestoreFlags(new Dictionary<string,bool>{{"특별상점",true}});Assert.That(c.Campaign.ShopWeapons,Is.EqualTo(new[]{shopWeapon}));
            Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.True);
            c.eventRunner.RestoreFlags(new Dictionary<string,bool>());Assert.That(c.Campaign.Purchase(shopWeapon,out _),Is.False);
            shop.additions[0].resultConditions.RemoveAt(0);shop.additions[0].resultConditions[0].value=0;Assert.That(c.Campaign.ShopWeapons,Is.Empty);
        }
        [UnityTest] public IEnumerator 대기자원_보유로_판매목록을_추가하고_여러추가목록은_합산한다()
        {
            yield return Open();var shop=c.Campaign.CurrentStage.shop;
            var special=New<SkillDefinition>();special.price=100;
            shop.additions.Add(new ConditionalShopAddition {weapons=new(){special,shopWeapon},rosterConditions=new(){new DeploymentCondition {kind=DeploymentConditionKind.OwnedMech,mech=shopMech}}});
            shop.additions.Add(new ConditionalShopAddition {pilots=new(){shopPilot},rosterConditions=new(){new DeploymentCondition {kind=DeploymentConditionKind.RecruitedAlly,ally=b}}});
            Assert.That(c.Campaign.ShopWeapons,Has.No.Member(special));Assert.That(c.Campaign.Purchase(shopMech,out _),Is.True);
            Assert.That(c.Campaign.ShopWeapons,Is.EquivalentTo(new[]{shopWeapon,special}));Assert.That(c.Campaign.ShopPilots,Has.Count.EqualTo(1));Assert.That(c.Campaign.Purchase(special,out _),Is.True);
            Assert.That(c.Campaign.RecruitedAllies,Has.Count.EqualTo(2));
        }
        [UnityTest] public IEnumerator 조건부판매는_저장에서_복원하고_다음스테이지로_자동누적하지않는다()
        {
            yield return Open();var stage=c.Campaign.CurrentStage;stage.shop.includeCampaignProducts=false;
            stage.shop.additions.Add(new ConditionalShopAddition {weapons=new(){shopWeapon},resultConditions=new(){new StageCondition {kind=StageConditionKind.Flag,key="상점해금"}}});
            c.eventRunner.RestoreFlags(new Dictionary<string,bool>{{"상점해금",true}});var catalog=Catalog();string saved=c.Campaign.SerializeSave(catalog);
            c.eventRunner.RestoreFlags(new Dictionary<string,bool>());Assert.That(c.Campaign.ShopWeapons,Is.Empty);
            Assert.That(c.Campaign.RestoreSave(saved,catalog,out var error),Is.True,error);Assert.That(c.Campaign.ShopWeapons,Is.EqualTo(new[]{shopWeapon}));
            next.shop.includeCampaignProducts=false;Assert.That(c.Campaign.Depart(),Is.True);Assert.That(c.Campaign.CurrentStage,Is.SameAs(next));Assert.That(c.Campaign.ShopWeapons,Is.Empty);
            Assert.That(stage.shop.weapons,Is.Empty);Assert.That(stage.shop.additions,Has.Count.EqualTo(1));
        }
        [UnityTest] public IEnumerator 인터미션_모든페이지는_실제화면에_표시된다()
        {
            yield return Open();c.showControls=true;
            var field=typeof(BattleController).GetField("intermissionPage",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var page in new[]{"기체 리스트","기체 개조","무장 변경","파일럿 리스트","파일럿 변경","기체 상점","무장 상점","파일럿 상점","저장","불러오기","설정","종료"}){field.SetValue(c,page);yield return null;}
            Assert.That(c.Campaign.NextDeployment().SelectableCount,Is.EqualTo(1));
        }
    }
}
