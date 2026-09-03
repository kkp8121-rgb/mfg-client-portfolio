using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 도감 패널 컨트롤러.
    /// 5개 카테고리(몬스터/장비/무기/유물/비밀) 탭 전환,
    /// 수집 카드 그리드, 마일스톤 보상, 칭호 표시를 담당한다.
    /// CollectionBookManager와 연동하여 수집 상태/마일스톤/칭호 데이터를 표시.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CollectionBookPanelUI : MonoBehaviour
    {
        // 카테고리 탭 키
        private static readonly string[] CAT_KEYS =
            { "monster", "equipment", "weapon", "relic", "secret" };

        // 카테고리 키 → CollectionCategory 매핑 (secret은 미지원)
        // 2026-04-20 유물 시스템 제거 — "relic" 매핑 제외 (UXML tab-relic은 display:none)
        private static readonly Dictionary<string, CollectionCategory> KeyToCategory = new()
        {
            { "monster",   CollectionCategory.Monster },
            { "equipment", CollectionCategory.Equipment },
            { "weapon",    CollectionCategory.Weapon },
        };

        // 카테고리 한글 이름
        private static readonly Dictionary<string, string> CatDisplayNames = new()
        {
            { "monster",   "몬스터" },
            { "equipment", "장비" },
            { "weapon",    "무기" },
            { "relic",     "유물" },
            { "secret",    "비밀" },
        };

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 헤더 ──
        private Label _overallLabel;
        private VisualElement _overallFill;
        private Button _closeBtn;

        // ── 탭 ──
        private VisualElement[] _tabElements;
        private int _currentTab = -1;

        // ── 콘텐츠 ──
        private ScrollView _contentScroll;
        private Label _categoryProgressLabel;
        private VisualElement _categoryProgressFill;
        private VisualElement _cardGrid;
        private VisualElement _milestoneList;
        private VisualElement _titleList;

        // ── 재사용 리스트 (GC 최소화) ──
        private readonly List<MilestoneInfo> _milestoneCache = new();
        private readonly List<TitleInfo> _titleCache = new();

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 58; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[CollectionBookPanelUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindTabs();
            BindCloseButton();

            // 이벤트 구독 (중복 방지: Unsubscribe 먼저)
            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnCollectionRegistered);
            EventBus<CollectionEntryRegisteredEvent>.Subscribe(OnCollectionRegistered);

            EventBus<CollectionMilestoneClaimedEvent>.Unsubscribe(OnMilestoneClaimed);
            EventBus<CollectionMilestoneClaimedEvent>.Subscribe(OnMilestoneClaimed);

            EventBus<CollectionTitleChangedEvent>.Unsubscribe(OnTitleChanged);
            EventBus<CollectionTitleChangedEvent>.Subscribe(OnTitleChanged);

            // 기본 탭 선택
            SwitchTab(0);

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");

            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnCollectionRegistered);
            EventBus<CollectionMilestoneClaimedEvent>.Unsubscribe(OnMilestoneClaimed);
            EventBus<CollectionTitleChangedEvent>.Unsubscribe(OnTitleChanged);
        }

        // ── 열기 애니메이션 ──

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ── 요소 캐싱 ──

        private void CacheElements()
        {
            _overallLabel = _root.Q<Label>("overall-label");
            _overallFill = _root.Q<VisualElement>("overall-fill");
            _closeBtn = _root.Q<Button>("btn-close");

            _contentScroll = _root.Q<ScrollView>("content-scroll");
            _categoryProgressLabel = _root.Q<Label>("category-progress-label");
            _categoryProgressFill = _root.Q<VisualElement>("category-progress-fill");
            _cardGrid = _root.Q<VisualElement>("card-grid");
            _milestoneList = _root.Q<VisualElement>("milestone-list");
            _titleList = _root.Q<VisualElement>("title-list");

            int tabCount = CAT_KEYS.Length;
            _tabElements = new VisualElement[tabCount];
            for (int i = 0; i < tabCount; i++)
            {
                _tabElements[i] = _root.Q<VisualElement>($"tab-{CAT_KEYS[i]}");
            }
        }

        // ── 탭 바인딩 ──

        private void BindTabs()
        {
            for (int i = 0; i < CAT_KEYS.Length; i++)
            {
                int idx = i;
                _tabElements[i]?.RegisterCallback<ClickEvent>(_ => SwitchTab(idx));
            }
        }

        private void BindCloseButton()
        {
            if (_closeBtn != null)
            {
                _closeBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    gameObject.SetActive(false);
                });
            }
        }

        private void SwitchTab(int index)
        {
            if (index == _currentTab) return;
            if (index < 0 || index >= CAT_KEYS.Length) return;

            // 탭 시각 전환
            for (int i = 0; i < CAT_KEYS.Length; i++)
            {
                if (_tabElements[i] == null) continue;

                if (i == index)
                    _tabElements[i].AddToClassList("collection-tab--active");
                else
                    _tabElements[i].RemoveFromClassList("collection-tab--active");
            }

            _currentTab = index;
            RefreshAll();

            Debug.Log($"[CollectionBookPanelUI] 탭 전환: {CAT_KEYS[index]}");
        }

        // ── 전체 갱신 ──

        private void RefreshAll()
        {
            RefreshOverallProgress();

            string key = CAT_KEYS[_currentTab];
            if (key == "secret")
            {
                ShowComingSoon();
                return;
            }

            if (!KeyToCategory.TryGetValue(key, out CollectionCategory category))
                return;

            RefreshCategoryProgress(category);
            RefreshCardGrid(category);
            RefreshMilestones(category);
            RefreshTitles();
        }

        // ── 전체 진행도 ──

        private void RefreshOverallProgress()
        {
            if (CollectionBookManager.Instance == null)
            {
                if (_overallLabel != null) _overallLabel.text = "전체 0/0";
                SetProgressFill(_overallFill, 0f);
                return;
            }

            int totalCollected = CollectionBookManager.Instance.GetTotalCollected();
            int totalAll = 0;
            foreach (var kvp in KeyToCategory)
            {
                var (_, total) = CollectionBookManager.Instance.GetProgress(kvp.Value);
                totalAll += total;
            }

            if (_overallLabel != null)
                _overallLabel.text = $"전체 {totalCollected}/{totalAll}";

            float rate = totalAll > 0 ? (float)totalCollected / totalAll : 0f;
            SetProgressFill(_overallFill, rate);
        }

        // ── 카테고리 진행도 ──

        private void RefreshCategoryProgress(CollectionCategory category)
        {
            if (CollectionBookManager.Instance == null)
            {
                if (_categoryProgressLabel != null) _categoryProgressLabel.text = "수집 0/0";
                SetProgressFill(_categoryProgressFill, 0f);
                return;
            }

            var (collected, total) = CollectionBookManager.Instance.GetProgress(category);

            if (_categoryProgressLabel != null)
                _categoryProgressLabel.text = $"수집 {collected}/{total}";

            float rate = total > 0 ? (float)collected / total : 0f;
            SetProgressFill(_categoryProgressFill, rate);
        }

        // ── 수집 카드 그리드 ──

        private void RefreshCardGrid(CollectionCategory category)
        {
            if (_cardGrid == null) return;
            _cardGrid.Clear();

            // 카테고리 진행도/전체 콘텐츠 영역 표시
            ShowContentElements(true);

            if (CollectionBookManager.Instance == null) return;

            var (_, total) = CollectionBookManager.Instance.GetProgress(category);
            var collectedEntries = CollectionBookManager.Instance.GetCollectedEntries(category);

            // 수집된 엔트리들을 카드로 표시
            if (collectedEntries != null)
            {
                foreach (string entryId in collectedEntries)
                {
                    var card = CreateCollectedCard(entryId, category);
                    _cardGrid.Add(card);
                }
            }

            // 미발견 슬롯 (전체 수 - 수집 수)
            int collectedCount = collectedEntries != null ? collectedEntries.Count : 0;
            int unknownCount = total - collectedCount;
            for (int i = 0; i < unknownCount; i++)
            {
                var card = CreateUnknownCard(category);
                _cardGrid.Add(card);
            }
        }

        /// <summary>
        /// 카테고리별 fallback 아이콘 경로.
        /// entry별 개별 스프라이트 매핑이 완성되기 전까지 카테고리 기본 아이콘 사용.
        /// </summary>
        private static string GetCategoryIconPath(CollectionCategory category)
        {
            return category switch
            {
                CollectionCategory.Monster => "Icons/DarkGeo/icon_skull",
                CollectionCategory.Equipment => "Icons/DarkGeo/icon_shield_1",
                CollectionCategory.Weapon => "Icons/DarkGeo/sword_f",
                CollectionCategory.Companion => "Icons/DarkGeo/icon_feather",
                CollectionCategory.Pet => "Icons/DarkGeo/icon_feather",
                // CollectionCategory.Costume: 2026-04-20 제거
                _ => "Icons/DarkGeo/gift",
            };
        }

        private VisualElement CreateCollectedCard(string entryId, CollectionCategory category)
        {
            var card = new VisualElement();
            card.AddToClassList("collection-card");
            card.AddToClassList("collection-card--collected");

            // 아이콘 — entry별 개별 스프라이트 우선, 없으면 카테고리 fallback
            var icon = new VisualElement();
            icon.AddToClassList("collection-card__icon");
            var (_, entryIcon) = ResolveEntryMeta(entryId, category);
            Sprite iconSprite = entryIcon != null
                ? entryIcon
                : Resources.Load<Sprite>(GetCategoryIconPath(category));
            if (iconSprite != null)
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            card.Add(icon);

            // 이름
            var nameLabel = new Label();
            nameLabel.AddToClassList("collection-card__name");
            nameLabel.text = FormatEntryName(entryId);
            card.Add(nameLabel);

            return card;
        }

        private VisualElement CreateUnknownCard(CollectionCategory category)
        {
            var card = new VisualElement();
            card.AddToClassList("collection-card");
            card.AddToClassList("collection-card--unknown");

            // 아이콘 영역 (실루엣) — 카테고리 기본 아이콘 + 어둡게 tint (unknown USS 규칙 적용)
            var icon = new VisualElement();
            icon.AddToClassList("collection-card__icon");
            var iconSprite = Resources.Load<Sprite>(GetCategoryIconPath(category));
            if (iconSprite != null)
                icon.style.backgroundImage = new StyleBackground(iconSprite);
            card.Add(icon);

            // 물음표
            var question = new Label();
            question.AddToClassList("collection-card__question");
            question.text = "?";
            card.Add(question);

            // 이름 (미확인)
            var nameLabel = new Label();
            nameLabel.AddToClassList("collection-card__name");
            nameLabel.text = "???";
            card.Add(nameLabel);

            return card;
        }

        // ── 마일스톤 보상 ──

        private void RefreshMilestones(CollectionCategory category)
        {
            if (_milestoneList == null) return;
            _milestoneList.Clear();

            if (CollectionBookManager.Instance == null) return;

            _milestoneCache.Clear();
            var milestones = CollectionBookManager.Instance.GetMilestoneInfos(category);
            _milestoneCache.AddRange(milestones);

            for (int i = 0; i < _milestoneCache.Count; i++)
            {
                var info = _milestoneCache[i];
                var row = CreateMilestoneRow(info, category);
                _milestoneList.Add(row);
            }
        }

        private VisualElement CreateMilestoneRow(MilestoneInfo info, CollectionCategory category)
        {
            var row = new VisualElement();
            row.AddToClassList("milestone-row");

            if (info.IsAchieved)
                row.AddToClassList("milestone-row--achieved");
            if (info.IsClaimed)
                row.AddToClassList("milestone-row--claimed");

            // 별 아이콘
            var star = new VisualElement();
            star.AddToClassList("milestone-row__star");
            row.Add(star);

            // 정보
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("milestone-row__info");

            var threshold = new Label();
            threshold.AddToClassList("milestone-row__threshold");
            threshold.text = $"{info.Threshold}종 수집";
            infoContainer.Add(threshold);

            var desc = new Label();
            desc.AddToClassList("milestone-row__desc");
            desc.text = !string.IsNullOrEmpty(info.Description) ? info.Description : $"{info.StatType} 보너스";
            infoContainer.Add(desc);

            row.Add(infoContainer);

            // 보상 버튼 또는 수령 완료 라벨
            if (info.IsClaimed)
            {
                var claimedLabel = new Label();
                claimedLabel.AddToClassList("milestone-row__claimed-label");
                claimedLabel.text = "수령 완료";
                row.Add(claimedLabel);
            }
            else if (info.IsAchieved)
            {
                int milestoneIndex = info.Index;
                var claimBtn = new Button();
                claimBtn.AddToClassList("milestone-row__claim-btn");
                var btnLabel = new Label();
                btnLabel.text = "받기";
                claimBtn.Add(btnLabel);

                claimBtn.RegisterCallback<ClickEvent>(_ => OnClaimMilestone(category, milestoneIndex));
                row.Add(claimBtn);
            }
            else
            {
                var claimBtn = new Button();
                claimBtn.AddToClassList("milestone-row__claim-btn");
                claimBtn.AddToClassList("milestone-row__claim-btn--disabled");
                claimBtn.SetEnabled(false);
                var btnLabel = new Label();
                btnLabel.text = "미달성";
                claimBtn.Add(btnLabel);
                row.Add(claimBtn);
            }

            return row;
        }

        private void OnClaimMilestone(CollectionCategory category, int milestoneIndex)
        {
            if (CollectionBookManager.Instance == null)
            {
                Debug.LogWarning("[CollectionBookPanelUI] CollectionBookManager.Instance가 null");
                return;
            }

            bool success = CollectionBookManager.Instance.ClaimMilestoneReward(category, milestoneIndex);
            if (success)
            {
                Debug.Log($"[CollectionBookPanelUI] 마일스톤 보상 수령: {category} #{milestoneIndex}");
                RefreshAll();
            }
            else
            {
                Debug.LogWarning($"[CollectionBookPanelUI] 마일스톤 보상 수령 실패: {category} #{milestoneIndex}");
            }
        }

        // ── 칭호 ──

        private void RefreshTitles()
        {
            if (_titleList == null) return;
            _titleList.Clear();

            if (CollectionBookManager.Instance == null) return;

            _titleCache.Clear();
            var titles = CollectionBookManager.Instance.GetTitleInfos();
            _titleCache.AddRange(titles);

            for (int i = 0; i < _titleCache.Count; i++)
            {
                var info = _titleCache[i];
                var row = CreateTitleRow(info);
                _titleList.Add(row);
            }
        }

        private VisualElement CreateTitleRow(TitleInfo info)
        {
            var row = new VisualElement();
            row.AddToClassList("title-row");

            if (info.IsCurrent)
                row.AddToClassList("title-row--current");
            else if (info.IsAchieved)
                row.AddToClassList("title-row--achieved");
            else
                row.AddToClassList("title-row--locked");

            // 칭호 이름 (등급 컬러 적용)
            var nameLabel = new Label();
            nameLabel.AddToClassList("title-row__name");
            nameLabel.text = !string.IsNullOrEmpty(info.TitleName) ? info.TitleName : "???";

            if (info.IsAchieved && info.TitleColor != default)
                nameLabel.style.color = info.TitleColor;

            row.Add(nameLabel);

            // 요구 조건
            var reqLabel = new Label();
            reqLabel.AddToClassList("title-row__requirement");
            reqLabel.text = !string.IsNullOrEmpty(info.Description) ? info.Description : $"전체 {info.RequiredTotal}종";
            row.Add(reqLabel);

            // 상태 뱃지
            var badge = new Label();
            badge.AddToClassList("title-row__badge");
            if (info.IsCurrent)
                badge.text = "착용 중";
            else if (info.IsAchieved)
                badge.text = "달성";
            else
                badge.text = "미달성";
            row.Add(badge);

            return row;
        }

        // ── 비밀 탭 (준비 중) ──

        private void ShowComingSoon()
        {
            ShowContentElements(false);

            if (_cardGrid == null) return;
            _cardGrid.Clear();

            var comingSoon = new VisualElement();
            comingSoon.AddToClassList("collection-coming-soon");

            var icon = new VisualElement();
            icon.AddToClassList("collection-coming-soon__icon");
            comingSoon.Add(icon);

            var text = new Label();
            text.AddToClassList("collection-coming-soon__text");
            text.text = "준비 중...";
            comingSoon.Add(text);

            _cardGrid.Add(comingSoon);
        }

        private void ShowContentElements(bool isVisible)
        {
            // 카테고리 진행도 표시/숨김
            var progressContainer = _categoryProgressLabel?.parent;
            if (progressContainer != null)
                progressContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            // 마일스톤/칭호 섹션 표시/숨김 (section-header 포함)
            if (_milestoneList != null)
            {
                _milestoneList.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                // 마일스톤 섹션 헤더 (milestoneList의 이전 형제)
                var milestoneHeader = _milestoneList.parent?.Q<VisualElement>(className: "section-header");
                if (milestoneHeader != null)
                    milestoneHeader.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_titleList != null)
            {
                _titleList.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ── 이벤트 핸들러 ──

        private void OnCollectionRegistered(CollectionEntryRegisteredEvent evt)
        {
            // 현재 보고 있는 카테고리와 관련 있으면 갱신
            if (_currentTab >= 0 && _currentTab < CAT_KEYS.Length)
            {
                string key = CAT_KEYS[_currentTab];
                if (KeyToCategory.TryGetValue(key, out CollectionCategory cat) && cat == evt.Category)
                {
                    RefreshAll();
                }
                else
                {
                    // 다른 카테고리라도 전체 진행도는 갱신
                    RefreshOverallProgress();
                }
            }
        }

        private void OnMilestoneClaimed(CollectionMilestoneClaimedEvent evt)
        {
            if (_currentTab >= 0 && _currentTab < CAT_KEYS.Length)
            {
                string key = CAT_KEYS[_currentTab];
                if (KeyToCategory.TryGetValue(key, out CollectionCategory cat) && cat == evt.Category)
                {
                    RefreshAll();
                }
            }
        }

        private void OnTitleChanged(CollectionTitleChangedEvent evt)
        {
            RefreshTitles();
        }

        // ── 유틸리티 ──

        private static void SetProgressFill(VisualElement fill, float rate)
        {
            if (fill == null) return;
            float percent = Mathf.Clamp01(rate) * 100f;
            fill.style.width = new Length(percent, LengthUnit.Percent);
        }

        // ═══ Entry 메타 (이름 + 아이콘) 캐시 ═══
        // Resources/Data/Monsters, Data/Equipment, Data/Weapons 등에서 lazy-load.
        // 카테고리별 Dictionary로 분리하여 동일 id가 서로 다른 카테고리에 존재해도 충돌 없음.
        // 예: "weapon_steel_sword"는 Equipment(슬롯) 및 Weapon(독립) 양쪽에 존재.
        private const string MONSTER_RESOURCES_PATH = "Data/Monsters";
        private const string EQUIPMENT_RESOURCES_PATH = "Data/Equipment";
        // Weapon 리소스 경로 후보 (둘 다 존재 가능 — 아이콘이 설정된 쪽을 우선).
        private static readonly string[] WEAPON_RESOURCES_PATHS = { "Data/Weapons", "Data/Weapon" };

        private static Dictionary<CollectionCategory, Dictionary<string, (string displayName, Sprite icon)>> _entryMetaByCategory;
        // 전역 fallback: 카테고리 미지정 조회 시 사용 (FormatEntryName).
        private static Dictionary<string, (string displayName, Sprite icon)> _entryMetaFallback;

        private static void BuildEntryMetaCacheOnce()
        {
            if (_entryMetaByCategory != null) return;

            _entryMetaByCategory = new Dictionary<CollectionCategory, Dictionary<string, (string, Sprite)>>();
            _entryMetaFallback = new Dictionary<string, (string, Sprite)>();

            // Monster — entry key는 asset 이름 (MonsterSpawner의 "(Clone)" 제거 후 파일명).
            var monsterMap = new Dictionary<string, (string, Sprite)>();
            var monsters = Resources.LoadAll<MkLike.Data.MonsterDataSO>(MONSTER_RESOURCES_PATH);
            if (monsters != null)
            {
                for (int i = 0; i < monsters.Length; i++)
                {
                    var m = monsters[i];
                    if (m == null) continue;
                    string key = m.name; // "Monster_Bandit" 등
                    string name = string.IsNullOrEmpty(m.displayName) ? key : m.displayName;
                    monsterMap[key] = (name, m.icon);
                    AddToFallback(key, name, m.icon);
                }
            }
            _entryMetaByCategory[CollectionCategory.Monster] = monsterMap;

            // Equipment — entry key는 SO.id (EquipmentChangedEvent.EquipmentId).
            var equipmentMap = new Dictionary<string, (string, Sprite)>();
            var equipments = Resources.LoadAll<MkLike.Data.EquipmentDataSO>(EQUIPMENT_RESOURCES_PATH);
            if (equipments != null)
            {
                for (int i = 0; i < equipments.Length; i++)
                {
                    var e = equipments[i];
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    string name = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
                    equipmentMap[e.id] = (name, e.icon);
                    AddToFallback(e.id, name, e.icon);
                }
            }
            _entryMetaByCategory[CollectionCategory.Equipment] = equipmentMap;

            // Weapon — entry key는 SO.id (WeaponChangedEvent.WeaponId).
            // 두 후보 경로를 모두 로드하여 아이콘이 설정된 SO가 덮어쓰도록 한다.
            var weaponMap = new Dictionary<string, (string displayName, Sprite icon)>();
            for (int p = 0; p < WEAPON_RESOURCES_PATHS.Length; p++)
            {
                var weapons = Resources.LoadAll<MkLike.Data.WeaponDataSO>(WEAPON_RESOURCES_PATHS[p]);
                if (weapons == null) continue;
                for (int i = 0; i < weapons.Length; i++)
                {
                    var w = weapons[i];
                    if (w == null || string.IsNullOrEmpty(w.id)) continue;
                    string name = string.IsNullOrEmpty(w.displayName) ? w.id : w.displayName;
                    // 이미 등록된 id가 있어도, 새 항목이 icon을 가지면 덮어쓴다.
                    if (weaponMap.TryGetValue(w.id, out var existing))
                    {
                        if (existing.icon != null) continue; // 기존이 icon 있으면 유지
                    }
                    weaponMap[w.id] = (name, w.icon);
                    AddToFallback(w.id, name, w.icon);
                }
            }
            _entryMetaByCategory[CollectionCategory.Weapon] = weaponMap;

            Debug.Log($"[CollectionBookPanelUI] EntryMeta 캐시: Monster={monsterMap.Count}, Equipment={equipmentMap.Count}, Weapon={weaponMap.Count}");
        }

        private static void AddToFallback(string key, string displayName, Sprite icon)
        {
            if (string.IsNullOrEmpty(key)) return;
            // icon이 있는 항목을 우선한다 (동일 id 충돌 시).
            if (_entryMetaFallback.TryGetValue(key, out var existing) && existing.icon != null && icon == null)
                return;
            _entryMetaFallback[key] = (displayName, icon);
        }

        /// <summary>
        /// entryId에 대한 (displayName, icon) 조회. 카테고리를 알면 namespaced lookup 우선.
        /// 매칭 없으면 (null, null).
        /// </summary>
        private static (string displayName, Sprite icon) ResolveEntryMeta(string entryId, CollectionCategory? category = null)
        {
            BuildEntryMetaCacheOnce();
            if (string.IsNullOrEmpty(entryId)) return (null, null);

            if (category.HasValue
                && _entryMetaByCategory.TryGetValue(category.Value, out var catMap)
                && catMap.TryGetValue(entryId, out var meta))
            {
                return meta;
            }

            // 카테고리 미지정 또는 매칭 실패 → 전역 fallback
            if (_entryMetaFallback.TryGetValue(entryId, out var fbMeta))
                return fbMeta;
            return (null, null);
        }

        private static string FormatEntryName(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return entryId;
            var (name, _) = ResolveEntryMeta(entryId);
            if (!string.IsNullOrEmpty(name)) return name;

            // fallback: 언더스코어 → 공백
            string formatted = entryId.Replace('_', ' ');
            if (formatted.Length > 0)
                formatted = char.ToUpper(formatted[0]) + formatted.Substring(1);
            return formatted;
        }
    }
}
