using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Equipment;
using MkLike.Utils;
using EquipmentInstance = MkLike.Core.EquipmentInstance;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 장비 탭 컨트롤러.
    /// 서브탭 3개: 장비(슬롯+인벤토리) / 무기 / 강화.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EquipmentTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 서브탭 (장비/강화 — 무기는 독립 탭으로 분리) ──
        private VisualElement _subtabEquipment;
        private VisualElement _subtabEnhance;
        private VisualElement _tabContentEquipment;
        private VisualElement _tabContentEnhance;
        private int _currentSubTab; // 0=장비, 1=강화

        // ── 장비 탭 요소 ──
        private Label _bonusAtk;
        private Label _bonusHp;
        private Label _bonusDef;
        private Label _inventoryCount;
        private Label _cpLabel;
        private VisualElement _equipmentInventory;

        // 슬롯 캐싱 (EquipmentSlot 8종)
        private const int SLOT_COUNT = 8;
        private VisualElement[] _slotMarkers;
        private VisualElement[] _slotIcons;
        private Label[] _slotNames;
        private Label[] _slotGrades;
        private Label[] _slotEmpties;

        // ── 무기 탭 요소 ──
        private Label _weaponEquippedText;
        private Label _weaponCount;
        private VisualElement _weaponInventory;

        // ── 강화 탭 요소 ──
        private VisualElement _enhanceContent;
        private string _selectedEquipmentId;

        // ── 슬롯 인벤토리 팝업 ──
        private PopupEquipSlotInventory _slotPopup;

        // ── 등급별 색상 ──
        private static readonly Dictionary<string, Color> GradeColors = new()
        {
            { "Normal", new Color(0.7f, 0.7f, 0.7f) },
            { "Rare", new Color(0.3f, 0.6f, 1f) },
            { "Epic", new Color(0.7f, 0.3f, 0.9f) },
            { "Unique", new Color(1f, 0.85f, 0.1f) },
            { "Legendary", new Color(1f, 0.5f, 0.1f) },
            { "Mythic", new Color(1f, 0.2f, 0.3f) },
        };

        // ── EquipmentSlot 순서 → 슬롯 인덱스 매핑 ──
        // EquipmentSlot enum: Weapon=0, Helmet=1, Top=2, Gloves=3,
        //   Boots=4, Ring=5, Necklace=6, FaceAccessory=7
        // UXML 슬롯 순서: 0=Helmet, 1=Top, 2=Gloves, 3=Boots,
        //   4=Ring, 5=Necklace, 6=FaceAccessory, 7=Weapon
        private static readonly EquipmentSlot[] SLOT_ORDER =
        {
            EquipmentSlot.Helmet,
            EquipmentSlot.Top,
            EquipmentSlot.Gloves,
            EquipmentSlot.Boots,
            EquipmentSlot.Ring,
            EquipmentSlot.Necklace,
            EquipmentSlot.FaceAccessory,
            EquipmentSlot.Weapon,
        };

        // ═══════════════════════════════════
        //  라이프사이클
        // ═══════════════════════════════════

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 51; // 탭별 고유값
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[EquipmentTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindSubTabs();
            BindButtons();

            EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Subscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<WeaponChangedEvent>(OnWeaponChanged);
            EventBus.Subscribe<WeaponInventoryChangedEvent>(OnWeaponInventoryChanged);
            EventBus.Subscribe<WeaponLevelUpEvent>(OnWeaponLevelUp);
            EventBus.Subscribe<WeaponAwakeningEvent>(OnWeaponAwakening);
            EventBus.Subscribe<WeaponPromoteEvent>(OnWeaponPromote);

            SwitchSubTab(0);

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            EventBus.Unsubscribe<EquipmentInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<WeaponChangedEvent>(OnWeaponChanged);
            EventBus.Unsubscribe<WeaponInventoryChangedEvent>(OnWeaponInventoryChanged);
            EventBus.Unsubscribe<WeaponLevelUpEvent>(OnWeaponLevelUp);
            EventBus.Unsubscribe<WeaponAwakeningEvent>(OnWeaponAwakening);
            EventBus.Unsubscribe<WeaponPromoteEvent>(OnWeaponPromote);
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ═══════════════════════════════════
        //  요소 캐싱
        // ═══════════════════════════════════

        private void CacheElements()
        {
            // 서브탭 (장비/강화 — 무기는 독립 탭)
            _subtabEquipment = _root.Q<VisualElement>("subtab-equipment");
            _subtabEnhance = _root.Q<VisualElement>("subtab-enhance");
            _tabContentEquipment = _root.Q<VisualElement>("tab-content-equipment");
            _tabContentEnhance = _root.Q<VisualElement>("tab-content-enhance");

            // 장비 탭
            _bonusAtk = _root.Q<Label>("bonus-atk");
            _bonusHp = _root.Q<Label>("bonus-hp");
            _bonusDef = _root.Q<Label>("bonus-def");
            _inventoryCount = _root.Q<Label>("inventory-count");
            _cpLabel = _root.Q<Label>("cp-label");

            // 강화 버튼
            var enhanceBtn = _root.Q<Button>("enhance-btn");
            enhanceBtn?.RegisterCallback<ClickEvent>(_ => OnEnhanceClicked());
            _equipmentInventory = _root.Q<VisualElement>("equipment-inventory");

            // 슬롯 캐싱
            _slotMarkers = new VisualElement[SLOT_COUNT];
            _slotIcons = new VisualElement[SLOT_COUNT];
            _slotNames = new Label[SLOT_COUNT];
            _slotGrades = new Label[SLOT_COUNT];
            _slotEmpties = new Label[SLOT_COUNT];

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slotMarkers[i] = _root.Q<VisualElement>($"slot-marker-{i}");
                _slotIcons[i] = _root.Q<VisualElement>($"slot-icon-{i}");
                _slotNames[i] = _root.Q<Label>($"slot-name-{i}");
                _slotGrades[i] = _root.Q<Label>($"slot-grade-{i}");
                _slotEmpties[i] = _root.Q<Label>($"slot-empty-{i}");
            }

            // 무기 탭
            _weaponEquippedText = _root.Q<Label>("weapon-equipped-text");
            _weaponCount = _root.Q<Label>("weapon-count");
            _weaponInventory = _root.Q<VisualElement>("weapon-inventory");

            // 강화 탭
            _enhanceContent = _root.Q<VisualElement>("enhance-content");
        }

        // ═══════════════════════════════════
        //  서브탭 전환
        // ═══════════════════════════════════

        private void BindSubTabs()
        {
            _subtabEquipment?.RegisterCallback<ClickEvent>(_ => SwitchSubTab(0));
            _subtabEnhance?.RegisterCallback<ClickEvent>(_ => SwitchSubTab(1));
        }

        private void SwitchSubTab(int index)
        {
            _currentSubTab = index;

            // 서브탭 시각 상태
            SetSubTabActive(_subtabEquipment, index == 0);
            SetSubTabActive(_subtabEnhance, index == 1);

            // 콘텐츠 표시/숨김
            SetDisplay(_tabContentEquipment, index == 0);
            SetDisplay(_tabContentEnhance, index == 1);

            RefreshCurrentTab();
        }

        private static void SetSubTabActive(VisualElement tab, bool isActive)
        {
            if (tab == null) return;
            if (isActive)
                tab.AddToClassList("equip__subtab--active");
            else
                tab.RemoveFromClassList("equip__subtab--active");
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ═══════════════════════════════════
        //  이벤트 핸들러
        // ═══════════════════════════════════

        private void OnEquipmentChanged(EquipmentChangedEvent evt) => RefreshCurrentTab();
        private void OnInventoryChanged(EquipmentInventoryChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponChanged(WeaponChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponInventoryChanged(WeaponInventoryChangedEvent evt) => RefreshCurrentTab();
        private void OnWeaponLevelUp(WeaponLevelUpEvent evt) => RefreshCurrentTab();
        private void OnWeaponAwakening(WeaponAwakeningEvent evt) => RefreshCurrentTab();
        private void OnWeaponPromote(WeaponPromoteEvent evt) => RefreshCurrentTab();

        private void RefreshCurrentTab()
        {
            switch (_currentSubTab)
            {
                case 0:
                    RefreshEquipmentTab();
                    break;
                case 1:
                    RefreshEnhanceTab();
                    break;
            }
        }

        // ═══════════════════════════════════
        //  버튼 바인딩
        // ═══════════════════════════════════

        private void BindButtons()
        {
            var recommendBtn = _root.Q<Button>("recommend-btn");
            recommendBtn?.RegisterCallback<ClickEvent>(_ => OnRecommendEquipClicked());

            // 슬롯 클릭 → 팝업 열기
            BindSlotClickEvents();
        }

        private void BindSlotClickEvents()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var slotElement = _root.Q<VisualElement>($"slot-{i}");
                if (slotElement == null) continue;

                int capturedIndex = i;
                slotElement.RegisterCallback<ClickEvent>(_ => OnSlotClicked(capturedIndex));
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SLOT_ORDER.Length) return;
            var slot = SLOT_ORDER[slotIndex];

            // 팝업 참조 확보 (lazy)
            if (_slotPopup == null)
                _slotPopup = FindFirstObjectByType<PopupEquipSlotInventory>();

            if (_slotPopup != null)
            {
                _slotPopup.Show(slot);
                Debug.Log($"[EquipmentTabUI] 슬롯 클릭: {slot} (index={slotIndex})");
            }
            else
            {
                Debug.LogWarning("[EquipmentTabUI] PopupEquipSlotInventory를 찾을 수 없습니다.");
            }
        }

        // ═══════════════════════════════════
        //  탭 0: 장비
        // ═══════════════════════════════════

        private void RefreshEquipmentTab()
        {
            RefreshSlots();
            RefreshEquipmentInventory();
            RefreshBonusStats();
            RefreshCpLabel();
        }

        private void RefreshCpLabel()
        {
            if (_cpLabel == null) return;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var stats = player.GetComponent<CombatStats>();
                if (stats != null)
                {
                    int cp = CombatFormula.CalculateCP(stats.Atk, stats.Def, stats.MaxHp, stats.CritRate);
                    _cpLabel.text = $"전투력 {NumberFormatter.FormatKorean(cp)}";
                }
            }
        }

        private void OnEnhanceClicked()
        {
            var enhanceUI = FindFirstObjectByType<PopupEquipEnhanceUI>(FindObjectsInactive.Include);
            if (enhanceUI != null)
            {
                enhanceUI.gameObject.SetActive(true);
                enhanceUI.Show();
            }
        }

        private void RefreshSlots()
        {
            if (EquipmentManager.Instance == null)
            {
                // 폴백: 모든 슬롯 빈 상태 표시
                for (int i = 0; i < SLOT_COUNT; i++)
                {
                    if (_slotNames[i] != null) _slotNames[i].style.display = DisplayStyle.None;
                    if (_slotGrades[i] != null) _slotGrades[i].style.display = DisplayStyle.None;
                    if (_slotIcons[i] != null) _slotIcons[i].style.opacity = 0.3f;
                    if (_slotEmpties[i] != null)
                    {
                        _slotEmpties[i].style.display = DisplayStyle.Flex;
                        _slotEmpties[i].text = GetSlotDisplayName(SLOT_ORDER[i]);
                    }
                }
                return;
            }

            var equipped = EquipmentManager.Instance.GetAllEquipped();

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var slot = SLOT_ORDER[i];
                if (equipped.TryGetValue(slot, out var instance))
                {
                    var data = EquipmentManager.Instance.GetData(instance.equipmentId);
                    string displayName = data != null ? data.displayName : instance.equipmentId;
                    Color gradeColor = GetGradeColor(instance.grade);

                    if (_slotNames[i] != null)
                    {
                        _slotNames[i].text = displayName;
                        _slotNames[i].style.color = gradeColor;
                        _slotNames[i].style.display = DisplayStyle.Flex;
                    }

                    if (_slotGrades[i] != null)
                    {
                        _slotGrades[i].text = instance.grade;
                        _slotGrades[i].style.color = gradeColor;
                        _slotGrades[i].style.display = DisplayStyle.Flex;
                    }

                    if (_slotIcons[i] != null)
                    {
                        if (data != null && data.icon != null)
                            _slotIcons[i].style.backgroundImage = new StyleBackground(data.icon);
                        _slotIcons[i].style.opacity = 1f;
                    }

                    if (_slotMarkers[i] != null)
                        _slotMarkers[i].style.backgroundColor = gradeColor;

                    if (_slotEmpties[i] != null)
                        _slotEmpties[i].style.display = DisplayStyle.None;
                }
                else
                {
                    // 빈 슬롯
                    if (_slotNames[i] != null)
                    {
                        _slotNames[i].text = "";
                        _slotNames[i].style.display = DisplayStyle.None;
                    }

                    if (_slotGrades[i] != null)
                    {
                        _slotGrades[i].text = "";
                        _slotGrades[i].style.display = DisplayStyle.None;
                    }

                    // 빈 슬롯 기본 아이콘 — Stone Kit 기반
                    if (_slotIcons[i] != null)
                    {
                        string iconPath = slot switch
                        {
                            EquipmentSlot.Weapon => "Icons/Stone/icon_sword",
                            EquipmentSlot.Helmet => "Icons/Stone/icon_crown",
                            EquipmentSlot.Top => "Icons/Stone/icon_shield",
                            EquipmentSlot.Gloves => "Icons/Stone/icon_hammer",
                            EquipmentSlot.Boots => "Icons/Stone/icon_horseshoes",
                            EquipmentSlot.Ring => "Icons/Stone/icon_treasure",
                            EquipmentSlot.Necklace => "Icons/Stone/icon_gem",
                            _ => "Icons/Stone/icon_star"
                        };
                        var defaultIcon = Resources.Load<Sprite>(iconPath);
                        if (defaultIcon != null)
                            _slotIcons[i].style.backgroundImage = new StyleBackground(defaultIcon);
                        _slotIcons[i].style.opacity = 0.3f;
                    }

                    if (_slotMarkers[i] != null)
                        _slotMarkers[i].style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

                    if (_slotEmpties[i] != null)
                    {
                        _slotEmpties[i].style.display = DisplayStyle.Flex;
                        _slotEmpties[i].text = GetSlotDisplayName(slot);
                    }
                }
            }
        }

        private void RefreshEquipmentInventory()
        {
            if (_equipmentInventory == null) return;

            _equipmentInventory.Clear();

            if (EquipmentManager.Instance == null)
            {
                if (_inventoryCount != null)
                    _inventoryCount.text = "보유: 0개";
                var emptyLabel = new Label("장비 없음 — 가챠에서 획득하세요");
                emptyLabel.AddToClassList("empty-placeholder");
                _equipmentInventory.Add(emptyLabel);
                return;
            }

            var sorted = EquipmentManager.Instance.GetSortedInventory();

            if (_inventoryCount != null)
                _inventoryCount.text = $"보유: {sorted.Count}개";

            if (sorted.Count == 0)
            {
                var emptyLabel = new Label("장비 없음 — 가챠에서 획득하세요");
                emptyLabel.AddToClassList("empty-placeholder");
                _equipmentInventory.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                var row = CreateEquipmentItemRow(sorted[i]);
                _equipmentInventory.Add(row);
            }
        }

        private VisualElement CreateEquipmentItemRow(EquipmentInstance instance)
        {
            var data = EquipmentManager.Instance.GetData(instance.equipmentId);
            bool isEquipped = EquipmentManager.Instance.IsEquipped(instance.instanceId);
            Color gradeColor = GetGradeColor(instance.grade);

            // 행 컨테이너
            var row = new VisualElement();
            row.AddToClassList("equip__item-row");
            if (isEquipped)
                row.AddToClassList("equip__item-row--equipped");

            // 등급 마커
            var marker = new VisualElement();
            marker.AddToClassList("equip__item-marker");
            marker.style.backgroundColor = gradeColor;
            row.Add(marker);

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("equip__item-icon");
            if (data != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);
            row.Add(icon);

            // 정보 컬럼
            var info = new VisualElement();
            info.AddToClassList("equip__item-info");

            string name = data != null ? data.displayName : instance.equipmentId;
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("equip__item-name");
            nameLabel.style.color = gradeColor;
            info.Add(nameLabel);

            string slotName = data != null ? GetSlotDisplayName(data.slot) : "";
            var subSb = new StringBuilder();
            subSb.Append($"{instance.grade} · {slotName}");
            if (data != null)
            {
                var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(data.slot);
                if (slotEnh.scrollLevel > 0) subSb.Append($" · 주문서+{slotEnh.scrollLevel}");
                if (slotEnh.starForce > 0) subSb.Append($" · \u2605{slotEnh.starForce}");
            }

            var subLabel = new Label(subSb.ToString());
            subLabel.AddToClassList("equip__item-sub");
            info.Add(subLabel);
            row.Add(info);

            // 스탯 요약
            if (data != null)
            {
                var statSb = new StringBuilder();
                int atk = data.GetAtk(instance.grade);
                int hp = data.GetHp(instance.grade);
                int def = data.GetDef(instance.grade);
                if (atk > 0) statSb.Append($"ATK+{atk} ");
                if (hp > 0) statSb.Append($"HP+{hp} ");
                if (def > 0) statSb.Append($"DEF+{def}");

                var statLabel = new Label(statSb.ToString());
                statLabel.AddToClassList("equip__item-stats");
                row.Add(statLabel);
            }

            // 버튼 영역
            var btnArea = new VisualElement();
            btnArea.AddToClassList("equip__item-buttons");

            // 장착/해제 버튼
            string capturedId = instance.instanceId;
            EquipmentSlot capturedSlot = data != null ? data.slot : EquipmentSlot.Weapon;

            var equipBtn = new Button(() =>
            {
                if (EquipmentManager.Instance == null) return;
                int cpBefore = GetCurrentCP();
                if (isEquipped)
                    EquipmentManager.Instance.Unequip(capturedSlot);
                else
                    EquipmentManager.Instance.Equip(capturedId);
                int cpAfter = GetCurrentCP();
                ShowCpDeltaFeedback(cpAfter - cpBefore);
            });
            equipBtn.AddToClassList("equip__item-btn");
            equipBtn.AddToClassList(isEquipped ? "equip__item-btn--unequip" : "equip__item-btn--equip");
            equipBtn.Add(new Label(isEquipped ? "해제" : "장착"));
            btnArea.Add(equipBtn);

            // 강화 버튼
            var enhanceBtn = new Button(() =>
            {
                _selectedEquipmentId = capturedId;
                SwitchSubTab(2);
            });
            enhanceBtn.AddToClassList("equip__item-btn");
            enhanceBtn.AddToClassList("equip__item-btn--enhance");
            enhanceBtn.Add(new Label("강화"));
            btnArea.Add(enhanceBtn);

            // 비교 버튼 (장착 중이 아닐 때만)
            if (!isEquipped && data != null)
            {
                string compareId = instance.instanceId;
                EquipmentSlot compareSlot = data.slot;
                var compareBtn = new Button(() => ShowEquipmentComparison(compareId, compareSlot));
                compareBtn.AddToClassList("equip__item-btn");
                compareBtn.AddToClassList("equip__item-btn--compare");
                compareBtn.Add(new Label("비교"));
                btnArea.Add(compareBtn);
            }

            row.Add(btnArea);
            return row;
        }

        private void RefreshBonusStats()
        {
            if (EquipmentManager.Instance == null)
            {
                if (_bonusAtk != null) _bonusAtk.text = "ATK +0";
                if (_bonusHp != null) _bonusHp.text = "HP +0";
                if (_bonusDef != null) _bonusDef.text = "DEF +0";
                return;
            }

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

            if (_bonusAtk != null)
                _bonusAtk.text = $"ATK +{totalAtk}";
            if (_bonusHp != null)
                _bonusHp.text = $"HP +{totalHp}";
            if (_bonusDef != null)
                _bonusDef.text = $"DEF +{totalDef}";
        }

        // ═══════════════════════════════════
        //  탭 1: 무기
        // ═══════════════════════════════════

        private void RefreshWeaponTab()
        {
            if (WeaponManager.Instance == null)
            {
                if (_weaponEquippedText != null)
                {
                    _weaponEquippedText.text = "장착 무기: 없음";
                    _weaponEquippedText.style.color = new Color(0.682f, 0.631f, 0.565f);
                }
                if (_weaponCount != null)
                    _weaponCount.text = "보유: 0개";
                if (_weaponInventory != null)
                {
                    _weaponInventory.Clear();
                    var emptyLabel = new Label("무기 없음 — 가챠에서 획득하세요");
                    emptyLabel.AddToClassList("empty-placeholder");
                    _weaponInventory.Add(emptyLabel);
                }
                return;
            }

            // 장착 무기 표시
            if (_weaponEquippedText != null)
            {
                var equipped = WeaponManager.Instance.EquippedWeapon;
                if (equipped != null)
                {
                    var data = WeaponManager.Instance.GetData(equipped.weaponId);
                    string name = data != null ? data.displayName : equipped.weaponId;
                    Color gradeColor = GetGradeColor(equipped.grade);
                    int eqLevelCap = WeaponManager.GetWeaponLevelCap(equipped.awakeningStars);
                    _weaponEquippedText.text = $"장착: {name} ({equipped.grade}) Lv{equipped.level}/{eqLevelCap} \u2605{equipped.awakeningStars}";
                    _weaponEquippedText.style.color = gradeColor;
                }
                else
                {
                    _weaponEquippedText.text = "장착 무기: 없음";
                    _weaponEquippedText.style.color = new Color(0.682f, 0.631f, 0.565f);
                }
            }

            // 인벤토리 갱신
            if (_weaponInventory == null) return;
            _weaponInventory.Clear();

            var sorted = WeaponManager.Instance.GetSortedInventory();
            if (_weaponCount != null)
                _weaponCount.text = $"보유: {sorted.Count}개";

            // 2026-04-23 빈 상태 처리: 무기 0개인 경우 안내 메시지
            if (sorted.Count == 0)
            {
                var empty = new Label("보유 무기가 없습니다.\n무기 가챠로 뽑아보세요!");
                empty.AddToClassList("equip__empty-msg");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                empty.style.fontSize = 16;
                empty.style.paddingTop = 32;
                empty.style.paddingBottom = 32;
                _weaponInventory.Add(empty);
                return;
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                var row = CreateWeaponItemRow(sorted[i]);
                _weaponInventory.Add(row);
            }
        }

        private VisualElement CreateWeaponItemRow(WeaponInstance instance)
        {
            var data = WeaponManager.Instance.GetData(instance.weaponId);
            bool isEquipped = instance.isEquipped;
            Color gradeColor = GetGradeColor(instance.grade);
            int finalAtk = data != null ? data.GetFinalAtk(instance.grade, instance.level, instance.awakeningStars) : 0;

            // 행 컨테이너
            var row = new VisualElement();
            row.AddToClassList("equip__item-row");
            if (isEquipped)
                row.AddToClassList("equip__item-row--equipped");

            // 등급 마커
            var marker = new VisualElement();
            marker.AddToClassList("equip__item-marker");
            marker.style.backgroundColor = gradeColor;
            row.Add(marker);

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("equip__item-icon");
            if (data != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);
            row.Add(icon);

            // 정보 컬럼
            var info = new VisualElement();
            info.AddToClassList("equip__item-info");

            string name = data != null ? data.displayName : instance.weaponId;
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("equip__item-name");
            nameLabel.style.color = gradeColor;
            info.Add(nameLabel);

            var subSb = new StringBuilder();
            int dispLevelCap = WeaponManager.GetWeaponLevelCap(instance.awakeningStars);
            subSb.Append($"{instance.grade} Lv{instance.level}/{dispLevelCap}");
            if (instance.awakeningStars > 0)
                subSb.Append($" \u2605{instance.awakeningStars}");

            var subLabel = new Label(subSb.ToString());
            subLabel.AddToClassList("equip__item-sub");
            info.Add(subLabel);
            row.Add(info);

            // 스탯
            var statSb = new StringBuilder();
            statSb.Append($"ATK {finalAtk}");
            if (data != null)
            {
                float critRate = data.GetCritRate(instance.grade);
                if (critRate > 0) statSb.Append($"\nCrit {critRate:P0}");
                if (data.passiveAtkPercent > 0)
                    statSb.Append($"\n보유효과 ATK+{data.passiveAtkPercent:F1}%");
            }

            var statLabel = new Label(statSb.ToString());
            statLabel.AddToClassList("equip__item-stats");
            row.Add(statLabel);

            // 버튼 영역
            var btnArea = new VisualElement();
            btnArea.AddToClassList("equip__item-buttons");

            string capturedId = instance.instanceId;

            // 장착/해제
            var equipBtn = new Button(() =>
            {
                if (WeaponManager.Instance == null) return;
                int cpBefore = GetCurrentCP();
                if (isEquipped)
                    WeaponManager.Instance.Unequip();
                else
                    WeaponManager.Instance.Equip(capturedId);
                int cpAfter = GetCurrentCP();
                ShowCpDeltaFeedback(cpAfter - cpBefore);
            });
            equipBtn.AddToClassList("equip__item-btn");
            equipBtn.AddToClassList(isEquipped ? "equip__item-btn--unequip" : "equip__item-btn--equip");
            equipBtn.Add(new Label(isEquipped ? "해제" : "장착"));
            btnArea.Add(equipBtn);

            // 레벨업
            bool canLevelUp = WeaponManager.Instance.CanLevelUp(capturedId);
            var levelUpBtn = new Button(() =>
            {
                if (WeaponManager.Instance == null) return;
                WeaponManager.Instance.LevelUpWeapon(capturedId);
            });
            levelUpBtn.AddToClassList("equip__item-btn");
            levelUpBtn.AddToClassList(canLevelUp ? "equip__item-btn--levelup" : "equip__item-btn--disabled");
            levelUpBtn.Add(new Label("레벨업"));
            levelUpBtn.SetEnabled(canLevelUp);
            btnArea.Add(levelUpBtn);

            // 각성
            bool canAwaken = WeaponManager.Instance.CanAwaken(capturedId);
            int matCount = WeaponManager.Instance.GetAwakeningMaterialCount(capturedId);
            int levelCap = WeaponManager.GetWeaponLevelCap(instance.awakeningStars);
            var awakenBtn = new Button(() =>
            {
                if (WeaponManager.Instance == null) return;
                WeaponManager.Instance.AwakenWeapon(capturedId);
            });
            awakenBtn.AddToClassList("equip__item-btn");
            awakenBtn.AddToClassList(canAwaken ? "equip__item-btn--awaken" : "equip__item-btn--disabled");
            awakenBtn.Add(new Label($"각성 ({matCount}/4) Cap:{levelCap}"));
            awakenBtn.SetEnabled(canAwaken);
            btnArea.Add(awakenBtn);

            // 승급
            bool canPromote = WeaponManager.Instance.CanPromote(capturedId);
            int promoteMatCount = WeaponManager.Instance.GetPromoteMaterialCount(capturedId);
            string nextGrade = WeaponManager.GetNextGrade(instance.grade);
            if (nextGrade != null)
            {
                var promoteBtn = new Button(() =>
                {
                    if (WeaponManager.Instance == null) return;
                    WeaponManager.Instance.PromoteWeapon(capturedId);
                });
                promoteBtn.AddToClassList("equip__item-btn");
                promoteBtn.AddToClassList(canPromote ? "equip__item-btn--promote" : "equip__item-btn--disabled");
                promoteBtn.Add(new Label($"승급→{nextGrade} ({promoteMatCount}/4)"));
                promoteBtn.SetEnabled(canPromote);
                btnArea.Add(promoteBtn);
            }

            // 비교 (장착 중이 아닐 때만)
            if (!isEquipped)
            {
                string weaponCompareId = instance.instanceId;
                var compareBtn = new Button(() => ShowWeaponComparison(weaponCompareId));
                compareBtn.AddToClassList("equip__item-btn");
                compareBtn.AddToClassList("equip__item-btn--compare");
                compareBtn.Add(new Label("비교"));
                btnArea.Add(compareBtn);
            }

            row.Add(btnArea);
            return row;
        }

        // ═══════════════════════════════════
        //  탭 2: 강화
        // ═══════════════════════════════════

        private void RefreshEnhanceTab()
        {
            if (_enhanceContent == null) return;
            if (EquipmentManager.Instance == null)
            {
                _enhanceContent.Clear();
                var emptyLabel = new Label("강화할 장비가 없습니다.");
                emptyLabel.AddToClassList("empty-placeholder");
                _enhanceContent.Add(emptyLabel);
                return;
            }

            _enhanceContent.Clear();

            // 선택 없으면 목록 표시
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

            // 헤더
            BuildEnhanceHeader(inst, data, gradeColor);

            // 뒤로가기 버튼
            var backBtn = new Button(() =>
            {
                _selectedEquipmentId = null;
                RefreshEnhanceTab();
            });
            backBtn.AddToClassList("equip__back-btn");
            backBtn.Add(new Label("\u2190 장비 선택"));
            _enhanceContent.Add(backBtn);

            // 잠재능력 섹션
            BuildPotentialSection(inst, data.slot);

            // 보조 잠재능력 섹션 (12성 이상)
            var enhForCheck = EquipmentManager.Instance.GetSlotEnhancement(data.slot);
            if (enhForCheck.starForce >= 12)
                BuildSubPotentialSection(inst, data.slot);
        }

        private void BuildEnhanceSelectionList()
        {
            // 제목
            var title = new Label("강화할 장비를 선택하세요");
            title.AddToClassList("equip__enhance-title");
            _enhanceContent.Add(title);

            var sorted = EquipmentManager.Instance.GetSortedInventory();
            for (int i = 0; i < sorted.Count; i++)
            {
                var instance = sorted[i];
                var eqData = EquipmentManager.Instance.GetData(instance.equipmentId);
                if (eqData == null) continue;

                Color gradeColor = GetGradeColor(instance.grade);
                string capturedId = instance.instanceId;

                var row = new VisualElement();
                row.AddToClassList("equip__select-row");

                // 등급 마커
                var marker = new VisualElement();
                marker.AddToClassList("equip__item-marker");
                marker.style.backgroundColor = gradeColor;
                row.Add(marker);

                // 아이콘
                var icon = new VisualElement();
                icon.AddToClassList("equip__item-icon");
                icon.style.width = 44;
                icon.style.height = 44;
                if (eqData.icon != null)
                    icon.style.backgroundImage = new StyleBackground(eqData.icon);
                row.Add(icon);

                // 정보
                var info = new VisualElement();
                info.AddToClassList("equip__item-info");

                var nameLabel = new Label(eqData.displayName);
                nameLabel.AddToClassList("equip__item-name");
                nameLabel.style.color = gradeColor;
                info.Add(nameLabel);

                var subSb = new StringBuilder();
                subSb.Append($"{instance.grade} · {GetSlotDisplayName(eqData.slot)}");
                var selSlotEnh = EquipmentManager.Instance.GetSlotEnhancement(eqData.slot);
                if (selSlotEnh.scrollLevel > 0) subSb.Append($" · 주문서+{selSlotEnh.scrollLevel}");
                if (selSlotEnh.starForce > 0) subSb.Append($" · \u2605{selSlotEnh.starForce}");
                var subLabel = new Label(subSb.ToString());
                subLabel.AddToClassList("equip__item-sub");
                info.Add(subLabel);
                row.Add(info);

                // 선택 버튼
                var selectBtn = new Button(() =>
                {
                    _selectedEquipmentId = capturedId;
                    RefreshEnhanceTab();
                });
                selectBtn.AddToClassList("equip__select-btn");
                selectBtn.Add(new Label("선택"));
                row.Add(selectBtn);

                _enhanceContent.Add(row);
            }
        }

        private void BuildEnhanceHeader(EquipmentInstance inst, EquipmentDataSO data, Color gradeColor)
        {
            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(data.slot);

            var header = new VisualElement();
            header.AddToClassList("equip__enhance-header");

            var marker = new VisualElement();
            marker.AddToClassList("equip__item-marker");
            marker.style.backgroundColor = gradeColor;
            header.Add(marker);

            var icon = new VisualElement();
            icon.AddToClassList("equip__item-icon");
            if (data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);
            header.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("equip__item-info");

            var nameLabel = new Label(data.displayName);
            nameLabel.AddToClassList("equip__item-name");
            nameLabel.style.color = gradeColor;
            info.Add(nameLabel);

            var subSb = new StringBuilder();
            subSb.Append($"{inst.grade} · {GetSlotDisplayName(data.slot)}");
            if (slotEnh.scrollLevel > 0) subSb.Append($" · 주문서+{slotEnh.scrollLevel}");
            if (slotEnh.starForce > 0) subSb.Append($" · \u2605{slotEnh.starForce}");
            if (slotEnh.potentialGrade > PotentialGrade.None) subSb.Append($" · 잠재:{slotEnh.potentialGrade}");
            var subLabel = new Label(subSb.ToString());
            subLabel.AddToClassList("equip__item-sub");
            info.Add(subLabel);
            header.Add(info);

            _enhanceContent.Add(header);
        }

        private void AddSectionTitle(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("equip__section-title");
            var label = new Label(title);
            label.AddToClassList("equip__section-title-label");
            section.Add(label);
            _enhanceContent.Add(section);
        }

        private void BuildPotentialSection(EquipmentInstance inst, EquipmentSlot slot)
        {
            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);

            AddSectionTitle("잠재능력");

            var row = new VisualElement();
            row.AddToClassList("equip__enhance-row");

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

            var infoLabel = new Label(infoSb.ToString());
            infoLabel.AddToClassList("equip__enhance-info");
            row.Add(infoLabel);

            bool canChange = PotentialSystem.Instance != null
                && PotentialSystem.Instance.CanChangePotential(slot);
            bool isMaxGrade = slotEnh.potentialGrade >= PotentialGrade.Mythic;
            EquipmentSlot capturedSlotForPotential = slot;

            var btn = new Button(() =>
            {
                if (PotentialSystem.Instance == null) return;
                PotentialSystem.Instance.ChangePotential(capturedSlotForPotential);
                RefreshEnhanceTab();
            });
            btn.AddToClassList("equip__enhance-btn");
            btn.AddToClassList(isMaxGrade || !canChange ? "equip__enhance-btn--disabled" : "equip__enhance-btn--active");
            btn.Add(new Label(isMaxGrade ? "최대" : "변경"));
            btn.SetEnabled(!isMaxGrade && canChange);
            row.Add(btn);

            _enhanceContent.Add(row);
        }

        private void BuildSubPotentialSection(EquipmentInstance inst, EquipmentSlot slot)
        {
            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);

            AddSectionTitle("보조 잠재능력 (12성+)");

            var row = new VisualElement();
            row.AddToClassList("equip__enhance-row");

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

            var infoLabel = new Label(infoSb.ToString());
            infoLabel.AddToClassList("equip__enhance-info");
            row.Add(infoLabel);

            bool canChange = PotentialSystem.Instance != null
                && PotentialSystem.Instance.CanChangeSubPotential(slot);
            bool isMaxGrade = slotEnh.subPotentialGrade >= PotentialGrade.Mythic;
            EquipmentSlot capturedSlotForSub = slot;

            var btn = new Button(() =>
            {
                if (PotentialSystem.Instance == null) return;
                PotentialSystem.Instance.ChangeSubPotential(capturedSlotForSub);
                RefreshEnhanceTab();
            });
            btn.AddToClassList("equip__enhance-btn");
            btn.AddToClassList(isMaxGrade || !canChange ? "equip__enhance-btn--disabled" : "equip__enhance-btn--active");
            btn.Add(new Label(isMaxGrade ? "최대" : "변경"));
            btn.SetEnabled(!isMaxGrade && canChange);
            row.Add(btn);

            _enhanceContent.Add(row);
        }

        // ═══════════════════════════════════
        //  추천 장착
        // ═══════════════════════════════════

        private void OnRecommendEquipClicked()
        {
            if (RecommendationManager.Instance == null) return;

            int count = RecommendationManager.Instance.AutoEquipRecommended();
            Debug.Log($"[EquipmentTabUI] 추천 장착: {count}개");

            // 2026-04-23 침묵 액션 보완: 결과 Toast (0개면 이미 장착됨 안내)
            string msg = count > 0 ? $"추천 장비 {count}개 장착" : "이미 최선 장비 장착됨";
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                msg,
                shakeIntensity: count > 0 ? 1 : 0);

            RefreshCurrentTab();
        }

        // ═══════════════════════════════════
        //  비교
        // ═══════════════════════════════════

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

        // ═══════════════════════════════════
        //  유틸
        // ═══════════════════════════════════

        private static Color GetGradeColor(string grade)
        {
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

        // ═══════════════════════════════════
        //  CP 변화 피드백
        // ═══════════════════════════════════

        private static int GetCurrentCP()
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return 0;
            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return 0;
            return CombatFormula.CalculateCP(stats.Atk, stats.Def, stats.MaxHp, stats.CritRate);
        }

        private void ShowCpDeltaFeedback(int delta)
        {
            if (delta == 0) return;

            string sign = delta > 0 ? "+" : "";
            string icon = delta > 0 ? "\u2191" : "\u2193"; // ↑ / ↓
            ToastUI.Show($"전투력 {sign}{delta:N0}", icon);
            Debug.Log($"[EquipmentTabUI] CP 변화: {sign}{delta:N0}");
        }
    }
}
