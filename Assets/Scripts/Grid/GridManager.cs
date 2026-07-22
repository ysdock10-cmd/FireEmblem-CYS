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

        // 튜토리얼이 "지금은 이 칸만 누르세요"라고 짚어줄 때 쓰는 깜박이는 노란 테두리 표시
        private GameObject guideMarker;
        private SpriteRenderer guideMarkerRenderer;
        private const float GuideMarkerBlinkSpeed = 5f;
        private static readonly Color GuideMarkerColor = new Color(1f, 0.95f, 0.2f);

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
                        // 평지는 사용자가 넣어둔 잔디 그림(StreamingAssets/Tiles/grass.png)을 쓰고, 없으면 기존처럼 단색으로 대체
                        sprite = type == TileType.Plain
                            ? VisualFactory.LoadTileSprite("grass.png") ?? VisualFactory.SquareSprite(Terrain.Get(type).color)
                            : VisualFactory.SquareSprite(Terrain.Get(type).color);
                        spriteByType[type] = sprite;
                    }

                    var go = new GameObject($"Tile_{x}_{y}");
                    go.transform.SetParent(tileRoot);
                    go.transform.position = GridToWorld(pos);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 0;

                    // 평지는 전부 같은 그림을 쓰다 보니 격자처럼 반복되어 보이므로, 칸마다 90도 단위로 무작위 회전/좌우반전해서 반복 패턴을 덜 티나게 하고,
                    // 원본 그림 자체가 밝고 채도가 높아서 색을 살짝 어둡게 틴트를 곱함
                    if (type == TileType.Plain)
                    {
                        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
                        float flipX = Random.value < 0.5f ? -1f : 1f;
                        go.transform.localScale = new Vector3(flipX, 1f, 1f);
                        sr.color = new Color(0.9f, 0.9f, 0.9f);
                    }
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

        // 지정한 칸에 깜박이는 노란 테두리를 띄움(튜토리얼이 "지금은 이 칸만" 누르라고 짚어줄 때 씀)
        public void ShowGuideMarker(GridPosition pos)
        {
            if (guideMarker == null)
            {
                guideMarker = new GameObject("GuideMarker");
                guideMarker.transform.SetParent(highlightRoot);
                guideMarkerRenderer = guideMarker.AddComponent<SpriteRenderer>();
                // 투명한 안쪽 색을 테두리색과 같은 RGB로 맞춰서, 이중선형 필터링으로 경계가 어둡게 번지지 않게 함
                guideMarkerRenderer.sprite = VisualFactory.SquareSprite(new Color(GuideMarkerColor.r, GuideMarkerColor.g, GuideMarkerColor.b, 0f), GuideMarkerColor, 256, 24);
                guideMarkerRenderer.sortingOrder = 3; // 이동/공격 하이라이트(1), 유닛 타일 테두리(2)보다 위
            }
            guideMarker.transform.position = GridToWorld(pos);
            guideMarker.SetActive(true);
        }

        public void HideGuideMarker()
        {
            if (guideMarker != null) guideMarker.SetActive(false);
        }

        private void Update()
        {
            if (guideMarker == null || !guideMarker.activeSelf) return;
            float alpha = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(Time.time * GuideMarkerBlinkSpeed) + 1f) * 0.5f);
            var c = guideMarkerRenderer.color;
            guideMarkerRenderer.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
