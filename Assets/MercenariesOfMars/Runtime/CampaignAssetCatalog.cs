using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    [Serializable] public sealed class CampaignAssetEntry {public string id;public ScriptableObject asset;}
    public sealed class CampaignAssetCatalog : ScriptableObject
    {
        public List<CampaignAssetEntry> entries=new();
        public string Id(ScriptableObject asset)
        {
            if(asset is MechDefinition m)asset=m.SourceDefinition;if(asset is PilotDefinition p)asset=p.SourceDefinition;
            var entry=entries.FirstOrDefault(e=>e.asset==asset);
            if(entry==null)throw new InvalidOperationException("저장 참조 목록에 없는 에셋입니다. 에디터에서 저장 참조 목록을 갱신하세요.");return entry.id;
        }
        public T Asset<T>(string id) where T:ScriptableObject
        {
            var asset=entries.FirstOrDefault(e=>e.id==id)?.asset as T;
            if(asset==null)throw new InvalidOperationException("저장 데이터가 참조하는 에셋을 찾을 수 없습니다.");return asset;
        }
        public static CampaignAssetCatalog Load()=>Resources.Load<CampaignAssetCatalog>("MercenariesOfMars/저장참조목록");
    }
}
