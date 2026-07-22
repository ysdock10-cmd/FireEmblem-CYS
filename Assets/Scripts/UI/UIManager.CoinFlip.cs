using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 공격/방어 코인 던지기 연출과 실시간 데미지 집계 숫자
    public partial class UIManager
    {
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
    }
}
