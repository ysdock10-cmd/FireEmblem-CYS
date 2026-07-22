using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    public static class CombatAnimator
    {
        public static IEnumerator PlaySequence(UIManager ui, Unit attacker, Unit defender, GridManager grid, CameraController camera, Action<List<string>> onComplete)
        {
            Vector3 attackerStart = attacker.transform.position;
            Vector3 defenderStart = defender.transform.position;

            // 1) 맵에서 카메라가 두 유닛이 함께 보이는 중간 지점으로 이동 + 살짝 확대(15%)하고 끝날 때까지 기다림
            if (camera != null)
            {
                camera.FocusOnWorldPoint(Vector3.Lerp(attackerStart, defenderStart, 0.5f));
                camera.ZoomInForCombat();
                yield return new WaitUntil(() => !camera.IsFocusing && !camera.IsZooming);

                // 2) 화면 전환 직전 한 번 더 확 당겨서(펀치인) 전환감을 줌
                yield return camera.PunchZoomIn();
            }

            var log = new List<string>();
            var forecast = CombatCalculator.ComputeForecast(attacker, defender, grid);

            bool defenderCanCounter = forecast.defenderCanCounter;

            // 3) 화면 전환: 맵을 가리고 아군/적이 마주보는 VS 화면으로 진입. 이후 코인/공격 모션/피격 연출은 전부 이 화면 안에서 진행됨
            ui.ShowVsScreen(attacker, defender);

            // 선공: 코인을 먼저 굴려 결과를 정한 뒤(RollHit) 공격 모션을 재생하고,
            // 모션이 끝나면 그 결과를 실제로 적용(ApplyHit)해 로그/피격 연출과 1:1로 맞도록 함
            log.Add($"{attacker.unitName}의 공격!");
            HitOutcome firstOutcome = default;
            yield return CombatResolver.RollHit(ui, attacker, defender, forecast.attackerBaseDamage, result => firstOutcome = result);
            yield return ui.PlayVsLunge(attacker);
            CombatResolver.ApplyHit(defender, firstOutcome, log);

            // 브레이크: 무기 상성 우위인 쪽이 공격하면(명중 판정 없이 항상 적중) 상대는 이번 턴 반격 불가 (후속타는 없음)
            if (forecast.attackerHasAdvantage && defender.IsAlive)
            {
                defender.isBroken = true;
                defenderCanCounter = false;
                log.Add($"{defender.unitName} 브레이크!");
            }
            yield return ui.PlayVsHitReaction(defender, defender.isBroken);

            if (defenderCanCounter && defender.IsAlive)
            {
                log.Add($"{defender.unitName}의 반격!");
                HitOutcome counterOutcome = default;
                yield return CombatResolver.RollHit(ui, defender, attacker, forecast.defenderBaseDamage, result => counterOutcome = result);
                yield return ui.PlayVsLunge(defender);
                CombatResolver.ApplyHit(attacker, counterOutcome, log);

                if (forecast.defenderHasAdvantage && attacker.IsAlive)
                {
                    attacker.isBroken = true;
                    log.Add($"{attacker.unitName} 브레이크!");
                }
                yield return ui.PlayVsHitReaction(attacker, attacker.isBroken);
            }

            // 4) 체력바가 실제로 다 깎인 상태를 보여줄 때까지 기다린 뒤, 결과를 잠깐 더 보여주고(0.3초) 나서 VS 화면을 닫고 맵으로 복귀
            yield return ui.WaitForCombatMiniHpDrain();
            yield return new WaitForSeconds(0.3f);
            ui.HideVsScreen();
            camera?.ResetZoom();
            onComplete?.Invoke(log);
        }
    }
}
