using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public static class PilotGrowthMigration
    {
        public static void ConvertPilot(PilotDefinition pilot,Func<PilotGrowthPreset,AnimationCurve,string,PilotGrowthPreset> resolve)
        {
            var old=pilot.growthPreset;
            if(old==null) return;
            pilot.attackGrowth=pilot.attackGrowth!=null?pilot.attackGrowth:resolve(old,old.curve,"공격력");
            pilot.defenseGrowth=pilot.defenseGrowth!=null?pilot.defenseGrowth:resolve(old,old.defense,"방어력");
            pilot.accuracyGrowth=pilot.accuracyGrowth!=null?pilot.accuracyGrowth:resolve(old,old.accuracy,"명중력");
            pilot.evasionGrowth=pilot.evasionGrowth!=null?pilot.evasionGrowth:resolve(old,old.evasion,"회피력");
            pilot.growthPreset=null;
        }
        public static int ConvertProject()
        {
            var settings=PilotProgressionSetup.EnsureDefaults();
            var variants=new Dictionary<string,PilotGrowthPreset>();
            PilotGrowthPreset Resolve(PilotGrowthPreset source,AnimationCurve curve,string stat)
            {
                curve??=AnimationCurve.Linear(0,0,1,1);
                if(curve.preWrapMode==source.curve.preWrapMode && curve.postWrapMode==source.curve.postWrapMode && curve.keys.SequenceEqual(source.curve.keys)) return source;
                string key=AssetDatabase.GetAssetPath(source)+stat;
                if(variants.TryGetValue(key,out var found)) return found;
                var variant=ScriptableObject.CreateInstance<PilotGrowthPreset>();
                variant.displayName=source.displayName+"（이전 "+stat+"）";
                variant.curve=new AnimationCurve(curve.keys) {preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
                string folder=System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\','/');
                AssetDatabase.CreateAsset(variant,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+variant.displayName+".asset"));
                settings.growthPresets.Add(variant); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(variant);
                variants[key]=variant; return variant;
            }
            int count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:PilotDefinition"))
            {
                var pilot=AssetDatabase.LoadAssetAtPath<PilotDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(pilot.growthPreset==null) continue;
                var before=Enumerable.Range(1,99).Select(pilot.StatsAtLevel).ToArray();
                ConvertPilot(pilot,Resolve);
                for(int lv=1;lv<=99;lv++)
                {
                    var a=before[lv-1]; var b=pilot.StatsAtLevel(lv);
                    if(!a.Equals(b)) throw new InvalidOperationException(pilot.displayName+": 성장 이전 결과 불일치");
                }
                EditorUtility.SetDirty(pilot); AssetDatabase.SaveAssetIfDirty(pilot); count++;
            }
            AssetDatabase.SaveAssetIfDirty(settings); return count;
        }
    }
}
