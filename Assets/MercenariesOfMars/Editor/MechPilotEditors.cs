using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public static class MechPilotLists
    {
        public static ReorderableList Portraits(SerializedObject so)
        {
            var list=new ReorderableList(so,so.FindProperty("portraits"),true,true,true,true); list.elementHeight=48;
            list.drawHeaderCallback=r=>EditorGUI.LabelField(r,"포트레이트 리스트 · + 추가 / − 삭제 / 드래그 정렬");
            list.drawElementCallback=(r,i,a,f)=>
            {
                var item=list.serializedProperty.GetArrayElementAtIndex(i); r.y+=2; r.height=20;
                EditorGUI.PropertyField(r,item.FindPropertyRelative("label"),new GUIContent("표정 이름")); r.y+=23;
                EditorGUI.PropertyField(r,item.FindPropertyRelative("sprite"),new GUIContent("이미지"));
            };
            list.onAddCallback=_=>
            {
                var item=list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize++);
                item.FindPropertyRelative("id").stringValue=System.Guid.NewGuid().ToString("N");
                item.FindPropertyRelative("label").stringValue="새 표정"; item.FindPropertyRelative("sprite").objectReferenceValue=null;
            };
            return list;
        }
        public static ReorderableList Weapons(SerializedObject so,System.Action<string> report=null)
        {
            var list=new ReorderableList(so,so.FindProperty("weapons"),true,true,true,true); list.elementHeight=48;
            list.drawHeaderCallback=r=>EditorGUI.LabelField(r,"장비 리스트（무장 스킬） · + 추가 / − 삭제");
            list.drawElementCallback=(r,i,a,f)=>
            {
                var item=list.serializedProperty.GetArrayElementAtIndex(i); r.y+=2; r.height=20;
                var before=item.objectReferenceValue;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(new Rect(r.x,r.y,r.width-48,20),item,GUIContent.none);
                if(EditorGUI.EndChangeCheck())
                {
                    if(!CanAssignWeapon(so,i,item.objectReferenceValue as SkillDefinition))
                    { item.objectReferenceValue=before; report?.Invoke("같은 무장의 중복 장착, 허용중량 초과 또는 잘못된 무장 설정으로 추가할 수 없습니다."); }
                    else report?.Invoke(null);
                }
                var skill=item.objectReferenceValue as SkillDefinition;
                using(new EditorGUI.DisabledScope(skill==null)) if(GUI.Button(new Rect(r.xMax-44,r.y,44,20),"편집")) SkillEditorWindow.Edit(skill);
                int stage=so.FindProperty("weaponUpgrade")?.intValue??0;
                r.y+=23; EditorGUI.LabelField(r,skill!=null?$"공격 {skill.power} → {MechDefinition.ApplyUpgrade(skill.power,stage)} · 거리 {skill.minRange}~{skill.maxRange} · EN {skill.energyCost} · 무게 {skill.weight}":"무장 스킬을 지정하세요.");
            };
            list.onAddCallback=_=> { int i=list.serializedProperty.arraySize++; list.serializedProperty.GetArrayElementAtIndex(i).objectReferenceValue=null; list.index=i; };
            list.onRemoveCallback=_=>
            {
                if(list.index<0 || list.index>=list.serializedProperty.arraySize) return;
                list.serializedProperty.GetArrayElementAtIndex(list.index).objectReferenceValue=null;
                list.serializedProperty.DeleteArrayElementAtIndex(list.index); list.index=Mathf.Min(list.index,list.serializedProperty.arraySize-1);
            };
            return list;
        }
        public static bool CanAssignWeapon(SerializedObject so,int index,SkillDefinition candidate)
        {
            if(candidate==null) return true;
            if(candidate.ValidateSkill().Count>0) return false;
            long total=0; var weapons=so.FindProperty("weapons");
            for(int i=0;i<weapons.arraySize;i++)
            {
                if(i!=index && weapons.GetArrayElementAtIndex(i).objectReferenceValue==candidate) return false;
                var skill=i==index?candidate:weapons.GetArrayElementAtIndex(i).objectReferenceValue as SkillDefinition;
                if(skill!=null) total+=Mathf.Max(0,skill.weight);
            }
            int capacity=so.FindProperty("maxWeaponWeight").intValue;
            return capacity>=0 && capacity<=990 && total<=capacity;
        }
        public static void Fields(SerializedObject so,string[] fields,string[] labels)
        { for(int i=0;i<fields.Length;i++) EditorGUILayout.PropertyField(so.FindProperty(fields[i]),new GUIContent(labels[i]),true); }
    }
    [CustomEditor(typeof(MechDefinition))]
    public sealed class MechDefinitionEditor : UnityEditor.Editor
    {
        ReorderableList weapons;
        string equipmentError;
        void OnEnable()=>weapons=MechPilotLists.Weapons(serializedObject,message=>equipmentError=message);
        void BaseStat(string field,string label,int min,int max)
        {
            var property=serializedObject.FindProperty(field);
            EditorGUI.BeginChangeCheck();
            int value=EditorGUILayout.IntField($"{label}（{min:N0}~{max:N0}）",property.intValue);
            if(EditorGUI.EndChangeCheck())property.intValue=Mathf.Clamp(value,min,max);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MechPilotLists.Fields(serializedObject,new[]{"displayName","mapIcon"},new[]{"기체 이름","맵 아이콘"});
            EditorGUILayout.LabelField("기본 능력치",EditorStyles.boldLabel);
            BaseStat("maxHp","HP",1000,9999000);BaseStat("maxEnergy","EN",10,990);
            BaseStat("armor","장갑치",10,9990);BaseStat("targeting","조준치",10,9990);BaseStat("mobility","운동성",10,9990);
            BaseStat("movement","이동력",1,99);BaseStat("price","가격",0,9999000);BaseStat("maxWeaponWeight","허용중량",0,990);
            EditorGUILayout.Space();EditorGUILayout.LabelField("개조 단계（기본값 대비 단계당 +5%）",EditorStyles.boldLabel);
            MechPilotLists.Fields(serializedObject,new[]{"hpUpgrade","energyUpgrade","armorUpgrade","targetingUpgrade","mobilityUpgrade","weaponUpgrade"},
                new[]{"HP 개조","EN 개조","장갑치 개조","조준치 개조","운동성 개조","무장 개조"});
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"),new GUIContent("설명"));
            var environments=serializedObject.FindProperty("movementEnvironments");
            environments.intValue=(int)(MovementEnvironment)EditorGUILayout.EnumFlagsField("이동 가능 지형",(MovementEnvironment)environments.intValue);
            weapons.DoLayoutList(); serializedObject.ApplyModifiedProperties();
            var mech=(MechDefinition)target;
            EditorGUILayout.LabelField("개조 적용값（캠페인 상한 적용 전）",EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"HP {mech.EffectiveMaxHp:N0} · EN {mech.EffectiveMaxEnergy:N0}");
            EditorGUILayout.LabelField($"장갑치 {mech.EffectiveArmor:N0} · 조준치 {mech.EffectiveTargeting:N0} · 운동성 {mech.EffectiveMobility:N0}");
            EditorGUILayout.Space();EditorGUILayout.LabelField("다음 1단계 개조 비용",EditorStyles.boldLabel);
            var upgradeTypes=new[]{MechUpgradeType.Hp,MechUpgradeType.Energy,MechUpgradeType.Armor,MechUpgradeType.Targeting,MechUpgradeType.Mobility,MechUpgradeType.Weapon};
            var upgradeNames=new[]{"HP","EN","장갑치","조준치","운동성","무장"};
            for(int i=0;i<upgradeTypes.Length;i++)
            {
                int stage=Mathf.Max(0,mech.UpgradeStage(upgradeTypes[i]));
                if(stage==int.MaxValue)EditorGUILayout.LabelField(upgradeNames[i],"다음 단계 없음");
                else EditorGUILayout.LabelField($"{upgradeNames[i]} {stage} → {stage+1}",$"{MechUpgradeCost.NextStage(upgradeTypes[i],stage):N0}");
            }
            EditorGUILayout.HelpBox("무장 비용은 목표 단계 × 5,000, 나머지 5개 항목은 각각 목표 단계 × 3,000입니다. 이 표는 비용 미리보기이며 에디터에서 단계를 직접 수정하면 자금을 차감하지 않습니다.",MessageType.Info);
            EditorGUILayout.HelpBox("개조는 기본값 × (100 + 단계 × 5) ÷ 100으로 계산하며 소수점 이하는 버립니다. 기체에는 10단계 초과 값도 설정할 수 있으며 실제 전투는 캠페인의 최대 개조 단계까지만 적용합니다（단독 맵 테스트 기본 10）. 상한을 늘려도 기체의 개조 단계 자체는 증가하지 않습니다. 무장 공격력은 장착 기체의 무장 개조 단계를 사용합니다. 이동력·가격·허용중량·EN 소모·잔탄·무게는 개조하지 않습니다.",MessageType.Info);
            EditorGUILayout.LabelField("무장 총중량 / 허용중량",$"{mech.TotalWeaponWeight} / {mech.maxWeaponWeight}");
            if(!string.IsNullOrEmpty(equipmentError)) EditorGUILayout.HelpBox(equipmentError,MessageType.Warning);
            foreach(var error in mech.ValidateEquipment()) EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.HelpBox("장갑치·조준치·운동성의 전투 합산 계산식과 이동 유형 판정은 기존 규칙을 유지합니다. 인터미션에서 개조·구매 비용을 적용하며, 수리·매각 비용은 아직 적용하지 않습니다.",MessageType.Info);
            if(GUILayout.Button("무장 스킬 에디터 열기")) SkillEditorWindow.Open();
        }
    }
    [CustomEditor(typeof(PilotDefinition))]
    public sealed class PilotDefinitionEditor : UnityEditor.Editor
    {
        ReorderableList portraits;
        int previewLevel=1;
        void OnEnable() { portraits=MechPilotLists.Portraits(serializedObject); previewLevel=Mathf.Clamp(((PilotDefinition)target).level,1,99); }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MechPilotLists.Fields(serializedObject,new[]{"displayName","price","level","experience","attackGrowth","defenseGrowth","accuracyGrowth","evasionGrowth","attack","defense","accuracy","evasion","attackAt99","defenseAt99","accuracyAt99","evasionAt99","description"},
                new[]{"파일럿 이름","영입 가격","레벨（1~99）","누적 경험치","공격력 성장 커브","방어력 성장 커브","명중력 성장 커브","회피력 성장 커브","1레벨 공격력","1레벨 방어력","1레벨 명중력","1레벨 회피력","99레벨 공격력","99레벨 방어력","99레벨 명중력","99레벨 회피력","설명"});
            portraits.DoLayoutList(); serializedObject.ApplyModifiedProperties();
            var pilot=(PilotDefinition)target;
            foreach(var error in pilot.ValidateGrowth()) EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.HelpBox("능력치마다 성장 커브 프리셋을 선택하세요. 미지정한 능력치는 1레벨 기본값을 유지합니다.",MessageType.Info);
            previewLevel=EditorGUILayout.IntSlider("능력치 미리보기 레벨",previewLevel,1,99);
            var stats=pilot.StatsAtLevel(previewLevel);
            EditorGUILayout.LabelField($"공격 {stats.Attack} · 방어 {stats.Defense} · 명중 {stats.Accuracy} · 회피 {stats.Evasion}");
            if(GUILayout.Button("성장 프리셋 설정 열기")) PilotProgressionWindow.OpenGrowth();
        }
    }
    public abstract class DefinitionEditorWindow<T> : EditorWindow where T:ScriptableObject
    {
        T data; UnityEditor.Editor inspector; Vector2 scroll;
        protected abstract string DataLabel {get;}
        public void SetTarget(T target) { data=target; Focus(); }
        void OnDisable() { if(inspector!=null) DestroyImmediate(inspector); }
        protected virtual void OnGUI()
        {
            data=(T)EditorGUILayout.ObjectField(DataLabel,data,typeof(T),false);
            if(GUILayout.Button("새 "+DataLabel+" 만들기"))
            {
                string path=EditorUtility.SaveFilePanelInProject(DataLabel+" 저장","새 "+DataLabel,"asset","저장 위치를 선택하세요.");
                if(!string.IsNullOrEmpty(path))
                {
                    data=CreateInstance<T>();
                    if(data is PilotDefinition pilot) PilotProgressionSetup.InitializePilot(pilot);
                    AssetDatabase.CreateAsset(data,path); Selection.activeObject=data;
                }
            }
            if(data==null) return;
            UnityEditor.Editor.CreateCachedEditor(data,null,ref inspector); scroll=EditorGUILayout.BeginScrollView(scroll);
            inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
            if(GUILayout.Button(DataLabel+" 저장")) AssetDatabase.SaveAssetIfDirty(data);
        }
    }
    public sealed class MechEditorWindow : DefinitionEditorWindow<MechDefinition>
    {
        protected override string DataLabel=>"기체";
        [MenuItem("화성의 용병단/기체 에디터")] public static void Open()=>GetWindow<MechEditorWindow>("기체 에디터");
        public static void Edit(MechDefinition target)=>GetWindow<MechEditorWindow>("기체 에디터").SetTarget(target);
    }
    public sealed class PilotEditorWindow : DefinitionEditorWindow<PilotDefinition>
    {
        protected override string DataLabel=>"파일럿";
        [MenuItem("화성의 용병단/파일럿 에디터")] public static void Open()=>GetWindow<PilotEditorWindow>("파일럿 에디터");
        public static void Edit(PilotDefinition target)=>GetWindow<PilotEditorWindow>("파일럿 에디터").SetTarget(target);
    }
}
