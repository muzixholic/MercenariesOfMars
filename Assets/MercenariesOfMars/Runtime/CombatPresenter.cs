using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MercenariesOfMars
{
    // 저장된 씬을 변경하지 않고 재생 중에만 독립적인 전투 무대를 구성한다.
    public sealed class CombatPresenter : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        public string Phase { get; private set; }
        public int DisplayedAttackerHp { get; private set; }
        public int DisplayedTargetHp { get; private set; }
        readonly List<Material> materials = new();
        BattleSession session;
        BattleAttackResult primary;
        Camera originalCamera;
        bool originalEnabled;
        GameObject stage;
        Transform left, right;
        string damageText;
        bool damageOnLeft;
        float damageProgress;
        Color damageColor;
        TacticalGuiTheme uiTheme;
        public Font UiFont { get; set; }
        static readonly Vector3 Origin = new(10000, 10000, 10000);

        public void Play(BattleSession battle, Camera source, int attackerHp, int targetHp, CombatPresentationProfile fallback)
        {
            Finish();
            session = battle; primary = battle.LastAttack;
            DisplayedAttackerHp = attackerHp; DisplayedTargetHp = targetHp;
            IsPlaying = true;
            originalCamera = source; originalEnabled = source != null && source.enabled;
            try
            {
                stage = new GameObject("전투 연출 무대（임시）"); stage.transform.position = Origin;
                var cameraObject = new GameObject("전투 연출 카메라"); cameraObject.transform.SetParent(stage.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.localPosition = new Vector3(0, 2.5f, -9);
                camera.transform.LookAt(Origin + Vector3.up);
                camera.orthographic = true; camera.orthographicSize = 3.2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .08f);
                camera.depth = source != null ? source.depth + 10 : 10;
                if (source != null) source.enabled = false;
                left = Shape(PrimitiveType.Capsule, new Vector3(-2, 1, 0), Vector3.one, UnitColor(primary.Attacker));
                right = Shape(PrimitiveType.Capsule, new Vector3(2, 1, 0), Vector3.one, UnitColor(primary.Target));
                Shape(PrimitiveType.Cube, new Vector3(0, -.2f, 0), new Vector3(12, .2f, 5), new Color(.12f, .16f, .23f));
                StartCoroutine(Playback(fallback));
            }
            catch { Finish(); throw; }
        }
        static Color UnitColor(BattleUnit unit) => unit.Faction == Faction.Ally ? new Color(.2f, .55f, 1) : new Color(1, .25f, .2f);
        Transform Shape(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(type); item.transform.SetParent(stage.transform, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            Destroy(item.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            material.color = color; materials.Add(material); item.GetComponent<Renderer>().sharedMaterial = material;
            return item.transform;
        }
        IEnumerator Playback(CombatPresentationProfile fallback)
        {
            try
            {
                yield return Strike(primary, false, fallback);
                if (session.LastCounterattack != null) yield return Strike(session.LastCounterattack, true, fallback);
                Phase = "전투 종료";
                yield return new WaitForSecondsRealtime(.45f);
            }
            finally { Finish(); }
        }
        IEnumerator Strike(BattleAttackResult result, bool counter, CombatPresentationProfile fallback)
        {
            var profile = result.Skill.Definition.presentation != null ? result.Skill.Definition.presentation : fallback;
            float attack = profile != null ? Mathf.Max(.05f, profile.attackSeconds) : .65f;
            float display = profile != null ? Mathf.Max(.05f, profile.damageSeconds) : .9f;
            float pause = profile != null ? Mathf.Max(.05f, profile.pauseSeconds) : .3f;
            float size = profile != null ? Mathf.Max(.1f, profile.effectScale) : 1;
            Color color = profile != null ? profile.effectColor : new Color(1, .72f, .16f);
            damageColor = profile != null ? profile.damageColor : new Color(1, .35f, .25f);
            var actor = counter ? right : left; var victim = counter ? left : right;
            Vector3 home = actor.localPosition;
            Phase = (counter ? "반격" : "공격") + " · " + result.Attacker.Name + " · " + result.Skill.Name;
            var effect = Shape(PrimitiveType.Sphere, actor.localPosition, Vector3.one * .15f, color);
            GameObject custom = null;
            if (profile != null && profile.attackEffectPrefab != null)
                custom = Instantiate(profile.attackEffectPrefab, victim.position, Quaternion.identity, stage.transform);
            for (float t = 0; t < attack; t += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(t / attack);
                actor.localPosition = home + (victim.localPosition - home).normalized * (Mathf.Sin(progress * Mathf.PI) * .65f);
                effect.localPosition = Vector3.Lerp(home, victim.localPosition, progress);
                effect.localScale = Vector3.one * size * (.15f + Mathf.Sin(progress * Mathf.PI) * .6f);
                yield return null;
            }
            actor.localPosition = home; Destroy(effect.gameObject);
            if (custom != null) Destroy(custom);
            Phase = counter ? "반격 피해" : "공격 피해";
            damageText = result.Hit ? $"−{result.Damage}" : "빗나감";
            damageOnLeft = counter;
            int oldHp = counter ? DisplayedAttackerHp : DisplayedTargetHp;
            for (float t = 0; t < display; t += Time.unscaledDeltaTime)
            {
                damageProgress = Mathf.Clamp01(t / display);
                int hp = Mathf.RoundToInt(Mathf.Lerp(oldHp, oldHp - result.Damage, Mathf.Min(1, damageProgress * 2)));
                if (counter) DisplayedAttackerHp = hp; else DisplayedTargetHp = hp;
                victim.localScale = Vector3.one * (1 + (result.Hit ? Mathf.Sin(damageProgress * Mathf.PI * 6) * .08f * (1 - damageProgress) : 0));
                yield return null;
            }
            if (counter) DisplayedAttackerHp = oldHp - result.Damage; else DisplayedTargetHp = oldHp - result.Damage;
            victim.localScale = Vector3.one;
            if (oldHp - result.Damage <= 0) victim.gameObject.SetActive(false);
            damageText = null;
            yield return new WaitForSecondsRealtime(pause);
        }
        public void Finish()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            StopAllCoroutines();
            if (originalCamera != null) originalCamera.enabled = originalEnabled;
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear(); damageText = null;
            var battle = session; session = null; battle?.CompletePresentation();
        }
        void OnDisable() => Finish();
        void OnDestroy() { Finish(); uiTheme?.Dispose(); }
        void OnGUI()
        {
            if(!IsPlaying || primary==null) return;
            uiTheme??=new TacticalGuiTheme();
            using(uiTheme.Use(UiFont))
            {
                float scale=TacticalGuiTheme.LayoutScale;
                uiTheme.Marker(new Rect(0,0,Screen.width,66*scale),"전투 연출",Phase,TacticalGuiTheme.Blue);
                float width=Mathf.Min(330*scale,Screen.width*.42f), height=115*scale;
                DrawCombatant(new Rect(12,Screen.height-height-12,width,height),primary.Attacker,DisplayedAttackerHp,"공격측");
                DrawCombatant(new Rect(Screen.width-width-12,Screen.height-height-12,width,height),primary.Target,DisplayedTargetHp,"대응측");
                if(damageText!=null)
                {
                    var damage=new GUIStyle(GUI.skin.label) {alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(42*scale),fontStyle=FontStyle.Bold};
                    damage.normal.textColor=TacticalGuiTheme.ForGui(damageColor);
                    GUI.Label(new Rect(Screen.width*(damageOnLeft ? .15f : .65f),Screen.height*.4f-damageProgress*45,Screen.width*.2f,70),damageText,damage);
                }
                if(GUI.Button(new Rect((Screen.width-150*scale)/2,Screen.height-55*scale,150*scale,36*scale),"연출 건너뛰기",uiTheme.Primary)) Finish();
            }
        }
        void DrawCombatant(Rect rect,BattleUnit unit,int hp,string role)
        {
            var area=uiTheme.Panel(rect,role+" · "+unit.Name);
            uiTheme.Resource(new Rect(area.x,area.y,area.width,20),hp,unit.MaxHp);
            uiTheme.Resource(new Rect(area.x,area.y+24,area.width,18),unit.Energy,unit.MaxEnergy,true);
        }
    }
}
