using System.Collections.Generic;

namespace SRPG
{
    public static class Pathfinding
    {
        // 다익스트라 확장 본체. 도달 비용(costSoFar)과 함께 각 칸에 처음 도달했을 때 어디서 왔는지(cameFrom)도 기록해 경로 복원에 씀
        private static (Dictionary<GridPosition, int> cost, Dictionary<GridPosition, GridPosition> cameFrom) RunDijkstra(GridManager grid, GridPosition start, int moveRange, Team team)
        {
            var costSoFar = new Dictionary<GridPosition, int> { [start] = 0 };
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            var frontier = new List<GridPosition> { start };

            while (frontier.Count > 0)
            {
                frontier.Sort((a, b) => costSoFar[a].CompareTo(costSoFar[b]));
                var current = frontier[0];
                frontier.RemoveAt(0);

                foreach (var dir in GridPosition.Directions)
                {
                    var next = current + dir;
                    if (!grid.InBounds(next) || !grid.IsWalkable(next)) continue;

                    var occupant = grid.GetOccupant(next);
                    if (occupant != null && occupant.team != team) continue;

                    int stepCost = Terrain.Get(grid.GetTileType(next)).moveCost;
                    int newCost = costSoFar[current] + stepCost;
                    if (newCost > moveRange) continue;
                    if (costSoFar.TryGetValue(next, out int existing) && existing <= newCost) continue;

                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Add(next);
                }
            }

            return (costSoFar, cameFrom);
        }

        // 이동 가능 범위: 아군이 서 있는 칸은 지나갈 수 있지만 멈출 수 없고, 적이 서 있는 칸은 아예 지나갈 수 없음
        public static Dictionary<GridPosition, int> GetReachableTiles(GridManager grid, GridPosition start, int moveRange, Team team)
        {
            var (costSoFar, _) = RunDijkstra(grid, start, moveRange, team);

            var result = new Dictionary<GridPosition, int>();
            foreach (var kv in costSoFar)
            {
                var occ = grid.GetOccupant(kv.Key);
                if (occ != null && kv.Key != start) continue;
                result[kv.Key] = kv.Value;
            }
            return result;
        }

        // start에서 goal까지 실제로 밟는 경로(시작 칸 포함, 가로/세로 이동만)를 순서대로 반환. 도달 불가하면 시작 칸만 담은 리스트를 반환
        // 이동 애니메이션이 대각선으로 벽을 뚫고 지나가지 않고, 반드시 갈 수 있는 칸만 밟으며 이동하도록 하는 데 씀
        public static List<GridPosition> GetPath(GridManager grid, GridPosition start, GridPosition goal, int moveRange, Team team)
        {
            if (start == goal) return new List<GridPosition> { start };

            var (costSoFar, cameFrom) = RunDijkstra(grid, start, moveRange, team);
            if (!costSoFar.ContainsKey(goal)) return new List<GridPosition> { start };

            var path = new List<GridPosition> { goal };
            var cur = goal;
            while (cur != start)
            {
                cur = cameFrom[cur];
                path.Add(cur);
            }
            path.Reverse();
            return path;
        }

        public static List<GridPosition> GetTilesInRange(GridManager grid, GridPosition center, int minRange, int maxRange)
        {
            var result = new List<GridPosition>();
            for (int dx = -maxRange; dx <= maxRange; dx++)
            {
                int remaining = maxRange - System.Math.Abs(dx);
                for (int dy = -remaining; dy <= remaining; dy++)
                {
                    var p = new GridPosition(center.x + dx, center.y + dy);
                    if (!grid.InBounds(p)) continue;
                    int dist = center.ManhattanDistance(p);
                    if (dist < minRange || dist > maxRange) continue;
                    result.Add(p);
                }
            }
            return result;
        }
    }
}
