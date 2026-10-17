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
            GUILayout.Label($"보유 자금  {Campaign.Funds:N0}",uiTheme.Section);
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
            var allies=Campaign.CurrentStage.map.units.Where(p=>p?.character!=null && p.character.faction==Faction.Ally).Select(p=>p.character).Distinct().ToList();
            switch(intermissionPage)
            {
                case "기체 리스트":
                    foreach(var d in allies)
                    {
                        GUILayout.Label(d.UnitName,uiTheme.Heading);
                        GUILayout.Label($"파일럿 {d.PilotName} · HP {(d.mech!=null?d.mech.maxHp:d.maxHp)} · EN {d.UnitMaxEnergy} · 이동력 {d.UnitMovement}");
                        GUILayout.Label("무장  "+string.Join(" / ",d.Weapons.Where(w=>w!=null).Select(w=>w.displayName)),uiTheme.Muted);
                        uiTheme.Separator();
                    }
                    break;
                case "파일럿 리스트":
                    foreach(var d in allies)
                    {
                        GUILayout.Label(d.PilotName,uiTheme.Heading);
                        GUILayout.Label($"Lv.{(d.pilot!=null?d.pilot.level:d.level)} · 탑승 기체 {d.UnitName}");
                        if(d.pilot!=null) GUILayout.Label($"경험치 {d.pilot.experience}",uiTheme.Muted);
                        uiTheme.Separator();
                    }
                    break;
                case "설정":
                    board.showGrid=GUILayout.Toggle(board.showGrid,"격자 표시",GUI.skin.button);
                    enableCombatPresentation=GUILayout.Toggle(enableCombatPresentation,"전투 연출",GUI.skin.button);
                    GUILayout.Label($"음량 {Mathf.RoundToInt(AudioListener.volume*100)}%");
                    AudioListener.volume=GUILayout.HorizontalSlider(AudioListener.volume,0,1);
                    break;
                case "종료":
                    GUILayout.Label("게임을 종료합니다.");
                    if(GUILayout.Button("종료",GUILayout.Height(40)))
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying=false;
#else
                        Application.Quit();
#endif
                    }
                    break;
                case "기체 개조": case "무장 변경": case "파일럿 변경": case "저장": case "불러오기":
                    GUILayout.Label("아직 사용할 수 없는 메뉴입니다.",uiTheme.Muted); break;
                case "기체 상점": case "무장 상점": case "파일럿 상점":
                    GUILayout.Label("등록된 판매 품목이 없습니다.",uiTheme.Muted); break;
                default:
                    GUILayout.Label("다음 작전을 준비하세요.",uiTheme.Heading);
                    GUILayout.Label("기체와 파일럿 정보를 확인하거나 설정을 변경할 수 있습니다. 준비가 끝나면 다음 미션을 선택하세요.");
                    if(Campaign.LastResult!=null) GUILayout.Label($"이전 작전: {Campaign.LastResult.ClearTurns}턴 · 적 격추 {Campaign.LastResult.DefeatedEnemies}기",uiTheme.Muted);
                    break;
            }
        }
    }
}
