using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(CharacterDefinition))]
    public sealed class CharacterDefinitionEditor : UnityEditor.Editor
    {
        ReorderableList skills;
        ReorderableList portraits;
        UnityEditor.Editor mechInspector,pilotInspector;
        bool showMech=true,showPilot=true,showLegacy;
        void OnDisable()
        { if(mechInspector!=null) DestroyImmediate(mechInspector); if(pilotInspector!=null) DestroyImmediate(pilotInspector); }
        void OnEnable()
        {
            portraits = new ReorderableList(serializedObject,serializedObject.FindProperty("portraits"),true,true,true,true);
            portraits.drawHeaderCallback = rect => EditorGUI.LabelField(rect,"대화 포트레이트 세트 · + 추가 / − 삭제 / 드래그로 정렬");
            portraits.elementHeight = 48;
            portraits.drawElementCallback = (rect,index,active,focused) =>
            {
                var item = portraits.serializedProperty.GetArrayElementAtIndex(index); rect.y += 2; rect.height = 20;
                EditorGUI.PropertyField(rect,item.FindPropertyRelative("label"),new GUIContent("표정 이름"));
                rect.y += 23; EditorGUI.PropertyField(rect,item.FindPropertyRelative("sprite"),new GUIContent("포트레이트 이미지"));
            };
            portraits.onAddCallback = _ =>
            {
                int index = portraits.serializedProperty.arraySize++; var item = portraits.serializedProperty.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
                item.FindPropertyRelative("label").stringValue = "새 표정"; item.FindPropertyRelative("sprite").objectReferenceValue = null; portraits.index = index;
            };
            skills = new ReorderableList(serializedObject, serializedObject.FindProperty("skills"), true, true, true, true);
            skills.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "보유 스킬 · + 추가 / − 삭제 / 드래그로 정렬");
            skills.elementHeight = 68;
            skills.drawElementCallback = (rect, index, active, focused) =>
            {
                var property = skills.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2; rect.height = 20;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - 48, 20), property, GUIContent.none);
                var skill = property.objectReferenceValue as SkillDefinition;
                using (new EditorGUI.DisabledScope(skill == null))
                    if (GUI.Button(new Rect(rect.xMax - 44, rect.y, 44, 20), "편집")) SkillEditorWindow.Edit(skill);
                rect.y += 23;
                EditorGUI.LabelField(rect, skill == null ? "스킬 에셋을 지정하세요." : $"공격 {skill.power} · 거리 {skill.minRange}~{skill.maxRange} · 에너지 {skill.energyCost}");
                if (skill != null)
                { rect.y += 20; EditorGUI.LabelField(rect, $"명중 보정 {skill.accuracyModifier:+0;-0;0}%p · {(skill.canUseAfterMoving ? "이동 후 가능" : "이동 후 불가")}"); }
            };
            skills.onAddCallback = _ =>
            {
                int index = skills.serializedProperty.arraySize++;
                skills.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue = null; skills.index = index;
            };
            skills.onRemoveCallback = _ =>
            {
                int index = skills.index;
                if (index < 0 || index >= skills.serializedProperty.arraySize) return;
                skills.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue = null;
                skills.serializedProperty.DeleteArrayElementAtIndex(index);
                skills.index = Mathf.Min(index, skills.serializedProperty.arraySize - 1);
            };
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("faction"),new GUIContent("기본 진영"));
            var mech=serializedObject.FindProperty("mech");var pilot=serializedObject.FindProperty("pilot");
            var previousMech=mech.objectReferenceValue;var previousPilot=pilot.objectReferenceValue;
            mech.objectReferenceValue=UnitFormationCreation.MechSelector(mech.objectReferenceValue as MechDefinition);
            pilot.objectReferenceValue=UnitFormationCreation.PilotSelector(pilot.objectReferenceValue as PilotDefinition);
            if((previousMech!=mech.objectReferenceValue || previousPilot!=pilot.objectReferenceValue) && mech.objectReferenceValue!=null && pilot.objectReferenceValue!=null)
                serializedObject.FindProperty("displayName").stringValue=UnitFormationCreation.SuggestedName(mech.objectReferenceValue as MechDefinition,pilot.objectReferenceValue as PilotDefinition);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"),new GUIContent("편성 이름"));
            using(new EditorGUI.DisabledScope(mech.objectReferenceValue==null || pilot.objectReferenceValue==null))
                if(GUILayout.Button("기체·파일럿 이름으로 맞추기"))
                    serializedObject.FindProperty("displayName").stringValue=UnitFormationCreation.SuggestedName(mech.objectReferenceValue as MechDefinition,pilot.objectReferenceValue as PilotDefinition);
            serializedObject.ApplyModifiedProperties();
            var character = (CharacterDefinition)target;
            if(character.mech!=null || character.pilot!=null)
            {
                foreach(var error in character.ValidateAssignment()) EditorGUILayout.HelpBox(error,MessageType.Error);
                if(character.mech!=null)
                {
                    showMech=EditorGUILayout.Foldout(showMech,"기체 능력치",true);
                    if(showMech) { UnityEditor.Editor.CreateCachedEditor(character.mech,null,ref mechInspector); mechInspector.OnInspectorGUI(); }
                }
                if(character.pilot!=null)
                {
                    showPilot=EditorGUILayout.Foldout(showPilot,"파일럿 능력치",true);
                    if(showPilot) { UnityEditor.Editor.CreateCachedEditor(character.pilot,null,ref pilotInspector); pilotInspector.OnInspectorGUI(); }
                }
                return;
            }
            EditorGUILayout.HelpBox("기체와 파일럿을 지정하세요. 기존 캐릭터는 아래 버튼으로 저장값을 나누고 기존 맵·씬 연결을 유지할 수 있습니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(!AssetDatabase.Contains(character)))
                if(GUILayout.Button("기존 데이터를 기체·파일럿으로 분리")) { CharacterSplitMigration.Split(character); GUIUtility.ExitGUI(); }
            showLegacy=EditorGUILayout.Foldout(showLegacy,"이전 캐릭터 데이터（호환용）",true);
            if(!showLegacy) return;
            serializedObject.Update();
            var fields = new[] { "displayName", "faction", "level", "mapIcon", "maxHp", "attack", "accuracy", "evasion", "defense", "movement", "maxEnergy", "description" };
            var labels = new[] { "이름", "기본 진영", "레벨", "맵 아이콘", "최대 체력", "공격력", "명중（%）", "회피（%）", "방어력", "이동력", "최대 에너지", "설명" };
            for (int i = 0; i < fields.Length; i++) EditorGUILayout.PropertyField(serializedObject.FindProperty(fields[i]),new GUIContent(labels[i]));
            EditorGUILayout.Space(); portraits.DoLayoutList();
            EditorGUILayout.Space(); skills.DoLayoutList(); serializedObject.ApplyModifiedProperties();
            var seen = new HashSet<SkillDefinition>();
            foreach (var skill in character.skills)
            {
                if (skill == null) { EditorGUILayout.HelpBox("비어 있는 스킬 항목을 지정하거나 삭제하세요.", MessageType.Warning); continue; }
                if (!seen.Add(skill)) EditorGUILayout.HelpBox($"중복 스킬: {skill.displayName}. 전투에는 한 번만 등록됩니다.", MessageType.Warning);
                if (skill.energyCost > character.maxEnergy) EditorGUILayout.HelpBox($"{skill.displayName}: 소비 에너지가 이 캐릭터의 최대 에너지보다 큽니다.", MessageType.Warning);
            }
            if (seen.Count == 0) EditorGUILayout.HelpBox("보유 스킬이 없으면 공격할 수 없습니다.", MessageType.Info);
            if (GUILayout.Button("스킬 에디터 열기")) SkillEditorWindow.Open();
        }
    }
}
