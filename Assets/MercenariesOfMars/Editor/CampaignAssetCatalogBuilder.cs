using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MercenariesOfMars.Editor
{
    public sealed class CampaignAssetCatalogBuilder : AssetPostprocessor
    {
        const string Path="Assets/MercenariesOfMars/Resources/MercenariesOfMars/저장참조목록.asset";
        static bool queued;
        [MenuItem("화성의 용병단/저장 참조 목록 갱신")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var catalog=AssetDatabase.LoadAssetAtPath<CampaignAssetCatalog>(Path);
            var entries=new System.Collections.Generic.List<CampaignAssetEntry>();
            foreach(var type in new[]{"CampaignDefinition","StageDefinition","CharacterDefinition","MechDefinition","PilotDefinition","SkillDefinition"})
                foreach(var guid in AssetDatabase.FindAssets("t:"+type,new[]{"Assets"}))
                    entries.Add(new CampaignAssetEntry {id=guid,asset=AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid))});
            entries=entries.OrderBy(e=>e.id).ToList();
            if(catalog!=null && catalog.entries.Count==entries.Count && catalog.entries.Zip(entries,(a,b)=>a.id==b.id && a.asset==b.asset).All(x=>x))return;
            if(catalog==null){catalog=ScriptableObject.CreateInstance<CampaignAssetCatalog>();AssetDatabase.CreateAsset(catalog,Path);}
            catalog.entries=entries;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] from)
        {
            if(queued || EditorApplication.isPlayingOrWillChangePlaymode || !imported.Concat(deleted).Concat(moved).Any(p=>p.EndsWith(".asset") && p!=Path))return;
            queued=true;EditorApplication.delayCall+=()=>{queued=false;Build();};
        }
    }
}
