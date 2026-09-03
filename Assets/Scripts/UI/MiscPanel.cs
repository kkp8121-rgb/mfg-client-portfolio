using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Quest;
using MkLike.Utils;
using MkLike.Combat;

namespace MkLike.UI
{
    /// <summary>
    /// 기타 패널. 5개 서브 탭: 퀘스트/업적/출석/우편함/설정.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MiscPanel : BasePopup
    {
        [Header("서브 탭")]
        [SerializeField] private Button[] subTabButtons;
        [SerializeField] private Image[] subTabBgs;
        [SerializeField] private GameObject[] subTabContents;

        [Header("출석 체크")]
        [SerializeField] private Button[] attendanceDayButtons;
        [SerializeField] private Image[] attendanceDayChecks;
        [SerializeField] private Button attendanceCheckBtn;

        [Header("우편함")]
        [SerializeField] private Transform mailListContent;
        [SerializeField] private Button claimAllBtn;

        [Header("설정 - 사운드")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI bgmValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private Toggle sfxMuteToggle;

        [Header("설정 - 그래픽")]
        [SerializeField] private Button[] qualityButtons;
        [SerializeField] private Image[] qualityBgs;
        [SerializeField] private Toggle batterySaverToggle;

        [Header("축복")]
        [SerializeField] private TMP_Text _blessingNameText;
        [SerializeField] private TMP_Text _blessingEffectText;
        [SerializeField] private Button _blessingRerollButton;
        [SerializeField] private TMP_Text _blessingRerollText;

        [Header("업적 카테고리 필터")]
        [SerializeField] private Button[] _achievementCategoryButtons;
        [SerializeField] private Image[] _achievementCategoryBgs;
        [SerializeField] private Transform _achievementListContent;
        [SerializeField] private TMP_Text _achievementCountText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _titleUnequipButton;

        [Header("콘텐츠 바로가기")]
        [SerializeField] private Button _costumeButton;
        [SerializeField] private Button _eventButton;
        [SerializeField] private Button _collectionButton;

        [Header("색상")]
        [SerializeField] private Color subTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        [SerializeField] private Color subTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);

        private int _currentSubTab = -1;
        private bool _isRefreshingSettings;
        private AchievementCategory _selectedAchievementCategory = AchievementCategory.Combat;

        private void OnEnable()
        {
            EventBus<BlessingChangedEvent>.Subscribe(OnBlessingChanged);
            EventBus<MkLike.Quest.AchievementCompletedEvent>.Subscribe(OnAchievementCompleted);
            EventBus<MkLike.Quest.AchievementClaimedEvent>.Subscribe(OnAchievementClaimed);
            EventBus<TitleUnlockedEvent>.Subscribe(OnTitleUnlocked);
        }

        private void OnDisable()
        {
            EventBus<BlessingChangedEvent>.Unsubscribe(OnBlessingChanged);
            EventBus<MkLike.Quest.AchievementCompletedEvent>.Unsubscribe(OnAchievementCompleted);
            EventBus<MkLike.Quest.AchievementClaimedEvent>.Unsubscribe(OnAchievementClaimed);
            EventBus<TitleUnlockedEvent>.Unsubscribe(OnTitleUnlocked);
        }

        private void Start()
        {
            if (subTabButtons != null)
            {
                for (int i = 0; i < subTabButtons.Length; i++)
                {
                    if (subTabButtons[i] == null) continue;
                    int idx = i;
                    subTabButtons[i].onClick.AddListener(() => SwitchSubTab(idx));
                }
            }

            SetupSoundListeners();
            SetupGraphicsListeners();

            if (attendanceCheckBtn != null)
                attendanceCheckBtn.onClick.AddListener(OnAttendanceCheck);

            if (claimAllBtn != null)
                claimAllBtn.onClick.AddListener(OnClaimAll);

            if (_blessingRerollButton != null)
                _blessingRerollButton.onClick.AddListener(OnRerollClicked);

            // 코스튬 시스템 제거됨 — 버튼이 씬에 남아있다면 비활성화
            if (_costumeButton != null)
                _costumeButton.gameObject.SetActive(false);
            if (_eventButton != null)
                _eventButton.onClick.AddListener(OnEventClicked);
            if (_collectionButton != null)
                _collectionButton.onClick.AddListener(OnCollectionClicked);

            if (_titleUnequipButton != null)
                _titleUnequipButton.onClick.AddListener(OnTitleUnequipClicked);

            SetupAchievementCategoryButtons();
            ApplyShortcutIcons();
            RefreshBlessingSection();
            SwitchSubTab(0);
        }

        private void SetupSoundListeners()
        {
            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            if (bgmMuteToggle != null)
                bgmMuteToggle.onValueChanged.AddListener(OnBgmMuteChanged);

            if (sfxMuteToggle != null)
                sfxMuteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
        }

        private void SetupGraphicsListeners()
        {
            if (qualityButtons != null)
            {
                for (int i = 0; i < qualityButtons.Length; i++)
                {
                    if (qualityButtons[i] == null) continue;
                    int idx = i;
                    qualityButtons[i].onClick.AddListener(() => SetQuality(idx));
                }
            }

            if (batterySaverToggle != null)
                batterySaverToggle.onValueChanged.AddListener(OnBatterySaverChanged);
        }

        protected override void OnShow()
        {
            RefreshSettings();
        }

        public void SwitchSubTab(int index)
        {
            if (index == _currentSubTab) return;
            if (subTabContents == null || subTabContents.Length == 0) return;
            if (index < 0 || index >= subTabContents.Length) return;

            // 테마 탭 스프라이트 로드
            Sprite tabNormalSprite = null, tabSelectedSprite = null;
            var theme = UIThemeManager.Instance;
            if (theme != null)
            {
                var (normal, selected) = theme.GetTabSprites();
                tabNormalSprite = normal;
                tabSelectedSprite = selected;
            }

            for (int i = 0; i < subTabContents.Length; i++)
            {
                if (subTabContents[i] == null) continue;
                bool active = i == index;
                subTabContents[i].SetActive(active);

                if (subTabBgs != null && i < subTabBgs.Length && subTabBgs[i] != null)
                {
                    var bg = subTabBgs[i];
                    var sprite = active ? tabSelectedSprite : tabNormalSprite;
                    if (sprite != null)
                    {
                        bg.sprite = sprite;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;
                    }
                    else
                    {
                        DOTween.To(
                            () => bg.color,
                            c => bg.color = c,
                            active ? subTabSelected : subTabNormal, 0.15f)
                            .SetUpdate(true).SetLink(gameObject);
                    }
                }
            }

            var activeContent = subTabContents[index];
            if (activeContent != null)
            {
                var cg = activeContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = activeContent.AddComponent<CanvasGroup>();
                cg.alpha = 0;
                DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, 0.2f).SetUpdate(true).SetLink(gameObject);
            }

            _currentSubTab = index;

            // 설정 탭으로 전환 시 최신 값 반영 (마지막 탭이 설정 탭)
            if (subTabContents.Length > 0 && index == subTabContents.Length - 1)
                RefreshSettings();
        }

        /// <summary>
        /// SettingsManager의 현재 값을 UI에 반영한다.
        /// _isRefreshingSettings 플래그로 콜백 재진입을 방지한다.
        /// </summary>
        private void RefreshSettings()
        {
            if (SettingsManager.Instance == null) return;

            _isRefreshingSettings = true;

            var settings = SettingsManager.Instance;

            // 사운드
            if (bgmSlider != null)
                bgmSlider.value = settings.BgmVolume;

            if (sfxSlider != null)
                sfxSlider.value = settings.SfxVolume;

            UpdateBgmValueText(settings.BgmVolume);
            UpdateSfxValueText(settings.SfxVolume);

            if (bgmMuteToggle != null)
                bgmMuteToggle.isOn = settings.IsBgmMuted;

            if (sfxMuteToggle != null)
                sfxMuteToggle.isOn = settings.IsSfxMuted;

            // 그래픽
            UpdateQualityBgs(settings.QualityLevel);

            if (batterySaverToggle != null)
                batterySaverToggle.isOn = settings.IsBatterySaver;

            _isRefreshingSettings = false;
        }

        private void OnBgmChanged(float value)
        {
            UpdateBgmValueText(value);

            if (_isRefreshingSettings) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.BgmVolume = value;
        }

        private void OnSfxChanged(float value)
        {
            UpdateSfxValueText(value);

            if (_isRefreshingSettings) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.SfxVolume = value;
        }

        private void OnBgmMuteChanged(bool isMuted)
        {
            if (_isRefreshingSettings) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsBgmMuted = isMuted;
        }

        private void OnSfxMuteChanged(bool isMuted)
        {
            if (_isRefreshingSettings) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsSfxMuted = isMuted;
        }

        private void OnBatterySaverChanged(bool isEnabled)
        {
            if (_isRefreshingSettings) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsBatterySaver = isEnabled;
        }

        private void SetQuality(int index)
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.QualityLevel = index;

            UpdateQualityBgs(index);
        }

        private void UpdateQualityBgs(int selectedIndex)
        {
            if (qualityBgs == null) return;

            Sprite tabNormalSprite = null, tabSelectedSprite = null;
            var theme = UIThemeManager.Instance;
            if (theme != null)
            {
                var (normal, selected) = theme.GetTabSprites();
                tabNormalSprite = normal;
                tabSelectedSprite = selected;
            }

            for (int i = 0; i < qualityBgs.Length; i++)
            {
                if (qualityBgs[i] == null) continue;
                var bg = qualityBgs[i];
                bool isSelected = i == selectedIndex;
                var sprite = isSelected ? tabSelectedSprite : tabNormalSprite;
                if (sprite != null)
                {
                    bg.sprite = sprite;
                    bg.type = Image.Type.Sliced;
                    bg.color = Color.white;
                }
                else
                {
                    DOTween.To(
                        () => bg.color,
                        c => bg.color = c,
                        isSelected ? subTabSelected : subTabNormal, 0.15f)
                        .SetUpdate(true).SetLink(gameObject);
                }
            }
        }

        private void ApplyShortcutIcons()
        {
            var iconRegistry = IconRegistry.Instance;
            if (iconRegistry == null) return;

            // 코스튬 시스템 제거됨 — 아이콘 적용 대상에서 제외
            ApplyButtonIcon(_eventButton, iconRegistry.GetUIIcon("event"));
            ApplyButtonIcon(_collectionButton, iconRegistry.GetUIIcon("collection"));
        }

        private static void ApplyButtonIcon(Button btn, Sprite icon)
        {
            if (btn == null || icon == null) return;

            // 버튼에 아이콘 자식이 없으면 생성
            var existing = btn.transform.Find("ShortcutIcon");
            if (existing != null) return;

            var iconGo = new GameObject("ShortcutIcon", typeof(RectTransform));
            iconGo.transform.SetParent(btn.transform, false);
            var rt = iconGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(28, 28);
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(20, 0);
            var img = iconGo.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0.8f);
        }

        private void UpdateBgmValueText(float value)
        {
            if (bgmValueText != null)
                bgmValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void UpdateSfxValueText(float value)
        {
            if (sfxValueText != null)
                sfxValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void OnAttendanceCheck()
        {
            if (attendanceCheckBtn != null)
            {
                attendanceCheckBtn.transform.DOKill();
                attendanceCheckBtn.transform.localScale = Vector3.one;
                attendanceCheckBtn.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 6, 0.5f)
                    .SetUpdate(true).SetLink(attendanceCheckBtn.gameObject);
            }

            Debug.Log("[MiscPanel] 출석 체크 (추후 SaveData 연동)");
        }

        private void OnClaimAll()
        {
            if (claimAllBtn != null)
            {
                claimAllBtn.transform.DOKill();
                claimAllBtn.transform.localScale = Vector3.one;
                claimAllBtn.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 6, 0.5f)
                    .SetUpdate(true).SetLink(claimAllBtn.gameObject);
            }

            Debug.Log("[MiscPanel] 전체 수령 (추후 메일 시스템 연동)");
        }

        private void OnBlessingChanged(BlessingChangedEvent e)
        {
            RefreshBlessingSection();
        }

        private void RefreshBlessingSection()
        {
            var system = DailyBlessingSystem.Instance;
            if (system == null) return;

            if (_blessingNameText != null)
                _blessingNameText.text = GetBlessingName(system.CurrentBlessing);

            if (_blessingEffectText != null)
                _blessingEffectText.text = GetBlessingEffect(system.CurrentBlessing);

            if (_blessingRerollText != null)
                _blessingRerollText.text = $"재롤 ({system.RerollsRemaining}/3) - 루비 50";

            if (_blessingRerollButton != null)
                _blessingRerollButton.interactable = system.RerollsRemaining > 0;
        }

        private void OnRerollClicked()
        {
            var system = DailyBlessingSystem.Instance;
            if (system == null) return;

            if (_blessingRerollButton != null)
            {
                _blessingRerollButton.transform.DOKill();
                _blessingRerollButton.transform.localScale = Vector3.one;
                _blessingRerollButton.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 6, 0.5f)
                    .SetUpdate(true).SetLink(_blessingRerollButton.gameObject);
            }

            system.TryReroll();
        }

        private static string GetBlessingName(BlessingType type)
        {
            return type switch
            {
                BlessingType.Luck => "행운의 축복",
                BlessingType.Warrior => "전사의 축복",
                BlessingType.Guardian => "수호의 축복",
                BlessingType.Explorer => "탐험가의 축복",
                BlessingType.Blessed => "축복받은 자",
                _ => "알 수 없는 축복"
            };
        }

        private static string GetBlessingEffect(BlessingType type)
        {
            return type switch
            {
                BlessingType.Luck => "드롭률 +50%",
                BlessingType.Warrior => "공격력 +30%",
                BlessingType.Guardian => "방어력 +30%",
                BlessingType.Explorer => "경험치 +50%",
                BlessingType.Blessed => "전체 스탯 +20%",
                _ => ""
            };
        }

        private void OnEventClicked()
        {
            Debug.Log("[MiscPanel] 이벤트 패널 (추후 구현 예정)");
        }

        private void OnCollectionClicked()
        {
            var panel = FindFirstObjectByType<CollectionBookPanel>(FindObjectsInactive.Include);
            if (panel != null) panel.Show();
            else Debug.LogWarning("[MiscPanel] CollectionBookPanel을 찾을 수 없습니다.");
        }

        // ── 업적 카테고리 필터 ──

        private static readonly string[] AchievementCategoryLabels =
        {
            "전투", "성장", "장비", "수집", "코스튬", "챌린지",
            "파티", "축복", "광고", "환생", "도감",
            "시즌", "PvP", "펫", "길드", "등반조합"
        };

        private void SetupAchievementCategoryButtons()
        {
            if (_achievementCategoryButtons == null) return;

            for (int i = 0; i < _achievementCategoryButtons.Length; i++)
            {
                if (_achievementCategoryButtons[i] == null) continue;
                int idx = i;
                _achievementCategoryButtons[i].onClick.AddListener(() => SelectAchievementCategory(idx));
            }
        }

        /// <summary>
        /// 업적 카테고리를 선택한다.
        /// </summary>
        public void SelectAchievementCategory(int categoryIndex)
        {
            var values = System.Enum.GetValues(typeof(AchievementCategory));
            if (categoryIndex < 0 || categoryIndex >= values.Length) return;

            _selectedAchievementCategory = (AchievementCategory)values.GetValue(categoryIndex);

            // 카테고리 버튼 하이라이트
            Sprite tabNormalSprite = null, tabSelectedSprite = null;
            var themeRef = UIThemeManager.Instance;
            if (themeRef != null)
            {
                var (normal, selected) = themeRef.GetTabSprites();
                tabNormalSprite = normal;
                tabSelectedSprite = selected;
            }

            if (_achievementCategoryBgs != null)
            {
                for (int i = 0; i < _achievementCategoryBgs.Length; i++)
                {
                    if (_achievementCategoryBgs[i] == null) continue;
                    var bg = _achievementCategoryBgs[i];
                    bool isSelected = i == categoryIndex;
                    var sprite = isSelected ? tabSelectedSprite : tabNormalSprite;
                    if (sprite != null)
                    {
                        bg.sprite = sprite;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;
                    }
                    else
                    {
                        DOTween.To(
                            () => bg.color,
                            c => bg.color = c,
                            isSelected ? subTabSelected : subTabNormal, 0.15f)
                            .SetUpdate(true).SetLink(gameObject);
                    }
                }
            }

            RefreshAchievementList();
        }

        private void RefreshAchievementList()
        {
            var system = AchievementSystem.Instance;
            if (system == null) return;

            var filtered = system.GetByCategory(_selectedAchievementCategory);

            int completed = 0;
            int claimable = 0;
            for (int i = 0; i < filtered.Count; i++)
            {
                if (filtered[i].isCompleted) completed++;
                if (filtered[i].isCompleted && !filtered[i].isClaimed) claimable++;
            }

            if (_achievementCountText != null)
            {
                string catName = (int)_selectedAchievementCategory < AchievementCategoryLabels.Length
                    ? AchievementCategoryLabels[(int)_selectedAchievementCategory]
                    : _selectedAchievementCategory.ToString();
                _achievementCountText.text = $"{catName}: {completed}/{filtered.Count} (수령 가능: {claimable})";
            }

            RefreshTitleDisplay();
        }

        private void RefreshTitleDisplay()
        {
            var system = AchievementSystem.Instance;
            if (system == null) return;

            if (_titleText != null)
            {
                string equippedId = system.EquippedTitleId;
                if (!string.IsNullOrEmpty(equippedId))
                {
                    _titleText.text = $"칭호: {equippedId}";
                }
                else
                {
                    _titleText.text = $"칭호: 없음 (해금: {system.UnlockedTitles.Count}개)";
                }
            }

            if (_titleUnequipButton != null)
                _titleUnequipButton.interactable = !string.IsNullOrEmpty(system.EquippedTitleId);
        }

        private void OnTitleUnequipClicked()
        {
            var system = AchievementSystem.Instance;
            if (system == null) return;

            system.UnequipTitle();
            RefreshTitleDisplay();
        }

        private void OnAchievementCompleted(MkLike.Quest.AchievementCompletedEvent evt)
        {
            RefreshAchievementList();
        }

        private void OnAchievementClaimed(MkLike.Quest.AchievementClaimedEvent evt)
        {
            RefreshAchievementList();
        }

        private void OnTitleUnlocked(TitleUnlockedEvent evt)
        {
            RefreshTitleDisplay();
        }

        private void OnDestroy()
        {
            if (subTabButtons != null)
            {
                for (int i = 0; i < subTabButtons.Length; i++)
                {
                    if (subTabButtons[i] != null)
                        subTabButtons[i].transform.DOKill();
                }
            }

            if (attendanceCheckBtn != null)
                attendanceCheckBtn.transform.DOKill();
            if (claimAllBtn != null)
                claimAllBtn.transform.DOKill();
            if (_blessingRerollButton != null)
                _blessingRerollButton.transform.DOKill();
            if (_titleUnequipButton != null)
                _titleUnequipButton.transform.DOKill();
        }
    }
}
