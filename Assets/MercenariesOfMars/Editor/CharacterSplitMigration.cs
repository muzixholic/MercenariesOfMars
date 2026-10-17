using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public static class CharacterSplitMigration
    {
        public static void Split(CharacterDefinition source)
        {
            string path=AssetDatabase.GetAssetPath(source);
            if(string.IsNullOrEmpty(path)) throw new System.ArgumentException("저장된 캐릭터 에셋이 필요합니다.");
            string folder=Path.GetDirectoryName(path).Replace('\\','/');
            Undo.RecordObject(source,"기체와 파일럿 분리");
            if(source.mech==null)
            {
                if(!AssetDatabase.IsValidFolder(folder+"/기체")) AssetDatabase.CreateFolder(folder,"기체");
                var mech=ScriptableObject.CreateInstance<MechDefinition>(); mech.displayName=source.displayName;
                mech.mapIcon=source.mapIcon; mech.maxHp=source.maxHp; mech.maxEnergy=source.maxEnergy; mech.movement=source.movement;
                mech.description=source.description; mech.weapons=new List<SkillDefinition>(source.skills??new());
                AssetDatabase.CreateAsset(mech,AssetDatabase.GenerateUniqueAssetPath(folder+"/기체/"+Path.GetFileNameWithoutExtension(path)+"_기체.asset"));
                Undo.RegisterCreatedObjectUndo(mech,"기체 데이터 만들기"); source.mech=mech; AssetDatabase.SaveAssetIfDirty(mech);
            }
            if(source.pilot==null)
            {
                if(!AssetDatabase.IsValidFolder(folder+"/파일럿")) AssetDatabase.CreateFolder(folder,"파일럿");
                var pilot=ScriptableObject.CreateInstance<PilotDefinition>(); pilot.displayName=source.displayName; pilot.level=source.level;
                pilot.attack=source.attack; pilot.defense=source.defense; pilot.accuracy=source.accuracy; pilot.evasion=source.evasion;
                pilot.portraits=(source.portraits??new()).Where(p=>p!=null).Select(p=>new CharacterPortrait { id=p.id,label=p.label,sprite=p.sprite }).ToList();
                PilotProgressionSetup.InitializePilot(pilot,true);
                AssetDatabase.CreateAsset(pilot,AssetDatabase.GenerateUniqueAssetPath(folder+"/파일럿/"+Path.GetFileNameWithoutExtension(path)+"_파일럿.asset"));
                Undo.RegisterCreatedObjectUndo(pilot,"파일럿 데이터 만들기"); source.pilot=pilot; AssetDatabase.SaveAssetIfDirty(pilot);
            }
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
        }
    }
}
