using System.Collections.Generic;
using DG.Tweening;
using MkLike.Core;
using MkLike.Equipment;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 장비/무기 장착 시 전후 비교 팝업.
    /// 각 스탯의 증감을 빨간/초록 화살표로 표시한다.
    /// EquipmentChangedEvent, WeaponChangedEvent를 구독.
    /// </summary>
    public class EquipComparePopup : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _popupRoot;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private StatCompareRow[] _statRows;
        [SerializeField] private TMP_Text _cpBeforeText;
        [SerializeField] private TMP_Text _cpAfterText;
        [SerializeField] private TMP_Text _cpDeltaText;
        [SerializeField] private Button _closeButton;

        [Header("색상")]
        [SerializeField] private Color _increaseColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _decreaseColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _neutralColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("설정")]
        [SerializeField] private float _autoCloseDelay = 4f;

        private Sequence _showSeq;
        private Tween _autoCloseTween;

        // 장착 스탯 스냅샷: 장착 전후 비교를 위해 슬롯별 이전 스탯을 기억
        private readonly Dictionary<EquipmentSlot, EquipStatSnapshot> _equipSnapshot = new();
        private WeaponStatSnapshot _weaponSnapshot;

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);

            HideImmediate();
            InitializeSnapshots();
        }

        private void OnEnable()
        {
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Subscribe(OnWeaponChanged);
        }

        private void OnDisable()
        {
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<WeaponChangedEvent>.Unsubscribe(OnWeaponChanged);
        }

        private void InitializeSnapshots()
        {
            if (EquipmentManager.Instance == null) return;

            // 현재 장착된 장비의 스탯을 스냅샷으로 저장
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var equipped = EquipmentManager.Instance.GetEquipped(slot);
                if (equipped != null)
                {
                    var data = EquipmentManager.Instance.GetData(equipped.equipmentId);
                    if (data != null)
                    {
                        _equipSnapshot[slot] = new EquipStatSnapshot
                        {
                            Atk = data.GetAtk(equipped.grade),
                            Hp = data.GetHp(equipped.grade),
                            Def = data.GetDef(equipped.grade),
                            CritRate = data.GetCritRate(equipped.grade)
                        };
                    }
                }
            }

            // 무기 스냅샷
            if (WeaponManager.Instance != null)
            {
                var weapon = WeaponManager.Instance.EquippedWeapon;
                if (weapon != null)
                {
                    var wData = WeaponManager.Instance.GetData(weapon.weaponId);
                    if (wData != null)
                    {
                        _weaponSnapshot = new WeaponStatSnapshot
                        {
                            Atk = wData.GetFinalAtk(weapon.grade, weapon.level, weapon.awakeningStars),
                            CritRate = wData.GetCritRate(weapon.grade) + wData.equipCritRate,
                            AtkSpeed = wData.GetAtkSpeed(weapon.grade)
                        };
                    }
                }
            }
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (!evt.IsEquipped) return;
            if (EquipmentManager.Instance == null) return;

            var data = EquipmentManager.Instance.GetData(evt.EquipmentId);
            if (data == null) return;

            // 새 아이템 스탯
            int newAtk = data.GetAtk(evt.Grade);
            int newHp = data.GetHp(evt.Grade);
            int newDef = data.GetDef(evt.Grade);
            float newCrit = data.GetCritRate(evt.Grade);

            // 이전 스탯 (스냅샷)
            _equipSnapshot.TryGetValue(evt.Slot, out var old);

            var deltas = new[]
            {
                new StatDelta { StatName = "공격력", Before = old.Atk, After = newAtk },
                new StatDelta { StatName = "체력", Before = old.Hp, After = newHp },
                new StatDelta { StatName = "방어력", Before = old.Def, After = newDef },
            };

            long cpBefore = CalculateEquipCp(old.Atk, old.Hp, old.Def, old.CritRate);
            long cpAfter = CalculateEquipCp(newAtk, newHp, newDef, newCrit);

            // 스냅샷 갱신
            _equipSnapshot[evt.Slot] = new EquipStatSnapshot
            {
                Atk = newAtk, Hp = newHp, Def = newDef, CritRate = newCrit
            };

            // CP 변화가 있을 때만 표시
            if (cpAfter != cpBefore)
                ShowComparison("장비 장착", deltas, cpBefore, cpAfter);
        }

        private void OnWeaponChanged(WeaponChangedEvent evt)
        {
            if (!evt.IsEquipped) return;
            if (WeaponManager.Instance == null) return;

            var wData = WeaponManager.Instance.GetData(evt.WeaponId);
            if (wData == null) return;

            // 새 무기: 방금 장착된 인벤토리 아이템에서 실제 레벨/각성 조회
            int newAtk = wData.GetFinalAtk(evt.Grade, 1, 0);
            float newCrit = wData.GetCritRate(evt.Grade) + wData.equipCritRate;
            float newAtkSpd = wData.GetAtkSpeed(evt.Grade);

            // 인벤토리에서 실제 인스턴스 찾아 정확한 스탯 계산
            var inventory = WeaponManager.Instance.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].instanceId == evt.InstanceId)
                {
                    newAtk = wData.GetFinalAtk(inventory[i].grade, inventory[i].level, inventory[i].awakeningStars);
                    break;
                }
            }

            var old = _weaponSnapshot;

            var deltas = new[]
            {
                new StatDelta { StatName = "공격력", Before = old.Atk, After = newAtk },
            };

            long cpBefore = CalculateWeaponCp(old.Atk, old.CritRate, old.AtkSpeed);
            long cpAfter = CalculateWeaponCp(newAtk, newCrit, newAtkSpd);

            _weaponSnapshot = new WeaponStatSnapshot
            {
                Atk = newAtk, CritRate = newCrit, AtkSpeed = newAtkSpd
            };

            if (cpAfter != cpBefore)
                ShowComparison("무기 장착", deltas, cpBefore, cpAfter);
        }

        private static long CalculateEquipCp(int atk, int hp, int def, float crit)
        {
            return (long)(atk * 3 + hp * 0.5f + def * 2 + crit * 500);
        }

        private static long CalculateWeaponCp(int atk, float crit, float atkSpd)
        {
            return (long)(atk * 3 + crit * 500 + atkSpd * 100);
        }

        private struct EquipStatSnapshot
        {
            public int Atk;
            public int Hp;
            public int Def;
            public float CritRate;
        }

        private struct WeaponStatSnapshot
        {
            public int Atk;
            public float CritRate;
            public float AtkSpeed;
        }

        /// <summary>
        /// 전후 비교 팝업을 표시한다.
        /// </summary>
        public void ShowComparison(string title, StatDelta[] deltas, long cpBefore, long cpAfter)
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            if (_titleText != null)
                _titleText.text = title;

            // 스탯 행 표시
            if (_statRows != null)
            {
                for (int i = 0; i < _statRows.Length; i++)
                {
                    if (i < deltas.Length)
                    {
                        _statRows[i].root.SetActive(true);
                        var delta = deltas[i];

                        if (_statRows[i].nameText != null)
                            _statRows[i].nameText.text = delta.StatName;

                        if (_statRows[i].beforeText != null)
                            _statRows[i].beforeText.text = delta.Before.ToString("N0");

                        if (_statRows[i].afterText != null)
                            _statRows[i].afterText.text = delta.After.ToString("N0");

                        int diff = delta.After - delta.Before;
                        if (_statRows[i].deltaText != null)
                        {
                            if (diff > 0)
                            {
                                _statRows[i].deltaText.text = $"+{diff:N0}";
                                _statRows[i].deltaText.color = _increaseColor;
                            }
                            else if (diff < 0)
                            {
                                _statRows[i].deltaText.text = $"{diff:N0}";
                                _statRows[i].deltaText.color = _decreaseColor;
                            }
                            else
                            {
                                _statRows[i].deltaText.text = "0";
                                _statRows[i].deltaText.color = _neutralColor;
                            }
                        }

                        if (_statRows[i].arrowImage != null)
                        {
                            _statRows[i].arrowImage.gameObject.SetActive(diff != 0);
                            _statRows[i].arrowImage.color = diff > 0 ? _increaseColor : _decreaseColor;
                            _statRows[i].arrowImage.rectTransform.localRotation =
                                Quaternion.Euler(0f, 0f, diff > 0 ? 0f : 180f);
                        }
                    }
                    else
                    {
                        _statRows[i].root.SetActive(false);
                    }
                }
            }

            // CP 비교
            long cpDelta = cpAfter - cpBefore;
            if (_cpBeforeText != null)
                _cpBeforeText.text = $"{cpBefore:N0}";
            if (_cpAfterText != null)
                _cpAfterText.text = $"{cpAfter:N0}";
            if (_cpDeltaText != null)
            {
                if (cpDelta > 0)
                {
                    _cpDeltaText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(_increaseColor)}>+{cpDelta:N0}</color>";
                }
                else if (cpDelta < 0)
                {
                    _cpDeltaText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(_decreaseColor)}>{cpDelta:N0}</color>";
                }
                else
                {
                    _cpDeltaText.text = "0";
                }
            }

            // 연출
            _popupRoot.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _popupRoot.localScale = Vector3.one * 0.85f;

            _showSeq = DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.2f))
                .Join(_popupRoot.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .OnComplete(() =>
                {
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                })
                .SetUpdate(true)
                .SetLink(gameObject);

            // 스탯 행 순차 등장
            if (_statRows != null)
            {
                for (int i = 0; i < _statRows.Length && i < deltas.Length; i++)
                {
                    var row = _statRows[i];
                    if (row.root == null || !row.root.activeSelf) continue;

                    var cg = row.root.GetComponent<CanvasGroup>();
                    if (cg == null) cg = row.root.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;

                    float delay = 0.1f + i * 0.08f;
                    DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.2f)
                        .SetDelay(delay)
                        .SetUpdate(true)
                        .SetLink(gameObject);

                    // 증감이 있는 행은 펀치
                    int diff = deltas[i].After - deltas[i].Before;
                    if (diff != 0 && row.deltaText != null)
                    {
                        row.deltaText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.5f)
                            .SetDelay(delay + 0.15f)
                            .SetUpdate(true)
                            .SetLink(gameObject);
                    }
                }
            }

            // CP 카운트업
            if (_cpAfterText != null && cpDelta != 0)
            {
                _cpAfterText.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.5f)
                    .SetDelay(0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_compare");

            _autoCloseTween = DOVirtual.DelayedCall(_autoCloseDelay, () => Hide(), true)
                .SetLink(gameObject);
        }

        private void Hide()
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 0f, 0.15f))
                .Join(_popupRoot.DOScale(0.9f, 0.12f).SetEase(Ease.InBack))
                .OnComplete(() => _popupRoot.gameObject.SetActive(false))
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
            if (_popupRoot != null)
                _popupRoot.gameObject.SetActive(false);
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 210;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_popupRoot == null)
            {
                // Dim overlay background
                var dimGo = CreateUIChild("DimOverlay");
                var dimRt = dimGo.GetComponent<RectTransform>();
                SetStretchAll(dimRt);
                var dimImg = dimGo.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.6f);
                dimImg.raycastTarget = true;

                // Popup panel
                var go = new GameObject("PopupRoot", typeof(RectTransform));
                go.transform.SetParent(dimGo.transform, false);
                _popupRoot = go.GetComponent<RectTransform>();
                _popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
                _popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _popupRoot.pivot = new Vector2(0.5f, 0.5f);
                _popupRoot.sizeDelta = new Vector2(400f, 420f);
                _popupRoot.anchoredPosition = Vector2.zero;
                var bg = go.AddComponent<Image>();
                bg.color = Color.white;
                var tm = UIThemeManager.Instance;
                if (tm != null) tm.ApplyPanelBackground(bg);
                else bg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);

                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 6f;
                vlg.padding = new RectOffset(16, 16, 12, 12);
            }

            Transform panelParent = _popupRoot != null ? _popupRoot : transform;

            if (_titleText == null)
            {
                var go = CreateLayoutChild(panelParent, "TitleText", 36f);
                _titleText = go.AddComponent<TextMeshProUGUI>();
                _titleText.fontSize = 22f;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = Color.white;
                _titleText.fontStyle = FontStyles.Bold;
            }

            // Separator
            var sep1 = CreateLayoutChild(panelParent, "Sep1", 2f);
            sep1.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            // Stat rows container
            if (_statRows == null || _statRows.Length == 0)
            {
                _statRows = new StatCompareRow[4];
                string[] defaultStats = { "ATK", "DEF", "HP", "CRI" };
                for (int i = 0; i < 4; i++)
                {
                    var rowGo = CreateLayoutChild(panelParent, $"StatRow_{i}", 32f);
                    var rowBg = rowGo.AddComponent<Image>();
                    rowBg.color = i % 2 == 0
                        ? new Color(0.15f, 0.13f, 0.22f, 0.6f)
                        : new Color(0.1f, 0.09f, 0.18f, 0.6f);

                    var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                    hlg.childAlignment = TextAnchor.MiddleLeft;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.spacing = 4f;
                    hlg.padding = new RectOffset(8, 8, 0, 0);

                    var nameGo = new GameObject("Name", typeof(RectTransform));
                    nameGo.transform.SetParent(rowGo.transform, false);
                    nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 30f);
                    var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
                    nameTmp.fontSize = 14f;
                    nameTmp.color = new Color(0.8f, 0.8f, 0.8f);
                    nameTmp.text = defaultStats[i];
                    nameTmp.alignment = TextAlignmentOptions.Left;

                    var beforeGo = new GameObject("Before", typeof(RectTransform));
                    beforeGo.transform.SetParent(rowGo.transform, false);
                    beforeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 30f);
                    var beforeTmp = beforeGo.AddComponent<TextMeshProUGUI>();
                    beforeTmp.fontSize = 14f;
                    beforeTmp.color = new Color(0.6f, 0.6f, 0.6f);
                    beforeTmp.alignment = TextAlignmentOptions.Right;

                    var arrowGo = new GameObject("Arrow", typeof(RectTransform));
                    arrowGo.transform.SetParent(rowGo.transform, false);
                    arrowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 30f);
                    var arrowTmp = arrowGo.AddComponent<TextMeshProUGUI>();
                    arrowTmp.text = ">";
                    arrowTmp.fontSize = 14f;
                    arrowTmp.color = new Color(0.5f, 0.5f, 0.5f);
                    arrowTmp.alignment = TextAlignmentOptions.Center;

                    var afterGo = new GameObject("After", typeof(RectTransform));
                    afterGo.transform.SetParent(rowGo.transform, false);
                    afterGo.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 30f);
                    var afterTmp = afterGo.AddComponent<TextMeshProUGUI>();
                    afterTmp.fontSize = 14f;
                    afterTmp.color = Color.white;
                    afterTmp.alignment = TextAlignmentOptions.Right;

                    var deltaGo = new GameObject("Delta", typeof(RectTransform));
                    deltaGo.transform.SetParent(rowGo.transform, false);
                    deltaGo.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 30f);
                    var deltaTmp = deltaGo.AddComponent<TextMeshProUGUI>();
                    deltaTmp.fontSize = 14f;
                    deltaTmp.color = _increaseColor;
                    deltaTmp.alignment = TextAlignmentOptions.Right;

                    _statRows[i] = new StatCompareRow
                    {
                        root = rowGo,
                        nameText = nameTmp,
                        beforeText = beforeTmp,
                        afterText = afterTmp,
                        deltaText = deltaTmp
                    };
                }
            }

            // Separator
            var sep2 = CreateLayoutChild(panelParent, "Sep2", 2f);
            sep2.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            // CP comparison row
            var cpRow = CreateLayoutChild(panelParent, "CpRow", 36f);
            var cpHlg = cpRow.AddComponent<HorizontalLayoutGroup>();
            cpHlg.childAlignment = TextAnchor.MiddleCenter;
            cpHlg.childControlWidth = false;
            cpHlg.childControlHeight = false;
            cpHlg.spacing = 8f;

            if (_cpBeforeText == null)
            {
                var go = new GameObject("CpBeforeText", typeof(RectTransform));
                go.transform.SetParent(cpRow.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 36f);
                _cpBeforeText = go.AddComponent<TextMeshProUGUI>();
                _cpBeforeText.fontSize = 18f;
                _cpBeforeText.alignment = TextAlignmentOptions.Center;
                _cpBeforeText.color = new Color(0.6f, 0.6f, 0.6f);
            }

            // Arrow text
            var cpArrow = new GameObject("CpArrow", typeof(RectTransform));
            cpArrow.transform.SetParent(cpRow.transform, false);
            cpArrow.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 36f);
            var cpArrowTmp = cpArrow.AddComponent<TextMeshProUGUI>();
            cpArrowTmp.text = ">";
            cpArrowTmp.fontSize = 18f;
            cpArrowTmp.color = new Color(0.5f, 0.5f, 0.5f);
            cpArrowTmp.alignment = TextAlignmentOptions.Center;

            if (_cpAfterText == null)
            {
                var go = new GameObject("CpAfterText", typeof(RectTransform));
                go.transform.SetParent(cpRow.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 36f);
                _cpAfterText = go.AddComponent<TextMeshProUGUI>();
                _cpAfterText.fontSize = 20f;
                _cpAfterText.alignment = TextAlignmentOptions.Center;
                _cpAfterText.color = Color.white;
                _cpAfterText.fontStyle = FontStyles.Bold;
            }

            if (_cpDeltaText == null)
            {
                var go = CreateLayoutChild(panelParent, "CpDeltaText", 30f);
                _cpDeltaText = go.AddComponent<TextMeshProUGUI>();
                _cpDeltaText.fontSize = 20f;
                _cpDeltaText.alignment = TextAlignmentOptions.Center;
                _cpDeltaText.color = _increaseColor;
                _cpDeltaText.fontStyle = FontStyles.Bold;
            }

            // Close button (top-right corner)
            if (_closeButton == null)
            {
                var go = new GameObject("CloseButton", typeof(RectTransform));
                go.transform.SetParent(panelParent, false);
                var closeBg = go.AddComponent<Image>();
                closeBg.color = Color.white;
                _closeButton = go.AddComponent<Button>();
                var tmClose = UIThemeManager.Instance;
                if (tmClose != null) tmClose.ApplyCloseButton(closeBg);
                else closeBg.color = new Color(0.5f, 0.2f, 0.2f, 0.9f);
                var closeRt = go.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.sizeDelta = new Vector2(32f, 32f);
                closeRt.anchoredPosition = new Vector2(-4f, -4f);
                var le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                var txtGo = new GameObject("X", typeof(RectTransform));
                txtGo.transform.SetParent(go.transform, false);
                var txtRt = txtGo.GetComponent<RectTransform>();
                SetStretchAll(txtRt);
                var tmp = txtGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "X";
                tmp.fontSize = 18f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static GameObject CreateLayoutChild(Transform parent, string childName, float height)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return go;
        }

        private static void SetStretchAll(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();
            if (_popupRoot != null) _popupRoot.DOKill();
        }
    }

    /// <summary>스탯 비교 행 UI 구조체.</summary>
    [System.Serializable]
    public struct StatCompareRow
    {
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text beforeText;
        public TMP_Text afterText;
        public TMP_Text deltaText;
        public Image arrowImage;
    }

    /// <summary>스탯 증감 데이터.</summary>
    public struct StatDelta
    {
        public string StatName;
        public int Before;
        public int After;
    }
}
