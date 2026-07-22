using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 캐릭터 정보칸(선택/정찰 중 유닛 표시, 캐릭터 확인 화면)
    public partial class UIManager
    {
        public void ShowSelectedUnit(Unit u, Action onCancel = null)
        {
            bottomBar.SetActive(true);
            SetUnitInfo(selectedInfo, u);

            if (onCancel != null)
            {
                targetCancelButton.onClick.RemoveAllListeners();
                targetCancelButton.onClick.AddListener(() => onCancel());
                targetCancelButton.interactable = true; // 이전에 RestrictTargetConfirmToAttack으로 잠겼을 수 있으니 매번 새로 열 때 원래대로 되돌림
                targetCancelButton.gameObject.SetActive(true);
            }
        }

        public void HideSelectedUnit()
        {
            bottomBar.SetActive(false);
            targetCancelButton.gameObject.SetActive(false);
        }

        // 캐릭터 확인 화면. units 목록으로 선택 버튼을 채우고(기본은 첫 번째 캐릭터를 보여줌),
        // 버튼을 누르면 그 캐릭터 정보를 전투 중과 같은 정보칸에 표시함
        public void ShowCharacterView(IReadOnlyList<Unit> units, Action onBack)
        {
            characterViewPanel.SetActive(true);

            for (int i = 0; i < characterSelectButtons.Length; i++)
            {
                var button = characterSelectButtons[i];
                button.onClick.RemoveAllListeners();
                bool hasUnit = i < units.Count;
                button.gameObject.SetActive(hasUnit);
                if (!hasUnit) continue;

                var unit = units[i];
                button.GetComponentInChildren<Text>().text = unit.unitName;
                button.onClick.AddListener(() => SelectCharacterViewUnit(unit));
            }

            // 처음 열었을 땐 이름 버튼만 보이고, 캐릭터를 눌러야 그때 정보(일러스트/스탯/레벨업 버튼)가 나타남
            HideCharacterViewDetail();
            RefreshCharacterViewLevelUp(); // 재화 표시만 최신으로(캐릭터 미선택 상태라 나머지는 그대로 비워 둠)

            characterViewLevelUpButton.onClick.RemoveAllListeners();
            characterViewLevelUpButton.onClick.AddListener(() =>
            {
                if (characterViewCurrentUnit == null) return;
                if (PlayerProgress.TryLevelUp(characterViewCurrentUnit.unitName))
                {
                    // 체력/기본공격/기본수비가 "캐릭터 레벨업" 한 번으로 함께 오름(스탯별 개별 강화 없음)
                    characterViewCurrentUnit.LevelUp(PlayerProgress.HpPerLevelUp, PlayerProgress.AtkPerLevelUp, PlayerProgress.DefPerLevelUp);
                    SelectCharacterViewUnit(characterViewCurrentUnit); // 정보칸/재화/버튼 상태를 전부 새로고침
                }
            });

            characterViewExitButton.onClick.RemoveAllListeners();
            characterViewExitButton.onClick.AddListener(() =>
            {
                characterViewPanel.SetActive(false);
                onBack();
            });
        }

        private void SelectCharacterViewUnit(Unit unit)
        {
            characterViewCurrentUnit = unit;

            // 캐릭터를 고른 순간 선택 버튼 줄을 치우고, 그 자리까지 정보칸을 넓힘(일러스트/스탯이 그만큼 커짐)
            foreach (var button in characterSelectButtons) button.gameObject.SetActive(false);
            characterViewContentRt.anchorMax = new Vector2(1f, DetailContentHeightExpanded);

            characterViewLevelUpButton.gameObject.SetActive(true);
            SetCharacterViewDetail(characterViewDetail, unit);
            RefreshCharacterViewLevelUp();
        }

        // 캐릭터를 고르기 전(화면을 막 열었을 때)엔 이름 버튼 줄만 보이고 정보칸은 전부 비워/숨겨 둠(높이도 좁은 상태로 되돌림)
        private void HideCharacterViewDetail()
        {
            characterViewCurrentUnit = null;
            characterViewContentRt.anchorMax = new Vector2(1f, DetailContentHeightCompact);

            characterViewDetail.illustrationImage.gameObject.SetActive(false);
            characterViewDetail.nameText.text = string.Empty;
            characterViewDetail.jobLevelText.text = string.Empty;
            characterViewDetail.hpLabelText.text = string.Empty;
            characterViewDetail.hpText.text = string.Empty;
            characterViewDetail.hpBarFill.anchorMax = new Vector2(0f, 1f);
            foreach (var t in characterViewDetail.statTexts) t.text = string.Empty;

            characterViewLevelUpButton.gameObject.SetActive(false);
            characterViewLevelUpInfoText.text = string.Empty;
        }

        // 재화 표시와 강화 버튼(비용/효과 안내, 재화 부족 시 비활성화)을 최신 상태로 갱신
        private void RefreshCharacterViewLevelUp()
        {
            characterViewCurrencyText.text = $"재화 {PlayerProgress.Currency}";
            if (characterViewCurrentUnit == null) return;

            int levelUps = PlayerProgress.GetLevelUps(characterViewCurrentUnit.unitName);
            var u = characterViewCurrentUnit;
            characterViewLevelUpInfoText.text = $"캐릭터 레벨업 {levelUps}회 (체력 {u.stats.maxHP} / 기본공격 {u.stats.atk} / 기본수비 {u.stats.def})";

            bool canAfford = PlayerProgress.Currency >= PlayerProgress.LevelUpCost;
            characterViewLevelUpButton.interactable = canAfford;
            characterViewLevelUpButton.GetComponentInChildren<Text>().text =
                $"캐릭터 레벨업\n(체력+{PlayerProgress.HpPerLevelUp}, 공격+{PlayerProgress.AtkPerLevelUp}, 수비+{PlayerProgress.DefPerLevelUp}, 비용 {PlayerProgress.LevelUpCost})";
        }

        // 정보칸 높이(전체 화면 기준 위쪽 경계). 선택 버튼 줄이 보일 땐 좁게(Compact), 캐릭터를 선택해 버튼 줄이 사라지면
        // 그 자리까지 넓게(Expanded) 써서 일러스트/스탯이 다 같이 커짐. 아래 배치값은 전부 Compact(0.60) 기준 비율이었던 걸
        // characterViewContentRt 안에서의 비율(0~1)로 다시 환산한 값이라, 컨테이너 높이만 바꾸면 안의 내용도 같이 늘어남
        private const float DetailContentHeightCompact = 0.60f;
        private const float DetailContentHeightExpanded = 0.84f; // 제목(0.86~) 바로 아래까지

        // 캐릭터 확인 화면 일러스트를 박스 안에 딱 맞는 크기보다 이 배수만큼 더 키워서 보여줌(박스 밖으로 넘쳐도 됨)
        private const float CharacterIllustrationScale = 2f;
        // 위로 조금 올리고 왼쪽으로 옮긴 공통 기본 위치(대부분의 캐릭터가 이 값을 그대로 씀)
        private static readonly Vector2 CharacterIllustrationBaseOffset = new Vector2(-80f, 80f);

        // 캐릭터 확인 화면 본문: 왼쪽엔 캐릭터 일러스트를 크게(무기 정보 없이), 오른쪽엔 이름/레벨-직업/체력/스탯(무기 제외)을 세로로 나열.
        // 레벨업 버튼/안내문은 이미 필드로 있는 characterViewLevelUpButton/InfoText를 여기서 만들어 이 세로 목록 맨 아래에 둠
        private CharacterViewDetail CreateCharacterViewDetail(RectTransform parent)
        {
            var contentGo = new GameObject("CharacterViewContent");
            contentGo.transform.SetParent(parent, false);
            characterViewContentRt = contentGo.AddComponent<RectTransform>();
            characterViewContentRt.anchorMin = new Vector2(0f, 0f);
            characterViewContentRt.anchorMax = new Vector2(1f, DetailContentHeightCompact);
            characterViewContentRt.offsetMin = Vector2.zero; characterViewContentRt.offsetMax = Vector2.zero;

            // 왼쪽 일러스트: 뒤로가기 버튼이 없어져서 아래까지 전부 쓸 수 있음 + 가로도 오른쪽 목록 쪽으로 크게 넓힘
            var illustrationGo = new GameObject("Illustration");
            illustrationGo.transform.SetParent(characterViewContentRt, false);
            var illustrationBoxRt = illustrationGo.AddComponent<RectTransform>();
            illustrationBoxRt.anchorMin = new Vector2(0.02f, 0.03f);
            illustrationBoxRt.anchorMax = new Vector2(0.48f, 1f);
            illustrationBoxRt.offsetMin = Vector2.zero; illustrationBoxRt.offsetMax = Vector2.zero;

            var illustrationImgGo = new GameObject("Image");
            illustrationImgGo.transform.SetParent(illustrationBoxRt, false);
            var illustrationImgRt = illustrationImgGo.AddComponent<RectTransform>();
            // 박스 위쪽 가운데를 기준점으로 잡음: 2배로 확대해도 얼굴(그림 위쪽)은 항상 박스 위쪽에 그대로 있고, 몸통만 아래로 더 길게 늘어나 박스 밑으로 넘침
            illustrationImgRt.anchorMin = new Vector2(0.5f, 1f); illustrationImgRt.anchorMax = new Vector2(0.5f, 1f); illustrationImgRt.pivot = new Vector2(0.5f, 1f);
            // 실제 위치는 캐릭터별로 다를 수 있어 SetCharacterViewDetail에서 매번 다시 계산해 넣음(여기 값은 초기값일 뿐)
            illustrationImgRt.anchoredPosition = CharacterIllustrationBaseOffset;
            var illustrationImg = illustrationImgGo.AddComponent<Image>();
            illustrationImg.raycastTarget = false;

            // 오른쪽 세로 목록: 이름 -> 레벨/직업 -> 체력(+체력바) -> 스탯 5줄 -> 레벨업 버튼/안내(일러스트가 넓어진 만큼 오른쪽으로 밀림)
            const float colMin = 0.51f, colMax = 0.98f;
            var nameText = CreateRegionText(characterViewContentRt, new Vector2(colMin, 0.8667f), new Vector2(colMax, 1f), TextAnchor.MiddleLeft, 30, 4f, 2f);
            nameText.fontStyle = FontStyle.Bold;
            var jobLevelText = CreateRegionText(characterViewContentRt, new Vector2(colMin, 0.7667f), new Vector2(colMax, 0.8667f), TextAnchor.MiddleLeft, 20, 4f, 2f);

            var hpLabelText = CreateRegionText(characterViewContentRt, new Vector2(colMin, 0.6333f), new Vector2(colMin + 0.07f, 0.7667f), TextAnchor.MiddleLeft, 18, 2f, 2f);
            var hpText = CreateRegionText(characterViewContentRt, new Vector2(colMin + 0.07f, 0.6333f), new Vector2(colMax, 0.7667f), TextAnchor.MiddleLeft, 32, 2f, 2f);
            hpText.fontStyle = FontStyle.Bold;
            var hpBarFill = CreateSimpleHpBar(characterViewContentRt, new Vector2(colMin, 0.5667f), new Vector2(colMax, 0.6167f));

            // 스탯 5줄(기본공격/공격코인/기본수비/수비코인/속도)을 세로 1열x5행 그리드로 균등하게 채움
            var statTexts = CreateStatGrid(characterViewContentRt, new Vector2(colMin, 0.2f), new Vector2(colMax, 0.5333f), 1, 5, 22, 4f, 2f);

            characterViewLevelUpInfoText = CreateRegionText(characterViewContentRt, new Vector2(colMin, 0.1f), new Vector2(colMax, 0.2f), TextAnchor.MiddleCenter, 18);
            characterViewLevelUpButton = CreateButton(characterViewContentRt, "CharacterViewLevelUpButton", new Vector2((colMin + colMax) / 2f, 0.0833f), Vector2.zero, 320, 60, "캐릭터 레벨업");

            return new CharacterViewDetail
            {
                illustrationBox = illustrationBoxRt,
                illustrationImage = illustrationImg,
                nameText = nameText,
                jobLevelText = jobLevelText,
                hpLabelText = hpLabelText,
                hpText = hpText,
                hpBarFill = hpBarFill,
                statTexts = statTexts,
            };
        }

        // 캐릭터 확인 화면 세로 상세정보를 유닛 데이터로 채움(무기 정보는 다루지 않음)
        private void SetCharacterViewDetail(CharacterViewDetail info, Unit u)
        {
            info.nameText.text = u.unitName;
            info.nameText.color = AllyInfoTextColor;
            info.jobLevelText.text = $"Lv.{u.stats.level} {JobName(u.weapon.type)}";
            info.jobLevelText.color = AllyInfoTextColor;

            info.hpLabelText.text = "HP";
            info.hpLabelText.color = AllyInfoTextColor;
            info.hpText.text = $"{u.currentHP}/{u.stats.maxHP}";
            info.hpText.color = AllyInfoTextColor;
            float maxHP = Mathf.Max(1, u.stats.maxHP);
            info.hpBarFill.anchorMax = new Vector2(Mathf.Clamp01(u.currentHP / maxHP), 1f);

            info.statTexts[0].text = $"기본공격 {u.stats.atk}";
            info.statTexts[1].text = $"공격코인 {CoinFaceSingle(u.stats.atkCoins)}";
            info.statTexts[2].text = $"기본수비 {u.stats.def}";
            info.statTexts[3].text = $"수비코인 {CoinFaceSummary(u.stats.defCoins)}";
            info.statTexts[4].text = $"속도 {u.stats.spd}";
            foreach (var t in info.statTexts) t.color = AllyInfoTextColor;

            bool hasIllustration = u.illustrationSprite != null;
            info.illustrationImage.gameObject.SetActive(hasIllustration);
            info.illustrationImage.sprite = u.illustrationSprite;
            if (hasIllustration)
            {
                // 박스 안에 비율을 유지한 채 꽉 차게(가로/세로 중 더 좁게 걸리는 쪽에 맞춤) 그려서 잘리지 않고 전신이 다 보이게 함.
                // 박스 크기를 매번 rect에서 다시 읽어오므로, 버튼 줄이 사라져 정보칸이 넓어진 상태에서도(Expanded) 그만큼 커진 박스에 맞게 그려짐.
                // CharacterIllustrationScale만큼 더 키워서 보여줌(박스보다 커져 위/옆으로 넘칠 수 있음 - 캐릭터가 크게 두드러져 보이도록 의도한 것)
                var tex = u.illustrationSprite.texture;
                float aspect = (float)tex.width / tex.height;
                Rect box = info.illustrationBox.rect;
                float w = box.width;
                float h = w / aspect;
                if (h > box.height)
                {
                    h = box.height;
                    w = h * aspect;
                }
                info.illustrationImage.rectTransform.sizeDelta = new Vector2(w, h) * CharacterIllustrationScale;

                // 캐릭터마다 그림 안에서 얼굴 위치가 조금씩 달라서, 공통 위치에서 필요한 만큼만 개별 보정
                Vector2 offset = CharacterIllustrationBaseOffset;
                if (u.unitName == "알리어") offset += new Vector2(40f, 0f); // 알리어만 살짝 오른쪽으로
                info.illustrationImage.rectTransform.anchoredPosition = offset;
            }
        }

        // 본문은 왼쪽부터: 무기 - 체력(가운데, 크게) - 스탯 두 줄. 이름/레벨/직업은 본문이 아니라 왼쪽 위 명찰(NamePlate)에 표시
        private UnitInfoTexts CreateUnitInfoBlock(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            var containerGo = new GameObject("UnitInfo");
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

            var info = new UnitInfoTexts
            {
                container = rt,
                background = bgImg,
                // 이름/직업/레벨이 명찰로 빠지며 비워진 왼쪽 0.24만큼 무기 그림/무기 정보/체력 칸을 전부 왼쪽으로 옮김
                // 무기 그림 크기: 44 -> 50으로 아주 조금만 키움 (더 키우면 옆 칸과 부딪힘)
                weaponIcon = CreateWeaponIconPlaceholder(rt, new Vector2(0.02f, 0.5f), 50f),
                // 착용 중인 무기 이름: 기존 2x2 무기 격자의 왼쪽 위 칸 자리를 그대로 씀(나머지 3칸은 삭제)
                weaponNameText = CreateRegionText(rt, new Vector2(0.05f, 0.5f), new Vector2(0.165f, 1f), TextAnchor.MiddleLeft, 20, 3f, 3f),
                // 무기 이름 칸이 좁아지며 생긴 빈 공간(0.115)만큼 체력 칸을 왼쪽으로 옮김
                hpLabelText = CreateRegionText(rt, new Vector2(0.165f, 0.28f), new Vector2(0.20f, 1f), TextAnchor.MiddleLeft, 18, 2f, 2f),
                hpText = CreateRegionText(rt, new Vector2(0.20f, 0.28f), new Vector2(0.345f, 1f), TextAnchor.MiddleLeft, 38, 2f, 2f),
                // 체력 칸(hpText, ~0.345까지)부터 코인 원 칸 앞까지 3열x2행으로 채움.
                // 속도(0번 칸) 위치를 기준으로 두고, 그 뒤 칸들은 폭은 그대로 유지한 채 칸 사이 간격만 조금씩(Gap) 넓혀서 오른쪽으로 밀어냄
                statTexts = new[]
                {
                    CreateRegionText(rt, new Vector2(0.365f, 0.5f), new Vector2(0.4633f, 1f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 0: 속도
                    CreateRegionText(rt, new Vector2(0.4753f, 0.5f), new Vector2(0.5937f, 1f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 1: 기본공격
                    CreateRegionText(rt, new Vector2(0.6057f, 0.5f), new Vector2(0.844f, 1f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 2: 공격코인
                    CreateRegionText(rt, new Vector2(0.365f, 0f), new Vector2(0.4633f, 0.5f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 3: (빈 칸)
                    CreateRegionText(rt, new Vector2(0.4753f, 0f), new Vector2(0.5937f, 0.5f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 4: 기본수비
                    CreateRegionText(rt, new Vector2(0.6057f, 0f), new Vector2(0.844f, 0.5f), TextAnchor.MiddleLeft, 25, 1f, 1f), // 5: 수비코인
                },
                // 코인 개수: 숫자/라벨 없이 색깔 원으로만 표시(공격=노란색/수비=파란색)
                atkCoinDots = CreateCoinDots(rt, new Vector2(0.79f, 0.5f), new Vector2(1f, 1f), MaxCoinDots, CoinDotAtkColor),
                defCoinDots = CreateCoinDots(rt, new Vector2(0.79f, 0f), new Vector2(1f, 0.5f), MaxCoinDots, CoinDotDefColor),
            };
            info.hpBarFill = CreateSimpleHpBar(rt, new Vector2(0.165f, 0.08f), new Vector2(0.345f, 0.20f));
            info.hpText.fontStyle = FontStyle.Bold;
            info.hpLabelText.text = "HP";
            // 착용 무기 이름 칸 바로 아래에 나머지 무기를 작은 그림으로 옆으로 나열(최대 WeaponSlotCount-1개)
            info.otherWeaponIcons = CreateSmallWeaponIcons(rt, new Vector2(0.05f, 0f), new Vector2(0.165f, 0.5f), Unit.WeaponSlotCount - 1, 16f);

            info.weaponNameText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            foreach (var t in info.statTexts) t.color = textColor;

            // 상반신 일러스트: 정보칸(parent, 세로 1칸/가로 14칸) 왼쪽 위로 가로 4칸 x 세로 4칸 크기의 창을 내고,
            // RectMask2D로 창 밖을 잘라낸 뒤 그 안의 그림을 확대해서 위쪽(상반신)만 보이게 함(전신 대신).
            // 가로 폭이 4칸을 넘지 않도록 제한(캐릭터가 왼쪽 칸을 벗어나 옆 요소와 겹치지 않게)
            var illustrationMaskGo = new GameObject("IllustrationMask");
            illustrationMaskGo.transform.SetParent(parent, false);
            var illustrationMaskRt = illustrationMaskGo.AddComponent<RectTransform>();
            illustrationMaskRt.anchorMin = new Vector2(0f, 1f);
            illustrationMaskRt.anchorMax = new Vector2(4f / 14f, 5f);
            illustrationMaskRt.offsetMin = Vector2.zero; illustrationMaskRt.offsetMax = Vector2.zero;
            illustrationMaskGo.AddComponent<RectMask2D>();

            // 그림 크기는 실제 스프라이트 비율에 맞춰 SetUnitInfo에서 다시 계산함(캐릭터마다 원본 그림 비율이 다를 수 있어서).
            // 위쪽 가운데를 기준점으로 잡아 창 위쪽에 딱 붙이고, 아래로/양옆으로 넘치는 부분만 마스크가 잘라냄
            var illustrationGo = new GameObject("Illustration");
            illustrationGo.transform.SetParent(illustrationMaskRt, false);
            var illustrationRt = illustrationGo.AddComponent<RectTransform>();
            illustrationRt.anchorMin = new Vector2(0.5f, 1f); illustrationRt.anchorMax = new Vector2(0.5f, 1f); illustrationRt.pivot = new Vector2(0.5f, 1f);
            illustrationRt.anchoredPosition = IllustrationOffset;
            info.illustrationImage = illustrationGo.AddComponent<Image>();
            info.illustrationImage.raycastTarget = false;

            // 이름/레벨/직업 명찰: 일러스트보다 나중에(더 위쪽 형제로) 만들어서 겹치면 명찰이 그림을 덮도록 함.
            // 정보칸 왼쪽 위, 가로 4칸 x 세로 0.5칸, 세로 1.0(정보칸 윗변)~1.5, 가로 0~4/14
            var nameplateGo = new GameObject("NamePlate");
            nameplateGo.transform.SetParent(parent, false);
            var nameplateRt = nameplateGo.AddComponent<RectTransform>();
            nameplateRt.anchorMin = new Vector2(0f, 1f);
            nameplateRt.anchorMax = new Vector2(4f / 14f, 1.5f);
            nameplateRt.offsetMin = Vector2.zero; nameplateRt.offsetMax = Vector2.zero;

            var nameplateBgGo = new GameObject("Background");
            nameplateBgGo.transform.SetParent(nameplateRt, false);
            var nameplateBgRt = nameplateBgGo.AddComponent<RectTransform>();
            nameplateBgRt.anchorMin = Vector2.zero; nameplateBgRt.anchorMax = Vector2.one;
            nameplateBgRt.offsetMin = Vector2.zero; nameplateBgRt.offsetMax = Vector2.zero;
            var nameplateBgImg = nameplateBgGo.AddComponent<Image>();
            nameplateBgImg.color = bgColor;
            nameplateBgImg.raycastTarget = false;

            info.nameplateBackground = nameplateBgImg;
            info.nameplateNameText = CreateRegionText(nameplateRt, new Vector2(0f, 0f), new Vector2(0.6f, 1f), TextAnchor.MiddleLeft, 26, 10f, 2f);
            info.nameplateJobLevelText = CreateRegionText(nameplateRt, new Vector2(0.6f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleLeft, 18, 4f, 2f);
            info.nameplateNameText.fontStyle = FontStyle.Bold;
            info.nameplateNameText.color = textColor;
            info.nameplateJobLevelText.color = textColor;

            return info;
        }

        // 캐릭터 정보창의 체력 숫자 아래에 넣는 단순한 체력바(검정 배경 + 초록 채움). 전투창 체력바와 달리 예측/깜박임 없이 현재 체력만 보여줌
        private RectTransform CreateSimpleHpBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
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

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barRt, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.3f);
            fillImg.raycastTarget = false;

            return fillRt;
        }

        // 영역을 동일한 간격의 cols x rows 칸으로 나눠 칸마다 텍스트 하나씩 배치
        private Text[] CreateStatGrid(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int cols, int rows, int fontSize, float paddingX = 14f, float paddingY = 14f)
        {
            var texts = new Text[cols * rows];
            float cellW = (anchorMax.x - anchorMin.x) / cols;
            float cellH = (anchorMax.y - anchorMin.y) / rows;
            for (int i = 0; i < texts.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float xMin = anchorMin.x + cellW * col;
                float yMax = anchorMax.y - cellH * row;
                texts[i] = CreateRegionText(parent, new Vector2(xMin, yMax - cellH), new Vector2(xMin + cellW, yMax), TextAnchor.MiddleLeft, fontSize, paddingX, paddingY);
            }
            return texts;
        }

        // 무기가 없을 땐 빈 자리만 흐리게 표시하고, 있으면 SetWeaponIcon이 무기 타입에 맞는 그림으로 채움
        // anchorCenter 위치에 고정 픽셀 크기(size)의 정사각형을 배치함 (비율 사각형을 쓰면 칸마다 가로세로 비가 달라 그림이 눌려 보임)
        private Image CreateWeaponIconPlaceholder(Transform parent, Vector2 anchorCenter, float size)
        {
            var go = new GameObject("WeaponIcon");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorCenter; rt.anchorMax = anchorCenter; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
            img.sprite = null;
            return img;
        }

        // anchorMin~anchorMax 구간에 작은 정사각형 아이콘 maxCount개를 가로로 나란히 만들어 둠(착용하지 않은 나머지 무기 표시용).
        // 실제 보유 무기 개수만큼만 SetActive(true)로 켜서 씀(SetUnitInfo에서 갱신)
        private Image[] CreateSmallWeaponIcons(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, int maxCount, float iconSize)
        {
            var icons = new Image[maxCount];
            float cellW = (anchorMax.x - anchorMin.x) / maxCount;
            float centerY = (anchorMin.y + anchorMax.y) / 2f;
            for (int i = 0; i < maxCount; i++)
            {
                var go = new GameObject($"OtherWeaponIcon{i}");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                float centerX = anchorMin.x + cellW * (i + 0.5f);
                rt.anchorMin = new Vector2(centerX, centerY); rt.anchorMax = rt.anchorMin; rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(iconSize, iconSize);

                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                icons[i] = img;
            }
            return icons;
        }

        private static void SetWeaponIcon(Image img, WeaponData weapon)
        {
            if (weapon == null)
            {
                img.sprite = null;
                img.color = new Color(1f, 1f, 1f, 0.12f);
                return;
            }

            img.sprite = VisualFactory.WeaponIconSprite(weapon);
            img.color = Color.white;
        }

        private void SetUnitInfo(UnitInfoTexts info, Unit u)
        {
            info.container.gameObject.SetActive(true);

            // 아군 선택/공격뿐 아니라 적 정찰(클릭 확인)에도 같은 블록이 재사용되므로, 팀에 맞는 색으로 매번 갱신
            bool isAlly = u.team == Team.Player;
            Color bgColor = isAlly ? AllyInfoBgColor : EnemyInfoBgColor;
            Color textColor = isAlly ? AllyInfoTextColor : EnemyInfoTextColor;
            info.background.color = bgColor;
            info.weaponNameText.color = textColor;
            info.hpLabelText.color = textColor;
            info.hpText.color = textColor;
            foreach (var t in info.statTexts) t.color = textColor;

            info.nameplateBackground.color = bgColor;
            info.nameplateNameText.color = textColor;
            info.nameplateJobLevelText.color = textColor;
            info.nameplateNameText.text = u.unitName;
            info.nameplateJobLevelText.text = $"Lv.{u.stats.level} {JobName(u.weapon.type)}";
            SetWeaponIcon(info.weaponIcon, u.weapon);

            // 일러스트가 있는 캐릭터만 보여주고, 없으면 숨김(빈 사각형이 보이는 것을 방지)
            bool hasIllustration = u.illustrationSprite != null;
            info.illustrationImage.gameObject.SetActive(hasIllustration);
            info.illustrationImage.sprite = u.illustrationSprite;
            if (hasIllustration)
            {
                // 마스크 창(세로 4칸) 높이의 IllustrationZoom배 크기로 그려서, 그중 위쪽 1/Zoom만 창에 보이게(=상반신만) 함.
                // 원본 그림 비율(가로/세로)은 캐릭터마다 다를 수 있어 실제 텍스처 크기로 매번 다시 계산
                // 캐릭터별로 위치/배율을 다르게 두고 싶으면 Unit.illustrationOffset/illustrationZoom을 채움(없으면 공통값)
                var tex = u.illustrationSprite.texture;
                float nativeAspect = (float)tex.width / tex.height;
                float zoom = u.illustrationZoom ?? IllustrationZoom;
                float renderedHeight = 4f * TilePixels * zoom;
                float renderedWidth = renderedHeight * nativeAspect;
                info.illustrationImage.rectTransform.sizeDelta = new Vector2(renderedWidth, renderedHeight);
                info.illustrationImage.rectTransform.anchoredPosition = u.illustrationOffset ?? IllustrationOffset;
            }

            info.weaponNameText.text = u.weapon.weaponName;

            // 착용 중인 무기와 빈 슬롯을 뺀 나머지 무기만 작은 그림으로 옆으로 나열
            int otherIndex = 0;
            foreach (var w in u.weaponSlots)
            {
                if (w == null || w == u.weapon) continue;
                if (otherIndex >= info.otherWeaponIcons.Length) break;
                SetWeaponIcon(info.otherWeaponIcons[otherIndex], w);
                info.otherWeaponIcons[otherIndex].gameObject.SetActive(true);
                otherIndex++;
            }
            for (; otherIndex < info.otherWeaponIcons.Length; otherIndex++)
                info.otherWeaponIcons[otherIndex].gameObject.SetActive(false);

            info.hpText.text = $"{u.currentHP}/{u.stats.maxHP}";
            float maxHP = Mathf.Max(1, u.stats.maxHP);
            info.hpBarFill.anchorMax = new Vector2(Mathf.Clamp01(u.currentHP / maxHP), 1f);

            // 방어는 "앞면/뒷면" 합산값을 그대로 보여줌(코인이 여러 개면 각각 합산).
            // 공격은 코인을 여러 개 들어도 실제 전투 계산과 무관하게 화면엔 코인 1개(첫 번째)의 값만 보여줌(어차피 같은 값의 코인만 들도록 하기 때문).
            // 그리드(3열x2행): 속도를 맨 앞(0번, row0/col0)에 두고 그 바로 아래 칸(3번, row1/col0)은 짝이 없어 항상 비워 둠
            info.statTexts[0].text = $"속도 {u.stats.spd}";
            info.statTexts[1].text = $"기본공격 {u.stats.atk}";
            info.statTexts[2].text = $"공격코인 {CoinFaceSingle(u.stats.atkCoins)}";
            info.statTexts[3].text = string.Empty;
            info.statTexts[4].text = $"기본수비 {u.stats.def}";
            info.statTexts[5].text = $"수비코인 {CoinFaceSummary(u.stats.defCoins)}";

            // 코인 개수는 숫자 대신 전투창과 같은 색깔 원(atkCoinDots/defCoinDots)으로 표시. 보유한 코인 개수만큼만 켬
            for (int i = 0; i < info.atkCoinDots.Length; i++)
                info.atkCoinDots[i].gameObject.SetActive(i < u.stats.atkCoins.Count);
            for (int i = 0; i < info.defCoinDots.Length; i++)
                info.defCoinDots[i].gameObject.SetActive(i < u.stats.defCoins.Count);
        }

        // 코인 리스트의 앞면 합/뒷면 합을 "앞/뒤" 형태 문자열로 반환(예: 앞면 12+12, 뒷면 0+0인 코인 2개 -> "24/0")
        private static string CoinFaceSummary(List<Coin> coins)
        {
            int heads = 0, tails = 0;
            foreach (var c in coins) { heads += c.heads; tails += c.tails; }
            return $"{heads}/{tails}";
        }

        // 코인 1개(첫 번째)의 앞면/뒷면 값만 "앞/뒤" 형태 문자열로 반환(코인이 없으면 "0/0")
        private static string CoinFaceSingle(List<Coin> coins)
        {
            if (coins.Count == 0) return "0/0";
            return $"{coins[0].heads}/{coins[0].tails}";
        }

        private static string JobName(WeaponType t) => t switch
        {
            WeaponType.Sword => "검사",
            WeaponType.Lance => "창병",
            WeaponType.Axe => "도끼병",
            WeaponType.Bow => "궁수",
            WeaponType.Tome => "마도사",
            WeaponType.Dagger => "도적",
            _ => "전사",
        };
    }
}
