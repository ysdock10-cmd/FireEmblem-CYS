using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    public static class WeaponLibrary
    {
        public static WeaponData IronSword() => new WeaponData("아이언 소드", WeaponType.Sword, 5, 5, 1, 1);
        public static WeaponData IronLance() => new WeaponData("아이언 랜스", WeaponType.Lance, 7, 8, 1, 1);
        public static WeaponData IronAxe() => new WeaponData("아이언 액스", WeaponType.Axe, 8, 10, 1, 1);
        public static WeaponData IronBow() => new WeaponData("아이언 보우", WeaponType.Bow, 6, 6, 2, 2);
        public static WeaponData Elfire() => new WeaponData("엘파이어", WeaponType.Tome, 7, 5, 1, 2);
        public static WeaponData IronDagger() => new WeaponData("아이언 대거", WeaponType.Dagger, 4, 3, 1, 1);

        // "큰 무기": 기본 무기보다 위력은 높지만 무게/사거리 등에서 대가가 있는 상위 버전
        public static WeaponData GreatSword() => new WeaponData("대검", WeaponType.Sword, 11, 13, 1, 1, true);
        public static WeaponData LongLance() => new WeaponData("장창", WeaponType.Lance, 10, 14, 1, 2, true);
        public static WeaponData GreatAxe() => new WeaponData("대전투도끼", WeaponType.Axe, 14, 16, 1, 1, true);
        public static WeaponData LongBow() => new WeaponData("장궁", WeaponType.Bow, 7, 9, 2, 3, true);
        public static WeaponData GreatTome() => new WeaponData("파이어스톰", WeaponType.Tome, 9, 8, 1, 2, true);

        // 사용자 제공 그림(StreamingAssets/Weapons/revolution_staff.png)을 아이콘으로 쓰는 무기. 이브가 기본 장착
        public static WeaponData RevolutionStaff() => new WeaponData("혁명군 지팡이", WeaponType.Tome, 6, 4, 1, 2, iconFile: "revolution_staff.png");

        // 튜토리얼 전용 허수아비 무기: 위력 0이라 맞아도 피해가 없음(안전하게 연습하도록)
        public static WeaponData TrainingDummyWeapon() => new WeaponData("허수아비 몽둥이", WeaponType.Sword, 0, 1, 1, 1);

        // 기본 무기 타입에 대응하는 "큰 무기"를 찾아줌 (없으면 null)
        public static WeaponData BigVariant(WeaponType type) => type switch
        {
            WeaponType.Sword => GreatSword(),
            WeaponType.Lance => LongLance(),
            WeaponType.Axe => GreatAxe(),
            WeaponType.Bow => LongBow(),
            WeaponType.Tome => GreatTome(),
            _ => null,
        };
    }

    public static class RosterLibrary
    {
        // 튜토리얼: 실제 아군 편성 그대로 + 허수아비 3체(위력 0이라 반격을 받아도 피해가 없어 마음껏 연습 가능)
        public static UnitDefinition[] TutorialRoster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                Enemy("훈련용 허수아비", WeaponLibrary.TrainingDummyWeapon(), new Stats { maxHP = 8, atkCoins = OneCoin(0, 0), spd = 1, defCoins = OneCoin(0, 0), lck = 0, build = 1 }, new GridPosition(11, 5)),
                Enemy("훈련용 허수아비", WeaponLibrary.TrainingDummyWeapon(), new Stats { maxHP = 8, atkCoins = OneCoin(0, 0), spd = 1, defCoins = OneCoin(0, 0), lck = 0, build = 1 }, new GridPosition(11, 6)),
                Enemy("훈련용 허수아비", WeaponLibrary.TrainingDummyWeapon(), new Stats { maxHP = 8, atkCoins = OneCoin(0, 0), spd = 1, defCoins = OneCoin(0, 0), lck = 0, build = 1 }, new GridPosition(10, 7)),
            });

            return units.ToArray();
        }

        public static UnitDefinition[] DefaultRoster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            // 맵 우하단 구석 대신 중앙보다 살짝 오른쪽 아래에 모여서 시작(아군과 마주보도록)
            units.AddRange(new[]
            {
                Enemy("적 병사", WeaponLibrary.IronLance(), new Stats { maxHP = 19, atkCoins = OneCoin(8, 0), spd = 5, defCoins = OneCoin(5, 0), lck = 0, build = 9 }, new GridPosition(12, 7)),
                Enemy("적 병사", WeaponLibrary.IronLance(), new Stats { maxHP = 19, atkCoins = OneCoin(8, 0), spd = 5, defCoins = OneCoin(5, 0), lck = 0, build = 9 }, new GridPosition(11, 7)),
                Enemy("적 전사", WeaponLibrary.IronAxe(), new Stats { maxHP = 22, atkCoins = OneCoin(10, 0), spd = 4, defCoins = OneCoin(4, 0), lck = 0, build = 12 }, new GridPosition(13, 8)),
                Enemy("적 궁수", WeaponLibrary.IronBow(), new Stats { maxHP = 16, atkCoins = OneCoin(7, 0), spd = 7, defCoins = OneCoin(4, 0), lck = 0, build = 6 }, new GridPosition(12, 6)),
                Enemy("적 마법사", WeaponLibrary.Elfire(), new Stats { maxHP = 15, atkCoins = OneCoin(8, 0), spd = 6, defCoins = OneCoin(5, 0), lck = 0, build = 5 }, new GridPosition(13, 7)),
            });

            return units.ToArray();
        }

        // 스테이지 2: 직업(무기 타입) 6종(검사/창병/도끼병/궁수/마도사/도적)이 전부 한 명씩 나오는 구성
        public static UnitDefinition[] Stage2Roster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                Enemy("적 검사", WeaponLibrary.IronSword(), new Stats { maxHP = 20, atkCoins = OneCoin(9, 0), spd = 7, defCoins = OneCoin(6, 0), lck = 0, build = 9 }, new GridPosition(12, 5)),
                Enemy("적 창병", WeaponLibrary.IronLance(), new Stats { maxHP = 22, atkCoins = OneCoin(9, 0), spd = 5, defCoins = OneCoin(7, 0), lck = 0, build = 11 }, new GridPosition(13, 6)),
                Enemy("적 도끼병", WeaponLibrary.IronAxe(), new Stats { maxHP = 24, atkCoins = OneCoin(11, 0), spd = 4, defCoins = OneCoin(5, 0), lck = 0, build = 13 }, new GridPosition(11, 6)),
                Enemy("적 궁수", WeaponLibrary.IronBow(), new Stats { maxHP = 17, atkCoins = OneCoin(8, 0), spd = 8, defCoins = OneCoin(4, 0), lck = 0, build = 6 }, new GridPosition(14, 7)),
                Enemy("적 마법사", WeaponLibrary.Elfire(), new Stats { maxHP = 16, atkCoins = OneCoin(9, 0), spd = 6, defCoins = OneCoin(5, 0), lck = 0, build = 5 }, new GridPosition(12, 8)),
                Enemy("적 도적", WeaponLibrary.IronDagger(), new Stats { maxHP = 15, atkCoins = OneCoin(7, 0), spd = 11, defCoins = OneCoin(3, 0), lck = 0, build = 5 }, new GridPosition(13, 8)),
            });

            return units.ToArray();
        }

        // 스테이지 3: 산악 요새(MapLibrary.Stage3Map) 통로를 지키는 수문장 2명 + 성채 거점(Fort) 위에 버티는 정예 3명 + 후방 지원 2명, 총 7명.
        // 절반 이상이 기본 무기 대신 "큰 무기"(위력이 훨씬 높은 상위 무기)를 장착하고 있어 스테이지 2보다 확실히 강함
        public static UnitDefinition[] Stage3Roster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                // 통로(y=5) 수문장
                Enemy("적 성문지기", WeaponLibrary.GreatAxe(), new Stats { maxHP = 28, atkCoins = OneCoin(15, 0), spd = 4, defCoins = OneCoin(6, 0), lck = 0, build = 15 }, new GridPosition(14, 5)),
                // 통로(y=8) 수문장
                Enemy("적 성문지기", WeaponLibrary.LongLance(), new Stats { maxHP = 26, atkCoins = OneCoin(13, 0), spd = 5, defCoins = OneCoin(8, 0), lck = 0, build = 13 }, new GridPosition(14, 8)),
                // 성채 요새 타일 위의 정예
                Enemy("적 대검병", WeaponLibrary.GreatSword(), new Stats { maxHP = 24, atkCoins = OneCoin(14, 0), spd = 7, defCoins = OneCoin(7, 0), lck = 0, build = 10 }, new GridPosition(16, 5)),
                Enemy("적 대마도사", WeaponLibrary.GreatTome(), new Stats { maxHP = 19, atkCoins = OneCoin(12, 0), spd = 7, defCoins = OneCoin(6, 0), lck = 0, build = 6 }, new GridPosition(17, 6)),
                // 후방 지원
                Enemy("적 장궁병", WeaponLibrary.LongBow(), new Stats { maxHP = 20, atkCoins = OneCoin(10, 0), spd = 9, defCoins = OneCoin(5, 0), lck = 0, build = 7 }, new GridPosition(15, 7)),
                Enemy("적 도적", WeaponLibrary.IronDagger(), new Stats { maxHP = 17, atkCoins = OneCoin(9, 0), spd = 13, defCoins = OneCoin(4, 0), lck = 0, build = 6 }, new GridPosition(16, 8)),
                Enemy("적 창병", WeaponLibrary.IronLance(), new Stats { maxHP = 23, atkCoins = OneCoin(10, 0), spd = 6, defCoins = OneCoin(7, 0), lck = 0, build = 12 }, new GridPosition(17, 4)),
            });

            return units.ToArray();
        }

        // 스테이지 4: 협곡(MapLibrary.Stage4Map) S자 통로 양 끝을 지키는 궁수 2명 + 통로 사이 중간 저지선 2명 + 안쪽 정예 3명, 총 7명.
        // 스테이지 3보다 스탯을 전반적으로 한 단계씩 올림
        public static UnitDefinition[] Stage4Roster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                // 남쪽 통로 어귀(산 위) 저격수
                Enemy("적 협곡 궁수", WeaponLibrary.LongBow(), new Stats { maxHP = 21, atkCoins = OneCoin(11, 0), spd = 10, defCoins = OneCoin(5, 0), lck = 0, build = 7 }, new GridPosition(9, 9)),
                // 중간 통로 저지선
                Enemy("적 창병", WeaponLibrary.IronLance(), new Stats { maxHP = 24, atkCoins = OneCoin(10, 0), spd = 6, defCoins = OneCoin(8, 0), lck = 0, build = 12 }, new GridPosition(11, 9)),
                Enemy("적 검사", WeaponLibrary.IronSword(), new Stats { maxHP = 22, atkCoins = OneCoin(10, 0), spd = 8, defCoins = OneCoin(7, 0), lck = 0, build = 10 }, new GridPosition(11, 3)),
                // 북쪽 통로 출구(산 위) 저격수
                Enemy("적 협곡 궁수", WeaponLibrary.LongBow(), new Stats { maxHP = 21, atkCoins = OneCoin(11, 0), spd = 10, defCoins = OneCoin(5, 0), lck = 0, build = 7 }, new GridPosition(13, 2)),
                // 안쪽 정예
                Enemy("적 도끼병", WeaponLibrary.GreatAxe(), new Stats { maxHP = 30, atkCoins = OneCoin(16, 0), spd = 5, defCoins = OneCoin(7, 0), lck = 0, build = 16 }, new GridPosition(13, 5)),
                Enemy("적 대마도사", WeaponLibrary.GreatTome(), new Stats { maxHP = 21, atkCoins = OneCoin(13, 0), spd = 8, defCoins = OneCoin(6, 0), lck = 0, build = 6 }, new GridPosition(15, 6)),
                Enemy("적 도적", WeaponLibrary.IronDagger(), new Stats { maxHP = 19, atkCoins = OneCoin(10, 0), spd = 14, defCoins = OneCoin(4, 0), lck = 0, build = 6 }, new GridPosition(15, 8)),
            });

            return units.ToArray();
        }

        // 스테이지 5: 늪지(MapLibrary.Stage5Map) 마른 둔덕에서 늪을 지키는 2명 + 본진(x>=15)의 정예 6명, 총 8명.
        // 스테이지 4보다 한 단계 더 강함
        public static UnitDefinition[] Stage5Roster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                // 늪 안 마른 둔덕(요새 타일)에서 도하 지점을 지킴
                Enemy("적 늪지 궁수", WeaponLibrary.LongBow(), new Stats { maxHP = 22, atkCoins = OneCoin(11, 0), spd = 10, defCoins = OneCoin(5, 0), lck = 0, build = 7 }, new GridPosition(11, 9)),
                Enemy("적 늪지 도적", WeaponLibrary.IronDagger(), new Stats { maxHP = 20, atkCoins = OneCoin(10, 0), spd = 15, defCoins = OneCoin(4, 0), lck = 0, build = 6 }, new GridPosition(12, 1)),
                // 본진 정예
                Enemy("적 검사", WeaponLibrary.GreatSword(), new Stats { maxHP = 26, atkCoins = OneCoin(15, 0), spd = 8, defCoins = OneCoin(8, 0), lck = 0, build = 11 }, new GridPosition(15, 4)),
                Enemy("적 창병", WeaponLibrary.LongLance(), new Stats { maxHP = 28, atkCoins = OneCoin(15, 0), spd = 6, defCoins = OneCoin(9, 0), lck = 0, build = 14 }, new GridPosition(15, 8)),
                Enemy("적 도끼병", WeaponLibrary.GreatAxe(), new Stats { maxHP = 31, atkCoins = OneCoin(17, 0), spd = 5, defCoins = OneCoin(7, 0), lck = 0, build = 17 }, new GridPosition(17, 6)),
                Enemy("적 마법사", WeaponLibrary.Elfire(), new Stats { maxHP = 19, atkCoins = OneCoin(9, 0), spd = 7, defCoins = OneCoin(6, 0), lck = 0, build = 5 }, new GridPosition(16, 3)),
                Enemy("적 대마도사", WeaponLibrary.GreatTome(), new Stats { maxHP = 22, atkCoins = OneCoin(14, 0), spd = 8, defCoins = OneCoin(7, 0), lck = 0, build = 6 }, new GridPosition(18, 7)),
                Enemy("적 궁수", WeaponLibrary.IronBow(), new Stats { maxHP = 19, atkCoins = OneCoin(9, 0), spd = 10, defCoins = OneCoin(5, 0), lck = 0, build = 6 }, new GridPosition(17, 9)),
            });

            return units.ToArray();
        }

        // 스테이지 6: 최종 요새(MapLibrary.Stage6Map) 이중 관문을 지키는 파수병 4명 + 최심부 정예 4명 + 최종 보스(성채 요새 타일) 1명, 총 9명.
        // 게임 내 가장 강한 스테이지 - 마지막에 "적 요새 사령관"이 전체 최고 스탯으로 버팀
        public static UnitDefinition[] Stage6Roster()
        {
            var units = new List<UnitDefinition>(PlayerSquad());

            units.AddRange(new[]
            {
                // 바깥 관문(산맥) 파수병
                Enemy("적 성문 파수병", WeaponLibrary.LongBow(), new Stats { maxHP = 23, atkCoins = OneCoin(12, 0), spd = 10, defCoins = OneCoin(6, 0), lck = 0, build = 7 }, new GridPosition(13, 4)),
                Enemy("적 성문 파수병", WeaponLibrary.LongBow(), new Stats { maxHP = 23, atkCoins = OneCoin(12, 0), spd = 10, defCoins = OneCoin(6, 0), lck = 0, build = 7 }, new GridPosition(13, 9)),
                // 안쪽 관문(성벽) 앞 저지선
                Enemy("적 창병", WeaponLibrary.LongLance(), new Stats { maxHP = 30, atkCoins = OneCoin(16, 0), spd = 6, defCoins = OneCoin(10, 0), lck = 0, build = 15 }, new GridPosition(14, 6)),
                Enemy("적 도끼병", WeaponLibrary.GreatAxe(), new Stats { maxHP = 32, atkCoins = OneCoin(18, 0), spd = 5, defCoins = OneCoin(8, 0), lck = 0, build = 18 }, new GridPosition(14, 4)),
                // 최심부 정예
                Enemy("적 검사", WeaponLibrary.GreatSword(), new Stats { maxHP = 28, atkCoins = OneCoin(17, 0), spd = 9, defCoins = OneCoin(9, 0), lck = 0, build = 12 }, new GridPosition(17, 5)),
                Enemy("적 대마도사", WeaponLibrary.GreatTome(), new Stats { maxHP = 24, atkCoins = OneCoin(16, 0), spd = 9, defCoins = OneCoin(8, 0), lck = 0, build = 7 }, new GridPosition(18, 6)),
                Enemy("적 도적", WeaponLibrary.IronDagger(), new Stats { maxHP = 22, atkCoins = OneCoin(12, 0), spd = 16, defCoins = OneCoin(5, 0), lck = 0, build = 6 }, new GridPosition(17, 7)),
                Enemy("적 장궁병", WeaponLibrary.LongBow(), new Stats { maxHP = 23, atkCoins = OneCoin(13, 0), spd = 10, defCoins = OneCoin(6, 0), lck = 0, build = 7 }, new GridPosition(18, 7)),
                // 최종 보스: 성채 요새 타일 위, 게임 내 전체 최고 스탯
                Enemy("적 요새 사령관", WeaponLibrary.GreatAxe(), new Stats { maxHP = 38, atkCoins = OneCoin(20, 0), spd = 7, defCoins = OneCoin(11, 0), lck = 0, build = 17 }, new GridPosition(19, 6)),
            });

            return units.ToArray();
        }

        // atk/def 모두 코인들의 앞면 값을 더한 값(뒷면은 0 -> 뒷면이 나오면 무기 위력만 적용).
        // TODO: 뒷면 값은 전부 임시로 0으로 맞춘 상태라 밸런스 재조정 필요(원래 뒷면 값은 커밋 히스토리 참고)
        // 맵(20x12) 좌상단 구석 대신 중앙(10,6) 근처, 중앙보다 살짝 왼쪽 위에 모여서 시작(적군과 마주보도록)
        // 일러스트만 공통값(오프셋 -40,35 / 배율 2.5)보다 조금 더 아래로 내리고 살짝 확대해서 보여줌
        private static UnitDefinition[] PlayerSquad() => new[]
        {
            Player("알리어", WeaponLibrary.IronSword(), new Stats { maxHP = 21, atkCoins = OneCoin(9, 0), spd = 9, defCoins = OneCoin(7, 0), lck = 6, build = 8 }, new GridPosition(8, 5), "vander.png", "vander.png",
                illustrationOffset: new Vector2(-40f, 15f), illustrationZoom: 2.6f),
            // "코인" 스탯 표시 확인용으로 반더만 코인 2개
            Player("반더", WeaponLibrary.IronLance(), new Stats { maxHP = 27, atkCoins = new List<Coin> { new Coin(12, 0), new Coin(12, 0) }, spd = 5, defCoins = OneCoin(8, 0), lck = 4, build = 13 }, new GridPosition(7, 5), "bandeo.png", "bandeo.png",
                illustrationOffset: new Vector2(-40f, 55f)),
            Player("에티에", WeaponLibrary.IronBow(), new Stats { maxHP = 18, atkCoins = OneCoin(10, 0), spd = 12, defCoins = OneCoin(5, 0), lck = 7, build = 6 }, new GridPosition(8, 4)),
            Player("이브", WeaponLibrary.RevolutionStaff(), new Stats { maxHP = 16, atkCoins = OneCoin(10, 0), spd = 9, defCoins = OneCoin(7, 0), lck = 6, build = 5 }, new GridPosition(7, 4), "eve.png", "eve.png"),
            // 아군 첫 도끼(둔기) 사용자. 도끼는 무겁다는 설정에 맞춰 build를 가장 높게, spd는 가장 낮게 잡음
            Player("가론", WeaponLibrary.IronAxe(), new Stats { maxHP = 24, atkCoins = OneCoin(11, 0), spd = 6, defCoins = OneCoin(6, 0), lck = 3, build = 14 }, new GridPosition(8, 6)),
        };

        // 기본적으로 코인 1개만 드는 캐릭터를 표현하기 위한 축약 헬퍼
        private static List<Coin> OneCoin(int heads, int tails) => new List<Coin> { new Coin(heads, tails) };

        // portraitFile: 맵에 들어가는 캐릭터 SD(미니 캐릭터), illustrationFile: 정보창에 보이는 캐릭터 일러스트
        // illustrationOffset/illustrationZoom: 이 캐릭터의 일러스트만 공통 위치/배율 대신 따로 두고 싶을 때만 채움
        private static UnitDefinition Player(string name, WeaponData w, Stats s, GridPosition pos, string portraitFile = null, string illustrationFile = null,
            Vector2? illustrationOffset = null, float? illustrationZoom = null)
            => new UnitDefinition { unitName = name, team = Team.Player, weapon = w, baseStats = s, startPosition = pos, portraitFile = portraitFile, illustrationFile = illustrationFile,
                illustrationOffset = illustrationOffset, illustrationZoom = illustrationZoom };

        private static UnitDefinition Enemy(string name, WeaponData w, Stats s, GridPosition pos)
            => new UnitDefinition { unitName = name, team = Team.Enemy, weapon = w, baseStats = s, startPosition = pos };
    }
}
