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
        public static TerrainInfo Get(TileType type)
        {
            switch (type)
            {
                case TileType.Plain:
                    return new TerrainInfo { moveCost = 1, avoidBonus = 0, defBonus = 0, walkable = true, color = new Color(0.55f, 0.75f, 0.35f) };
                case TileType.Forest:
                    return new TerrainInfo { moveCost = 2, avoidBonus = 20, defBonus = 1, walkable = true, color = new Color(0.16f, 0.45f, 0.18f) };
                case TileType.Mountain:
                    return new TerrainInfo { moveCost = 3, avoidBonus = 30, defBonus = 2, walkable = true, color = new Color(0.5f, 0.42f, 0.32f) };
                case TileType.Fort:
                    return new TerrainInfo { moveCost = 1, avoidBonus = 10, defBonus = 2, walkable = true, color = new Color(0.6f, 0.6f, 0.65f) };
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
