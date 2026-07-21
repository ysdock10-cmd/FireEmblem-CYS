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
            grid.BuildGrid(stageIndex == 2 ? MapLibrary.Stage2Map() : MapLibrary.DefaultMap());
            cameraController.grid = grid;

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(transform);
            var gm = gmGo.AddComponent<GameManager>();
            gm.grid = grid;
            gm.cameraController = cameraController;

            SpawnRoster(grid, gm, stageIndex == 2 ? RosterLibrary.Stage2Roster() : RosterLibrary.DefaultRoster());

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
            // 스테이지 2는 스테이지 1을 한 번이라도 깨기 전까지 잠겨 있음
            void ShowStageSelect() => ui.ShowStageSelect(onStage1: () => EnterStage(1), onStage2: () => EnterStage(2), onBack: ShowHome,
                stage2Unlocked: GameSession.ClearedStages.Contains(1));
            void ShowCharacterView() => ui.ShowCharacterView(gm.PlayerUnits.ToList(), ShowHome);

            gm.OnGameOver += phase =>
            {
                if (phase == TurnPhase.Victory)
                {
                    GameSession.ClearedStages.Add(stageIndex);
                    bool hasNextStage = stageIndex < GameSession.StageCount;
                    ui.ShowGameOver(true, hasNextStage ? NextStage : (System.Action)null, "다음 스테이지", GoHome);
                }
                else
                {
                    ui.ShowGameOver(false, RetryStage, "재도전", GoHome);
                }
            };

            if (enterStageDirectly) ui.ShowLoading(() => gm.StartPlayerPhase());
            else ShowHome();
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
