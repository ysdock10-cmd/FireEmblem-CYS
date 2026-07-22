using UnityEngine;

namespace SRPG
{
    public enum TileType { Plain, Forest, Mountain, Fort, Wall, Water }

    public struct TerrainInfo
    {
        public int moveCost;
        public int avoidBonus;
        public int defBonus; // 물리/마법 구분 없이 방어를 하나로 봄(기존 defBonus/resBonus 통합)
        public bool walkable;
        public Color color;
    }

    public static class Terrain
    {
        // 지형 보너스(회피/방어 가산치)는 일단 전부 0으로 꺼둠(나중에 재설계해서 다시 넣을 예정).
        // 이동 비용/통행 가능 여부/색상은 그대로 유지하므로 지형에 따라 움직임과 시각적 구분은 여전히 존재함
        public static TerrainInfo Get(TileType type)
        {
            switch (type)
            {
                case TileType.Plain:
                    return new TerrainInfo { moveCost = 1, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.55f, 0.75f, 0.35f) };
                case TileType.Forest:
                    return new TerrainInfo { moveCost = 2, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.16f, 0.45f, 0.18f) };
                case TileType.Mountain:
                    return new TerrainInfo { moveCost = 3, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.5f, 0.42f, 0.32f) };
                case TileType.Fort:
                    return new TerrainInfo { moveCost = 1, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.6f, 0.6f, 0.65f) };
                case TileType.Water:
                    return new TerrainInfo { moveCost = 4, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.25f, 0.45f, 0.75f) };
                case TileType.Wall:
                    return new TerrainInfo { moveCost = 999, avoidBonus = 0, defBonus = 0, walkable = false, color = new Color(0.25f, 0.25f, 0.28f) };
                default:
                    return new TerrainInfo { moveCost = 1, avoidBonus = 0, defBonus = 0, walkable = true, color = Color.white };
            }
        }
    }
}
