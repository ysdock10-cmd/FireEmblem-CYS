using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 홈/스테이지선택/로딩/게임오버 화면과 턴 시작 페이즈 배너 연출
    public partial class UIManager
    {
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

        // 스테이지 선택 화면. isUnlocked(스테이지 번호)가 false를 반환하면 그 버튼은 눌러도 반응하지 않게 잠금.
        // 버튼 배열 인덱스 0은 튜토리얼, 1~StageCount는 실제 스테이지 번호와 그대로 대응함.
        // 열려있는 스테이지 중 가장 높은 번호(=다음에 도전할 스테이지)가 화면 가운데 오도록 스크롤을 맞춰서 보여줌
        public void ShowStageSelect(Action<int> onSelectStage, Action onBack, Func<int, bool> isUnlocked)
        {
            stageSelectPanel.SetActive(true);

            int highestUnlocked = 0;
            for (int stageNum = 0; stageNum < stageButtons.Length; stageNum++)
            {
                int capturedStageNum = stageNum; // 클로저 캡처용 지역 변수(반복 변수를 그대로 캡처하면 전부 마지막 값을 참조하게 됨)
                bool unlocked = isUnlocked(stageNum);
                if (unlocked) highestUnlocked = stageNum;
                string label = stageNum == 0 ? "튜토리얼" : $"스테이지 {stageNum}";
                SetStageButton(stageButtons[stageNum], label, unlocked, () =>
                {
                    stageSelectPanel.SetActive(false);
                    onSelectStage(capturedStageNum);
                });
            }
            CenterStageScroll(highestUnlocked);

            stageSelectBackButton.onClick.RemoveAllListeners();
            stageSelectBackButton.onClick.AddListener(() =>
            {
                stageSelectPanel.SetActive(false);
                onBack();
            });
        }

        // 지정한 스테이지 버튼이 가로 스크롤 뷰포트 한가운데 오도록 스크롤 위치를 옮김(내용이 뷰포트보다 좁으면 그냥 맨 왼쪽에 둠)
        private void CenterStageScroll(int stageNum)
        {
            if (stageScrollRect == null || stageButtonCenterX == null || stageNum < 0 || stageNum >= stageButtonCenterX.Length) return;

            float viewportWidth = stageScrollRect.viewport.rect.width;
            float contentWidth = stageScrollRect.content.rect.width;
            float scrollableWidth = contentWidth - viewportWidth;
            if (scrollableWidth <= 0f)
            {
                stageScrollRect.horizontalNormalizedPosition = 0f;
                return;
            }

            float targetOffset = stageButtonCenterX[stageNum] - viewportWidth / 2f;
            stageScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetOffset / scrollableWidth);
        }

        private static void SetStageButton(Button button, string label, bool unlocked, Action onEnter)
        {
            button.interactable = unlocked;
            button.GetComponentInChildren<Text>().text = unlocked ? label : $"{label}\n(잠김)";
            button.onClick.RemoveAllListeners();
            if (unlocked) button.onClick.AddListener(() => onEnter());
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
    }
}
