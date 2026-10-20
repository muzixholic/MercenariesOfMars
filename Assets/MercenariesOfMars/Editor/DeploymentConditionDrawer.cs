using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    [CustomPropertyDrawer(typeof(DeploymentCondition))]
    public sealed class DeploymentConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property,GUIContent label)=>3*(EditorGUIUtility.singleLineHeight+3);
        public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
            var row=new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
            var kind=property.FindPropertyRelative("kind");EditorGUI.PropertyField(row,kind,new GUIContent("조건 종류"));
            string field=((DeploymentConditionKind)kind.enumValueIndex) switch
            {DeploymentConditionKind.Flag=>"flag",DeploymentConditionKind.RecruitedAlly=>"ally",DeploymentConditionKind.OwnedMech=>"mech",_=>"pilot"};
            string title=field switch {"flag"=>"플래그 이름","ally"=>"영입 여부 확인 아군","mech"=>"보유 여부 확인 기체",_=>"영입 여부 확인 파일럿"};
            row.y+=row.height+3;EditorGUI.PropertyField(row,property.FindPropertyRelative(field),new GUIContent(title));
            row.y+=row.height+3;EditorGUI.PropertyField(row,property.FindPropertyRelative("expected"),new GUIContent("기대값（켜짐: 영입·보유·참）"));
            EditorGUI.EndProperty();
        }
    }
}
