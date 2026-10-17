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
        public string displayName="새 기체";
        public Sprite mapIcon;
        [Min(1)] public int maxHp=30;
        [Min(0)] public int maxEnergy=10;
        [Min(0)] public int armor;
        [Min(0)] public int targeting;
        [Min(0)] public int mobility;
        public MovementEnvironment movementEnvironments=MovementEnvironment.Ground;
        [Min(0)] public int movement=4;
        [Min(0)] public int price;
        [Min(0)] public int maxWeaponWeight=100;
        public List<SkillDefinition> weapons=new();
        public long TotalWeaponWeight
        {
            get { long total=0; if(weapons!=null) foreach(var weapon in weapons) if(weapon!=null) total+=Mathf.Max(0,weapon.weight); return total; }
        }
        public bool TryAddWeapon(SkillDefinition weapon)
        {
            if(weapon==null || weapons!=null && weapons.Contains(weapon) || weapon.ValidateSkill().Count>0 || maxWeaponWeight<0 || TotalWeaponWeight+weapon.weight>maxWeaponWeight) return false;
            weapons??=new List<SkillDefinition>(); weapons.Add(weapon); return true;
        }
        public List<string> ValidateEquipment()
        {
            var errors=new List<string>();
            if(maxWeaponWeight<0) errors.Add("허용중량은 0 이상이어야 합니다.");
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
            maxWeaponWeight=Mathf.Max(0,maxWeaponWeight);
            maxHp=Mathf.Max(1,maxHp); maxEnergy=Mathf.Max(0,maxEnergy); armor=Mathf.Max(0,armor);
            targeting=Mathf.Max(0,targeting); mobility=Mathf.Max(0,mobility); movement=Mathf.Max(0,movement); price=Mathf.Max(0,price);
            weapons??=new List<SkillDefinition>();
        }
    }
}
