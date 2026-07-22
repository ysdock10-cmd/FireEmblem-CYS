using System;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 튜토리얼 스테이지(0번) 전용 안내 말풍선. 화면 위쪽에 배너로 뜨고, "확인"을 눌러야 닫힘
    public partial class UIManager
    {
        private GameObject tutorialPanel;
        private Text tutorialText;
        private Button tutorialConfirmButton;
        // 설명창이 떠 있는 동안 화면 전체를 덮어, "확인" 버튼 말고는 아무것도(다른 버튼/그리드 칸) 눌리지 않게 막는 투명 차단막
        private GameObject tutorialBlockerPanel;

        // 안내 문구가 떠 있는 동안은 PlayerController가 탭/드래그 입력을 무시해서, "확인"을 눌러야만 다음 행동을 할 수 있게 함
        public bool IsShowingTutorialMessage => tutorialPanel != null && tutorialPanel.activeSelf;

        private void BuildTutorialPanel()
        {
            // 차단막: 화면 전체를 덮는 투명(하지만 레이캐스트는 막는) 패널. 설명창보다 먼저 만들어서 항상 그 아래(뒤)에 깔리게 함
            tutorialBlockerPanel = new GameObject("TutorialBlocker");
            tutorialBlockerPanel.transform.SetParent(canvasRoot, false);
            var blockerRt = tutorialBlockerPanel.AddComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero; blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero; blockerRt.offsetMax = Vector2.zero;
            var blockerImg = tutorialBlockerPanel.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0f);
            tutorialBlockerPanel.SetActive(false);

            tutorialPanel = new GameObject("TutorialPanel");
            tutorialPanel.transform.SetParent(canvasRoot, false);
            var rt = tutorialPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.80f);
            rt.anchorMax = new Vector2(0.94f, 0.98f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = tutorialPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.09f, 0.92f);

            tutorialText = CreateRegionText(tutorialPanel.transform, new Vector2(0f, 0f), new Vector2(0.78f, 1f), TextAnchor.MiddleLeft, 20, 16f, 10f);
            tutorialConfirmButton = CreateButton(tutorialPanel.transform, "TutorialConfirmButton", new Vector2(1f, 0.5f), new Vector2(-16f, 0f), 140, 56, "확인");

            tutorialPanel.SetActive(false);
        }

        // 안내 문구를 띄움. onConfirm은 "확인"을 눌러 닫은 뒤 실행할 추가 동작이 필요할 때만 넘기면 됨(대부분 null로 충분)
        public void ShowTutorialMessage(string text, Action onConfirm)
        {
            tutorialText.text = text;
            tutorialConfirmButton.onClick.RemoveAllListeners();
            tutorialConfirmButton.onClick.AddListener(() =>
            {
                tutorialPanel.SetActive(false);
                tutorialBlockerPanel.SetActive(false);
                onConfirm?.Invoke();
            });
            tutorialBlockerPanel.SetActive(true);
            tutorialBlockerPanel.transform.SetAsLastSibling();
            tutorialPanel.SetActive(true);
            tutorialPanel.transform.SetAsLastSibling(); // 차단막보다도 위(나중)에 와야 확인 버튼 자체는 눌림
        }

        public void HideTutorialMessage()
        {
            tutorialPanel.SetActive(false);
            tutorialBlockerPanel.SetActive(false);
        }
    }
}
