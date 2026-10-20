using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomPropertyDrawer(typeof(EventCommand))]
    public sealed class EventCommandDrawer : PropertyDrawer
    {
        static IEnumerable<string> Fields(SerializedProperty property)
        {
            yield return "kind";
            switch ((EventCommandKind)property.FindPropertyRelative("kind").enumValueIndex)
            {
                case EventCommandKind.Dialogue:
                    yield return "speakerPilot";
                    if(property.FindPropertyRelative("speakerPilot").objectReferenceValue==null && property.FindPropertyRelative("speakerCharacter").objectReferenceValue!=null) yield return "speakerCharacter";
                    yield return "speaker";
                    if (property.FindPropertyRelative("speakerPilot").objectReferenceValue!=null || property.FindPropertyRelative("speakerCharacter").objectReferenceValue != null) yield return "portraitId";
                    yield return "text"; break;
                case EventCommandKind.Wait: yield return "seconds"; break;
                case EventCommandKind.IncreaseMaxUpgradeStage: yield return "upgradeStageIncrease";break;
                case EventCommandKind.RecruitAlly:case EventCommandKind.RemoveAlly:yield return "rosterAlly";break;
                case EventCommandKind.SetDeploymentBlocked:yield return "rosterAlly";yield return "deploymentBlocked";break;
                case EventCommandKind.ChangeHp: yield return "unitId"; yield return "amount"; break;
                case EventCommandKind.SetFlag: yield return "flag"; yield return "flagValue"; break;
                case EventCommandKind.BranchFlag: yield return "flag"; yield return "flagValue"; yield return "jumpTo"; break;
                case EventCommandKind.BranchHp: yield return "unitId"; yield return "amount"; yield return "jumpTo"; break;
                case EventCommandKind.Move: yield return "unitId"; yield return "cell"; break;
                case EventCommandKind.Attack: yield return "unitId"; yield return "targetId"; yield return "skill"; break;
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0;
            foreach (string field in Fields(property)) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 3;
            return height;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            foreach (string field in Fields(property))
            {
                var child = property.FindPropertyRelative(field);
                position.height = EditorGUI.GetPropertyHeight(child, true);
                var character = property.FindPropertyRelative("speakerCharacter").objectReferenceValue as CharacterDefinition;
                var pilot=property.FindPropertyRelative("speakerPilot").objectReferenceValue as PilotDefinition;
                if(field=="speakerPilot")
                {
                    EditorGUI.BeginChangeCheck(); EditorGUI.PropertyField(position,child,new GUIContent("화자 파일럿（선택）"));
                    if(EditorGUI.EndChangeCheck())
                    {
                        pilot=child.objectReferenceValue as PilotDefinition;
                        property.FindPropertyRelative("speakerCharacter").objectReferenceValue=null;
                        property.FindPropertyRelative("portraitId").stringValue=pilot?.portraits?.FirstOrDefault(p=>p!=null)?.id??"";
                    }
                }
                else if (field == "speakerCharacter")
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(position, child, new GUIContent("기존 화자 연결（호환）"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        character = child.objectReferenceValue as CharacterDefinition;
                        property.FindPropertyRelative("portraitId").stringValue = character?.DialoguePortraits?.FirstOrDefault(p => p != null)?.id ?? "";
                    }
                }
                else if (field == "speaker" && (pilot!=null || character != null))
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUI.TextField(position,"화자 이름（자동）",pilot!=null?pilot.displayName:character.PilotName);
                }
                else if (field == "portraitId") DrawPortrait(position, child, pilot!=null?pilot.portraits:character?.DialoguePortraits);
                else EditorGUI.PropertyField(position, child, new GUIContent(Label(field)), true);
                position.y += position.height + 3;
            }
            EditorGUI.EndProperty();
        }
        static void DrawPortrait(Rect rect, SerializedProperty property, IReadOnlyList<CharacterPortrait> portraits)
        {
            var entries = portraits?.Where(p => p != null).ToList() ?? new();
            var labels = new List<string> { "없음" }; labels.AddRange(entries.Select((p,i) => $"{i+1}. {p.label}{(p.sprite == null ? "（이미지 없음）" : "")}"));
            int index = string.IsNullOrEmpty(property.stringValue) ? 0 : entries.FindIndex(p => p.id == property.stringValue)+1;
            if (index == 0 && !string.IsNullOrEmpty(property.stringValue)) { labels.Add("삭제된 포트레이트（다시 선택）"); index = labels.Count-1; }
            EditorGUI.BeginChangeCheck(); int next = EditorGUI.Popup(rect,"포트레이트",index,labels.ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = next == 0 ? "" : next <= entries.Count ? entries[next-1].id : property.stringValue;
        }
        static string Label(string field) => field switch
        {
            "kind" => "명령", "rosterAlly"=>"대상 아군 편성", "deploymentBlocked"=>"출격 선택 제한（끄면 허용）", "speaker" => "화자 이름（직접 입력）", "text" => "대사 내용", "unitId" => "대상 유닛 ID",
            "targetId" => "공격 대상 ID", "skill" => "사용 스킬", "seconds" => "대기 시간（초）", "amount" => "체력 변화 / 체력 이하 조건",
            "cell" => "목적지", "flag" => "플래그 이름", "flagValue" => "플래그 값", "jumpTo" => "분기 명령 번호", "upgradeStageIncrease"=>"최대 개조 단계 증가량", _ => field
        };
    }
}
