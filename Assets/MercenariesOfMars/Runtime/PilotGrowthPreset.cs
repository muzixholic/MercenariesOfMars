using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MercenariesOfMars
{
    [CreateAssetMenu(menuName="화성의 용병단/파일럿 성장 프리셋",fileName="새 성장 프리셋")]
    public sealed class PilotGrowthPreset : ScriptableObject
    {
        public string displayName="새 성장 프리셋";
        [FormerlySerializedAs("attack")] public AnimationCurve curve=AnimationCurve.Linear(0,0,1,1);
        // 기존 다중 커브 에셋을 단일 커브 프리셋으로 이전할 때만 사용한다.
        [HideInInspector] public AnimationCurve defense;
        [HideInInspector] public AnimationCurve accuracy;
        [HideInInspector] public AnimationCurve evasion;
        public float Evaluate(int level)=>Evaluate(curve,level);
        public float Evaluate(AnimationCurve curve,int level)
        {
            float t=(Mathf.Clamp(level,1,99)-1)/98f;
            float value=curve!=null?curve.Evaluate(t):t;
            return float.IsFinite(value)?Mathf.Clamp01(value):t;
        }
        public List<string> ValidatePreset()
        {
            var errors=new List<string>();
            var curves=new[]{curve}; var names=new[]{"성장 커브"};
            for(int i=0;i<curves.Length;i++)
            {
                var curve=curves[i];
                if(curve==null || curve.length<2) { errors.Add(names[i]+": 커브의 시작점과 끝점이 필요합니다."); continue; }
                float previous=-.0001f;
                for(int level=1;level<=99;level++)
                {
                    float v=curve.Evaluate((level-1)/98f);
                    if(!float.IsFinite(v)||v<-.0001f||v>1.0001f||v<previous-.0001f
                        ||(level==1 && Mathf.Abs(v)>.0001f)||(level==99 && Mathf.Abs(v-1)>.0001f))
                    { errors.Add(names[i]+": X/Y 0~1, 시작 0·끝 1, 레벨에 따라 감소하지 않는 커브로 설정하세요."); break; }
                    previous=v;
                }
            }
            return errors;
        }
    }
}
