using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 범용 UI 생성 헬퍼(패널/버튼/텍스트 등 기본 부품 조립). 다른 partial 파일들이 이 메서드들로 구체적인 화면을 구성함
    public partial class UIManager
    {
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

        // 좌우로 스와이프해서 넘기는 가로 스크롤 영역을 만들고, 그 안에 항목을 채울 Content RectTransform을 돌려줌.
        // 실제 폭(sizeDelta.x)은 항목을 다 채운 뒤 호출한 쪽에서 정함(예: 스테이지 선택 화면)
        private RectTransform CreateHorizontalScroll(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var scrollGo = new GameObject("HScroll");
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = anchorMin; scrollRt.anchorMax = anchorMax;
            scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = Vector2.zero;
            // 드래그 판정을 받으려면 이 오브젝트에 라이캐스트 대상 그래픽이 있어야 하므로 옅은 배경을 겸해서 둠
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.2f);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.15f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero; viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            return contentRt;
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

        private static void EnableBestFit(Text text, int minSize, int maxSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
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
