using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public sealed class EventRunner : MonoBehaviour
    {
        [Min(1), InspectorName("최대 실행 명령 수")] public int instructionLimit = 1000;
        readonly Dictionary<string, bool> flags = new();
        public bool IsRunning { get; private set; }
        public string Speaker { get; private set; }
        public Sprite Portrait { get; private set; }
        public string Dialogue { get; private set; }
        public string LastError { get; private set; }
        public event Action<string, string> DialogueShown;
        bool awaitingDialogue;
        public bool GetFlag(string key) => !string.IsNullOrEmpty(key) && flags.TryGetValue(key, out bool value) && value;
        public Dictionary<string,bool> CaptureFlags()=>new(flags);
        public void RestoreFlags(IReadOnlyDictionary<string,bool> values)
        { flags.Clear(); if(values!=null) foreach(var pair in values) flags[pair.Key]=pair.Value; }
        public void ContinueDialogue() => awaitingDialogue = false;
        public bool Play(EventSequence sequence, BattleSession battle)
        {
            if (IsRunning || !isActiveAndEnabled) return false;
            LastError = null;
            if (sequence == null || battle == null) { LastError = "이벤트와 전투가 필요합니다."; return false; }
            var errors = sequence.ValidateCommands();
            if (errors.Count > 0) { LastError = string.Join("\n", errors); return false; }
            IsRunning = true; StartCoroutine(Execute(sequence, battle)); return true;
        }
        IEnumerator Execute(EventSequence sequence, BattleSession battle)
        {
            int steps = 0;
            try
            {
                for (int i = 0; i < sequence.commands.Count; i++)
                {
                    if (battle.Outcome == BattleOutcome.Defeat) yield break;
                    if (++steps > Mathf.Max(1, instructionLimit)) { LastError = "실행 한도를 초과했습니다. 순환 분기를 확인하세요."; yield break; }
                    var c = sequence.commands[i];
                    bool succeeded = true;
                    switch (c.kind)
                    {
                        case EventCommandKind.Dialogue:
                            Speaker = c.ResolvedSpeaker; Portrait = c.ResolvedPortrait; Dialogue = c.text; awaitingDialogue = true;
                            DialogueShown?.Invoke(Speaker, Dialogue);
                            while (awaitingDialogue) yield return null;
                            Dialogue = null; Speaker = null; Portrait = null; break;
                        case EventCommandKind.Wait: yield return new WaitForSecondsRealtime(c.seconds); break;
                        case EventCommandKind.ChangeHp: succeeded = battle.ChangeHp(c.unitId, c.amount); break;
                        case EventCommandKind.SetFlag: flags[c.flag] = c.flagValue; break;
                        case EventCommandKind.BranchFlag:
                            if (GetFlag(c.flag) == c.flagValue) i = c.jumpTo - 1;
                            break;
                        case EventCommandKind.BranchHp:
                            var unit = battle.Find(c.unitId);
                            succeeded = unit != null;
                            if (unit != null && unit.Hp <= c.amount) i = c.jumpTo - 1;
                            break;
                        case EventCommandKind.Move: succeeded = battle.TryMove(battle.Find(c.unitId), c.cell); break;
                        case EventCommandKind.Attack:
                            var attacker = battle.Find(c.unitId); var target = battle.Find(c.targetId);
                            succeeded = c.skill == null ? battle.TryAttack(attacker, target)
                                : battle.TryAttack(attacker, target, attacker?.Skills.FirstOrDefault(s => s.Definition == c.skill));
                            var request = battle.PendingAttack;
                            if (succeeded && request != null)
                            {
                                while (battle.PendingAttack == request || battle.IsPresenting) yield return null;
                                if (request.Cancelled) { LastError = $"명령 {i}: 전투가 취소되어 이벤트를 중단했습니다."; yield break; }
                            }
                            break;
                        case EventCommandKind.EndTurn: battle.EndTurn(); break;
                        case EventCommandKind.Stop: yield break;
                    }
                    if (!succeeded) { LastError = $"명령 {i}: 실행할 수 없습니다. 유닛 ID, 턴, 거리와 생존 상태를 확인하세요."; yield break; }
                    // 순환 분기도 매 프레임 제어권을 반환한다.
                    yield return null;
                }
            }
            finally { IsRunning = false; awaitingDialogue = false; Speaker = null; Portrait = null; Dialogue = null; }
        }
        public void StopPlayback()
        { StopAllCoroutines(); IsRunning = false; awaitingDialogue = false; Speaker = null; Portrait = null; Dialogue = null; }
        public void ResetState()
        { StopPlayback(); flags.Clear(); LastError = null; }
        void OnDisable() => StopPlayback();
    }
}
