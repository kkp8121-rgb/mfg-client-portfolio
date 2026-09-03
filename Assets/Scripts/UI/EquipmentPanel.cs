using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Data;
using MkLike.Equipment;
using MkLike.Utils;
using EquipmentInstance = MkLike.Core.EquipmentInstance;

namespace MkLike.UI
{
    /// <summary>
    /// 장비 패널. 3개 서브탭: 장비(12슬롯 + 인벤토리) / 무기 / 강화.
    /// CharacterPanel의 서브 탭으로 사용.
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        [Header("서브탭 버튼")]
        [SerializeField] private Button _tabEquipmentBtn;
        [SerializeField] private Button _tabWeaponBtn;
        [SerializeField] private Button _tabEnhanceBtn;

        [Header("장비 탭 컨테이너")]
        [SerializeField] private GameObject _equipmentTabContent;

        [Header("장비 슬롯")]
        [SerializeField] private EquipmentSlotUI[] _slotUIs;

        [Header("인벤토리")]
        [SerializeField] private Transform _inventoryContent;
        [SerializeField] private TextMeshProUGUI _inventoryCountText;

        [Header("보너스 스탯 표시")]
        [SerializeField] private TextMeshProUGUI _bonusAtkText;
        [SerializeField] private TextMeshProUGUI _bonusHpText;
        [SerializeField] private TextMeshProUGUI _bonusDefText;

        [Header("추천 장착")]
        [SerializeField] private Button _recommendEquipButton;
        [SerializeField] private TMP_Text _recommendResultText;

        // 서브탭별 루트 오브젝트 (런타임 생성)
        private GameObject _weaponTabRoot;
        private GameObject _enhanceTabRoot;

        // 무기 탭 요소
        private Transform _weaponInventoryContent;
        private TextMeshProUGUI _weaponEquippedText;
        private TextMeshProUGUI _weaponCountText;

        // 강화 탭 요소
        private Transform _enhanceContent;
        private string _selectedEquipmentId; // 강화 대상으로 선택된 장비 인스턴스 ID

        // 현재 서브탭
        private int _currentSubTab; // 0=장비, 1=무기, 2=강화

        // 등급별 색상
        private static readonly Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.7f, 0.7f, 0.7f) },
            { "Rare", new Color(0.3f, 0.6f, 1f) },
            { "Epic", new Color(0.7f, 0.3f, 0.9f) },
            { "Unique", new Color(1f, 0.85f, 0.1f) },
            { "Legendary", new Color(1f, 0.5f, 0.1f) },
            { "Mythic", new Color(1f, 0.2f, 0.3f) },
        };

        private static readonly Color TextWhite = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color TextTan = new(0.682f, 0.631f, 0.565f, 1f);
        private static readonly Color TextGold = new(1f, 0.85f, 0.086f, 1f);
        private static readonly Color BtnGreen = new(0.25f, 0.45f, 0.30f, 1f);
        private static readonly Color BtnRed = new(0.5f, 0.25f, 0.25f, 1f);
        private static readonly Color BtnBlue = new(0.25f, 0.30f, 0.50f, 1f);
        private static readonly Color BtnDisabled = new(0.3f, 0.3f, 0.3f, 0.7f);
        private static readonly Color RowBg = new(0.18f, 0.16f, 0.14f, 0.9f);
        private static readonly Color RowBgEquipped = new(0.22f, 0.28f, 0.22f, 0.9f);
        private static readonly Color TabActive = new(0.3f, 0.35f, 0.4f, 1f);
        private static readonly Color TabInactive = new(0.15f, 0.14f, 0.12f, 0.9f);

        private bool _initialized;

        private void Start()
        {
            EnsureComponents();
            _initialized = true;
            SetupSubTabButtons();
            SwitchTab(0);

            if (_recommendEquipButton != null)
                _recommendEquipButton.onClick.AddListener(OnRecommendEquipClicked);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Subscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<WeaponChangedEvent>(OnWeaponChanged);
            EventBus.Subscribe<WeaponInventoryChangedEvent>(OnWeaponInventoryChanged);
            EventBus.Subscribe<WeaponLevelUpEvent>(OnWeaponLevelUp);
            EventBus.Subscribe<WeaponAwakeningEvent>(OnWeaponAwakening);
            EventBus.Subscribe<WeaponPromoteEvent>(OnWeaponPromote);
            if (_initialized) RefreshCurrentTab();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Unsubscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<WeaponChangedEvent>(OnWeaponChanged);
            EventBus.Unsubscribe<WeaponInventoryChangedEvent>(OnWeaponInventoryChanged);
            EventBus.Unsubscribe<WeaponLevelUpEvent>(OnWeaponLevelUp);
            EventBus.Unsubscribe<WeaponAwakeningEvent>(OnWeaponAwakening);
            EventBus.Unsubscribe<WeaponPromoteEvent>(OnWeaponPromote);
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt) => RefreshCurrentTab();
        private void OnInventoryChanged(EquipmentInventoryChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponChanged(WeaponChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponInventoryChanged(WeaponInventoryChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponLevelUp(WeaponLevelUpEvent evt) => RefreshCurrentTab();
        private void OnWeaponAwakening(WeaponAwakeningEvent evt) => RefreshCurrentTab();
        private void OnWeaponPromote(WeaponPromoteEvent evt) => RefreshCurrentTab();

        // ── 서브탭 관리 ──

        private void SetupSubTabButtons()
        {
            if (_tabEquipmentBtn != null)
                _tabEquipmentBtn.onClick.AddListener(() => SwitchTab(0));
            if (_tabWeaponBtn != null)
                _tabWeaponBtn.onClick.AddListener(() => SwitchTab(1));
            if (_tabEnhanceBtn != null)
                _tabEnhanceBtn.onClick.AddListener(() => SwitchTab(2));
        }

        private void SwitchTab(int tabIndex)
        {
            _currentSubTab = tabIndex;
            UpdateTabButtonColors();
            ShowTabContent(tabIndex);
            RefreshCurrentTab();
        }

        private void UpdateTabButtonColors()
        {
            SetTabColor(_tabEquipmentBtn, _currentSubTab == 0);
            SetTabColor(_tabWeaponBtn, _currentSubTab == 1);
            SetTabColor(_tabEnhanceBtn, _currentSubTab == 2);
        }

        private static void SetTabColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;

            // UIThemeManager 탭 스프라이트 적용
            if (UIThemeManager.Instance != null)
            {
                var (tabNormal, tabSelected) = UIThemeManager.Instance.GetTabSprites();
                var sprite = isActive ? tabSelected : tabNormal;
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                    return;
                }
            }
            img.color = isActive ? TabActive : TabInactive;
        }

        private void ShowTabContent(int tabIndex)
        {
            // 장비 탭 기본 요소 표시/숨김
            SetEquipmentTabVisible(tabIndex == 0);

            // 무기 탭
            if (_weaponTabRoot != null)
                _weaponTabRoot.SetActive(tabIndex == 1);
            else if (tabIndex == 1)
                BuildWeaponTab();

            // 강화 탭
            if (_enhanceTabRoot != null)
                _enhanceTabRoot.SetActive(tabIndex == 2);
            else if (tabIndex == 2)
                BuildEnhanceTab();
        }

        private void SetEquipmentTabVisible(bool visible)
        {
            if (_equipmentTabContent != null)
                _equipmentTabContent.SetActive(visible);
        }

        private void RefreshCurrentTab()
        {
            switch (_currentSubTab)
            {
                case 0:
                    Refresh();
                    break;
                case 1:
                    RefreshWeaponTab();
                    break;
                case 2:
                    RefreshEnhanceTab();
                    break;
            }
        }

        /// <summary>
        /// 장비 탭 전체 UI 갱신.
        /// </summary>
        public void Refresh()
        {
            RefreshSlots();
            RefreshInventory();
            RefreshBonusStats();
        }

        // ══════════════════════════════════
        //  탭 0: 장비 슬롯 + 인벤토리
        // ══════════════════════════════════

        private void RefreshSlots()
        {
            if (_slotUIs == null || EquipmentManager.Instance == null) return;

            var equipped = EquipmentManager.Instance.GetAllEquipped();
            int slotCount = System.Enum.GetValues(typeof(EquipmentSlot)).Length;

            for (int i = 0; i < _slotUIs.Length && i < slotCount; i++)
            {
                var slotUI = _slotUIs[i];
                if (slotUI.nameText == null) continue;

                var slot = (EquipmentSlot)i;
                if (equipped.TryGetValue(slot, out var instance))
                {
                    var data = EquipmentManager.Instance.GetData(instance.equipmentId);
                    string name = data != null ? data.displayName : instance.equipmentId;
                    Color gradeColor = GetGradeColor(instance.grade);

                    slotUI.nameText.text = name;
                    slotUI.nameText.color = gradeColor;

                    if (slotUI.gradeText != null)
                    {
                        slotUI.gradeText.text = instance.grade;
                        slotUI.gradeText.color = gradeColor;
                    }

                    if (slotUI.gradeMarker != null)
                        slotUI.gradeMarker.color = gradeColor;

                    if (slotUI.iconImage != null)
                    {
                        if (data != null && data.icon != null)
                        {
                            slotUI.iconImage.sprite = data.icon;
                            slotUI.iconImage.color = Color.white;
                            slotUI.iconImage.gameObject.SetActive(true);
                        }
                        else
                        {
                            // 장비 데이터에 아이콘 없으면 EquipSlot 기본 아이콘 사용
                            var slotIcon = GetEquipSlotFallbackIcon(slot);
                            if (slotIcon != null)
                            {
                                slotUI.iconImage.sprite = slotIcon;
                                slotUI.iconImage.color = gradeColor;
                                slotUI.iconImage.gameObject.SetActive(true);
                            }
                            else
                            {
                                slotUI.iconImage.gameObject.SetActive(false);
                            }
                        }
                    }

                    // 등급 프레임에 UIThemeManager 색상 적용
                    if (slotUI.gradeMarker != null && UIThemeManager.Instance != null)
                        slotUI.gradeMarker.color = UIThemeManager.Instance.GetGradeColor(instance.grade);

                    if (slotUI.emptyText != null)
                        slotUI.emptyText.gameObject.SetActive(false);
                }
                else
                {
                    slotUI.nameText.text = "";
                    if (slotUI.gradeText != null) slotUI.gradeText.text = "";
                    if (slotUI.gradeMarker != null) slotUI.gradeMarker.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

                    // 빈 슬롯에도 부위별 플레이스홀더 아이콘 표시
                    if (slotUI.iconImage != null)
                    {
                        var slotIcon = GetEquipSlotFallbackIcon(slot);
                        if (slotIcon != null)
                        {
                            slotUI.iconImage.sprite = slotIcon;
                            slotUI.iconImage.color = new Color(0.4f, 0.4f, 0.4f, 0.3f);
                            slotUI.iconImage.gameObject.SetActive(true);
                        }
                        else
                        {
                            slotUI.iconImage.gameObject.SetActive(false);
                        }
                    }

                    if (slotUI.emptyText != null)
                    {
                        slotUI.emptyText.gameObject.SetActive(true);
                        slotUI.emptyText.text = GetSlotDisplayName(slot);
                    }
                }
            }
        }

        // ── 인벤토리 갱신 ──

        /// <summary>
        /// 인벤토리 장비가 현재 장착 장비 대비 CP 이득이 있는지 계산한다.
        /// 이득이 있으면 양수, 없으면 0을 반환한다.
        /// </summary>
        private int CalculateCpGainOverEquipped(EquipmentDataSO data, EquipmentInstance instance)
        {
            if (EquipmentManager.Instance == null) return 0;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(data.slot);
            float enhMult = (1f + slotEnh.scrollLevel * 0.05f) * (1f + slotEnh.starForce * 0.03f);

            int newAtk = data.GetAtk(instance.grade);
            int newHp = data.GetHp(instance.grade);
            int newDef = data.GetDef(instance.grade);
            int newCp = Mathf.RoundToInt((newAtk * 3f + newDef * 2f + newHp * 0.5f) * enhMult * instance.AwakeningMultiplier);

            var equipped = EquipmentManager.Instance.GetEquipped(data.slot);
            int equippedCp = 0;
            if (equipped != null)
            {
                var equippedData = EquipmentManager.Instance.GetData(equipped.equipmentId);
                if (equippedData != null)
                {
                    int eAtk = equippedData.GetAtk(equipped.grade);
                    int eHp = equippedData.GetHp(equipped.grade);
                    int eDef = equippedData.GetDef(equipped.grade);
                    equippedCp = Mathf.RoundToInt((eAtk * 3f + eDef * 2f + eHp * 0.5f) * enhMult * equipped.AwakeningMultiplier);
                }
            }

            int gain = newCp - equippedCp;
            return gain > 0 ? gain : 0;
        }

        private void RefreshInventory()
        {
            if (_inventoryContent == null || EquipmentManager.Instance == null) return;

            ClearChildren(_inventoryContent);

            var sorted = EquipmentManager.Instance.GetSortedInventory();

            if (_inventoryCountText != null)
                _inventoryCountText.text = $"보유: {sorted.Count}개";

            for (int i = 0; i < sorted.Count; i++)
            {
                CreateInventoryItem(sorted[i]);
            }
        }

        private void CreateInventoryItem(EquipmentInstance instance)
        {
            var data = EquipmentManager.Instance.GetData(instance.equipmentId);
            if (data == null) return;

            bool isEquipped = EquipmentManager.Instance.IsEquipped(instance.instanceId);
            Color gradeColor = GetGradeColor(instance.grade);

            // 행 컨테이너
            var row = new GameObject($"Item_{instance.instanceId[..8]}");
            row.transform.SetParent(_inventoryContent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 72);

            var bg = row.AddComponent<Image>();
            bg.color = isEquipped ? RowBgEquipped : RowBg;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(12, 12, 4, 4);

            // 등급 마커
            CreateGradeMarker(row.transform, gradeColor);

            // CP 비교 화살표 (장착 장비보다 CP가 높으면 ↑ 표시)
            int cpGain = 0;
            if (!isEquipped)
            {
                cpGain = CalculateCpGainOverEquipped(data, instance);
            }
            if (cpGain > 0)
            {
                var arrowObj = new GameObject("UpArrow");
                arrowObj.transform.SetParent(row.transform, false);
                var arrowRt = arrowObj.AddComponent<RectTransform>();
                arrowRt.sizeDelta = new Vector2(36, 64);
                var arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
                arrowText.text = $"\u25B2\n+{cpGain}";
                arrowText.fontSize = 11f;
                arrowText.alignment = TextAlignmentOptions.Center;
                arrowText.color = new Color(0.2f, 1f, 0.4f);
                arrowText.raycastTarget = false;
            }

            // 아이콘
            if (data.icon != null)
                CreateIcon(row.transform, data.icon, 56);

            // 이름 + 등급 + 슬롯
            string namePrefix = cpGain > 0 ? "" : "";
            var infoObj = CreateInfoColumn(row.transform, data.displayName, gradeColor,
                $"{instance.grade} · {GetSlotDisplayName(data.slot)}");

            // 스탯 요약
            var sb = new StringBuilder();
            int atk = data.GetAtk(instance.grade);
            int hp = data.GetHp(instance.grade);
            int def = data.GetDef(instance.grade);
            if (atk > 0) sb.Append($"ATK+{atk} ");
            if (hp > 0) sb.Append($"HP+{hp} ");
            if (def > 0) sb.Append($"DEF+{def}");

            // 강화 정보 표시 (슬롯 기반)
            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(data.slot);
            if (slotEnh.scrollLevel > 0 || slotEnh.starForce > 0)
            {
                sb.Append('\n');
                if (slotEnh.scrollLevel > 0) sb.Append($"주문서+{slotEnh.scrollLevel} ");
                if (slotEnh.starForce > 0) sb.Append($"★{slotEnh.starForce}");
            }

            CreateStatText(row.transform, sb.ToString(), 180);

            // 버튼 영역 (장착/해제 + 강화)
            var btnArea = new GameObject("BtnArea");
            btnArea.transform.SetParent(row.transform, false);
            btnArea.AddComponent<RectTransform>().sizeDelta = new Vector2(90, 64);
            var btnVlg = btnArea.AddComponent<VerticalLayoutGroup>();
            btnVlg.childAlignment = TextAnchor.MiddleCenter;
            btnVlg.childControlWidth = true;
            btnVlg.childControlHeight = false;
            btnVlg.childForceExpandWidth = true;
            btnVlg.spacing = 2;

            // 장착/해제 버튼
            string capturedId = instance.instanceId;
            EquipmentSlot capturedSlot = data.slot;

            CreateActionButton(btnArea.transform, isEquipped ? "해제" : "장착",
                isEquipped ? BtnRed : BtnGreen, 28, () =>
                {
                    if (EquipmentManager.Instance == null) return;
                    if (isEquipped)
                        EquipmentManager.Instance.Unequip(capturedSlot);
                    else
                        EquipmentManager.Instance.Equip(capturedId);
                });

            // 강화 버튼
            CreateActionButton(btnArea.transform, "강화", BtnBlue, 28, () =>
            {
                _selectedEquipmentId = capturedId;
                SwitchTab(2);
            });

            // 비교 버튼 (UX-17: 장비 비교)
            if (!isEquipped)
            {
                string compareId = instance.instanceId;
                EquipmentSlot compareSlot = data.slot;
                CreateActionButton(btnArea.transform, "비교", new Color(0.35f, 0.30f, 0.45f, 1f), 24, () =>
                {
                    ShowEquipmentComparison(compareId, compareSlot);
                });
            }

            // 팝인 연출
            rt.localScale = Vector3.zero;
            rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        // ── 보너스 스탯 표시 ──

        private void RefreshBonusStats()
        {
            if (EquipmentManager.Instance == null) return;

            int totalAtk = 0, totalHp = 0, totalDef = 0;

            var equipped = EquipmentManager.Instance.GetAllEquipped();
            foreach (var kv in equipped)
            {
                var data = EquipmentManager.Instance.GetData(kv.Value.equipmentId);
                if (data == null) continue;
                totalAtk += data.GetAtk(kv.Value.grade);
                totalHp += data.GetHp(kv.Value.grade);
                totalDef += data.GetDef(kv.Value.grade);
            }

            if (_bonusAtkText != null)
                _bonusAtkText.text = $"ATK +{totalAtk}";
            if (_bonusHpText != null)
                _bonusHpText.text = $"HP +{totalHp}";
            if (_bonusDefText != null)
                _bonusDefText.text = $"DEF +{totalDef}";
        }

        // ══════════════════════════════════
        //  탭 1: 무기
        // ══════════════════════════════════

        private void BuildWeaponTab()
        {
            _weaponTabRoot = new GameObject("WeaponTab");
            _weaponTabRoot.transform.SetParent(transform, false);
            var rootRT = _weaponTabRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.sizeDelta = Vector2.zero;
            rootRT.anchoredPosition = Vector2.zero;

            var vlg = _weaponTabRoot.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6;
            vlg.padding = new RectOffset(12, 12, 10, 10);

            // 장착 중인 무기 표시
            var equippedRow = new GameObject("EquippedWeapon");
            equippedRow.transform.SetParent(_weaponTabRoot.transform, false);
            equippedRow.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
            equippedRow.AddComponent<Image>().color = new Color(0.14f, 0.20f, 0.14f, 0.7f);
            equippedRow.AddComponent<LayoutElement>().preferredHeight = 40;

            _weaponEquippedText = AddTMP(equippedRow.transform, "EquippedText",
                "장착 무기: 없음", 16, TextGold, TextAlignmentOptions.Center);
            FullStretch(_weaponEquippedText.gameObject);

            // 인벤토리 카운트
            var countObj = new GameObject("WeaponCount");
            countObj.transform.SetParent(_weaponTabRoot.transform, false);
            countObj.AddComponent<RectTransform>();
            countObj.AddComponent<LayoutElement>().preferredHeight = 24;
            _weaponCountText = AddTMP(countObj.transform, "CountText",
                "보유: 0개", 14, TextTan, TextAlignmentOptions.Left);
            FullStretch(_weaponCountText.gameObject);

            // 스크롤뷰
            var scrollObj = BuildScrollView(_weaponTabRoot.transform, out _weaponInventoryContent);
            scrollObj.AddComponent<LayoutElement>().flexibleHeight = 1;

            RefreshWeaponTab();
        }

        private void RefreshWeaponTab()
        {
            if (_weaponTabRoot == null || !_weaponTabRoot.activeSelf) return;
            if (WeaponManager.Instance == null) return;

            // 장착 중인 무기 표시
            if (_weaponEquippedText != null)
            {
                var equipped = WeaponManager.Instance.EquippedWeapon;
                if (equipped != null)
                {
                    var data = WeaponManager.Instance.GetData(equipped.weaponId);
                    string name = data != null ? data.displayName : equipped.weaponId;
                    Color gradeColor = GetGradeColor(equipped.grade);
                    int eqLevelCap = WeaponManager.GetWeaponLevelCap(equipped.awakeningStars);
                    _weaponEquippedText.text = $"장착: {name} ({equipped.grade}) Lv{equipped.level}/{eqLevelCap} ★{equipped.awakeningStars}";
                    _weaponEquippedText.color = gradeColor;
                }
                else
                {
                    _weaponEquippedText.text = "장착 무기: 없음";
                    _weaponEquippedText.color = TextTan;
                }
            }

            // 인벤토리 갱신
            if (_weaponInventoryContent == null) return;
            ClearChildren(_weaponInventoryContent);

            var sorted = WeaponManager.Instance.GetSortedInventory();
            if (_weaponCountText != null)
                _weaponCountText.text = $"보유: {sorted.Count}개";

            for (int i = 0; i < sorted.Count; i++)
            {
                CreateWeaponItem(sorted[i]);
            }
        }

        private void CreateWeaponItem(WeaponInstance instance)
        {
            var data = WeaponManager.Instance.GetData(instance.weaponId);
            if (data == null) return;

            bool isEquipped = instance.isEquipped;
            Color gradeColor = GetGradeColor(instance.grade);
            int finalAtk = data.GetFinalAtk(instance.grade, instance.level, instance.awakeningStars);

            // 행 컨테이너
            var row = new GameObject($"Weapon_{instance.instanceId[..8]}");
            row.transform.SetParent(_weaponInventoryContent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 88);

            var bg = row.AddComponent<Image>();
            bg.color = isEquipped ? RowBgEquipped : RowBg;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 6;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            // 등급 마커
            CreateGradeMarker(row.transform, gradeColor);

            // 아이콘
            if (data.icon != null)
                CreateIcon(row.transform, data.icon, 52);

            // 이름 + 등급 + 레벨 (레벨캡 표시)
            int dispLevelCap = WeaponManager.GetWeaponLevelCap(instance.awakeningStars);
            var sb = new StringBuilder();
            sb.Append($"{instance.grade} Lv{instance.level}/{dispLevelCap}");
            if (instance.awakeningStars > 0)
                sb.Append($" ★{instance.awakeningStars}");

            var infoObj = CreateInfoColumn(row.transform, data.displayName, gradeColor, sb.ToString());

            // 스탯
            var statSb = new StringBuilder();
            statSb.Append($"ATK {finalAtk}");
            float critRate = data.GetCritRate(instance.grade);
            if (critRate > 0) statSb.Append($"\nCrit {critRate:P0}");
            if (data.passiveAtkPercent > 0)
                statSb.Append($"\n보유효과 ATK+{data.passiveAtkPercent:F1}%");
            CreateStatText(row.transform, statSb.ToString(), 140);

            // 버튼 영역
            var btnArea = new GameObject("BtnArea");
            btnArea.transform.SetParent(row.transform, false);
            btnArea.AddComponent<RectTransform>().sizeDelta = new Vector2(90, 80);
            var btnVlg = btnArea.AddComponent<VerticalLayoutGroup>();
            btnVlg.childAlignment = TextAnchor.MiddleCenter;
            btnVlg.childControlWidth = true;
            btnVlg.childControlHeight = false;
            btnVlg.childForceExpandWidth = true;
            btnVlg.spacing = 2;

            string capturedId = instance.instanceId;

            // 장착/해제 버튼
            CreateActionButton(btnArea.transform, isEquipped ? "해제" : "장착",
                isEquipped ? BtnRed : BtnGreen, 24, () =>
                {
                    if (WeaponManager.Instance == null) return;
                    if (isEquipped)
                        WeaponManager.Instance.Unequip();
                    else
                        WeaponManager.Instance.Equip(capturedId);
                });

            // 레벨업 버튼
            bool canLevelUp = WeaponManager.Instance.CanLevelUp(capturedId);
            long goldCost = WeaponManager.Instance.GetLevelUpGoldCost(instance.level);
            long stoneCost = WeaponManager.Instance.GetLevelUpStoneCost(instance.level);
            CreateActionButton(btnArea.transform, $"레벨업",
                canLevelUp ? BtnBlue : BtnDisabled, 24, () =>
                {
                    if (WeaponManager.Instance == null) return;
                    WeaponManager.Instance.LevelUpWeapon(capturedId);
                });

            // 각성 버튼
            bool canAwaken = WeaponManager.Instance.CanAwaken(capturedId);
            int matCount = WeaponManager.Instance.GetAwakeningMaterialCount(capturedId);
            int levelCap = WeaponManager.GetWeaponLevelCap(instance.awakeningStars);
            CreateActionButton(btnArea.transform, $"각성 ({matCount}/4) Cap:{levelCap}",
                canAwaken ? new Color(0.5f, 0.35f, 0.1f, 1f) : BtnDisabled, 24, () =>
                {
                    if (WeaponManager.Instance == null) return;
                    WeaponManager.Instance.AwakenWeapon(capturedId);
                });

            // 승급 버튼
            string nextGrade = WeaponManager.GetNextGrade(instance.grade);
            if (nextGrade != null)
            {
                bool canPromote = WeaponManager.Instance.CanPromote(capturedId);
                int promoteMatCount = WeaponManager.Instance.GetPromoteMaterialCount(capturedId);
                CreateActionButton(btnArea.transform, $"승급→{nextGrade} ({promoteMatCount}/4)",
                    canPromote ? new Color(0.6f, 0.2f, 0.2f, 1f) : BtnDisabled, 24, () =>
                    {
                        if (WeaponManager.Instance == null) return;
                        WeaponManager.Instance.PromoteWeapon(capturedId);
                    });
            }

            // 비교 버튼 (UX-18: 무기 비교)
            if (!isEquipped)
            {
                string weaponCompareId = instance.instanceId;
                CreateActionButton(btnArea.transform, "비교", new Color(0.35f, 0.30f, 0.45f, 1f), 24, () =>
                {
                    ShowWeaponComparison(weaponCompareId);
                });
            }

            // 팝인 연출
            rt.localScale = Vector3.zero;
            rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        // ══════════════════════════════════
        //  탭 2: 강화
        // ══════════════════════════════════

        private void BuildEnhanceTab()
        {
            _enhanceTabRoot = new GameObject("EnhanceTab");
            _enhanceTabRoot.transform.SetParent(transform, false);
            var rootRT = _enhanceTabRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.sizeDelta = Vector2.zero;
            rootRT.anchoredPosition = Vector2.zero;

            var scrollObj = BuildScrollView(_enhanceTabRoot.transform, out _enhanceContent);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.sizeDelta = Vector2.zero;
            scrollRT.anchoredPosition = Vector2.zero;

            RefreshEnhanceTab();
        }

        private void RefreshEnhanceTab()
        {
            if (_enhanceTabRoot == null || !_enhanceTabRoot.activeSelf) return;
            if (_enhanceContent == null) return;
            if (EquipmentManager.Instance == null) return;

            ClearChildren(_enhanceContent);

            // 장비 선택이 없으면 목록 표시
            if (string.IsNullOrEmpty(_selectedEquipmentId))
            {
                BuildEnhanceSelectionList();
                return;
            }

            var inst = EquipmentManager.Instance.GetInstance(_selectedEquipmentId);
            if (inst == null)
            {
                _selectedEquipmentId = null;
                BuildEnhanceSelectionList();
                return;
            }

            var data = EquipmentManager.Instance.GetData(inst.equipmentId);
            if (data == null)
            {
                _selectedEquipmentId = null;
                BuildEnhanceSelectionList();
                return;
            }

            Color gradeColor = GetGradeColor(inst.grade);
            var enhSlot = EquipmentManager.Instance.GetSlotEnhancement(data.slot);

            // 헤더: 선택된 장비 정보
            BuildEnhanceHeader(inst, data, gradeColor, enhSlot);

            // 뒤로가기 버튼
            var backRow = new GameObject("BackRow");
            backRow.transform.SetParent(_enhanceContent, false);
            backRow.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            CreateActionButton(backRow.transform, "← 장비 선택", TabActive, 36, () =>
            {
                _selectedEquipmentId = null;
                RefreshEnhanceTab();
            });

            // 잠재능력 섹션
            BuildPotentialSection(data.slot, enhSlot);

            // 보조 잠재능력 섹션 (12성 이상)
            if (enhSlot.starForce >= 12)
                BuildSubPotentialSection(data.slot, enhSlot);
        }

        private void BuildEnhanceSelectionList()
        {
            // 제목
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_enhanceContent, false);
            titleObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            var titleTmp = AddTMP(titleObj.transform, "Text", "강화할 장비를 선택하세요",
                18, TextWhite, TextAlignmentOptions.Center);
            FullStretch(titleTmp.gameObject);

            var sorted = EquipmentManager.Instance.GetSortedInventory();
            for (int i = 0; i < sorted.Count; i++)
            {
                var instance = sorted[i];
                var eqData = EquipmentManager.Instance.GetData(instance.equipmentId);
                if (eqData == null) continue;

                Color gradeColor = GetGradeColor(instance.grade);
                string capturedId = instance.instanceId;

                var row = new GameObject($"Select_{instance.instanceId[..8]}");
                row.transform.SetParent(_enhanceContent, false);
                var rt = row.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 56);
                row.AddComponent<Image>().color = RowBg;

                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.spacing = 8;
                hlg.padding = new RectOffset(12, 12, 4, 4);

                CreateGradeMarker(row.transform, gradeColor);

                if (eqData.icon != null)
                    CreateIcon(row.transform, eqData.icon, 44);

                var selSlotEnh = EquipmentManager.Instance.GetSlotEnhancement(eqData.slot);
                var sb = new StringBuilder();
                sb.Append($"{instance.grade} · {GetSlotDisplayName(eqData.slot)}");
                if (selSlotEnh.scrollLevel > 0) sb.Append($" · 주문서+{selSlotEnh.scrollLevel}");
                if (selSlotEnh.starForce > 0) sb.Append($" · ★{selSlotEnh.starForce}");

                CreateInfoColumn(row.transform, eqData.displayName, gradeColor, sb.ToString());

                CreateActionButton(row.transform, "선택", BtnBlue, 44, () =>
                {
                    _selectedEquipmentId = capturedId;
                    RefreshEnhanceTab();
                });
            }
        }

        private void BuildEnhanceHeader(EquipmentInstance inst, EquipmentDataSO data, Color gradeColor, SlotEnhancementData slotEnh)
        {
            var headerRow = new GameObject("Header");
            headerRow.transform.SetParent(_enhanceContent, false);
            headerRow.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 64);
            headerRow.AddComponent<Image>().color = new Color(0.14f, 0.13f, 0.12f, 0.95f);

            var hlg = headerRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(12, 12, 4, 4);

            CreateGradeMarker(headerRow.transform, gradeColor);

            if (data.icon != null)
                CreateIcon(headerRow.transform, data.icon, 48);

            var sb = new StringBuilder();
            sb.Append($"{inst.grade} · {GetSlotDisplayName(data.slot)}");
            if (slotEnh.scrollLevel > 0) sb.Append($" · 주문서+{slotEnh.scrollLevel}");
            if (slotEnh.starForce > 0) sb.Append($" · ★{slotEnh.starForce}");
            if (slotEnh.potentialGrade > PotentialGrade.None) sb.Append($" · 잠재:{slotEnh.potentialGrade}");

            CreateInfoColumn(headerRow.transform, data.displayName, gradeColor, sb.ToString());
        }

        private void BuildPotentialSection(EquipmentSlot slot, SlotEnhancementData slotEnh)
        {
            BuildSectionTitle("잠재능력");

            var row = new GameObject("Potential");
            row.transform.SetParent(_enhanceContent, false);
            row.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 90);
            row.AddComponent<Image>().color = new Color(0.14f, 0.13f, 0.12f, 0.9f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(12, 12, 4, 4);

            var infoSb = new StringBuilder();
            infoSb.AppendLine($"등급: {(slotEnh.potentialGrade == PotentialGrade.None ? "없음" : slotEnh.potentialGrade.ToString())}");

            if (slotEnh.potentialOptions != null && slotEnh.potentialOptions.Count > 0)
            {
                for (int i = 0; i < slotEnh.potentialOptions.Count; i++)
                    infoSb.AppendLine($"  {slotEnh.potentialOptions[i]}");
            }

            if (PotentialSystem.Instance != null)
            {
                int pity = PotentialSystem.Instance.GetPityCount(slot);
                int ceiling = PotentialSystem.Instance.GetPityCeiling(slotEnh.potentialGrade);
                if (ceiling > 0)
                    infoSb.Append($"천장: {pity}/{ceiling}");

                long goldCost = PotentialSystem.Instance.GetPotentialChangeCost(slotEnh);
                infoSb.Append($" · 비용: {goldCost:N0}G + 잠재석1");
            }

            CreateStatText(row.transform, infoSb.ToString(), 320);

            bool canChange = PotentialSystem.Instance != null
                && PotentialSystem.Instance.CanChangePotential(slot);
            EquipmentSlot capturedSlot = slot;

            string btnText = slotEnh.potentialGrade >= PotentialGrade.Mythic ? "최대" : "변경";

            CreateActionButton(row.transform, btnText,
                slotEnh.potentialGrade >= PotentialGrade.Mythic ? BtnDisabled : (canChange ? BtnGreen : BtnDisabled),
                56, () =>
                {
                    if (PotentialSystem.Instance == null) return;
                    PotentialSystem.Instance.ChangePotential(capturedSlot);
                    RefreshEnhanceTab();
                });
        }

        private void BuildSubPotentialSection(EquipmentSlot slot, SlotEnhancementData slotEnh)
        {
            BuildSectionTitle("보조 잠재능력 (12성+)");

            var row = new GameObject("SubPotential");
            row.transform.SetParent(_enhanceContent, false);
            row.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 90);
            row.AddComponent<Image>().color = new Color(0.14f, 0.13f, 0.12f, 0.9f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(12, 12, 4, 4);

            var infoSb = new StringBuilder();
            infoSb.AppendLine($"보조 등급: {(slotEnh.subPotentialGrade == PotentialGrade.None ? "없음" : slotEnh.subPotentialGrade.ToString())}");

            if (slotEnh.subPotentialOptions != null && slotEnh.subPotentialOptions.Count > 0)
            {
                for (int i = 0; i < slotEnh.subPotentialOptions.Count; i++)
                    infoSb.AppendLine($"  {slotEnh.subPotentialOptions[i]}");
            }

            if (PotentialSystem.Instance != null)
            {
                int pity = PotentialSystem.Instance.GetSubPityCount(slot);
                int ceiling = PotentialSystem.Instance.GetPityCeiling(slotEnh.subPotentialGrade);
                if (ceiling > 0)
                    infoSb.Append($"천장: {pity}/{ceiling}");

                long goldCost = PotentialSystem.Instance.GetSubPotentialChangeCost(slotEnh);
                infoSb.Append($" · 비용: {goldCost:N0}G + 고급잠재석1");
            }

            CreateStatText(row.transform, infoSb.ToString(), 320);

            bool canChange = PotentialSystem.Instance != null
                && PotentialSystem.Instance.CanChangeSubPotential(slot);
            EquipmentSlot capturedSlot = slot;

            string btnText = slotEnh.subPotentialGrade >= PotentialGrade.Mythic ? "최대" : "변경";

            CreateActionButton(row.transform, btnText,
                slotEnh.subPotentialGrade >= PotentialGrade.Mythic ? BtnDisabled : (canChange ? BtnGreen : BtnDisabled),
                56, () =>
                {
                    if (PotentialSystem.Instance == null) return;
                    PotentialSystem.Instance.ChangeSubPotential(capturedSlot);
                    RefreshEnhanceTab();
                });
        }

        // ══════════════════════════════════
        //  공용 UI 빌더 유틸
        // ══════════════════════════════════

        private void BuildSectionTitle(string title)
        {
            var titleObj = new GameObject($"Section_{title}");
            titleObj.transform.SetParent(_enhanceContent, false);
            titleObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            titleObj.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.15f, 0.8f);

            var tmp = AddTMP(titleObj.transform, "Label", title, 15, TextGold, TextAlignmentOptions.Left);
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.anchorMin = Vector2.zero;
            tmpRT.anchorMax = Vector2.one;
            tmpRT.sizeDelta = Vector2.zero;
            tmpRT.anchoredPosition = Vector2.zero;
            tmpRT.offsetMin = new Vector2(12, 0);
        }

        private static void CreateGradeMarker(Transform parent, Color color)
        {
            var marker = new GameObject("Marker");
            marker.transform.SetParent(parent, false);
            marker.AddComponent<RectTransform>().sizeDelta = new Vector2(6, 56);
            marker.AddComponent<Image>().color = color;
        }

        private static void CreateIcon(Transform parent, Sprite sprite, float size)
        {
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(parent, false);
            iconObj.AddComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = sprite;
            iconImg.preserveAspect = true;
        }

        private static GameObject CreateInfoColumn(Transform parent, string name, Color nameColor, string subText)
        {
            var infoObj = new GameObject("Info");
            infoObj.transform.SetParent(parent, false);
            infoObj.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 60);
            var infoVlg = infoObj.AddComponent<VerticalLayoutGroup>();
            infoVlg.childAlignment = TextAnchor.MiddleLeft;
            infoVlg.childControlWidth = true;
            infoVlg.childControlHeight = true;
            infoVlg.childForceExpandWidth = true;
            infoVlg.childForceExpandHeight = true;
            infoVlg.spacing = 0;

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(infoObj.transform, false);
            nameObj.AddComponent<RectTransform>();
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = name;
            nameTmp.fontSize = 18;
            nameTmp.color = nameColor;
            nameTmp.fontStyle = FontStyles.Bold;

            var subObj = new GameObject("Sub");
            subObj.transform.SetParent(infoObj.transform, false);
            subObj.AddComponent<RectTransform>();
            var subTmp = subObj.AddComponent<TextMeshProUGUI>();
            subTmp.text = subText;
            subTmp.fontSize = 13;
            subTmp.color = TextTan;

            return infoObj;
        }

        private static void CreateStatText(Transform parent, string text, float width)
        {
            var statsObj = new GameObject("Stats");
            statsObj.transform.SetParent(parent, false);
            statsObj.AddComponent<RectTransform>().sizeDelta = new Vector2(width, 60);
            var statsTmp = statsObj.AddComponent<TextMeshProUGUI>();
            statsTmp.fontSize = 13;
            statsTmp.color = TextTan;
            statsTmp.alignment = TextAlignmentOptions.Left;
            statsTmp.text = text;
        }

        private static void CreateActionButton(Transform parent, string label, Color bgColor,
            float height, System.Action onClick)
        {
            var btnObj = new GameObject("Btn");
            btnObj.transform.SetParent(parent, false);
            btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(80, height);
            var btnBg = btnObj.AddComponent<Image>();
            btnBg.color = bgColor;
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnObj.transform, false);
            var btnRT = btnLabel.AddComponent<RectTransform>();
            btnRT.anchorMin = Vector2.zero;
            btnRT.anchorMax = Vector2.one;
            btnRT.sizeDelta = Vector2.zero;
            btnRT.anchoredPosition = Vector2.zero;
            var btnTmp = btnLabel.AddComponent<TextMeshProUGUI>();
            btnTmp.text = label;
            btnTmp.fontSize = 14;
            btnTmp.color = TextWhite;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontStyle = FontStyles.Bold;

            btn.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                // 펀치 피드백
                btnObj.transform.DOKill();
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f)
                    .SetUpdate(true).SetLink(btnObj);
            });
        }

        private static TextMeshProUGUI AddTMP(Transform parent, string name, string text,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            return tmp;
        }

        private static GameObject BuildScrollView(Transform parent, out Transform contentTransform)
        {
            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(parent, false);
            var scrollRT = scrollObj.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.sizeDelta = Vector2.zero;

            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.11f, 0.10f, 0.09f, 0.8f);
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            FullStretch(viewport);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewport.transform, false);
            var contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0, 0);

            var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.spacing = 4;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRT;
            contentTransform = contentObj.transform;

            return scrollObj;
        }

        // ── 유틸 ──

        private static Color GetGradeColor(string grade)
        {
            // UIThemeManager의 통합 등급 색상 우선 사용
            if (UIThemeManager.Instance != null)
                return UIThemeManager.Instance.GetGradeColor(grade);
            return GradeColors.TryGetValue(grade, out var color) ? color : Color.white;
        }

        private static string GetSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Weapon => "무기",
                EquipmentSlot.Helmet => "투구",
                EquipmentSlot.Top => "상의",
                EquipmentSlot.Gloves => "장갑",
                EquipmentSlot.Boots => "신발",
                EquipmentSlot.Ring => "반지",
                EquipmentSlot.Necklace => "목걸이",
                EquipmentSlot.FaceAccessory => "얼굴장식",
                _ => slot.ToString()
            };
        }

        /// <summary>
        /// EquipmentSlot → IconRegistry.EquipSlot 변환 후 부위별 아이콘을 반환한다.
        /// </summary>
        private static Sprite GetEquipSlotFallbackIcon(EquipmentSlot slot)
        {
            if (IconRegistry.Instance == null) return null;

            var iconSlot = slot switch
            {
                EquipmentSlot.Weapon => EquipSlot.Weapon,
                EquipmentSlot.Helmet => EquipSlot.Helmet,
                EquipmentSlot.Top => EquipSlot.Armor,
                EquipmentSlot.Gloves => EquipSlot.Gloves,
                EquipmentSlot.Boots => EquipSlot.Boots,
                EquipmentSlot.Ring => EquipSlot.Ring,
                EquipmentSlot.Necklace => EquipSlot.Necklace,
                EquipmentSlot.FaceAccessory => EquipSlot.Earring,
                _ => EquipSlot.Weapon,
            };

            return IconRegistry.Instance.GetEquipSlotIcon(iconSlot);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void FullStretch(GameObject obj)
        {
            var r = obj.GetComponent<RectTransform>();
            if (r == null) r = obj.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            r.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            // DOTween 정리
            if (_weaponTabRoot != null)
                _weaponTabRoot.transform.DOKill();
            if (_enhanceTabRoot != null)
                _enhanceTabRoot.transform.DOKill();
        }

        // ── UX-17: 장비 비교 ──

        private void ShowEquipmentComparison(string instanceId, EquipmentSlot slot)
        {
            if (EquipmentManager.Instance == null) return;

            var newInst = EquipmentManager.Instance.GetInstance(instanceId);
            if (newInst == null) return;

            var newData = EquipmentManager.Instance.GetData(newInst.equipmentId);
            if (newData == null) return;

            // 현재 장착 장비
            var curInst = EquipmentManager.Instance.GetEquipped(slot);
            int curAtk = 0, curHp = 0, curDef = 0;
            if (curInst != null)
            {
                var curData = EquipmentManager.Instance.GetData(curInst.equipmentId);
                if (curData != null)
                {
                    curAtk = curData.GetAtk(curInst.grade);
                    curHp = curData.GetHp(curInst.grade);
                    curDef = curData.GetDef(curInst.grade);
                }
            }

            int newAtk = newData.GetAtk(newInst.grade);
            int newHp = newData.GetHp(newInst.grade);
            int newDef = newData.GetDef(newInst.grade);

            var deltas = new StatDelta[]
            {
                new() { StatName = "공격력", Before = curAtk, After = newAtk },
                new() { StatName = "체력", Before = curHp, After = newHp },
                new() { StatName = "방어력", Before = curDef, After = newDef }
            };

            long cpBefore = CombatFormula.CalculateCP(curAtk, curDef, curHp, 0f);
            long cpAfter = CombatFormula.CalculateCP(newAtk, newDef, newHp, 0f);

            var view = FindFirstObjectByType<StatCompareView>();
            if (view != null)
            {
                view.Show(deltas, cpBefore, cpAfter, "장비 비교");
            }
        }

        // ── UX-18: 무기 비교 ──

        private void ShowWeaponComparison(string instanceId)
        {
            if (WeaponManager.Instance == null) return;

            var newInst = WeaponManager.Instance.GetInstance(instanceId);
            if (newInst == null) return;

            var newData = WeaponManager.Instance.GetData(newInst.weaponId);
            if (newData == null) return;

            int newAtk = newData.GetAtk(newInst.grade, newInst.level);

            // 현재 장착 무기
            var curInst = WeaponManager.Instance.EquippedWeapon;
            int curAtk = 0;
            if (curInst != null)
            {
                var curData = WeaponManager.Instance.GetData(curInst.weaponId);
                if (curData != null)
                    curAtk = curData.GetAtk(curInst.grade, curInst.level);
            }

            var deltas = new StatDelta[]
            {
                new() { StatName = "공격력", Before = curAtk, After = newAtk }
            };

            long cpBefore = CombatFormula.CalculateCP(curAtk, 0, 0, 0f);
            long cpAfter = CombatFormula.CalculateCP(newAtk, 0, 0, 0f);

            var view = FindFirstObjectByType<StatCompareView>();
            if (view != null)
            {
                view.Show(deltas, cpBefore, cpAfter, "무기 비교");
            }
        }

        // ── UX-25: 추천 장착 ──

        private void OnRecommendEquipClicked()
        {
            if (RecommendationManager.Instance == null) return;

            int count = RecommendationManager.Instance.AutoEquipRecommended();
            if (_recommendResultText != null)
            {
                _recommendResultText.text = count > 0
                    ? $"추천 장비 {count}개 장착 완료!"
                    : "이미 최적 장비를 장착 중입니다.";
            }

            RefreshCurrentTab();
        }

        private void EnsureComponents()
        {
            // CharacterPanel의 서브탭으로 동작하는지 확인
            bool isEmbedded = GetComponentInParent<CharacterPanel>() != null;

            if (isEmbedded)
            {
                // 임베디드 모드: 자체 Canvas/오버레이 불필요
                var myCanvas = GetComponent<Canvas>();
                if (myCanvas != null) myCanvas.enabled = false;
                var overlayBg = GetComponent<Image>();
                if (overlayBg != null) overlayBg.color = Color.clear;
            }
            else
            {
                // 독립 모드: 자체 Canvas + 오버레이
                var myCanvas = GetComponent<Canvas>();
                if (myCanvas == null)
                {
                    myCanvas = gameObject.AddComponent<Canvas>();
                    gameObject.AddComponent<GraphicRaycaster>();
                }
                myCanvas.overrideSorting = true;
                myCanvas.sortingOrder = 100;

                var myRt = GetComponent<RectTransform>();
                if (myRt != null) SetStretch(myRt);

                var overlayBg = GetComponent<Image>();
                if (overlayBg == null) overlayBg = gameObject.AddComponent<Image>();
                overlayBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
            }

            // ── 내부 콘텐츠 패널 ──
            var contentPanel = EnsureChild(transform, "ContentPanel");
            var contentRt = contentPanel.GetComponent<RectTransform>();
            if (isEmbedded)
            {
                // 임베디드: 부모 공간 전체 사용
                contentRt.anchorMin = Vector2.zero;
                contentRt.anchorMax = Vector2.one;
            }
            else
            {
                contentRt.anchorMin = new Vector2(0.05f, 0.05f);
                contentRt.anchorMax = new Vector2(0.95f, 0.95f);
            }
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var contentBg = EnsureComp<Image>(contentPanel);
            var tm = UIThemeManager.Instance;
            if (tm != null) tm.ApplyPanelBackground(contentBg);
            else contentBg.color = new Color(0.1f, 0.1f, 0.18f, 0.95f);

            // ── 타이틀 / 닫기 버튼 — CharacterPanel Header에서 이미 표시하므로 비활성화 ──
            var titleGo = contentPanel.transform.Find("TitleText");
            if (titleGo != null) titleGo.gameObject.SetActive(false);
            var closeGo = contentPanel.transform.Find("CloseButton");
            if (closeGo != null) closeGo.gameObject.SetActive(false);

            // ── 서브탭 버튼 행 (타이틀 아래) ──
            var tabRow = EnsureChild(contentPanel.transform, "TabRow");
            var tabRowRt = tabRow.GetComponent<RectTransform>();
            tabRowRt.anchorMin = new Vector2(0f, 1f);
            tabRowRt.anchorMax = new Vector2(1f, 1f);
            tabRowRt.pivot = new Vector2(0.5f, 1f);
            tabRowRt.anchoredPosition = new Vector2(0f, -5f);
            tabRowRt.sizeDelta = new Vector2(-20f, 44f);
            var tabHlg = EnsureComp<HorizontalLayoutGroup>(tabRow);
            tabHlg.spacing = 4f;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.padding = new RectOffset(4, 4, 2, 2);

            string[] tabNames = { "장비", "무기", "강화" };
            Button[] tabBtns = { _tabEquipmentBtn, _tabWeaponBtn, _tabEnhanceBtn };
            Color[] tabColors = { TabActive, TabInactive, TabInactive };

            for (int i = 0; i < 3; i++)
            {
                if (tabBtns[i] == null)
                {
                    var btnGo = EnsureChild(tabRow.transform, $"Tab{tabNames[i]}Btn");
                    var tabBgImg = EnsureComp<Image>(btnGo);
                    if (tm != null)
                    {
                        var (tabN, tabS) = tm.GetTabSprites();
                        UIThemeManager.ApplySpriteOrColor(tabBgImg, i == 0 ? tabS : tabN, tabColors[i]);
                    }
                    else tabBgImg.color = tabColors[i];
                    tabBtns[i] = EnsureComp<Button>(btnGo);
                    var lblGo = EnsureChild(btnGo.transform, "Label");
                    SetStretch(lblGo.GetComponent<RectTransform>());
                    var lblTmp = EnsureComp<TextMeshProUGUI>(lblGo);
                    lblTmp.text = tabNames[i];
                    lblTmp.fontSize = 16f;
                    lblTmp.fontStyle = FontStyles.Bold;
                    lblTmp.color = Color.white;
                    lblTmp.alignment = TextAlignmentOptions.Center;
                }
            }
            _tabEquipmentBtn = tabBtns[0];
            _tabWeaponBtn = tabBtns[1];
            _tabEnhanceBtn = tabBtns[2];

            // ── 콘텐츠 영역 (탭 아래 나머지 공간) ──
            var contentArea = EnsureChild(contentPanel.transform, "ContentArea");
            var contentAreaRt = contentArea.GetComponent<RectTransform>();
            contentAreaRt.anchorMin = new Vector2(0f, 0f);
            contentAreaRt.anchorMax = new Vector2(1f, 1f);
            contentAreaRt.offsetMin = new Vector2(10f, 10f);
            contentAreaRt.offsetMax = new Vector2(-10f, -55f);

            // ═══════════════════════════════════
            // 장비 탭 콘텐츠
            // ═══════════════════════════════════
            if (_equipmentTabContent == null)
            {
                _equipmentTabContent = EnsureChild(contentArea.transform, "EquipmentTabContent");
                SetStretch(_equipmentTabContent.GetComponent<RectTransform>());
            }

            var equipVlg = EnsureComp<VerticalLayoutGroup>(_equipmentTabContent);
            equipVlg.spacing = 6f;
            equipVlg.childForceExpandWidth = true;
            equipVlg.childForceExpandHeight = false;
            equipVlg.padding = new RectOffset(6, 6, 6, 6);

            // ── 보너스 스탯 표시 행 ──
            var bonusRow = EnsureChild(_equipmentTabContent.transform, "BonusStatRow");
            bonusRow.AddComponent<LayoutElement>().preferredHeight = 30f;
            var bonusBg = EnsureComp<Image>(bonusRow);
            if (tm != null && tm.FrameBackground != null)
            {
                bonusBg.sprite = tm.FrameBackground;
                bonusBg.type = Image.Type.Sliced;
                bonusBg.color = new Color(0.8f, 0.8f, 0.75f, 1f);
            }
            else bonusBg.color = new Color(0.12f, 0.14f, 0.22f, 0.9f);
            var bonusHlg = EnsureComp<HorizontalLayoutGroup>(bonusRow);
            bonusHlg.spacing = 8f;
            bonusHlg.childForceExpandWidth = true;
            bonusHlg.childForceExpandHeight = true;
            bonusHlg.padding = new RectOffset(10, 10, 2, 2);

            if (_bonusAtkText == null)
            {
                var go = EnsureChild(bonusRow.transform, "BonusAtkText");
                _bonusAtkText = EnsureComp<TextMeshProUGUI>(go);
                _bonusAtkText.fontSize = 14f;
                _bonusAtkText.color = new Color(1f, 0.6f, 0.5f);
                _bonusAtkText.text = "ATK +0";
                _bonusAtkText.alignment = TextAlignmentOptions.Center;
            }
            if (_bonusHpText == null)
            {
                var go = EnsureChild(bonusRow.transform, "BonusHpText");
                _bonusHpText = EnsureComp<TextMeshProUGUI>(go);
                _bonusHpText.fontSize = 14f;
                _bonusHpText.color = new Color(0.5f, 1f, 0.6f);
                _bonusHpText.text = "HP +0";
                _bonusHpText.alignment = TextAlignmentOptions.Center;
            }
            if (_bonusDefText == null)
            {
                var go = EnsureChild(bonusRow.transform, "BonusDefText");
                _bonusDefText = EnsureComp<TextMeshProUGUI>(go);
                _bonusDefText.fontSize = 14f;
                _bonusDefText.color = new Color(0.6f, 0.7f, 1f);
                _bonusDefText.text = "DEF +0";
                _bonusDefText.alignment = TextAlignmentOptions.Center;
            }

            // ── 장비 슬롯 그리드 (4열 x 3행) ──
            var slotGrid = EnsureChild(_equipmentTabContent.transform, "SlotGrid");
            var slotGridLe = slotGrid.AddComponent<LayoutElement>();
            slotGridLe.preferredHeight = 240f;
            var gridLayout = EnsureComp<GridLayoutGroup>(slotGrid);
            gridLayout.cellSize = new Vector2(0f, 72f);
            gridLayout.spacing = new Vector2(4f, 4f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.padding = new RectOffset(4, 4, 4, 4);
            // 셀 너비를 부모에 맞추기 위해 ContentSizeFitter 대신 수동 계산
            // GridLayout에서 flexible width는 지원 안 되므로 적절한 고정 크기 설정
            gridLayout.cellSize = new Vector2(90f, 72f);

            if (_slotUIs == null || _slotUIs.Length == 0)
            {
                string[] slotLabels = { "투구", "갑옷", "장갑", "신발", "목걸이", "반지", "귀걸이", "벨트", "망토", "견갑", "팔찌", "문장" };
                _slotUIs = new EquipmentSlotUI[12];
                for (int i = 0; i < 12; i++)
                {
                    var slotGo = EnsureChild(slotGrid.transform, $"EquipSlot_{i}");
                    var slotBg = EnsureComp<Image>(slotGo);
                    if (tm != null) tm.ApplyFrameBackground(slotBg);
                    else slotBg.color = new Color(0.13f, 0.13f, 0.2f, 0.85f);

                    // 등급 마커 (좌상단 컬러 바)
                    var markerGo = EnsureChild(slotGo.transform, "Marker");
                    var markerRt = markerGo.GetComponent<RectTransform>();
                    markerRt.anchorMin = new Vector2(0f, 1f);
                    markerRt.anchorMax = new Vector2(1f, 1f);
                    markerRt.pivot = new Vector2(0.5f, 1f);
                    markerRt.anchoredPosition = Vector2.zero;
                    markerRt.sizeDelta = new Vector2(0f, 4f);

                    // 아이콘 (중앙 상단)
                    var iconGo = EnsureChild(slotGo.transform, "Icon");
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.5f, 1f);
                    iconRt.anchorMax = new Vector2(0.5f, 1f);
                    iconRt.pivot = new Vector2(0.5f, 1f);
                    iconRt.anchoredPosition = new Vector2(0f, -6f);
                    iconRt.sizeDelta = new Vector2(32f, 32f);

                    // 이름 텍스트 (아이콘 아래)
                    var nameGo = EnsureChild(slotGo.transform, "Name");
                    var nameRt = nameGo.GetComponent<RectTransform>();
                    nameRt.anchorMin = new Vector2(0f, 0f);
                    nameRt.anchorMax = new Vector2(1f, 0.45f);
                    nameRt.offsetMin = new Vector2(2f, 14f);
                    nameRt.offsetMax = new Vector2(-2f, 0f);

                    // 등급 텍스트 (하단)
                    var gradeGo = EnsureChild(slotGo.transform, "Grade");
                    var gradeRt = gradeGo.GetComponent<RectTransform>();
                    gradeRt.anchorMin = new Vector2(0f, 0f);
                    gradeRt.anchorMax = new Vector2(1f, 0f);
                    gradeRt.pivot = new Vector2(0.5f, 0f);
                    gradeRt.anchoredPosition = new Vector2(0f, 2f);
                    gradeRt.sizeDelta = new Vector2(0f, 14f);

                    // 비어있음 텍스트 (중앙)
                    var emptyGo = EnsureChild(slotGo.transform, "Empty");
                    SetStretch(emptyGo.GetComponent<RectTransform>());

                    // 해제 버튼 (우상단 작은 X)
                    var btnGo = EnsureChild(slotGo.transform, "UnequipBtn");
                    var btnRt = btnGo.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(1f, 1f);
                    btnRt.anchorMax = new Vector2(1f, 1f);
                    btnRt.pivot = new Vector2(1f, 1f);
                    btnRt.anchoredPosition = new Vector2(-1f, -1f);
                    btnRt.sizeDelta = new Vector2(18f, 18f);
                    var unequipBg = EnsureComp<Image>(btnGo);
                    if (tm != null) tm.ApplyCloseButton(unequipBg);
                    else unequipBg.color = BtnRed;

                    _slotUIs[i] = new EquipmentSlotUI
                    {
                        gradeMarker = EnsureComp<Image>(markerGo),
                        iconImage = EnsureComp<Image>(iconGo),
                        nameText = EnsureComp<TextMeshProUGUI>(nameGo),
                        gradeText = EnsureComp<TextMeshProUGUI>(gradeGo),
                        emptyText = EnsureComp<TextMeshProUGUI>(emptyGo),
                        unequipBtn = EnsureComp<Button>(btnGo)
                    };
                    _slotUIs[i].gradeMarker.color = new Color(0.3f, 0.3f, 0.3f);
                    _slotUIs[i].iconImage.color = new Color(1f, 1f, 1f, 0.5f);
                    _slotUIs[i].nameText.fontSize = 12f;
                    _slotUIs[i].nameText.color = TextWhite;
                    _slotUIs[i].nameText.alignment = TextAlignmentOptions.Center;
                    _slotUIs[i].gradeText.fontSize = 11f;
                    _slotUIs[i].gradeText.color = TextTan;
                    _slotUIs[i].gradeText.alignment = TextAlignmentOptions.Center;
                    _slotUIs[i].emptyText.fontSize = 12f;
                    _slotUIs[i].emptyText.color = TextTan;
                    _slotUIs[i].emptyText.text = slotLabels[i];
                    _slotUIs[i].emptyText.alignment = TextAlignmentOptions.Center;
                }
            }

            // ── 추천 장착 + 인벤토리 카운트 행 ──
            var actionRow = EnsureChild(_equipmentTabContent.transform, "ActionRow");
            actionRow.AddComponent<LayoutElement>().preferredHeight = 36f;
            var actionBg = EnsureComp<Image>(actionRow);
            if (tm != null && tm.FrameBackground != null)
            {
                actionBg.sprite = tm.FrameBackground;
                actionBg.type = Image.Type.Sliced;
                actionBg.color = new Color(0.7f, 0.7f, 0.65f, 1f);
            }
            else actionBg.color = new Color(0.1f, 0.1f, 0.16f, 0.8f);
            var actionHlg = EnsureComp<HorizontalLayoutGroup>(actionRow);
            actionHlg.spacing = 8f;
            actionHlg.childForceExpandWidth = false;
            actionHlg.childForceExpandHeight = true;
            actionHlg.padding = new RectOffset(8, 8, 3, 3);
            actionHlg.childAlignment = TextAnchor.MiddleLeft;

            if (_recommendEquipButton == null)
            {
                var go = EnsureChild(actionRow.transform, "RecommendEquipBtn");
                go.AddComponent<LayoutElement>().preferredWidth = 100f;
                var recEqBg = EnsureComp<Image>(go);
                _recommendEquipButton = EnsureComp<Button>(go);
                if (tm != null) tm.ApplyButtonFull(_recommendEquipButton);
                else recEqBg.color = BtnBlue;
                var lbl = EnsureChild(go.transform, "Label");
                SetStretch(lbl.GetComponent<RectTransform>());
                var lblTmp = EnsureComp<TextMeshProUGUI>(lbl);
                lblTmp.text = "추천 장착";
                lblTmp.fontSize = 14f;
                lblTmp.fontStyle = FontStyles.Bold;
                lblTmp.color = Color.white;
                lblTmp.alignment = TextAlignmentOptions.Center;
            }
            if (_recommendResultText == null)
            {
                var go = EnsureChild(actionRow.transform, "RecommendResultText");
                go.AddComponent<LayoutElement>().flexibleWidth = 1f;
                _recommendResultText = EnsureComp<TextMeshProUGUI>(go);
                _recommendResultText.fontSize = 13f;
                _recommendResultText.color = new Color(0.75f, 0.75f, 0.75f);
                _recommendResultText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (_inventoryCountText == null)
            {
                var go = EnsureChild(actionRow.transform, "InventoryCountText");
                go.AddComponent<LayoutElement>().preferredWidth = 80f;
                _inventoryCountText = EnsureComp<TextMeshProUGUI>(go);
                _inventoryCountText.fontSize = 14f;
                _inventoryCountText.color = TextTan;
                _inventoryCountText.text = "0/100";
                _inventoryCountText.alignment = TextAlignmentOptions.MidlineRight;
            }

            // ── 인벤토리 스크롤 영역 ──
            if (_inventoryContent == null)
            {
                var scrollArea = EnsureChild(_equipmentTabContent.transform, "InventoryScrollArea");
                var scrollLe = scrollArea.AddComponent<LayoutElement>();
                scrollLe.flexibleHeight = 1f;
                var scrollBg = EnsureComp<Image>(scrollArea);
                if (tm != null && tm.FrameBackground != null)
                {
                    scrollBg.sprite = tm.FrameBackground;
                    scrollBg.type = Image.Type.Sliced;
                    scrollBg.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                else scrollBg.color = new Color(0.08f, 0.08f, 0.14f, 0.8f);

                var invContent = EnsureChild(scrollArea.transform, "InventoryContent");
                SetStretch(invContent.GetComponent<RectTransform>());
                var invVlg = EnsureComp<VerticalLayoutGroup>(invContent);
                invVlg.spacing = 3f;
                invVlg.childForceExpandWidth = true;
                invVlg.childForceExpandHeight = false;
                invVlg.padding = new RectOffset(4, 4, 4, 4);
                _inventoryContent = invContent.transform;
            }
        }

        private GameObject EnsureChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private T EnsureComp<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }
    }

    /// <summary>
    /// 장비 슬롯 UI 데이터.
    /// </summary>
    [Serializable]
    public struct EquipmentSlotUI
    {
        public Image gradeMarker;
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI gradeText;
        public TextMeshProUGUI emptyText;
        public Button unequipBtn;
    }
}
