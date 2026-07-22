using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SRPG
{
    // UIManager는 코드 분량이 많아 기능별로 partial 파일로 나눠져 있음:
    // UIManager.cs(필드/초기화/매프레임 갱신), UIManager.Build.cs(범용 UI 생성 헬퍼),
    // UIManager.CoinFlip.cs(코인 던지기/데미지 집계), UIManager.UnitInfo.cs(캐릭터 정보칸),
    // UIManager.Battle.cs(전투 예측창), UIManager.VsScreen.cs(VS 연출/전투 미니정보),
    // UIManager.ActionMenu.cs(액션메뉴/무기선택), UIManager.Screens.cs(홈/스테이지선택/로딩/게임오버/페이즈배너)
    public partial class UIManager : MonoBehaviour
    {
        // CameraController.ViewHeightTiles와 반드시 같은 값이어야 함: 캐릭터 정보창/전투창이 정확히 1칸/2칸 높이를 차지하도록 맞추는 기준
        private const float ViewHeightTiles = 7f;
        // 캔버스 참조 해상도(1280x640, Initialize에서 CanvasScaler에 설정)에서 타일 한 칸이 차지하는 UI 단위 크기.
        // 일러스트처럼 sizeDelta(고정 크기)로 다뤄야 하는 요소를 "타일 몇 칸" 단위로 계산할 때 씀
        private const float TilePixels = 640f / ViewHeightTiles;
        // 상반신 일러스트를 얼마나 확대해서 보여줄지(창 높이의 몇 배 크기로 그릴지). 클수록 더 확대되어 위쪽 일부만 보임
        private const float IllustrationZoom = 2.5f;
        // 확대된 그림을 창 안에서 살짝 왼쪽 위로 밀어서 보여주기 위한 미세 조정값(양수 Y = 위로, 음수 X = 왼쪽으로)
        private static readonly Vector2 IllustrationOffset = new Vector2(-40f, 35f);

        private Font font;
        private Font titleFont; // 메뉴 턴 배너/페이즈 배너/게임오버처럼 크고 임팩트 있어야 하는 제목글씨 전용 폰트
        private Font phaseBannerFont; // 턴 시작 배너(MY TURN/ENEMY TURN) 전용. Cinzel은 한글을 지원하지 않으므로 영어 문구에만 씀
        private Transform canvasRoot;
        private RectTransform canvasRect;
        private GameManager gm;

        private struct UnitInfoTexts
        {
            public RectTransform container;
            public Image background;
            public Image weaponIcon;
            public Text weaponNameText; // 착용 중인 무기 이름 한 줄만 표시
            public Image[] otherWeaponIcons; // 착용하지 않은 나머지 무기를 작은 그림으로 나열(빈 슬롯/착용 무기는 숨김)
            public Text hpLabelText; // 체력 숫자 왼쪽에 작게 붙는 "HP" 표시
            public Text hpText;
            public RectTransform hpBarFill; // 체력 숫자 아래 시인성용 체력바
            // 체력 칸(hpText) 오른쪽부터 코인 칸 앞까지를 균등한 간격으로 채우는 3열x2행 그리드.
            // 속도를 맨 앞(0번, row0/col0)에 두고 그 아래 칸(3번, row1/col0)은 짝이 없어 항상 비움. 나머지 칸: 1=기본공격,2=공격,4=기본수비,5=수비
            public Text[] statTexts;
            // 코인 개수는 숫자/라벨 없이 전투창과 같은 방식(색깔 있는 작은 원, 공격=노란색/수비=파란색)으로만 표시
            public Image[] atkCoinDots;
            public Image[] defCoinDots;
            // 이름/레벨/직업은 본문이 아니라 정보칸 왼쪽 위에 튀어나온 별도의 명찰(NamePlate)에 표시
            public Image nameplateBackground;
            public Text nameplateNameText;
            public Text nameplateJobLevelText;
            // 정보칸 왼쪽 위로 튀어나오는 전신 일러스트(파이어 엠블렘 상태창처럼). 그림이 없는 캐릭터는 숨김
            public Image illustrationImage;
        }

        // 전투창 코인 개수 표시(원 아이콘)에 미리 만들어 두는 슬롯 수. 지금 나오는 유닛 중 코인 최대 개수(2개)보다 넉넉하게 잡음
        private const int MaxCoinDots = 3;

        // 전투창 3분할 경계: 가운데(데미지 예측) 칸을 기존 1/3보다 줄이고, 줄어든 만큼을 좌우 유닛 정보칸에 나눠 줌
        private const float MidColumnMin = 0.4f;
        private const float MidColumnMax = 0.6f;

        // 전투창 전용 유닛 정보 블록: 스탯 없이 모든 텍스트를 가로 한 줄로 쓰고, 체력은 막대로 표시
        private struct BattleUnitInfo
        {
            public RectTransform container;
            public Image background;
            public Text nameText;
            public Text jobLevelText;
            public Image weaponIcon;
            public Text weaponText;
            public Text hpLabelText; // 체력 숫자 왼쪽에 작게 붙는 "HP" 표시
            public Text hpText;
            public Text atkCoinLabel; // "공격코인" 고정 텍스트(노란 원 줄 맨 앞)
            public Image[] atkCoinDots; // 공격 코인 개수만큼 노란 원으로 표시(안 쓰는 슬롯은 숨김)
            public Text defCoinLabel; // "수비코인" 고정 텍스트(파란 원 줄 맨 앞)
            public Image[] defCoinDots; // 수비 코인 개수만큼 파란 원으로 표시(안 쓰는 슬롯은 숨김)
            public RectTransform hpBarDamagePreview; // 깎일 체력 구간(초록→흰색→검정 순으로 깜박임)
            public Image hpBarDamagePreviewImage;    // 위 구간을 깜박이게 만들기 위한 Image 참조
            public RectTransform hpBarFill;          // 녹색: 전투 후에도 남는 체력 구간
            public RectTransform hpBarSplitMarker;   // 깜박이는 구간과 안 깜박이는 구간의 경계를 짚어주는 빨간 막대 줄
            public Text hpBarSplitLabel;              // 전투 후 남는 체력 숫자를 그 경계 위에 작게 표시
        }

        // 캐릭터 정보창: 선택/정찰 중인 유닛 한 명만 보여주는 작은 카드
        private UnitInfoTexts selectedInfo;
        private GameObject bottomBar;

        // 전투 예측칸: 공격 대상을 겨눌 때만 뜨는, 캐릭터 정보창보다 큰 3분할(아군/예측/적) 패널
        private GameObject battlePanel;
        private BattleUnitInfo battleAllyInfo;
        private BattleUnitInfo battleEnemyInfo;

        // 가운데 예측 칸: 화살표/스탯 줄과 별개로 BREAK 표시를 고정 칸에 둬서, BREAK가 뜨든 안 뜨든 화살표/피해/명중 줄 위치가 흔들리지 않게 함
        private Text allyBreakText, allyArrowText, allyStatsText;
        private Text enemyBreakText, enemyArrowText, enemyStatsText;
        private bool allyBreakActive, enemyBreakActive;

        // 공격 확정 후 카메라 줌인이 끝나면 화면이 전환되며 뜨는 1대1 대치 화면: 맵을 완전히 가리고
        // 아군(왼쪽)/적(오른쪽) 스프라이트를 크게 확대해 마주보게 배치함. 코인 굴리기/공격 모션/피격 반응이 전부 이 화면 안에서 진행됨
        private GameObject vsPanel;
        private RectTransform vsAllyRect, vsEnemyRect;
        private Image vsAllyImage, vsEnemyImage;

        // 전투 연출(코인+카메라 줌인) 동안 캐릭터 정보창/전투창 대신 뜨는 최소 정보창: 이름/체력(바)/브레이크 상태만, 세로 1타일
        private GameObject combatMiniBar;
        private Text combatMiniAllyName, combatMiniAllyHpText, combatMiniAllyBreakText;
        private RectTransform combatMiniAllyHpFill;
        private Text combatMiniEnemyName, combatMiniEnemyHpText, combatMiniEnemyBreakText;
        private RectTransform combatMiniEnemyHpFill;
        // 매 프레임 실제 유닛의 currentHP/isBroken을 읽어와 갱신하기 위해 켜져 있는 동안 추적해두는 참조
        private Unit combatMiniAllyUnit, combatMiniEnemyUnit;

        // 메인 화면: 게임을 켜자마자 뜨는 화면. 오른쪽 아래 "전투"/"캐릭터" 버튼이 있음
        private GameObject homeScreenPanel;
        private Button battleMenuButton;
        private Button characterMenuButton;

        // 스테이지 선택 화면: 튜토리얼(0) + GameSession.StageCount개 스테이지를 가로로 늘어놓고 스와이프로 넘겨봄(스테이지가 늘어나도 이 목록만 늘리면 됨)
        private GameObject stageSelectPanel;
        private Button[] stageButtons;
        private float[] stageButtonCenterX; // 각 버튼이 스크롤 콘텐츠 안에서 가로로 어디(중심 x)에 있는지 - 특정 스테이지를 화면 가운데로 스크롤할 때 씀
        private ScrollRect stageScrollRect;
        private Button stageSelectBackButton;

        // 로딩 화면: 스테이지별 전투 로직이 아직 따로 분리되기 전까지 쓰는 임시 화면.
        // 스테이지를 고르면 잠깐 띄웠다가 이미 만들어져 있는 전투로 넘어감
        private GameObject loadingPanel;

        // 캐릭터 확인(레벨업) 화면: 위쪽에 캐릭터 선택 버튼 목록, 아래쪽엔 왼쪽에 큰 일러스트/오른쪽에 이름-레벨-직업-체력-스탯을 세로로 나열
        private const int MaxCharacterSelectButtons = 8;
        private GameObject characterViewPanel;
        private Button characterViewExitButton;
        private Button[] characterSelectButtons;
        private CharacterViewDetail characterViewDetail;
        // 캐릭터 선택 버튼 줄이 보이는 동안(compact)과 캐릭터를 선택해 버튼 줄이 사라진 뒤(expanded) 정보칸 높이를 다르게 주기 위한 컨테이너
        private RectTransform characterViewContentRt;
        // 재화로 체력/기본공격/기본수비를 한번에 강화하는 버튼(캐릭터 레벨업). characterViewCurrentUnit은 지금 화면에 표시 중인 캐릭터를 기억해서 버튼이 그 캐릭터에 적용되게 함
        private Text characterViewCurrencyText;
        private Text characterViewLevelUpInfoText;
        private Button characterViewLevelUpButton;
        private Unit characterViewCurrentUnit;

        // 캐릭터 확인 화면 오른쪽 세로 목록에 쓰는 텍스트/일러스트 참조(무기 정보 없이 이름/레벨/직업/체력/스탯만 다룸)
        private struct CharacterViewDetail
        {
            public RectTransform illustrationBox; // 일러스트를 담는 박스 자체(비율 계산 시 실제 크기를 다시 읽어오기 위해 따로 보관)
            public Image illustrationImage; // 왼쪽에 크게 보여줄 전신 일러스트(비율 유지, 박스 안에 꽉 차게)
            public Text nameText;
            public Text jobLevelText;
            public Text hpLabelText;
            public Text hpText;
            public RectTransform hpBarFill;
            public Text[] statTexts; // 순서대로 기본공격/공격코인/기본수비/수비코인/속도
        }

        // 공격력/마법력 코인을 실제로 던지는 연출. 아군 코인일 때는 화면 어디를 탭해도 그 순간 보이는 면의 값으로 확정되고,
        // 안 누르면(또는 적 코인이면) 시간 초과로 자동 확정됨. 공격/방어 코인이 동시에 각자 앞에서 돌아가야 하므로 코인은 두 개(아군용/적용)를 따로 둠.
        private class CoinDial
        {
            public GameObject go;
            public RectTransform rect;
            public Image image;
            public Text text;
        }
        private CoinDial coinDialAlly, coinDialEnemy;
        // 탭 판정은 코인 자체가 아니라 화면 전체를 덮는 투명 버튼 하나가 전담(항상 플레이어 소유 코인 한쪽만 쓰므로 공유 가능)
        private GameObject coinFlipPanel;
        private Button coinFlipButton;
        private Sprite coinSpriteAttack; // 금색 바탕(공격 코인은 이 위에 흰색/회색을 곱해 금색을 그대로 살림)
        private Sprite coinSpriteDefense; // 흰색 바탕(방어 코인은 이 위에 하늘색을 곱해야 실제로 하늘색으로 보임 - 금색 바탕에 곱하면 초록빛이 섞여버림)
        // 기본 데미지에서 시작해 코인 결과가 나올 때마다 즉석에서 더해지고/빼지는 실시간 데미지 집계 숫자
        private GameObject damageTallyPanel;
        private Text damageTallyText;
        private RectTransform damageTallyRect;
        private int damageTallyValue;
        // 공격/방어 코인이 동시에 돌아가다 보니 결과가 거의 같은 순간에 나올 수 있어, 집계 숫자/임팩트 갱신은 한 번에 하나씩만 재생되도록 순번을 매김
        private bool tallyBusy;
        // 코인이 멈출 때마다 짧게 번쩍이는 임팩트 이펙트(합계가 바뀌는 순간을 강조)
        private GameObject impactFlashPanel;
        private Image impactFlashImage;
        private RectTransform impactFlashRect;
        // 공격 코인은 아군/적 중 공격자 쪽 앞에, 방어 코인은 방어자 쪽 앞에 뜨도록 화면 중앙 기준 좌/우로 살짝 띄움.
        // 캐릭터 스프라이트(중앙에서 ±307px, 260px 크기라 안쪽 끝이 ±177px)와 안 겹치도록 중앙 쪽으로 당겨서 배치(코인 150px 크기 기준 ±90이면 안쪽 끝과 12px 여유)
        private static readonly Vector2 CoinAllyOffset = new Vector2(-90f, 20f);
        private static readonly Vector2 CoinEnemyOffset = new Vector2(90f, 20f);
        // 데미지 집계 숫자는 화면 정중앙 위쪽(코인들보다 위)에 고정
        private static readonly Vector2 DamageTallyCenterOffset = new Vector2(0f, 130f);
        private static readonly Color DamageTallyAddColor = new Color(0.55f, 1f, 0.55f, 1f); // 코인 값이 더해지는 순간(공격 코인)
        private static readonly Color DamageTallySubColor = new Color(1f, 0.5f, 0.5f, 1f); // 코인 값이 빠지는 순간(방어 코인)
        // VS 화면에서 공격 모션(돌진)/피격 반응(밀림)에 쓰는 이동 거리·시간
        private const float VsLungeDistance = 90f;
        private const float VsLungeOutDuration = 0.12f;
        private const float VsLungeBackDuration = 0.12f;
        private const float VsKnockbackDistance = 40f;
        private const float VsHitReactionDuration = 0.18f;

        private Text gameOverText;
        private Text menuTurnText;
        private Text menuLogText;

        private GameObject gameOverPanel;
        private Button gameOverPrimaryButton;
        private Button gameOverHomeButton;
        private GameObject actionMenuPanel;
        private GameObject menuPanel;
        private Button attackButton;
        private Button waitButton;
        private Button cancelButton;
        private Button targetCancelButton;
        private Button confirmAttackButton;
        private Button weaponChangeButton; // 공격 버튼 왼쪽에 나란히 뜸: 사거리 닿는 무기끼리 순환 장착

        private GameObject weaponSelectPanel;
        private readonly Button[] weaponSlotButtons = new Button[Unit.WeaponSlotCount];
        private readonly Text[] weaponSlotLabels = new Text[Unit.WeaponSlotCount];
        private readonly Image[] weaponSlotIcons = new Image[Unit.WeaponSlotCount];
        private Button weaponSelectCancelButton;

        private static readonly Color WeaponSlotNormalColor = new Color(0.22f, 0.28f, 0.4f, 0.95f);
        private static readonly Color WeaponSlotSelectedColor = new Color(0.85f, 0.65f, 0.15f, 0.95f);

        private GameObject phaseBannerPanel;
        private Image phaseBannerBg;
        private Text phaseBannerText;
        private Image screenFlash;
        private Coroutine phaseImpactRoutine;

        public bool IsPlayingPhaseImpact => phaseImpactRoutine != null;

        private readonly List<string> battleHistory = new List<string>();

        private static readonly Color AllyColor = new Color(0.45f, 0.65f, 1f);
        private static readonly Color EnemyColor = new Color(1f, 0.4f, 0.4f);
        private static readonly Color BreakColor = new Color(1f, 0.831f, 0f); // FFD400

        // 캐릭터 정보 창 배경색: 아군은 남색, 적은 빨강색으로 구분
        private static readonly Color AllyInfoBgColor = new Color(0.08f, 0.1f, 0.35f, 0.92f);
        private static readonly Color EnemyInfoBgColor = new Color(0.45f, 0.04f, 0.1f, 0.92f);
        private static readonly Color AllyInfoTextColor = Color.white;
        private static readonly Color EnemyInfoTextColor = Color.white;

        public void Initialize(GameManager gameManager)
        {
            gm = gameManager;
            font = Resources.Load<Font>("Fonts/NanumSquareNeo-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleFont = Resources.Load<Font>("Fonts/BlackHanSans-Regular") ?? font;
            phaseBannerFont = Resources.Load<Font>("Fonts/Cinzel-Black") ?? titleFont;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            // Overlay는 카메라의 3D 깊이 정렬과 무관하게 항상 맨 위에 그려져 캐릭터가 팝업을 뚫고 보이는 문제가 없음
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 화면비 2:1(CameraController.TargetAspect)에 맞춤
            scaler.referenceResolution = new Vector2(1280, 640);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasGo.transform;
            canvasRect = canvasGo.GetComponent<RectTransform>();

            var menuButton = CreateHamburgerButton(canvasRoot, new Vector2(0f, 1f), new Vector2(24, -24), 56);
            menuButton.onClick.AddListener(() => menuPanel.SetActive(!menuPanel.activeSelf));

            menuPanel = CreatePanel("MenuPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 640, 560);
            menuTurnText = CreateRegionText(menuPanel.transform, new Vector2(0f, 0.88f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, 26);
            menuTurnText.font = titleFont;
            var closeButton = CreateButton(menuPanel.transform, "CloseButton", new Vector2(1f, 1f), new Vector2(-14, -14), 48, 48, "X");
            closeButton.onClick.AddListener(() => menuPanel.SetActive(false));
            var logTitle = CreateRegionText(menuPanel.transform, new Vector2(0f, 0.8f), new Vector2(1f, 0.88f), TextAnchor.MiddleLeft, 20);
            logTitle.text = "배틀 이력";
            menuLogText = CreateScrollingLog(menuPanel.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.8f));
            menuPanel.SetActive(false);

            // 세로로 딱 한 칸 높이만 차지하도록 화면 바닥에 붙임 (카메라 세로 시야는 CameraController.ViewHeightTiles = 7칸)
            // 선택/정찰 중인 유닛 한 명만 보여주는 캐릭터 정보창
            // 가로는 화면 폭 전체로 늘림: 폰(세로 화면, 2:1보다 좁은 비율)에서는 카메라가 레터박스로 화면 폭 전체를 채우고,
            // 그 폭이 TargetAspect(2:1) x ViewHeightTiles(7) 기준으로 항상 14칸이라 정보창 가로도 그 폭과 일치함
            bottomBar = CreatePanel("BottomBar", new Vector2(0.5f, 0f), Vector2.zero, 1180, 0);
            var bottomBarRt = bottomBar.GetComponent<RectTransform>();
            bottomBarRt.anchorMin = new Vector2(0f, 0f);
            bottomBarRt.anchorMax = new Vector2(1f, 1f / ViewHeightTiles);
            bottomBarRt.offsetMin = Vector2.zero;
            bottomBarRt.offsetMax = Vector2.zero;
            selectedInfo = CreateUnitInfoBlock(bottomBar.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), AllyInfoBgColor, AllyInfoTextColor);
            bottomBar.SetActive(false);

            // 전투 예측칸: 캐릭터 정보창과 동시에 뜨는 일이 없으므로 같은 화면 바닥 자리에 배치, 세로 크기만 2배. 아군(좌)/피해 예측(중)/적(우) 3분할
            battlePanel = CreatePanel("BattlePanel", new Vector2(0.5f, 0f), Vector2.zero, 1180, 0);
            var battleRt = battlePanel.GetComponent<RectTransform>();
            battleRt.anchorMin = new Vector2(0f, 0f);
            battleRt.anchorMax = new Vector2(1f, 2f / ViewHeightTiles);
            battleRt.offsetMin = Vector2.zero;
            battleRt.offsetMax = Vector2.zero;
            battleAllyInfo = CreateBattleUnitInfoBlock(battlePanel.transform, new Vector2(0f, 0f), new Vector2(MidColumnMin, 1f), AllyInfoBgColor, AllyInfoTextColor);
            CreateBattleMidTexts();
            battleEnemyInfo = CreateBattleUnitInfoBlock(battlePanel.transform, new Vector2(MidColumnMax, 0f), new Vector2(1f, 1f), EnemyInfoBgColor, EnemyInfoTextColor);
            // 체력바 예측 깜빡임/브레이크 깜빡임이 켜있는 동안 매 프레임 갱신되므로 독립 Canvas로 분리
            battlePanel.AddComponent<Canvas>();
            battlePanel.SetActive(false);

            // 공격 확정 후 카메라 줌인이 끝나면 화면이 전환되며 뜨는 1대1 대치 화면: 배경이 맵을 완전히 가리고,
            // 아군(왼쪽)/적(오른쪽) 스프라이트(맵 위 작은 도형을 그대로 크게 확대)를 마주보게 배치함.
            // combatMiniBar/coinDialAlly/coinDialEnemy/damageTallyPanel/impactFlashPanel보다 먼저 만들어 항상 그 밑에 깔리게 함(자식 순서로 위아래 결정)
            vsPanel = CreatePanel("VsPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var vsRt = vsPanel.GetComponent<RectTransform>();
            vsRt.anchorMin = Vector2.zero; vsRt.anchorMax = Vector2.one;
            vsRt.offsetMin = Vector2.zero; vsRt.offsetMax = Vector2.zero;
            vsPanel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 1f);

            var vsAllyGo = new GameObject("AllySprite");
            vsAllyGo.transform.SetParent(vsRt, false);
            vsAllyRect = vsAllyGo.AddComponent<RectTransform>();
            vsAllyRect.anchorMin = new Vector2(0.26f, 0.5f); vsAllyRect.anchorMax = new Vector2(0.26f, 0.5f);
            vsAllyRect.sizeDelta = new Vector2(260f, 260f);
            vsAllyImage = vsAllyGo.AddComponent<Image>();
            vsAllyImage.raycastTarget = false;

            var vsEnemyGo = new GameObject("EnemySprite");
            vsEnemyGo.transform.SetParent(vsRt, false);
            vsEnemyRect = vsEnemyGo.AddComponent<RectTransform>();
            vsEnemyRect.anchorMin = new Vector2(0.74f, 0.5f); vsEnemyRect.anchorMax = new Vector2(0.74f, 0.5f);
            vsEnemyRect.sizeDelta = new Vector2(260f, 260f);
            vsEnemyImage = vsEnemyGo.AddComponent<Image>();
            vsEnemyImage.raycastTarget = false;
            vsEnemyRect.localScale = new Vector3(-1f, 1f, 1f); // 아군 쪽(왼쪽)을 바라보도록 좌우 반전

            // 이 화면 안에서 돌진/피격 위치가 매 프레임 바뀌므로, 독립 Canvas로 분리해 이 변화가
            // 전체 캔버스(다른 모든 UI)의 리빌드를 매 프레임 유발하지 않도록 함
            vsPanel.AddComponent<Canvas>();

            vsPanel.SetActive(false);

            // 전투 연출 중(코인+줌인) 뜨는 최소 정보창: 이름/체력(바)/브레이크만, 세로 1타일. 배경은 투명하게 두고
            // 좌(아군)/우(적) 두 개의 작은 박스만 떠 있게 해서 카메라가 확대해서 보여주는 두 유닛을 가리지 않게 함
            combatMiniBar = CreatePanel("CombatMiniBar", new Vector2(0.5f, 0f), Vector2.zero, 1180, 0);
            var combatMiniRt = combatMiniBar.GetComponent<RectTransform>();
            combatMiniRt.anchorMin = new Vector2(0f, 0f);
            combatMiniRt.anchorMax = new Vector2(1f, 1f / ViewHeightTiles);
            combatMiniRt.offsetMin = Vector2.zero;
            combatMiniRt.offsetMax = Vector2.zero;
            combatMiniBar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            (combatMiniAllyName, combatMiniAllyHpText, combatMiniAllyHpFill, combatMiniAllyBreakText) =
                CreateCombatMiniSide(combatMiniRt, new Vector2(0f, 0f), new Vector2(0.46f, 1f), AllyInfoBgColor, AllyInfoTextColor);
            (combatMiniEnemyName, combatMiniEnemyHpText, combatMiniEnemyHpFill, combatMiniEnemyBreakText) =
                CreateCombatMiniSide(combatMiniRt, new Vector2(0.54f, 0f), new Vector2(1f, 1f), EnemyInfoBgColor, EnemyInfoTextColor);
            // 켜있는 동안 매 프레임 currentHP/isBroken을 다시 읽어와 갱신하므로 독립 Canvas로 분리
            combatMiniBar.AddComponent<Canvas>();
            combatMiniBar.SetActive(false);

            gameOverPanel = CreatePanel("GameOverPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 580, 300);
            gameOverText = CreateRegionText(gameOverPanel.transform, new Vector2(0f, 0.55f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, 44);
            gameOverText.font = titleFont;
            gameOverPrimaryButton = CreateButton(gameOverPanel.transform, "GameOverPrimaryButton", new Vector2(0.5f, 0f), new Vector2(0, 76), 260, 64, "");
            gameOverHomeButton = CreateButton(gameOverPanel.transform, "GameOverHomeButton", new Vector2(0.5f, 0f), new Vector2(0, 16), 260, 64, "홈으로");
            gameOverPanel.SetActive(false);

            // 액션 메뉴/무기 선택창: 화면 우하단, 캐릭터 정보창(bottomBar, 1/ViewHeightTiles 높이) 바로 위에 고정 배치
            // CreatePanel은 anchor와 pivot을 같은 값으로 묶으므로, 분수 anchor에서도 패널 모서리가 정확히 붙도록 pivot을 (1,0)(우하단 모서리)으로 다시 맞춤
            actionMenuPanel = CreatePanel("ActionMenuPanel", new Vector2(1f, 1f / ViewHeightTiles), new Vector2(-16, 8), 240, 280);
            actionMenuPanel.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            attackButton = CreateButton(actionMenuPanel.transform, "AttackButton", new Vector2(0.5f, 1f), new Vector2(0, -24), 200, 60, "공격");
            waitButton = CreateButton(actionMenuPanel.transform, "WaitButton", new Vector2(0.5f, 1f), new Vector2(0, -94), 200, 60, "대기");
            cancelButton = CreateButton(actionMenuPanel.transform, "CancelButton", new Vector2(0.5f, 1f), new Vector2(0, -164), 200, 60, "취소");
            actionMenuPanel.SetActive(false);

            // 무기 선택창: 공격 버튼을 누르면 액션 메뉴 대신 뜨는 무기 슬롯 목록. 슬롯은 5칸까지 있지만 지금은 일반 무기/큰 무기 2칸만 채워짐
            float weaponPanelHeight = 24 + 56 * (Unit.WeaponSlotCount + 1);
            weaponSelectPanel = CreatePanel("WeaponSelectPanel", new Vector2(1f, 1f / ViewHeightTiles), new Vector2(-16, 8), 320, weaponPanelHeight);
            weaponSelectPanel.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            for (int i = 0; i < Unit.WeaponSlotCount; i++)
            {
                var slotButton = CreateButton(weaponSelectPanel.transform, $"WeaponSlot{i}", new Vector2(0.5f, 1f), new Vector2(0, -24 - 56 * i), 280, 50, "-");
                weaponSlotButtons[i] = slotButton;
                weaponSlotLabels[i] = slotButton.GetComponentInChildren<Text>();
                weaponSlotLabels[i].fontSize = 18;

                // 라벨 왼쪽에 무기 그림이 들어갈 자리를 비워둠
                var labelRt = weaponSlotLabels[i].GetComponent<RectTransform>();
                labelRt.offsetMin = new Vector2(46, labelRt.offsetMin.y);

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(slotButton.transform, false);
                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f); iconRt.anchorMax = new Vector2(0f, 0.5f); iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(6, 0); iconRt.sizeDelta = new Vector2(38, 38);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.color = new Color(1f, 1f, 1f, 0f);
                weaponSlotIcons[i] = iconImg;
            }
            weaponSelectCancelButton = CreateButton(weaponSelectPanel.transform, "WeaponSelectCancel", new Vector2(0.5f, 1f), new Vector2(0, -24 - 56 * Unit.WeaponSlotCount), 280, 50, "취소");
            weaponSelectPanel.SetActive(false);

            // 액션메뉴/무기선택창과 같은 우하단 정렬로 배치. 화면 바닥에 캐릭터 정보창(1/ViewHeightTiles) 또는 전투창(2/ViewHeightTiles)이 뜨므로, 더 큰 쪽인 전투창 위에 붙임
            targetCancelButton = CreateButton(canvasRoot, "TargetCancelButton", new Vector2(1f, 2f / ViewHeightTiles), new Vector2(-16, 8), 160, 56, "취소");
            targetCancelButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            targetCancelButton.gameObject.SetActive(false);

            // 취소 버튼 왼쪽에 나란히 배치. 전투 예측창에서 적을 한 번 선택(타겟 지정)한 뒤 확정할 때 씀
            confirmAttackButton = CreateButton(canvasRoot, "ConfirmAttackButton", new Vector2(1f, 2f / ViewHeightTiles), new Vector2(-188, 8), 160, 56, "공격");
            confirmAttackButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            confirmAttackButton.gameObject.SetActive(false);

            // 공격 버튼 왼쪽에 나란히 배치. 지금 타겟까지 사거리가 닿는 무기가 둘 이상일 때만 호출 쪽에서 띄움
            weaponChangeButton = CreateButton(canvasRoot, "WeaponChangeButton", new Vector2(1f, 2f / ViewHeightTiles), new Vector2(-360, 8), 180, 56, "무기변경");
            weaponChangeButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            weaponChangeButton.gameObject.SetActive(false);

            screenFlash = CreateScreenFlash(canvasRoot);
            phaseBannerPanel = CreatePhaseBanner(canvasRoot, out phaseBannerBg, out phaseBannerText);
            phaseBannerPanel.SetActive(false);

            BuildTutorialPanel();

            gm.OnPhaseChanged += (phase, turn) =>
            {
                menuTurnText.text = $"{turn}턴 - {(phase == TurnPhase.Player ? "플레이어 페이즈" : "적 페이즈")}";
                if (phase == TurnPhase.Player || phase == TurnPhase.Enemy)
                    PlayPhaseImpact(phase);
            };
            menuTurnText.text = "1턴 - 플레이어 페이즈";

            // 화면 전체를 덮는 메인 화면. 다른 모든 UI보다 나중에(캔버스의 마지막 자식으로) 만들어야 맨 위에 그려짐
            // 오른쪽 아래 "전투"/"캐릭터" 버튼
            homeScreenPanel = CreatePanel("HomeScreenPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var homeRt = homeScreenPanel.GetComponent<RectTransform>();
            homeRt.anchorMin = Vector2.zero; homeRt.anchorMax = Vector2.one;
            homeRt.offsetMin = Vector2.zero; homeRt.offsetMax = Vector2.zero;
            homeScreenPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 1f);
            battleMenuButton = CreateButton(homeScreenPanel.transform, "BattleMenuButton", new Vector2(1f, 0f), new Vector2(-24f, 24f), 220, 70, "전투");
            characterMenuButton = CreateButton(homeScreenPanel.transform, "CharacterMenuButton", new Vector2(1f, 0f), new Vector2(-260f, 24f), 220, 70, "캐릭터");
            homeScreenPanel.transform.SetAsLastSibling();

            // 스테이지 선택 화면: 지금은 스테이지 1만 있어서 버튼도 하나뿐. 뒤로가기로 메인 화면으로 돌아갈 수 있음
            stageSelectPanel = CreatePanel("StageSelectPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var stageSelectRt = stageSelectPanel.GetComponent<RectTransform>();
            stageSelectRt.anchorMin = Vector2.zero; stageSelectRt.anchorMax = Vector2.one;
            stageSelectRt.offsetMin = Vector2.zero; stageSelectRt.offsetMax = Vector2.zero;
            stageSelectPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 1f);

            var stageSelectTitle = CreateRegionText(stageSelectPanel.transform, new Vector2(0f, 0.72f), new Vector2(1f, 0.88f), TextAnchor.MiddleCenter, 40);
            stageSelectTitle.font = titleFont;
            stageSelectTitle.text = "스테이지 선택";

            // 스테이지 버튼을 가로로 한 줄로 쭉 늘어놓고, 화면 폭을 넘치면 좌우로 스와이프해서 넘겨볼 수 있게 함.
            // 맨 앞(인덱스 0)은 언제나 열려 있는 튜토리얼이고, 이후 인덱스 1~StageCount가 실제 스테이지 1~StageCount에 그대로 대응함
            const float stageButtonWidth = 220f;
            const float stageButtonHeight = 240f;
            const float stageButtonSpacing = 40f;
            int stageButtonTotal = GameSession.StageCount + 1;
            var stageScrollContent = CreateHorizontalScroll(stageSelectPanel.transform, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.70f));
            stageScrollRect = stageScrollContent.GetComponentInParent<ScrollRect>();
            // 맨 앞/맨 뒤 스테이지도 화면 정가운데로 스크롤할 수 있도록, 콘텐츠 양 끝에 뷰포트 절반만큼 여백을 둠
            // (여백이 없으면 첫 스테이지를 "가운데"로 스크롤하려 해도 왼쪽으로 더 갈 곳이 없어 화면 왼쪽에 붙어버림)
            float stageScrollViewportWidth = stageScrollRect.viewport.rect.width;
            float stageButtonEndPadding = Mathf.Max(stageButtonSpacing, stageScrollViewportWidth / 2f - stageButtonWidth / 2f);
            stageScrollContent.sizeDelta = new Vector2(stageButtonEndPadding * 2f + stageButtonTotal * stageButtonWidth + (stageButtonTotal - 1) * stageButtonSpacing, 0f);
            stageButtons = new Button[stageButtonTotal];
            stageButtonCenterX = new float[stageButtonTotal];
            for (int i = 0; i < stageButtonTotal; i++)
            {
                float x = stageButtonEndPadding + i * (stageButtonWidth + stageButtonSpacing);
                string label = i == 0 ? "튜토리얼" : $"스테이지 {i}";
                stageButtons[i] = CreateButton(stageScrollContent, $"Stage{i}Button", new Vector2(0f, 0.5f), new Vector2(x, 0f), stageButtonWidth, stageButtonHeight, label);
                stageButtonCenterX[i] = x + stageButtonWidth / 2f;
            }
            stageSelectBackButton = CreateButton(stageSelectPanel.transform, "StageSelectBackButton", new Vector2(0f, 0f), new Vector2(24f, 24f), 160, 56, "뒤로");
            stageSelectPanel.transform.SetAsLastSibling();

            // 로딩 화면: 스테이지별 전투 로직이 아직 따로 분리되기 전까지 쓰는 임시 화면.
            // 스테이지를 고르면 잠깐 띄웠다가 이미 만들어져 있는 전투로 넘어감
            loadingPanel = CreatePanel("LoadingPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var loadingRt = loadingPanel.GetComponent<RectTransform>();
            loadingRt.anchorMin = Vector2.zero; loadingRt.anchorMax = Vector2.one;
            loadingRt.offsetMin = Vector2.zero; loadingRt.offsetMax = Vector2.zero;
            loadingPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 1f);
            var loadingText = CreateRegionText(loadingPanel.transform, new Vector2(0f, 0.44f), new Vector2(1f, 0.56f), TextAnchor.MiddleCenter, 36);
            loadingText.text = "로딩중...";
            loadingPanel.transform.SetAsLastSibling();

            // 캐릭터 확인(레벨업) 화면: 위쪽엔 캐릭터 선택 버튼(최대 MaxCharacterSelectButtons개, 실제 인원수만큼만 켜서 씀),
            // 아래쪽엔 왼쪽에 큰 일러스트 / 오른쪽에 이름-레벨/직업-체력-스탯(무기 제외)-레벨업 버튼을 세로로 나열
            characterViewPanel = CreatePanel("CharacterViewPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var characterViewRt = characterViewPanel.GetComponent<RectTransform>();
            characterViewRt.anchorMin = Vector2.zero; characterViewRt.anchorMax = Vector2.one;
            characterViewRt.offsetMin = Vector2.zero; characterViewRt.offsetMax = Vector2.zero;
            characterViewPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 1f);

            var characterViewTitle = CreateRegionText(characterViewPanel.transform, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), TextAnchor.MiddleCenter, 36);
            characterViewTitle.font = titleFont;
            characterViewTitle.text = "캐릭터 확인";

            // 보유 재화 표시(우상단, 제목과 같은 줄)
            characterViewCurrencyText = CreateRegionText(characterViewPanel.transform, new Vector2(0.66f, 0.86f), new Vector2(0.98f, 0.98f), TextAnchor.MiddleRight, 24);

            // 오른쪽 위 "나가기": menuPanel의 X 닫기 버튼과 같은 자리(우상단)
            characterViewExitButton = CreateButton(characterViewPanel.transform, "CharacterViewExitButton", new Vector2(1f, 1f), new Vector2(-24f, -24f), 140, 56, "나가기");

            characterSelectButtons = new Button[MaxCharacterSelectButtons];
            float charSlotW = 1f / MaxCharacterSelectButtons;
            for (int i = 0; i < MaxCharacterSelectButtons; i++)
            {
                float x0 = charSlotW * i;
                var btnGo = new GameObject($"CharacterSelect{i}");
                btnGo.transform.SetParent(characterViewPanel.transform, false);
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(x0 + 0.01f, 0.62f); btnRt.anchorMax = new Vector2(x0 + charSlotW - 0.01f, 0.82f);
                btnRt.offsetMin = Vector2.zero; btnRt.offsetMax = Vector2.zero;
                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0.22f, 0.28f, 0.4f, 0.95f);
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnImg;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var labelRt = labelGo.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero; labelRt.offsetMax = Vector2.zero;
                var labelText = labelGo.AddComponent<Text>();
                ConfigureText(labelText, 20, TextAnchor.MiddleCenter);

                characterSelectButtons[i] = btn;
            }

            // 선택 버튼 줄(0.62~0.82) 아래 남는 공간(0.02~0.58)에 왼쪽 일러스트 + 오른쪽 세로 상세정보를 배치
            characterViewDetail = CreateCharacterViewDetail(characterViewRt);

            characterViewPanel.transform.SetAsLastSibling();

            homeScreenPanel.SetActive(false);
            stageSelectPanel.SetActive(false);
            loadingPanel.SetActive(false);
            characterViewPanel.SetActive(false);

            // 동전 던지기 연출: 아군용/적용 두 개(coinDialAlly/coinDialEnemy)를 따로 둬서 공격 코인과 방어 코인이 각자 자기 자리에서 동시에 돌 수 있게 함.
            // 탭 판정(coinFlipPanel)은 화면 전체를 덮는 투명한 버튼 하나로 둬서, 화면 어디를 눌러도 지금 돌아가는 아군 코인이 멈추게 함
            coinFlipPanel = new GameObject("CoinFlipPanel");
            coinFlipPanel.transform.SetParent(canvasRoot, false);
            var catcherRt = coinFlipPanel.AddComponent<RectTransform>();
            catcherRt.anchorMin = Vector2.zero; catcherRt.anchorMax = Vector2.one;
            catcherRt.offsetMin = Vector2.zero; catcherRt.offsetMax = Vector2.zero;
            var catcherImg = coinFlipPanel.AddComponent<Image>();
            catcherImg.color = new Color(0f, 0f, 0f, 0f);
            coinFlipButton = coinFlipPanel.AddComponent<Button>();
            coinFlipButton.targetGraphic = catcherImg;
            coinFlipButton.transition = Selectable.Transition.None; // 투명 버튼이라 눌렀을 때 색이 번쩍이지 않도록 함
            coinFlipPanel.transform.SetAsLastSibling();
            coinFlipPanel.SetActive(false);

            coinSpriteAttack = VisualFactory.CircleSprite(new Color(0.85f, 0.68f, 0.15f));
            coinSpriteDefense = VisualFactory.CircleSprite(Color.white);
            coinDialAlly = CreateCoinDial(CoinAllyOffset);
            coinDialEnemy = CreateCoinDial(CoinEnemyOffset);

            // 기본 데미지에서 시작해 코인 결과마다 오르내리는 실시간 집계 숫자(코인보다 위쪽에 떠 있음)
            damageTallyPanel = new GameObject("DamageTallyPopup");
            damageTallyPanel.transform.SetParent(canvasRoot, false);
            damageTallyRect = damageTallyPanel.AddComponent<RectTransform>();
            damageTallyRect.anchorMin = new Vector2(0.5f, 0.5f); damageTallyRect.anchorMax = new Vector2(0.5f, 0.5f);
            damageTallyRect.sizeDelta = new Vector2(140, 64);
            var damageTallyBg = damageTallyPanel.AddComponent<Image>();
            damageTallyBg.sprite = VisualFactory.CircleSprite(new Color(0f, 0f, 0f, 0.55f));
            damageTallyBg.raycastTarget = false;

            damageTallyText = CreateFillText(damageTallyPanel.transform, 40, TextAnchor.MiddleCenter, 0);
            damageTallyText.fontStyle = FontStyle.Bold;
            damageTallyText.color = Color.white;
            damageTallyText.raycastTarget = false; // 코인과 마찬가지로, 켜져 있으면 이 숫자 위를 탭했을 때 coinFlipPanel까지 안 내려감
            var damageTallyOutline = damageTallyText.gameObject.AddComponent<Outline>();
            damageTallyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            damageTallyOutline.effectDistance = new Vector2(2f, -2f);

            // 팝 애니메이션 동안 스케일/색이 매 프레임 바뀌므로 독립 Canvas로 분리
            damageTallyPanel.AddComponent<Canvas>();
            damageTallyPanel.transform.SetAsLastSibling();
            damageTallyPanel.SetActive(false);

            // 코인이 멈추는 순간 짧게 번쩍이는 임팩트(터지듯 커지면서 사라짐)
            impactFlashPanel = new GameObject("ImpactFlash");
            impactFlashPanel.transform.SetParent(canvasRoot, false);
            impactFlashRect = impactFlashPanel.AddComponent<RectTransform>();
            impactFlashRect.anchorMin = new Vector2(0.5f, 0.5f); impactFlashRect.anchorMax = new Vector2(0.5f, 0.5f);
            impactFlashRect.sizeDelta = new Vector2(180, 180);
            impactFlashImage = impactFlashPanel.AddComponent<Image>();
            impactFlashImage.sprite = VisualFactory.CircleSprite(Color.white);
            impactFlashImage.raycastTarget = false;

            // 번쩍임 애니메이션 동안 스케일/알파가 매 프레임 바뀌므로 독립 Canvas로 분리
            impactFlashPanel.AddComponent<Canvas>();
            impactFlashPanel.transform.SetAsLastSibling();
            impactFlashPanel.SetActive(false);
        }

        private const float DamagePreviewBlinkSpeed = 4f; // 12는 너무 빨라서 다시 느리게 낮춤
        private static readonly Color DamagePreviewGreen = new Color(0.25f, 0.85f, 0.3f); // hpBarFill과 동일한 초록색
        private static readonly Color DamagePreviewBlack = new Color(0.06f, 0.06f, 0.06f);
        private const float BreakBlinkSpeed = 7f;

        private void Update()
        {
            if (battlePanel != null && battlePanel.activeSelf)
            {
                // 초록 -> 흰색 -> 검정 -> 초록 순으로 3단계를 반복 순환
                float cycle = (Time.time * DamagePreviewBlinkSpeed) % 3f;
                Color blinkColor;
                if (cycle < 1f) blinkColor = Color.Lerp(DamagePreviewGreen, Color.white, cycle);
                else if (cycle < 2f) blinkColor = Color.Lerp(Color.white, DamagePreviewBlack, cycle - 1f);
                else blinkColor = Color.Lerp(DamagePreviewBlack, DamagePreviewGreen, cycle - 2f);

                SetDamagePreviewColor(battleAllyInfo.hpBarDamagePreviewImage, blinkColor);
                SetDamagePreviewColor(battleEnemyInfo.hpBarDamagePreviewImage, blinkColor);

                UpdateBreakBlink(allyBreakText, allyBreakActive);
                UpdateBreakBlink(enemyBreakText, enemyBreakActive);
            }

            // 전투 연출 중엔 유닛의 currentHP/isBroken이 실시간으로 바뀌므로 매 프레임 다시 읽어와 반영
            if (combatMiniBar != null && combatMiniBar.activeSelf)
                RefreshCombatMiniInfo();
        }

        private static void SetDamagePreviewColor(Image img, Color color)
        {
            if (img == null) return;
            img.color = color;
        }

        // BREAK 상황일 때만 깜박이고, 아니면 완전히 투명하게 해서 화살표/스탯 칸 위치는 그대로 둔 채 안 보이게 함
        private static void UpdateBreakBlink(Text text, bool active)
        {
            if (!active)
            {
                if (text.color.a != 0f) text.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);
                return;
            }

            float alpha = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.time * BreakBlinkSpeed) + 1f) * 0.5f);
            text.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, alpha);
        }
    }
}
