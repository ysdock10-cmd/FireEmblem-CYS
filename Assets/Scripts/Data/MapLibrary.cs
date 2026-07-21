using UnityEngine;

namespace SRPG
{
    public static class MapLibrary
    {
        public const int Width = 20;
        public const int Height = 12;

        public static TileType[,] DefaultMap() => BuildMap(20240713);

        // 스테이지 2용 맵. 크기/스폰 구역은 DefaultMap과 동일하고 지형 배치 시드만 다름
        public static TileType[,] Stage2Map() => BuildMap(20260721);

        private static TileType[,] BuildMap(int seed)
        {
            var map = new TileType[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    map[x, y] = TileType.Plain;

            var rng = new System.Random(seed);

            void ScatterPatches(TileType type, int patchCount, int minSize, int maxSize, double density)
            {
                for (int i = 0; i < patchCount; i++)
                {
                    int cx = rng.Next(0, Width);
                    int cy = rng.Next(0, Height);
                    int size = rng.Next(minSize, maxSize + 1);
                    for (int dx = -size; dx <= size; dx++)
                    {
                        for (int dy = -size; dy <= size; dy++)
                        {
                            if (Mathf.Abs(dx) + Mathf.Abs(dy) > size) continue;
                            int x = cx + dx, y = cy + dy;
                            if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                            if (IsSpawnArea(x, y)) continue;
                            if (rng.NextDouble() < density) map[x, y] = type;
                        }
                    }
                }
            }

            ScatterPatches(TileType.Forest, 8, 1, 2, 0.75);
            ScatterPatches(TileType.Mountain, 5, 1, 2, 0.7);
            ScatterPatches(TileType.Water, 4, 1, 2, 0.65);
            ScatterPatches(TileType.Wall, 6, 0, 1, 0.9);

            SetSafe(map, Width / 2, Height / 2, TileType.Fort);
            SetSafe(map, Width / 2 + 3, Height / 2 - 2, TileType.Fort);
            SetSafe(map, Width / 2 - 3, Height / 2 + 2, TileType.Fort);

            return map;
        }

        // 아군/적 소환 구역(RosterLibrary의 시작 좌표, 이제 맵 중앙 근처)에는 지형을 배치하지 않아 유닛이 장애물 위에 겹치지 않게 함
        private static bool IsSpawnArea(int x, int y) =>
            (x >= 6 && x <= 9 && y >= 3 && y <= 7) || (x >= 10 && x <= 14 && y >= 5 && y <= 9);

        private static void SetSafe(TileType[,] map, int x, int y, TileType type)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) map[x, y] = type;
        }
    }
}
