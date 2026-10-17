using System;
using System.Collections.Generic;

namespace MercenariesOfMars
{
    public sealed class ReactionDecision
    {
        public ReactionChoice Choice { get; }
        public BattleSkill CounterSkill { get; }
        public string Reason { get; }
        internal ReactionDecision(ReactionChoice choice, BattleSkill skill, string reason)
        { Choice = choice; CounterSkill = skill; Reason = reason; }
    }

    public static class EnemyReactionPolicy
    {
        sealed class Candidate
        {
            public ReactionChoice Choice;
            public BattleSkill Skill;
            public double DeathRisk, KillChance, Taken, Dealt, Energy;
            public double Utility => Dealt - Taken - Energy * .1;
        }
        public static ReactionDecision Choose(BattleSession battle)
        {
            var incoming = battle.PendingAttack;
            if (incoming == null || incoming.Target.Faction != Faction.Enemy)
                throw new InvalidOperationException("적군이 공격받는 대기 상태가 필요합니다.");
            var candidates = new List<Candidate>();
            foreach (var choice in new[] { ReactionChoice.Defend, ReactionChoice.Evade })
            {
                double chance = battle.GetReactionHitChance(choice) / 100.0;
                int damage = battle.GetReactionDamage(choice);
                candidates.Add(new Candidate { Choice = choice,
                    DeathRisk = damage >= incoming.Target.Hp ? chance : 0,
                    Taken = Math.Min(damage, incoming.Target.Hp) * chance });
            }
            foreach (var skill in incoming.Target.Skills)
            {
                if (!battle.CanCounterattack(skill)) continue;
                double chance = battle.GetReactionHitChance(ReactionChoice.Counterattack) / 100.0;
                int taken = battle.GetReactionDamage(ReactionChoice.Counterattack);
                double deathRisk = taken >= incoming.Target.Hp ? chance : 0;
                double counterChance = battle.GetHitChance(incoming.Target, incoming.Attacker, skill) / 100.0;
                int dealt = battle.GetDamage(incoming.Target, incoming.Attacker, skill);
                candidates.Add(new Candidate { Choice = ReactionChoice.Counterattack, Skill = skill, DeathRisk = deathRisk,
                    Taken = Math.Min(taken, incoming.Target.Hp) * chance,
                    Dealt = Math.Min(dealt, incoming.Attacker.Hp) * counterChance * (1 - deathRisk),
                    KillChance = dealt >= incoming.Attacker.Hp ? counterChance * (1 - deathRisk) : 0,
                    Energy = skill.EnergyCost * (1 - deathRisk) });
            }
            var best = candidates[0];
            foreach (var candidate in candidates)
            {
                if (candidate.DeathRisk < best.DeathRisk
                    || (candidate.DeathRisk == best.DeathRisk && candidate.KillChance > best.KillChance)
                    || (candidate.DeathRisk == best.DeathRisk && candidate.KillChance == best.KillChance && candidate.Utility > best.Utility)) best = candidate;
            }
            string name = best.Choice == ReactionChoice.Counterattack ? "반격" : best.Choice == ReactionChoice.Defend ? "방어" : "회피";
            string reason = $"{name} 선택: 전투 불능 위험 {best.DeathRisk * 100:0.##}%, 예상 피격 피해 {best.Taken:0.##}, 예상 반격 피해 {best.Dealt:0.##}. 생존을 우선하고 반격 기회와 에너지를 비교했습니다.";
            return new ReactionDecision(best.Choice, best.Skill, reason);
        }
    }
}
