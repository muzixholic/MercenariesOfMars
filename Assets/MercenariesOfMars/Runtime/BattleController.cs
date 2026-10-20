using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MercenariesOfMars
{
    [Serializable]
    public sealed class UnitPlacement
    {
        [InspectorName("유닛 ID")] public string id;
        [InspectorName("전투 유닛 편성")] public CharacterDefinition character;
        [InspectorName("시작 칸")] public Vector2Int cell;
        [InspectorName("적군 자동 레벨")] public bool autoEnemyLevel=true;
        [InspectorName("적군 레벨 보정（미지정 시 기본）")] public EnemyLevelPreset enemyLevelPreset;
        [InspectorName("배치 조건 결합")] public DeploymentConditionMatch conditionMatch;
        [InspectorName("배치 조건（비어 있으면 항상 배치）")] public List<DeploymentCondition> conditions=new();
        public bool MeetsConditions(IReadOnlyList<CharacterDefinition> roster,Func<string,bool> flag,IReadOnlyList<MechDefinition> mechs=null,IReadOnlyList<PilotDefinition> pilots=null)
            =>conditions==null || conditions.Count==0 || (conditionMatch==DeploymentConditionMatch.Any
                ?conditions.Any(c=>c.Matches(roster,flag,mechs,pilots)):conditions.All(c=>c.Matches(roster,flag,mechs,pilots)));
        public UnitPlacement Copy(CharacterDefinition definition=null)=>new() {id=id,character=definition??character,cell=cell,
            autoEnemyLevel=autoEnemyLevel,enemyLevelPreset=enemyLevelPreset,conditionMatch=conditionMatch,
            conditions=conditions?.Select(c=>c==null?null:new DeploymentCondition {kind=c.kind,flag=c.flag,ally=c.ally,mech=c.mech,pilot=c.pilot,expected=c.expected}).ToList()??new()};
    }

    public enum UnitCommandMode { None, Move, Attack, SkillSelection }

    public sealed partial class BattleController : MonoBehaviour
    {
        [InspectorName("격자 보드")] public GridBoard board;
        [InspectorName("전투 카메라")] public Camera battleCamera;
        [InspectorName("성장 설정（미지정 시 공통 설정）")] public PilotProgressionSettings progressionSettings;
        [InspectorName("초기 유닛 배치")] public List<UnitPlacement> placements = new();
        [InspectorName("자동 적군 행동")] public bool automaticEnemy = true;
        [InspectorName("시작 이벤트")] public EventSequence openingEvent;
        [InspectorName("이벤트 실행기")] public EventRunner eventRunner;
        [InspectorName("기본 조작 패널 표시")] public bool showControls = true;
        [InspectorName("공통 게임 UI 글꼴（미지정 시 시스템 글꼴）")] public Font weaponMenuFont;
        [InspectorName("캠페인（미지정 시 기존 단일 전투）")] public CampaignDefinition campaign;
        public CampaignFlow Campaign { get; private set; }
        public int MaxUpgradeStage=>IsCampaignActive?Campaign.MaxUpgradeStage:10;
        public bool IsCampaignActive=>!IsMapTest && !IsTitleScreen && Campaign!=null && Campaign.Phase!=CampaignPhase.Inactive;
        public BattleSession Session { get; private set; }
        public bool RequireConfirmation { get; set; } = true;
        public bool IsTitleScreen { get; private set; } = true;
        public bool IsGameOver => !IsTitleScreen && Session != null && !Session.IsPresenting && Session.Outcome == BattleOutcome.Defeat;
        public bool IsVictory => !IsTitleScreen && Session != null && !Session.IsPresenting && Session.Outcome == BattleOutcome.Victory && !IsCampaignActive;
        Action exitStageTest;
        bool stageTestExitRequested;
        public bool IsStageTest=>exitStageTest!=null;
        public void ConfigureStageTest(Action onExit)
        {exitStageTest=onExit??throw new ArgumentNullException(nameof(onExit));stageTestExitRequested=false;}
        public bool ExitStageTest()
        {
            if(!isActiveAndEnabled || !IsStageTest || stageTestExitRequested) return false;
            stageTestExitRequested=true;exitStageTest();return true;
        }
        Action exitMapTest;
        bool mapTestExitRequested;
        public bool IsMapTest => exitMapTest != null;
        public bool ShowsMapTestExit => IsMapTest && (IsGameOver || IsVictory);
        public void ConfigureMapTest(Action onExit)
        { exitMapTest = onExit ?? throw new ArgumentNullException(nameof(onExit)); mapTestExitRequested = false; }
        public bool ExitMapTest()
        {
            if (!isActiveAndEnabled || !IsMapTest || (!ShowsMapTestExit && !IsDeploying && !HasFormationNotice) || mapTestExitRequested) return false;
            mapTestExitRequested = true; exitMapTest(); return true;
        }
        [InspectorName("전투 연출 사용")] public bool enableCombatPresentation = true;
        [InspectorName("기본 전투 연출")] public CombatPresentationProfile defaultPresentation;
        CombatPresenter presenter;
        DialoguePortraitUI dialoguePortraitUI;
        TacticalGuiTheme uiTheme;
        public CombatPresenter Presenter => presenter;
        public BattleMapCamera MapCamera { get; private set; }
        [Min(.1f), InspectorName("턴 전환 표시 시간")] public float turnBannerSeconds = 1;
        float turnBannerUntil;
        Faction shownFaction;
        int shownRound;
        public bool IsTurnBannerVisible => !IsTitleScreen && Session != null && Session.Outcome == BattleOutcome.Ongoing
            && !Session.IsPresenting && Session.PendingAttack == null && Time.unscaledTime < turnBannerUntil;
        public string TurnBannerText => Session == null ? "" : $"{(Session.ActiveFaction == Faction.Ally ? "아군 턴" : "적군 턴")}\n{Session.Round} 라운드";
        public Rect DialogueRect
        {
            get { float width=Mathf.Min(900*TacticalGuiTheme.LayoutScale,Screen.width-24), height=Mathf.Min(200*TacticalGuiTheme.LayoutScale,Screen.height-24); return new Rect((Screen.width-width)/2,Screen.height-height-12,width,height); }
        }
        public Rect EndScreenRect=>TacticalGuiTheme.Centered(560,390);
        public Rect CombatConfirmationRect=>TacticalGuiTheme.Centered(860,570);
        public bool TryAdvanceDialogue()
        {
            if (IsTitleScreen || Session == null || (Session.Outcome != BattleOutcome.Ongoing && !(IsCampaignActive && Session.Outcome==BattleOutcome.Victory)) || Session.IsPresenting
                || Session.PendingAttack != null || eventRunner == null || !eventRunner.IsRunning || eventRunner.Dialogue == null) return false;
            eventRunner.ContinueDialogue(); return true;
        }
        readonly Dictionary<BattleUnit, Transform> views = new();
        readonly List<Material> materials = new();
        BattleUnit selected;
        BattleUnit inspectedEnemy;
        public BattleUnit InspectedEnemy => inspectedEnemy;
        public BattleUnit DetailsUnit { get; private set; }
        public bool IsContextMenuOpen { get; private set; }
        public bool IsSettingsOpen { get; private set; }
        Vector2 contextMenuPosition;
        public Rect ContextMenuRect
        {
            get
            {
                float width = Mathf.Min(250*TacticalGuiTheme.LayoutScale, Screen.width - 16), height = Mathf.Min(270*TacticalGuiTheme.LayoutScale, Screen.height - 16);
                return new Rect(Mathf.Clamp(contextMenuPosition.x, 8, Screen.width - width - 8),
                    Mathf.Clamp(contextMenuPosition.y, 8, Screen.height - height - 8), width, height);
            }
        }
        Vector2 detailsScroll;
        Rect ModalRect=>DetailsUnit!=null?TacticalGuiTheme.Centered(760,650):IsSettingsOpen?TacticalGuiTheme.Centered(500,390):TacticalGuiTheme.Centered(700,610);
        public Rect IncomingDecisionRect => ModalRect;
        public bool OpenUnitDetails()
        {
            if (!CanInspect || DisplayUnit == null || !DisplayUnit.IsAlive) return false;
            DetailsUnit = DisplayUnit; detailsScroll = Vector2.zero; IsContextMenuOpen = false; IsSettingsOpen = false;
            CommandMode = UnitCommandMode.None; board.ClearMovementHighlights(); return true;
        }
        public bool HandleRightClick(Vector2Int cell, Vector2 screenPoint)
        {
            if (!CanInspect) return false;
            ClearSelection();
            if (!Session.Grid.Contains(cell) || Session.At(cell) != null) return false;
            IsContextMenuOpen = true;
            contextMenuPosition = screenPoint;
            return true;
        }
        public bool OpenSettings()
        {
            if (!CanInspect || !IsContextMenuOpen) return false;
            IsContextMenuOpen = false; IsSettingsOpen = true; return true;
        }
        public bool EndTurnFromMenu()
        {
            if (!CanInspect || !IsContextMenuOpen) return false;
            ClearSelection(); Session.EndTurn(); return true;
        }
        BattleUnit DisplayUnit => inspectedEnemy ?? selected;
        bool CanInspect => isActiveAndEnabled && !IsTitleScreen && Session != null && !Session.IsPresenting
            && Session.PendingAttack == null && Session.Outcome == BattleOutcome.Ongoing && Session.ActiveFaction == Faction.Ally
            && (eventRunner == null || !eventRunner.IsRunning);
        public void ClearSelection()
        {
            selected = null; inspectedEnemy = null; selectedSkill = null; CommandMode = UnitCommandMode.None;
            DetailsUnit = null; IsContextMenuOpen = false; IsSettingsOpen = false;
            error = null; WeaponMenu?.Hide();
            if (board != null) board.ClearMovementHighlights();
        }
        public bool InspectEnemy(string id)
        {
            var unit = Session?.Find(id);
            if (!CanInspect || unit == null || !unit.IsAlive || unit.Faction != Faction.Enemy) return false;
            ClearSelection(); inspectedEnemy = unit; RefreshMovementHighlights(); return true;
        }
        BattleSkill selectedSkill;
        Vector2 scroll;
        public BattleUnit SelectedUnit => selected;
        public BattleSkill SelectedSkill => selectedSkill;
        public UnitCommandMode CommandMode { get; private set; }
        public bool CanShowUnitMenu => isActiveAndEnabled && !IsTitleScreen && Session != null && !Session.IsPresenting
            && Session.PendingAttack == null && Session.Outcome == BattleOutcome.Ongoing
            && selected != null && selected.IsAlive && !selected.HasActed && selected.Faction == Faction.Ally
            && Session.ActiveFaction == Faction.Ally && !(IsCampaignActive && Campaign.UsesFullScreen)
            && (eventRunner == null || !eventRunner.IsRunning);
        public Rect UnitMenuRect
        {
            get
            {
                if (selected == null || battleCamera == null || board == null) return default;
                var point = battleCamera.WorldToScreenPoint(board.CellToWorld(selected.Cell) + board.transform.up * board.cellSize);
                if (point.z <= 0) return default;
                float width = Mathf.Min(230*TacticalGuiTheme.LayoutScale, Screen.width - 16), height = Mathf.Min(290*TacticalGuiTheme.LayoutScale, Screen.height - 16);
                float x = point.x + 30;
                return new Rect(Mathf.Clamp(x, 8, Screen.width - width - 8),
                    Mathf.Clamp(Screen.height - point.y - 30, 8, Screen.height - height - 8), width, height);
            }
        }
        public bool CanUseAttackWeapon(BattleUnit unit,BattleSkill skill)
            => Session!=null && Session.Units.Any(target=>Session.CanUseSkill(unit,target,skill));
        public bool CanOpenAttackMenu=>CanShowUnitMenu && selected.Skills.Any(skill=>CanUseAttackWeapon(selected,skill));
        public bool SetCommandMode(UnitCommandMode mode)
        {
            if (!CanShowUnitMenu) return false;
            if (mode == UnitCommandMode.SkillSelection && !CanOpenAttackMenu) return false;
            if (mode == UnitCommandMode.Move && (selected.HasMoved || Session.MovementOptions(selected).Count <= 1)) return false;
            if (mode == UnitCommandMode.Attack && !selected.Skills.Any(skill => Session.CanSelectSkill(selected, skill))) return false;
            if (!Enum.IsDefined(typeof(UnitCommandMode), mode)) return false;
            if (mode == UnitCommandMode.Attack && (selectedSkill == null || !Session.CanSelectSkill(selected, selectedSkill)))
                selectedSkill = selected.Skills.First(skill => Session.CanSelectSkill(selected, skill));
            CommandMode = mode; error = null; RefreshMovementHighlights();
            if(mode==UnitCommandMode.SkillSelection)
            {
                if(WeaponMenu==null) { WeaponMenu=gameObject.AddComponent<WeaponSelectionView>(); WeaponMenu.Initialize(this); }
                WeaponMenu.Open();
            }
            else WeaponMenu?.Hide();
            return true;
        }
        public WeaponSelectionView WeaponMenu { get; private set; }
        public bool OpenSkillSelection()
        {
            if (!CanShowUnitMenu) return false;
            return SetCommandMode(UnitCommandMode.SkillSelection);
        }
        public Rect SkillSelectionRect=>WeaponSelectionView.CalculateRect(Screen.width,Screen.height);
        public float SelectedHpFraction => DisplayUnit == null ? 0 : Mathf.Clamp01((float)DisplayUnit.Hp / DisplayUnit.MaxHp);
        public float SelectedEnergyFraction => DisplayUnit == null || DisplayUnit.MaxEnergy == 0 ? 0 : Mathf.Clamp01((float)DisplayUnit.Energy / DisplayUnit.MaxEnergy);
        public Rect SelectedStatusRect
        {
            get
            {
                if (DisplayUnit == null || board == null || battleCamera == null) return default;
                var point = battleCamera.WorldToScreenPoint(board.CellToWorld(DisplayUnit.Cell) + board.transform.up * board.cellSize * 1.7f);
                if (point.z <= 0) return default;
                float width = Mathf.Min(160, Screen.width - 16);
                return new Rect(Mathf.Clamp(point.x - width / 2, 8, Screen.width - width - 8),
                    Mathf.Clamp(Screen.height - point.y - 48, 8, Mathf.Max(8, Screen.height - 56)), width, 44);
            }
        }
        void RefreshMovementHighlights()
        {
            if (board == null) return;
            var incoming = Session?.PendingAttack;
            if (incoming != null && incoming.Attacker.Faction == Faction.Enemy)
            {
                CommandMode = UnitCommandMode.None;
                var cells = new List<Vector2Int>();
                for (int y = 0; y < Session.Grid.Height; y++)
                    for (int x = 0; x < Session.Grid.Width; x++)
                        if (incoming.Skill.IsInRange(incoming.Attacker.Cell, new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
                board.SetIncomingAttackHighlights(cells, incoming.Attacker.Cell, incoming.Target.Cell);
                return;
            }
            if (DetailsUnit != null || IsContextMenuOpen || IsSettingsOpen) { board.ClearMovementHighlights(); return; }
            if (inspectedEnemy != null)
            {
                if (CanInspect && inspectedEnemy.IsAlive)
                {
                    board.SetMovementHighlights(Session.Grid.Reachable(inspectedEnemy.Cell, inspectedEnemy.Movement,
                        cell => Session.At(cell) != null).Keys.Where(cell => cell != inspectedEnemy.Cell));
                    return;
                }
                inspectedEnemy = null;
            }
            if (!CanShowUnitMenu || (CommandMode == UnitCommandMode.Move && selected.HasMoved)) CommandMode = UnitCommandMode.None;
            if (CommandMode == UnitCommandMode.Move)
                board.SetMovementHighlights(Session.MovementOptions(selected).Keys.Where(cell => cell != selected.Cell));
            else if (CommandMode == UnitCommandMode.Attack && selectedSkill != null && Session.CanSelectSkill(selected, selectedSkill))
            {
                var cells = new List<Vector2Int>();
                for (int y = 0; y < Session.Grid.Height; y++)
                    for (int x = 0; x < Session.Grid.Width; x++)
                        if (selectedSkill.IsInRange(selected.Cell, new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
                board.SetAttackHighlights(cells);
            }
            else board.ClearMovementHighlights();
        }
        public bool WaitSelectedUnit()
            => CanShowUnitMenu && Session.TryWait(selected);
        public bool HandleCellClick(Vector2Int cell)
        {
            if (IsTitleScreen || Session == null || Session.IsPresenting || Session.PendingAttack != null
                || Session.Outcome != BattleOutcome.Ongoing || Session.ActiveFaction != Faction.Ally
                || (eventRunner != null && eventRunner.IsRunning)) return false;
            var unit = Session.At(cell);
            if (DetailsUnit != null || IsSettingsOpen) { ClearSelection(); return false; }
            IsContextMenuOpen = false;
            if (CanShowUnitMenu && CommandMode == UnitCommandMode.Attack && unit != null && unit.Faction == Faction.Enemy)
                return TrySelectedAttack(unit.Id);
            if (unit == selected && selected != null && (CommandMode == UnitCommandMode.None || CommandMode == UnitCommandMode.SkillSelection))
            { ClearSelection(); return false; }
            if (unit != null && unit.Faction == Faction.Ally && !unit.HasActed) return SelectUnit(unit.Id);
            if (unit != null && unit == inspectedEnemy) return OpenUnitDetails();
            if (unit != null && unit.Faction == Faction.Enemy) return InspectEnemy(unit.Id);
            if (CanShowUnitMenu && CommandMode == UnitCommandMode.Move && unit == null) return Session.TryMove(selected, cell);
            ClearSelection();
            return false;
        }
        string error;
        float nextEnemyAction;
        bool choosingCounterSkill;
        IncomingAttack shownIncoming;
        readonly HashSet<MapEventRegion> firedRegions = new();

        void InitializeBattle()
        {
            try
            {
                if (board == null) throw new InvalidOperationException("격자 보드를 지정하세요.");
                if (board.map != null)
                {
                    var issues = board.map.ValidateMap();
                    if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
                    board.LoadMap(true);
                    openingEvent=IsCampaignActive?Campaign.CurrentStage.intro:board.map.openingEvent;
                    if (eventRunner == null) eventRunner = GetComponent<EventRunner>() ?? gameObject.AddComponent<EventRunner>();
                    if(rosterDeployment==null)
                    {
                        var roster=IsCampaignActive?Campaign.RecruitedAllies:board.map.testRecruitedAllies;
                        // 기존 단독 맵 테스트는 명단이 비어 있으면 고정 아군으로 시험한다. 캠페인은 항상 영입 목록을 요구한다.
                        rosterDeployment=IsCampaignActive?Campaign.ResolveDeployment(board.map,eventRunner.GetFlag):new RosterDeployment(board.map,roster,eventRunner.GetFlag,roster.Count>0);
                        if(rosterDeployment.Notices.Count==0 && IsCampaignActive)Campaign.ApplyDeploymentRoster(rosterDeployment);
                    }
                    if(HasFormationNotice)return;
                    placements=deploymentPlacements??rosterDeployment.Placements.ToList();
                }
                firedRegions.Clear();
                if (battleCamera == null) battleCamera = Camera.main;
                if (battleCamera == null) throw new InvalidOperationException("전투 카메라를 지정하세요.");
                if (MapCamera == null) MapCamera = GetComponent<BattleMapCamera>() ?? gameObject.AddComponent<BattleMapCamera>();
                MapCamera.Configure(battleCamera, board);
                if(board.map!=null && board.map.deploymentSlots.Count>0 && deploymentPlacements==null)
                {
                    Deployment=new AllyDeploymentPlan(board.map,IsCampaignActive?Campaign.RecruitedAllies:board.map.testRecruitedAllies,eventRunner.GetFlag,rosterDeployment,ally=>IsCampaignActive && Campaign.IsDeploymentBlocked(ally));
                    selectedDeploymentSlot=Deployment.Slots[0].id;
                    board.SetMovementHighlights(Deployment.Slots.Select(s=>s.cell));
                    return;
                }
                if (!placements.Any(p => p.character != null && p.character.faction == Faction.Ally)
                    || !placements.Any(p => p.character != null && p.character.faction == Faction.Enemy))
                    throw new InvalidOperationException("아군과 적군을 각각 한 명 이상 배치하세요.");
                Session = new BattleSession(board.CreateGrid(), BattleUnitFactory.Create(placements,progressionSettings,MaxUpgradeStage), requireConfirmation: RequireConfirmation,
                    victoryCell: board.map != null && board.map.victoryCondition == MapVictoryCondition.ReachCell ? board.map.goalCell : null);
                shownRound = 0;
                foreach (var unit in Session.Units)
                {
                    var view = unit.MapIcon != null ? new GameObject() : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    view.name = unit.Name + " (" + unit.Id + ")"; view.transform.SetParent(transform);
                    if (unit.MapIcon != null)
                    {
                        var sprite = view.AddComponent<SpriteRenderer>(); sprite.sprite = unit.MapIcon;
                        view.transform.rotation = battleCamera.transform.rotation;
                        view.transform.localScale = Vector3.one * board.cellSize * 1.3f / Mathf.Max(.001f,unit.MapIcon.bounds.size.x,unit.MapIcon.bounds.size.y);
                    }
                    else
                    {
                        view.transform.localScale = Vector3.one * board.cellSize * .65f;
                        var material = view.GetComponent<Renderer>().material;
                        material.color = unit.Faction == Faction.Ally ? new Color(.2f, .5f, 1) : new Color(1, .25f, .2f);
                        materials.Add(material);
                    }
                    views.Add(unit, view.transform);
                }
                Session.Changed += SyncViews; SyncViews();
                if (openingEvent != null && (eventRunner == null || !eventRunner.Play(openingEvent, Session,IsCampaignActive?Campaign:null)))
                    error = eventRunner == null ? "시작 이벤트의 실행기를 지정하세요." : eventRunner.LastError;
            }
            catch (Exception exception) { error = exception.Message; Session = null; Debug.LogError("전투 초기화 실패: " + error, this); }
        }
        void SyncViews()
        {
            if (Session.Outcome == BattleOutcome.Ongoing && (shownRound != Session.Round || shownFaction != Session.ActiveFaction))
            {
                shownRound = Session.Round; shownFaction = Session.ActiveFaction;
                turnBannerUntil = Time.unscaledTime + Mathf.Max(.1f, turnBannerSeconds);
            }
            if (shownIncoming != Session.PendingAttack)
            { shownIncoming = Session.PendingAttack; choosingCounterSkill = false; scroll = Vector2.zero; confirmationScroll = Vector2.zero; error = null; }
            foreach (var pair in views)
            {
                pair.Value.gameObject.SetActive(pair.Key.IsAlive);
                pair.Value.position = board.CellToWorld(pair.Key.Cell) + board.transform.up * board.cellSize * .65f;
                if (pair.Value.TryGetComponent<SpriteRenderer>(out var icon))
                {
                    icon.color = pair.Key.HasActed ? new Color(.45f,.45f,.45f) : Color.white;
                    pair.Value.rotation = battleCamera.transform.rotation;
                }
                else pair.Value.GetComponent<Renderer>().sharedMaterial.color = pair.Key.HasActed ? new Color(.45f,.45f,.45f)
                    : pair.Key.Faction == Faction.Ally ? new Color(.2f,.5f,1) : new Color(1,.25f,.2f);
            }
            if (selected != null && (!selected.IsAlive || selected.HasActed || selected.Faction != Session.ActiveFaction)) { selected = null; selectedSkill = null; }
            else if (Session.PendingAttack == null && selected != null && selectedSkill != null && !Session.CanSelectSkill(selected, selectedSkill)) selectedSkill = null;
            if (inspectedEnemy != null && !inspectedEnemy.IsAlive) inspectedEnemy = null;
            if (DetailsUnit != null && !DetailsUnit.IsAlive) DetailsUnit = null;
            RefreshMovementHighlights();
            if (!CanInspect) { DetailsUnit = null; IsSettingsOpen = false; IsContextMenuOpen = false; }
        }
        void Update()
        {
            if(HasFormationNotice)return;
            if(IsDeploying) {HandleDeploymentInput();return;}
            if(IsCampaignActive) Campaign.Tick();
            if(IsCampaignActive && Campaign.UsesFullScreen) { MapCamera?.CancelPointer(); return; }
            if (IsTitleScreen || IsGameOver || IsVictory)
            {
                MapCamera?.CancelPointer();
                if (eventRunner != null && eventRunner.IsRunning) eventRunner.StopPlayback();
                return;
            }
            if (board != null && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) board.ToggleGrid();
            if (board != null && board.map != null && Session != null && Session.Outcome == BattleOutcome.Ongoing
                && !Session.IsPresenting && Session.PendingAttack == null && eventRunner != null && !eventRunner.IsRunning)
            {
                var region = board.map.eventRegions.FirstOrDefault(r => r != null && r.sequence != null && !firedRegions.Contains(r)
                    && Session.Units.Any(u => u.IsAlive && u.Faction == Faction.Ally && r.area.Contains(u.Cell)));
                if (region != null) { firedRegions.Add(region); eventRunner.Play(region.sequence, Session,IsCampaignActive?Campaign:null); }
            }
            if (Session?.PendingAttack != null)
            {
                MapCamera?.CancelPointer();
                if (Session.PendingAttack.Attacker.Faction == Faction.Enemy && board.AttackerMarkerCell == null) RefreshMovementHighlights();
                if(IsCounterWeaponSelectionOpen) WeaponMenu?.HandleInput();
                return;
            }
            if (eventRunner != null && eventRunner.Dialogue != null)
            {
                MapCamera?.CancelPointer();
                if (inspectedEnemy != null || DetailsUnit != null || IsContextMenuOpen || IsSettingsOpen) ClearSelection();
                else if (CommandMode != UnitCommandMode.None) { CommandMode = UnitCommandMode.None; board.ClearMovementHighlights(); }
                bool advance = Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame
                    || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    var point = Mouse.current.position.ReadValue();
                    advance |= DialogueRect.Contains(new Vector2(point.x, Screen.height-point.y));
                }
                if (showControls && advance) TryAdvanceDialogue();
                return;
            }
            if (Session == null || Session.IsPresenting || Session.PendingAttack != null || Session.Outcome != BattleOutcome.Ongoing || (eventRunner != null && eventRunner.IsRunning))
            { MapCamera?.CancelPointer(); if (inspectedEnemy != null || DetailsUnit != null || IsContextMenuOpen || IsSettingsOpen) ClearSelection(); else if (CommandMode != UnitCommandMode.None) { CommandMode = UnitCommandMode.None; board.ClearMovementHighlights(); } return; }
            if(showControls && CanShowUnitMenu && CommandMode==UnitCommandMode.SkillSelection)
            { MapCamera?.CancelPointer(); WeaponMenu?.HandleInput(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { MapCamera?.CancelPointer(); ClearSelection(); return; }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                MapCamera?.CancelPointer();
                var point = Mouse.current.position.ReadValue();
                if (board.RayToCell(battleCamera.ScreenPointToRay(point), out var contextCell))
                    HandleRightClick(contextCell, new Vector2(point.x, Screen.height - point.y));
                else ClearSelection();
                return;
            }
            if (automaticEnemy && Session.ActiveFaction == Faction.Enemy)
            {
                MapCamera?.CancelPointer();
                if (IsTurnBannerVisible) return;
                if (Time.unscaledTime >= nextEnemyAction) { EnemyStep(); nextEnemyAction = Time.unscaledTime + .4f; }
                return;
            }
            if (Mouse.current == null) return;
            var screen = Mouse.current.position.ReadValue();
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            if (showControls && (DetailsUnit != null || IsSettingsOpen))
            { MapCamera?.CancelPointer(); if (Mouse.current.leftButton.wasPressedThisFrame && !ModalRect.Contains(guiPoint)) ClearSelection(); return; }
            bool overMenu = showControls && IsContextMenuOpen && ContextMenuRect.Contains(guiPoint);
            if (showControls && CanShowUnitMenu
                && ((CommandMode == UnitCommandMode.None && UnitMenuRect.Contains(new Vector2(screen.x, Screen.height - screen.y)))
                    || (CommandMode == UnitCommandMode.SkillSelection && SkillSelectionRect.Contains(new Vector2(screen.x, Screen.height - screen.y))))) overMenu = true;
            if (overMenu && !(MapCamera != null && MapCamera.IsTrackingPointer)) return;
            if (!overMenu && !IsContextMenuOpen && CommandMode != UnitCommandMode.SkillSelection)
                MapCamera?.Zoom(Mouse.current.scroll.ReadValue().y);
            if (Mouse.current.leftButton.wasPressedThisFrame) { MapCamera?.BeginPointer(screen); return; }
            if (Mouse.current.leftButton.isPressed) { MapCamera?.DragPointer(screen); return; }
            if (!Mouse.current.leftButton.wasReleasedThisFrame || MapCamera == null || !MapCamera.EndPointer(screen)) return;
            if (!board.RayToCell(battleCamera.ScreenPointToRay(screen), out var cell)) { ClearSelection(); return; }
            HandleCellClick(cell);
        }
        public bool SelectUnit(string id)
        {
            var unit = Session?.Find(id);
            if (IsTitleScreen || unit == null || !unit.IsAlive || unit.HasActed || unit.Faction != Session.ActiveFaction
                || Session.IsPresenting || Session.PendingAttack != null || Session.Outcome != BattleOutcome.Ongoing || (automaticEnemy && unit.Faction == Faction.Enemy)
                || (eventRunner != null && eventRunner.IsRunning)) return false;
            inspectedEnemy = null; selected = unit; selectedSkill = unit.Skills.FirstOrDefault(s => Session.CanSelectSkill(unit, s));
            DetailsUnit = null; IsContextMenuOpen = false; IsSettingsOpen = false;
            CommandMode = UnitCommandMode.None; RefreshMovementHighlights();
            error = null; return true;
        }
        public bool SelectSkill(int index)
        {
            if (IsTitleScreen || selected == null || !selected.IsAlive || selected.HasActed || selected.Faction != Session.ActiveFaction
                || Session.IsPresenting || Session.PendingAttack != null || Session.Outcome != BattleOutcome.Ongoing || (eventRunner != null && eventRunner.IsRunning)
                || index < 0 || index >= selected.Skills.Count || !Session.CanSelectSkill(selected, selected.Skills[index])) return false;
            selectedSkill = selected.Skills[index]; CommandMode = UnitCommandMode.Attack; WeaponMenu?.Hide(); RefreshMovementHighlights(); error = null; return true;
        }
        public bool TrySelectedAttack(string targetId)
        {
            if (IsTitleScreen || Session == null || (eventRunner != null && eventRunner.IsRunning)) return false;
            if (selectedSkill == null) { error = "사용할 스킬을 먼저 선택하세요."; return false; }
            if (!Session.TryAttack(selected, Session.Find(targetId), selectedSkill))
            { error = "공격 불가: 이동 후 사용 제한, 사거리, 에너지와 대상 상태를 확인하세요."; return false; }
            error = null; return true;
        }
        public bool IsCounterWeaponSelectionOpen=>choosingCounterSkill && !IsTitleScreen && Session?.PendingAttack!=null
            && !Session.IsAwaitingConfirmation && Session.PendingAttack.Target.Faction==Faction.Ally;
        public bool OpenCounterWeaponSelection()
        {
            var incoming=Session?.PendingAttack;
            if(incoming==null || Session.IsAwaitingConfirmation || incoming.Target.Faction!=Faction.Ally
                || !incoming.Target.Skills.Any(Session.CanCounterattack)) return false;
            choosingCounterSkill=true;
            if(WeaponMenu==null) { WeaponMenu=gameObject.AddComponent<WeaponSelectionView>(); WeaponMenu.Initialize(this); }
            WeaponMenu.Open(true); return true;
        }
        public void CancelCounterWeaponSelection() { choosingCounterSkill=false; WeaponMenu?.Hide(); }
        public bool ChooseReaction(ReactionChoice choice, int skillIndex = -1)
        {
            var incoming = Session?.PendingAttack;
            if (incoming == null) return false;
            BattleSkill skill = skillIndex >= 0 && skillIndex < incoming.Target.Skills.Count ? incoming.Target.Skills[skillIndex] : null;
            if (!Session.ResolveReaction(choice, skill))
            { error = "반응 불가: 반격 스킬의 보유 여부, 사거리와 에너지를 확인하세요."; return false; }
            CancelCounterWeaponSelection(); error = null; return true;
        }
        public bool ConfirmCombat()
        {
            if (Session == null) return false;
            var incoming = Session.PendingAttack;
            if (incoming == null) return false;
            int attackerHp = incoming.Attacker.Hp, targetHp = incoming.Target.Hp;
            bool animate = enableCombatPresentation && isActiveAndEnabled;
            if (!Session.ConfirmCombat(animate)) return false;
            if (animate)
            {
                if (presenter == null) presenter = gameObject.AddComponent<CombatPresenter>();
                presenter.UiFont=weaponMenuFont;
                presenter.Play(Session, battleCamera, attackerHp, targetHp, defaultPresentation);
            }
            choosingCounterSkill = false; error = null; nextEnemyAction = Time.unscaledTime + .4f; return true;
        }
        public bool CancelCombat()
        {
            if (Session == null || !Session.CancelCombat()) return false;
            choosingCounterSkill = false; scroll = Vector2.zero; confirmationScroll = Vector2.zero; error = null; return true;
        }
        public bool RestartBattle()
        {
            if (!isActiveAndEnabled || (!IsGameOver && !IsVictory && !IsTitleScreen
                && !(IsCampaignActive && (Campaign.Phase==CampaignPhase.Error || IsStageTest && Campaign.UsesFullScreen)))) return false;
            if(campaign!=null && !IsMapTest)
            {
                Campaign??=new CampaignFlow(this);
                bool begin=IsTitleScreen || Campaign.CurrentStage==null;
                IsTitleScreen=false;
                return begin?Campaign.Begin(campaign):Campaign.RestartStage();
            }
            return ResetBattleState(null);
        }
        internal bool LoadStageBattle(StageDefinition stage,IReadOnlyDictionary<string,bool> flags)
        {
            if(board==null) return false;
            board.map=stage.map;
            if(eventRunner==null) eventRunner=GetComponent<EventRunner>()??gameObject.AddComponent<EventRunner>();
            return ResetBattleState(flags);
        }
        bool ResetBattleState(IReadOnlyDictionary<string,bool> flags)
        {
            ReleaseDeployment();
            Deployment=null;deploymentPlacements=null;deploymentScroll=Vector2.zero;ClearSelection();
            if (eventRunner != null) { eventRunner.ResetState(); eventRunner.RestoreFlags(flags); }
            if (Session != null) Session.Changed -= SyncViews;
            Session = null;
            foreach (var view in views.Values)
                if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            views.Clear();
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
            selected = null; inspectedEnemy = null; selectedSkill = null; shownIncoming = null; CommandMode = UnitCommandMode.None;
            if (board != null) board.ClearMovementHighlights();
            choosingCounterSkill = false; error = null; scroll = Vector2.zero; confirmationScroll = Vector2.zero;
            nextEnemyAction = Time.unscaledTime + .4f; IsTitleScreen = false;
            if(IsCampaignActive && !eventRunner.RunDeploymentSetup(Campaign.CurrentStage.deploymentSetup,Campaign))
            {error=eventRunner.LastError;return false;}
            InitializeBattle();
            return Session != null || IsDeploying || HasFormationNotice;
        }
        public bool ReturnToTitle()
        {
            if (!isActiveAndEnabled || (!IsGameOver && !IsVictory && !IsDeploying && !HasFormationNotice && !(IsCampaignActive && Campaign.UsesFullScreen))) return false;
            if (eventRunner != null) eventRunner.ResetState();
            ReleaseDeployment();Campaign?.Stop();
            Deployment=null;deploymentPlacements=null;
            selected = null; inspectedEnemy = null; selectedSkill = null; IsTitleScreen = true; CommandMode = UnitCommandMode.None;
            if (board != null) board.ClearMovementHighlights();
            return true;
        }
        void DrawEndScreen()
        {
            TacticalGuiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),TacticalGuiTheme.Navy);
            TacticalGuiTheme.Fill(new Rect(0,Screen.height*.2f,Screen.width,1),new Color(.35f,.43f,.52f));
            TacticalGuiTheme.Fill(new Rect(0,Screen.height*.8f,Screen.width,1),new Color(.35f,.43f,.52f));
            uiTheme.BeginPanel(EndScreenRect,IsTitleScreen?"작전 통제":IsVictory?"작전 결과 · 목표 달성":"작전 결과 · 작전 실패");
            GUILayout.FlexibleSpace();
            var title=new GUIStyle(uiTheme.Heading) {alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(36*TacticalGuiTheme.LayoutScale)};
            title.normal.textColor=TacticalGuiTheme.ForGui(IsGameOver?TacticalGuiTheme.Red:TacticalGuiTheme.Navy);
            GUILayout.Label(IsTitleScreen?"화성의 용병단":IsVictory?"승리":"게임 오버",title);
            GUILayout.Space(12);
            GUILayout.Label(IsTitleScreen?"기체와 파일럿을 지휘하는 전술 작전":IsVictory?"작전 목표를 달성했습니다.":"아군이 모두 전투 불능이 되었습니다.",new GUIStyle(GUI.skin.label) {alignment=TextAnchor.MiddleCenter});
            GUILayout.Space(20); uiTheme.Separator(); GUILayout.Space(12);
            bool exitRequested=false,restart=false,titleRequested=false;
            if(IsStageTest)
            {
                restart=GUILayout.Button("다시 테스트",uiTheme.Primary,GUILayout.Height(44));
                exitRequested=GUILayout.Button("종료",GUILayout.Height(38));
            }
            else
            if(ShowsMapTestExit) exitRequested=GUILayout.Button("종료",uiTheme.Primary,GUILayout.Height(44));
            else
            {
                restart=GUILayout.Button(IsTitleScreen?"전투 시작":"다시 시작",uiTheme.Primary,GUILayout.Height(44));
                if(IsTitleScreen && !IsMapTest && !IsStageTest && GUILayout.Button("불러오기",GUILayout.Height(36))){titleLoadOpen=true;workshopMessage=null;}
                if(!IsTitleScreen) titleRequested=GUILayout.Button("타이틀로 돌아가기",GUILayout.Height(40));
            }
            GUILayout.FlexibleSpace(); TacticalGuiTheme.EndPanel();
            if(exitRequested) {if(IsStageTest) ExitStageTest();else ExitMapTest();} else if(restart) RestartBattle(); else if(titleRequested) ReturnToTitle();
        }
        static string ReactionName(ReactionChoice choice)
            => choice == ReactionChoice.Counterattack ? "반격" : choice == ReactionChoice.Defend ? "방어" : "회피";
        Vector2 confirmationScroll;
        public Rect AttackerForecastRect { get; private set; }
        public Rect DefenderForecastRect { get; private set; }
        void DrawCombatConfirmation()
        {
            var incoming=Session.PendingAttack; var decision=incoming.Decision;
            TacticalGuiTheme.DimScreen(); uiTheme.BeginPanel(CombatConfirmationRect,"전투 확인");
            GUILayout.Label("양측의 무장과 대응을 확인하세요.",uiTheme.Muted);
            confirmationScroll.x=0;
            confirmationScroll=GUILayout.BeginScrollView(confirmationScroll,false,true,GUIStyle.none,GUI.skin.verticalScrollbar);
            // 스크롤바와 간격을 제외한 공간을 정확히 반씩 배정한다.
            float cardWidth=Mathf.Max(1,(CombatConfirmationRect.width-28-GUI.skin.verticalScrollbar.fixedWidth-10-8)/2);
            GUILayout.BeginHorizontal();
            DrawForecastCard(incoming.Attacker,incoming.Skill,incoming.Attacker.Faction==Faction.Ally?"아군 · 공격":"적군 · 공격",
                Session.GetReactionHitChance(decision.Choice),Session.GetReactionDamage(decision.Choice),cardWidth);
            if(Event.current.type==EventType.Repaint) AttackerForecastRect=GUILayoutUtility.GetLastRect();
            GUILayout.Space(10);
            var skill=decision.Choice==ReactionChoice.Counterattack?decision.CounterSkill:null;
            DrawForecastCard(incoming.Target,skill,(incoming.Target.Faction==Faction.Ally?"아군 · ":"적군 · ")+ReactionName(decision.Choice),
                skill!=null?Session.GetHitChance(incoming.Target,incoming.Attacker,skill):0,
                skill!=null?Session.GetDamage(incoming.Target,incoming.Attacker,skill):0,cardWidth);
            if(Event.current.type==EventType.Repaint) DefenderForecastRect=GUILayoutUtility.GetLastRect();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            if(decision.Choice==ReactionChoice.Counterattack) GUILayout.Label("피격 후 생존할 때만 반격합니다.",uiTheme.Muted);
            if(incoming.Target.Faction==Faction.Enemy) GUILayout.Label(decision.Reason,uiTheme.Muted);
            GUILayout.EndScrollView(); GUILayout.Space(8);
            bool confirm=GUILayout.Button("전투 진행",uiTheme.Primary,GUILayout.Height(38));
            bool cancel=GUILayout.Button(incoming.Attacker.Faction==Faction.Ally?"공격 취소":"취소 · 대응 다시 선택",GUILayout.Height(34));
            TacticalGuiTheme.EndPanel();
            if(confirm) ConfirmCombat(); else if(cancel) CancelCombat();
        }
        void DrawForecastCard(BattleUnit unit,BattleSkill skill,string role,float chance,int damage,float width)
        {
            GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(width),GUILayout.MinWidth(width),GUILayout.MaxWidth(width));
            GUILayout.Label(role,uiTheme.Section,GUILayout.MinWidth(0),GUILayout.MaxWidth(width-20)); GUILayout.Label(unit.Name,uiTheme.Heading,GUILayout.MinWidth(0),GUILayout.MaxWidth(width-20));
            GUILayout.Label(unit.PilotName+" · Lv."+unit.Level,uiTheme.Muted,GUILayout.MinWidth(0),GUILayout.MaxWidth(width-20));
            uiTheme.Resource(GUILayoutUtility.GetRect(80,22,GUILayout.ExpandWidth(true)),unit.Hp,unit.MaxHp);
            uiTheme.Resource(GUILayoutUtility.GetRect(80,22,GUILayout.ExpandWidth(true)),unit.Energy,unit.MaxEnergy,true);
            GUILayout.Space(10); uiTheme.Separator(); GUILayout.Space(8);
            if(skill!=null)
            {
                GUILayout.Label(skill.Name,uiTheme.Section,GUILayout.MinWidth(0),GUILayout.MaxWidth(width-20));
                GUILayout.Label($"명중률  {chance:0.##}%\n명중 시 피해  {damage}");
                GUILayout.Label($"소비 EN {skill.EnergyCost} · {skill.AmmoText}\n사거리 {skill.MinRange}~{skill.MaxRange}",uiTheme.Muted,GUILayout.MinWidth(0),GUILayout.MaxWidth(width-20));
            }
            else GUILayout.Label(Session.PendingAttack.Decision.Choice==ReactionChoice.Defend?"받는 피해 절반\n대응 EN 소모 없음":"피격률 절반\n대응 EN 소모 없음");
            GUILayout.EndVertical();
        }
        void DrawReaction()
        {
            var incoming = Session.PendingAttack;
            GUILayout.Label("적의 공격에 대한 대응을 선택하세요.",uiTheme.Section);
            GUILayout.Label($"{incoming.Attacker.Name} → {incoming.Target.Name} · {incoming.Skill.Name}");
            GUILayout.Label($"거리 {incoming.Skill.MinRange}~{incoming.Skill.MaxRange} · 빨간 칸: 적 공격 범위\n노란 테두리: 공격자 · 청록 테두리: 피격 대상");
            GUILayout.Label($"체력 {incoming.Target.Hp}/{incoming.Target.MaxHp} · 에너지 {incoming.Target.Energy}/{incoming.Target.MaxEnergy}");
            GUILayout.Label($"기본 피격률 {Session.GetReactionHitChance(ReactionChoice.Counterattack):0.##}% · 예상 피해 {Session.GetReactionDamage(ReactionChoice.Counterattack)}");
            bool canCounter = incoming.Target.Skills.Any(s => Session.CanCounterattack(s));
            GUI.enabled = canCounter;
            if (GUILayout.Button("반격 · 무장 선택",uiTheme.Primary)) OpenCounterWeaponSelection();
            GUI.enabled = true;
            if (!canCounter) GUILayout.Label("반격 가능한 무장이 없습니다. 사거리·EN·잔탄을 확인하세요.");
            if (GUILayout.Button($"방어 · 피해 절반 ({Session.GetReactionDamage(ReactionChoice.Defend)})"))
            { ChooseReaction(ReactionChoice.Defend); return; }
            if (GUILayout.Button($"회피 · 피격률 절반 ({Session.GetReactionHitChance(ReactionChoice.Evade):0.##}%)"))
            { ChooseReaction(ReactionChoice.Evade); return; }
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
        }
        void EnemyStep()
        {
            var unit = Session.Units.FirstOrDefault(u => u.IsAlive && u.Faction == Faction.Enemy && !u.HasActed);
            if (unit == null) { Session.EndTurn(); return; }
            var enemies = Session.Units.Where(u => u.IsAlive && u.Faction != unit.Faction).ToList();
            var skills = unit.Skills.Where(s => Session.CanSelectSkill(unit, s)).ToList();
            if (skills.Count == 0 || enemies.Count == 0) { Session.TryWait(unit); return; }
            var options = Session.MovementOptions(unit);
            if (!options.ContainsKey(unit.Cell)) options[unit.Cell] = new List<Vector2Int>();
            var plan = (from cell in options.Keys from target in enemies from skill in skills
                let distance = BattleGrid.Distance(cell, target.Cell)
                let gap = Mathf.Max(skill.MinRange - distance, distance - skill.MaxRange, 0)
                let canAttackNow = gap == 0 && (cell == unit.Cell || skill.CanUseAfterMoving)
                let expectedDamage = (long)Session.GetDamage(unit, target, skill) * Session.GetHitChance(unit, target, skill)
                orderby canAttackNow descending, gap, options[cell].Sum(c => (long)Session.Grid.MovementCost(c)), expectedDamage descending, target.Hp, skill.EnergyCost
                select new { cell, target, skill }).First();
            if (plan.cell != unit.Cell) Session.TryMove(unit, plan.cell);
            // 이동 후 사용 불가 스킬만 있으면 접근 후 대기하고 다음 턴을 준비한다.
            if (!Session.TryAttack(unit, plan.target, plan.skill)) Session.TryWait(unit);
        }
        void DrawUnitMenu()
        {
            if (!CanShowUnitMenu || CommandMode != UnitCommandMode.None || UnitMenuRect.width <= 0) return;
            uiTheme.BeginPanel(UnitMenuRect,selected.Name);
            GUI.enabled = !selected.HasMoved && Session.MovementOptions(selected).Count > 1;
            bool move = GUILayout.Button(CommandMode == UnitCommandMode.Move ? "▶ 이동" : "이동", GUILayout.Height(30));
            GUI.enabled = CanOpenAttackMenu;
            bool attack = GUILayout.Button("공격", uiTheme.Primary,GUILayout.Height(30));
            GUI.enabled = true;
            bool wait = GUILayout.Button("대기", GUILayout.Height(30));
            bool details = GUILayout.Button("능력", GUILayout.Height(30));
            GUILayout.Label(CommandMode == UnitCommandMode.Move ? "색칠된 칸을 클릭하세요." : CommandMode == UnitCommandMode.Attack ? "스킬 선택 후 적을 클릭하세요." : "행동을 선택하세요.");
            GUILayout.EndArea();
            if (move) SetCommandMode(UnitCommandMode.Move);
            else if (attack) OpenSkillSelection();
            else if (wait) WaitSelectedUnit();
            else if (details) OpenUnitDetails();
        }
        void DrawSelectedStatus()
        {
            if ((!CanShowUnitMenu && !(CanInspect && inspectedEnemy != null)) || SelectedStatusRect.width <= 0) return;
            var rect = SelectedStatusRect;
            DrawResourceBar(new Rect(rect.x, rect.y, rect.width, 20), SelectedHpFraction, new Color(.85f, .12f, .16f), $"HP {DisplayUnit.Hp}/{DisplayUnit.MaxHp}");
            DrawResourceBar(new Rect(rect.x, rect.y + 24, rect.width, 20), SelectedEnergyFraction, new Color(.12f, .4f, 1), $"에너지 {DisplayUnit.Energy}/{DisplayUnit.MaxEnergy}");
        }
        static void DrawResourceBar(Rect rect, float fraction, Color color, string label)
        {
            TacticalGuiTheme.Fill(rect,new Color(.03f,.04f,.07f));
            TacticalGuiTheme.Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*fraction,rect.height-4),color);
            var text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            text.normal.textColor = Color.white;
            GUI.Label(rect, label, text);
        }
        void DrawUnitDetails()
        {
            var unit = DetailsUnit;
            TacticalGuiTheme.DimScreen();
            uiTheme.BeginPanel(ModalRect,unit.Name+" · 상세 능력");
            detailsScroll = GUILayout.BeginScrollView(detailsScroll);
            GUILayout.Label($"{(unit.Faction == Faction.Ally ? "아군" : "적군")} · Lv.{unit.Level}",uiTheme.Muted);
            GUILayout.Label("기체와 파일럿",uiTheme.Section);
            GUILayout.Label($"기체: {unit.Name}\n파일럿: {unit.PilotName} · 레벨 {unit.Level} · 누적 경험치 {unit.Experience:N0}");
            GUILayout.Label($"기체 데이터: 장갑치 {unit.Armor} · 조준치 {unit.Targeting} · 운동성 {unit.Mobility}\n가격 {unit.Price:N0} · 무장 중량 {unit.WeaponWeight}/{unit.MaxWeaponWeight} · 이동 가능 지형 {MovementEnvironmentText(unit.MovementEnvironments)}");
            uiTheme.Resource(GUILayoutUtility.GetRect(100,22,GUILayout.ExpandWidth(true)),unit.Hp,unit.MaxHp);
            uiTheme.Resource(GUILayoutUtility.GetRect(100,22,GUILayout.ExpandWidth(true)),unit.Energy,unit.MaxEnergy,true);
            GUILayout.Label($"파일럿 공격력 {unit.Attack} · 방어력 {unit.Defense}\n명중력 {unit.Accuracy} · 회피력 {unit.Evasion} · 기체 이동력 {unit.Movement}");
            GUILayout.Label($"현재 칸 ({unit.Cell.x}, {unit.Cell.y}) · {(unit.HasMoved ? "이동 완료" : "이동 전")} · {(unit.HasActed ? "행동 완료" : "행동 전")}");
            if (board.map != null)
            {
                var terrain = board.map.TerrainAt(unit.Cell);
                GUILayout.Label($"지형: {(terrain != null ? terrain.displayName : "평지")} · 이동 비용 {Session.Grid.MovementCost(unit.Cell)}\n지형 보정: 명중 {Session.Grid.AccuracyModifier(unit.Cell):+0;-0;0}%p · 방어 {Session.Grid.DefenseModifier(unit.Cell):+0;-0;0}");
            }
            var definition = placements.FirstOrDefault(placement => placement.id == unit.Id)?.character;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.description)) GUILayout.Label(definition.description, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(12); uiTheme.Separator(); GUILayout.Space(8); GUILayout.Label("무장 목록",uiTheme.Section);
            if (unit.Skills.Count == 0) GUILayout.Label("보유 스킬이 없습니다.");
            foreach (var skill in unit.Skills)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(skill.Name,uiTheme.Section);
                GUILayout.Label($"공격력 {skill.Power} · 거리 {skill.MinRange}~{skill.MaxRange}\n소비 에너지 {skill.EnergyCost} · {skill.AmmoText} · 무게 {skill.Weight} · 명중 보정 {skill.AccuracyModifier:+0;-0;0}%p\n{(skill.CanUseAfterMoving ? "이동 후 사용 가능" : "이동 후 사용 불가")}");
                if (!string.IsNullOrWhiteSpace(skill.Definition.description)) GUILayout.Label(skill.Definition.description, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            bool close = GUILayout.Button("닫기", GUILayout.Height(34));
            GUILayout.EndArea();
            if (close) ClearSelection();
        }
        static string MovementEnvironmentText(MovementEnvironment environments)
        {
            var names=new List<string>();
            if((environments&MovementEnvironment.Ground)!=0) names.Add("지상");
            if((environments&MovementEnvironment.Air)!=0) names.Add("공중");
            if((environments&MovementEnvironment.Space)!=0) names.Add("우주");
            return names.Count==0?"없음":string.Join(" / ",names);
        }
        void DrawContextMenu()
        {
            uiTheme.BeginPanel(ContextMenuRect,"작전 메뉴");
            GUILayout.Label($"{Session.Round} 라운드 · {(Session.ActiveFaction == Faction.Ally ? "아군" : "적군")} 턴",uiTheme.Section);
            bool settings = GUILayout.Button("설정", GUILayout.Height(36));
            bool endTurn = GUILayout.Button("턴 종료",uiTheme.Primary,GUILayout.Height(36));
            bool close = GUILayout.Button("닫기", GUILayout.Height(30));
            GUILayout.EndArea();
            if (settings) OpenSettings(); else if (endTurn) EndTurnFromMenu(); else if (close) ClearSelection();
        }
        void DrawSettings()
        {
            TacticalGuiTheme.DimScreen();
            uiTheme.BeginPanel(ModalRect,"설정");
            GUILayout.Label("표시·전투",uiTheme.Section);
            board.showGrid = GUILayout.Toggle(board.showGrid,"격자 선 표시 · "+(board.showGrid?"켜짐":"꺼짐"),GUI.skin.button,GUILayout.Height(36));
            enableCombatPresentation = GUILayout.Toggle(enableCombatPresentation,"전투 연출 · "+(enableCombatPresentation?"켜짐":"꺼짐"),GUI.skin.button,GUILayout.Height(36));
            GUILayout.Space(18); uiTheme.Separator(); GUILayout.Space(12); GUILayout.Label($"음량 {Mathf.RoundToInt(AudioListener.volume * 100)}%");
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0, 1);
            GUILayout.Label("설정은 현재 실행 중에 적용됩니다.");
            GUILayout.FlexibleSpace();
            bool close = GUILayout.Button("닫기", GUILayout.Height(34));
            GUILayout.EndArea();
            if (close) ClearSelection();
        }
        void DrawIncomingLabel(BattleUnit unit, string role, Color color)
        {
            bool attacker = role == "공격자";
            var point = battleCamera.WorldToScreenPoint(board.CellToWorld(unit.Cell) + board.transform.up * board.cellSize * (attacker ? 1.9f : 0));
            if (point.z <= 0) return;
            float width = Mathf.Min(180, Screen.width - 16);
            var rect = new Rect(Mathf.Clamp(point.x-width/2,8,Screen.width-width-8),
                Mathf.Clamp(Screen.height-point.y+(attacker ? -44 : 8),8,Mathf.Max(8,Screen.height-52)),width,44);
            uiTheme.Marker(rect,role,unit.Name,color);
        }
        void OnGUI()
        {
            uiTheme??=new TacticalGuiTheme();
            using(uiTheme.Use(weaponMenuFont)) DrawGameGUI();
        }
        void DrawGameGUI()
        {
            LoadPreferences();
            if(workshopConfirmation!=null){DrawWorkshopConfirmation();return;}
            if(IsTitleScreen && titleLoadOpen){DrawTitleLoad();return;}
            if(HasFormationNotice){dialoguePortraitUI?.Hide();if(showControls)DrawFormationNotice();return;}
            if(IsDeploying) {dialoguePortraitUI?.Hide();if(showControls) DrawDeployment();return;}
            if(IsCampaignActive && Campaign.UsesFullScreen)
            { dialoguePortraitUI?.Hide(); if(showControls) DrawCampaignScreen(); return; }
            if (IsTitleScreen || IsGameOver || IsVictory || !showControls || Session == null || Session.IsPresenting
                || Session.PendingAttack != null || DetailsUnit != null || IsSettingsOpen || eventRunner == null || eventRunner.Dialogue == null)
                dialoguePortraitUI?.Hide();
            if (IsTitleScreen || IsGameOver || IsVictory) { DrawEndScreen(); return; }
            if (!showControls || Session == null || Session.IsPresenting) return;
            if(IsTurnBannerVisible)
            {
                float width=Mathf.Min(400*TacticalGuiTheme.LayoutScale,Screen.width-16);
                var rect=new Rect((Screen.width-width)/2,Mathf.Max(8,Screen.height*.22f),width,110*TacticalGuiTheme.LayoutScale);
                uiTheme.BeginPanel(rect,Session.ActiveFaction==Faction.Ally?"아군 턴":"적군 턴");
                GUILayout.Label(Session.Round+" 라운드",new GUIStyle(uiTheme.Section) {alignment=TextAnchor.MiddleCenter});
                TacticalGuiTheme.EndPanel();
            }
            if (Session.PendingAttack != null)
            {
                if (Session.PendingAttack.Attacker.Faction == Faction.Enemy && !IsCounterWeaponSelectionOpen && !Session.IsAwaitingConfirmation)
                {
                    DrawIncomingLabel(Session.PendingAttack.Attacker, "공격자", new Color(1,.85f,.1f));
                    DrawIncomingLabel(Session.PendingAttack.Target, "피격 대상", new Color(.15f,1,1));
                }
                if (Session.IsAwaitingConfirmation) DrawCombatConfirmation();
                else if(!IsCounterWeaponSelectionOpen)
                {
                    uiTheme.BeginPanel(IncomingDecisionRect,"전투 대응");
                    scroll = GUILayout.BeginScrollView(scroll);
                    DrawReaction();
                    GUILayout.EndScrollView(); GUILayout.EndArea();
                }
                return;
            }
            if (DetailsUnit != null) { DrawUnitDetails(); return; }
            if (IsSettingsOpen) { DrawSettings(); return; }
            if (eventRunner != null && eventRunner.Dialogue != null)
            {
                var portrait = eventRunner.Portrait;
                float portraitWidth = Mathf.Min(100,DialogueRect.width*.25f);
                if (portrait != null)
                {
                    if (dialoguePortraitUI == null) dialoguePortraitUI = gameObject.AddComponent<DialoguePortraitUI>();
                    dialoguePortraitUI.Show(portrait,new Rect(DialogueRect.x+14,DialogueRect.y+42*TacticalGuiTheme.LayoutScale+14,portraitWidth,Mathf.Max(1,DialogueRect.height-42*TacticalGuiTheme.LayoutScale-48)));
                }
                else dialoguePortraitUI?.Hide();
                uiTheme.BeginPanel(DialogueRect,string.IsNullOrEmpty(eventRunner.Speaker)?"통신":eventRunner.Speaker);
                GUILayout.BeginHorizontal();
                if (portrait != null) GUILayout.Space(portraitWidth+14);
                GUILayout.BeginVertical();
                scroll = GUILayout.BeginScrollView(scroll);
                GUILayout.Label(eventRunner.Dialogue, new GUIStyle(GUI.skin.label) { wordWrap = true });
                GUILayout.EndScrollView();
                GUILayout.EndVertical(); GUILayout.EndHorizontal();
                GUILayout.Label("클릭 · Space / Enter", new GUIStyle(uiTheme.Muted) { alignment = TextAnchor.MiddleRight });
                GUILayout.EndArea();
                return;
            }
            DrawUnitMenu(); if(CommandMode!=UnitCommandMode.SkillSelection) DrawSelectedStatus();
            if (IsContextMenuOpen) DrawContextMenu();
            string message = error ?? (eventRunner != null ? eventRunner.LastError : null);
            if (!string.IsNullOrEmpty(message))
                GUI.Box(new Rect(8, Screen.height - 50, Mathf.Min(650, Screen.width - 16), 42), message);
        }
        void OnDisable()
        {
            dialoguePortraitUI?.Hide(); WeaponMenu?.Hide();
            MapCamera?.CancelPointer();
            ClearSelection();
            if (presenter != null) presenter.Finish();
        }
        void OnDestroy()
        {
            ReleaseDeployment();Campaign?.Stop();
            uiTheme?.Dispose();
            if (Session != null) Session.Changed -= SyncViews;
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
