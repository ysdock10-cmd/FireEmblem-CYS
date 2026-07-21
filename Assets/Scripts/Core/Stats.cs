using System.Collections.Generic;
using System.Linq;

namespace SRPG
{
    // 공격력을 이루는 코인 하나. (앞면+뒷면)/2가 이 코인이 기여하는 수치
    [System.Serializable]
    public struct Coin
    {
        public int heads;
        public int tails;
        public int Average => (heads + tails) / 2;

        public Coin(int heads, int tails)
        {
            this.heads = heads;
            this.tails = tails;
        }
    }

    [System.Serializable]
    public class Stats
    {
        public int level = 1;
        public int maxHP;

        // 공격력/방어력 모두 코인을 여러 개 들 수 있고, 각 코인의 평균값을 전부 더한 값이 기준 수치가 됨
        // 물리/마법 구분 없이 이 값을 그대로 씀(기본적으로 캐릭터는 코인을 1개만 들고 시작함)
        public List<Coin> atkCoins = new List<Coin>();
        public int atk => atkCoins.Sum(c => c.Average);

        public List<Coin> defCoins = new List<Coin>();
        public int def => defCoins.Sum(c => c.Average);

        public int spd;
        public int lck;
        public int build;

        public Stats Clone() => new Stats
        {
            level = level, maxHP = maxHP,
            atkCoins = new List<Coin>(atkCoins),
            defCoins = new List<Coin>(defCoins),
            spd = spd, lck = lck, build = build
        };
    }
}
