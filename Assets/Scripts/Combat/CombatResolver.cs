using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    // 히트 한 번의 굴림 결과(실제 적용할 피해량). RollHit에서 채워 ApplyHit에 전달한다.
    public struct HitOutcome
    {
        public int damage;
    }

    public static class CombatResolver
    {
        // 공격 코인(공격자)과 방어 코인(방어자)을 동시에(각자 자기 자리에서) 던져 실제 피해량을 계산한다(명중 판정 없이 항상 적중).
        // 애니메이션 타이밍과 맞추기 위해 언제 굴릴지는 호출자(CombatAnimator)가 결정하며,
        // 코인을 굴리는 연출을 공격 모션보다 먼저 보여준 뒤, 모션이 끝나면 ApplyHit으로 실제 피해를 적용한다.
        // baseDamage(무기 위력 - 지형 보정, 코인과 무관한 고정값)에서 시작해, 코인이 하나 멈출 때마다
        // 그 결과값을 화면에서 즉시 더하거나(공격 코인) 빼서(방어 코인) 최종 데미지를 눈으로 보여주며 계산한다.
        // 공격 코인 쪽과 방어 코인 쪽을 각각 별도 코루틴(UIManager.StartCoroutine)으로 동시에 굴려, 둘 다 끝날 때까지 기다린다.
        public static IEnumerator RollHit(UIManager ui, Unit attacker, Unit defender, int baseDamage, System.Action<HitOutcome> onRolled)
        {
            int damage = baseDamage;
            yield return ui.ShowDamageBase(damage);

            bool atkIsAllySlot = attacker.team == Team.Player;
            bool defIsAllySlot = defender.team == Team.Player;
            // 적이 공격/방어할 때는 플레이어가 탭할 이유가 없으므로 코인을 자동으로 멈추고, 색도 어둡게 해 "적의 코인"임을 표시
            bool isEnemyAtkCoin = attacker.team != Team.Player;
            bool isEnemyDefCoin = defender.team != Team.Player;
            // 브레이크 상태면 방어 코인을 던지지 않고 방어 보너스 없이 그대로 맞음
            bool rollDefCoins = !defender.isBroken;

            bool atkDone = false;
            bool defDone = !rollDefCoins;

            ui.StartCoroutine(RunCoinSide(ui, attacker.stats.atkCoins, atkIsAllySlot, isEnemyAtkCoin, isDefense: false,
                result => damage += result, () => atkDone = true));

            if (rollDefCoins)
            {
                ui.StartCoroutine(RunCoinSide(ui, defender.stats.defCoins, defIsAllySlot, isEnemyDefCoin, isDefense: true,
                    result => damage -= result, () => defDone = true));
            }

            yield return new WaitUntil(() => atkDone && defDone);

            damage = Mathf.Max(0, damage);
            ui.HideDamageTally();

            onRolled?.Invoke(new HitOutcome { damage = damage });
        }

        // 한쪽(공격 또는 방어)이 가진 코인들을 그 쪽 자리에서 순서대로 굴리고, 코인이 멈출 때마다 applyDamage로 데미지에 반영한 뒤
        // 눈에 보이는 집계 숫자도 갱신한다. 다 끝나면 onDone을 호출해 RollHit이 양쪽 완료를 알 수 있게 한다.
        private static IEnumerator RunCoinSide(UIManager ui, List<Coin> coins, bool isAllySlot, bool autoStop, bool isDefense,
            System.Action<int> applyDamage, System.Action onDone)
        {
            foreach (var coin in coins)
            {
                int result = 0;
                yield return ui.RunCoinFlip(isAllySlot, coin.heads, coin.tails, r => result = r, autoStop: autoStop, dim: autoStop, isDefense: isDefense);
                applyDamage(result);
                yield return ui.ApplyDamageTallyDelta(isDefense ? -result : result, isAllySlot);
            }
            onDone();
        }

        // RollHit의 결과를 실제로 적용한다(데미지 반영, 로그 기록). 공격 모션이 끝난 뒤 호출한다.
        public static void ApplyHit(Unit defender, HitOutcome outcome, List<string> log)
        {
            int dealt = defender.TakeDamage(outcome.damage);
            log.Add($"{dealt} 데미지!");

            if (!defender.IsAlive)
                log.Add($"{defender.unitName} 격파!");
        }
    }
}
