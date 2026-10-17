using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    // 기존 IMGUI 메뉴와 Canvas 무장창이 같은 색·글꼴을 사용한다.
    public sealed class TacticalGuiTheme : IDisposable
    {
        public static readonly Color Ink=new(.07f,.11f,.16f), Paper=new(.73f,.76f,.77f), Line=new(.34f,.39f,.43f);
        public static readonly Color Navy=new(.09f,.16f,.26f), Blue=new(.14f,.32f,.55f), Red=new(.68f,.12f,.15f);
        readonly List<Texture2D> textures=new();
        GUISkin skin;
        Font font,requestedFont;
        bool ownsFont;
        GUIStyle heading,section,muted,primary;
        float currentScale;
        public static Color ForGui(Color color)=>QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
        public static float LayoutScale=>Mathf.Clamp(Mathf.Min(Screen.width/1280f,Screen.height/800f),.8f,1.25f);
        public GUIStyle Heading=>heading;
        public GUIStyle Section=>section;
        public GUIStyle Muted=>muted;
        public GUIStyle Primary=>primary;
        public static Rect Centered(float width,float height)
        {
            width=Mathf.Min(width*LayoutScale,Mathf.Max(1,Screen.width-24));
            height=Mathf.Min(height*LayoutScale,Mathf.Max(1,Screen.height-24));
            return new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        }
        public static Font CreateFont(Font preferred,out bool owned)
        {
            owned=false; if(preferred!=null) return preferred;
            var installed=Font.GetOSInstalledFontNames();
            string family=new[]{"Apple SD Gothic Neo","Malgun Gothic","Noto Sans CJK KR","Noto Sans KR","Arial Unicode MS"}.FirstOrDefault(installed.Contains);
            if(family==null) return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            owned=true; return Font.CreateDynamicFontFromOSFont(family,20);
        }
        public Scope Use(Font preferred=null)
        {
            if(skin==null || requestedFont!=preferred) Build(preferred);
            if(!Mathf.Approximately(currentScale,LayoutScale)) Resize();
            return new Scope(skin);
        }
        void Build(Font preferred)
        {
            Dispose(); requestedFont=preferred; font=CreateFont(preferred,out ownsFont);
            skin=UnityEngine.Object.Instantiate(GUI.skin); skin.name="고전 전술 공통 UI"; skin.hideFlags=HideFlags.DontSave; skin.font=font;
            var paper=Texture(Paper,new Color(.66f,.70f,.72f));
            var blue=Texture(new Color(.22f,.40f,.64f),new Color(.11f,.25f,.45f));
            skin.box=new GUIStyle(skin.box) {border=new RectOffset(4,4,4,4),padding=new RectOffset(12,12,10,10),margin=new RectOffset(3,3,3,3),alignment=TextAnchor.UpperLeft};
            skin.box.normal.background=paper; skin.box.normal.textColor=Ink;
            skin.label=new GUIStyle(skin.label) {wordWrap=true,padding=new RectOffset(0,0,2,2),margin=new RectOffset(2,2,2,2)};
            skin.label.normal.textColor=Ink;
            skin.button=new GUIStyle(skin.button) {border=new RectOffset(4,4,4,4),padding=new RectOffset(10,10,3,3),margin=new RectOffset(2,2,3,3),wordWrap=true,fontStyle=FontStyle.Bold};
            skin.button.normal.background=paper; skin.button.normal.textColor=Ink;
            foreach(var state in new[]{skin.button.hover,skin.button.active,skin.button.focused,skin.button.onNormal,skin.button.onHover,skin.button.onActive,skin.button.onFocused})
            { state.background=blue; state.textColor=Color.white; }
            skin.label.font=skin.box.font=skin.button.font=skin.toggle.font=font;
            skin.toggle.normal.textColor=Ink; skin.toggle.onNormal.textColor=Navy;
            skin.toggle.hover.textColor=Navy; skin.toggle.onHover.textColor=Navy;
            skin.horizontalSlider.normal.background=Texture(new Color(.26f,.31f,.35f),new Color(.26f,.31f,.35f));
            skin.horizontalSliderThumb.normal.background=blue;
            skin.verticalScrollbarThumb.normal.background=blue; skin.verticalScrollbarThumb.hover.background=blue;
            heading=new GUIStyle(skin.label) {fontStyle=FontStyle.Bold}; heading.normal.textColor=Navy;
            section=new GUIStyle(skin.label) {fontStyle=FontStyle.Bold}; section.normal.textColor=Navy;
            muted=new GUIStyle(skin.label); muted.normal.textColor=Line;
            primary=new GUIStyle(skin.button); primary.normal.background=blue; primary.normal.textColor=Color.white;
            foreach(var style in new[]{skin.box,skin.label,skin.toggle,heading,section,muted})
                foreach(var state in new[]{style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused}) state.textColor=style.normal.textColor;
            foreach(var style in new[]{skin.box,skin.label,skin.button,skin.toggle,heading,section,muted,primary})
                foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused}) state.textColor=ForGui(state.textColor);
            Resize();
        }
        void Resize()
        {
            currentScale=LayoutScale;
            skin.label.fontSize=skin.box.fontSize=skin.toggle.fontSize=Mathf.RoundToInt(15*currentScale);
            skin.button.fontSize=primary.fontSize=Mathf.RoundToInt(16*currentScale);
            heading.fontSize=Mathf.RoundToInt(25*currentScale); section.fontSize=Mathf.RoundToInt(18*currentScale); muted.fontSize=Mathf.RoundToInt(13*currentScale);
        }
        Texture2D Texture(Color top,Color bottom)
        {
            const int size=32;
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true) {name="고전 전술 입체 테두리",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                var color=Color.Lerp(bottom,top,y/(size-1f));
                if(x==0 || y==0 || x==size-1 || y==size-1) color=Ink;
                else if(x==1 || y==size-2) color=new Color(.94f,.95f,.96f);
                else if(x==size-2 || y==1) color=Line;
                // GUIStyle 배경 텍스처의 추가 색 변환을 보정해 Canvas 팔레트와 맞춘다.
                t.SetPixel(x,y,ForGui(ForGui(color)));
            }
            t.Apply(); textures.Add(t); return t;
        }
        public Rect Panel(Rect rect,string title)
        {
            GUI.Box(rect,GUIContent.none);
            float header=Mathf.Min(42*LayoutScale,rect.height*.3f);
            Fill(new Rect(rect.x+5,rect.y+5,rect.width-10,header),Navy);
            var style=new GUIStyle(heading) {alignment=TextAnchor.MiddleLeft}; style.normal.textColor=Color.white; style.fontSize=Mathf.RoundToInt(20*LayoutScale);
            GUI.Label(new Rect(rect.x+16,rect.y+6,rect.width-32,header-2),title,style);
            return new Rect(rect.x+14,rect.y+header+14,Mathf.Max(1,rect.width-28),Mathf.Max(1,rect.height-header-28));
        }
        public void BeginPanel(Rect rect,string title)=>GUILayout.BeginArea(Panel(rect,title));
        public static void EndPanel()=>GUILayout.EndArea();
        public static void Fill(Rect rect,Color color)
        {
            var old=GUI.color; GUI.color=ForGui(color); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old;
        }
        public static void DimScreen()=>Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.02f,.04f,.07f,.55f));
        public void Resource(Rect rect,int value,int maximum,bool energy=false)
        {
            Fill(rect,new Color(.17f,.21f,.25f));
            float fraction=maximum>0?Mathf.Clamp01((float)value/maximum):0;
            Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*fraction,rect.height-4),energy?new Color(.12f,.4f,1):new Color(.85f,.12f,.16f));
            var label=new GUIStyle(skin.label) {alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(12*LayoutScale),fontStyle=FontStyle.Bold};
            label.normal.textColor=Color.white; GUI.Label(rect,$"{(energy?"EN":"HP")} {value} / {maximum}",label);
        }
        public void Separator()=>Fill(GUILayoutUtility.GetRect(1,1,GUILayout.ExpandWidth(true)),Line);
        public void Marker(Rect rect,string title,string text,Color accent)
        {
            Fill(rect,Navy); Fill(new Rect(rect.x,rect.y,4,rect.height),accent);
            var label=new GUIStyle(skin.label) {alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(12*LayoutScale),fontStyle=FontStyle.Bold};
            label.normal.textColor=Color.white; GUI.Label(rect,title+"\n"+text,label);
        }
        public void Dispose()
        {
            if(skin!=null) UnityEngine.Object.Destroy(skin);
            foreach(var texture in textures) if(texture!=null) UnityEngine.Object.Destroy(texture);
            textures.Clear(); if(ownsFont && font!=null) UnityEngine.Object.Destroy(font);
            skin=null; font=null; ownsFont=false;
        }
        public readonly struct Scope : IDisposable
        {
            readonly GUISkin previous;
            readonly Color color,content,background;
            readonly bool enabled;
            public Scope(GUISkin next)
            {
                previous=GUI.skin; color=GUI.color; content=GUI.contentColor; background=GUI.backgroundColor; enabled=GUI.enabled;
                GUI.skin=next; GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white; GUI.enabled=true;
            }
            public void Dispose() { GUI.skin=previous; GUI.color=color; GUI.contentColor=content; GUI.backgroundColor=background; GUI.enabled=enabled; }
        }
    }
}
