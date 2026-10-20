using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    static class MapPropertyLabels
    {
        static readonly Dictionary<string, string> Labels = new()
        {
            ["displayName"] = "이름", ["width"] = "가로 칸 수", ["height"] = "세로 칸 수", ["cellSize"] = "칸 크기",
            ["defaultTerrain"] = "기본 지형", ["terrainCells"] = "칠한 지형", ["structures"] = "구조물 배치", ["units"] = "유닛 배치",
            ["openingEvent"] = "시작 이벤트", ["eventRegions"] = "진입 이벤트 구역", ["victoryCondition"] = "승리 조건", ["goalCell"] = "목표 칸",
            ["color"] = "표시 색상", ["walkable"] = "이동 가능", ["movementCost"] = "진입 이동 비용",
            ["accuracyModifier"] = "공격 명중 보정（%p）", ["defenseModifier"] = "피격 방어력 보정", ["material"] = "표면 재질", ["description"] = "설명",
            ["size"] = "차지하는 칸 수", ["blocksMovement"] = "이동 차단", ["prefab"] = "모델 프리팹",
            ["cell"] = "기준 칸", ["terrain"] = "지형", ["structure"] = "구조물", ["label"] = "구역 이름", ["area"] = "영역",
            ["sequence"] = "진입 이벤트", ["id"] = "유닛 ID", ["character"] = "전투 유닛 편성",
            ["autoEnemyLevel"]="적군 자동 레벨", ["enemyLevelPreset"]="적군 레벨 보정",
            ["conditions"]="배치 조건（비어 있으면 항상 배치）",["conditionMatch"]="배치 조건 결합",
            ["deploymentSlots"]="아군 출격 칸", ["testRecruitedAllies"]="단독 테스트용 영입 아군"
        };
        public static GUIContent Label(string name) => new(Labels.TryGetValue(name, out var text) ? text : name);
        public static void Field(SerializedObject so, string name) => EditorGUILayout.PropertyField(so.FindProperty(name), Label(name), true);
    }
    [CustomEditor(typeof(TerrainDefinition))]
    public sealed class TerrainDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "displayName", "color", "walkable", "movementCost", "accuracyModifier", "defenseModifier", "material", "description" }) MapPropertyLabels.Field(serializedObject, name);
            serializedObject.ApplyModifiedProperties();
        }
    }
    [CustomEditor(typeof(StructureDefinition))]
    public sealed class StructureDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "displayName", "size", "blocksMovement", "color" }) MapPropertyLabels.Field(serializedObject, name);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("height"),new GUIContent("임시 모델 높이"));
            MapPropertyLabels.Field(serializedObject,"prefab"); serializedObject.ApplyModifiedProperties();
        }
    }
    public abstract class MapEntryDrawer : PropertyDrawer
    {
        protected abstract string[] Fields { get; }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded) foreach (var field in Fields) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field),true)+2;
            return height;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
            var row = new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row,property.isExpanded,new GUIContent("배치 항목"),true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var field in Fields)
                {
                    var p = property.FindPropertyRelative(field); row.y += row.height+2; row.height = EditorGUI.GetPropertyHeight(p,true);
                    EditorGUI.PropertyField(row,p,MapPropertyLabels.Label(field),true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
    [CustomPropertyDrawer(typeof(TerrainCell))] public sealed class TerrainCellDrawer : MapEntryDrawer { protected override string[] Fields => new[] { "cell", "terrain" }; }
    [CustomPropertyDrawer(typeof(StructurePlacement))] public sealed class StructurePlacementDrawer : MapEntryDrawer { protected override string[] Fields => new[] { "cell", "structure" }; }
    [CustomPropertyDrawer(typeof(MapEventRegion))] public sealed class MapEventRegionDrawer : MapEntryDrawer { protected override string[] Fields => new[] { "label", "area", "sequence" }; }
    [CustomPropertyDrawer(typeof(UnitPlacement))] public sealed class UnitPlacementDrawer : MapEntryDrawer { protected override string[] Fields => new[] { "id", "character", "cell", "autoEnemyLevel", "enemyLevelPreset", "conditionMatch", "conditions" }; }
    [CustomPropertyDrawer(typeof(AllyDeploymentSlot))] public sealed class AllyDeploymentSlotDrawer : MapEntryDrawer { protected override string[] Fields=>new[]{"id","cell"}; }
}
