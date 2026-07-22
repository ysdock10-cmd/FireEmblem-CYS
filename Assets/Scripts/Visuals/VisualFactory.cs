using System.IO;
using UnityEngine;

namespace SRPG
{
    public static class VisualFactory
    {
        private static Sprite ToSprite(Texture2D tex, Vector2 pivot, float pixelsPerUnit)
        {
            // Point로 하면 화면 크기에 맞춰 도형이 축소될 때 대각선/곡선 경계가 앨리어싱(계단/지글거림)됨
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit);
        }

        // StreamingAssets/Portraits 안의 이미지를 캐릭터 스프라이트로 불러옴 (없으면 null 반환)
        public static Sprite LoadPortraitSprite(string fileName, float worldHeight = 1.1f)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Portraits", fileName);
            if (!File.Exists(path)) return null;

            // 원본 그림이 화면에 작게 축소되어 그려질 때 밉맵이 없으면 도트처럼 어른거리므로(aliasing) 밉맵 체인을 켬
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;

            float ppu = tex.height / worldHeight;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        }

        // StreamingAssets/Illustrations 안의 이미지를 불러옴(캐릭터 선택 시 정보칸 왼쪽에 크게 보여줄 전신 그림, 없으면 null 반환)
        // UI Image는 RectTransform 크기에 맞춰 그려지므로(픽셀 단위가 아님) pixelsPerUnit은 그냥 기본값(100)을 씀
        public static Sprite LoadIllustrationSprite(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Illustrations", fileName);
            if (!File.Exists(path)) return null;

            // 이 그림은 UI에서 원본 해상도에 가깝게(또는 확대해서) 평면으로만 보여주고 기울어진 각도로 볼 일이 없어서,
            // 밉맵을 켜면 트라일리니어 블렌딩 때문에 오히려 더 흐릿해짐 -> 밉맵을 끄고 Bilinear만 씀(항상 원본 해상도 샘플링)
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Bilinear;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        // StreamingAssets/Tiles 안의 이미지를 지형 타일 스프라이트로 불러옴(없으면 null 반환). 타일 한 칸(1x1)에 꽉 차게 그려짐
        public static Sprite LoadTileSprite(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Tiles", fileName);
            if (!File.Exists(path)) return null;

            // 화면이 축소될 때 도트처럼 어른거리지 않도록 밉맵 체인을 켬(캐릭터 초상화와 같은 이유)
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height);
        }

        public static Sprite SquareSprite(Color fill, Color? border = null, int size = 256, int borderWidth = 16)
        {
            var tex = new Texture2D(size, size);
            Color b = border ?? new Color(fill.r * 0.6f, fill.g * 0.6f, fill.b * 0.6f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < borderWidth || y < borderWidth || x >= size - borderWidth || y >= size - borderWidth;
                    tex.SetPixel(x, y, edge ? b : fill);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CircleSprite(Color fill, int size = 256)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float r = size / 2f;
            var c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    tex.SetPixel(x, y, d <= r ? fill : clear);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        public static Sprite FlatSprite(Color fill, Vector2 pivot)
        {
            const int size = 4;
            var tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, fill);
            return ToSprite(tex, pivot, size);
        }

        // 상성 표시용 X 마크(브레이크 가능)
        public static Sprite XMarkSprite(Color color, int size = 128, int thickness = 20)
        {
            var tex = new Texture2D(size, size);
            var clear = new Color(color.r, color.g, color.b, 0f);
            float margin = size * 0.12f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inMargin = x >= margin && x < size - margin && y >= margin && y < size - margin;
                    float d1 = Mathf.Abs((x - y));
                    float d2 = Mathf.Abs((x - (size - 1 - y)));
                    bool inside = inMargin && (d1 <= thickness || d2 <= thickness);
                    tex.SetPixel(x, y, inside ? color : clear);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        // 상성 표시용 불꽃(내가 브레이크 당함). 아래는 넓고 위로 갈수록 좁아지며 살짝 흔들리는 모양, 빨강->노랑 그라데이션
        public static Sprite FireSprite(int size = 128)
        {
            var tex = new Texture2D(size, size);
            Color red = new Color(0.95f, 0.2f, 0.05f);
            Color yellow = new Color(1f, 0.85f, 0.15f);
            var clear = new Color(1f, 0.4f, 0.05f, 0f);
            float cx = size / 2f;
            for (int y = 0; y < size; y++)
            {
                float ny = y / (float)(size - 1); // 0: 아래(넓음), 1: 위(뾰족)
                float wave = Mathf.Sin(ny * 6.5f) * size * 0.035f;
                float halfWidth = (1f - Mathf.Pow(ny, 1.3f)) * size * 0.40f;
                Color rowColor = Color.Lerp(red, yellow, ny);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - (cx + wave));
                    bool inside = dx <= halfWidth && ny <= 0.97f;
                    tex.SetPixel(x, y, inside ? rowColor : clear);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        // StreamingAssets/Weapons 안의 정사각형 이미지를 무기 아이콘으로 불러옴 (없으면 null 반환)
        public static Sprite LoadWeaponIconSprite(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Weapons", fileName);
            if (!File.Exists(path)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height);
        }

        // 무기마다 지정된 그림 파일(iconFile)이 있으면 그걸 쓰고, 없으면 절차적 도형 아이콘으로 대체
        public static Sprite WeaponIconSprite(WeaponData weapon, int size = 128)
        {
            if (!string.IsNullOrEmpty(weapon.iconFile))
            {
                var loaded = LoadWeaponIconSprite(weapon.iconFile);
                if (loaded != null) return loaded;
            }
            return WeaponIconSprite(weapon.type, weapon.isBig, size);
        }

        // 무기 타입마다 고유한 색을 입혀서, 같은 모양(ClassSprite)이라도 UI에서 한눈에 구분되도록 함
        // 큰 무기(isBig)는 같은 모양을 더 크고 진한 테두리로 둘러 일반 무기와 구분되도록 함
        public static Sprite WeaponIconSprite(WeaponType type, bool isBig = false, int size = 128)
        {
            Color fill = WeaponIconColor(type);
            if (!isBig) return ClassSprite(type, fill, size);

            Color border = new Color(fill.r * 0.45f, fill.g * 0.45f, fill.b * 0.45f, 1f);
            return ClassSprite(type, fill, size, 0.14f, border);
        }

        private static Color WeaponIconColor(WeaponType type) => type switch
        {
            WeaponType.Sword => new Color(0.80f, 0.82f, 0.88f),
            WeaponType.Lance => new Color(0.50f, 0.72f, 0.95f),
            WeaponType.Axe => new Color(0.70f, 0.45f, 0.22f),
            WeaponType.Bow => new Color(0.45f, 0.78f, 0.35f),
            WeaponType.Tome => new Color(0.70f, 0.38f, 0.90f),
            WeaponType.Dagger => new Color(0.88f, 0.85f, 0.30f),
            _ => Color.white,
        };

        // 무기(직업 계열)마다 다른 모양을 써서 한눈에 구분되도록 함
        // borderFrac > 0이면 도형 테두리를 borderColor로 둘러 그림 (큰 무기 표시용)
        public static Sprite ClassSprite(WeaponType type, Color fill, int size = 256, float borderFrac = 0f, Color? borderColor = null)
        {
            switch (type)
            {
                case WeaponType.Sword: return DiamondSprite(fill, size, borderFrac, borderColor);
                case WeaponType.Lance: return TriangleSprite(fill, size, true, borderFrac, borderColor);
                case WeaponType.Bow: return TriangleSprite(fill, size, false, borderFrac, borderColor);
                case WeaponType.Axe: return OctagonSprite(fill, size, borderFrac, borderColor);
                case WeaponType.Tome: return RingSprite(fill, size, borderFrac, borderColor);
                default: return CircleSprite(fill, size, borderFrac, borderColor);
            }
        }

        private static Sprite DiamondSprite(Color fill, int size, float borderFrac = 0f, Color? borderColor = null)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float r = size * 0.48f;
            float border = borderFrac * size;
            Color b = borderColor ?? fill;
            float cx = size / 2f, cy = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - cx) + Mathf.Abs(y + 0.5f - cy);
                    Color c = d > r ? clear : (border > 0f && d >= r - border ? b : fill);
                    tex.SetPixel(x, y, c);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        private static Sprite TriangleSprite(Color fill, int size, bool pointUp, float borderFrac = 0f, Color? borderColor = null)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float r = size * 0.44f;
            float border = borderFrac * size;
            Color b = borderColor ?? fill;
            float cx = size / 2f;
            float margin = size * 0.08f;
            for (int y = 0; y < size; y++)
            {
                float t = pointUp ? (size - 1 - y) / (float)(size - 1) : y / (float)(size - 1);
                float halfWidth = t * r;
                bool inRow = y >= margin && y <= size - 1 - margin;
                bool nearFlatEdge = pointUp ? y >= size - 1 - margin - border : y <= margin + border;
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    bool inside = inRow && dx <= halfWidth;
                    bool nearSideEdge = dx >= halfWidth - border;
                    Color c = !inside ? clear : (border > 0f && (nearSideEdge || nearFlatEdge) ? b : fill);
                    tex.SetPixel(x, y, c);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        private static Sprite OctagonSprite(Color fill, int size, float borderFrac = 0f, Color? borderColor = null)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float cx = size / 2f, cy = size / 2f;
            float rSquare = size * 0.34f;
            float rDiamond = size * 0.5f;
            float border = borderFrac * size;
            Color b = borderColor ?? fill;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    float dy = Mathf.Abs(y + 0.5f - cy);
                    bool inside = dx <= rSquare && dy <= rSquare && dx + dy <= rDiamond;
                    float edgeDist = Mathf.Min(rSquare - dx, rSquare - dy, rDiamond - (dx + dy));
                    Color c = !inside ? clear : (border > 0f && edgeDist <= border ? b : fill);
                    tex.SetPixel(x, y, c);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        private static Sprite RingSprite(Color fill, int size, float borderFrac = 0f, Color? borderColor = null)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float cx = size / 2f, cy = size / 2f;
            float outerR = size * 0.46f;
            float innerR = size * 0.24f;
            float border = borderFrac * size;
            Color b = borderColor ?? fill;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    bool inside = d <= outerR && d >= innerR;
                    bool nearEdge = d >= outerR - border || d <= innerR + border;
                    Color c = !inside ? clear : (border > 0f && nearEdge ? b : fill);
                    tex.SetPixel(x, y, c);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }

        private static Sprite CircleSprite(Color fill, int size, float borderFrac, Color? borderColor)
        {
            var tex = new Texture2D(size, size);
            // 이중선형 필터링 시 투명 경계에서 검은 테두리가 번지지 않도록 채움색과 같은 RGB로 맞춤
            var clear = new Color(fill.r, fill.g, fill.b, 0f);
            float r = size / 2f;
            float border = borderFrac * size;
            Color b = borderColor ?? fill;
            var c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    Color col = d > r ? clear : (border > 0f && d >= r - border ? b : fill);
                    tex.SetPixel(x, y, col);
                }
            }
            return ToSprite(tex, new Vector2(0.5f, 0.5f), size * 1.4f);
        }
    }
}
