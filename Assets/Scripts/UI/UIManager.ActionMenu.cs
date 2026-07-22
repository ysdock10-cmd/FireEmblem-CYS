using System;
using UnityEngine;
using UnityEngine.UI;

namespace SRPG
{
    // 액션 메뉴(공격/대기/취소)와 무기 선택창
    public partial class UIManager
    {
        public void ShowActionMenu(bool canAttack, Action onAttack, Action onWait, Action onCancel)
        {
            attackButton.onClick.RemoveAllListeners();
            waitButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            attackButton.interactable = canAttack;
            waitButton.interactable = true; // 이전에 RestrictActionMenuToAttack으로 잠겼을 수 있으니 매번 새로 열 때 원래대로 되돌림
            cancelButton.interactable = true;
            attackButton.onClick.AddListener(() => onAttack());
            waitButton.onClick.AddListener(() => onWait());
            cancelButton.onClick.AddListener(() => onCancel());

            actionMenuPanel.SetActive(true);
        }

        public void HideActionMenu() => actionMenuPanel.SetActive(false);

        // 튜토리얼이 "지금은 공격만 눌러보세요"라고 안내할 때, 대기/취소를 눌러도 반응하지 않게 잠금.
        // 다음에 ShowActionMenu가 다시 열릴 때 자동으로 풀림
        public void RestrictActionMenuToAttack()
        {
            waitButton.interactable = false;
            cancelButton.interactable = false;
        }

        // 무기를 처음 누르면 onPreview(사거리만 표시), 이미 미리보기 중인 무기를 한 번 더 누르면 onConfirm으로 확정
        private int previewedWeaponSlot = -1;

        // usable[i]가 false면(그 무기 사거리에 닿는 적이 없으면) 칸을 흐리게 표시하고 고를 수 없게 함
        public void ShowWeaponSelect(WeaponData[] slots, bool[] usable, Action<WeaponData> onPreview, Action onConfirm, Action onCancel)
        {
            previewedWeaponSlot = -1;

            for (int i = 0; i < weaponSlotButtons.Length; i++)
            {
                var w = i < slots.Length ? slots[i] : null;
                bool canUse = w != null && i < usable.Length && usable[i];
                var btn = weaponSlotButtons[i];
                btn.onClick.RemoveAllListeners();
                SetWeaponSlotSelected(i, false);

                if (w != null)
                {
                    weaponSlotLabels[i].text = canUse
                        ? $"{w.weaponName}   위력 {w.might}  사거리 {w.minRange}-{w.maxRange}"
                        : $"{w.weaponName}   위력 {w.might}  사거리 {w.minRange}-{w.maxRange}  (사거리 밖)";
                    weaponSlotLabels[i].color = canUse ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                    weaponSlotIcons[i].sprite = VisualFactory.WeaponIconSprite(w);
                    weaponSlotIcons[i].color = canUse ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    btn.interactable = canUse;

                    if (canUse)
                    {
                        int index = i;
                        btn.onClick.AddListener(() =>
                        {
                            if (previewedWeaponSlot == index)
                            {
                                onConfirm();
                                return;
                            }

                            previewedWeaponSlot = index;
                            for (int j = 0; j < weaponSlotButtons.Length; j++) SetWeaponSlotSelected(j, j == index);
                            onPreview(w);
                        });
                    }
                }
                else
                {
                    weaponSlotLabels[i].text = "-";
                    weaponSlotIcons[i].sprite = null;
                    weaponSlotIcons[i].color = new Color(1f, 1f, 1f, 0f);
                    btn.interactable = false;
                }
            }

            weaponSelectCancelButton.onClick.RemoveAllListeners();
            weaponSelectCancelButton.onClick.AddListener(() => onCancel());
            weaponSelectCancelButton.interactable = true; // 이전에 RestrictWeaponSelectToSlot으로 잠겼을 수 있으니 매번 새로 열 때 원래대로 되돌림

            weaponSelectPanel.SetActive(true);
        }

        public void HideWeaponSelect()
        {
            weaponSelectPanel.SetActive(false);
            previewedWeaponSlot = -1;
        }

        private void SetWeaponSlotSelected(int index, bool selected)
        {
            weaponSlotButtons[index].GetComponent<Image>().color = selected ? WeaponSlotSelectedColor : WeaponSlotNormalColor;
        }

        // 튜토리얼이 특정 무기 슬롯만 고르게 할 때 씀(다른 슬롯/취소는 눌러도 반응하지 않음). 다음에 ShowWeaponSelect가 열리면 자동으로 풀림
        public void RestrictWeaponSelectToSlot(int index)
        {
            for (int i = 0; i < weaponSlotButtons.Length; i++)
                if (i != index) weaponSlotButtons[i].interactable = false;
            weaponSelectCancelButton.interactable = false;
        }
    }
}
