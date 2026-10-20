using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MercenariesOfMars
{
    public sealed partial class BattleController
    {
        public AllyDeploymentPlan Deployment {get;private set;}
        public bool IsDeploying=>!IsTitleScreen && Deployment!=null;
        List<UnitPlacement> deploymentPlacements;
        RosterDeployment rosterDeployment;
        bool formationNoticeConfirmed;
        Vector2 formationNoticeScroll;
        public bool HasFormationNotice=>!IsTitleScreen && rosterDeployment!=null && rosterDeployment.Notices.Count>0 && !formationNoticeConfirmed;
        public string FormationNotice=>rosterDeployment==null?"":string.Join("\n\n",rosterDeployment.Notices);
        public bool ConfirmFormationNotice()
        {
            if(!HasFormationNotice)return false;
            formationNoticeConfirmed=true;
            if(IsCampaignActive)Campaign.ApplyDeploymentRoster(rosterDeployment);
            InitializeBattle();
            if(Session==null && !IsDeploying && IsCampaignActive)Campaign.DeploymentFailed(error??"강제 편성 후 전투를 초기화하지 못했습니다.");
            return Session!=null || IsDeploying;
        }
        internal bool WithdrawAllyFromBattle(CharacterDefinition ally)
        {
            if(Session==null)return true;
            if(Session.IsPresenting || Session.PendingAttack!=null)return false;
            foreach(var unit in Session.Units.Where(u=>u.Faction==Faction.Ally && DeploymentCondition.SamePilot(u.Definition,ally)).ToList())
                Session.Withdraw(unit.Id);
            return true;
        }
        void ReleaseDeployment()
        {Deployment?.Dispose();rosterDeployment?.Dispose();rosterDeployment=null;formationNoticeConfirmed=false;formationNoticeScroll=Vector2.zero;}
        void DrawFormationNotice()
        {
            uiTheme.BeginPanel(TacticalGuiTheme.Centered(760,500),"고정 배치 · 강제 편성 알림");
            formationNoticeScroll=GUILayout.BeginScrollView(formationNoticeScroll);
            GUILayout.Label(FormationNotice,uiTheme.Section);
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            bool confirm=GUILayout.Button("확인 · 지정 편성 적용",uiTheme.Primary,GUILayout.Height(42));
            bool exit=GUILayout.Button(IsMapTest || IsStageTest?"테스트 종료":"타이틀로 돌아가기");
            TacticalGuiTheme.EndPanel();
            if(confirm)ConfirmFormationNotice();
            else if(exit){if(IsStageTest)ExitStageTest();else if(IsMapTest)ExitMapTest();else ReturnToTitle();}
        }
        string selectedDeploymentSlot;
        Vector2 deploymentScroll;
        public Rect DeploymentRect=>new(Screen.width-Mathf.Min(460*TacticalGuiTheme.LayoutScale,Screen.width*.48f)-12,12,
            Mathf.Min(460*TacticalGuiTheme.LayoutScale,Screen.width*.48f),Screen.height-24);
        public bool SelectDeploymentSlot(string id)
        {if(!IsDeploying || !Deployment.Slots.Any(s=>s.id==id))return false;selectedDeploymentSlot=id;return true;}
        public bool ConfirmDeployment()
        {
            if(!IsDeploying || !Deployment.CanConfirm) return false;
            try {deploymentPlacements=Deployment.CreatePlacements();}
            catch(System.Exception exception) {error=exception.Message;return false;}
            var plan=Deployment;Deployment=null;error=null;board.ClearMovementHighlights();
            InitializeBattle();
            if(Session!=null)return true;
            // 초기화가 실패하면 출격 편성을 유지해 다시 시도할 수 있게 한다.
            Deployment=plan;deploymentPlacements=null;board.SetMovementHighlights(plan.Slots.Select(s=>s.cell));return false;
        }
        void HandleDeploymentInput()
        {
            if(Mouse.current==null || battleCamera==null || MapCamera==null)return;
            var point=Mouse.current.position.ReadValue();var gui=new Vector2(point.x,Screen.height-point.y);
            if(showControls && DeploymentRect.Contains(gui)) {MapCamera.CancelPointer();return;}
            MapCamera.Zoom(Mouse.current.scroll.ReadValue().y);
            if(Mouse.current.leftButton.wasPressedThisFrame)MapCamera.BeginPointer(point);
            else if(Mouse.current.leftButton.isPressed)MapCamera.DragPointer(point);
            else if(Mouse.current.leftButton.wasReleasedThisFrame && MapCamera.EndPointer(point)
                && board.RayToCell(battleCamera.ScreenPointToRay(point),out var cell))
            {
                var slot=Deployment.Slots.FirstOrDefault(s=>s.cell==cell);if(slot!=null) SelectDeploymentSlot(slot.id);
            }
        }
        void DrawDeploymentLabel(Vector2Int cell,string text,bool ally)
        {
            var point=battleCamera.WorldToScreenPoint(board.CellToWorld(cell)+board.transform.up*board.cellSize*.15f);
            if(point.z<=0)return;
            var side=battleCamera.WorldToScreenPoint(board.CellToWorld(cell)+board.transform.right*board.cellSize);
            float width=Mathf.Clamp(Vector2.Distance(point,side)*.9f,40,110),height=width*.65f;
            var rect=new Rect(point.x-width/2,Screen.height-point.y-height/2,width,height);
            var old=GUI.backgroundColor;GUI.backgroundColor=ally?new Color(.2f,.6f,1):new Color(1,.3f,.2f);
            var style=new GUIStyle(GUI.skin.box) {fontSize=Mathf.Clamp(Mathf.RoundToInt(width/5),9,13),alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset(1,1,1,1)};
            GUI.Box(rect,text,style);GUI.backgroundColor=old;
        }
        void DrawDeployment()
        {
            var assignments=Deployment.Assignments;
            foreach(var slot in Deployment.Slots)
            {
                assignments.TryGetValue(slot.id,out var assigned);
                DrawDeploymentLabel(slot.cell,(slot.id==selectedDeploymentSlot?"▶":"")+slot.id+"\n"+(assigned!=null?assigned.PilotName.Split(' ').Last():"미배치"),true);
            }
            foreach(var unit in Deployment.FixedPlacements) DrawDeploymentLabel(unit.cell,unit.id+"\n"+unit.character.UnitName,unit.character.faction==Faction.Ally);
            uiTheme.BeginPanel(DeploymentRect,"아군 출격 배치");
            GUILayout.Label($"영입 아군 {Deployment.Candidates.Count}명 · 선택 배치 {assignments.Count}/{Deployment.Slots.Count}",uiTheme.Section);
            GUILayout.Label("파란 출격 칸을 선택한 뒤 아군을 지정하세요.",uiTheme.Muted);
            uiTheme.Separator();
            var current=Deployment.Slots.First(s=>s.id==selectedDeploymentSlot);int index=Deployment.Slots.ToList().FindIndex(s=>s.id==current.id);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("◀",GUILayout.Width(34))) SelectDeploymentSlot(Deployment.Slots[(index+Deployment.Slots.Count-1)%Deployment.Slots.Count].id);
            GUILayout.Label($"{current.id} ({current.cell.x}, {current.cell.y})",uiTheme.Section);
            if(GUILayout.Button("▶",GUILayout.Width(34))) SelectDeploymentSlot(Deployment.Slots[(index+1)%Deployment.Slots.Count].id);
            GUILayout.EndHorizontal();
            assignments.TryGetValue(current.id,out var currentAlly);
            GUILayout.Label(currentAlly!=null?"배치: "+currentAlly.PilotName:"배치: 비어 있음",uiTheme.Muted);
            GUILayout.Label("영입된 아군 목록",uiTheme.Heading);
            deploymentScroll=GUILayout.BeginScrollView(deploymentScroll);
            if(Deployment.Candidates.Count==0) GUILayout.Label(IsCampaignActive?"캠페인 에디터에서 초기 영입 아군을 지정하세요.":"맵 에디터에서 단독 테스트용 영입 아군을 지정하세요.",uiTheme.Muted);
            foreach(var ally in Deployment.Candidates)
            {
                bool can=Deployment.CanAssign(selectedDeploymentSlot,ally);
                bool before=GUI.enabled;GUI.enabled=before&&can;
                string restriction=Deployment.SelectionRestriction(ally);
                if(GUILayout.Button($"{ally.PilotName} / {ally.UnitName}\nHP {(ally.mech!=null?ally.mech.MaxHpAtLimit(MaxUpgradeStage):ally.maxHp)} · EN {(ally.mech!=null?ally.mech.MaxEnergyAtLimit(MaxUpgradeStage):ally.maxEnergy)} · 이동 {ally.UnitMovement}"+(restriction!=null?"\n"+restriction:"")))
                    Deployment.Assign(selectedDeploymentSlot,ally);
                GUI.enabled=before;
            }
            GUILayout.EndScrollView();uiTheme.Separator();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("선택 칸 해제")) Deployment.Remove(selectedDeploymentSlot);
            if(GUILayout.Button("자동 배치")) Deployment.AutoFill();
            GUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(error)) GUILayout.Label(error,uiTheme.Muted);
            GUILayout.Label("빈 칸을 남겨도 됩니다. 고정 아군이 없으면 한 명 이상 선택하세요.",uiTheme.Muted);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&Deployment.CanConfirm;
            bool confirm=GUILayout.Button("배치 확정 · 작전 시작",uiTheme.Primary,GUILayout.Height(40));GUI.enabled=enabled;
            bool exit=GUILayout.Button(IsMapTest || IsStageTest?"테스트 종료":"타이틀로 돌아가기");
            TacticalGuiTheme.EndPanel();
            if(confirm) ConfirmDeployment();
            else if(exit) {if(IsStageTest) ExitStageTest();else if(IsMapTest) ExitMapTest();else ReturnToTitle();}
        }
    }
}
