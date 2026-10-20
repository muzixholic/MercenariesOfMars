using System;
using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    [Flags] public enum MovementEnvironment
    {
        [InspectorName("없음")] None=0,
        [InspectorName("지상")] Ground=1,
        [InspectorName("공중")] Air=2,
        [InspectorName("우주")] Space=4
    }
    [CreateAssetMenu(menuName="화성의 용병단/기체",fileName="새 기체")]
    public sealed class MechDefinition : ScriptableObject
    {
        [NonSerialized] internal MechDefinition sourceDefinition;
        public MechDefinition SourceDefinition=>sourceDefinition!=null?sourceDefinition:this;
        public string displayName="새 기체";
        public Sprite mapIcon;
        [Range(1000,9999000)] public int maxHp=1000;
        [Range(10,990)] public int maxEnergy=10;
        [Range(10,9990)] public int armor=10;
        [Range(10,9990)] public int targeting=10;
        [Range(10,9990)] public int mobility=10;
        public MovementEnvironment movementEnvironments=MovementEnvironment.Ground;
        [Range(1,99)] public int movement=4;
        [Range(0,9999000)] public int price;
        [Range(0,990)] public int maxWeaponWeight=100;
        [Min(0)] public int hpUpgrade;
        [Min(0)] public int energyUpgrade;
        [Min(0)] public int armorUpgrade;
        [Min(0)] public int targetingUpgrade;
        [Min(0)] public int mobilityUpgrade;
        [Min(0)] public int weaponUpgrade;
        public int UpgradeStage(MechUpgradeType type)=>type switch
        {
            MechUpgradeType.Hp=>hpUpgrade,MechUpgradeType.Energy=>energyUpgrade,
            MechUpgradeType.Armor=>armorUpgrade,MechUpgradeType.Targeting=>targetingUpgrade,
            MechUpgradeType.Mobility=>mobilityUpgrade,MechUpgradeType.Weapon=>weaponUpgrade,
            _=>throw new ArgumentOutOfRangeException(nameof(type),"올바른 개조 항목을 선택하세요.")
        };
        // 기본값을 변경하지 않고 개조 보정값을 계산한다. 정수 나눗셈으로 소수점 이하는 버린다.
        public static int ApplyUpgrade(int baseValue,int stage)
            =>(int)Math.Min(int.MaxValue,Mathf.Max(0,baseValue)+(long)Mathf.Max(0,baseValue)*Mathf.Max(0,stage)/20);
        public int EffectiveMaxHp=>MaxHpAtLimit(int.MaxValue);
        public int MaxHpAtLimit(int limit)=>ApplyUpgrade(Mathf.Clamp(maxHp,1000,9999000),Mathf.Clamp(hpUpgrade,0,Mathf.Max(0,limit)));
        public int EffectiveMaxEnergy=>MaxEnergyAtLimit(int.MaxValue);
        public int MaxEnergyAtLimit(int limit)=>ApplyUpgrade(Mathf.Clamp(maxEnergy,10,990),Mathf.Clamp(energyUpgrade,0,Mathf.Max(0,limit)));
        public int EffectiveArmor=>ArmorAtLimit(int.MaxValue);
        public int ArmorAtLimit(int limit)=>ApplyUpgrade(Mathf.Clamp(armor,10,9990),Mathf.Clamp(armorUpgrade,0,Mathf.Max(0,limit)));
        public int EffectiveTargeting=>TargetingAtLimit(int.MaxValue);
        public int TargetingAtLimit(int limit)=>ApplyUpgrade(Mathf.Clamp(targeting,10,9990),Mathf.Clamp(targetingUpgrade,0,Mathf.Max(0,limit)));
        public int EffectiveMobility=>MobilityAtLimit(int.MaxValue);
        public int MobilityAtLimit(int limit)=>ApplyUpgrade(Mathf.Clamp(mobility,10,9990),Mathf.Clamp(mobilityUpgrade,0,Mathf.Max(0,limit)));
        public int WeaponPower(int basePower)=>ApplyUpgrade(basePower,weaponUpgrade);
        public List<SkillDefinition> weapons=new();
        public long TotalWeaponWeight
        {
            get { long total=0; if(weapons!=null) foreach(var weapon in weapons) if(weapon!=null) total+=Mathf.Max(0,weapon.weight); return total; }
        }
        public bool TryAddWeapon(SkillDefinition weapon)
        {
            if(weapon==null || weapons!=null && weapons.Contains(weapon) || weapon.ValidateSkill().Count>0 || maxWeaponWeight<0 || maxWeaponWeight>990 || TotalWeaponWeight+weapon.weight>maxWeaponWeight) return false;
            weapons??=new List<SkillDefinition>(); weapons.Add(weapon); return true;
        }
        public List<string> ValidateEquipment()
        {
            var errors=new List<string>();
            if(maxWeaponWeight<0 || maxWeaponWeight>990) errors.Add("허용중량은 0~990이어야 합니다.");
            if(TotalWeaponWeight>maxWeaponWeight) errors.Add($"무장 총중량 {TotalWeaponWeight}이 기체 허용중량 {maxWeaponWeight}을 초과합니다. 무장을 제거하거나 허용중량을 늘리세요.");
            var assigned=new HashSet<SkillDefinition>();
            if(weapons!=null) foreach(var weapon in weapons) if(weapon!=null)
            {
                if(!assigned.Add(weapon)) errors.Add(weapon.displayName+": 같은 무장은 한 기체에 한 번만 장착할 수 있습니다.");
                foreach(var error in weapon.ValidateSkill()) errors.Add(weapon.displayName+": "+error);
            }
            return errors;
        }
        [TextArea] public string description;
        void OnValidate()
        {
            maxWeaponWeight=Mathf.Clamp(maxWeaponWeight,0,990);
            maxHp=Mathf.Clamp(maxHp,1000,9999000); maxEnergy=Mathf.Clamp(maxEnergy,10,990); armor=Mathf.Clamp(armor,10,9990);
            targeting=Mathf.Clamp(targeting,10,9990); mobility=Mathf.Clamp(mobility,10,9990); movement=Mathf.Clamp(movement,1,99); price=Mathf.Clamp(price,0,9999000);
            hpUpgrade=Mathf.Max(0,hpUpgrade); energyUpgrade=Mathf.Max(0,energyUpgrade);
            armorUpgrade=Mathf.Max(0,armorUpgrade); targetingUpgrade=Mathf.Max(0,targetingUpgrade);
            mobilityUpgrade=Mathf.Max(0,mobilityUpgrade); weaponUpgrade=Mathf.Max(0,weaponUpgrade);
            weapons??=new List<SkillDefinition>();
        }
    }
}
