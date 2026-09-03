using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 아이템/장비/동료 획득 후 관련 패널로 바로가기 버튼을 제공하는 팝업.
    /// 가챠 결과 → "장착하기", 장비 획득 → "강화하기", 동료 획득 → "파티 편성" 등.
    /// 각 획득 이벤트를 구독하여 자동으로 표시된다.
    /// </summary>
    public class AcquisitionShortcutPopup : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _popupRoot;
        [SerializeField] private TMP_Text _itemNameText;
        [SerializeField] private TMP_Text _itemGradeText;
        [SerializeField] private TMP_Text _comparisonText;
        [SerializeField] private Image _gradeGlow;

        [Header("바로가기 버튼")]
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonText;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private TMP_Text _secondaryButtonText;
        [SerializeField] private Button _closeButton;

        [Header("설정")]
        [SerializeField] private float _autoCloseDelay = 5f;

        private Sequence _showSeq;
        private Tween _autoCloseTween;
        private ShortcutAction _primaryAction;
        private ShortcutAction _secondaryAction;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<EquipmentInventoryChangedEvent>.Subscribe(OnEquipmentObtained);
            EventBus<WeaponInventoryChangedEvent>.Subscribe(OnWeaponObtained);
        }

        private void OnDisable()
        {
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<EquipmentInventoryChangedEvent>.Unsubscribe(OnEquipmentObtained);
            EventBus<WeaponInventoryChangedEvent>.Unsubscribe(OnWeaponObtained);
        }

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HidePopup);

            if (_primaryButton != null)
                _primaryButton.onClick.AddListener(OnPrimaryClicked);

            if (_secondaryButton != null)
                _secondaryButton.onClick.AddListener(OnSecondaryClicked);

            HideImmediate();
        }

        // ── 이벤트 핸들러 ──

        private void OnGachaResult(GachaResultEvent evt)
        {
            // 에픽 이상만 바로가기 표시
            if (evt.Grade is "Normal" or "Rare") return;

            // GachaQuickCompare가 처리 중이면 중복 팝업 방지
            if (GachaCompareSystem.Instance != null && GachaCompareSystem.Instance.IsComparing)
                return;

            string comparison = BuildCpComparison(evt.ItemId, evt.Grade, evt.PoolName);

            ShowPopup(
                itemName: ResolveDisplayName(evt.ItemId),
                gradeLabel: GetGradeLabel(evt.Grade),
                gradeColor: GetGradeColor(evt.Grade),
                comparison: comparison,
                primaryLabel: "장착하기",
                primaryAction: ShortcutAction.OpenEquipment,
                secondaryLabel: "강화하기",
                secondaryAction: ShortcutAction.OpenEnhance
            );
        }

        private void OnEquipmentObtained(EquipmentInventoryChangedEvent evt)
        {
            if (!evt.IsAdded) return;
            if (evt.Grade is "Normal" or "Rare") return;

            string comparison = BuildCpComparison(evt.EquipmentId, evt.Grade, "Equipment");

            ShowPopup(
                itemName: ResolveDisplayName(evt.EquipmentId),
                gradeLabel: GetGradeLabel(evt.Grade),
                gradeColor: GetGradeColor(evt.Grade),
                comparison: comparison,
                primaryLabel: "장착하기",
                primaryAction: ShortcutAction.OpenEquipment,
                secondaryLabel: "강화하기",
                secondaryAction: ShortcutAction.OpenEnhance
            );
        }

        private void OnWeaponObtained(WeaponInventoryChangedEvent evt)
        {
            if (!evt.IsAdded) return;

            string comparison = BuildCpComparison(evt.WeaponId, evt.Grade, "Weapon");

            ShowPopup(
                itemName: ResolveDisplayName(evt.WeaponId),
                gradeLabel: GetGradeLabel(evt.Grade),
                gradeColor: GetGradeColor(evt.Grade),
                comparison: comparison,
                primaryLabel: "장착하기",
                primaryAction: ShortcutAction.OpenWeapon,
                secondaryLabel: "강화하기",
                secondaryAction: ShortcutAction.OpenWeaponEnhance
            );
        }

        // OnRelicObtained: 2026-04-20 유물 시스템 완전 제거

        /// <summary>
        /// 획득 아이템의 CP 이득을 계산하여 비교 문자열을 생성한다.
        /// 현재 장착 대비 CP 향상이 있으면 "전투력 +N" 표시.
        /// </summary>
        private static string BuildCpComparison(string itemId, string grade, string poolName)
        {
            long cpDelta = 0;

            switch (poolName)
            {
                case "Equipment":
                    cpDelta = CalculateEquipmentCpDelta(itemId, grade);
                    break;
                case "Weapon":
                    cpDelta = CalculateWeaponCpDelta(itemId, grade);
                    break;
            }

            if (cpDelta > 0)
                return $"<color=#4DE680>▲ 전투력 +{cpDelta:N0}</color>";
            if (cpDelta < 0)
                return $"<color=#E64D4D>▼ 전투력 {cpDelta:N0}</color>";

            return "";
        }

        private static long CalculateEquipmentCpDelta(string equipmentId, string grade)
        {
            if (MkLike.Equipment.EquipmentManager.Instance == null) return 0;

            var data = MkLike.Equipment.EquipmentManager.Instance.GetData(equipmentId);
            if (data == null) return 0;

            int newAtk = data.GetAtk(grade);
            int newHp = data.GetHp(grade);
            int newDef = data.GetDef(grade);
            float newCrit = data.GetCritRate(grade);

            int curAtk = 0, curHp = 0, curDef = 0;
            float curCrit = 0f;

            var equipped = MkLike.Equipment.EquipmentManager.Instance.GetEquipped(data.slot);
            if (equipped != null)
            {
                var curData = MkLike.Equipment.EquipmentManager.Instance.GetData(equipped.equipmentId);
                if (curData != null)
                {
                    curAtk = curData.GetAtk(equipped.grade);
                    curHp = curData.GetHp(equipped.grade);
                    curDef = curData.GetDef(equipped.grade);
                    curCrit = curData.GetCritRate(equipped.grade);
                }
            }

            long newCp = (long)(newAtk * 3 + newHp * 0.5f + newDef * 2 + newCrit * 500);
            long curCp = (long)(curAtk * 3 + curHp * 0.5f + curDef * 2 + curCrit * 500);

            return newCp - curCp;
        }

        private static long CalculateWeaponCpDelta(string weaponId, string grade)
        {
            if (MkLike.Equipment.WeaponManager.Instance == null) return 0;

            var data = MkLike.Equipment.WeaponManager.Instance.GetData(weaponId);
            if (data == null) return 0;

            int newAtk = data.GetFinalAtk(grade, 1, 0);
            float newCrit = data.GetCritRate(grade) + data.equipCritRate;
            float newAtkSpd = data.GetAtkSpeed(grade);

            int curAtk = 0;
            float curCrit = 0f, curAtkSpd = 0f;

            var equipped = MkLike.Equipment.WeaponManager.Instance.EquippedWeapon;
            if (equipped != null)
            {
                var curData = MkLike.Equipment.WeaponManager.Instance.GetData(equipped.weaponId);
                if (curData != null)
                {
                    curAtk = curData.GetFinalAtk(equipped.grade, equipped.level, equipped.awakeningStars);
                    curCrit = curData.GetCritRate(equipped.grade) + curData.equipCritRate;
                    curAtkSpd = curData.GetAtkSpeed(equipped.grade);
                }
            }

            long newCp = (long)(newAtk * 3 + newCrit * 500 + newAtkSpd * 100);
            long curCp = (long)(curAtk * 3 + curCrit * 500 + curAtkSpd * 100);

            return newCp - curCp;
        }

        // ── 표시/숨김 ──

        private void ShowPopup(string itemName, string gradeLabel, Color gradeColor,
            string comparison, string primaryLabel, ShortcutAction primaryAction,
            string secondaryLabel, ShortcutAction secondaryAction)
        {
            // 뽑기 결과 팝업이 열려있으면 이중 팝업 방지
            if (ShopPanel.IsPullPopupOpen) return;

            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            _primaryAction = primaryAction;
            _secondaryAction = secondaryAction;

            if (_itemNameText != null)
                _itemNameText.text = itemName;

            if (_itemGradeText != null)
            {
                _itemGradeText.text = gradeLabel;
                _itemGradeText.color = gradeColor;
            }

            if (_comparisonText != null)
            {
                _comparisonText.text = comparison;
                _comparisonText.gameObject.SetActive(!string.IsNullOrEmpty(comparison));
            }

            if (_gradeGlow != null)
                _gradeGlow.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.4f);

            if (_primaryButton != null)
            {
                _primaryButton.gameObject.SetActive(primaryAction != ShortcutAction.None);
                if (_primaryButtonText != null) _primaryButtonText.text = primaryLabel;
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.gameObject.SetActive(secondaryAction != ShortcutAction.None);
                if (_secondaryButtonText != null) _secondaryButtonText.text = secondaryLabel;
            }

            // 연출
            _popupRoot.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _popupRoot.localScale = Vector3.one * 0.8f;

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

            // 자동 닫기
            _autoCloseTween = DOVirtual.DelayedCall(_autoCloseDelay, () => HidePopup(), true)
                .SetLink(gameObject);

            AudioManager.Instance?.PlayUiSfx(SfxType.UiPopupOpen);
        }

        private void HidePopup()
        {
            _showSeq?.Kill();
            _autoCloseTween?.Kill();

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 0f, 0.15f))
                .Join(_popupRoot.DOScale(0.9f, 0.15f).SetEase(Ease.InBack))
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

        // ── 바로가기 실행 ──

        private void OnPrimaryClicked()
        {
            ExecuteAction(_primaryAction);
            HidePopup();
        }

        private void OnSecondaryClicked()
        {
            ExecuteAction(_secondaryAction);
            HidePopup();
        }

        private static void ExecuteAction(ShortcutAction action)
        {
            if (UIManager.Instance == null) return;

            // EquipmentPanel은 CharacterPanel의 서브탭(인덱스 1)이므로 직접 OpenPopup하면 안 된다.
            // CharacterPanel을 TabController로 열고 서브탭을 전환해야 한다.
            switch (action)
            {
                case ShortcutAction.OpenEquipment:
                case ShortcutAction.OpenEnhance:
                    UIManager.Instance.OpenCharacterPanelTab(1); // 장비 서브탭
                    return;
                case ShortcutAction.OpenWeapon:
                case ShortcutAction.OpenWeaponEnhance:
                    UIManager.Instance.OpenCharacterPanelTab(1); // 장비 서브탭 (무기도 장비 탭 내부)
                    return;
            }

            // 유물 시스템 제거 (2026-04-20) — 모든 ShortcutAction은 위 switch에서 처리됨
        }

        // ── 유틸 ──

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

        /// <summary>
        /// 내부 ID를 한국어 표시 이름으로 변환한다.
        /// EquipmentManager 등에서 displayName을 조회하고,
        /// 찾지 못하면 ID에서 접두어를 제거한 fallback 이름을 반환한다.
        /// </summary>
        private static string ResolveDisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return itemId;

            // 장비 SO에서 한글 이름 조회
            if (MkLike.Equipment.EquipmentManager.Instance != null)
            {
                var eqData = MkLike.Equipment.EquipmentManager.Instance.GetData(itemId);
                if (eqData != null && !string.IsNullOrEmpty(eqData.displayName))
                    return eqData.displayName;
            }

            // fallback: 접두어 제거 후 PascalCase 변환
            string[] parts = itemId.Split('_');
            int start = parts.Length > 1 ? 1 : 0;
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < parts.Length; i++)
            {
                if (i > start) sb.Append(' ');
                if (parts[i].Length > 0)
                    sb.Append(char.ToUpper(parts[i][0])).Append(parts[i], 1, parts[i].Length - 1);
            }
            return sb.ToString();
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 220;
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
                var go = CreateUIChild("PopupRoot");
                _popupRoot = go.GetComponent<RectTransform>();
                _popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
                _popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
                _popupRoot.pivot = new Vector2(0.5f, 0.5f);
                _popupRoot.sizeDelta = new Vector2(380f, 280f);
                _popupRoot.anchoredPosition = Vector2.zero;
                var bg = go.AddComponent<Image>();
                bg.color = Color.white;
                var tm = UIThemeManager.Instance;
                if (tm != null) tm.ApplyPanelBackground(bg);
                else bg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);

                // VerticalLayoutGroup for panel content
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 8f;
                vlg.padding = new RectOffset(16, 16, 12, 12);
            }

            Transform panelParent = _popupRoot != null ? _popupRoot : transform;

            // Grade glow behind text (first child so it renders behind)
            if (_gradeGlow == null)
            {
                var go = new GameObject("GradeGlow", typeof(RectTransform));
                go.transform.SetParent(panelParent, false);
                _gradeGlow = go.AddComponent<Image>();
                _gradeGlow.color = new Color(1f, 1f, 1f, 0.15f);
                _gradeGlow.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(120f, 120f);
                rt.anchoredPosition = new Vector2(0f, 30f);
                var le = go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            if (_itemNameText == null)
            {
                var go = CreateLayoutChild(panelParent, "ItemNameText", 32f);
                _itemNameText = go.AddComponent<TextMeshProUGUI>();
                _itemNameText.fontSize = 22f;
                _itemNameText.alignment = TextAlignmentOptions.Center;
                _itemNameText.color = Color.white;
                _itemNameText.fontStyle = FontStyles.Bold;
            }

            if (_itemGradeText == null)
            {
                var go = CreateLayoutChild(panelParent, "ItemGradeText", 26f);
                _itemGradeText = go.AddComponent<TextMeshProUGUI>();
                _itemGradeText.fontSize = 18f;
                _itemGradeText.alignment = TextAlignmentOptions.Center;
                _itemGradeText.color = Color.white;
            }

            if (_comparisonText == null)
            {
                var go = CreateLayoutChild(panelParent, "ComparisonText", 24f);
                _comparisonText = go.AddComponent<TextMeshProUGUI>();
                _comparisonText.fontSize = 16f;
                _comparisonText.alignment = TextAlignmentOptions.Center;
                _comparisonText.color = new Color(0.5f, 0.9f, 0.5f);
            }

            // Separator
            var sep = CreateLayoutChild(panelParent, "Separator", 2f);
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.15f);

            // Button row
            var btnRow = CreateLayoutChild(panelParent, "ButtonRow", 44f);
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 12f;

            if (_primaryButton == null)
            {
                var go = new GameObject("PrimaryButton", typeof(RectTransform));
                go.transform.SetParent(btnRow.transform, false);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = Color.white;
                _primaryButton = go.AddComponent<Button>();
                var tmPri = UIThemeManager.Instance;
                if (tmPri != null) tmPri.ApplyConfirmButton(btnBg);
                else btnBg.color = new Color(0.2f, 0.5f, 0.8f);
                var btnRt = go.GetComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(140f, 40f);
                var txtGo = new GameObject("Text", typeof(RectTransform));
                txtGo.transform.SetParent(go.transform, false);
                var txtRt = txtGo.GetComponent<RectTransform>();
                SetStretchAll(txtRt);
                _primaryButtonText = txtGo.AddComponent<TextMeshProUGUI>();
                _primaryButtonText.fontSize = 16f;
                _primaryButtonText.alignment = TextAlignmentOptions.Center;
                _primaryButtonText.color = Color.white;
                _primaryButtonText.text = "장착하기";
            }

            if (_secondaryButton == null)
            {
                var go = new GameObject("SecondaryButton", typeof(RectTransform));
                go.transform.SetParent(btnRow.transform, false);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = Color.white;
                _secondaryButton = go.AddComponent<Button>();
                var tmSec = UIThemeManager.Instance;
                if (tmSec != null) tmSec.ApplyButtonFull(_secondaryButton);
                else btnBg.color = new Color(0.35f, 0.35f, 0.4f);
                var btnRt = go.GetComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(140f, 40f);
                var txtGo = new GameObject("Text", typeof(RectTransform));
                txtGo.transform.SetParent(go.transform, false);
                var txtRt = txtGo.GetComponent<RectTransform>();
                SetStretchAll(txtRt);
                _secondaryButtonText = txtGo.AddComponent<TextMeshProUGUI>();
                _secondaryButtonText.fontSize = 16f;
                _secondaryButtonText.alignment = TextAlignmentOptions.Center;
                _secondaryButtonText.color = Color.white;
                _secondaryButtonText.text = "강화하기";
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

        private enum ShortcutAction
        {
            None,
            OpenEquipment,
            OpenEnhance,
            OpenWeapon,
            OpenWeaponEnhance
            // OpenRelic: 2026-04-20 유물 시스템 완전 제거
        }
    }
}
