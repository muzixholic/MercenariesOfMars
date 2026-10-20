using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    [Serializable] public sealed class ConditionalShopAddition
    {
        [InspectorName("추가 목록 이름")] public string label="추가 판매";
        [InspectorName("클리어·플래그 조건（모두 만족）")] public List<StageCondition> resultConditions=new();
        [InspectorName("영입·보유·플래그 조건（모두 만족）")] public List<DeploymentCondition> rosterConditions=new();
        [InspectorName("추가 판매 기체")] public List<MechDefinition> mechs=new();
        [InspectorName("추가 판매 무장")] public List<SkillDefinition> weapons=new();
        [InspectorName("추가 영입 파일럿")] public List<PilotDefinition> pilots=new();
        public bool Matches(StageBattleResult result,Func<string,bool> flag,IReadOnlyList<CharacterDefinition> roster,IReadOnlyList<MechDefinition> ownedMechs,IReadOnlyList<PilotDefinition> ownedPilots)
            =>Validate().Count==0 && resultConditions.All(c=>c.Matches(result,flag)) && rosterConditions.All(c=>c.Matches(roster,flag,ownedMechs,ownedPilots));
        public List<string> Validate()
        {
            var errors=new List<string>();
            if(resultConditions==null || rosterConditions==null){errors.Add("조건 목록이 없습니다.");return errors;}
            if(resultConditions.Count+rosterConditions.Count==0)errors.Add("추가 판매 조건을 지정하세요. 무조건 판매는 기본 목록에 넣으세요.");
            foreach(var c in resultConditions)
            {
                if(c==null || !Enum.IsDefined(typeof(StageConditionKind),c.kind))errors.Add("잘못된 클리어·플래그 조건입니다.");
                else if((c.kind==StageConditionKind.Flag || c.kind==StageConditionKind.AllySurvived) && string.IsNullOrWhiteSpace(c.key))errors.Add("조건의 플래그 또는 유닛 ID를 지정하세요.");
                else if(c.value<0)errors.Add("조건 기준 수치는 0 이상이어야 합니다.");
            }
            foreach(var c in rosterConditions)
            {var error=c==null?"비어 있는 영입·보유 조건입니다.":c.Validate();if(error!=null)errors.Add(error);}
            StageShopSettings.CheckProducts(mechs,weapons,pilots,errors);
            return errors;
        }
    }
    [Serializable] public sealed class StageShopSettings
    {
        [InspectorName("캠페인 공통 판매 목록 포함")] public bool includeCampaignProducts=true;
        [InspectorName("기본 판매 기체")] public List<MechDefinition> mechs=new();
        [InspectorName("기본 판매 무장")] public List<SkillDefinition> weapons=new();
        [InspectorName("기본 영입 파일럿")] public List<PilotDefinition> pilots=new();
        [InspectorName("조건부 추가 판매 목록（일치한 항목 모두 합산）")] public List<ConditionalShopAddition> additions=new();
        internal static void CheckProducts(List<MechDefinition> mechs,List<SkillDefinition> weapons,List<PilotDefinition> pilots,List<string> errors)
        {
            if(mechs==null || weapons==null || pilots==null){errors.Add("판매 목록이 없습니다.");return;}
            if(mechs.Any(x=>x==null) || weapons.Any(x=>x==null) || pilots.Any(x=>x==null))errors.Add("비어 있는 판매 품목을 지정하거나 삭제하세요.");
        }
        public List<string> Validate()
        {
            var errors=new List<string>();CheckProducts(mechs,weapons,pilots,errors);
            if(additions==null)errors.Add("조건부 판매 목록이 없습니다.");
            else foreach(var item in additions)
                if(item==null)errors.Add("비어 있는 조건부 판매 목록입니다.");
                else errors.AddRange(item.Validate().Select(e=>item.label+": "+e));
            return errors;
        }
    }
    public sealed partial class CampaignFlow
    {
        IEnumerable<T> ShopProducts<T>(Func<StageShopSettings,IEnumerable<T>> basic,Func<ConditionalShopAddition,IEnumerable<T>> extra,IEnumerable<T> common) where T:ScriptableObject
        {
            var shop=CurrentStage?.shop;
            var products=shop==null || shop.includeCampaignProducts?common??Enumerable.Empty<T>():Enumerable.Empty<T>();
            if(shop!=null)
            {
                products=products.Concat(basic(shop)??Enumerable.Empty<T>());
                var mechs=OwnedMechs;var pilots=OwnedPilots;
                foreach(var item in shop.additions??new List<ConditionalShopAddition>())
                    if(item!=null && item.Matches(LastResult,key=>owner.eventRunner!=null && owner.eventRunner.GetFlag(key),recruitedAllies,mechs,pilots))
                        products=products.Concat(extra(item)??Enumerable.Empty<T>());
            }
            return products.Where(p=>p!=null).Distinct();
        }
        public List<MechDefinition> ShopMechs=>ShopProducts(s=>s.mechs,s=>s.mechs,Definition?.shopMechs).ToList();
        public List<SkillDefinition> ShopWeapons=>ShopProducts(s=>s.weapons,s=>s.weapons,Definition?.shopWeapons).ToList();
        public List<PilotDefinition> ShopPilots=>ShopProducts(s=>s.pilots,s=>s.pilots,Definition?.shopPilots).ToList();
    }
}
