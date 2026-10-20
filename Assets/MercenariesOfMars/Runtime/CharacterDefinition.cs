using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum Faction { [InspectorName("아군")] Ally, [InspectorName("적군")] Enemy }
    [Serializable]
    public sealed class CharacterPortrait
    {
        [HideInInspector] public string id = Guid.NewGuid().ToString("N");
        [InspectorName("표정 이름")] public string label = "기본";
        [InspectorName("포트레이트 이미지")] public Sprite sprite;
        public static void EnsureIds(IEnumerable<CharacterPortrait> portraits)
        {
            var ids=new HashSet<string>();
            foreach(var portrait in portraits)
                if(portrait!=null && (string.IsNullOrEmpty(portrait.id)||!ids.Add(portrait.id)))
                { portrait.id=Guid.NewGuid().ToString("N"); ids.Add(portrait.id); }
        }
    }

    [CreateAssetMenu(menuName = "화성의 용병단/유닛 편성", fileName = "새 유닛 편성")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [InspectorName("이름")] public string displayName = "새 캐릭터";
        [InspectorName("기본 진영")] public Faction faction;
        public MechDefinition mech;
        public PilotDefinition pilot;
        [Min(1), InspectorName("레벨")] public int level = 1;
        [InspectorName("맵 아이콘")] public Sprite mapIcon;
        [InspectorName("대화 포트레이트 세트")] public List<CharacterPortrait> portraits = new();
        [Min(1), InspectorName("최대 체력")] public int maxHp = 30;
        [Min(0), InspectorName("공격력")] public int attack = 10;
        [Range(0, 100), InspectorName("명중 (%)")] public int accuracy = 100;
        [Range(0, 100), InspectorName("회피 (%)")] public int evasion;
        [Min(0), InspectorName("방어력")] public int defense = 3;
        [Min(0), InspectorName("이동력")] public int movement = 4;
        [HideInInspector] public int range = 1;
        [Min(0), InspectorName("최대 에너지")] public int maxEnergy = 10;
        [InspectorName("보유 스킬")] public List<SkillDefinition> skills = new();
        [TextArea, InspectorName("설명")] public string description;
        // 기존 데이터의 참조와 저장값을 보존하기 위해 이전 필드를 유지한다.
        public string UnitName=>mech!=null?mech.displayName:displayName;
        public string PilotName=>pilot!=null?pilot.displayName:displayName;
        public int UnitMaxHp=>mech!=null?mech.EffectiveMaxHp:maxHp;
        public int UnitMovement=>mech!=null?Mathf.Clamp(mech.movement,1,99):movement;
        public int UnitMaxEnergy=>mech!=null?mech.EffectiveMaxEnergy:maxEnergy;
        public IReadOnlyList<SkillDefinition> Weapons=>mech!=null?mech.weapons:skills;
        public IReadOnlyList<CharacterPortrait> DialoguePortraits=>pilot!=null?pilot.portraits:portraits;
        public Sprite FindPortrait(string id) => pilot!=null?pilot.FindPortrait(id):string.IsNullOrEmpty(id) ? null : portraits?.FirstOrDefault(p => p != null && p.id == id)?.sprite;
        public List<string> ValidateAssignment()
        {
            var errors=new List<string>();
            if(mech==null && pilot==null) return errors;
            if(mech==null) errors.Add("전투 유닛의 기체를 지정하세요.");
            if(mech!=null) errors.AddRange(mech.ValidateEquipment());
            if(pilot==null) errors.Add("전투 유닛의 파일럿을 지정하세요.");
            else errors.AddRange(pilot.ValidateGrowth());
            return errors;
        }

        void OnValidate()
        {
            skills ??= new List<SkillDefinition>();
            portraits ??= new List<CharacterPortrait>();
            CharacterPortrait.EnsureIds(portraits);
            level = Mathf.Max(1, level);
            maxHp = Mathf.Max(1, maxHp);
            maxEnergy = Mathf.Max(0, maxEnergy);
            attack = Mathf.Max(0, attack);
            accuracy = Mathf.Clamp(accuracy, 0, 100);
            evasion = Mathf.Clamp(evasion, 0, 100);
            defense = Mathf.Max(0, defense);
            movement = Mathf.Max(0, movement);
            range = Mathf.Max(1, range);
        }
    }
}
