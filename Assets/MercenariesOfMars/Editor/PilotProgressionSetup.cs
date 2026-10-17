using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public static class PilotProgressionSetup
    {
        public const string Folder="Assets/MercenariesOfMars/Resources/MercenariesOfMars";
        public static PilotProgressionSettings EnsureDefaults()
        {
            var existing=AssetDatabase.LoadAssetAtPath<PilotProgressionSettings>(Folder+"/성장설정.asset");
            if(existing!=null) return existing;
            EnsureFolder(Folder);
            var settings=ScriptableObject.CreateInstance<PilotProgressionSettings>();
            for(int i=0;i<3;i++)
            {
                var growth=ScriptableObject.CreateInstance<PilotGrowthPreset>(); growth.displayName=new[]{"선형 성장","초반 성장형","후반 성장형"}[i];
                AnimationCurve Curve()=>i==0?AnimationCurve.Linear(0,0,1,1):new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,i==1?.7f:.3f),new Keyframe(1,1));
                growth.curve=Curve();
                AssetDatabase.CreateAsset(growth,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+growth.displayName+".asset"));
                settings.growthPresets.Add(growth); AssetDatabase.SaveAssetIfDirty(growth);
                var enemy=ScriptableObject.CreateInstance<EnemyLevelPreset>(); enemy.displayName=new[]{"일반","정예","보스"}[i];
                enemy.multiplier=new[]{1f,1.05f,1.10f}[i]; enemy.offset=i;
                AssetDatabase.CreateAsset(enemy,AssetDatabase.GenerateUniqueAssetPath(Folder+"/적군_"+enemy.displayName+".asset"));
                settings.enemyLevelPresets.Add(enemy); AssetDatabase.SaveAssetIfDirty(enemy);
            }
            settings.newPilotGrowth=settings.growthPresets[0]; settings.defaultEnemyLevel=settings.enemyLevelPresets[0];
            AssetDatabase.CreateAsset(settings,Folder+"/성장설정.asset"); AssetDatabase.SaveAssetIfDirty(settings); return settings;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            int slash=path.LastIndexOf('/'); EnsureFolder(path.Substring(0,slash)); AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
        public static void InitializePilot(PilotDefinition pilot,bool preserveCurrentStats=false)
        {
            pilot.level=Mathf.Clamp(pilot.level,1,99);
            pilot.attack=Mathf.Clamp(pilot.attack,1,999); pilot.defense=Mathf.Clamp(pilot.defense,1,999);
            pilot.accuracy=Mathf.Clamp(pilot.accuracy,1,999); pilot.evasion=Mathf.Clamp(pilot.evasion,1,999);
            if(pilot.growthPreset==null && pilot.attackGrowth==null && pilot.defenseGrowth==null && pilot.accuracyGrowth==null && pilot.evasionGrowth==null)
            {
                pilot.attackAt99=Mathf.Min(999,pilot.attack+98); pilot.defenseAt99=Mathf.Min(999,pilot.defense+98);
                pilot.accuracyAt99=Mathf.Min(999,pilot.accuracy+98); pilot.evasionAt99=Mathf.Min(999,pilot.evasion+98);
                var preset=EnsureDefaults().newPilotGrowth;
                pilot.attackGrowth=pilot.defenseGrowth=pilot.accuracyGrowth=pilot.evasionGrowth=preset;
                if(preserveCurrentStats && preset!=null)
                {
                    void Rebase(ref int start,ref int end,AnimationCurve curve)
                    {
                        int current=start; float t=preset.Evaluate(curve,pilot.level);
                        if(t<=.000001f) return;
                        start=Mathf.Max(1,current-(pilot.level-1));
                        end=Mathf.Clamp(Mathf.FloorToInt(start+(current-start)/t+.5f),current,999);
                        if(t<.999999f) start=Mathf.Clamp(Mathf.FloorToInt((current-end*t)/(1-t)+.5f),1,current);
                        else end=current;
                        if(Mathf.FloorToInt(start+(end-start)*t+.5f)!=current) start=end=current;
                    }
                    Rebase(ref pilot.attack,ref pilot.attackAt99,preset.curve);
                    Rebase(ref pilot.defense,ref pilot.defenseAt99,preset.curve);
                    Rebase(ref pilot.accuracy,ref pilot.accuracyAt99,preset.curve);
                    Rebase(ref pilot.evasion,ref pilot.evasionAt99,preset.curve);
                }
            }
        }
    }
}
