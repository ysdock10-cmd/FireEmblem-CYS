using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRPG
{
    // 선택된 아군 기준으로 이 적을 공격했을 때의 무기 상성 결과를 표시하는 용도
    public enum MatchupIndicator { None, CanBreak, WillBeBroken }

    public class Unit : MonoBehaviour
    {
        public string unitName;
        public Team team;
        public Stats stats;
        public int currentHP;
        public WeaponData weapon;
        public const int WeaponSlotCount = 4; // 캐릭터 정보창에 4칸으로 깔끔하게 나눠 보여주기 위한 상한
        public WeaponData[] weaponSlots = new WeaponData[WeaponSlotCount]; // 0: 일반 무기, 1: 큰 무기 (나머지는 추후 확장용, 지금은 비어 있음)
        public GridPosition position;
        public bool hasActed;
        public bool isBroken;

        public SpriteRenderer bodyRenderer;
        public Sprite portraitSprite;
        public Sprite illustrationSprite; // 선택 시 정보칸 왼쪽에 크게 보여줄 전신 그림(없으면 null)
        public Vector2? illustrationOffset; // 이 캐릭터만 다른 위치에 그림을 놓고 싶을 때(없으면 UIManager 공통값 사용)
        public float? illustrationZoom; // 이 캐릭터만 다른 배율로 확대하고 싶을 때(없으면 UIManager 공통값 사용)
        private Transform hpFillTransform;
        private Transform visualRoot;
        private float shakeSeed;
        private Coroutine hpBarAnimRoutine;
        private const float HpBarAnimDuration = 0.8f; // 데미지 입을 때 체력바가 한번에 줄지 않고 서서히 깎이도록 (0.35는 너무 빨라서 늦춤)
        // 직업 원이 타일 밖으로 나가 가려지던 문제 해결: 체력바 왼쪽을 줄여(0.8->0.6) 그만큼 원이 들어올 자리를 냄
        private const float HpBarWidth = 0.6f;
        private const float HpBarLeftX = -0.2f;

        private SpriteRenderer matchupOverlayRenderer; // 선택된 아군 기준 상성 표시(X: 내가 브레이크 가능, 불꽃: 내가 브레이크 당함)
        // 원이 타일 밖으로 나가 가려지는 문제 해결: 체력바 왼쪽을 줄인 만큼(0.2) 원을 오른쪽으로 당겨 타일 안에 들어오게 함
        private static readonly Vector3 JobIconLocalPosition = new Vector3(-0.36f, 0.42f, 0f);

        private const float BrokenShakeAmplitude = 0.06f;
        private const float BrokenShakeFrequency = 26f;

        public bool IsAlive => currentHP > 0;

        // 이동 범위는 속도 스탯 값을 그대로 사용
        public int MoveRange => stats.spd;

        // 보유한 무기 슬롯 중 사거리(maxRange)가 가장 긴 무기. 선택 시 공격범위 미리보기에 사용
        public WeaponData LongestRangeWeapon
        {
            get
            {
                WeaponData best = weapon;
                foreach (var w in weaponSlots)
                {
                    if (w == null) continue;
                    if (best == null || w.maxRange > best.maxRange) best = w;
                }
                return best;
            }
        }

        public void Initialize(UnitDefinition def, GridManager grid)
        {
            unitName = def.unitName;
            team = def.team;
            stats = def.baseStats.Clone();
            // 재화로 강화한 만큼 최대 체력/기본공격/기본수비를 가산(아군만 해당 - 적 이름과 겹칠 일은 없지만 그래도 팀으로 한 번 더 안전하게 구분)
            if (team == Team.Player)
            {
                stats.maxHP += PlayerProgress.GetBonusHP(unitName);
                AddCoinBonus(stats.atkCoins, PlayerProgress.GetBonusAtk(unitName));
                AddCoinBonus(stats.defCoins, PlayerProgress.GetBonusDef(unitName));
            }
            currentHP = stats.maxHP;
            weapon = def.weapon;
            weaponSlots[0] = weapon;
            weaponSlots[1] = WeaponLibrary.BigVariant(weapon.type);
            position = def.startPosition;
            shakeSeed = UnityEngine.Random.Range(0f, 100f);

            gameObject.name = unitName;

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(transform, false);
            visualRoot = visualGo.transform;

            Color teamColor = def.teamColorOverride.a > 0f
                ? def.teamColorOverride
                : (team == Team.Player ? new Color(0.25f, 0.45f, 0.95f) : new Color(0.85f, 0.2f, 0.2f));

            portraitSprite = string.IsNullOrEmpty(def.portraitFile) ? null : VisualFactory.LoadPortraitSprite(def.portraitFile);
            illustrationSprite = string.IsNullOrEmpty(def.illustrationFile) ? null : VisualFactory.LoadIllustrationSprite(def.illustrationFile);
            illustrationOffset = def.illustrationOffset;
            illustrationZoom = def.illustrationZoom;

            bodyRenderer = visualGo.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = portraitSprite != null ? portraitSprite : VisualFactory.ClassSprite(weapon.type, teamColor);
            bodyRenderer.sortingOrder = 10;

            BuildTileOutline();
            BuildHpBar();
            BuildJobIcon();
            SnapToGrid(grid);
            RefreshHpBar();
        }

        // 반투명하게 둬서 밑에 깔린 타일 색(잔디 그림의 밝고 어두운 부분 등)이 살짝 비쳐 보이도록 함
        private static readonly Color PlayerTileOutlineColor = new Color(0.3f, 0.55f, 1f, 0.5f);
        private static readonly Color EnemyTileOutlineColor = new Color(1f, 0.25f, 0.25f, 0.5f);
        private const int TileOutlineThickness = 18;

        // 이 유닛이 서 있는 칸 테두리에 선을 그려서 아군(파랑)/적(빨강)이 있는 칸을 한눈에 구분되게 함.
        // visualRoot가 아니라 루트(transform)에 붙여서 브레이크 상태의 흔들림에 영향받지 않고 칸에 고정됨
        private void BuildTileOutline()
        {
            var outlineGo = new GameObject("TileOutline");
            outlineGo.transform.SetParent(transform, false);
            Color color = team == Team.Player ? PlayerTileOutlineColor : EnemyTileOutlineColor;
            var sr = outlineGo.AddComponent<SpriteRenderer>();
            // 투명한 안쪽 색을 테두리색과 같은 RGB로 맞춰서, 이중선형 필터링으로 경계가 어둡게 번지지 않게 함
            sr.sprite = VisualFactory.SquareSprite(new Color(color.r, color.g, color.b, 0f), color, 256, TileOutlineThickness);
            sr.sortingOrder = 2; // 타일(0)/이동·공격 하이라이트(1)보다 위, 캐릭터 그림(10)보다는 아래
        }

        // 브레이크 상태인 동안 그리드/전투 애니메이션(루트 transform)과 겹치지 않게 자식(Visual)만 흔듦
        private void Update()
        {
            if (visualRoot == null) return;

            if (isBroken && IsAlive)
            {
                float offset = Mathf.Sin((Time.time + shakeSeed) * BrokenShakeFrequency) * BrokenShakeAmplitude;
                visualRoot.localPosition = new Vector3(offset, 0f, 0f);
            }
            else if (visualRoot.localPosition != Vector3.zero)
            {
                visualRoot.localPosition = Vector3.zero;
            }
        }

        private void BuildHpBar()
        {
            var bgGo = new GameObject("HPBarBG");
            bgGo.transform.SetParent(visualRoot, false);
            bgGo.transform.localPosition = new Vector3(HpBarLeftX, 0.42f, 0f);
            bgGo.transform.localScale = new Vector3(HpBarWidth, 0.12f, 1f);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = VisualFactory.FlatSprite(new Color(0.08f, 0.08f, 0.08f), new Vector2(0f, 0.5f));
            bg.sortingOrder = 11;

            var fillGo = new GameObject("HPBarFill");
            fillGo.transform.SetParent(visualRoot, false);
            fillGo.transform.localPosition = new Vector3(HpBarLeftX, 0.42f, 0f);
            fillGo.transform.localScale = new Vector3(HpBarWidth, 0.12f, 1f);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = VisualFactory.FlatSprite(new Color(0.25f, 0.85f, 0.3f), new Vector2(0f, 0.5f));
            fill.sortingOrder = 12;
            hpFillTransform = fillGo.transform;
        }

        private const float JobIconCircleScale = 0.32f; // 기존 0.22에서 키움
        private const float JobIconShapeScale = 0.27f;  // 원 대비 비율을 키워서 배경(테두리처럼 보이던 여백)을 얇게

        // 체력바 옆에 작은 원 + 무기 타입(직업 계열) 모양 아이콘을 넣어, 원거리 시점에서도 이 캐릭터의 직업을 한눈에 구분할 수 있게 함
        private void BuildJobIcon()
        {
            var circleGo = new GameObject("JobIconCircle");
            circleGo.transform.SetParent(visualRoot, false);
            circleGo.transform.localPosition = JobIconLocalPosition;
            circleGo.transform.localScale = new Vector3(JobIconCircleScale, JobIconCircleScale, 1f);
            var circle = circleGo.AddComponent<SpriteRenderer>();
            circle.sprite = VisualFactory.CircleSprite(new Color(0.05f, 0.05f, 0.07f, 0.9f));
            circle.sortingOrder = 11;

            var shapeGo = new GameObject("JobIconShape");
            shapeGo.transform.SetParent(visualRoot, false);
            shapeGo.transform.localPosition = JobIconLocalPosition;
            shapeGo.transform.localScale = new Vector3(JobIconShapeScale, JobIconShapeScale, 1f);
            var shape = shapeGo.AddComponent<SpriteRenderer>();
            shape.sprite = VisualFactory.WeaponIconSprite(weapon.type);
            shape.sortingOrder = 12;

            var overlayGo = new GameObject("MatchupOverlay");
            overlayGo.transform.SetParent(visualRoot, false);
            overlayGo.transform.localPosition = JobIconLocalPosition;
            overlayGo.transform.localScale = new Vector3(JobIconCircleScale, JobIconCircleScale, 1f);
            matchupOverlayRenderer = overlayGo.AddComponent<SpriteRenderer>();
            matchupOverlayRenderer.sortingOrder = 13;
            overlayGo.SetActive(false);
        }

        // 캐릭터를 선택했을 때, 그 캐릭터 기준으로 이 유닛과 붙으면 브레이크를 주는지/당하는지를 원 위에 표시
        public void SetMatchupIndicator(MatchupIndicator kind)
        {
            if (matchupOverlayRenderer == null) return;

            if (kind == MatchupIndicator.None)
            {
                matchupOverlayRenderer.gameObject.SetActive(false);
                return;
            }

            matchupOverlayRenderer.sprite = kind == MatchupIndicator.CanBreak
                ? VisualFactory.XMarkSprite(new Color(0.95f, 0.15f, 0.15f))
                : VisualFactory.FireSprite();
            matchupOverlayRenderer.gameObject.SetActive(true);
        }

        private void RefreshHpBar()
        {
            float frac = stats.maxHP > 0 ? Mathf.Clamp01((float)currentHP / stats.maxHP) : 0f;
            var s = hpFillTransform.localScale;
            hpFillTransform.localScale = new Vector3(HpBarWidth * frac, s.y, s.z);
        }

        // 데미지를 입었을 때 체력바를 즉시 줄이지 않고 fromFrac -> toFrac으로 서서히 깎아 "깎이는 느낌"을 줌
        // 죽는 경우(die)엔 체력바가 다 깎일 때까지 기다렸다가 그제서야 유닛을 감춤
        private IEnumerator AnimateHpBar(float fromFrac, float toFrac, bool die)
        {
            float t = 0f;
            while (t < HpBarAnimDuration)
            {
                t += Time.deltaTime;
                float frac = Mathf.Lerp(fromFrac, toFrac, t / HpBarAnimDuration);
                var s = hpFillTransform.localScale;
                hpFillTransform.localScale = new Vector3(HpBarWidth * frac, s.y, s.z);
                yield return null;
            }
            var final = hpFillTransform.localScale;
            hpFillTransform.localScale = new Vector3(HpBarWidth * toFrac, final.y, final.z);
            hpBarAnimRoutine = null;

            if (die)
                gameObject.SetActive(false);
        }

        public int TakeDamage(int amount)
        {
            amount = Mathf.Max(0, amount);
            float maxHP = Mathf.Max(1, stats.maxHP);
            // 연속 공격 등으로 이전 애니메이션이 끝나기 전에 또 맞아도, currentHP 기준값이 아니라
            // 지금 실제로 보이는 막대 지점부터 이어서 깎이도록 해 중간에 튀어 보이지 않게 함
            float fromFrac = hpFillTransform.localScale.x / HpBarWidth;
            currentHP = Mathf.Max(0, currentHP - amount);
            float toFrac = Mathf.Clamp01(currentHP / maxHP);

            if (hpBarAnimRoutine != null) StopCoroutine(hpBarAnimRoutine);
            hpBarAnimRoutine = StartCoroutine(AnimateHpBar(fromFrac, toFrac, !IsAlive));

            DamagePopup.Spawn(transform.position, amount);
            return amount;
        }

        // 캐릭터 확인 화면의 "캐릭터 레벨업" 한 번으로 체력/기본공격/기본수비가 함께 오름.
        // 씬을 다시 로드하지 않고도 바로 반영되도록 이미 Initialize된 런타임 스탯을 직접 올림
        public void LevelUp(int hpAmount, int atkAmount, int defAmount)
        {
            stats.maxHP += hpAmount;
            currentHP += hpAmount;
            RefreshHpBar();
            AddCoinBonus(stats.atkCoins, atkAmount);
            AddCoinBonus(stats.defCoins, defAmount);
        }

        // 코인 리스트의 첫 번째 코인 앞/뒷면에 각각 bonus를 더해, 그 코인의 평균(및 기본공격/기본수비 합산값)이 정확히 bonus만큼 오르게 함
        private static void AddCoinBonus(List<Coin> coins, int bonus)
        {
            if (bonus == 0 || coins.Count == 0) return;
            var c = coins[0];
            c.heads += bonus;
            c.tails += bonus;
            coins[0] = c;
        }

        public void SnapToGrid(GridManager grid)
        {
            transform.position = grid.GridToWorld(position);
        }

        public void SetGridPosition(GridManager grid, GridPosition newPos)
        {
            grid.RemoveOccupant(position);
            position = newPos;
            grid.SetOccupant(position, this);
            transform.position = grid.GridToWorld(position);
        }

        public void RefreshActedVisual()
        {
            if (!IsAlive) return;
            bodyRenderer.color = hasActed ? new Color(0.28f, 0.28f, 0.28f, 1f) : Color.white;
        }
    }
}
