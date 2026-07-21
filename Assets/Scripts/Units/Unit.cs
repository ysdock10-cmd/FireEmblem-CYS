using System.Collections;
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

            BuildHpBar();
            BuildJobIcon();
            SnapToGrid(grid);
            RefreshHpBar();
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
