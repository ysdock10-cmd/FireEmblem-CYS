using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 전투 예측창(아군/데미지 예측/적 3분할 패널)
    public partial class UIManager
    {
        // 전투창에서 코인 개수를 표시하는 작은 원 색: 공격 코인은 노란색, 수비 코인은 파란색(코인 다이얼 색과 계열을 맞춤)
        private static readonly Color CoinDotAtkColor = new Color(0.85f, 0.68f, 0.15f, 1f);
        private static readonly Color CoinDotDefColor = new Color(0.35f, 0.65f, 0.95f, 1f);

        // 전투 예측칸을 띄움
        public void ShowForecast(Unit attacker, Unit defender, CombatForecast f)
        {
            // 전투창이 뜨는 동안엔 같은 자리를 다루는 캐릭터 정보창은 숨김
            bottomBar.SetActive(false);
            battlePanel.SetActive(true);

            int allyIncomingDamage = f.defenderCanCounter ? f.defenderDamage : 0;
            int enemyIncomingDamage = f.attackerDamage;

            SetBattleUnitInfo(battleAllyInfo, attacker, allyIncomingDamage);
            UpdateBattleMidTexts(f);
            SetBattleUnitInfo(battleEnemyInfo, defender, enemyIncomingDamage);
        }

        public void ClearForecast()
        {
            battlePanel.SetActive(false);
            bottomBar.SetActive(true);
            confirmAttackButton.gameObject.SetActive(false);
            weaponChangeButton.gameObject.SetActive(false);
        }

        // 적을 한 번 선택(타겟 지정)해서 전투 예측창이 떠 있는 동안, 취소 옆에 "공격" 버튼을 노출해 바로 확정할 수 있게 함
        public void ShowAttackConfirmButton(System.Action onAttack)
        {
            confirmAttackButton.onClick.RemoveAllListeners();
            confirmAttackButton.onClick.AddListener(() => onAttack());
            confirmAttackButton.interactable = true; // 이전에 RestrictTargetConfirmToAttack으로 잠겼을 수 있으니 매번 새로 열 때 원래대로 되돌림
            confirmAttackButton.gameObject.SetActive(true);
        }

        // 공격 버튼 왼쪽에 같이 뜸: 누르면 지금 타겟까지 사거리가 닿는 무기들끼리 순환 장착함(무기가 하나뿐이면 호출한 쪽에서 아예 안 띄움)
        public void ShowWeaponChangeButton(System.Action onWeaponChange)
        {
            weaponChangeButton.onClick.RemoveAllListeners();
            weaponChangeButton.onClick.AddListener(() => onWeaponChange());
            weaponChangeButton.interactable = true; // 이전에 RestrictTargetConfirmToAttack으로 잠겼을 수 있으니 매번 새로 열 때 원래대로 되돌림
            weaponChangeButton.gameObject.SetActive(true);
        }

        // 튜토리얼이 "지금은 공격 확정만" 안내할 때, 취소/무기변경을 눌러도 반응하지 않게 잠금
        public void RestrictTargetConfirmToAttack()
        {
            targetCancelButton.interactable = false;
            weaponChangeButton.interactable = false;
        }

        // 전투 연출(코인 굴리기 + 카메라 줌인) 동안엔 예측 패널도, 캐릭터 정보창(bottomBar)도 화면에서 완전히 비워서
        // 카메라가 비추는 두 유닛에만 시선이 가게 함. ClearForecast는 bottomBar를 다시 켜버리므로 이 경우엔 쓰면 안 됨
        public void HideAllBattleUI()
        {
            battlePanel.SetActive(false);
            bottomBar.SetActive(false);
            confirmAttackButton.gameObject.SetActive(false);
            weaponChangeButton.gameObject.SetActive(false);
        }

        // 위에서부터 간격을 두고: 이름/직업/레벨/무기(가로 한 줄) - 체력(코인은 그 옆에 공격/수비 2줄로) - 체력바
        private BattleUnitInfo CreateBattleUnitInfoBlock(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            var containerGo = new GameObject("BattleUnitInfo");
            containerGo.transform.SetParent(parent, false);
            var rt = containerGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(rt, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            // 위에서부터: 이름/직업/레벨/무기 - (간격) - 체력+코인(체력 옆에 공격코인/수비코인 2줄) - (간격) - 체력바
            var info = new BattleUnitInfo
            {
                container = rt,
                background = bgImg,
                // 이름 칸을 넓게 잡아 긴 이름도 잘리지 않게 함(직업/레벨을 오른쪽 끝으로 옮기며 확보한 공간)
                nameText = CreateRegionText(rt, new Vector2(0.04f, 0.72f), new Vector2(0.46f, 1f), TextAnchor.MiddleLeft, 40, 0f, 4f),
                // 들고 있는 무기를 아이콘+이름으로 표시(클릭 불가, 정보 표시 전용)
                weaponIcon = CreateWeaponIconPlaceholder(rt, new Vector2(0.52f, 0.86f), 20f),
                weaponText = CreateRegionText(rt, new Vector2(0.56f, 0.72f), new Vector2(0.78f, 1f), TextAnchor.MiddleLeft, 26, 0f, 4f),
                // 직업/레벨은 맨 오른쪽 끝으로 옮김. 글씨 크기는 코인 라벨과 같은 크기(22)로 맞춤
                jobLevelText = CreateRegionText(rt, new Vector2(0.80f, 0.72f), new Vector2(0.98f, 1f), TextAnchor.MiddleLeft, 22, 0f, 4f),
                // 이름 줄과 간격을 두고(0.64~0.72 비움) 체력을 표시. 코인이 옆에 오므로 세로로는 이 칸 전체 높이를 다 씀
                hpLabelText = CreateRegionText(rt, new Vector2(0.04f, 0.30f), new Vector2(0.12f, 0.64f), TextAnchor.MiddleLeft, 18, 0f, 2f),
                hpText = CreateRegionText(rt, new Vector2(0.15f, 0.30f), new Vector2(0.38f, 0.64f), TextAnchor.MiddleLeft, 38, 0f, 2f),
                // 코인 정보는 체력 옆에(0.38~0.44 간격을 두고): 공격코인(위)/수비코인(아래) 2줄로 나눠 라벨+원을 함께 표시
                atkCoinLabel = CreateRegionText(rt, new Vector2(0.44f, 0.49f), new Vector2(0.66f, 0.64f), TextAnchor.MiddleLeft, 22, 0f, 2f),
                atkCoinDots = CreateCoinDots(rt, new Vector2(0.70f, 0.49f), new Vector2(0.98f, 0.64f), MaxCoinDots, CoinDotAtkColor),
                defCoinLabel = CreateRegionText(rt, new Vector2(0.44f, 0.30f), new Vector2(0.66f, 0.45f), TextAnchor.MiddleLeft, 22, 0f, 2f),
                defCoinDots = CreateCoinDots(rt, new Vector2(0.70f, 0.30f), new Vector2(0.98f, 0.45f), MaxCoinDots, CoinDotDefColor),
            };
            info.nameText.fontStyle = FontStyle.Bold;
            info.hpText.fontStyle = FontStyle.Bold;
            info.atkCoinLabel.text = "공격코인";
            info.defCoinLabel.text = "수비코인";

            info.nameText.color = textColor;
            info.jobLevelText.color = textColor;
            info.weaponText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            info.atkCoinLabel.color = textColor;
            info.defCoinLabel.color = textColor;

            // 체력바를 더 얇게 하고 아래쪽으로 내려서, 위쪽에 남는 체력 숫자를 표시할 빈 공간을 확보
            var (damagePreview, fill, splitMarker, splitLabel) = CreateHpBar(rt, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.11f));
            info.hpBarDamagePreview = damagePreview;
            info.hpBarDamagePreviewImage = damagePreview.GetComponent<Image>();
            info.hpBarFill = fill;
            info.hpBarSplitMarker = splitMarker;
            info.hpBarSplitLabel = splitLabel;

            return info;
        }

        // 배경(빈 칸) 위에 깜박이는 색(이번 전투로 깎일 체력) → 그 위에 초록(전투 후 남는 체력) 순으로 겹쳐, 두 값의 차이만큼 깜박이는 구간이 드러남
        private (RectTransform damagePreview, RectTransform fill, RectTransform splitMarker, Text splitLabel) CreateHpBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var barGo = new GameObject("HpBar");
            barGo.transform.SetParent(parent, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = anchorMin; barRt.anchorMax = anchorMax;
            barRt.offsetMin = Vector2.zero; barRt.offsetMax = Vector2.zero;

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(barRt, false);
            var trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one;
            trackRt.offsetMin = Vector2.zero; trackRt.offsetMax = Vector2.zero;
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0f, 0f, 0f, 0.5f);
            trackImg.raycastTarget = false;

            var damageGo = new GameObject("DamagePreview");
            damageGo.transform.SetParent(barRt, false);
            var damageRt = damageGo.AddComponent<RectTransform>();
            damageRt.anchorMin = new Vector2(0f, 0f); damageRt.anchorMax = new Vector2(0f, 1f);
            damageRt.offsetMin = Vector2.zero; damageRt.offsetMax = Vector2.zero;
            var damageImg = damageGo.AddComponent<Image>();
            damageImg.color = Color.white;
            damageImg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRt, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.3f);
            fillImg.raycastTarget = false;

            // 깜박이는 구간(깎일 체력)과 안 깜박이는 구간(남는 체력)의 경계를 짚어주는 빨간 막대 줄(삼각형 대신).
            // 세로로 막대 전체 높이만큼 걸치고 위아래로 살짝 튀어나오게 해서 경계선이 뚜렷이 보이게 함
            var markerGo = new GameObject("SplitMarker");
            markerGo.transform.SetParent(barRt, false);
            var markerRt = markerGo.AddComponent<RectTransform>();
            markerRt.anchorMin = new Vector2(0f, 0f); markerRt.anchorMax = new Vector2(0f, 1f);
            markerRt.pivot = new Vector2(0.5f, 0.5f);
            markerRt.sizeDelta = new Vector2(5f, 8f);
            var markerImg = markerGo.AddComponent<Image>();
            markerImg.sprite = VisualFactory.FlatSprite(new Color(0.95f, 0.15f, 0.15f), new Vector2(0.5f, 0.5f));
            markerImg.raycastTarget = false;

            // 전투 후 남는 체력 숫자: 막대 안이 아니라 막대 위쪽 빈 공간에 크게 표시해 시인성을 높임
            var labelGo = new GameObject("SplitLabel");
            labelGo.transform.SetParent(barRt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 1f); labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 4f);
            labelRt.sizeDelta = new Vector2(58f, 32f);
            var labelText = labelGo.AddComponent<Text>();
            ConfigureText(labelText, 22, TextAnchor.MiddleCenter);
            labelText.fontStyle = FontStyle.Bold;
            var labelOutline = labelGo.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            return (damageRt, fillRt, markerRt, labelText);
        }

        private void SetBattleUnitInfo(BattleUnitInfo info, Unit u, int incomingDamage)
        {
            bool isAlly = u.team == Team.Player;
            info.background.color = isAlly ? AllyInfoBgColor : EnemyInfoBgColor;
            Color textColor = isAlly ? AllyInfoTextColor : EnemyInfoTextColor;
            info.nameText.color = textColor;
            info.jobLevelText.color = textColor;
            info.weaponText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            info.atkCoinLabel.color = textColor;
            info.defCoinLabel.color = textColor;

            info.nameText.text = u.unitName;
            info.jobLevelText.text = $"{JobName(u.weapon.type)} Lv.{u.stats.level}";
            SetWeaponIcon(info.weaponIcon, u.weapon);
            info.weaponText.text = u.weapon.weaponName;
            info.hpLabelText.text = "HP";
            info.hpText.text = $"{u.currentHP}/{u.stats.maxHP}";

            for (int i = 0; i < info.atkCoinDots.Length; i++)
                info.atkCoinDots[i].gameObject.SetActive(i < u.stats.atkCoins.Count);
            for (int i = 0; i < info.defCoinDots.Length; i++)
                info.defCoinDots[i].gameObject.SetActive(i < u.stats.defCoins.Count);

            float maxHP = Mathf.Max(1, u.stats.maxHP);
            float currentFraction = Mathf.Clamp01(u.currentHP / maxHP);
            float postHP = Mathf.Clamp(u.currentHP - incomingDamage, 0, u.stats.maxHP);
            float postFraction = Mathf.Clamp01(postHP / maxHP);

            info.hpBarDamagePreview.anchorMax = new Vector2(currentFraction, 1f);
            info.hpBarFill.anchorMax = new Vector2(postFraction, 1f);

            info.hpBarSplitMarker.anchorMin = new Vector2(postFraction, 0f);
            info.hpBarSplitMarker.anchorMax = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.rectTransform.anchorMin = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.rectTransform.anchorMax = new Vector2(postFraction, 1f);
            info.hpBarSplitLabel.text = ((int)postHP).ToString();
        }

        // anchorMin~anchorMax 구간에 작은 원(코인 하나당 하나씩) maxCount개를 가로로 나란히 만들어 둠.
        // 실제 코인 개수만큼만 SetActive(true)로 켜서 쓰고 나머지는 숨김(SetBattleUnitInfo에서 갱신)
        private Image[] CreateCoinDots(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int maxCount, Color color)
        {
            var dots = new Image[maxCount];
            float cellW = (anchorMax.x - anchorMin.x) / maxCount;
            float centerY = (anchorMin.y + anchorMax.y) / 2f;
            var sprite = VisualFactory.CircleSprite(color);
            for (int i = 0; i < maxCount; i++)
            {
                var go = new GameObject($"CoinDot{i}");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                float centerX = anchorMin.x + cellW * (i + 0.5f);
                rt.anchorMin = new Vector2(centerX, centerY); rt.anchorMax = rt.anchorMin; rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(16f, 16f);

                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                dots[i] = img;
            }
            return dots;
        }

        // 가운데 예측 칸을 위(내가 주는 피해)/아래(적이 주는 피해) 두 칸으로 뚜렷이 나누고, 각각 그 안에 BREAK/화살표/스탯을 고정 배치.
        // BREAK 유무와 상관없이 화살표·스탯 위치가 항상 그대로라 안 밀리고, Best Fit으로 글자가 칸 밖으로 넘치지 않게 함
        private void CreateBattleMidTexts()
        {
            // 내 피해 칸(위쪽 절반) / 적 피해 칸(아래쪽 절반) 배경을 옅게 색으로 구분
            CreateMidHalfBackground(new Vector2(MidColumnMin, 0.5f), new Vector2(MidColumnMax, 1f), AllyColor);
            CreateMidHalfBackground(new Vector2(MidColumnMin, 0f), new Vector2(MidColumnMax, 0.5f), EnemyColor);

            // 두 칸 사이 경계선
            var dividerGo = new GameObject("MidDivider");
            dividerGo.transform.SetParent(battlePanel.transform, false);
            var dividerRt = dividerGo.AddComponent<RectTransform>();
            dividerRt.anchorMin = new Vector2(MidColumnMin, 0.5f); dividerRt.anchorMax = new Vector2(MidColumnMax, 0.5f);
            dividerRt.pivot = new Vector2(0.5f, 0.5f);
            dividerRt.sizeDelta = new Vector2(0f, 2f);
            var dividerImg = dividerGo.AddComponent<Image>();
            dividerImg.color = new Color(1f, 1f, 1f, 0.25f);
            dividerImg.raycastTarget = false;

            // 칸 배분(BREAK/화살표/스탯 = 0.14/0.14/0.22): BREAK 시인성을 위해 화살표 칸에서 공간을 좀 덜어옴(스탯 칸은 그대로 유지)
            allyBreakText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.86f), new Vector2(MidColumnMax, 1f), TextAnchor.MiddleCenter, 18, 6f, 0f);
            allyArrowText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.72f), new Vector2(MidColumnMax, 0.86f), TextAnchor.MiddleCenter, 34, 6f, 0f);
            allyStatsText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.50f), new Vector2(MidColumnMax, 0.72f), TextAnchor.MiddleCenter, 24, 6f, 0f);

            enemyStatsText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0f), new Vector2(MidColumnMax, 0.22f), TextAnchor.MiddleCenter, 24, 6f, 0f);
            enemyArrowText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.22f), new Vector2(MidColumnMax, 0.36f), TextAnchor.MiddleCenter, 34, 6f, 0f);
            enemyBreakText = CreateRegionText(battlePanel.transform, new Vector2(MidColumnMin, 0.36f), new Vector2(MidColumnMax, 0.5f), TextAnchor.MiddleCenter, 18, 6f, 0f);

            allyBreakText.fontStyle = FontStyle.Bold;
            enemyBreakText.fontStyle = FontStyle.Bold;
            allyBreakText.text = "BREAK!";
            enemyBreakText.text = "BREAK!";
            allyBreakText.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);
            enemyBreakText.color = new Color(BreakColor.r, BreakColor.g, BreakColor.b, 0f);

            // 글자가 길어져도(피해 두 자리, 명중 100% 등) 칸 밖으로 넘치지 않도록 자동으로 줄어들게 함
            // 화살표(최대 46)/BREAK(최대 30)/스탯(최대 26) 모두 시인성을 위해 키움
            EnableBestFit(allyBreakText, 14, 30);
            EnableBestFit(allyArrowText, 20, 46);
            EnableBestFit(allyStatsText, 14, 26);
            EnableBestFit(enemyBreakText, 14, 30);
            EnableBestFit(enemyArrowText, 20, 46);
            EnableBestFit(enemyStatsText, 14, 26);
        }

        private void CreateMidHalfBackground(Vector2 anchorMin, Vector2 anchorMax, Color tint)
        {
            var go = new GameObject("MidHalfBackground");
            go.transform.SetParent(battlePanel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(tint.r, tint.g, tint.b, 0.12f);
            img.raycastTarget = false;
        }

        private const string AllyArrowLine = "――――――→"; // 피해/명중 위에 길게 놓는 화살표 (시인성)
        private const string EnemyArrowLine = "←――――――";
        private const string EnemyDamageHighlightHex = "FF7A3D"; // 적이 넣는 피해 강조색

        // "피해 X" 부분만 굵게+색을 넣어 강조
        private static string BuildStatsLine(int damage, string highlightHex)
            => $"<b><color=#{highlightHex}>피해 {damage}</color></b>";

        private void UpdateBattleMidTexts(CombatForecast f)
        {
            allyBreakActive = f.attackerHasAdvantage;
            allyArrowText.text = AllyArrowLine;
            allyArrowText.color = f.attackerHasAdvantage ? BreakColor : AllyColor;
            // 아군이 넣는 피해는 아군 색(파랑)으로 강조
            allyStatsText.text = BuildStatsLine(f.attackerDamage, ColorUtility.ToHtmlStringRGB(AllyColor));

            enemyBreakActive = f.defenderHasAdvantage;
            if (f.defenderCanCounter)
            {
                enemyArrowText.text = EnemyArrowLine;
                enemyArrowText.color = f.defenderHasAdvantage ? BreakColor : EnemyColor;
                enemyStatsText.text = BuildStatsLine(f.defenderDamage, EnemyDamageHighlightHex);
            }
            else
            {
                enemyArrowText.text = "—";
                enemyArrowText.color = EnemyColor;
                enemyStatsText.text = "반격 없음";
            }
        }
    }
}
