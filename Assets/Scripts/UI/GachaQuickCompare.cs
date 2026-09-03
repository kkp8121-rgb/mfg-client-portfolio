using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 가챠 연출 종료 직후 즉시 비교 + 원터치 장착 오버레이.
    /// GachaCompareSystem이 추천 아이템 목록을 전달하면 표시된다.
    /// [원터치 장착] 버튼으로 EquipmentManager/WeaponManager를 호출하여 즉시 장착.
    /// </summary>
    public class GachaQuickCompare : MonoBehaviour
    {
        [Header("오버레이")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panelRoot;

        [Header("추천 아이템 행")]
        [SerializeField] private GachaCompareRow[] _rows;

        [Header("CP 요약")]
        [SerializeField] private TMP_Text _cpBeforeText;
        [SerializeField] private TMP_Text _cpAfterText;
        [SerializeField] private TMP_Text _cpDeltaText;

        [Header("버튼")]
        [SerializeField] private Button _equipAllButton;
        [SerializeField] private TMP_Text _equipAllButtonText;
        [SerializeField] private Button _laterButton;

        [Header("색상")]
        [SerializeField] private Color _increaseColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _decreaseColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("설정")]
        [SerializeField] private float _autoCloseDelay = 8f;

        private Sequence _showSeq;
        private Tween _autoCloseTween;
        private List<GachaCompareSystem.GachaUpgradeInfo> _currentUpgrades;
        private long _currentCp;

        private void Start()
        {
            if (_equipAllButton != null)
                _equipAllButton.onClick.AddListener(OnEquipAllClicked);
            if (_laterButton != null)
                _laterButton.onClick.AddListener(OnLaterClicked);

            HideImmediate();
        }

        /// <summary>
        /// 추천 아이템 목록을 받아 비교 오버레이를 표시한다.
        /// </summary>
        public void Show(List<GachaCompareSystem.GachaUpgradeInfo> upgrades, long currentCp)
        {
            if (upgrades == null || upgrades.Count == 0) return;

            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            _currentUpgrades = upgrades;
            _currentCp = currentCp;

            // CP 요약
            long totalDelta = 0;
            for (int i = 0; i < upgrades.Count; i++)
                totalDelta += upgrades[i].CpDelta;

            if (_cpBeforeText != null)
                _cpBeforeText.text = $"{currentCp:N0}";
            if (_cpAfterText != null)
                _cpAfterText.text = $"{currentCp + totalDelta:N0}";
            if (_cpDeltaText != null)
            {
                _cpDeltaText.text = $"+{totalDelta:N0}";
                _cpDeltaText.color = _increaseColor;
            }

            // 행 표시
            if (_rows != null)
            {
                for (int i = 0; i < _rows.Length; i++)
                {
                    if (i < upgrades.Count)
                    {
                        var info = upgrades[i];
                        _rows[i].root.SetActive(true);

                        if (_rows[i].itemNameText != null)
                        {
                            // 2026-04-23 UX 리포트: 영문 ID 노출 수정 (예: "weapon_flame_blade" → "화염검").
                            // PoolName으로 장비/무기 구분 후 각 Data의 displayName 조회, 실패 시 ItemId fallback.
                            _rows[i].itemNameText.text = ResolveItemDisplayName(info.ItemId, info.PoolName);
                        }

                        if (_rows[i].gradeText != null)
                        {
                            _rows[i].gradeText.text = GetGradeLabel(info.Grade);
                            _rows[i].gradeText.color = GetGradeColor(info.Grade);
                        }

                        if (_rows[i].cpDeltaText != null)
                        {
                            _rows[i].cpDeltaText.text = $"전투력 +{info.CpDelta:N0}";
                            _rows[i].cpDeltaText.color = _increaseColor;
                        }

                        if (_rows[i].poolText != null)
                            _rows[i].poolText.text = GetPoolLabel(info.PoolName);
                    }
                    else
                    {
                        _rows[i].root.SetActive(false);
                    }
                }
            }

            // 버튼 텍스트
            if (_equipAllButtonText != null)
            {
                _equipAllButtonText.text = upgrades.Count == 1 ? "장착하기" : $"추천 {upgrades.Count}건 장착";
            }

            // 연출
            _panelRoot.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _panelRoot.localScale = Vector3.one * 0.85f;

            _showSeq = DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.2f))
                .Join(_panelRoot.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .OnComplete(() =>
                {
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                })
                .SetUpdate(true)
                .SetLink(gameObject);

            // 행 순차 등장
            if (_rows != null)
            {
                for (int i = 0; i < _rows.Length && i < upgrades.Count; i++)
                {
                    var row = _rows[i];
                    if (row.root == null || !row.root.activeSelf) continue;

                    var cg = row.root.GetComponent<CanvasGroup>();
                    if (cg == null) cg = row.root.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;

                    float delay = 0.15f + i * 0.1f;
                    DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.2f)
                        .SetDelay(delay)
                        .SetUpdate(true)
                        .SetLink(gameObject);
                }
            }

            // CP 델타 펀치
            if (_cpDeltaText != null)
            {
                _cpDeltaText.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 6, 0.5f)
                    .SetDelay(0.4f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_compare");

            _autoCloseTween = DOVirtual.DelayedCall(_autoCloseDelay, () => HidePopup(), true)
                .SetLink(gameObject);
        }

        private void OnEquipAllClicked()
        {
            if (_currentUpgrades == null) return;

            for (int i = 0; i < _currentUpgrades.Count; i++)
            {
                var info = _currentUpgrades[i];
                TryEquip(info);
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_equip");
            HidePopup();
        }

        private void OnLaterClicked()
        {
            HidePopup();
        }

        private static void TryEquip(GachaCompareSystem.GachaUpgradeInfo info)
        {
            switch (info.PoolName)
            {
                case "Equipment":
                    TryEquipEquipment(info.ItemId, info.Grade);
                    break;
                case "Weapon":
                    TryEquipWeapon(info.ItemId, info.Grade);
                    break;
            }
        }

        private static void TryEquipEquipment(string equipmentId, string grade)
        {
            var manager = MkLike.Equipment.EquipmentManager.Instance;
            if (manager == null) return;

            // 인벤토리에서 해당 아이템 찾기
            var inventory = manager.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].equipmentId == equipmentId && inventory[i].grade == grade)
                {
                    if (!manager.IsEquipped(inventory[i].instanceId))
                    {
                        manager.Equip(inventory[i].instanceId);
                        return;
                    }
                }
            }
        }

        private static void TryEquipWeapon(string weaponId, string grade)
        {
            var manager = MkLike.Equipment.WeaponManager.Instance;
            if (manager == null) return;

            var inventory = manager.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].weaponId == weaponId && inventory[i].grade == grade)
                {
                    if (manager.EquippedWeapon == null ||
                        manager.EquippedWeapon.instanceId != inventory[i].instanceId)
                    {
                        manager.Equip(inventory[i].instanceId);
                        return;
                    }
                }
            }
        }

        private void HidePopup()
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 0f, 0.15f))
                .Join(_panelRoot.DOScale(0.9f, 0.12f).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    _panelRoot.gameObject.SetActive(false);
                    GachaCompareSystem.Instance?.OnCompareFinished();
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_panelRoot != null)
                _panelRoot.gameObject.SetActive(false);
        }

        private static Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "Normal" => Color.white,
                "Rare" => new Color(0.3f, 0.5f, 1f),
                "Epic" => new Color(0.7f, 0.3f, 1f),
                "Unique" => new Color(1f, 0.4f, 0.4f),
                "Legendary" => new Color(1f, 0.8f, 0f),
                "Mythic" => new Color(1f, 0.2f, 0.2f),
                _ => Color.white
            };
        }

        private static string GetGradeLabel(string grade)
        {
            return grade switch
            {
                "Normal" => "일반",
                "Rare" => "희귀",
                "Epic" => "에픽",
                "Unique" => "유니크",
                "Legendary" => "전설",
                "Mythic" => "신화",
                _ => grade
            };
        }

        private static string GetPoolLabel(string poolName)
        {
            return poolName switch
            {
                "Equipment" => "장비",
                "Weapon" => "무기",
                _ => poolName
            };
        }

        /// <summary>
        /// 2026-04-23: ItemId → 한글 displayName 변환. Pool에 따라 WeaponDataSO/EquipmentDataSO 조회.
        /// Data 조회 실패 시 itemId를 그대로 반환 (디버깅용 fallback).
        /// </summary>
        private static string ResolveItemDisplayName(string itemId, string poolName)
        {
            if (string.IsNullOrEmpty(itemId)) return "";
            try
            {
                if (poolName == "Weapon")
                {
                    var data = MkLike.Equipment.WeaponManager.Instance?.GetData(itemId);
                    if (data != null && !string.IsNullOrEmpty(data.displayName)) return data.displayName;
                }
                else if (poolName == "Equipment")
                {
                    // EquipmentManager.GetData는 EquipmentDataSO 반환 — line 133 시그니처 확인됨
                    var eqData = MkLike.Equipment.EquipmentManager.Instance?.GetData(itemId);
                    if (eqData != null && !string.IsNullOrEmpty(eqData.displayName)) return eqData.displayName;
                }
            }
            catch { /* Data 조회 예외는 silent fallback */ }
            return itemId; // 최종 fallback
        }

        private void OnDestroy()
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();
            if (_panelRoot != null) _panelRoot.DOKill();
            if (_cpDeltaText != null) _cpDeltaText.transform.DOKill();
        }
    }

    /// <summary>가챠 비교 행 UI 구조체.</summary>
    [System.Serializable]
    public struct GachaCompareRow
    {
        public GameObject root;
        public TMP_Text itemNameText;
        public TMP_Text gradeText;
        public TMP_Text cpDeltaText;
        public TMP_Text poolText;
    }
}
