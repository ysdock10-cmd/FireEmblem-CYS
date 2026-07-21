namespace SRPG
{
    public enum WeaponType { Sword, Lance, Axe, Bow, Tome, Dagger }

    [System.Serializable]
    public class WeaponData
    {
        public string weaponName;
        public WeaponType type;
        public int might;
        public int weight;
        public int minRange;
        public int maxRange;
        public bool isBig; // "큰 무기"(대검/장창 등) 여부: 아이콘을 일반 무기와 다르게 그리는 데 씀
        public string iconFile; // StreamingAssets/Weapons 안의 정사각형 이미지 파일명. 없으면 절차적 도형 아이콘으로 대체됨

        public WeaponData(string weaponName, WeaponType type, int might, int weight, int minRange, int maxRange, bool isBig = false, string iconFile = null)
        {
            this.weaponName = weaponName;
            this.type = type;
            this.might = might;
            this.weight = weight;
            this.minRange = minRange;
            this.maxRange = maxRange;
            this.isBig = isBig;
            this.iconFile = iconFile;
        }
    }

    public static class WeaponTriangle
    {
        // 엔게이지 물리 삼각 관계: 소드 > 액스 > 랜스 > 소드
        // 명중/데미지 보정은 제거됨 — 상성 우위는 브레이크(반격 무력화+후속타) 여부만 결정함
        public static int GetAdvantage(WeaponType attacker, WeaponType defender)
        {
            if (attacker == defender) return 0;
            if (!IsTriangleType(attacker) || !IsTriangleType(defender)) return 0;

            if ((attacker == WeaponType.Sword && defender == WeaponType.Axe) ||
                (attacker == WeaponType.Axe && defender == WeaponType.Lance) ||
                (attacker == WeaponType.Lance && defender == WeaponType.Sword))
                return 1;

            if ((defender == WeaponType.Sword && attacker == WeaponType.Axe) ||
                (defender == WeaponType.Axe && attacker == WeaponType.Lance) ||
                (defender == WeaponType.Lance && attacker == WeaponType.Sword))
                return -1;

            return 0;
        }

        private static bool IsTriangleType(WeaponType w) => w == WeaponType.Sword || w == WeaponType.Lance || w == WeaponType.Axe;
    }
}
