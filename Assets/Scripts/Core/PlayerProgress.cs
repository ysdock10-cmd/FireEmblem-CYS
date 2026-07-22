using System.Collections.Generic;

namespace SRPG
{
    // 스테이지 클리어로 모은 재화와, 그 재화로 강화한 캐릭터별 체력을 기억해 둠(씬을 다시 로드해도 유지되도록 정적 필드로 둠).
    // 앱을 완전히 재시작하면 초기화됨(GameSession.ClearedStages와 같은 방식)
    public static class PlayerProgress
    {
        public static int Currency = 100; // 테스트하기 쉽도록 시작 재화를 넉넉히 줌
        private static readonly Dictionary<string, int> levelUpsByUnitName = new Dictionary<string, int>();

        public const int HpPerLevelUp = 3;  // 강화 1회당 늘어나는 최대 체력
        public const int AtkPerLevelUp = 1; // 강화 1회당 늘어나는 기본공격(atkCoins 합산값)
        public const int DefPerLevelUp = 1; // 강화 1회당 늘어나는 기본수비(defCoins 합산값)
        public const int LevelUpCost = 20;  // 강화 1회 비용. 체력/기본공격/기본수비가 이 하나의 "캐릭터 레벨업"으로 한번에 오름

        public static int GetLevelUps(string unitName) => levelUpsByUnitName.TryGetValue(unitName, out var n) ? n : 0;
        public static int GetBonusHP(string unitName) => GetLevelUps(unitName) * HpPerLevelUp;
        public static int GetBonusAtk(string unitName) => GetLevelUps(unitName) * AtkPerLevelUp;
        public static int GetBonusDef(string unitName) => GetLevelUps(unitName) * DefPerLevelUp;

        // 재화가 충분하면 차감하고 강화 횟수를 1 늘림. 성공 여부를 돌려줌
        public static bool TryLevelUp(string unitName)
        {
            if (Currency < LevelUpCost) return false;
            Currency -= LevelUpCost;
            levelUpsByUnitName[unitName] = GetLevelUps(unitName) + 1;
            return true;
        }
    }
}
