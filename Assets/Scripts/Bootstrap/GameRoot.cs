using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SRPG
{
    public class GameRoot : MonoBehaviour
    {
        private void Awake()
        {
            // 씬을 다시 로드해서 스테이지를 새로 시작하는 경우(재도전/다음 스테이지/스테이지 선택) GameSession에 담겨온 스테이지로,
            // 아니면(최초 부팅) 1스테이지로 맵/유닛을 미리 만들어 둠(홈/캐릭터 확인 화면에서 바로 보여줄 수 있도록)
            // PendingStageIndex가 있었다는 건 특정 스테이지로 들어가려고 리로드한 것이므로, 이 경우엔 홈 화면을 건너뛰고 바로 전투로 들어감
            bool enterStageDirectly = GameSession.PendingStageIndex.HasValue;
            int stageIndex = GameSession.PendingStageIndex ?? 1;
            GameSession.PendingStageIndex = null;

            var cam = SetupCamera();
            var cameraController = cam.gameObject.AddComponent<CameraController>();
            cameraController.cam = cam;

            var gridGo = new GameObject("GridManager");
            gridGo.transform.SetParent(transform);
            var grid = gridGo.AddComponent<GridManager>();
            grid.BuildGrid(stageIndex switch
            {
                0 => MapLibrary.TutorialMap(),
                2 => MapLibrary.Stage2Map(),
                3 => MapLibrary.Stage3Map(),
                4 => MapLibrary.Stage4Map(),
                5 => MapLibrary.Stage5Map(),
                6 => MapLibrary.Stage6Map(),
                _ => MapLibrary.DefaultMap(),
            });
            cameraController.grid = grid;

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(transform);
            var gm = gmGo.AddComponent<GameManager>();
            gm.grid = grid;
            gm.cameraController = cameraController;

            SpawnRoster(grid, gm, stageIndex switch
            {
                0 => RosterLibrary.TutorialRoster(),
                2 => RosterLibrary.Stage2Roster(),
                3 => RosterLibrary.Stage3Roster(),
                4 => RosterLibrary.Stage4Roster(),
                5 => RosterLibrary.Stage5Roster(),
                6 => RosterLibrary.Stage6Roster(),
                _ => RosterLibrary.DefaultRoster(),
            });

            var uiGo = new GameObject("UIManager");
            uiGo.transform.SetParent(transform);
            var ui = uiGo.AddComponent<UIManager>();
            ui.Initialize(gm);
            gm.ui = ui;

            var pcGo = new GameObject("PlayerController");
            pcGo.transform.SetParent(transform);
            var pc = pcGo.AddComponent<PlayerController>();
            pc.gameManager = gm;
            pc.grid = grid;
            pc.ui = ui;
            pc.cameraController = cameraController;
            cameraController.OnTap += pc.HandleTap;
            cameraController.IsUnitDragStart = pc.CanDragUnitAt;
            cameraController.OnUnitDragRelease += pc.HandleUnitDragRelease;

            // 아군 스폰이 맵 중앙 근처(RosterLibrary 참고)로 옮겨졌으므로 시작 카메라도 그쪽을 비추게 함
            cameraController.startFocus = new GridPosition(8, 5);

            // 재시작 계열(재도전/다음 스테이지/홈으로)은 전부 씬을 다시 로드해서 처리함:
            // GridManager/GameManager/Unit 어디에도 "다시 만들기" 로직이 없어서, 씬 리로드로 GameBootstrap이
            // 처음부터 다시 실행되게 하는 편이 grid/유닛/UI 이벤트 구독을 일일이 손으로 정리하는 것보다 훨씬 안전함.
            // 어느 스테이지로 들어갈지는 GameSession.PendingStageIndex에 담아 리로드 너머로 전달함.
            void ReloadInto(int? nextStageIndex)
            {
                GameSession.PendingStageIndex = nextStageIndex;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            void EnterStage(int idx)
            {
                // 이미 이 스테이지로 맵/유닛을 만들어 둔 상태면 리로드 없이 바로 진행
                if (idx == stageIndex) { ui.ShowLoading(() => gm.StartPlayerPhase()); return; }
                ReloadInto(idx);
            }
            void RetryStage() => ReloadInto(stageIndex);
            void NextStage() => ReloadInto(stageIndex + 1);
            void GoHome() => ReloadInto(null);

            void ShowHome() => ui.ShowHomeScreen(onBattleSelected: ShowStageSelect, onCharactersSelected: ShowCharacterView);
            // 튜토리얼(0)과 스테이지 1은 항상 열려 있고, 나머지는 바로 앞 스테이지를 한 번이라도 깨기 전까지 잠겨 있음
            void ShowStageSelect() => ui.ShowStageSelect(onSelectStage: EnterStage, onBack: ShowHome,
                isUnlocked: stageNum => stageNum <= 1 || GameSession.ClearedStages.Contains(stageNum - 1));
            void ShowCharacterView() => ui.ShowCharacterView(gm.PlayerUnits.ToList(), ShowHome);

            gm.OnGameOver += phase =>
            {
                // 튜토리얼은 진행도(ClearedStages)에 들어가지 않고 "다음 스테이지"로 이어지지도 않음 - 그냥 연습용
                if (stageIndex == 0)
                {
                    ui.ShowGameOver(phase == TurnPhase.Victory, null, "", GoHome);
                    return;
                }

                if (phase == TurnPhase.Victory)
                {
                    GameSession.ClearedStages.Add(stageIndex);
                    PlayerProgress.Currency += 50; // 스테이지 클리어 보상(캐릭터 확인 화면에서 체력 강화에 씀)
                    bool hasNextStage = stageIndex < GameSession.StageCount;
                    ui.ShowGameOver(true, hasNextStage ? NextStage : (System.Action)null, "다음 스테이지", GoHome);
                }
                else
                {
                    ui.ShowGameOver(false, RetryStage, "재도전", GoHome);
                }
            };

            if (stageIndex == 0) SetupTutorialGuide(pc, gm, ui, grid);

            if (enterStageDirectly) ui.ShowLoading(() => gm.StartPlayerPhase());
            else ShowHome();
        }

        // 튜토리얼 진행 단계에 맞춰 안내 말풍선을 띄움. PlayerController/GameManager의 기존 이벤트 훅만 구독하므로
        // 일반 스테이지 로직에는 전혀 영향을 주지 않음
        // 앞부분(유닛 선택 -> 이동 -> 공격)은 정확히 지정된 칸만 누를 수 있도록 pc.TutorialAllowedTap으로 제한해서,
        // 처음 해보는 사람이 엉뚱한 칸을 눌러 헤매지 않고 정해진 순서 그대로 따라오게 함(RosterLibrary.TutorialRoster 배치 기준)
        private static void SetupTutorialGuide(PlayerController pc, GameManager gm, UIManager ui, GridManager grid)
        {
            // TutorialAllowedTap 설정과 동시에 깜박이는 노란 테두리로 그 칸을 짚어줌(null이면 표시를 끔)
            void SetGuidedTap(GridPosition? pos)
            {
                pc.TutorialAllowedTap = pos;
                if (pos.HasValue) grid.ShowGuideMarker(pos.Value);
                else grid.HideGuideMarker();
            }

            var guidedUnitPos = new GridPosition(8, 5);   // 알리어
            var guidedMovePos = new GridPosition(10, 5);  // 허수아비(11,5) 바로 옆
            SetGuidedTap(guidedUnitPos);

            bool shownWelcome = false, shownMove = false, shownMenu = false, shownWeapon = false, shownWeaponPreview = false, shownWeaponConfirmed = false, shownTarget = false, shownEnd = false, shownEnemyPhase = false;

            gm.OnPhaseChanged += (phase, turn) =>
            {
                if (phase == TurnPhase.Player && turn == 1 && !shownWelcome)
                {
                    shownWelcome = true;
                    ShowSteps(ui, null,
                        "튜토리얼에 오신 걸 환영해요!",
                        "표시된 파란 아군 유닛을 탭해서 선택해보세요. 지금은 안내된 칸만 누를 수 있어요.");
                }
                else if (phase == TurnPhase.Enemy && !shownEnemyPhase)
                {
                    shownEnemyPhase = true;
                    ShowSteps(ui, null,
                        "적 턴이에요. 적들이 자동으로 움직이고 공격을 시도해요.",
                        "허수아비는 위력이 0이라 반격해도 피해가 없으니 안심하고 지켜보세요.");
                }
            };

            pc.OnUnitSelected += () =>
            {
                if (shownMove) return;
                shownMove = true;
                // 다음 단계 전까지는 표시된 이동 칸 외엔 눌러도 반응하지 않음
                ShowSteps(ui, () => SetGuidedTap(guidedMovePos),
                    "파란 칸은 이동 가능한 범위예요.",
                    "빨간 칸은 공격 가능한 범위예요.",
                    "숲/산처럼 울퉁불퉁한 지형은 이동에 힘이 더 들어요.",
                    "표시된 칸을 탭해서 이동해보세요.");
            };
            pc.OnActionMenuOpened += () =>
            {
                if (shownMenu) return;
                shownMenu = true;
                // 이제부터는 메뉴/무기 버튼과 자동으로 하나뿐인 공격 대상으로 진행되므로 칸 제한을 풂
                SetGuidedTap(null);
                // 이 단계에서는 '공격'만 누를 수 있고 '대기'/'취소'는 눌러도 반응하지 않음(다음 메뉴부터는 자동으로 다시 풀림)
                ui.RestrictActionMenuToAttack();
                ShowSteps(ui, null,
                    "이동을 마쳤어요!",
                    "'공격'을 누르면 사거리 안의 적을 공격할 수 있어요.",
                    "'대기'를 누르면 이 유닛의 턴을 마쳐요.",
                    "지금은 공격을 눌러보세요.");
            };
            pc.OnWeaponSelectOpened += () =>
            {
                if (shownWeapon) return;
                shownWeapon = true;
                // 무기는 기본 장착 중인 슬롯(0번, 아이언 소드)만 고를 수 있게 잠금
                ui.RestrictWeaponSelectToSlot(0);
                ShowSteps(ui, null,
                    "무기 선택창이 열렸어요.",
                    "소지한 무기 중에 아이언 소드를 눌러볼까요?");
            };
            pc.OnWeaponPreviewed += () =>
            {
                if (shownWeaponPreview) return;
                shownWeaponPreview = true;
                ShowSteps(ui, null,
                    "무기를 한 번 누르면 무기의 사거리를 알 수 있어요.",
                    "빨간 칸이 이 무기의 사거리예요.",
                    "같은 무기를 한 번 더 누르면 무기가 선택돼요.",
                    "아이언 소드를 한 번 더 탭해서 확정해보세요.");
            };
            pc.OnWeaponConfirmed += () =>
            {
                if (shownWeaponConfirmed) return;
                shownWeaponConfirmed = true;
                ShowSteps(ui, null,
                    "무기 선택을 마쳤어요!",
                    "이제 적을 선택해볼까요?",
                    "적 하나를 선택하면 그 적과의 전투를 미리 예측해볼 수 있어요.",
                    "그 상태에서 빨간 칸 안에 있는 다른 적을 클릭하면, 그 다른 적과의 전투로 예측이 바뀌어요.",
                    "허수아비를 탭해서 선택해보세요.");
            };
            pc.OnAttackTargetSelected += () =>
            {
                if (shownTarget) return;
                shownTarget = true;
                // 이 단계에서는 공격 확정만 가능하고 취소/무기변경은 눌러도 반응하지 않음
                ui.RestrictTargetConfirmToAttack();
                ShowSteps(ui, null,
                    "대상을 골랐어요. 화면에 예상 피해량이 보이죠?",
                    "'공격' 버튼을 누르면 실제 전투 연출이 시작돼요.",
                    "전투가 시작되면 화면에 동전이 나타나서 빙글빙글 돌아요.",
                    "동전에는 '앞'면과 '뒤'면이 있어요.",
                    "화면 아무 곳이나 탭하면, 그 순간 보이는 면으로 동전이 멈춰요.",
                    "'앞'에서 멈추면 위력이 그대로 추가되지만, '뒤'에서 멈추면 추가되지 않아요.",
                    "그러니 동전이 '앞'을 보여줄 때 맞춰서 탭하는 게 유리해요!",
                    "화면 위쪽 숫자는 지금까지 계산된 데미지예요. 동전 결과에 따라 오르내리는 걸 확인해보세요.",
                    "준비되면 '공격' 버튼을 눌러 전투를 시작해보세요.");
            };
            pc.OnUnitActionEnded += () =>
            {
                if (shownEnd) return;
                shownEnd = true;
                ShowSteps(ui, null,
                    "무기는 검-도끼-창-검 순서로 상성이 있어요.",
                    "상성에서 이기면 상대를 '브레이크'시켜서 반격을 막을 수 있어요.",
                    "남은 허수아비 두 마리는 자유롭게 공격해보세요.",
                    "준비되면 '대기'를 선택해 턴을 마쳐보세요.");
            };
        }

        // 안내 문구 여러 개를 한 번에 하나씩, "확인"을 눌러야 다음으로 넘어가도록 순서대로 띄움.
        // onAllDone은 마지막 문구까지 확인한 뒤 실행할 후속 동작(다음 단계로 칸 제한을 옮기는 등)이 필요할 때만 넘기면 됨
        private static void ShowSteps(UIManager ui, Action onAllDone, params string[] messages)
        {
            void ShowAt(int index)
            {
                if (index >= messages.Length) { onAllDone?.Invoke(); return; }
                ui.ShowTutorialMessage(messages[index], () => ShowAt(index + 1));
            }
            ShowAt(0);
        }

        private Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.depth = 0;

            // 2:1(CameraController.TargetAspect) 밖 화면 비율(다른 폰 화면 등)에서는 카메라 옆/위아래를 검은 여백(레터박스)으로 채움
            var letterboxGo = new GameObject("LetterboxCamera");
            letterboxGo.transform.SetParent(transform);
            var letterboxCam = letterboxGo.AddComponent<Camera>();
            letterboxCam.clearFlags = CameraClearFlags.SolidColor;
            letterboxCam.backgroundColor = Color.black;
            letterboxCam.cullingMask = 0;
            letterboxCam.depth = -10;

            return cam;
        }

        private void SpawnRoster(GridManager grid, GameManager gm, UnitDefinition[] roster)
        {
            foreach (var def in roster)
            {
                var go = new GameObject("Unit");
                go.transform.SetParent(transform);
                var unit = go.AddComponent<Unit>();
                unit.Initialize(def, grid);
                grid.SetOccupant(unit.position, unit);
                gm.RegisterUnit(unit);
            }
        }
    }
}
