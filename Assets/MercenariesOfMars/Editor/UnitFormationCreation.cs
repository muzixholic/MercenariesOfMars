using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public static class UnitFormationCreation
    {
        static string Part(ScriptableObject asset,string displayName,string fallback)
            =>asset==null?fallback:!string.IsNullOrWhiteSpace(displayName)?displayName.Trim():!string.IsNullOrWhiteSpace(asset.name)?asset.name:fallback;
        public static string SuggestedName(MechDefinition mech,PilotDefinition pilot)
            =>$"{Part(mech,mech!=null?mech.displayName:null,"기체 미선택")} ({Part(pilot,pilot!=null?pilot.displayName:null,"파일럿 미선택")})";
        public static string SuggestedFileName(MechDefinition mech,PilotDefinition pilot)
        {
            const string invalid="/\\:*?\"<>|";
            return new string(SuggestedName(mech,pilot).Select(c=>char.IsControl(c)||invalid.IndexOf(c)>=0?'_':c).ToArray()).Trim().TrimEnd('.');
        }
        public static List<string> SelectionErrors(MechDefinition mech,PilotDefinition pilot)
        {
            var errors=new List<string>();
            if(mech==null)errors.Add("기체를 선택하거나 신규 기체를 만드세요.");
            else {if(!EditorUtility.IsPersistent(mech))errors.Add("저장된 기체 에셋을 선택하세요.");errors.AddRange(mech.ValidateEquipment());}
            if(pilot==null)errors.Add("파일럿을 선택하거나 신규 파일럿을 만드세요.");
            else {if(!EditorUtility.IsPersistent(pilot))errors.Add("저장된 파일럿 에셋을 선택하세요.");errors.AddRange(pilot.ValidateGrowth());}
            return errors;
        }
        static string ValidateNewPath(string path)
        {
            path=path?.Replace('\\','/');
            if(string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/",StringComparison.Ordinal)
                || path.Split('/').Any(part=>part==".." || part==".") || !path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(path)) || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\','/')))
                throw new ArgumentException("Assets 안의 기존 폴더와 .asset 저장 경로를 선택하세요.");
            if(File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path)!=null)
                throw new InvalidOperationException("이미 파일이 있습니다. 새 파일 이름을 선택하세요.");
            return path;
        }
        static T SaveNew<T>(T asset,string path) where T:ScriptableObject
        {
            try
            {
                AssetDatabase.CreateAsset(asset,path);
                if(!EditorUtility.IsPersistent(asset))throw new InvalidOperationException("에셋을 저장하지 못했습니다. 저장 경로를 확인하세요.");
                AssetDatabase.SaveAssetIfDirty(asset);return asset;
            }
            catch {if(!EditorUtility.IsPersistent(asset))UnityEngine.Object.DestroyImmediate(asset);throw;}
        }
        public static CharacterDefinition Create(string path,MechDefinition mech,PilotDefinition pilot,Faction faction)
        {
            var errors=SelectionErrors(mech,pilot);
            if(!Enum.IsDefined(typeof(Faction),faction))errors.Add("올바른 진영을 선택하세요.");
            if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            path=ValidateNewPath(path);
            var unit=ScriptableObject.CreateInstance<CharacterDefinition>();
            unit.displayName=SuggestedName(mech,pilot);unit.mech=mech;unit.pilot=pilot;unit.faction=faction;
            return SaveNew(unit,path);
        }
        public static MechDefinition CreateMech(string path)
        {
            path=ValidateNewPath(path);var mech=ScriptableObject.CreateInstance<MechDefinition>();
            mech.displayName=Path.GetFileNameWithoutExtension(path);return SaveNew(mech,path);
        }
        public static PilotDefinition CreatePilot(string path)
        {
            path=ValidateNewPath(path);var pilot=ScriptableObject.CreateInstance<PilotDefinition>();
            try {pilot.displayName=Path.GetFileNameWithoutExtension(path);PilotProgressionSetup.InitializePilot(pilot);}
            catch {UnityEngine.Object.DestroyImmediate(pilot);throw;}
            return SaveNew(pilot,path);
        }
        static T Selector<T>(string label,T current,Func<string,T> create,Action<T> edit) where T:ScriptableObject
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                current=(T)EditorGUILayout.ObjectField(label,current,typeof(T),false);
                if(GUILayout.Button("신규",GUILayout.Width(46)))
                {
                    string path=EditorUtility.SaveFilePanelInProject("신규 "+label+" 저장","새 "+label,"asset","새 에셋을 만들고 현재 편성에 선택합니다.");
                    if(!string.IsNullOrEmpty(path))
                    {
                        try {current=create(path);}
                        catch(Exception e){EditorUtility.DisplayDialog(label+" 생성 확인",e.Message,"확인");}
                    }
                }
                using(new EditorGUI.DisabledScope(current==null))
                    if(GUILayout.Button("편집",GUILayout.Width(46)))edit(current);
            }
            return current;
        }
        public static MechDefinition MechSelector(MechDefinition current)=>Selector("기체",current,CreateMech,MechEditorWindow.Edit);
        public static PilotDefinition PilotSelector(PilotDefinition current)=>Selector("파일럿",current,CreatePilot,PilotEditorWindow.Edit);
    }
}
