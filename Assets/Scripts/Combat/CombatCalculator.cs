using UnityEngine;

namespace SRPG
{
    public struct CombatForecast
    {
        public int attackerDamage;
        public int defenderDamage;
        // 코인 없이(무기 위력 - 상대 지형 보정)만으로 정해지는 기준 데미지. 코인 결과는 전투 연출 중 이 값에 실시간으로 가감됨
        public int attackerBaseDamage;
        public int defenderBaseDamage;
        public bool defenderCanCounter;
        public bool attackerHasAdvantage;
        public bool defenderHasAdvantage;
    }

    public static class CombatCalculator
    {
        public static CombatForecast ComputeForecast(Unit attacker, Unit defender, GridManager grid)
            => ComputeForecast(attacker, defender, attacker.position, defender.position, grid);

        public static CombatForecast ComputeForecast(Unit attacker, Unit defender, GridPosition attackerPos, GridPosition defenderPos, GridManager grid)
        {
            var forecast = new CombatForecast();
            int distance = attackerPos.ManhattanDistance(defenderPos);

            // 이미 브레이크된 유닛은 이번 턴 동안 반격 불가
            forecast.defenderCanCounter = !defender.isBroken &&
                defender.weapon.minRange <= distance && distance <= defender.weapon.maxRange;

            int triangle = WeaponTriangle.GetAdvantage(attacker.weapon.type, defender.weapon.type);
            forecast.attackerHasAdvantage = triangle > 0;
            forecast.defenderHasAdvantage = triangle < 0;

            var atkTerrain = Terrain.Get(grid.GetTileType(attackerPos));
            var defTerrain = Terrain.Get(grid.GetTileType(defenderPos));

            forecast.attackerBaseDamage = attacker.weapon.might - defTerrain.defBonus;
            forecast.attackerDamage = ComputeDamage(attacker, defender, defTerrain);

            if (forecast.defenderCanCounter)
            {
                // 반격은 무기 위력(기본 데미지) 없이 공격/수비 코인만으로 데미지가 정해지도록 함
                forecast.defenderBaseDamage = 0;
                forecast.defenderDamage = Mathf.Max(0, defender.stats.atk - attacker.stats.def - atkTerrain.defBonus);
            }

            return forecast;
        }

        private static int ComputeDamage(Unit attacker, Unit defender, TerrainInfo defTerrain)
        {
            // 공격력/방어력 모두 물리/마법 구분 없이 하나의 값을 씀
            return Mathf.Max(0, attacker.weapon.might + attacker.stats.atk - defender.stats.def - defTerrain.defBonus);
        }
    }
}
