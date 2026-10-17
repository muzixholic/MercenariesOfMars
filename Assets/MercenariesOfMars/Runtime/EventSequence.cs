using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public enum EventCommandKind
    {
        [InspectorName("대사")] Dialogue, [InspectorName("대기")] Wait,
        [InspectorName("체력 변경")] ChangeHp, [InspectorName("플래그 설정")] SetFlag,
        [InspectorName("플래그 조건 분기")] BranchFlag, [InspectorName("체력 조건 분기")] BranchHp,
        [InspectorName("이동")] Move, [InspectorName("공격")] Attack,
        [InspectorName("턴 종료")] EndTurn, [InspectorName("이벤트 종료")] Stop
    }
    [Serializable]
    public sealed class EventCommand
    {
        [InspectorName("명령")] public EventCommandKind kind;
        [InspectorName("대상 유닛 ID")] public string unitId;
        [InspectorName("공격 대상 ID")] public string targetId;
        [InspectorName("사용 스킬 (미지정 시 첫 사용 가능 스킬)")] public SkillDefinition skill;
        [InspectorName("화자")] public string speaker;
        [InspectorName("화자 캐릭터（선택）")] public CharacterDefinition speakerCharacter;
        public PilotDefinition speakerPilot;
        [HideInInspector] public string portraitId;
        public string ResolvedSpeaker => speakerPilot!=null?speakerPilot.displayName:speakerCharacter != null ? speakerCharacter.PilotName : speaker;
        public IReadOnlyList<CharacterPortrait> SpeakerPortraits=>speakerPilot!=null?speakerPilot.portraits:speakerCharacter!=null?speakerCharacter.DialoguePortraits:null;
        public Sprite ResolvedPortrait => speakerPilot!=null?speakerPilot.FindPortrait(portraitId):speakerCharacter != null ? speakerCharacter.FindPortrait(portraitId) : null;
        [TextArea, InspectorName("대사 내용")] public string text;
        [Min(0), InspectorName("대기 시간(초)")] public float seconds = 1;
        [InspectorName("체력 변화 / 체력 이하 조건")] public int amount;
        [InspectorName("목적지")] public Vector2Int cell;
        [InspectorName("플래그 이름")] public string flag;
        [InspectorName("플래그 값")] public bool flagValue = true;
        [InspectorName("조건이 참일 때 이동할 명령 번호")] public int jumpTo;
    }
    [CreateAssetMenu(menuName = "화성의 용병단/조립형 이벤트", fileName = "새 이벤트")]
    public sealed class EventSequence : ScriptableObject
    {
        [InspectorName("명령 목록")] public List<EventCommand> commands = new();
        public List<string> ValidateCommands()
        {
            var errors = new List<string>();
            if (commands == null) { errors.Add("명령 목록이 없습니다."); return errors; }
            for (int i = 0; i < commands.Count; i++)
            {
                var c = commands[i];
                if (c == null) { errors.Add($"명령 {i}: 비어 있습니다."); continue; }
                if (!Enum.IsDefined(typeof(EventCommandKind), c.kind)) errors.Add($"명령 {i}: 알 수 없는 명령입니다.");
                if (c.kind == EventCommandKind.Dialogue && (c.speakerPilot!=null || c.speakerCharacter != null) && !string.IsNullOrEmpty(c.portraitId)
                    && !(c.SpeakerPortraits?.Any(p => p != null && p.id == c.portraitId) ?? false))
                    errors.Add($"명령 {i}: 화자의 포트레이트가 삭제되었습니다. 다시 선택하거나 없음으로 설정하세요.");
                if ((c.kind == EventCommandKind.BranchFlag || c.kind == EventCommandKind.BranchHp) && (c.jumpTo < 0 || c.jumpTo >= commands.Count))
                    errors.Add($"명령 {i}: 분기 번호가 목록 범위를 벗어납니다.");
                if ((c.kind == EventCommandKind.ChangeHp || c.kind == EventCommandKind.BranchHp || c.kind == EventCommandKind.Move || c.kind == EventCommandKind.Attack)
                    && string.IsNullOrWhiteSpace(c.unitId)) errors.Add($"명령 {i}: 유닛 ID가 필요합니다.");
                if (c.kind == EventCommandKind.Attack && string.IsNullOrWhiteSpace(c.targetId)) errors.Add($"명령 {i}: 공격 대상 ID가 필요합니다.");
                if ((c.kind == EventCommandKind.SetFlag || c.kind == EventCommandKind.BranchFlag) && string.IsNullOrWhiteSpace(c.flag))
                    errors.Add($"명령 {i}: 플래그 이름이 필요합니다.");
                if (c.kind == EventCommandKind.Wait && (float.IsNaN(c.seconds) || float.IsInfinity(c.seconds) || c.seconds < 0))
                    errors.Add($"명령 {i}: 대기 시간은 유효한 0 이상의 수여야 합니다.");
            }
            return errors;
        }
    }
}
