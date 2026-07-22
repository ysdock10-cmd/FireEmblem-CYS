using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 전투 연출(코인+카메라 줌인) 중 뜨는 VS 대치 화면과 그 동안의 최소 정보창(combatMiniBar)
    public partial class UIManager
    {
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

        // 체력바가 실제 currentHP에 순간 스냅하지 않고 일정 속도로 "쭈욱" 깎여 보이도록, 화면에 보여줄 체력 값을 따로 들고 있다가
        // 매 프레임 실제 값 쪽으로 서서히 좁혀감(Unit의 지도 위 체력바 애니메이션과 같은 기준 속도)
        private const float CombatMiniHpDrainDuration = 0.8f; // 이 시간 안에 체력바 가득~빈 상태를 훑고 지나가는 속도로 환산해서 씀
        private float combatMiniAllyDisplayedHp;
        private float combatMiniEnemyDisplayedHp;

        // 전투 연출 시작/끝에서 호출: attacker/defender 중 실제로 플레이어 팀인 쪽을 아군 칸에, 나머지를 적 칸에 표시
        public void ShowCombatMiniInfo(Unit attacker, Unit defender)
        {
            combatMiniAllyUnit = attacker.team == Team.Player ? attacker : defender;
            combatMiniEnemyUnit = attacker.team == Team.Player ? defender : attacker;
            // 뜨는 순간엔 애니메이션 없이 현재 체력을 그대로 보여줌(새로 입는 피해부터만 서서히 깎임)
            combatMiniAllyDisplayedHp = combatMiniAllyUnit.currentHP;
            combatMiniEnemyDisplayedHp = combatMiniEnemyUnit.currentHP;
            combatMiniBar.SetActive(true);
            RefreshCombatMiniInfo();
        }

        public void HideCombatMiniInfo()
        {
            combatMiniBar.SetActive(false);
            combatMiniAllyUnit = null;
            combatMiniEnemyUnit = null;
        }

        // 체력바가 실제 currentHP까지 다 깎이는 걸 보여준 뒤에야 VS 화면을 닫도록, 호출 쪽(CombatAnimator)이 화면을 끝내기 전에 기다림
        public IEnumerator WaitForCombatMiniHpDrain()
        {
            yield return new WaitUntil(() =>
                (combatMiniAllyUnit == null || Mathf.Abs(combatMiniAllyDisplayedHp - combatMiniAllyUnit.currentHP) < 0.05f) &&
                (combatMiniEnemyUnit == null || Mathf.Abs(combatMiniEnemyDisplayedHp - combatMiniEnemyUnit.currentHP) < 0.05f));
        }

        // 코인 결과로 데미지가 반영되거나(ApplyHit) 브레이크가 걸리는 등 유닛 상태가 실시간으로 바뀌므로,
        // 켜져 있는 동안 매 프레임(Update) 다시 읽어와 이름/체력바/브레이크 표시를 최신 상태로 유지함
        private void RefreshCombatMiniInfo()
        {
            RefreshCombatMiniSide(combatMiniAllyUnit, combatMiniAllyName, combatMiniAllyHpText, combatMiniAllyHpFill, combatMiniAllyBreakText, ref combatMiniAllyDisplayedHp);
            RefreshCombatMiniSide(combatMiniEnemyUnit, combatMiniEnemyName, combatMiniEnemyHpText, combatMiniEnemyHpFill, combatMiniEnemyBreakText, ref combatMiniEnemyDisplayedHp);
        }

        private void RefreshCombatMiniSide(Unit u, Text nameText, Text hpText, RectTransform hpFill, Text breakText, ref float displayedHp)
        {
            if (u == null) return;
            nameText.text = u.unitName;
            hpText.text = $"{u.currentHP}/{u.stats.maxHP}";

            float maxHP = Mathf.Max(1, u.stats.maxHP);
            float drainSpeed = maxHP / CombatMiniHpDrainDuration; // 초당 깎이는 체력량(절대값). 데미지가 크든 작든 항상 같은 속도로 훑고 지나가게 함
            displayedHp = Mathf.MoveTowards(displayedHp, u.currentHP, drainSpeed * Time.deltaTime);

            float frac = Mathf.Clamp01(displayedHp / maxHP);
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
    }
}
