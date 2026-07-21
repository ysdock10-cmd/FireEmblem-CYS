using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SRPG
{
    public class UIManager : MonoBehaviour
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
            public Text[] weaponTexts; // 무기 슬롯칸마다 하나씩(Unit.WeaponSlotCount개), 4칸으로 나눠 표시
            public Text hpLabelText; // 체력 숫자 왼쪽에 작게 붙는 "HP" 표시
            public Text hpText;
            public RectTransform hpBarFill; // 체력 숫자 아래 시인성용 체력바
            public Text[] statTexts;
            // 이름/레벨/직업은 본문이 아니라 정보칸 왼쪽 위에 튀어나온 별도의 명찰(NamePlate)에 표시
            public Image nameplateBackground;
            public Text nameplateNameText;
            public Text nameplateJobLevelText;
            // 정보칸 왼쪽 위로 튀어나오는 전신 일러스트(파이어 엠블렘 상태창처럼). 그림이 없는 캐릭터는 숨김
            public Image illustrationImage;
        }

        private static readonly string[] StatLabels = { "공격", "공격코인", "속도", "수비", "수비코인" };

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

        // 스테이지 선택 화면(스테이지가 더 늘어나면 이 목록만 늘리면 됨)
        private GameObject stageSelectPanel;
        private Button stage1Button;
        private Button stage2Button;
        private Button stageSelectBackButton;

        // 로딩 화면: 스테이지별 전투 로직이 아직 따로 분리되기 전까지 쓰는 임시 화면.
        // 스테이지를 고르면 잠깐 띄웠다가 이미 만들어져 있는 전투로 넘어감
        private GameObject loadingPanel;

        // 캐릭터 확인 화면: 위쪽에 캐릭터 선택 버튼 목록, 아래쪽엔 전투 중과 똑같은 정보칸을 그대로 재사용해서 보여줌
        private const int MaxCharacterSelectButtons = 8;
        private GameObject characterViewPanel;
        private Button characterViewBackButton;
        private Button characterViewExitButton;
        private Button[] characterSelectButtons;
        private UnitInfoTexts characterViewInfo;

        // 공격력/마법력 코인을 실제로 던지는 연출. 아군 코인일 때는 화면 어디를 탭해도 그 순간 보이는 면의 값으로 확정되고,
        // 안 누르면(또는 적 코인이면) 시간 초과로 자동 확정됨. 공격/방어 코인이 동시에 각자 자기 앞에서 돌아가야 하므로 코인은 두 개(아군용/적용)를 따로 둠.
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

            stage1Button = CreateButton(stageSelectPanel.transform, "Stage1Button", new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), 260, 90, "스테이지 1");
            stage2Button = CreateButton(stageSelectPanel.transform, "Stage2Button", new Vector2(0.5f, 0.5f), new Vector2(150f, 0f), 260, 90, "스테이지 2");
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

            // 캐릭터 확인 화면: 위쪽엔 캐릭터 선택 버튼(최대 MaxCharacterSelectButtons개, 실제 인원수만큼만 켜서 씀),
            // 아래쪽엔 전투 중 화면 하단에 뜨는 것과 완전히 같은 위치/크기(세로 1칸)로 만들어서 CreateUnitInfoBlock을 그대로 재사용
            characterViewPanel = CreatePanel("CharacterViewPanel", new Vector2(0.5f, 0.5f), Vector2.zero, 0, 0);
            var characterViewRt = characterViewPanel.GetComponent<RectTransform>();
            characterViewRt.anchorMin = Vector2.zero; characterViewRt.anchorMax = Vector2.one;
            characterViewRt.offsetMin = Vector2.zero; characterViewRt.offsetMax = Vector2.zero;
            characterViewPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 1f);

            var characterViewTitle = CreateRegionText(characterViewPanel.transform, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), TextAnchor.MiddleCenter, 36);
            characterViewTitle.font = titleFont;
            characterViewTitle.text = "캐릭터 확인";

            characterViewBackButton = CreateButton(characterViewPanel.transform, "CharacterViewBackButton", new Vector2(0f, 0f), new Vector2(24f, 24f), 160, 56, "뒤로");
            // 오른쪽 위 "나가기": menuPanel의 X 닫기 버튼과 같은 자리(우상단)에, 뒤로가기와 별개로 하나 더 둠
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

            var characterViewBarGo = new GameObject("CharacterViewInfoBar");
            characterViewBarGo.transform.SetParent(characterViewPanel.transform, false);
            var characterViewBarRt = characterViewBarGo.AddComponent<RectTransform>();
            characterViewBarRt.anchorMin = new Vector2(0f, 0f);
            characterViewBarRt.anchorMax = new Vector2(1f, 1f / ViewHeightTiles);
            characterViewBarRt.offsetMin = Vector2.zero; characterViewBarRt.offsetMax = Vector2.zero;
            characterViewInfo = CreateUnitInfoBlock(characterViewBarGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), AllyInfoBgColor, AllyInfoTextColor);

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

        // 아군용/적용 코인 다이얼 하나를 만들어 지정한 화면 중앙 기준 오프셋 위치에 고정해둠(평소엔 꺼져 있다가 RunCoinFlip이 켬)
        private CoinDial CreateCoinDial(Vector2 anchoredPos)
        {
            var go = new GameObject("CoinDial");
            go.transform.SetParent(canvasRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 150f);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = CoinHeadsColor;
            img.raycastTarget = false; // 터치 감지는 화면 전체를 덮는 coinFlipPanel/coinFlipButton이 전담

            var text = CreateFillText(go.transform, 54, TextAnchor.MiddleCenter, 0);
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.25f, 0.18f, 0.02f);
            text.raycastTarget = false; // 기본값 true로 두면 코인 위(거의 전체 영역)를 탭했을 때 이 텍스트가 가로채 coinFlipPanel까지 안 내려감

            // 회전하는 동안 스케일/색이 매 프레임 바뀌므로 독립 Canvas로 분리
            go.AddComponent<Canvas>();

            go.transform.SetAsLastSibling();
            go.SetActive(false);

            return new CoinDial { go = go, rect = rt, image = img, text = text };
        }

        private static readonly Color CoinHeadsColor = Color.white; // 앞면: 동전 스프라이트 원래 금색 그대로
        private static readonly Color CoinTailsColor = new Color(0.55f, 0.55f, 0.55f, 1f); // 뒷면: 앞면보다 어둡게 색을 곱함
        private static readonly Color CoinEnemyTint = new Color(0.5f, 0.5f, 0.5f, 1f); // 적이 굴리는 코인은 앞/뒷면 모두 이 값을 곱해 전체적으로 어둡게 함
        // 방어 코인은 공격 코인(금색)과 구분되도록 하늘색으로. 곱하는 틴트 대신 직접 밝은 색을 정해서 숫자(어두운 갈색 글씨)가 잘 보이게 함
        private static readonly Color CoinDefenseHeadsColor = new Color(0.6f, 0.85f, 1f, 1f);
        private static readonly Color CoinDefenseTailsColor = new Color(0.4f, 0.65f, 0.85f, 1f);

        // 전투창에서 코인 개수를 표시하는 작은 원 색: 공격 코인은 노란색, 수비 코인은 파란색(위 동전 색과 계열을 맞춤)
        private static readonly Color CoinDotAtkColor = new Color(0.85f, 0.68f, 0.15f, 1f);
        private static readonly Color CoinDotDefColor = new Color(0.35f, 0.65f, 0.95f, 1f);

        private const float CoinFlipSwitchInterval = 0.08f; // 동전이 반 바퀴 도는 데 걸리는 시간(면이 바뀌는 주기)
        private const float CoinFlipSpinDegPerSec = 180f / CoinFlipSwitchInterval; // 위 주기로부터 계산한 회전 속도
        private const float CoinFlipTimeout = 1.5f; // 이 시간 안에 안 누르면 자동으로 확정됨

        private const float CoinFlipAutoStopDelay = 0.3f; // 자동 정지 시 잠깐 번쩍이다가 멈추는 연출 시간(플레이어 코인보다 훨씬 빠르게 끝남)

        // 코인이 세로축을 중심으로 실제로 도는 것처럼(가로 스케일을 코사인 곡선으로 눌렀다 펴며) 앞/뒷면을 번갈아 보여주다가,
        // 화면을 탭한 순간 보이는 값으로 확정(안 누르면 시간 초과로 자동 확정)
        // autoStop이 true면 플레이어가 손댈 수 없도록 버튼을 비활성화하고, 입력을 기다리지 않고 정해진 시간 뒤 자동으로 멈춤
        // (예: 적의 공격, 또는 내 코인이라도 여러 개 중 두 번째부터는 탭 없이 자동으로 도는 것만 보여줌)
        // dim은 순수히 "이게 적의 코인인지" 색으로만 구분하는 용도라 autoStop과 독립적으로 넘김(내 코인은 자동 정지여도 밝게 유지)
        // isDefense가 true면 방어 코인임을 나타내는 하늘색을 씀(공격 코인은 기본 금색 그대로)
        // isAllySlot: 이 코인이 아군 자리(coinDialAlly)에서 돌지, 적 자리(coinDialEnemy)에서 돌지. 공격/방어 코인이 동시에 각자 자리에서 돌 수 있도록
        // 결과는 공유 필드 대신 onResult 콜백으로 돌려줌(두 코인이 동시에 굴러가는 동안 서로의 결과를 덮어쓰지 않도록)
        public IEnumerator RunCoinFlip(bool isAllySlot, int headsValue, int tailsValue, System.Action<int> onResult, bool autoStop = false, bool dim = false, bool isDefense = false)
        {
            var dial = isAllySlot ? coinDialAlly : coinDialEnemy;
            dial.go.SetActive(true);

            // 아군 코인일 때만 화면 전체를 덮는 투명 버튼을 켜서, 어디를 눌러도 이 코인이 멈추게 함(적 코인은 항상 자동 정지)
            bool tapped = false;
            if (!autoStop)
            {
                coinFlipPanel.SetActive(true);
                coinFlipButton.onClick.RemoveAllListeners();
                coinFlipButton.interactable = true;
                coinFlipButton.onClick.AddListener(() => tapped = true);
            }

            // 금색 바탕 위에 색을 곱하면 하늘색이 초록빛으로 섞여버리므로, 방어 코인은 흰색 바탕으로 바꿔서 순수한 하늘색이 나오게 함
            dial.image.sprite = isDefense ? coinSpriteDefense : coinSpriteAttack;

            Color headsBase = isDefense ? CoinDefenseHeadsColor : CoinHeadsColor;
            Color tailsBase = isDefense ? CoinDefenseTailsColor : CoinTailsColor;
            Color headsColor = dim ? headsBase * CoinEnemyTint : headsBase;
            Color tailsColor = dim ? tailsBase * CoinEnemyTint : tailsBase;

            bool showingHeads = true;
            dial.text.text = "앞";
            dial.image.color = headsColor;
            dial.rect.localScale = Vector3.one;

            float timeout = autoStop ? CoinFlipAutoStopDelay : CoinFlipTimeout;
            float elapsed = 0f;
            float spinAngle = 0f;
            while (elapsed < timeout && !tapped)
            {
                elapsed += Time.deltaTime;
                spinAngle = (spinAngle + CoinFlipSpinDegPerSec * Time.deltaTime) % 360f;

                // 회전 각도의 코사인으로 가로 스케일을 눌렀다 펴서 옆에서 보이는 동전이 도는 모습을 흉내냄
                float cos = Mathf.Cos(spinAngle * Mathf.Deg2Rad);
                dial.rect.localScale = new Vector3(Mathf.Abs(cos), 1f, 1f);

                // 동전이 옆면(스케일이 가장 얇아지는 지점)을 지날 때 보이는 면을 바꿈
                bool nowHeads = cos >= 0f;
                if (nowHeads != showingHeads)
                {
                    showingHeads = nowHeads;
                    // 코인에는 숫자를 표시하지 않고 앞/뒤 구분만 보여줌(실제 수치는 결과 확정 후 코인 굴림 로직에서만 사용)
                    dial.text.text = showingHeads ? "앞" : "뒤";
                    dial.image.color = showingHeads ? headsColor : tailsColor;
                }
                yield return null;
            }

            dial.rect.localScale = Vector3.one;
            int result = showingHeads ? headsValue : tailsValue;

            if (!autoStop)
                coinFlipPanel.SetActive(false);

            // 결과를 잠깐 보여주고 닫음
            yield return new WaitForSeconds(0.3f);
            dial.go.SetActive(false);
            onResult(result);
        }

        // 코인을 굴리기 전, 기본 데미지(무기 위력 - 지형 보정)를 미리 보여줌. 이후 코인 결과마다 이 숫자가 오르내림
        public IEnumerator ShowDamageBase(int baseDamage)
        {
            damageTallyValue = baseDamage;
            damageTallyText.text = damageTallyValue.ToString();
            damageTallyText.color = Color.white;
            damageTallyRect.localScale = Vector3.one;
            damageTallyRect.anchoredPosition = DamageTallyCenterOffset;
            damageTallyPanel.SetActive(true);
            yield return new WaitForSeconds(0.35f);
        }

        // 코인 하나가 멈출 때마다 그 자리(공격 코인은 아군 쪽, 방어 코인은 적 쪽 - isAllySlot으로 구분)에서 번쩍이는 임팩트를 재생한 뒤,
        // 집계 숫자에 결과값을 더하거나(공격) 뺌(방어). delta는 이미 부호가 반영된 값(공격 코인은 +, 방어 코인은 -).
        // 공격/방어 코인이 동시에 끝날 수 있어 tallyBusy로 순서를 매겨, 임팩트/숫자 갱신 애니메이션은 한 번에 하나씩만 재생함
        public IEnumerator ApplyDamageTallyDelta(int delta, bool isAllySlot)
        {
            yield return new WaitUntil(() => !tallyBusy);
            tallyBusy = true;

            yield return PlayImpactFlash(isAllySlot ? CoinAllyOffset : CoinEnemyOffset);

            damageTallyValue += delta;
            damageTallyText.text = damageTallyValue.ToString();
            Color flashColor = delta >= 0 ? DamageTallyAddColor : DamageTallySubColor;

            // 색이 확 튀었다가 흰색으로 가라앉으면서, 동시에 살짝 커졌다 원래 크기로 돌아오는 "팝" 강조
            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                damageTallyRect.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(p * 2f));
                damageTallyText.color = Color.Lerp(flashColor, Color.white, p);
                yield return null;
            }
            damageTallyRect.localScale = Vector3.one;
            damageTallyText.color = Color.white;

            tallyBusy = false;
        }

        public void HideDamageTally() => damageTallyPanel.SetActive(false);

        // 지정한 코인 자리에서 하얀 원이 확 커지며 옅어지는 짧은 번쩍임(코인 결과가 확정된 순간을 강조)
        private IEnumerator PlayImpactFlash(Vector2 offset)
        {
            impactFlashPanel.SetActive(true);
            impactFlashRect.anchoredPosition = offset;
            const float duration = 0.18f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                impactFlashRect.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, p);
                impactFlashImage.color = new Color(1f, 1f, 1f, 1f - p);
                yield return null;
            }
            impactFlashPanel.SetActive(false);
        }

        // 메인 화면을 띄우고, "전투"/"캐릭터" 버튼을 누르면 화면을 숨긴 뒤 각각의 콜백을 실행함
        public void ShowHomeScreen(Action onBattleSelected, Action onCharactersSelected)
        {
            homeScreenPanel.SetActive(true);
            battleMenuButton.onClick.RemoveAllListeners();
            battleMenuButton.onClick.AddListener(() =>
            {
                homeScreenPanel.SetActive(false);
                onBattleSelected();
            });
            characterMenuButton.onClick.RemoveAllListeners();
            characterMenuButton.onClick.AddListener(() =>
            {
                homeScreenPanel.SetActive(false);
                onCharactersSelected();
            });
        }

        // 캐릭터 확인 화면. units 목록으로 선택 버튼을 채우고(기본은 첫 번째 캐릭터를 보여줌),
        // 버튼을 누르면 그 캐릭터 정보를 전투 중과 같은 정보칸에 표시함
        public void ShowCharacterView(IReadOnlyList<Unit> units, Action onBack)
        {
            characterViewPanel.SetActive(true);

            for (int i = 0; i < characterSelectButtons.Length; i++)
            {
                var button = characterSelectButtons[i];
                button.onClick.RemoveAllListeners();
                bool hasUnit = i < units.Count;
                button.gameObject.SetActive(hasUnit);
                if (!hasUnit) continue;

                var unit = units[i];
                button.GetComponentInChildren<Text>().text = unit.unitName;
                button.onClick.AddListener(() => SetUnitInfo(characterViewInfo, unit));
            }

            if (units.Count > 0)
                SetUnitInfo(characterViewInfo, units[0]);

            characterViewBackButton.onClick.RemoveAllListeners();
            characterViewBackButton.onClick.AddListener(() =>
            {
                characterViewPanel.SetActive(false);
                onBack();
            });
            characterViewExitButton.onClick.RemoveAllListeners();
            characterViewExitButton.onClick.AddListener(() =>
            {
                characterViewPanel.SetActive(false);
                onBack();
            });
        }

        // 스테이지 선택 화면(스테이지가 더 늘어나면 이 목록만 늘리면 됨)
        // stage2Unlocked가 false면 스테이지 1을 아직 못 깬 상태라 스테이지 2 버튼을 눌러도 반응하지 않게 잠금
        public void ShowStageSelect(Action onStage1, Action onStage2, Action onBack, bool stage2Unlocked)
        {
            stageSelectPanel.SetActive(true);
            stage1Button.onClick.RemoveAllListeners();
            stage1Button.onClick.AddListener(() =>
            {
                stageSelectPanel.SetActive(false);
                onStage1();
            });

            stage2Button.interactable = stage2Unlocked;
            stage2Button.GetComponentInChildren<Text>().text = stage2Unlocked ? "스테이지 2" : "스테이지 2\n(잠김)";
            stage2Button.onClick.RemoveAllListeners();
            if (stage2Unlocked)
            {
                stage2Button.onClick.AddListener(() =>
                {
                    stageSelectPanel.SetActive(false);
                    onStage2();
                });
            }

            stageSelectBackButton.onClick.RemoveAllListeners();
            stageSelectBackButton.onClick.AddListener(() =>
            {
                stageSelectPanel.SetActive(false);
                onBack();
            });
        }

        // 스테이지별 전투 로직이 아직 따로 분리되기 전까지 쓰는 임시 로딩 화면. 잠깐 보여준 뒤 onLoaded를 실행함
        private const float LoadingScreenDuration = 1f;
        public void ShowLoading(Action onLoaded)
        {
            loadingPanel.SetActive(true);
            StartCoroutine(LoadingRoutine(onLoaded));
        }

        private IEnumerator LoadingRoutine(Action onLoaded)
        {
            yield return new WaitForSeconds(LoadingScreenDuration);
            loadingPanel.SetActive(false);
            onLoaded();
        }

        public void AddBattleLog(List<string> entries)
        {
            string block = $"[{gm.turnNumber}턴] " + string.Join(" / ", entries);
            battleHistory.Insert(0, block);
            if (battleHistory.Count > 50) battleHistory.RemoveAt(battleHistory.Count - 1);
            menuLogText.text = string.Join("\n\n", battleHistory);
        }

        public void ShowSelectedUnit(Unit u, Action onCancel = null)
        {
            bottomBar.SetActive(true);
            SetUnitInfo(selectedInfo, u);

            if (onCancel != null)
            {
                targetCancelButton.onClick.RemoveAllListeners();
                targetCancelButton.onClick.AddListener(() => onCancel());
                targetCancelButton.gameObject.SetActive(true);
            }
        }

        public void HideSelectedUnit()
        {
            bottomBar.SetActive(false);
            targetCancelButton.gameObject.SetActive(false);
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

        // 전투 예측칸을 띄움
        public void ShowForecast(Unit attacker, Unit defender, CombatForecast f)
        {
            // 전투창이 뜨는 동안엔 같은 자리를 다루는 캐릭터 정보창은 숨김
            bottomBar.SetActive(false);
            battlePanel.SetActive(true);

            int allyIncomingDamage = f.defenderCanCounter ? f.defenderDamage : 0;
            int enemyIncomingDamage = f.attackerDamage;

            SetBattleUnitInfo(battleAllyInfo, attacker, allyIncomingDamage);
            UpdateBattleMidTexts(f);
            SetBattleUnitInfo(battleEnemyInfo, defender, enemyIncomingDamage);
        }

        public void ClearForecast()
        {
            battlePanel.SetActive(false);
            bottomBar.SetActive(true);
            confirmAttackButton.gameObject.SetActive(false);
            weaponChangeButton.gameObject.SetActive(false);
        }

        // 적을 한 번 선택(타겟 지정)해서 전투 예측창이 떠 있는 동안, 취소 옆에 "공격" 버튼을 노출해 바로 확정할 수 있게 함
        public void ShowAttackConfirmButton(Action onAttack)
        {
            confirmAttackButton.onClick.RemoveAllListeners();
            confirmAttackButton.onClick.AddListener(() => onAttack());
            confirmAttackButton.gameObject.SetActive(true);
        }

        // 공격 버튼 왼쪽에 같이 뜸: 누르면 지금 타겟까지 사거리가 닿는 무기들끼리 순환 장착함(무기가 하나뿐이면 호출한 쪽에서 아예 안 띄움)
        public void ShowWeaponChangeButton(Action onWeaponChange)
        {
            weaponChangeButton.onClick.RemoveAllListeners();
            weaponChangeButton.onClick.AddListener(() => onWeaponChange());
            weaponChangeButton.gameObject.SetActive(true);
        }

        // 전투 연출(코인 굴리기 + 카메라 줌인) 동안엔 예측 패널도, 캐릭터 정보창(bottomBar)도 화면에서 완전히 비워서
        // 카메라가 비추는 두 유닛에만 시선이 가게 함. ClearForecast는 bottomBar를 다시 켜버리므로 이 경우엔 쓰면 안 됨
        public void HideAllBattleUI()
        {
            battlePanel.SetActive(false);
            bottomBar.SetActive(false);
            confirmAttackButton.gameObject.SetActive(false);
            weaponChangeButton.gameObject.SetActive(false);
        }

        // 본문은 왼쪽부터: 무기 - 체력(가운데, 크게) - 스탯 두 줄. 이름/레벨/직업은 본문이 아니라 왼쪽 위 명찰(NamePlate)에 표시
        private UnitInfoTexts CreateUnitInfoBlock(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            var containerGo = new GameObject("UnitInfo");
            containerGo.transform.SetParent(parent, false);
            var rt = containerGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(rt, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            var info = new UnitInfoTexts
            {
                container = rt,
                background = bgImg,
                // 이름/직업/레벨이 명찰로 빠지며 비워진 왼쪽 0.24만큼 무기 그림/무기 정보/체력 칸을 전부 왼쪽으로 옮김
                // 무기 그림 크기: 44 -> 50으로 아주 조금만 키움 (더 키우면 옆 칸과 부딪힘)
                weaponIcon = CreateWeaponIconPlaceholder(rt, new Vector2(0.02f, 0.5f), 50f),
                // 무기 슬롯(weaponSlots, 최대 Unit.WeaponSlotCount개)을 가로 2 x 세로 2칸으로 나눠 표시. 현재 장착 무기는 ▶ 표시로 구분
                weaponTexts = CreateStatGrid(rt, new Vector2(0.05f, 0f), new Vector2(0.28f, 1f), 2, Unit.WeaponSlotCount / 2, 20, 3f, 3f),
                // 체력바를 더 얇게(0.14~0.42 -> 0.08~0.20) 줄이고, 그만큼 확보된 공간을 체력 숫자 칸에 더 줘서 글씨를 키움
                hpLabelText = CreateRegionText(rt, new Vector2(0.28f, 0.28f), new Vector2(0.315f, 1f), TextAnchor.MiddleLeft, 18, 2f, 2f),
                hpText = CreateRegionText(rt, new Vector2(0.315f, 0.28f), new Vector2(0.46f, 1f), TextAnchor.MiddleLeft, 38, 2f, 2f),
                // 무기/체력 칸이 왼쪽으로 옮겨지며 남는 폭을 전부 스탯칸에 더해 가로로 더 길게(0.30 -> 0.54)
                statTexts = CreateStatGrid(rt, new Vector2(0.46f, 0f), new Vector2(1f, 1f), 3, 2, 25),
            };
            info.hpBarFill = CreateSimpleHpBar(rt, new Vector2(0.28f, 0.08f), new Vector2(0.46f, 0.20f));
            info.hpText.fontStyle = FontStyle.Bold;
            info.hpLabelText.text = "HP";

            foreach (var t in info.weaponTexts) t.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            foreach (var t in info.statTexts) t.color = textColor;

            // 상반신 일러스트: 정보칸(parent, 세로 1칸/가로 14칸) 왼쪽 위로 가로 4칸 x 세로 4칸 크기의 창을 내고,
            // RectMask2D로 창 밖을 잘라낸 뒤 그 안의 그림을 확대해서 위쪽(상반신)만 보이게 함(전신 대신).
            // 가로 폭이 4칸을 넘지 않도록 제한(캐릭터가 왼쪽 칸을 벗어나 옆 요소와 겹치지 않게)
            var illustrationMaskGo = new GameObject("IllustrationMask");
            illustrationMaskGo.transform.SetParent(parent, false);
            var illustrationMaskRt = illustrationMaskGo.AddComponent<RectTransform>();
            illustrationMaskRt.anchorMin = new Vector2(0f, 1f);
            illustrationMaskRt.anchorMax = new Vector2(4f / 14f, 5f);
            illustrationMaskRt.offsetMin = Vector2.zero; illustrationMaskRt.offsetMax = Vector2.zero;
            illustrationMaskGo.AddComponent<RectMask2D>();

            // 그림 크기는 실제 스프라이트 비율에 맞춰 SetUnitInfo에서 다시 계산함(캐릭터마다 원본 그림 비율이 다를 수 있어서).
            // 위쪽 가운데를 기준점으로 잡아 창 위쪽에 딱 붙이고, 아래로/양옆으로 넘치는 부분만 마스크가 잘라냄
            var illustrationGo = new GameObject("Illustration");
            illustrationGo.transform.SetParent(illustrationMaskRt, false);
            var illustrationRt = illustrationGo.AddComponent<RectTransform>();
            illustrationRt.anchorMin = new Vector2(0.5f, 1f); illustrationRt.anchorMax = new Vector2(0.5f, 1f); illustrationRt.pivot = new Vector2(0.5f, 1f);
            illustrationRt.anchoredPosition = IllustrationOffset;
            info.illustrationImage = illustrationGo.AddComponent<Image>();
            info.illustrationImage.raycastTarget = false;

            // 이름/레벨/직업 명찰: 일러스트보다 나중에(더 위쪽 형제로) 만들어서 겹치면 명찰이 그림을 덮도록 함.
            // 정보칸 왼쪽 위, 가로 4칸 x 세로 0.5칸, 세로 1.0(정보칸 윗변)~1.5, 가로 0~4/14
            var nameplateGo = new GameObject("NamePlate");
            nameplateGo.transform.SetParent(parent, false);
            var nameplateRt = nameplateGo.AddComponent<RectTransform>();
            nameplateRt.anchorMin = new Vector2(0f, 1f);
            nameplateRt.anchorMax = new Vector2(4f / 14f, 1.5f);
            nameplateRt.offsetMin = Vector2.zero; nameplateRt.offsetMax = Vector2.zero;

            var nameplateBgGo = new GameObject("Background");
            nameplateBgGo.transform.SetParent(nameplateRt, false);
            var nameplateBgRt = nameplateBgGo.AddComponent<RectTransform>();
            nameplateBgRt.anchorMin = Vector2.zero; nameplateBgRt.anchorMax = Vector2.one;
            nameplateBgRt.offsetMin = Vector2.zero; nameplateBgRt.offsetMax = Vector2.zero;
            var nameplateBgImg = nameplateBgGo.AddComponent<Image>();
            nameplateBgImg.color = bgColor;
            nameplateBgImg.raycastTarget = false;

            info.nameplateBackground = nameplateBgImg;
            info.nameplateNameText = CreateRegionText(nameplateRt, new Vector2(0f, 0f), new Vector2(0.6f, 1f), TextAnchor.MiddleLeft, 26, 10f, 2f);
            info.nameplateJobLevelText = CreateRegionText(nameplateRt, new Vector2(0.6f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, 18, 4f, 2f);
            info.nameplateNameText.fontStyle = FontStyle.Bold;
            info.nameplateNameText.color = textColor;
            info.nameplateJobLevelText.color = textColor;

            return info;
        }

        // 캐릭터 정보창의 체력 숫자 아래에 넣는 단순한 체력바(검정 배경 + 초록 채움). 전투창 체력바와 달리 예측/깜박임 없이 현재 체력만 보여줌
        private RectTransform CreateSimpleHpBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var barGo = new GameObject("HpBar");
            barGo.transform.SetParent(parent, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = anchorMin; barRt.anchorMax = anchorMax;
            barRt.offsetMin = Vector2.zero; barRt.offsetMax = Vector2.zero;

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(barRt, false);
            var trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one;
            trackRt.offsetMin = Vector2.zero; trackRt.offsetMax = Vector2.zero;
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0f, 0f, 0f, 0.5f);
            trackImg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRt, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.3f);
            fillImg.raycastTarget = false;

            return fillRt;
        }

        // combatMiniBar 한쪽(아군 또는 적) 블록: 왼쪽부터 이름 - 체력바+숫자 - 브레이크 표시 순으로 가로 배치
        private (Text name, Text hp, RectTransform hpFill, Text breakText) CreateCombatMiniSide(
            RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            var containerGo = new GameObject("Side");
            containerGo.transform.SetParent(parent, false);
            var rt = containerGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(rt, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            var nameText = CreateRegionText(rt, new Vector2(0f, 0f), new Vector2(0.32f, 1f), TextAnchor.MiddleLeft, 22, 14f, 6f);
            nameText.color = textColor;
            nameText.fontStyle = FontStyle.Bold;

            // 위쪽에 "HP" 라벨 + 큰 숫자, 아래쪽에 체력바 순으로 배치
            var hpLabel = CreateRegionText(rt, new Vector2(0.34f, 0.42f), new Vector2(0.46f, 1f), TextAnchor.MiddleLeft, 14, 2f, 4f);
            hpLabel.text = "HP";
            hpLabel.color = textColor;

            var hpText = CreateRegionText(rt, new Vector2(0.46f, 0.42f), new Vector2(0.8f, 1f), TextAnchor.MiddleLeft, 28, 2f, 2f);
            hpText.color = textColor;
            hpText.fontStyle = FontStyle.Bold;

            var hpFill = CreateSimpleHpBar(rt, new Vector2(0.34f, 0.06f), new Vector2(0.8f, 0.32f));

            var breakText = CreateRegionText(rt, new Vector2(0.8f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleCenter, 16, 4f, 4f);
            breakText.fontStyle = FontStyle.Bold;
            breakText.text = "BREAK";
            breakText.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);

            return (nameText, hpText, hpFill, breakText);
        }

        // 전투 연출 시작/끝에서 호출: attacker/defender 중 실제로 플레이어 팀인 쪽을 아군 칸에, 나머지를 적 칸에 표시
        public void ShowCombatMiniInfo(Unit attacker, Unit defender)
        {
            combatMiniAllyUnit = attacker.team == Team.Player ? attacker : defender;
            combatMiniEnemyUnit = attacker.team == Team.Player ? defender : attacker;
            combatMiniBar.SetActive(true);
            RefreshCombatMiniInfo();
        }

        public void HideCombatMiniInfo()
        {
            combatMiniBar.SetActive(false);
            combatMiniAllyUnit = null;
            combatMiniEnemyUnit = null;
        }

        // 코인 결과로 데미지가 반영되거나(ApplyHit) 브레이크가 걸리는 등 유닛 상태가 실시간으로 바뀌므로,
        // 켜져 있는 동안 매 프레임(Update) 다시 읽어와 이름/체력바/브레이크 표시를 최신 상태로 유지함
        private void RefreshCombatMiniInfo()
        {
            RefreshCombatMiniSide(combatMiniAllyUnit, combatMiniAllyName, combatMiniAllyHpText, combatMiniAllyHpFill, combatMiniAllyBreakText);
            RefreshCombatMiniSide(combatMiniEnemyUnit, combatMiniEnemyName, combatMiniEnemyHpText, combatMiniEnemyHpFill, combatMiniEnemyBreakText);
        }

        private void RefreshCombatMiniSide(Unit u, Text nameText, Text hpText, RectTransform hpFill, Text breakText)
        {
            if (u == null) return;
            nameText.text = u.unitName;
            hpText.text = $"{u.currentHP}/{u.stats.maxHP}";
            float frac = Mathf.Clamp01(u.currentHP / (float)Mathf.Max(1, u.stats.maxHP));
            hpFill.anchorMax = new Vector2(frac, 1f);
            UpdateBreakBlink(breakText, u.isBroken);
        }

        // 카메라 줌인이 끝난 뒤 호출: 맵을 가리고 VS 화면을 띄움. attacker/defender 중 실제 아군 팀을 항상 왼쪽에 고정 배치
        public void ShowVsScreen(Unit attacker, Unit defender)
        {
            Unit ally = attacker.team == Team.Player ? attacker : defender;
            Unit enemy = attacker.team == Team.Player ? defender : attacker;

            vsAllyImage.sprite = ally.bodyRenderer.sprite;
            vsAllyImage.color = ally.bodyRenderer.color;
            vsEnemyImage.sprite = enemy.bodyRenderer.sprite;
            vsEnemyImage.color = enemy.bodyRenderer.color;

            vsAllyRect.anchoredPosition = Vector2.zero;
            vsEnemyRect.anchoredPosition = Vector2.zero;

            vsPanel.SetActive(true);
        }

        public void HideVsScreen() => vsPanel.SetActive(false);

        // attacker가 아군/적 어느 쪽 슬롯인지에 따라 그 스프라이트가 상대 쪽으로 살짝 돌진했다가 되돌아옴
        public IEnumerator PlayVsLunge(Unit attacker)
        {
            bool isAllySlot = attacker.team == Team.Player;
            RectTransform rect = isAllySlot ? vsAllyRect : vsEnemyRect;
            float dir = isAllySlot ? 1f : -1f; // 아군은 오른쪽(적 쪽)으로, 적은 왼쪽(아군 쪽)으로 돌진
            Vector2 start = rect.anchoredPosition;
            Vector2 lunge = start + new Vector2(dir * VsLungeDistance, 0f);

            yield return LerpAnchoredPosition(rect, start, lunge, VsLungeOutDuration);
            yield return LerpAnchoredPosition(rect, lunge, start, VsLungeBackDuration);
        }

        // defender가 아군/적 어느 쪽 슬롯인지에 따라 그 스프라이트가 얻어맞은 반응(뒤로 밀림 + 색 번쩍임)을 보여줌.
        // 이 공격으로 죽었더라도(ApplyHit이 먼저 실행돼 defender.IsAlive가 이미 false일 수 있음) 반응은 그대로 재생함 —
        // 안 그러면 VS 화면에서 아무 반응 없이 멈춰있다가 그냥 닫혀서 즉사인지 아닌지 알기 어려움
        public IEnumerator PlayVsHitReaction(Unit defender, bool isBreak)
        {
            bool isAllySlot = defender.team == Team.Player;
            RectTransform rect = isAllySlot ? vsAllyRect : vsEnemyRect;
            Image img = isAllySlot ? vsAllyImage : vsEnemyImage;
            float dir = isAllySlot ? -1f : 1f; // 맞은 쪽은 상대 반대 방향(뒤)으로 밀림

            Vector2 start = rect.anchoredPosition;
            Vector2 knockback = start + new Vector2(dir * VsKnockbackDistance, 0f);
            Color original = img.color;
            Color flash = isBreak ? new Color(0.7f, 0.3f, 1f) : Color.white;

            rect.anchoredPosition = knockback;
            img.color = flash;

            float t = 0f;
            while (t < VsHitReactionDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / VsHitReactionDuration);
                rect.anchoredPosition = Vector2.Lerp(knockback, start, p);
                img.color = Color.Lerp(flash, original, p);
                yield return null;
            }
            rect.anchoredPosition = start;
            img.color = original;
        }

        private static IEnumerator LerpAnchoredPosition(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, t / duration);
                yield return null;
            }
            rect.anchoredPosition = to;
        }

        // 위에서부터 간격을 두고: 이름/직업/레벨/무기(가로 한 줄) - 체력(코인은 그 옆에 공격/수비 2줄로) - 체력바
        private BattleUnitInfo CreateBattleUnitInfoBlock(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            var containerGo = new GameObject("BattleUnitInfo");
            containerGo.transform.SetParent(parent, false);
            var rt = containerGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(rt, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            // 위에서부터: 이름/직업/레벨/무기 - (간격) - 체력+코인(체력 옆에 공격코인/수비코인 2줄) - (간격) - 체력바
            var info = new BattleUnitInfo
            {
                container = rt,
                background = bgImg,
                // 이름 뒤(0.30~0.40)는 특히 넓게 띄우고, 나머지 칸들 사이도 전부 간격을 둬서 각자 자기 칸으로 분리되게 함
                nameText = CreateRegionText(rt, new Vector2(0.04f, 0.72f), new Vector2(0.30f, 1f), TextAnchor.MiddleLeft, 40, 0f, 4f),
                // 직업/레벨 글씨 크기를 코인 라벨과 같은 크기(22)로 맞춤
                jobLevelText = CreateRegionText(rt, new Vector2(0.40f, 0.72f), new Vector2(0.58f, 1f), TextAnchor.MiddleLeft, 22, 0f, 4f),
                // 직업/레벨 옆에 들고 있는 무기를 아이콘+이름으로 표시(클릭 불가, 정보 표시 전용)
                weaponIcon = CreateWeaponIconPlaceholder(rt, new Vector2(0.66f, 0.86f), 20f),
                weaponText = CreateRegionText(rt, new Vector2(0.72f, 0.72f), new Vector2(0.98f, 1f), TextAnchor.MiddleLeft, 26, 0f, 4f),
                // 이름 줄과 간격을 두고(0.64~0.72 비움) 체력을 표시. 코인이 옆에 오므로 세로로는 이 칸 전체 높이를 다 씀
                hpLabelText = CreateRegionText(rt, new Vector2(0.04f, 0.30f), new Vector2(0.12f, 0.64f), TextAnchor.MiddleLeft, 18, 0f, 2f),
                hpText = CreateRegionText(rt, new Vector2(0.15f, 0.30f), new Vector2(0.38f, 0.64f), TextAnchor.MiddleLeft, 38, 0f, 2f),
                // 코인 정보는 체력 옆에(0.38~0.44 간격을 두고): 공격코인(위)/수비코인(아래) 2줄로 나눠 라벨+원을 함께 표시
                atkCoinLabel = CreateRegionText(rt, new Vector2(0.44f, 0.49f), new Vector2(0.66f, 0.64f), TextAnchor.MiddleLeft, 22, 0f, 2f),
                atkCoinDots = CreateCoinDots(rt, new Vector2(0.70f, 0.49f), new Vector2(0.98f, 0.64f), MaxCoinDots, CoinDotAtkColor),
                defCoinLabel = CreateRegionText(rt, new Vector2(0.44f, 0.30f), new Vector2(0.66f, 0.45f), TextAnchor.MiddleLeft, 22, 0f, 2f),
                defCoinDots = CreateCoinDots(rt, new Vector2(0.70f, 0.30f), new Vector2(0.98f, 0.45f), MaxCoinDots, CoinDotDefColor),
            };
            info.nameText.fontStyle = FontStyle.Bold;
            info.hpText.fontStyle = FontStyle.Bold;
            info.atkCoinLabel.text = "공격코인";
            info.defCoinLabel.text = "수비코인";

            info.nameText.color = textColor;
            info.jobLevelText.color = textColor;
            info.weaponText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            info.atkCoinLabel.color = textColor;
            info.defCoinLabel.color = textColor;

            // 체력바를 더 얇게 하고 아래쪽으로 내려서, 위쪽에 남는 체력 숫자를 표시할 빈 공간을 확보
            var (damagePreview, fill, splitMarker, splitLabel) = CreateHpBar(rt, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.11f));
            info.hpBarDamagePreview = damagePreview;
            info.hpBarDamagePreviewImage = damagePreview.GetComponent<Image>();
            info.hpBarFill = fill;
            info.hpBarSplitMarker = splitMarker;
            info.hpBarSplitLabel = splitLabel;

            return info;
        }

        // 배경(빈 칸) 위에 깜박이는 색(이번 전투로 깎일 체력) → 그 위에 초록(전투 후 남는 체력) 순으로 겹쳐, 두 값의 차이만큼 깜박이는 구간이 드러남
        private (RectTransform damagePreview, RectTransform fill, RectTransform splitMarker, Text splitLabel) CreateHpBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var barGo = new GameObject("HpBar");
            barGo.transform.SetParent(parent, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = anchorMin; barRt.anchorMax = anchorMax;
            barRt.offsetMin = Vector2.zero; barRt.offsetMax = Vector2.zero;

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(barRt, false);
            var trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one;
            trackRt.offsetMin = Vector2.zero; trackRt.offsetMax = Vector2.zero;
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0f, 0f, 0f, 0.5f);
            trackImg.raycastTarget = false;

            var damageGo = new GameObject("DamagePreview");
            damageGo.transform.SetParent(barRt, false);
            var damageRt = damageGo.AddComponent<RectTransform>();
            damageRt.anchorMin = new Vector2(0f, 0f); damageRt.anchorMax = new Vector2(0f, 1f);
            damageRt.offsetMin = Vector2.zero; damageRt.offsetMax = Vector2.zero;
            var damageImg = damageGo.AddComponent<Image>();
            damageImg.color = Color.white;
            damageImg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRt, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.3f);
            fillImg.raycastTarget = false;

            // 깜박이는 구간(깎일 체력)과 안 깜박이는 구간(남는 체력)의 경계를 짚어주는 빨간 막대 줄(삼각형 대신).
            // 세로로 막대 전체 높이만큼 걸치고 위아래로 살짝 튀어나오게 해서 경계선이 뚜렷이 보이게 함
            var markerGo = new GameObject("SplitMarker");
            markerGo.transform.SetParent(barRt, false);
            var markerRt = markerGo.AddComponent<RectTransform>();
            markerRt.anchorMin = new Vector2(0f, 0f); markerRt.anchorMax = new Vector2(0f, 1f);
            markerRt.pivot = new Vector2(0.5f, 0.5f);
            markerRt.sizeDelta = new Vector2(5f, 8f);
            var markerImg = markerGo.AddComponent<Image>();
            markerImg.sprite = VisualFactory.FlatSprite(new Color(0.95f, 0.15f, 0.15f), new Vector2(0.5f, 0.5f));
            markerImg.raycastTarget = false;

            // 전투 후 남는 체력 숫자: 막대 안이 아니라 막대 위쪽 빈 공간에 크게 표시해 시인성을 높임
            var labelGo = new GameObject("SplitLabel");
            labelGo.transform.SetParent(barRt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 1f); labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 4f);
            labelRt.sizeDelta = new Vector2(58f, 32f);
            var labelText = labelGo.AddComponent<Text>();
            ConfigureText(labelText, 22, TextAnchor.MiddleCenter);
            labelText.fontStyle = FontStyle.Bold;
            var labelOutline = labelGo.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            return (damageRt, fillRt, markerRt, labelText);
        }

        private void SetBattleUnitInfo(BattleUnitInfo info, Unit u, int incomingDamage)
        {
            bool isAlly = u.team == Team.Player;
            info.background.color = isAlly ? AllyInfoBgColor : EnemyInfoBgColor;
            Color textColor = isAlly ? AllyInfoTextColor : EnemyInfoTextColor;
            info.nameText.color = textColor;
            info.jobLevelText.color = textColor;
            info.weaponText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            info.atkCoinLabel.color = textColor;
            info.defCoinLabel.color = textColor;

            info.nameText.text = u.unitName;
            info.jobLevelText.text = $"{JobName(u.weapon.type)} Lv.{u.stats.level}";
            SetWeaponIcon(info.weaponIcon, u.weapon);
            info.weaponText.text = u.weapon.weaponName;
            info.hpLabelText.text = "HP";
            info.hpText.text = $"{u.currentHP}/{u.stats.maxHP}";

            for (int i = 0; i < info.atkCoinDots.Length; i++)
                info.atkCoinDots[i].gameObject.SetActive(i < u.stats.atkCoins.Count);
            for (int i = 0; i < info.defCoinDots.Length; i++)
                info.defCoinDots[i].gameObject.SetActive(i < u.stats.defCoins.Count);

            float maxHP = Mathf.Max(1, u.stats.maxHP);
            float currentFraction = Mathf.Clamp01(u.currentHP / maxHP);
            float postHP = Mathf.Clamp(u.currentHP - incomingDamage, 0, u.stats.maxHP);
            float postFraction = Mathf.Clamp01(postHP / maxHP);

            info.hpBarDamagePreview.anchorMax = new Vector2(currentFraction, 1f);
            info.hpBarFill.anchorMax = new Vector2(postFraction, 1f);

            info.hpBarSplitMarker.anchorMin = new Vector2(postFraction, 0f);
            info.hpBarSplitMarker.anchorMax = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.rectTransform.anchorMin = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.rectTransform.anchorMax = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.text = ((int)postHP).ToString();
        }

        // 영역을 동일한 간격의 cols x rows 칸으로 나눠 칸마다 텍스트 하나씩 배치
        private Text[] CreateStatGrid(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int cols, int rows, int fontSize, float paddingX = 14f, float paddingY = 14f)
        {
            var texts = new Text[cols * rows];
            float cellW = (anchorMax.x - anchorMin.x) / cols;
            float cellH = (anchorMax.y - anchorMin.y) / rows;
            for (int i = 0; i < texts.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float xMin = anchorMin.x + cellW * col;
                float yMax = anchorMax.y - cellH * row;
                texts[i] = CreateRegionText(parent, new Vector2(xMin, yMax - cellH), new Vector2(xMin + cellW, yMax), TextAnchor.MiddleLeft, fontSize, paddingX, paddingY);
            }
            return texts;
        }

        // 무기가 없을 땐 빈 자리만 흐리게 표시하고, 있으면 SetWeaponIcon이 무기 타입에 맞는 그림으로 채움
        // anchorCenter 위치에 고정 픽셀 크기(size)의 정사각형을 배치함 (비율 사각형을 쓰면 칸마다 가로세로 비가 달라 그림이 눌려 보임)
        private Image CreateWeaponIconPlaceholder(Transform parent, Vector2 anchorCenter, float size)
        {
            var go = new GameObject("WeaponIcon");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorCenter; rt.anchorMax = anchorCenter; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
            img.sprite = null;
            return img;
        }

        // anchorMin~anchorMax 구간에 작은 원(코인 하나당 하나씩) maxCount개를 가로로 나란히 만들어 둠.
        // 실제 코인 개수만큼만 SetActive(true)로 켜서 쓰고 나머지는 숨김(SetBattleUnitInfo에서 갱신)
        private Image[] CreateCoinDots(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int maxCount, Color color)
        {
            var dots = new Image[maxCount];
            float cellW = (anchorMax.x - anchorMin.x) / maxCount;
            float centerY = (anchorMin.y + anchorMax.y) / 2f;
            var sprite = VisualFactory.CircleSprite(color);
            for (int i = 0; i < maxCount; i++)
            {
                var go = new GameObject($"CoinDot{i}");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                float centerX = anchorMin.x + cellW * (i + 0.5f);
                rt.anchorMin = new Vector2(centerX, centerY); rt.anchorMax = rt.anchorMin; rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(16f, 16f);

                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                dots[i] = img;
            }
            return dots;
        }

        private static void SetWeaponIcon(Image img, WeaponData weapon)
        {
            if (weapon == null)
            {
                img.sprite = null;
                img.color = new Color(1f, 1f, 1f, 0.12f);
                return;
            }

            img.sprite = VisualFactory.WeaponIconSprite(weapon);
            img.color = Color.white;
        }

        private void SetUnitInfo(UnitInfoTexts info, Unit u)
        {
            info.container.gameObject.SetActive(true);

            // 아군 선택/공격뿐 아니라 적 정찰(클릭 확인)에도 같은 블록이 재사용되므로, 팀에 맞는 색으로 매번 갱신
            bool isAlly = u.team == Team.Player;
            Color bgColor = isAlly ? AllyInfoBgColor : EnemyInfoBgColor;
            Color textColor = isAlly ? AllyInfoTextColor : EnemyInfoTextColor;
            info.background.color = bgColor;
            foreach (var t in info.weaponTexts) t.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            foreach (var t in info.statTexts) t.color = textColor;

            info.nameplateBackground.color = bgColor;
            info.nameplateNameText.color = textColor;
            info.nameplateJobLevelText.color = textColor;
            info.nameplateNameText.text = u.unitName;
            info.nameplateJobLevelText.text = $"Lv.{u.stats.level} {JobName(u.weapon.type)}";
            SetWeaponIcon(info.weaponIcon, u.weapon);

            // 일러스트가 있는 캐릭터만 보여주고, 없으면 숨김(빈 사각형이 보이는 것을 방지)
            bool hasIllustration = u.illustrationSprite != null;
            info.illustrationImage.gameObject.SetActive(hasIllustration);
            info.illustrationImage.sprite = u.illustrationSprite;
            if (hasIllustration)
            {
                // 마스크 창(세로 4칸) 높이의 IllustrationZoom배 크기로 그려서, 그중 위쪽 1/Zoom만 창에 보이게(=상반신만) 함.
                // 원본 그림 비율(가로/세로)은 캐릭터마다 다를 수 있어 실제 텍스처 크기로 매번 다시 계산
                // 캐릭터별로 위치/배율을 다르게 두고 싶으면 Unit.illustrationOffset/illustrationZoom을 채움(없으면 공통값)
                var tex = u.illustrationSprite.texture;
                float nativeAspect = (float)tex.width / tex.height;
                float zoom = u.illustrationZoom ?? IllustrationZoom;
                float renderedHeight = 4f * TilePixels * zoom;
                float renderedWidth = renderedHeight * nativeAspect;
                info.illustrationImage.rectTransform.sizeDelta = new Vector2(renderedWidth, renderedHeight);
                info.illustrationImage.rectTransform.anchoredPosition = u.illustrationOffset ?? IllustrationOffset;
            }

            for (int i = 0; i < info.weaponTexts.Length; i++)
            {
                var w = i < u.weaponSlots.Length ? u.weaponSlots[i] : null;
                info.weaponTexts[i].text = w == null ? "-" : (w == u.weapon ? $"▶{w.weaponName}" : $"  {w.weaponName}");
            }

            info.hpText.text = $"{u.currentHP}/{u.stats.maxHP}";
            float maxHP = Mathf.Max(1, u.stats.maxHP);
            info.hpBarFill.anchorMax = new Vector2(Mathf.Clamp01(u.currentHP / maxHP), 1f);

            // 방어는 "앞면/뒷면" 합산값을 그대로 보여줌(코인이 여러 개면 각각 합산).
            // 공격은 코인을 여러 개 들어도 실제 전투 계산과 무관하게 화면엔 코인 1개(첫 번째)의 값만 보여줌(어차피 같은 값의 코인만 들도록 하기 때문).
            // 코인 개수도 옆에 같이 보여줌. 스탯이 5개뿐이라 6칸 그리드 중 1칸은 비워 둠
            string[] statValues = { CoinFaceSingle(u.stats.atkCoins), u.stats.atkCoins.Count.ToString(), u.stats.spd.ToString(), CoinFaceSummary(u.stats.defCoins), u.stats.defCoins.Count.ToString() };
            for (int i = 0; i < info.statTexts.Length; i++)
                info.statTexts[i].text = i < statValues.Length ? $"{StatLabels[i]} {statValues[i]}" : string.Empty;
        }

        // 코인 리스트의 앞면 합/뒷면 합을 "앞/뒤" 형태 문자열로 반환(예: 앞면 12+12, 뒷면 0+0인 코인 2개 -> "24/0")
        private static string CoinFaceSummary(List<Coin> coins)
        {
            int heads = 0, tails = 0;
            foreach (var c in coins) { heads += c.heads; tails += c.tails; }
            return $"{heads}/{tails}";
        }

        // 코인 1개(첫 번째)의 앞면/뒷면 값만 "앞/뒤" 형태 문자열로 반환(코인이 없으면 "0/0")
        private static string CoinFaceSingle(List<Coin> coins)
        {
            if (coins.Count == 0) return "0/0";
            return $"{coins[0].heads}/{coins[0].tails}";
        }

        private static string JobName(WeaponType t) => t switch
        {
            WeaponType.Sword => "검사",
            WeaponType.Lance => "창병",
            WeaponType.Axe => "도끼병",
            WeaponType.Bow => "궁수",
            WeaponType.Tome => "마도사",
            WeaponType.Dagger => "도적",
            _ => "전사",
        };

        private static readonly Color BreakColor = new Color(1f, 0.831f, 0f); // FFD400

        // 가운데 예측 칸을 위(내가 주는 피해)/아래(적이 주는 피해) 두 칸으로 뚜렷이 나누고, 각각 그 안에 BREAK/화살표/스탯을 고정 배치.
        // BREAK 유무와 상관없이 화살표·스탯 위치가 항상 그대로라 안 밀리고, Best Fit으로 글자가 칸 밖으로 넘치지 않게 함
        private void CreateBattleMidTexts()
        {
            // 내 피해 칸(위쪽 절반) / 적 피해 칸(아래쪽 절반) 배경을 옅게 색으로 구분
            CreateMidHalfBackground(new Vector2(MidColumnMin, 0.5f), new Vector2(MidColumnMax, 1f), AllyColor);
            CreateMidHalfBackground(new Vector2(MidColumnMin, 0f), new Vector2(MidColumnMax, 0.5f), EnemyColor);

            // 두 칸 사이 경계선
            var dividerGo = new GameObject("MidDivider");
            dividerGo.transform.SetParent(battlePanel.transform, false);
            var dividerRt = dividerGo.AddComponent<RectTransform>();
            dividerRt.anchorMin = new Vector2(MidColumnMin, 0.5f); dividerRt.anchorMax = new Vector2(MidColumnMax, 0.5f);
            dividerRt.pivot = new Vector2(0.5f, 0.5f);
            dividerRt.sizeDelta = new Vector2(0f, 2f);
            var dividerImg = dividerGo.AddComponent<Image>();
            dividerImg.color = new Color(1f, 1f, 1f, 0.25f);
            dividerImg.raycastTarget = false;

            // 칸 배분(BREAK/화살표/스탯 = 0.14/0.14/0.22): BREAK 시인성을 위해 화살표 칸에서 공간을 좀 덜어옴(스탯 칸은 그대로 유지)
            allyBreakText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.86f), new Vector2(MidColumnMax, 1f), TextAnchor.MiddleCenter, 18, 6f, 0f);
            allyArrowText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.72f), new Vector2(MidColumnMax, 0.86f), TextAnchor.MiddleCenter, 34, 6f, 0f);
            allyStatsText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.50f), new Vector2(MidColumnMax, 0.72f), TextAnchor.MiddleCenter, 24, 6f, 0f);

            enemyStatsText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0f), new Vector2(MidColumnMax, 0.22f), TextAnchor.MiddleCenter, 24, 6f, 0f);
            enemyArrowText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.22f), new Vector2(MidColumnMax, 0.36f), TextAnchor.MiddleCenter, 34, 6f, 0f);
            enemyBreakText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.36f), new Vector2(MidColumnMax, 0.5f), TextAnchor.MiddleCenter, 18, 6f, 0f);

            allyBreakText.fontStyle = FontStyle.Bold;
            enemyBreakText.fontStyle = FontStyle.Bold;
            allyBreakText.text = "BREAK!";
            enemyBreakText.text = "BREAK!";
            allyBreakText.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);
            enemyBreakText.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);

            // 글자가 길어져도(피해 두 자리, 명중 100% 등) 칸 밖으로 넘치지 않도록 자동으로 줄어들게 함
            // 화살표(최대 46)/BREAK(최대 30)/스탯(최대 26) 모두 시인성을 위해 키움
            EnableBestFit(allyBreakText, 14, 30);
            EnableBestFit(allyArrowText, 20, 46);
            EnableBestFit(allyStatsText, 14, 26);
            EnableBestFit(enemyBreakText, 14, 30);
            EnableBestFit(enemyArrowText, 20, 46);
            EnableBestFit(enemyStatsText, 14, 26);
        }

        private static void EnableBestFit(Text text, int minSize, int maxSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        private void CreateMidHalfBackground(Vector2 anchorMin, Vector2 anchorMax, Color tint)
        {
            var go = new GameObject("MidHalfBackground");
            go.transform.SetParent(battlePanel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(tint.r, tint.g, tint.b, 0.12f);
            img.raycastTarget = false;
        }

        private const string AllyArrowLine = "――――――→"; // 피해/명중 위에 길게 놓는 화살표 (시인성)
        private const string EnemyArrowLine = "←――――――";
        private const string EnemyDamageHighlightHex = "FF7A3D"; // 적이 넣는 피해 강조색

        // "피해 X" 부분만 굵게+색을 넣어 강조
        private static string BuildStatsLine(int damage, string highlightHex)
            => $"<b><color=#{highlightHex}>피해 {damage}</color></b>";

        private void UpdateBattleMidTexts(CombatForecast f)
        {
            allyBreakActive = f.attackerHasAdvantage;
            allyArrowText.text = AllyArrowLine;
            allyArrowText.color = f.attackerHasAdvantage ? BreakColor : AllyColor;
            // 아군이 넣는 피해는 아군 색(파랑)으로 강조
            allyStatsText.text = BuildStatsLine(f.attackerDamage, ColorUtility.ToHtmlStringRGB(AllyColor));

            enemyBreakActive = f.defenderHasAdvantage;
            if (f.defenderCanCounter)
            {
                enemyArrowText.text = EnemyArrowLine;
                enemyArrowText.color = f.defenderHasAdvantage ? BreakColor : EnemyColor;
                enemyStatsText.text = BuildStatsLine(f.defenderDamage, EnemyDamageHighlightHex);
            }
            else
            {
                enemyArrowText.text = "—";
                enemyArrowText.color = EnemyColor;
                enemyStatsText.text = "반격 없음";
            }
        }

        public void ShowActionMenu(bool canAttack, Action onAttack, Action onWait, Action onCancel)
        {
            attackButton.onClick.RemoveAllListeners();
            waitButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            attackButton.interactable = canAttack;
            attackButton.onClick.AddListener(() => onAttack());
            waitButton.onClick.AddListener(() => onWait());
            cancelButton.onClick.AddListener(() => onCancel());

            actionMenuPanel.SetActive(true);
        }

        public void HideActionMenu() => actionMenuPanel.SetActive(false);

        // 무기를 처음 누르면 onPreview(사거리만 표시), 이미 미리보기 중인 무기를 한 번 더 누르면 onConfirm으로 확정
        private int previewedWeaponSlot = -1;

        // usable[i]가 false면(그 무기 사거리에 닿는 적이 없으면) 칸을 흐리게 표시하고 고를 수 없게 함
        public void ShowWeaponSelect(WeaponData[] slots, bool[] usable, Action<WeaponData> onPreview, Action onConfirm, Action onCancel)
        {
            previewedWeaponSlot = -1;

            for (int i = 0; i < weaponSlotButtons.Length; i++)
            {
                var w = i < slots.Length ? slots[i] : null;
                bool canUse = w != null && i < usable.Length && usable[i];
                var btn = weaponSlotButtons[i];
                btn.onClick.RemoveAllListeners();
                SetWeaponSlotSelected(i, false);

                if (w != null)
                {
                    weaponSlotLabels[i].text = canUse
                        ? $"{w.weaponName}   위력 {w.might}  사거리 {w.minRange}-{w.maxRange}"
                        : $"{w.weaponName}   위력 {w.might}  사거리 {w.minRange}-{w.maxRange}  (사거리 밖)";
                    weaponSlotLabels[i].color = canUse ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                    weaponSlotIcons[i].sprite = VisualFactory.WeaponIconSprite(w);
                    weaponSlotIcons[i].color = canUse ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    btn.interactable = canUse;

                    if (canUse)
                    {
                        int index = i;
                        btn.onClick.AddListener(() =>
                        {
                            if (previewedWeaponSlot == index)
                            {
                                onConfirm();
                                return;
                            }

                            previewedWeaponSlot = index;
                            for (int j = 0; j < weaponSlotButtons.Length; j++) SetWeaponSlotSelected(j, j == index);
                            onPreview(w);
                        });
                    }
                }
                else
                {
                    weaponSlotLabels[i].text = "-";
                    weaponSlotIcons[i].sprite = null;
                    weaponSlotIcons[i].color = new Color(1f, 1f, 1f, 0f);
                    btn.interactable = false;
                }
            }

            weaponSelectCancelButton.onClick.RemoveAllListeners();
            weaponSelectCancelButton.onClick.AddListener(() => onCancel());

            weaponSelectPanel.SetActive(true);
        }

        public void HideWeaponSelect()
        {
            weaponSelectPanel.SetActive(false);
            previewedWeaponSlot = -1;
        }

        private void SetWeaponSlotSelected(int index, bool selected)
        {
            weaponSlotButtons[index].GetComponent<Image>().color = selected ? WeaponSlotSelectedColor : WeaponSlotNormalColor;
        }

        // victory면 승리, 아니면 패배 문구를 띄움. onPrimary는 승리 시 "다음 스테이지", 패배 시 "재도전" 콜백(더 진행할 스테이지가 없으면 null을 넘겨 버튼을 숨김).
        // onHome은 두 경우 모두 "홈으로" 콜백.
        public void ShowGameOver(bool victory, Action onPrimary, string primaryLabel, Action onHome)
        {
            gameOverPanel.SetActive(true);
            gameOverText.text = victory ? "승리!" : "패배...";

            gameOverPrimaryButton.gameObject.SetActive(onPrimary != null);
            gameOverPrimaryButton.onClick.RemoveAllListeners();
            if (onPrimary != null)
            {
                gameOverPrimaryButton.GetComponentInChildren<Text>().text = primaryLabel;
                gameOverPrimaryButton.onClick.AddListener(() => onPrimary());
            }

            gameOverHomeButton.onClick.RemoveAllListeners();
            gameOverHomeButton.onClick.AddListener(() => onHome());
        }

        private Image CreateScreenFlash(Transform parent)
        {
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            // 페이즈 전환 시 알파가 매 프레임 바뀌므로 독립 Canvas로 분리
            go.AddComponent<Canvas>();
            return img;
        }

        private GameObject CreatePhaseBanner(Transform parent, out Image bg, out Text text)
        {
            var go = new GameObject("PhaseBanner");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900, 130);
            rt.anchoredPosition = new Vector2(0f, 140f);

            bg = go.AddComponent<Image>();
            bg.raycastTarget = false;

            CreateAccentLine(go.transform, true);
            CreateAccentLine(go.transform, false);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            text = textGo.AddComponent<Text>();
            ConfigureText(text, 52, TextAnchor.MiddleCenter);
            text.font = phaseBannerFont; // Cinzel은 한글 글리프가 없어 MY TURN/ENEMY TURN 같은 영어 문구 전용으로 씀

            // 슬라이드 인/아웃 동안 위치가 매 프레임 바뀌므로 독립 Canvas로 분리
            go.AddComponent<Canvas>();

            return go;
        }

        private void CreateAccentLine(Transform parent, bool top)
        {
            var go = new GameObject(top ? "TopLine" : "BottomLine");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 6f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private void PlayPhaseImpact(TurnPhase phase)
        {
            if (phaseImpactRoutine != null) StopCoroutine(phaseImpactRoutine);
            phaseImpactRoutine = StartCoroutine(PhaseImpactRoutine(phase));
        }

        // 턴 시작을 화면 슬라이드+플래시로 강조해 페이즈 전환을 명확히 알림
        private IEnumerator PhaseImpactRoutine(TurnPhase phase)
        {
            bool isPlayer = phase == TurnPhase.Player;
            phaseBannerBg.color = isPlayer ? new Color(0.12f, 0.22f, 0.5f, 0.95f) : new Color(0.5f, 0.12f, 0.12f, 0.95f);
            phaseBannerText.text = isPlayer ? "MY TURN" : "ENEMY TURN";

            var rt = phaseBannerPanel.GetComponent<RectTransform>();
            float bannerY = rt.anchoredPosition.y;
            float offscreenX = isPlayer ? -1100f : 1100f;
            rt.anchoredPosition = new Vector2(offscreenX, bannerY);
            phaseBannerPanel.SetActive(true);

            const float slideInTime = 0.18f;
            const float holdTime = 0.65f;
            const float slideOutTime = 0.18f;
            const float flashTime = 0.15f;
            const float MaxStepTime = 1f / 30f; // 프레임이 한 번 크게 끊겨도(첫 시작 등) 슬라이드가 한 프레임 만에 끝나버리지 않도록 상한을 둠

            // 방금 활성화된 프레임의 deltaTime은 비활성 상태였던 만큼 부풀려져 있을 수 있어(특히 맨 처음 시작할 때),
            // 오프스크린 위치가 최소 한 프레임은 실제로 그려지고 나서 슬라이드를 시작하도록 함
            yield return null;

            float t = 0f;
            while (t < slideInTime)
            {
                t += Mathf.Min(Time.deltaTime, MaxStepTime);
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / slideInTime), 3f);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(offscreenX, 0f, eased), bannerY);
                yield return null;
            }
            rt.anchoredPosition = new Vector2(0f, bannerY);

            float flashT = 0f;
            while (flashT < flashTime)
            {
                flashT += Time.deltaTime;
                screenFlash.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 0f, flashT / flashTime));
                yield return null;
            }
            screenFlash.color = new Color(1f, 1f, 1f, 0f);

            yield return new WaitForSeconds(holdTime);

            t = 0f;
            while (t < slideOutTime)
            {
                t += Mathf.Min(Time.deltaTime, MaxStepTime);
                float eased = Mathf.Pow(Mathf.Clamp01(t / slideOutTime), 2f);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(0f, offscreenX, eased), bannerY);
                yield return null;
            }

            phaseBannerPanel.SetActive(false);
            phaseImpactRoutine = null;
        }

        private GameObject CreatePanel(string name, Vector2 anchor, Vector2 anchoredPos, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvasRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
            return go;
        }

        private Text CreateFillText(Transform parent, int fontSize, TextAnchor align, float bottomMargin = 10)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, bottomMargin);
            rt.offsetMax = new Vector2(-12, -10);
            var text = go.AddComponent<Text>();
            ConfigureText(text, fontSize, align);
            return text;
        }

        private Text CreateRegionText(Transform parent, Vector2 anchorMin, Vector2 anchorMax, TextAnchor align, int fontSize = 17, float paddingX = 14f, float paddingY = 14f)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(paddingX, paddingY);
            rt.offsetMax = new Vector2(-paddingX, -paddingY);
            var text = go.AddComponent<Text>();
            ConfigureText(text, fontSize, align);
            return text;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, float w, float h, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.28f, 0.4f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            ConfigureText(text, 24, TextAnchor.MiddleCenter);
            text.text = label;

            return btn;
        }

        private Button CreateHamburgerButton(Transform parent, Vector2 anchor, Vector2 anchoredPos, float size)
        {
            var go = new GameObject("MenuButton");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            for (int i = 0; i < 3; i++)
            {
                var barGo = new GameObject($"Bar{i}");
                barGo.transform.SetParent(go.transform, false);
                var brt = barGo.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(size * 0.6f, size * 0.09f);
                brt.anchoredPosition = new Vector2(0f, (1 - i) * size * 0.22f);
                var bar = barGo.AddComponent<Image>();
                bar.color = Color.white;
            }

            return btn;
        }

        private Text CreateScrollingLog(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var scrollGo = new GameObject("LogScroll");
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = anchorMin; scrollRt.anchorMax = anchorMax;
            scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = Vector2.zero;
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.25f);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero; viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(-16f, 0f);

            var text = contentGo.AddComponent<Text>();
            ConfigureText(text, 18, TextAnchor.UpperLeft);
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            return text;
        }

        private void ConfigureText(Text text, int fontSize, TextAnchor align)
        {
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
