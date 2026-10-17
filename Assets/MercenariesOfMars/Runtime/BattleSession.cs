using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum BattleOutcome { Ongoing, Victory, Defeat }
    public enum ReactionChoice
    {
        [InspectorName("반격")] Counterattack,
        [InspectorName("방어")] Defend,
        [InspectorName("회피")] Evade
    }
    public sealed class IncomingAttack
    {
        public BattleUnit Attacker { get; }
        public BattleUnit Target { get; }
        public BattleSkill Skill { get; }
        public ReactionDecision Decision { get; internal set; }
        public bool Cancelled { get; internal set; }
        public bool Resolved { get; internal set; }
        internal IncomingAttack(BattleUnit attacker, BattleUnit target, BattleSkill skill)
        { Attacker = attacker; Target = target; Skill = skill; }
    }

    public sealed class BattleUnit
    {
        public string Id { get; }
        public string Name { get; }
        public int Level { get; }
        public int Experience { get; }
        readonly float defeatExperienceMultiplier;
        readonly int defeatExperienceOffset;
        // 경험치 지급 시스템이 전달하는 기본 격추 경험치에 배치 프리셋을 적용한다.
        public int CalculateDefeatExperience(int baseExperience)=>EnemyLevelPreset.CalculateExperience(baseExperience,defeatExperienceMultiplier,defeatExperienceOffset);
        public Sprite MapIcon { get; }
        public Sprite Portrait { get; }
        public string PilotName { get; }
        public int Armor { get; }
        public int Targeting { get; }
        public int Mobility { get; }
        public int Price { get; }
        public long WeaponWeight { get; }
        public int MaxWeaponWeight { get; }
        public MovementEnvironment MovementEnvironments { get; }
        public Faction Faction { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int Defense { get; }
        public int Movement { get; }
        public int Hp { get; internal set; }
        public int MaxEnergy { get; }
        public int Energy { get; internal set; }
        public IReadOnlyList<BattleSkill> Skills { get; }
        public Vector2Int Cell { get; internal set; }
        public bool HasMoved { get; internal set; }
        public bool HasActed { get; internal set; }
        public bool IsAlive => Hp > 0;

        public BattleUnit(string id, CharacterDefinition definition, Vector2Int cell,int? pilotLevel=null,EnemyLevelPreset enemyPreset=null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("유닛 ID가 필요합니다.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var assignmentErrors=definition.ValidateAssignment();
            if(assignmentErrors.Count>0) throw new ArgumentException(string.Join("\n",assignmentErrors));
            var mech=definition.mech; var pilot=definition.pilot;
            Id = id; Name = definition.UnitName; PilotName=definition.PilotName; Faction = definition.faction; Cell = cell;
            Level = Mathf.Clamp(pilotLevel??(pilot!=null?pilot.level:definition.level),1,99); MapIcon = mech!=null?mech.mapIcon:definition.mapIcon;
            Portrait=definition.DialoguePortraits?.FirstOrDefault(p=>p!=null && p.sprite!=null)?.sprite;
            var stats=pilot!=null?pilot.StatsAtLevel(Level):default;
            defeatExperienceMultiplier=Faction==Faction.Enemy && enemyPreset!=null?enemyPreset.experienceMultiplier:1;
            defeatExperienceOffset=Faction==Faction.Enemy && enemyPreset!=null?enemyPreset.experienceOffset:0;
            Experience=pilot!=null?Mathf.Max(0,pilot.experience):0;
            MaxHp = Mathf.Max(1,mech!=null?mech.maxHp:definition.maxHp); Hp = MaxHp;
            Attack = pilot!=null?stats.Attack:Mathf.Max(0,definition.attack); Accuracy = pilot!=null?stats.Accuracy:Mathf.Clamp(definition.accuracy,0,100);
            Evasion = pilot!=null?stats.Evasion:Mathf.Clamp(definition.evasion,0,100); Defense = pilot!=null?stats.Defense:Mathf.Max(0,definition.defense); Movement = Mathf.Max(0,definition.UnitMovement);
            WeaponWeight=mech!=null?mech.TotalWeaponWeight:0; MaxWeaponWeight=mech!=null?Mathf.Max(0,mech.maxWeaponWeight):0;
            Armor=mech!=null?Mathf.Max(0,mech.armor):0; Targeting=mech!=null?Mathf.Max(0,mech.targeting):0;
            Mobility=mech!=null?Mathf.Max(0,mech.mobility):0; Price=mech!=null?Mathf.Max(0,mech.price):0;
            MovementEnvironments=mech!=null?mech.movementEnvironments:MovementEnvironment.Ground;
            MaxEnergy = Mathf.Max(0,definition.UnitMaxEnergy); Energy = MaxEnergy;
            Skills = (definition.Weapons ?? new List<SkillDefinition>()).Where(s => s != null).Distinct()
                .Select(s => new BattleSkill(s)).ToList().AsReadOnly();
        }
    }

    public sealed class BattleAttackResult
    {
        public BattleUnit Attacker { get; }
        public BattleUnit Target { get; }
        public BattleSkill Skill { get; }
        public float HitChance { get; }
        public bool IsCounterattack { get; }
        public bool Hit { get; }
        public int Damage { get; }
        internal BattleAttackResult(BattleUnit attacker, BattleUnit target, BattleSkill skill, float hitChance, bool hit, int damage, bool isCounterattack = false)
        { Attacker = attacker; Target = target; Skill = skill; HitChance = hitChance; Hit = hit; Damage = damage; IsCounterattack = isCounterattack; }
    }

    public sealed class BattleSession
    {
        readonly List<BattleUnit> units;
        readonly Func<float> rollPercent;
        public IReadOnlyList<BattleUnit> Units { get; }
        public BattleGrid Grid { get; }
        readonly Vector2Int? victoryCell;
        public Faction ActiveFaction { get; private set; } = Faction.Ally;
        public int Round { get; private set; } = 1;
        public BattleOutcome Outcome { get; private set; }
        public BattleAttackResult LastAttack { get; private set; }
        public BattleAttackResult LastCounterattack { get; private set; }
        public ReactionChoice? LastReaction { get; private set; }
        public IncomingAttack PendingAttack { get; private set; }
        public bool RequireConfirmation { get; }
        public bool IsPresenting { get; private set; }
        public bool IsAwaitingConfirmation => PendingAttack?.Decision != null;
        public event Action Changed;

        public BattleSession(BattleGrid grid, IEnumerable<BattleUnit> initialUnits, Func<int> rollPercent = null, bool requireConfirmation = false, Vector2Int? victoryCell = null)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            RequireConfirmation = requireConfirmation;
            this.victoryCell = victoryCell;
            this.rollPercent = rollPercent != null ? () => rollPercent() : () => UnityEngine.Random.Range(0, 10000) / 100f;
            units = initialUnits?.ToList() ?? throw new ArgumentNullException(nameof(initialUnits));
            if (units.Any(u => u == null) || units.Select(u => u.Id).Distinct().Count() != units.Count)
                throw new ArgumentException("유닛과 ID는 유효하고 중복되지 않아야 합니다.");
            if (units.Any(u => !Grid.IsWalkable(u.Cell)) || units.Select(u => u.Cell).Distinct().Count() != units.Count)
                throw new ArgumentException("유닛 배치가 격자 밖이거나 장애물 또는 다른 유닛과 겹칩니다.");
            Units = units.AsReadOnly(); EvaluateOutcome();
        }

        public BattleUnit Find(string id) => units.Find(u => u.Id == id);
        public BattleUnit At(Vector2Int cell) => units.Find(u => u.IsAlive && u.Cell == cell);
        bool CanAct(BattleUnit unit) => !IsPresenting && PendingAttack == null && Outcome == BattleOutcome.Ongoing && unit != null && units.Contains(unit)
            && unit.IsAlive && unit.Faction == ActiveFaction && !unit.HasActed;
        public Dictionary<Vector2Int, List<Vector2Int>> MovementOptions(BattleUnit unit)
            => !CanAct(unit) || unit.HasMoved ? new() : Grid.Reachable(unit.Cell, unit.Movement, c => At(c) != null);

        public bool TryMove(BattleUnit unit, Vector2Int destination)
        {
            if (unit == null || destination == unit.Cell || !MovementOptions(unit).ContainsKey(destination)) return false;
            unit.Cell = destination; unit.HasMoved = true; EvaluateOutcome(); Changed?.Invoke(); return true;
        }
        public bool CanSelectSkill(BattleUnit unit, BattleSkill skill)
            => CanAct(unit) && skill != null && unit.Skills.Contains(skill) && unit.Energy >= skill.EnergyCost && skill.HasAmmo
                && (!unit.HasMoved || skill.CanUseAfterMoving);
        public bool CanUseSkill(BattleUnit attacker, BattleUnit target, BattleSkill skill)
        {
            return CanSelectSkill(attacker, skill)
                && target != null && units.Contains(target) && target.IsAlive && target.Faction != attacker.Faction
                && skill.IsInRange(attacker.Cell, target.Cell);
        }
        // 명중률과 피해 예상은 행동 소모 없이 조회할 수 있다.
        bool IsAttackPair(BattleUnit attacker, BattleUnit target, BattleSkill skill)
            => attacker != null && target != null && skill != null && units.Contains(attacker) && units.Contains(target)
                && attacker.Skills.Contains(skill) && attacker.Faction != target.Faction && attacker.IsAlive && target.IsAlive;
        public int GetHitChance(BattleUnit attacker, BattleUnit target, BattleSkill skill)
            => !IsAttackPair(attacker, target, skill) ? 0 : (int)Math.Max(0L,Math.Min(100L,(long)attacker.Accuracy + skill.AccuracyModifier + Grid.AccuracyModifier(attacker.Cell) - target.Evasion));
        public int GetDamage(BattleUnit attacker, BattleUnit target, BattleSkill skill)
            => !IsAttackPair(attacker, target, skill) ? 0 : (int)Math.Max(1L, Math.Min(int.MaxValue, (long)attacker.Attack + skill.Power - target.Defense - Grid.DefenseModifier(target.Cell)));
        // 이전 이벤트의 스킬 미지정 공격은 사용 가능한 첫 보유 스킬로 처리한다.
        public bool TryAttack(BattleUnit attacker, BattleUnit target)
            => TryAttack(attacker, target, attacker?.Skills.FirstOrDefault(s => CanUseSkill(attacker, target, s)));
        public bool TryAttack(BattleUnit attacker, BattleUnit target, BattleSkill skill)
        {
            if (!CanUseSkill(attacker, target, skill)) return false;
            if (RequireConfirmation || (attacker.Faction == Faction.Enemy && target.Faction == Faction.Ally))
            {
                PendingAttack = new IncomingAttack(attacker, target, skill);
                if (target.Faction == Faction.Enemy) PendingAttack.Decision = EnemyReactionPolicy.Choose(this);
                Changed?.Invoke(); return true;
            }
            var result = PlanAttack(attacker, target, skill, GetHitChance(attacker, target, skill), GetDamage(attacker, target, skill));
            ApplyAttack(result); attacker.HasActed = true;
            LastAttack = result; LastCounterattack = null; LastReaction = null;
            AfterAction(); return true;
        }
        BattleAttackResult PlanAttack(BattleUnit attacker, BattleUnit target, BattleSkill skill, float chance, int damage, bool counter = false)
        {
            float roll = 0;
            if (chance > 0 && chance < 100)
            {
                roll = rollPercent();
                if (float.IsNaN(roll) || roll < 0 || roll >= 100) throw new InvalidOperationException("명중 판정 난수는 0 이상 100 미만이어야 합니다.");
            }
            bool hit = chance == 100 || (chance > 0 && roll < chance);
            return new BattleAttackResult(attacker, target, skill, chance, hit, hit ? Mathf.Min(target.Hp, damage) : 0, counter);
        }
        static void ApplyAttack(BattleAttackResult result)
        {
            result.Target.Hp -= result.Damage;
            result.Attacker.Energy -= result.Skill.EnergyCost;
            result.Skill.ConsumeAmmo();
        }
        public bool CanCounterattack(BattleSkill skill)
        {
            var incoming = PendingAttack;
            // 반격은 상대 진영 턴의 대응이다. 이전 자기 턴의 행동·이동 기록은 제한하지 않는다.
            return incoming != null && IsAttackPair(incoming.Target, incoming.Attacker, skill)
                && incoming.Target.Energy >= skill.EnergyCost && skill.HasAmmo && skill.IsInRange(incoming.Target.Cell, incoming.Attacker.Cell);
        }
        public float GetReactionHitChance(ReactionChoice choice)
        {
            var incoming = PendingAttack;
            if (incoming == null) return 0;
            int chance = GetHitChance(incoming.Attacker, incoming.Target, incoming.Skill);
            return choice == ReactionChoice.Evade ? chance * .5f : chance;
        }
        public int GetReactionDamage(ReactionChoice choice)
        {
            var incoming = PendingAttack;
            if (incoming == null) return 0;
            int damage = GetDamage(incoming.Attacker, incoming.Target, incoming.Skill);
            return choice == ReactionChoice.Defend ? (int)(((long)damage + 1) / 2) : damage;
        }
        public bool ResolveReaction(ReactionChoice choice, BattleSkill counterSkill = null)
        {
            var incoming = PendingAttack;
            if (incoming == null || !Enum.IsDefined(typeof(ReactionChoice), choice)) return false;
            if (choice == ReactionChoice.Counterattack && !CanCounterattack(counterSkill)) return false;
            if (RequireConfirmation)
            {
                if (incoming.Target.Faction != Faction.Ally) return false;
                incoming.Decision = new ReactionDecision(choice, counterSkill, "플레이어가 선택한 대응입니다.");
                Changed?.Invoke(); return true;
            }
            return ResolvePending(choice, counterSkill);
        }
        public bool ConfirmCombat(bool deferCompletion = false)
        {
            var decision = PendingAttack?.Decision;
            if (!RequireConfirmation || decision == null) return false;
            if (decision.Choice == ReactionChoice.Counterattack && !CanCounterattack(decision.CounterSkill)) return false;
            return ResolvePending(decision.Choice, decision.CounterSkill, deferCompletion);
        }
        public bool CancelCombat()
        {
            var incoming = PendingAttack;
            if (!RequireConfirmation || incoming?.Decision == null) return false;
            if (incoming.Attacker.Faction == Faction.Ally)
            { incoming.Cancelled = true; PendingAttack = null; }
            else incoming.Decision = null;
            Changed?.Invoke(); return true;
        }
        bool ResolvePending(ReactionChoice choice, BattleSkill counterSkill, bool deferCompletion = false)
        {
            var incoming = PendingAttack;
            if(incoming==null || !incoming.Skill.HasAmmo || incoming.Attacker.Energy<incoming.Skill.EnergyCost) return false;
            if(choice==ReactionChoice.Counterattack && !CanCounterattack(counterSkill)) return false;
            var primary = PlanAttack(incoming.Attacker, incoming.Target, incoming.Skill,
                GetReactionHitChance(choice), GetReactionDamage(choice));
            BattleAttackResult counter = null;
            // 두 공격의 판정을 먼저 계산해 잘못된 난수 등으로 인한 부분 적용을 방지한다.
            if (choice == ReactionChoice.Counterattack && incoming.Target.Hp > primary.Damage)
                counter = PlanAttack(incoming.Target, incoming.Attacker, counterSkill,
                    GetHitChance(incoming.Target, incoming.Attacker, counterSkill), GetDamage(incoming.Target, incoming.Attacker, counterSkill), true);
            PendingAttack = null; incoming.Resolved = true;
            ApplyAttack(primary); incoming.Attacker.HasActed = true;
            if (counter != null) ApplyAttack(counter);
            LastAttack = primary; LastCounterattack = counter; LastReaction = choice;
            IsPresenting = deferCompletion;
            if (deferCompletion) Changed?.Invoke(); else AfterAction();
            return true;
        }
        public void CompletePresentation()
        {
            if (!IsPresenting) return;
            IsPresenting = false; AfterAction();
        }
        public bool TryWait(BattleUnit unit)
        {
            if (!CanAct(unit)) return false;
            unit.HasActed = true; AfterAction(); return true;
        }
        public void EndTurn()
        {
            if (IsPresenting || PendingAttack != null || Outcome != BattleOutcome.Ongoing) return;
            foreach (var unit in units.Where(u => u.Faction == ActiveFaction)) unit.HasActed = true;
            AdvanceTurn(); Changed?.Invoke();
        }
        public bool ChangeHp(string id, int amount)
        {
            var unit = Find(id);
            // 초기 버전에서는 전투 불능 유닛을 부활시키지 않는다.
            if (IsPresenting || PendingAttack != null || Outcome != BattleOutcome.Ongoing || unit == null || !unit.IsAlive) return false;
            unit.Hp = (int)Math.Max(0L, Math.Min(unit.MaxHp, (long)unit.Hp + amount));
            AfterAction(); return true;
        }
        void AfterAction()
        {
            EvaluateOutcome();
            if (Outcome == BattleOutcome.Ongoing && !units.Any(u => u.IsAlive && u.Faction == ActiveFaction && !u.HasActed)) AdvanceTurn();
            Changed?.Invoke();
        }
        void AdvanceTurn()
        {
            ActiveFaction = ActiveFaction == Faction.Ally ? Faction.Enemy : Faction.Ally;
            if (ActiveFaction == Faction.Ally) Round++;
            foreach (var unit in units.Where(u => u.IsAlive && u.Faction == ActiveFaction))
            { unit.HasMoved = false; unit.HasActed = false; }
        }
        void EvaluateOutcome()
        {
            Outcome = !units.Any(u => u.IsAlive && u.Faction == Faction.Ally) ? BattleOutcome.Defeat
                : (victoryCell.HasValue ? units.Any(u => u.IsAlive && u.Faction == Faction.Ally && u.Cell == victoryCell.Value)
                    : !units.Any(u => u.IsAlive && u.Faction == Faction.Enemy)) ? BattleOutcome.Victory : BattleOutcome.Ongoing;
        }
    }
}
