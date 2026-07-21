using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SRPG
{
    public enum InputState { Idle, UnitSelected, MenuOpen, WeaponSelect, TargetSelected, Animating }

    public class PlayerController : MonoBehaviour
    {
        public GameManager gameManager;
        public GridManager grid;
        public UIManager ui;
        public CameraController cameraController;

        private InputState state = InputState.Idle;
        private Unit selectedUnit;
        private GridPosition originalPosition;
        private Dictionary<GridPosition, int> reachableTiles;
        private List<Unit> attackableTargets = new List<Unit>();
        private Unit hoveredTarget;
        // 적을 한 번 탭해서 "확정 대기" 상태로 지정해 둔 타겟(전투 예측창 + 공격 버튼이 이 타겟 기준으로 떠 있음).
        // null이 아닌 동안은 마우스를 움직여도 미리보기가 이걸 덮어쓰지 않고, 이 타겟을 다시 탭하거나 공격 버튼을 눌러야 실제로 전투가 시작됨
        private Unit pendingTarget;
        private WeaponData previewWeapon;

        private void Update()
        {
            if (gameManager.phase != TurnPhase.Player) return;
            if (Mouse.current == null) return;

            // 아직 타겟을 확정 대기 상태로 찍지 않았을 때만 마우스 위치로 미리보기를 갱신함(확정 대기 중엔 탭으로만 바뀜)
            if (state == InputState.TargetSelected && pendingTarget == null)
                UpdateHoveredTarget();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (state == InputState.MenuOpen) CancelMoveFromMenu();
                else if (state == InputState.WeaponSelect) CancelWeaponSelect();
                else HandleCancel();
            }
        }

        private GridPosition ScreenToGrid(Vector2 screenPos)
        {
            var cam = Camera.main;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            return grid.WorldToGrid(world);
        }

        private void UpdateHoveredTarget()
        {
            var gp = ScreenToGrid(Mouse.current.position.ReadValue());
            var target = attackableTargets.FirstOrDefault(t => t.position == gp);
            if (target == hoveredTarget) return;

            hoveredTarget = target;
            if (target != null) RefreshForecastFor(target);
            else ui.ClearForecast();
        }

        // 지금 예측창에 띄울 타겟 기준으로 전투 예측을 다시 계산해서 보여줌(공격/무기변경 버튼은 확정 대기 중일 때만 같이 띄움)
        private void RefreshForecastFor(Unit target)
        {
            var forecast = CombatCalculator.ComputeForecast(selectedUnit, target, grid);
            ui.ShowForecast(selectedUnit, target, forecast);
            if (pendingTarget != null)
            {
                ui.ShowAttackConfirmButton(() => ExecuteAttack(target));
                // 지금 사거리가 닿는 무기가 둘 이상일 때만 무기변경 버튼을 띄움(바꿀 게 없으면 버튼 자체를 안 보여줌)
                if (UsableWeaponsAgainst(target).Count > 1)
                    ui.ShowWeaponChangeButton(() => CycleAttackerWeapon(target));
            }
        }

        // 지금 이 타겟까지 사거리가 닿는(=실제로 장착해서 쓸 수 있는) 무기들만, 보유 순서대로 골라줌
        private List<WeaponData> UsableWeaponsAgainst(Unit target)
        {
            int dist = selectedUnit.position.ManhattanDistance(target.position);
            return selectedUnit.weaponSlots
                .Where(w => w != null && dist >= w.minRange && dist <= w.maxRange)
                .ToList();
        }

        // "무기변경" 버튼을 눌렀을 때: 지금 이 타겟까지 사거리가 닿는 무기들 중, 보유 순서대로 다음 무기로 갈아 끼움
        private void CycleAttackerWeapon(Unit target)
        {
            var usable = UsableWeaponsAgainst(target);
            if (usable.Count <= 1) return;

            int idx = usable.IndexOf(selectedUnit.weapon);
            selectedUnit.weapon = usable[(idx + 1) % usable.Count];

            RefreshMatchupIndicators();
            RefreshForecastFor(target);
        }

        public void HandleTap(Vector2 screenPos)
        {
            if (gameManager.phase != TurnPhase.Player) return;
            if (state == InputState.MenuOpen) return;

            var gp = ScreenToGrid(screenPos);
            if (!grid.InBounds(gp)) return;

            switch (state)
            {
                case InputState.Idle:
                    TrySelectUnit(gp);
                    break;
                case InputState.UnitSelected:
                    TryMoveOrReselect(gp);
                    break;
                case InputState.WeaponSelect:
                    // 무기를 미리 본(누른) 상태에서 사거리 안의 적을 바로 탭하면, 무기 확정을 또 누를 필요 없이 곧장 전투예측으로 넘어감
                    TryConfirmWeaponAndTarget(gp);
                    break;
                case InputState.TargetSelected:
                    TryConfirmAttack(gp);
                    break;
            }
        }

        public bool CanDragUnitAt(Vector2 screenPos)
        {
            if (gameManager.phase != TurnPhase.Player) return false;
            if (state != InputState.Idle && state != InputState.UnitSelected) return false;

            var gp = ScreenToGrid(screenPos);
            if (!grid.InBounds(gp)) return false;
            var unit = grid.GetOccupant(gp);
            // 이미 선택된 유닛의 자리를 다시 누르면 드래그 시작이 아니라 일반 탭으로 처리해서,
            // TryMoveOrReselect -> ConfirmDestination(제자리 이동)을 거쳐 행동 선택창이 뜨게 함
            if (unit == selectedUnit) return false;
            return unit != null && unit.team == Team.Player && !unit.hasActed;
        }

        public void HandleUnitDragRelease(Vector2 pressScreenPos, Vector2 releaseScreenPos)
        {
            var pressGp = ScreenToGrid(pressScreenPos);
            if (!grid.InBounds(pressGp)) return;

            var unit = grid.GetOccupant(pressGp);
            if (unit == null || unit.team != Team.Player || unit.hasActed) return;

            var releaseGp = ScreenToGrid(releaseScreenPos);
            var target = grid.InBounds(releaseGp) ? grid.GetOccupant(releaseGp) : null;

            if (target != null && target.team == Team.Enemy && target.IsAlive)
                TryQuickAttack(unit, target);
            else
                TrySelectUnit(pressGp);
        }

        // 사거리 안에 들어오는 도달 가능한 칸 중, 다른 적과 멀고 이동 비용이 낮은 곳을 고름
        private GridPosition? FindBestAttackTile(Unit unit, Unit target, Dictionary<GridPosition, int> reach)
        {
            GridPosition? best = null;
            float bestScore = float.NegativeInfinity;

            foreach (var kv in reach)
            {
                var tile = kv.Key;
                int dist = tile.ManhattanDistance(target.position);
                if (dist < unit.weapon.minRange || dist > unit.weapon.maxRange) continue;

                int nearestOtherEnemyDist = 10;
                foreach (var e in gameManager.EnemyUnits)
                {
                    if (!e.IsAlive || e == target) continue;
                    nearestOtherEnemyDist = Mathf.Min(nearestOtherEnemyDist, tile.ManhattanDistance(e.position));
                }

                float score = nearestOtherEnemyDist * 2f - kv.Value;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }
            return best;
        }

        private void TryQuickAttack(Unit unit, Unit target)
        {
            var reach = Pathfinding.GetReachableTiles(grid, unit.position, unit.MoveRange, unit.team);
            var bestTile = FindBestAttackTile(unit, target, reach);

            if (bestTile == null)
            {
                TrySelectUnit(unit.position);
                return;
            }

            selectedUnit = unit;
            originalPosition = unit.position;
            grid.ClearHighlights();
            state = InputState.Animating;
            StartCoroutine(AnimateQuickAttackMove(unit, target, bestTile.Value));
        }

        // 적을 직접 탭해서 공격 칸까지 이동하는 경우도, 이동 메뉴를 거칠 때와 같은 방식(가로/세로로 한 칸씩)으로 걸어가게 함
        private IEnumerator AnimateQuickAttackMove(Unit unit, Unit target, GridPosition destTile)
        {
            var path = Pathfinding.GetPath(grid, unit.position, destTile, unit.MoveRange, unit.team);
            yield return MovementAnimator.AnimateAlongPath(unit, path, grid);

            unit.SetGridPosition(grid, destTile);
            cameraController.FocusOn(destTile);

            attackableTargets = gameManager.EnemyUnits.Where(e => e.IsAlive &&
                destTile.ManhattanDistance(e.position) >= unit.weapon.minRange &&
                destTile.ManhattanDistance(e.position) <= unit.weapon.maxRange).ToList();

            var rangeTiles = Pathfinding.GetTilesInRange(grid, destTile, unit.weapon.minRange, unit.weapon.maxRange);
            grid.ShowHighlights(rangeTiles, HighlightType.Attack);
            ui.ShowSelectedUnit(unit, HandleCancel);

            state = InputState.TargetSelected;
            SelectPendingTarget(target);
        }

        private void TrySelectUnit(GridPosition gp)
        {
            var unit = grid.GetOccupant(gp);

            if (unit == null)
            {
                ui.HideSelectedUnit();
                return;
            }

            // 적이거나 이미 행동을 마친 아군은 이동/공격은 못 시키지만, 정보창은 똑같이 띄워서 확인할 수 있게 함
            if (unit.team == Team.Enemy || unit.hasActed)
            {
                ui.ShowSelectedUnit(unit);
                return;
            }

            selectedUnit = unit;
            originalPosition = unit.position;
            ui.ShowSelectedUnit(unit);
            ShowMoveAndAttackRange(unit);
            RefreshMatchupIndicators();
        }

        // 선택된 유닛이 지금 들고 있는(또는 무기 선택창에서 미리 보는) 무기 기준으로,
        // 화면에 있는 모든 적에게 브레이크를 주는지(X)/당하는지(불꽃) 표시
        private void RefreshMatchupIndicators()
        {
            if (selectedUnit == null) return;
            RefreshMatchupIndicators(selectedUnit.weapon.type);
        }

        private void RefreshMatchupIndicators(WeaponType attackerWeaponType)
        {
            foreach (var enemy in gameManager.EnemyUnits.Where(e => e.IsAlive))
            {
                int advantage = WeaponTriangle.GetAdvantage(attackerWeaponType, enemy.weapon.type);
                enemy.SetMatchupIndicator(advantage > 0 ? MatchupIndicator.CanBreak
                    : advantage < 0 ? MatchupIndicator.WillBeBroken
                    : MatchupIndicator.None);
            }
        }

        private void ClearMatchupIndicators()
        {
            foreach (var enemy in gameManager.EnemyUnits.Where(e => e.IsAlive))
                enemy.SetMatchupIndicator(MatchupIndicator.None);
        }

        private void ShowMoveAndAttackRange(Unit unit)
        {
            reachableTiles = Pathfinding.GetReachableTiles(grid, unit.position, unit.MoveRange, unit.team);

            // 공격범위 미리보기는 현재 장착 무기가 아니라, 보유 무기 중 가장 사거리가 긴 무기 기준으로 보여줌
            var previewRangeWeapon = unit.LongestRangeWeapon;
            var atkTiles = new HashSet<GridPosition>();
            foreach (var t in reachableTiles.Keys)
                foreach (var r in Pathfinding.GetTilesInRange(grid, t, previewRangeWeapon.minRange, previewRangeWeapon.maxRange))
                    if (!reachableTiles.ContainsKey(r)) atkTiles.Add(r);

            grid.ClearHighlights();
            grid.ShowHighlights(reachableTiles.Keys, HighlightType.Move);
            grid.ShowHighlights(atkTiles, HighlightType.Attack);

            cameraController.FocusOn(unit.position);
            state = InputState.UnitSelected;
        }

        private void TryMoveOrReselect(GridPosition gp)
        {
            var occupant = grid.GetOccupant(gp);
            if (occupant != null && occupant != selectedUnit && occupant.team == Team.Player && !occupant.hasActed)
            {
                CancelSelection();
                TrySelectUnit(gp);
                return;
            }
            // 적을 직접 클릭하면 공격 가능한 칸으로 이동해 바로 공격창을 띄움
            if (occupant != null && occupant.team == Team.Enemy && occupant.IsAlive)
            {
                TryQuickAttack(selectedUnit, occupant);
                return;
            }
            // 사거리(이동 가능 칸) 밖을 누르면 아무 반응 없이 무시하지 않고 선택을 취소함
            if (!reachableTiles.ContainsKey(gp))
            {
                CancelSelection();
                return;
            }
            ConfirmDestination(gp);
        }

        private void ConfirmDestination(GridPosition gp)
        {
            state = InputState.Animating;
            StartCoroutine(AnimateAndConfirmDestination(gp));
        }

        // 적군과 같은 방식(가로/세로로 한 칸씩)으로 걸어서 이동하게 함. 기존엔 순간이동하듯 한번에 옮겨져 어색했음
        private IEnumerator AnimateAndConfirmDestination(GridPosition gp)
        {
            var path = Pathfinding.GetPath(grid, selectedUnit.position, gp, selectedUnit.MoveRange, selectedUnit.team);
            yield return MovementAnimator.AnimateAlongPath(selectedUnit, path, grid);

            selectedUnit.SetGridPosition(grid, gp);
            cameraController.FocusOn(gp);

            // 장착 중인 무기만이 아니라, 가진 무기 중 하나라도 닿는 적이 있으면 공격 버튼을 켬(무기 교체로 공격 가능하므로)
            attackableTargets = TargetsInRangeOfAnyWeapon(selectedUnit, gp);

            grid.ClearHighlights();
            OpenActionMenu();
        }

        // 유닛이 가진 무기 슬롯 중 하나라도 사거리가 닿는 적들을 전부 모음(중복 제거)
        private List<Unit> TargetsInRangeOfAnyWeapon(Unit unit, GridPosition from)
        {
            var targets = new List<Unit>();
            foreach (var w in unit.weaponSlots)
            {
                if (w == null) continue;
                foreach (var e in gameManager.EnemyUnits)
                {
                    if (!e.IsAlive || targets.Contains(e)) continue;
                    int dist = from.ManhattanDistance(e.position);
                    if (dist >= w.minRange && dist <= w.maxRange) targets.Add(e);
                }
            }
            return targets;
        }

        // 무기 슬롯마다, 그 무기의 사거리가 실제로 닿는 적이 있는지(=무기 선택창에서 고를 수 있는지)를 계산
        private bool[] ComputeWeaponUsability(Unit unit, GridPosition from)
        {
            var usable = new bool[unit.weaponSlots.Length];
            for (int i = 0; i < unit.weaponSlots.Length; i++)
            {
                var w = unit.weaponSlots[i];
                usable[i] = w != null && gameManager.EnemyUnits.Any(e => e.IsAlive &&
                    from.ManhattanDistance(e.position) >= w.minRange &&
                    from.ManhattanDistance(e.position) <= w.maxRange);
            }
            return usable;
        }

        private void OpenActionMenu()
        {
            state = InputState.MenuOpen;
            ui.ShowActionMenu(attackableTargets.Count > 0, HandleAttackChosen, HandleWaitChosen, CancelMoveFromMenu);
        }

        private void HandleAttackChosen()
        {
            ui.HideActionMenu();
            state = InputState.WeaponSelect;
            previewWeapon = null;
            ui.ShowWeaponSelect(selectedUnit.weaponSlots, ComputeWeaponUsability(selectedUnit, selectedUnit.position), PreviewWeapon, ConfirmWeaponChoice, CancelWeaponSelect);
        }

        // 무기를 누르기만 한 상태: 아직 확정 전이므로 사거리만 미리 보여줌
        private void PreviewWeapon(WeaponData w)
        {
            previewWeapon = w;
            grid.ClearHighlights();
            var rangeTiles = Pathfinding.GetTilesInRange(grid, selectedUnit.position, w.minRange, w.maxRange);
            grid.ShowHighlights(rangeTiles, HighlightType.Attack);
            RefreshMatchupIndicators(w.type); // 미리 보는 무기 기준으로 상성 표시도 같이 갱신
        }

        // 확인 버튼을 눌러야 실제로 그 무기를 장착하고 타겟 선택 단계로 넘어감
        private void ConfirmWeaponChoice()
        {
            if (previewWeapon == null) return;
            EquipWeaponAndEnterTargeting(previewWeapon);
        }

        // 무기 선택창에서 무기를 미리 본(누른) 상태로 사거리 안의 적을 그리드에서 바로 탭하면,
        // 확인 버튼을 또 누를 필요 없이 그 무기를 장착하고 곧장 전투예측(TargetSelected)까지 진행함
        private void TryConfirmWeaponAndTarget(GridPosition gp)
        {
            if (previewWeapon == null) return;

            var target = grid.GetOccupant(gp);
            if (target == null || target.team != Team.Enemy || !target.IsAlive) return;

            int dist = selectedUnit.position.ManhattanDistance(gp);
            if (dist < previewWeapon.minRange || dist > previewWeapon.maxRange) return;

            var w = previewWeapon;
            EquipWeaponAndEnterTargeting(w);
            SelectPendingTarget(target);
        }

        private void EquipWeaponAndEnterTargeting(WeaponData w)
        {
            selectedUnit.weapon = w;
            ui.HideWeaponSelect();
            hoveredTarget = null;

            attackableTargets = gameManager.EnemyUnits.Where(e => e.IsAlive &&
                selectedUnit.position.ManhattanDistance(e.position) >= w.minRange &&
                selectedUnit.position.ManhattanDistance(e.position) <= w.maxRange).ToList();

            ui.ShowSelectedUnit(selectedUnit, HandleCancel);
            state = InputState.TargetSelected;
            pendingTarget = null;
            RefreshMatchupIndicators();
        }

        private void CancelWeaponSelect()
        {
            ui.HideWeaponSelect();
            grid.ClearHighlights();
            previewWeapon = null;
            RefreshMatchupIndicators(); // 미리보기 취소 -> 실제 장착 무기 기준으로 되돌림
            OpenActionMenu();
        }

        private void HandleWaitChosen()
        {
            ui.HideActionMenu();
            EndUnitAction();
        }

        private void CancelMoveFromMenu()
        {
            ui.HideActionMenu();
            selectedUnit.SetGridPosition(grid, originalPosition);
            ShowMoveAndAttackRange(selectedUnit);
        }

        // 사거리 내 적을 탭했을 때: 아직 확정 대기 중인 타겟이 아니면(처음 선택했거나 다른 적으로 바꾸는 경우) 선택만 하고 전투 예측창+공격 버튼을 띄움.
        // 이미 확정 대기 중인 그 적을 다시 탭하면(같은 적을 한 번 더 선택) 바로 공격을 확정함
        private void TryConfirmAttack(GridPosition gp)
        {
            var target = attackableTargets.FirstOrDefault(t => t.position == gp);
            if (target == null) return;

            if (pendingTarget == target)
            {
                ExecuteAttack(target);
                return;
            }

            SelectPendingTarget(target);
        }

        // 적을 탭 1회로 "선택"만 함(아직 공격 확정은 아님): 전투 예측창을 띄우고 공격 버튼을 노출.
        // 이미 다른 적이 선택된 상태에서 사거리 내 다른 적을 클릭하면 이 함수가 다시 호출되어 타겟이 바뀜
        private void SelectPendingTarget(Unit target)
        {
            pendingTarget = target;
            hoveredTarget = target;
            RefreshForecastFor(target);
        }

        // 공격 버튼을 누르거나, 확정 대기 중인 타겟을 다시 탭하면 실제로 전투를 시작함
        private void ExecuteAttack(Unit target)
        {
            ui.HideAllBattleUI();
            ui.ShowCombatMiniInfo(selectedUnit, target);
            hoveredTarget = null;
            pendingTarget = null;
            grid.ClearHighlights();
            state = InputState.Animating;
            StartCoroutine(RunPlayerCombat(selectedUnit, target));
        }

        private IEnumerator RunPlayerCombat(Unit attacker, Unit target)
        {
            yield return CombatAnimator.PlaySequence(ui, attacker, target, grid, cameraController, log => ui.AddBattleLog(log));
            ui.HideCombatMiniInfo();
            gameManager.RemoveDeadFromGrid();
            gameManager.CheckGameOver();
            EndUnitAction();
        }

        private void HandleCancel()
        {
            if (state == InputState.UnitSelected)
                CancelSelection();
            else if (state == InputState.TargetSelected)
            {
                grid.ClearHighlights();
                ui.ClearForecast();
                ui.HideSelectedUnit();
                hoveredTarget = null;
                pendingTarget = null;
                OpenActionMenu();
            }
        }

        private void CancelSelection()
        {
            grid.ClearHighlights();
            selectedUnit = null;
            ui.HideSelectedUnit();
            ClearMatchupIndicators();
            state = InputState.Idle;
        }

        private void EndUnitAction()
        {
            grid.ClearHighlights();
            ui.ClearForecast();
            hoveredTarget = null;
            pendingTarget = null;
            if (selectedUnit != null)
            {
                selectedUnit.hasActed = true;
                selectedUnit.RefreshActedVisual();
            }
            selectedUnit = null;
            ui.HideSelectedUnit();
            ClearMatchupIndicators();
            state = InputState.Idle;

            if (gameManager.phase == TurnPhase.Player && gameManager.PlayerUnits.Where(u => u.IsAlive).All(u => u.hasActed))
                gameManager.RequestEndPlayerPhase();
        }
    }
}
