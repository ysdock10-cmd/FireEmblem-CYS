using System.Collections;
using System.Linq;

namespace SRPG
{
    public static class EnemyAI
    {
        public static IEnumerator TakeTurn(Unit enemy, GameManager gm, GridManager grid)
        {
            if (!enemy.IsAlive || enemy.hasActed) { enemy.hasActed = true; yield break; }

            var reachable = Pathfinding.GetReachableTiles(grid, enemy.position, enemy.MoveRange, enemy.team);
            var players = gm.PlayerUnits.Where(u => u.IsAlive).ToList();

            GridPosition bestTile = enemy.position;
            Unit bestTarget = null;
            int bestScore = int.MinValue;

            foreach (var tile in reachable.Keys)
            {
                var targetsInRange = players.Where(p =>
                    tile.ManhattanDistance(p.position) >= enemy.weapon.minRange &&
                    tile.ManhattanDistance(p.position) <= enemy.weapon.maxRange);

                foreach (var target in targetsInRange)
                {
                    var forecast = CombatCalculator.ComputeForecast(enemy, target, tile, target.position, grid);
                    int score = ScoreForecast(forecast, target);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTile = tile;
                        bestTarget = target;
                    }
                }
            }

            GridPosition moveTarget = bestTile;
            if (bestTarget == null && players.Count > 0)
            {
                var nearestPlayer = players.OrderBy(p => enemy.position.ManhattanDistance(p.position)).First();
                moveTarget = reachable.Keys.OrderBy(t => t.ManhattanDistance(nearestPlayer.position)).First();
            }

            if (moveTarget != enemy.position)
            {
                var path = Pathfinding.GetPath(grid, enemy.position, moveTarget, enemy.MoveRange, enemy.team);
                yield return MovementAnimator.AnimateAlongPath(enemy, path, grid);
                enemy.SetGridPosition(grid, moveTarget);
            }

            if (bestTarget != null && bestTarget.IsAlive)
            {
                gm.ui.ShowCombatMiniInfo(enemy, bestTarget);
                yield return CombatAnimator.PlaySequence(gm.ui, enemy, bestTarget, grid, gm.cameraController, log => gm.ui.AddBattleLog(log));
                gm.ui.HideCombatMiniInfo();
                gm.RemoveDeadFromGrid();
            }

            enemy.hasActed = true;
            enemy.RefreshActedVisual();
        }

        private static int ScoreForecast(CombatForecast f, Unit target)
        {
            int lethalBonus = f.attackerDamage >= target.currentHP ? 1000 : 0;
            int riskPenalty = f.defenderCanCounter ? f.defenderDamage : 0;
            return f.attackerDamage + lethalBonus - riskPenalty;
        }
    }
}
