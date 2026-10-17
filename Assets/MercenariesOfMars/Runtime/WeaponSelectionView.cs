using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MercenariesOfMars
{
    // 선택창은 실행 중에만 생성한다. 씬·프리팹과 전투 데이터에는 UI 상태를 저장하지 않는다.
    public sealed class WeaponSelectionView : MonoBehaviour
    {
        static readonly Color Ink=TacticalGuiTheme.Ink, Paper=TacticalGuiTheme.Paper, Line=TacticalGuiTheme.Line;
        static readonly Color Navy=TacticalGuiTheme.Navy, Red=TacticalGuiTheme.Red;
        const float Width=1000, Height=660, ListTop=218, ListHeight=208, RowHeight=52;
        BattleController owner;
        Canvas canvas;
        RectTransform panel, content;
        GameObject root;
        Font font;
        bool ownsFont;
        Texture2D metalTexture,blueTexture;
        Sprite metalSprite,blueSprite;
        BattleUnit boundUnit;
        readonly List<Row> rows=new();
        Text pilotName,mechName,hpText,enText,weightText,movementText,detailName,detailText,detailTag,hint,missionText;
        Text portraitFallback,mechFallback,iconFallback;
        Image portrait,mechIcon,detailIcon,hpFill,enFill,weightFill,confirmFill,ammoFill;
        Text confirmText,ammoText;
        RectTransform scrollbar;
        int focusedIndex=-1;
        float scrollOffset;
        bool counterMode;
        IncomingAttack boundIncoming;
        BattleUnit SelectionUnit=>counterMode?owner.Session?.PendingAttack?.Target:owner.SelectedUnit;
        public bool Visible=>owner!=null && owner.isActiveAndEnabled && owner.showControls && (counterMode
            ?owner.IsCounterWeaponSelectionOpen && owner.Session.PendingAttack==boundIncoming
            :owner.CanShowUnitMenu && owner.CommandMode==UnitCommandMode.SkillSelection);
        bool HasTarget(BattleUnit unit,BattleSkill skill)=>owner.Session.Units.Any(target=>target.IsAlive
            && target.Faction!=unit.Faction && skill.IsInRange(unit.Cell,target.Cell));
        bool CanUse(BattleSkill skill)=>counterMode?owner.Session.CanCounterattack(skill)
            :owner.CanUseAttackWeapon(boundUnit,skill);
        public bool IsRendered=>root!=null && root.activeSelf;
        public int FocusedIndex=>focusedIndex;
        public BattleSkill FocusedWeapon=>boundUnit!=null && focusedIndex>=0 && focusedIndex<boundUnit.Skills.Count?boundUnit.Skills[focusedIndex]:null;
        public bool CanConfirm=>Visible && FocusedWeapon!=null && CanUse(FocusedWeapon);
        public string UnavailableReason=>Reason(boundUnit,FocusedWeapon);
        float ViewWidth=>canvas!=null?canvas.pixelRect.width:Screen.width;
        float ViewHeight=>canvas!=null?canvas.pixelRect.height:Screen.height;
        public Rect ScreenRect=>CalculateRect(ViewWidth,ViewHeight);
        public Sprite DisplayedPortrait=>portrait!=null?portrait.sprite:null;
        public Color HpColor=>hpFill!=null?hpFill.color:Red;
        public Color EnergyColor=>enFill!=null?enFill.color:new Color(.1f,.38f,.86f);
        public int RenderedRowCount=>rows.Count;
        sealed class Row
        {
            public RectTransform root;
            public Image background,icon;
            public Text cursor,name,power,range,accuracy,energy,ammo,movement,number;
        }
        public static float ScaleFor(float width,float height)=>Mathf.Max(.1f,Mathf.Min(width/1280f,height/800f));
        public static Rect CalculateRect(float width,float height)
        {
            float scale=ScaleFor(width,height);
            return new Rect((width-Width*scale)/2,(height-Height*scale)/2,Width*scale,Height*scale);
        }
        public void Initialize(BattleController controller) { owner=controller; }
        public void Open(bool counter=false)
        {
            if(owner==null) return;
            counterMode=counter; boundIncoming=counter?owner.Session?.PendingAttack:null;
            if(SelectionUnit==null) return;
            EnsureBuilt(); Bind(SelectionUnit);
            focusedIndex=boundUnit.Skills.Count==0?-1:Mathf.Max(0,boundUnit.Skills.ToList().FindIndex(s=>CanUse(s)));
            scrollOffset=0; Refresh();
        }
        public void Refresh()
        {
            if(!Visible) { Hide(); return; }
            EnsureBuilt(); if(boundUnit!=SelectionUnit) { Bind(SelectionUnit); focusedIndex=rows.Count==0?-1:0; scrollOffset=0; }
            root.SetActive(true);
            float scale=ScaleFor(ViewWidth,ViewHeight); canvas.scaleFactor=scale;
            var rect=ScreenRect; Place(panel,new Rect(rect.x/scale,rect.y/scale,Width,Height));
            pilotName.text=boundUnit.PilotName+" · Lv."+boundUnit.Level; mechName.text=boundUnit.Name;
            hpText.text=$"{boundUnit.Hp} / {boundUnit.MaxHp}"; enText.text=$"{boundUnit.Energy} / {boundUnit.MaxEnergy}";
            weightText.text=$"{boundUnit.WeaponWeight} / {boundUnit.MaxWeaponWeight}";
            movementText.text=boundUnit.HasMoved?"이동 완료":"이동 전";
            SetFill(hpFill,boundUnit.MaxHp>0?(float)boundUnit.Hp/boundUnit.MaxHp:0,156);
            SetFill(enFill,boundUnit.MaxEnergy>0?(float)boundUnit.Energy/boundUnit.MaxEnergy:0,156);
            SetFill(weightFill,boundUnit.MaxWeaponWeight>0?(float)boundUnit.WeaponWeight/boundUnit.MaxWeaponWeight:0,156);
            missionText.text=(owner.board.map!=null?owner.board.map.displayName:"전술 전투")+"   /   "+owner.Session.Round+"턴 · "+(owner.Session.ActiveFaction==Faction.Ally?"아군 턴":"적군 턴");
            for(int i=0;i<rows.Count;i++) RefreshRow(rows[i],boundUnit.Skills[i],i);
            var weapon=FocusedWeapon;
            detailName.text=weapon!=null?weapon.Name:"보유 무장 없음";
            detailText.text=weapon!=null?(string.IsNullOrWhiteSpace(weapon.Definition.description)?"무장 수치를 확인하고 공격 범위를 선택하세요.":weapon.Definition.description):"기체 에디터에서 무장을 추가하세요.";
            detailTag.text=weapon!=null?$"사거리 {weapon.MinRange}~{weapon.MaxRange}  ·  무게 {weapon.Weight}  ·  "+(weapon.CanUseAfterMoving?"이동 후 사용 가능":"이동 후 사용 불가"):"";
            SetSprite(detailIcon,iconFallback,weapon?.Icon);
            ammoText.text=weapon!=null?weapon.AmmoText:"―";
            SetFill(ammoFill,weapon==null?0:weapon.MaxAmmo==0?1:(float)weapon.CurrentAmmo/weapon.MaxAmmo,180);
            hint.text=CanConfirm?(counterMode?"결정하면 반격 무장을 설정하고 전투 확인으로 이동합니다.":"선택하면 공격 범위를 표시합니다."):UnavailableReason;
            hint.color=CanConfirm?Navy:Red;
            confirmFill.color=CanConfirm?Navy:new Color(.43f,.46f,.48f);
            confirmText.color=CanConfirm?Color.white:new Color(.73f,.75f,.76f);
            ScrollTo(scrollOffset);
        }
        void LateUpdate()=>Refresh();
        public bool FocusWeapon(int index)
        {
            if(!Visible || boundUnit==null || index<0 || index>=boundUnit.Skills.Count) return false;
            focusedIndex=index;
            if(index*RowHeight<scrollOffset) scrollOffset=index*RowHeight;
            if((index+1)*RowHeight>scrollOffset+ListHeight) scrollOffset=(index+1)*RowHeight-ListHeight;
            Refresh(); return true;
        }
        public bool MoveFocus(int delta)
        {
            if(boundUnit==null || boundUnit.Skills.Count==0) return false;
            int count=boundUnit.Skills.Count;
            return FocusWeapon((focusedIndex+delta%count+count)%count);
        }
        public bool ConfirmFocused()
        {
            if(!CanConfirm) return false;
            if(!(counterMode?owner.ChooseReaction(ReactionChoice.Counterattack,focusedIndex):owner.SelectSkill(focusedIndex))) return false;
            Hide(); return true;
        }
        public void Cancel()
        {
            if(!Visible) return;
            if(counterMode) owner.CancelCounterWeaponSelection();
            else owner.SetCommandMode(UnitCommandMode.None);
            Hide();
        }
        public void HandleInput()
        {
            if(!Visible) return;
            var keyboard=Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
                if(keyboard.upArrowKey.wasPressedThisFrame) MoveFocus(-1);
                if(keyboard.downArrowKey.wasPressedThisFrame) MoveFocus(1);
                if(keyboard.homeKey.wasPressedThisFrame) FocusWeapon(0);
                if(keyboard.endKey.wasPressedThisFrame) FocusWeapon(rows.Count-1);
                if(keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) { ConfirmFocused(); return; }
            }
            var mouse=Mouse.current; if(mouse==null) return;
            var point=mouse.position.ReadValue(); point.y=Screen.height-point.y;
            if(mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
            HandlePointer(point,mouse.leftButton.wasPressedThisFrame,mouse.scroll.ReadValue().y);
        }
        // BattleController가 먼저 입력을 넘기므로 창 클릭·휠이 맵 조작에 전달되지 않는다.
        public bool HandlePointer(Vector2 guiPoint,bool click,float wheel=0)
        {
            if(!Visible) return false;
            var rect=ScreenRect;
            if(!rect.Contains(guiPoint))
            {
                if(click) { if(counterMode) Cancel(); else { owner.ClearSelection(); Hide(); } }
                return false;
            }
            var p=(guiPoint-rect.position)/ScaleFor(ViewWidth,ViewHeight);
            if(new Rect(18,ListTop,964,ListHeight).Contains(p))
            {
                if(wheel!=0) { ScrollTo(scrollOffset-wheel/120f*RowHeight); Refresh(); }
                if(click) FocusWeapon(Mathf.FloorToInt((p.y-ListTop+scrollOffset)/RowHeight));
            }
            if(click && new Rect(744,568,104,42).Contains(p)) ConfirmFocused();
            else if(click && new Rect(860,568,104,42).Contains(p)) Cancel();
            return true;
        }
        string Reason(BattleUnit unit,BattleSkill skill)
        {
            if(skill==null || unit==null) return "사용할 수 있는 무장이 없습니다.";
            var reasons=new List<string>();
            if(!skill.HasAmmo) reasons.Add("잔탄 없음");
            if(unit.Energy<skill.EnergyCost) reasons.Add("EN 부족");
            if(counterMode && boundIncoming!=null && !skill.IsInRange(unit.Cell,boundIncoming.Attacker.Cell)) reasons.Add("반격 사거리 밖");
            if(!counterMode && unit.HasMoved && !skill.CanUseAfterMoving) reasons.Add("이동 후 사용 불가");
            if(!counterMode && !HasTarget(unit,skill)) reasons.Add("사거리 내 대상 없음");
            return string.Join(" · ",reasons);
        }
        public void Hide() { if(root!=null) root.SetActive(false); }
        void Bind(BattleUnit unit)
        {
            boundUnit=unit;
            foreach(var row in rows) { row.root.gameObject.SetActive(false); Destroy(row.root.gameObject); } rows.Clear();
            if(unit==null) return;
            SetSprite(portrait,portraitFallback,unit.Portrait); SetSprite(mechIcon,mechFallback,unit.MapIcon);
            for(int i=0;i<unit.Skills.Count;i++) rows.Add(BuildRow(i));
            content.sizeDelta=new Vector2(964,Mathf.Max(ListHeight,rows.Count*RowHeight));
        }
        void ScrollTo(float offset)
        {
            scrollOffset=Mathf.Clamp(offset,0,Mathf.Max(0,rows.Count*RowHeight-ListHeight));
            content.anchoredPosition=new Vector2(0,scrollOffset);
            bool overflow=rows.Count*RowHeight>ListHeight;
            scrollbar.gameObject.SetActive(overflow);
            if(overflow)
            {
                float thumb=ListHeight*ListHeight/(rows.Count*RowHeight);
                Place(scrollbar,new Rect(975,ListTop+(ListHeight-thumb)*scrollOffset/(rows.Count*RowHeight-ListHeight),5,thumb));
            }
        }
        void RefreshRow(Row row,BattleSkill skill,int index)
        {
            bool focused=index==focusedIndex,usable=CanUse(skill);
            row.background.sprite=focused?blueSprite:metalSprite;
            row.background.color=focused?Color.white:usable?Color.white:new Color(.72f,.74f,.75f);
            Color color=focused?Color.white:usable?Ink:new Color(.34f,.36f,.38f);
            foreach(var text in new[]{row.cursor,row.name,row.power,row.range,row.accuracy,row.energy,row.ammo,row.movement,row.number}) text.color=color;
            row.cursor.text=focused?"▶":""; row.name.text=skill.Name;
            row.power.text=skill.Power.ToString(); row.range.text=$"{skill.MinRange}~{skill.MaxRange}";
            row.accuracy.text=$"{skill.AccuracyModifier:+0;-0;0}"; row.energy.text=skill.EnergyCost.ToString();
            row.ammo.text=skill.MaxAmmo==0?"제한 없음":$"{skill.CurrentAmmo}/{skill.MaxAmmo}";
            string reason=Reason(boundUnit,skill);
            row.movement.text=string.IsNullOrEmpty(reason)?(skill.CanUseAfterMoving?"사용 가능":"이동 전만"):reason;
            if(!usable) row.movement.color=focused?new Color(1,.82f,.7f):Red;
            row.icon.sprite=skill.Icon; row.icon.enabled=skill.Icon!=null; row.number.enabled=skill.Icon==null;
        }
        void EnsureBuilt()
        {
            if(root!=null) return;
            font=TacticalGuiTheme.CreateFont(owner.weaponMenuFont,out ownsFont);
            metalSprite=Gradient(new Color(.79f,.81f,.82f),new Color(.65f,.69f,.71f),out metalTexture);
            blueSprite=Gradient(new Color(.21f,.39f,.62f),new Color(.10f,.25f,.45f),out blueTexture);
            root=new GameObject("고전 전술 무장 선택 UI",typeof(RectTransform),typeof(Canvas)); root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=60; canvas.pixelPerfect=false;
            panel=Block(root.transform,"무장 선택 패널",new Rect(0,0,Width,Height),Paper).rectTransform;
            Bevel(panel,new Rect(0,0,Width,Height));
            var title=Block(panel,"남색 제목줄",new Rect(7,7,986,45),Navy); 
            Label(panel,"무장 선택",new Rect(25,9,360,40),28,Color.white,true);
            missionText=Label(panel,"",new Rect(480,13,490,34),14,new Color(.87f,.91f,.96f),false,TextAnchor.MiddleRight);
            var status=Block(panel,"파일럿·기체 상태",new Rect(14,61,972,114),Paper).rectTransform; Bevel(status,new Rect(0,0,972,114));
            Block(status,"초상 배경",new Rect(9,9,92,96),new Color(.27f,.32f,.38f));
            portraitFallback=Label(status,"파일럿",new Rect(12,37,86,30),14,new Color(.7f,.76f,.8f),true,TextAnchor.MiddleCenter);
            portrait=SpriteImage(status,new Rect(10,10,90,94));
            pilotName=Label(status,"",new Rect(116,14,260,32),20,Ink,true);
            mechName=Label(status,"",new Rect(116,52,260,32),22,Navy,true);
            Label(status,"출격 유닛",new Rect(118,86,240,18),11,Line);
            mechFallback=Label(status,"기체",new Rect(382,46,85,24),14,Line,false,TextAnchor.MiddleCenter);
            mechIcon=SpriteImage(status,new Rect(380,9,90,96));
            Block(status,"상태 구분선",new Rect(484,8,1,99),Line);
            Label(status,"HP",new Rect(502,10,95,22),16,Ink,true); hpFill=Bar(status,new Rect(590,15,156,13),new Color(.83f,.12f,.16f));
            hpText=Label(status,"",new Rect(755,8,200,24),17,Ink,true);
            Label(status,"EN",new Rect(502,37,95,22),16,Ink,true); enFill=Bar(status,new Rect(590,42,156,13),new Color(.1f,.38f,.86f));
            enText=Label(status,"",new Rect(755,35,200,24),17,Ink,true);
            Label(status,"무장 중량",new Rect(502,64,95,22),14,Ink,true); weightFill=Bar(status,new Rect(590,69,156,13),new Color(.64f,.57f,.34f));
            weightText=Label(status,"",new Rect(755,62,200,24),17,Ink,true);
            Label(status,"이동 상태",new Rect(502,88,95,19),13,Ink,true); movementText=Label(status,"",new Rect(605,88,320,19),13,Ink,true);
            var table=Block(panel,"무장 표",new Rect(14,184,972,246),Paper).rectTransform; Bevel(table,new Rect(0,0,972,246));
            foreach(var cell in new[]{("무장명",24f,296f),("공격력",320f,100f),("사거리",420f,86f),("명중 보정",506f,102f),("소비 EN",608f,88f),("잔탄",696f,105f),("이동·사용 상태",801f,159f)})
                Label(table,cell.Item1,new Rect(cell.Item2,4,cell.Item3,28),14,Ink,true,TextAnchor.MiddleCenter);
            var viewport=Block(panel,"무장 목록 클리핑",new Rect(18,ListTop,964,ListHeight),Color.clear).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            content=Block(viewport,"무장 행 목록",new Rect(0,0,964,ListHeight),Color.clear).rectTransform;
            scrollbar=Block(panel,"스크롤 위치",new Rect(975,ListTop,5,ListHeight),Navy).rectTransform;
            var detail=Block(panel,"선택 무장 상세",new Rect(14,440,972,166),Paper).rectTransform; Bevel(detail,new Rect(0,0,972,166));
            detailName=Label(detail,"",new Rect(16,7,300,28),18,Ink,true);
            Block(detail,"도해 구분선",new Rect(300,8,1,148),Line);
            iconFallback=Label(detail,"무장",new Rect(40,55,230,36),24,Line,true,TextAnchor.MiddleCenter);
            detailIcon=SpriteImage(detail,new Rect(18,40,266,83));
            ammoText=Label(detail,"",new Rect(19,129,260,22),14,Ink,true,TextAnchor.MiddleCenter);
            ammoFill=Bar(detail,new Rect(60,116,180,6),new Color(.51f,.46f,.28f));
            detailText=Label(detail,"",new Rect(320,10,635,56),16,Ink,true); detailText.alignment=TextAnchor.UpperLeft; detailText.verticalOverflow=VerticalWrapMode.Truncate;
            detailTag=Label(detail,"",new Rect(320,73,635,28),14,Navy,true);
            hint=Label(detail,"",new Rect(320,113,390,39),13,Navy); hint.alignment=TextAnchor.MiddleLeft;
            confirmFill=Block(panel,"결정 버튼",new Rect(744,568,104,42),Navy); Bevel(confirmFill.transform,new Rect(0,0,104,42));
            confirmText=Label(confirmFill.transform,"결정",new Rect(0,0,104,42),20,Color.white,true,TextAnchor.MiddleCenter);
            var cancel=Block(panel,"취소 버튼",new Rect(860,568,104,42),Paper); Bevel(cancel.transform,new Rect(0,0,104,42));
            Label(cancel.transform,"취소",new Rect(0,0,104,42),20,Ink,true,TextAnchor.MiddleCenter);
            Label(panel,"↑↓ 선택   ·   Enter 결정   ·   Esc 취소",new Rect(450,617,516,25),13,Ink,false,TextAnchor.MiddleRight);
            Label(panel,"무장 선택 / 공격·반격 설정",new Rect(23,617,410,25),12,Line);
        }
        Row BuildRow(int index)
        {
            var row=new Row(); row.background=Block(content,"무장 "+(index+1),new Rect(0,index*RowHeight,956,RowHeight),Color.white); row.root=row.background.rectTransform;
            Block(row.root,"행 경계",new Rect(0,RowHeight-1,956,1),new Color(.9f,.92f,.93f));
            row.cursor=Label(row.root,"",new Rect(5,4,20,44),16,Ink,true,TextAnchor.MiddleCenter);
            row.icon=SpriteImage(row.root,new Rect(31,7,62,36));
            row.number=Label(row.root,(index+1).ToString("00"),new Rect(32,9,60,32),15,Line,true,TextAnchor.MiddleCenter);
            row.name=Label(row.root,"",new Rect(104,3,202,46),18,Ink,true);
            row.power=Cell(306,100); row.range=Cell(406,86); row.accuracy=Cell(492,102); row.energy=Cell(594,88); row.ammo=Cell(682,105); row.movement=Cell(787,169,12);
            Text Cell(float x,float width,int size=18)
            {
                Block(row.root,"열 구분선",new Rect(x,0,1,RowHeight),new Color(.5f,.56f,.6f,.45f));
                return Label(row.root,"",new Rect(x+3,3,width-6,46),size,Ink,false,TextAnchor.MiddleCenter);
            }
            return row;
        }
        Image Block(Transform parent,string name,Rect rect,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; Place(image.rectTransform,rect); return image;
        }
        Text Label(Transform parent,string text,Rect rect,int size,Color color,bool bold=false,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            var go=new GameObject(string.IsNullOrEmpty(text)?"정보":text,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); go.transform.SetParent(parent,false);
            var label=go.GetComponent<Text>(); label.font=font; label.text=text; label.fontSize=size; label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            label.color=color; label.alignment=anchor; label.raycastTarget=false; label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate;
            Place(label.rectTransform,rect); return label;
        }
        Image SpriteImage(Transform parent,Rect rect) { var image=Block(parent,"이미지",rect,Color.white); image.preserveAspect=true; image.enabled=false; return image; }
        static void SetSprite(Image image,Text fallback,Sprite sprite) { image.sprite=sprite; image.enabled=sprite!=null; fallback.enabled=sprite==null; }
        Image Bar(Transform parent,Rect rect,Color color)
        {
            var back=Block(parent,"게이지 배경",rect,new Color(.21f,.26f,.3f));
            var fill=Block(back.transform,"잔량",new Rect(1,1,rect.width-2,rect.height-2),color); return fill;
        }
        static void SetFill(Image fill,float fraction,float width) { var rect=fill.rectTransform; rect.sizeDelta=new Vector2(Mathf.Max(0,width-2)*Mathf.Clamp01(fraction),rect.sizeDelta.y); }
        static void Place(RectTransform target,Rect rect) { target.anchorMin=target.anchorMax=new Vector2(0,1); target.pivot=new Vector2(0,1); target.anchoredPosition=new Vector2(rect.x,-rect.y); target.sizeDelta=rect.size; }
        void Bevel(Transform parent,Rect r)
        {
            Block(parent,"외곽선",new Rect(r.x,r.y,r.width,1),Ink); Block(parent,"외곽선",new Rect(r.x,r.y,r.width>0?1:0,r.height),Ink);
            Block(parent,"하단 그림자",new Rect(r.x,r.yMax-2,r.width,2),Line); Block(parent,"우측 그림자",new Rect(r.xMax-2,r.y,2,r.height),Line);
            Block(parent,"상단 빛",new Rect(r.x+2,r.y+2,r.width-4,2),new Color(.94f,.95f,.96f));
            Block(parent,"좌측 빛",new Rect(r.x+2,r.y+2,2,r.height-4),new Color(.94f,.95f,.96f));
        }
        static Sprite Gradient(Color top,Color bottom,out Texture2D texture)
        {
            texture=new Texture2D(1,64,TextureFormat.RGBA32,false) {name="무장 패널 그라데이션",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
            for(int y=0;y<64;y++) texture.SetPixel(0,y,Color.Lerp(bottom,top,y/63f)); texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,1,64),new Vector2(.5f,.5f),100);
        }
        void OnDisable()=>Hide();
        void OnDestroy()
        {
            if(root!=null) Destroy(root); if(metalSprite!=null) Destroy(metalSprite); if(blueSprite!=null) Destroy(blueSprite);
            if(metalTexture!=null) Destroy(metalTexture); if(blueTexture!=null) Destroy(blueTexture); if(ownsFont && font!=null) Destroy(font);
        }
    }
}
