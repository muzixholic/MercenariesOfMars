using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomEditor(typeof(BattleMapDefinition))]
    public sealed class BattleMapDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("맵 에디터에서 열기")) MapEditorWindow.Edit((BattleMapDefinition)target);
            serializedObject.Update();
            foreach (string name in new[] { "displayName", "width", "height", "cellSize", "defaultTerrain", "terrainCells", "structures", "units", "deploymentSlots", "testRecruitedAllies", "openingEvent", "eventRegions", "victoryCondition", "goalCell" })
                MapPropertyLabels.Field(serializedObject, name);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
