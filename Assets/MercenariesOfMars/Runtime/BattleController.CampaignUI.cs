using System.Linq;
using UnityEngine;

namespace MercenariesOfMars
{
    public sealed partial class BattleController
    {
        string intermissionPage="작전 대기";
        Vector2 intermissionScroll;
        void DrawCampaignScreen()
        {
            TacticalGuiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),TacticalGuiTheme.Navy);
            if(IsStageTest) { DrawStageTestResult(); return; }
            if(Campaign.Phase==CampaignPhase.Intermission) { DrawIntermission(); return; }
            uiTheme.BeginPanel(TacticalGuiTheme.Centered(760,490),Campaign.Phase==CampaignPhase.Ending?"엔딩":"진행 경로 확인");
            GUILayout.Space(24);
            GUILayout.Label(Campaign.Phase==CampaignPhase.Ending?Campaign.EndingTitle:"스테이지 설정을 확인하세요.",uiTheme.Heading);
            GUILayout.Space(20);
            GUILayout.Label(Campaign.Phase==CampaignPhase.Ending?Campaign.EndingText:Campaign.Error);
            if(Campaign.LastResult!=null) GUILayout.Label($"클리어 {Campaign.LastResult.ClearTurns}턴 · 적 격추 {Campaign.LastResult.DefeatedEnemies}기",uiTheme.Muted);
            GUILayout.FlexibleSpace();
            bool retry=Campaign.Phase==CampaignPhase.Error && GUILayout.Button("다시 시도",uiTheme.Primary,GUILayout.Height(38));
            bool title=GUILayout.Button("타이틀로 돌아가기",GUILayout.Height(38));
            TacticalGuiTheme.EndPanel();
            if(retry) RestartBattle(); else if(title) ReturnToTitle();
        }
        void DrawStageTestResult()
        {
            uiTheme.BeginPanel(TacticalGuiTheme.Centered(760,490),"스테이지 테스트 결과");
            GUILayout.Label(Campaign.CurrentStage!=null?Campaign.CurrentStage.displayName:"스테이지 설정 오류",uiTheme.Heading);
            GUILayout.Space(16);
            if(Campaign.Phase==CampaignPhase.Error) GUILayout.Label(Campaign.Error);
            else
            {
                GUILayout.Label("전투·아웃트로 완료",uiTheme.Section);
                if(Campaign.LastResult!=null) GUILayout.Label($"클리어 {Campaign.LastResult.ClearTurns}턴 · 적 격추 {Campaign.LastResult.DefeatedEnemies}기");
                GUILayout.Label(Campaign.Phase==CampaignPhase.Ending?"선택된 경로  엔딩 · "+Campaign.EndingTitle:"선택된 다음 스테이지  "+Campaign.NextStage.displayName,uiTheme.Heading);
                if(Campaign.Phase==CampaignPhase.Ending) GUILayout.Label(Campaign.EndingText);
            }
            GUILayout.FlexibleSpace();
            bool retry=GUILayout.Button("다시 테스트",uiTheme.Primary,GUILayout.Height(40));
            bool exit=GUILayout.Button("종료",GUILayout.Height(38));
            TacticalGuiTheme.EndPanel();
            if(retry) RestartBattle();else if(exit) ExitStageTest();
        }
        void DrawIntermission()
        {
            var rect=TacticalGuiTheme.Centered(1100,680);
            uiTheme.BeginPanel(rect,"인터미션 · "+Campaign.CurrentStage.displayName+" 완료");
            GUILayout.Label("다음 미션  "+Campaign.NextStage.displayName,uiTheme.Heading);
            GUILayout.Label($"보유 자금  {Campaign.Funds:N0} · 최대 개조 단계  {Campaign.MaxUpgradeStage}",uiTheme.Section);
            var departure=Campaign.NextDeployment();
            GUILayout.Label(departure.Error==null?$"다음 미션 선택 출격 가능 {departure.SelectableCount}기 · 출격 칸 {departure.SlotCount} / 선택 후보 {departure.AvailableCount} · 고정·강제 {departure.ForcedCount}기":"출격 정보 확인 필요: "+departure.Error,uiTheme.Muted);
            uiTheme.Separator(); GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Min(220*TacticalGuiTheme.LayoutScale,rect.width*.3f)));
            CampaignMenuGroup("기체",new[]{"기체 리스트","기체 개조","무장 변경"});
            CampaignMenuGroup("파일럿",new[]{"파일럿 리스트","파일럿 변경"});
            CampaignMenuGroup("상점",new[]{"기체 상점","무장 상점","파일럿 상점"});
            CampaignMenuGroup("설정",new[]{"저장","불러오기","설정","종료"});
            GUILayout.FlexibleSpace();
            bool depart=GUILayout.Button("다음 미션",uiTheme.Primary,GUILayout.Height(40));
            GUILayout.EndVertical(); GUILayout.Space(16);
            GUILayout.BeginVertical(GUI.skin.box,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            GUILayout.Label(intermissionPage,uiTheme.Section);
            intermissionScroll=GUILayout.BeginScrollView(intermissionScroll);
            DrawIntermissionPage();
            if(!string.IsNullOrEmpty(workshopMessage))GUILayout.Label(workshopMessage,uiTheme.Muted);
            GUILayout.EndScrollView(); GUILayout.EndVertical(); GUILayout.EndHorizontal();
            TacticalGuiTheme.EndPanel();
            if(depart && Campaign.Depart()) {intermissionPage="작전 대기";intermissionScroll=Vector2.zero;}
        }
        void CampaignMenuGroup(string title,string[] items)
        {
            // 기본 화면에서는 그룹을 눌러 펼치므로 작은 화면에서도 다음 미션 버튼을 유지한다.
            bool open=intermissionPage==title || items.Contains(intermissionPage);
            if(GUILayout.Button((open?"▼ ":"▶ ")+title)) intermissionPage=open?"작전 대기":title;
            if(open) foreach(var item in items)
                if(GUILayout.Button(item,intermissionPage==item?uiTheme.Primary:GUI.skin.button)) {intermissionPage=item;intermissionScroll=Vector2.zero;}
        }
        void DrawIntermissionPage()
        {
            switch(intermissionPage)
            {
                case "기체 리스트":case "기체 개조":case "무장 변경":case "파일럿 변경":
                    var mech=DrawWorkshopMechs();if(mech==null)break;
                    if(intermissionPage=="기체 개조")DrawUpgradeMenu(mech);
                    else if(intermissionPage=="무장 변경")DrawWeaponChange(mech);
                    else if(intermissionPage=="파일럿 변경")DrawPilotChange(mech);
                    else{DrawMechDetails(mech);GUILayout.Label("무장  "+string.Join(" / ",mech.weapons.Where(w=>w!=null).Select(w=>w.displayName)));}
                    break;
                case "파일럿 리스트":DrawPilotList();break;
                case "기체 상점":DrawShop(Campaign.ShopMechs);break;
                case "무장 상점":DrawShop(Campaign.ShopWeapons);break;
                case "파일럿 상점":DrawShop(Campaign.ShopPilots);break;
                case "저장":DrawSaveSlots(false);break;
                case "불러오기":DrawSaveSlots(true);break;
                case "설정":DrawPersistentSettings();break;
                case "종료":
                    if(GUILayout.Button("타이틀로 돌아가기",GUILayout.Height(40)))AskWorkshop("저장하지 않은 진행은 사라집니다. 타이틀로 돌아갑니다.",()=>ReturnToTitle());
                    if(GUILayout.Button("게임 종료",GUILayout.Height(40)))AskWorkshop("저장하지 않은 진행은 사라집니다. 게임을 종료합니다.",()=>
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying=false;
#else
                        Application.Quit();
#endif
                    });
                    break;
                default:
                    GUILayout.Label("다음 작전을 준비하세요.",uiTheme.Heading);
                    GUILayout.Label("기체 개조, 장비와 파일럿 배정, 상점 구매를 마친 뒤 다음 미션을 선택하세요.");
                    GUILayout.Label("구매한 기체와 파일럿은 파일럿 변경에서 편성해야 선택 출격할 수 있습니다.",uiTheme.Muted);
                    if(Campaign.LastResult!=null)GUILayout.Label($"이전 작전: {Campaign.LastResult.ClearTurns}턴 · 적 격추 {Campaign.LastResult.DefeatedEnemies}기",uiTheme.Muted);
                    break;
            }
        }
    }
}
