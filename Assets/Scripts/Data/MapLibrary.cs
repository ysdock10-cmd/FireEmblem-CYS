namespace SRPG
{
    // 스테이지마다 손으로 설계한 서로 다른 지형(랜덤 산포 대신 고정 좌표)을 씀:
    // 0 튜토리얼 / 1 평원 / 2 강 / 3 산악 요새 / 4 협곡(S자 통로) / 5 늪지(대각선 습지대) / 6 최종 요새(이중 관문).
    // 아군 스폰(RosterLibrary.PlayerSquad, x=7~8/y=4~6)은 모든 스테이지가 공유하므로 모든 맵이 이 구역을 반드시 평지로 비워 둠
    public static class MapLibrary
    {
        public const int Width = 20;
        public const int Height = 12;

        // 튜토리얼: 장애물은 숲/산 한 칸씩뿐인 아주 단순한 연습용 맵. 이동 비용이 지형마다 다르다는 것만 살짝 보여줌
        public static TileType[,] TutorialMap()
        {
            var map = Blank();
            SetAll(map, TileType.Forest, (9, 4));
            SetAll(map, TileType.Mountain, (9, 7));
            return map;
        }

        // 스테이지 1: 평원 - 지형 장애물이 거의 없는 가장 쉬운 맵. 아군/적 스폰 사이 중앙에 요새 타일 두 칸만 고지대로 둠
        public static TileType[,] DefaultMap()
        {
            var map = Blank();
            SetAll(map, TileType.Forest,
                (3, 1), (4, 1), (3, 2), (4, 2),
                (15, 1), (16, 1), (15, 2),
                (4, 9), (5, 9), (4, 10),
                (16, 9), (17, 9), (16, 10));
            SetAll(map, TileType.Fort, (10, 5), (10, 6));
            return map;
        }

        // 스테이지 2: 숲과 강 - 아군(x=7~8)과 적(x=11~14) 스폰 사이(x=9~10)를 세로로 가로지르는 강이 전장을 반으로 가름.
        // 위/아래 두 군데(y=1~2, y=9~10)만 걸어서 건널 수 있는 여울이라, 그 두 통로를 두고 공방이 벌어지도록 유도함
        public static TileType[,] Stage2Map()
        {
            var map = Blank();
            for (int y = 0; y < Height; y++)
            {
                bool ford = (y >= 1 && y <= 2) || (y >= 9 && y <= 10);
                if (ford) continue;
                SetSafe(map, 9, y, TileType.Water);
                SetSafe(map, 10, y, TileType.Water);
            }
            // 강 양쪽 기슭의 숲(여울 근처 매복/엄폐 포인트)
            SetAll(map, TileType.Forest,
                (7, 0), (8, 0), (7, 1),
                (11, 0), (12, 0), (12, 1),
                (7, 10), (7, 11), (8, 11),
                (11, 11), (12, 10), (12, 11));
            // 여울 위에 요새 타일을 둬서 통로를 먼저 차지하는 쪽이 방어 보너스를 가져가게 함
            SetAll(map, TileType.Fort, (9, 1), (10, 1));
            return map;
        }

        // 스테이지 3: 산악 요새 - 적 스폰(x=14 이후) 앞을 산맥(x=12~13)이 가로막고, y=5/y=8 두 통로로만 진입 가능.
        // 통로를 지나면 성벽 모서리와 방어 보너스가 큰 요새 타일이 있는 성채가 나오는, 가장 어려운 지형
        public static TileType[,] Stage3Map()
        {
            var map = Blank();
            for (int y = 0; y < Height; y++)
            {
                bool pass = y == 5 || y == 8;
                if (pass) continue;
                SetSafe(map, 12, y, TileType.Mountain);
                SetSafe(map, 13, y, TileType.Mountain);
            }
            // 산맥 바깥쪽(통로와 떨어진 구석) 보강 벽 - 통로 자체는 막지 않음
            SetAll(map, TileType.Wall, (12, 0), (13, 0), (12, 11), (13, 11));
            // 요새 안쪽 방어 거점(정예 적들이 이 위에서 버팀)
            SetAll(map, TileType.Fort, (16, 5), (16, 6), (17, 6));
            // 통로 진입 전 엄폐용 숲
            SetAll(map, TileType.Forest, (10, 4), (10, 5), (10, 8), (10, 9));
            return map;
        }

        // 스테이지 4: 협곡 - 벽 두 겹이 어긋나게 놓여 S자로 꺾인 통로 하나만 남음(x=10은 남쪽만, x=12는 북쪽만 뚫림).
        // 통로 양 끝 산 위에서 궁수가 접근로를 견제하는, 스테이지 3보다 더 좁고 긴 접근전 지형
        public static TileType[,] Stage4Map()
        {
            var map = Blank();
            for (int y = 0; y <= 7; y++) SetSafe(map, 10, y, TileType.Wall); // x=10은 y=8~11(남쪽)만 통행 가능
            for (int y = 4; y < Height; y++) SetSafe(map, 12, y, TileType.Wall); // x=12는 y=0~3(북쪽)만 통행 가능
            // 통로 어귀를 지키는 궁수용 고지대
            SetAll(map, TileType.Mountain, (9, 9), (9, 10), (13, 1), (13, 2));
            return map;
        }

        // 스테이지 5: 늪지 - 전장 가운데(x=9~14)에 대각선 줄무늬로 물웅덩이가 깔려 이동력을 크게 깎음.
        // 적 스폰 쪽(x>=15)과 아군 스폰 쪽(x<=8)은 늪을 비워 둬 양쪽 진영 자체는 정상적으로 움직일 수 있음
        public static TileType[,] Stage5Map()
        {
            var map = Blank();
            for (int x = 9; x <= 14; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if ((x + y) % 3 == 0) SetSafe(map, x, y, TileType.Water);
                }
            }
            // 늪 사이 마른 땅(둔덕) - 발판 삼아 건널 수 있는 숲/요새 거점
            SetAll(map, TileType.Forest, (10, 2), (11, 5), (12, 8), (13, 3));
            SetAll(map, TileType.Fort, (11, 9), (12, 1));
            return map;
        }

        // 스테이지 6: 최종 요새 - 바깥쪽 산맥(통로 2곳)과 안쪽 성벽(통로 1곳)까지 이중으로 막은 마지막 관문.
        // 안쪽 성벽을 넘으면 방어 보너스가 큰 요새 타일 위에서 정예 부대가 버티고 있음
        public static TileType[,] Stage6Map()
        {
            var map = Blank();
            for (int y = 0; y < Height; y++)
            {
                if (y != 4 && y != 9) SetSafe(map, 11, y, TileType.Mountain);
                if (y != 4 && y != 9) SetSafe(map, 12, y, TileType.Mountain);
                if (y != 6) SetSafe(map, 15, y, TileType.Wall);
                if (y != 6) SetSafe(map, 16, y, TileType.Wall);
            }
            SetAll(map, TileType.Fort, (18, 6), (18, 7), (19, 6));
            SetAll(map, TileType.Forest, (13, 4), (13, 9));
            return map;
        }

        private static TileType[,] Blank()
        {
            var map = new TileType[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    map[x, y] = TileType.Plain;
            return map;
        }

        private static void SetAll(TileType[,] map, TileType type, params (int x, int y)[] coords)
        {
            foreach (var (x, y) in coords) SetSafe(map, x, y, type);
        }

        private static void SetSafe(TileType[,] map, int x, int y, TileType type)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) map[x, y] = type;
        }
    }
}
