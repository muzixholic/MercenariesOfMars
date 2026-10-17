using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MercenariesOfMars.PlayMode.Tests
{
    public sealed class TacticalThemeProbe : MonoBehaviour
    {
        readonly TacticalGuiTheme theme=new();
        public bool Sampled,Restored,ReadableHover,ButtonFeedback,RootEnabled;
        void OnGUI()
        {
            var originalSkin=GUI.skin; bool originalEnabled=GUI.enabled; var originalColor=GUI.color;
            GUI.enabled=false; GUI.color=new Color(.5f,.6f,.7f,.8f); var requestedColor=GUI.color;
            using(theme.Use())
            {
                RootEnabled=GUI.enabled;
                ReadableHover=GUI.skin.label.normal.textColor==GUI.skin.label.hover.textColor;
                ButtonFeedback=GUI.skin.button.normal.background!=null && GUI.skin.button.hover.background!=GUI.skin.button.normal.background;
            }
            Restored=GUI.skin==originalSkin && !GUI.enabled && GUI.color==requestedColor;
            GUI.enabled=originalEnabled; GUI.color=originalColor; Sampled=true;
        }
        void OnDestroy()=>theme.Dispose();
    }
    public sealed class TacticalThemeTests
    {
        [UnityTest] public IEnumerator 공통테마는_호버가독성과_버튼강조를_유지하고_GUI상태를_복원한다()
        {
            var root=new GameObject("공통 테마 상태 검증"); var probe=root.AddComponent<TacticalThemeProbe>();
            try
            {
                for(int i=0;i<15 && !probe.Sampled;i++) yield return null;
                Assert.That(probe.Sampled,Is.True); Assert.That(probe.RootEnabled,Is.True);
                Assert.That(probe.ReadableHover,Is.True); Assert.That(probe.ButtonFeedback,Is.True); Assert.That(probe.Restored,Is.True);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
