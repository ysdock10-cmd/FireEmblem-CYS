using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    public enum HighlightType { Move, Attack, Selected }

    public class GridManager : MonoBehaviour
    {
        public int width { get; private set; }
        public int height { get; private set; }
        public const float TileSize = 1f;

        private TileType[,] tileTypes;
        private readonly Dictionary<GridPosition, Unit> occupants = new Dictionary<GridPosition, Unit>();
        private readonly List<GameObject> highlightPool = new List<GameObject>();
        private Transform highlightRoot;
        private Sprite highlightSprite;
        private int usedHighlights;

        public void BuildGrid(TileType[,] layout)
        {
            width = layout.GetLength(0);
            height = layout.GetLength(1);
            tileTypes = layout;

            var tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform);
            highlightRoot = new GameObject("Highlights").transform;
            highlightRoot.SetParent(transform);
            highlightSprite = VisualFactory.SquareSprite(Color.white, Color.white, 256, 0);

            // 타일 종류별로 스프라이트를 한 번만 만들어 재사용 (큰 맵에서 텍스처 생성 비용 절감)
            var spriteByType = new Dictionary<TileType, Sprite>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var pos = new GridPosition(x, y);
                    var type = tileTypes[x, y];
                    if (!spriteByType.TryGetValue(type, out var sprite))
                    {
                        sprite = VisualFactory.SquareSprite(Terrain.Get(type).color);
                        spriteByType[type] = sprite;
                    }

                    var go = new GameObject($"Tile_{x}_{y}");
                    go.transform.SetParent(tileRoot);
                    go.transform.position = GridToWorld(pos);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 0;
                }
            }
        }

        public bool InBounds(GridPosition p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
        public bool IsWalkable(GridPosition p) => InBounds(p) && Terrain.Get(tileTypes[p.x, p.y]).walkable;
        public TileType GetTileType(GridPosition p) => InBounds(p) ? tileTypes[p.x, p.y] : TileType.Wall;

        public Vector3 GridToWorld(GridPosition p) => new Vector3((p.x + 0.5f) * TileSize, (p.y + 0.5f) * TileSize, 0f);
        public GridPosition WorldToGrid(Vector3 world) => new GridPosition(Mathf.FloorToInt(world.x / TileSize), Mathf.FloorToInt(world.y / TileSize));

        public Unit GetOccupant(GridPosition p) => occupants.TryGetValue(p, out var u) ? u : null;
        public void SetOccupant(GridPosition p, Unit u) => occupants[p] = u;
        public void RemoveOccupant(GridPosition p) => occupants.Remove(p);
        public void RemoveOccupant(Unit u)
        {
            if (occupants.TryGetValue(u.position, out var occ) && occ == u) occupants.Remove(u.position);
        }

        public void ClearHighlights()
        {
            foreach (var go in highlightPool) go.SetActive(false);
            usedHighlights = 0;
        }

        public void ShowHighlights(IEnumerable<GridPosition> positions, HighlightType type)
        {
            Color color;
            switch (type)
            {
                case HighlightType.Move: color = new Color(0.25f, 0.5f, 1f, 0.45f); break;
                case HighlightType.Attack: color = new Color(1f, 0.25f, 0.25f, 0.45f); break;
                default: color = new Color(1f, 0.95f, 0.3f, 0.55f); break;
            }

            foreach (var pos in positions)
            {
                var go = GetOrCreateHighlight(usedHighlights);
                go.transform.position = GridToWorld(pos);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.color = color;
                go.SetActive(true);
                usedHighlights++;
            }
        }

        private GameObject GetOrCreateHighlight(int index)
        {
            if (index < highlightPool.Count) return highlightPool[index];
            var go = new GameObject($"Highlight_{index}");
            go.transform.SetParent(highlightRoot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = highlightSprite;
            sr.sortingOrder = 1;
            highlightPool.Add(go);
            return go;
        }
    }
}
