using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Dungeon;
using MkLike.Utils;
using MkLike.Core.Save;

namespace MkLike.UI
{
    /// <summary>
    /// 던전 + 보스 통합 팝업 패널.
    /// 5개 서브탭: 0: 성장 던전 / 1: 월드보스 / 2: 보스 레이드 / 3: 타워 / 4: 챌린지
    /// </summary>
    public class DungeonPanel : BasePopup
    {
        [Header("서브 탭")]
        [SerializeField] private Button[] _subTabButtons;
        [SerializeField] private Image[] _subTabBgs;
        [SerializeField] private GameObject[] _subTabContents;

        [Header("색상")]
        [SerializeField] private Color _subTabNormal = new Color(0.25f, 0.23f, 0.20f, 1f);
        [SerializeField] private Color _subTabSelected = new Color(0.45f, 0.40f, 0.33f, 1f);

        [Header("성장 던전 탭")]
        [SerializeField] private DungeonDataSO[] _dungeonCatalog;
        [SerializeField] private TextMeshProUGUI _keyCountText;
        [SerializeField] private DungeonRowUI[] _dungeonRows;
        [SerializeField] private TMP_Text _recommendDungeonText;

        [Header("월드보스 탭")]
        [SerializeField] private TextMeshProUGUI _worldBossStageText;
        [SerializeField] private TextMeshProUGUI _worldBossHpText;
        [SerializeField] private TextMeshProUGUI _worldBossBestDamageText;
        [SerializeField] private TextMeshProUGUI _worldBossResultText;
        [SerializeField] private Button _worldBossChallengeButton;

        [Header("보스 레이드 탭")]
        [SerializeField] private TextMeshProUGUI _raidAttemptsText;
        [SerializeField] private TextMeshProUGUI _raidResultText;
        [SerializeField] private Button[] _raidDifficultyButtons;
        [SerializeField] private Image[] _raidDifficultyBgs;
        [SerializeField] private Button _raidExecuteButton;

        [Header("타워 탭")]
        [SerializeField] private TMP_Text _towerFloorText;
        [SerializeField] private TMP_Text _towerHighestText;
        [SerializeField] private TMP_Text _towerZoneText;
        [SerializeField] private TMP_Text _towerTitleText;
        [SerializeField] private Slider _towerProgressSlider;
        [SerializeField] private TMP_Text _towerProgressText;
        [SerializeField] private TMP_Text _towerGimmickText;
        [SerializeField] private TMP_Text _towerPrestigeText;
        [SerializeField] private Button _towerPrestigeButton;

        [Header("타워 탭 - 랭킹")]
        [SerializeField] private TMP_Text _towerOverallRankText;
        [SerializeField] private TMP_Text _towerWeeklyRankText;
        [SerializeField] private Button _towerWeeklyRewardButton;
        [SerializeField] private TMP_Text _towerWeeklyRewardText;

        [Header("타워 탭 - 챌린지")]
        // 챌린지 바로가기 제거됨 — SerializeField 유지 (씬 직렬화 안전)
        [SerializeField] private Button _challengeShortcutButton;
        [SerializeField] private TMP_Text _challengeUnlockText;

        private int _currentSubTab = -1;
        private int _selectedRaidDifficulty;
        private CombatStats _combatStats;
        private CancellationTokenSource _flashCts;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
            WireCloseButton();

            // 서브 탭 버튼 연결
            if (_subTabButtons != null)
            {
                for (int i = 0; i < _subTabButtons.Length; i++)
                {
                    int idx = i;
                    if (_subTabButtons[i] != null)
                        _subTabButtons[i].onClick.AddListener(() => SwitchSubTab(idx));
                }
            }

            // 월드보스 도전 버튼
            if (_worldBossChallengeButton != null)
                _worldBossChallengeButton.onClick.AddListener(OnWorldBossChallenge);

            // 보스 레이드 난이도 버튼
            if (_raidDifficultyButtons != null)
            {
                for (int i = 0; i < _raidDifficultyButtons.Length; i++)
                {
                    int idx = i;
                    if (_raidDifficultyButtons[i] != null)
                        _raidDifficultyButtons[i].onClick.AddListener(() => SelectRaidDifficulty(idx));
                }
            }

            // 보스 레이드 실행 버튼
            if (_raidExecuteButton != null)
                _raidExecuteButton.onClick.AddListener(OnRaidExecute);

            // 타워 프레스티지 버튼
            if (_towerPrestigeButton != null)
                _towerPrestigeButton.onClick.AddListener(OnPrestigeClicked);

            // 타워 주간 보상 버튼
            if (_towerWeeklyRewardButton != null)
                _towerWeeklyRewardButton.onClick.AddListener(OnWeeklyRewardClicked);

        }

        private void OnEnable()
        {
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<TowerFloorReachedEvent>.Subscribe(OnTowerFloorReached);
            EventBus<PrestigeExecutedEvent>.Subscribe(OnPrestigeExecuted);
        }

        private void OnDisable()
        {
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnTowerFloorReached);
            EventBus<PrestigeExecutedEvent>.Unsubscribe(OnPrestigeExecuted);
        }

        protected override void OnShow()
        {
            FindPlayerRefs();
            EnsureDungeonCatalog();
            // 패널이 다시 열릴 때 서브탭 상태를 리셋하여 SwitchSubTab이 전체 전환을 수행
            _currentSubTab = -1;
            SwitchSubTab(0);
        }

        // 2026-04-23 정적 캐시: 인스턴스별 재스캔 방지 (세션 1회)
        private static DungeonDataSO[] _sharedDungeonCatalog;

        private void EnsureDungeonCatalog()
        {
            if (_dungeonCatalog != null && _dungeonCatalog.Length > 0) return;

            if (_sharedDungeonCatalog != null && _sharedDungeonCatalog.Length > 0)
            {
                _dungeonCatalog = _sharedDungeonCatalog;
                return;
            }

            // Resources/Data/Dungeons/ 에서 자동 로드
            var loaded = Resources.LoadAll<DungeonDataSO>("Data/Dungeons");
            if (loaded == null || loaded.Length == 0)
                loaded = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");

            if (loaded != null && loaded.Length > 0)
            {
                _sharedDungeonCatalog = loaded;
                _dungeonCatalog = loaded;
                Debug.Log($"[DungeonPanel] 던전 카탈로그 자동 로드: {loaded.Length}개");
            }
        }

        /// <summary>
        /// 서브탭을 전환한다.
        /// 0: 성장 던전, 1: 월드보스, 2: 보스 레이드, 3: 타워, 4: 챌린지
        /// </summary>
        public void SwitchSubTab(int index)
        {
            if (_subTabContents == null || index < 0 || index >= _subTabContents.Length)
            {
                Debug.LogWarning($"[DungeonPanel] SwitchSubTab({index}) 실패: contents={_subTabContents?.Length ?? -1}");
                return;
            }

            if (index == _currentSubTab)
            {
                Refresh();
                return;
            }

            // 테마 탭 스프라이트 로드
            Sprite tabNormalSprite = null, tabSelectedSprite = null;
            var theme = UIThemeManager.Instance;
            if (theme != null)
            {
                var (normal, selected) = theme.GetTabSprites();
                tabNormalSprite = normal;
                tabSelectedSprite = selected;
            }

            for (int i = 0; i < _subTabContents.Length; i++)
            {
                bool isActive = i == index;
                if (_subTabContents[i] != null)
                {
                    _subTabContents[i].SetActive(isActive);

                    // 비활성화되는 탭의 이전 페이드 트윈을 정리하고, alpha를 1로 복원
                    if (!isActive)
                    {
                        var prevCg = _subTabContents[i].GetComponent<CanvasGroup>();
                        if (prevCg != null)
                        {
                            DOTween.Kill(prevCg);
                            prevCg.alpha = 1f;
                        }
                    }
                }

                if (_subTabBgs != null && i < _subTabBgs.Length && _subTabBgs[i] != null)
                {
                    var bg = _subTabBgs[i];
                    var sprite = isActive ? tabSelectedSprite : tabNormalSprite;
                    if (sprite != null)
                    {
                        bg.sprite = sprite;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;
                    }
                    else
                    {
                        var targetColor = isActive ? _subTabSelected : _subTabNormal;
                        DOTween.Kill(bg);
                        DOTween.To(
                            () => bg.color,
                            c => bg.color = c,
                            targetColor, 0.15f)
                            .SetUpdate(true);
                    }
                }
            }

            // 페이드인 연출
            if (_subTabContents[index] != null)
            {
                var cg = _subTabContents[index].GetComponent<CanvasGroup>();
                if (cg == null) cg = _subTabContents[index].AddComponent<CanvasGroup>();
                DOTween.Kill(cg);
                cg.alpha = 1f;
            }

            _currentSubTab = index;
            Refresh();
        }

        private void Refresh()
        {
            switch (_currentSubTab)
            {
                case 0:
                    RefreshDungeonTab();
                    break;
                case 1:
                    RefreshWorldBossTab();
                    break;
                case 2:
                    RefreshBossRaidTab();
                    break;
                case 3:
                    RefreshTowerTab();
                    break;
                default:
                    break;
            }
        }

        private void FindPlayerRefs()
        {
            if (_combatStats != null) return;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                _combatStats = player.GetComponent<CombatStats>();
            }
        }

        // ── 성장 던전 탭 ──

        private void RefreshDungeonTab()
        {
            // 플래시 진행 중이면 전체 갱신을 건너뛴다 (텍스트 덮어쓰기 방지)
            if (_isFlashingKey)
            {
                Debug.Log("[DungeonPanel] RefreshDungeonTab — SKIPPED (flash in progress)");
                return;
            }

            int currentKeys = DungeonManager.Instance != null ? DungeonManager.Instance.CurrentKeys : 0;
            int maxKeys = DungeonManager.Instance != null ? DungeonManager.Instance.MaxKeys : 10;

            if (_keyCountText != null)
            {
                _keyCountText.text = $"{currentKeys} / {maxKeys}";

                // 열쇠 아이콘 추가 (1회만)
                var iconRegistry = IconRegistry.Instance;
                if (iconRegistry != null && _keyCountText.transform.childCount == 0)
                {
                    var keyIcon = iconRegistry.GetUIIcon("key");
                    if (keyIcon != null)
                    {
                        var iconGo = new GameObject("KeyIcon", typeof(RectTransform));
                        iconGo.transform.SetParent(_keyCountText.transform, false);
                        var iconRt = iconGo.GetComponent<RectTransform>();
                        iconRt.sizeDelta = new Vector2(24, 24);
                        iconRt.anchorMin = new Vector2(0, 0.5f);
                        iconRt.anchorMax = new Vector2(0, 0.5f);
                        iconRt.anchoredPosition = new Vector2(-28, 0);
                        var iconImg = iconGo.AddComponent<Image>();
                        iconImg.sprite = keyIcon;
                        iconImg.preserveAspect = true;
                    }
                }
            }

            if (_dungeonRows == null || _dungeonCatalog == null) return;

            for (int i = 0; i < _dungeonRows.Length && i < _dungeonCatalog.Length; i++)
            {
                var dungeon = _dungeonCatalog[i];
                var row = _dungeonRows[i];

                if (dungeon == null) continue;

                if (row.nameText != null)
                    row.nameText.text = dungeon.displayName;

                if (row.rewardText != null)
                {
                    string rewardName = DisplayNameUtils.GetCurrencyDisplayName(dungeon.mainRewardType);
                    row.rewardText.text = $"{rewardName} x{dungeon.baseRewardAmount}";
                }

                if (row.icon != null && dungeon.icon != null)
                    row.icon.sprite = dungeon.icon;

                if (row.enterButton != null)
                {
                    // 항상 클릭 가능 — 입장 불가 시 피드백을 표시하기 위해
                    row.enterButton.interactable = true;

                    // 버튼 이벤트 재바인딩
                    row.enterButton.onClick.RemoveAllListeners();
                    int dungeonIndex = i;
                    row.enterButton.onClick.AddListener(() => OnEnterDungeon(dungeonIndex));
                }
            }

            // 추천 던전 표시
            RefreshDungeonRecommendation();
        }

        private void RefreshDungeonRecommendation()
        {
            if (_recommendDungeonText == null) return;
            if (_isFlashingKey) return;
            if (RecommendationManager.Instance == null)
            {
                _recommendDungeonText.text = "";
                return;
            }

            var rec = RecommendationManager.Instance.GetRecommendedDungeon();
            _recommendDungeonText.text = $"추천: {rec.Reason}";
        }

        private void OnEnterDungeon(int index)
        {
            if (_dungeonCatalog == null || index < 0 || index >= _dungeonCatalog.Length) return;

            var dungeon = _dungeonCatalog[index];
            if (dungeon == null) return;

            if (DungeonManager.Instance == null) return;

            // 입장 불가 시 사유별 피드백 — GetDenyReason()으로 구체적 사유 표시
            if (!DungeonManager.Instance.CanEnter(dungeon))
            {
                string reason = DungeonManager.Instance.GetDenyReason(dungeon);
                if (string.IsNullOrEmpty(reason)) reason = "입장 불가";
                Debug.Log($"[DungeonPanel] {reason}");

                // 사유별 시각 피드백
                FlashKeyInsufficient(reason);

                // FloatingTextManager로 화면 중앙에도 표시
                if (FloatingTextManager.Instance != null)
                    FloatingTextManager.Instance.ShowSystemMessage(reason);

                // 해당 행의 버튼도 흔들림 (DOPunchScale은 WebGL에서 안전)
                if (_dungeonRows != null && index < _dungeonRows.Length && _dungeonRows[index].enterButton != null)
                {
                    var enterBtn = _dungeonRows[index].enterButton;
                    enterBtn.transform.DOKill();
                    enterBtn.transform.localScale = Vector3.one;
                    enterBtn.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 8, 0.5f)
                        .SetUpdate(true)
                        .SetLink(enterBtn.gameObject);
                }
                return;
            }

            if (!DungeonManager.Instance.EnterDungeon(dungeon)) return;

            // 현재는 즉시 완료 (score 1.0 = 완벽 클리어)
            DungeonManager.Instance.CompleteDungeon(dungeon, 1f);

            // 갱신은 DungeonCompletedEvent를 통해 자동으로 트리거된다
        }

        private void ShowDungeonEntryFeedback(DungeonDataSO dungeon)
        {
            int keys = DungeonManager.Instance.CurrentKeys;
            if (keys < dungeon.requiredKeys)
            {
                ShowToast($"열쇠 부족! (보유: {keys} / 필요: {dungeon.requiredKeys})");
                return;
            }

            long playerCp = _combatStats != null ? _combatStats.PowerScore : 0;
            if (dungeon.requiredCp > 0 && playerCp < dungeon.requiredCp)
            {
                ShowToast($"전투력 부족! (보유: {NumberFormatter.FormatKorean(playerCp)} / 필요: {NumberFormatter.FormatKorean(dungeon.requiredCp)})");
                return;
            }

            ShowToast("입장 조건을 충족하지 못했습니다.");
        }

        private void ShowToast(string message)
        {
            // UniTask 기반 피드백 (DOTween.To<Color> AOT 스트립 회피)
            FlashKeyInsufficient(message);

            // FloatingTextManager 보조 (보일 수도 있으므로)
            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.ShowSystemMessage(message);
        }

        // ── 열쇠/입장 부족 피드백 (풀스크린 오버레이 + 텍스트 플래시) ──
        private bool _isFlashingKey;

        private void FlashKeyInsufficient(string message)
        {
            Debug.Log($"[DungeonPanel] FlashKeyInsufficient — message={message}, _isFlashingKey={_isFlashingKey}");

            // 텍스트 플래시 피드백 (WebGL 안전 — 기존 TMP 텍스트만 변경)
            if (!_isFlashingKey)
                FlashKeyTextAsync(message).Forget();
        }

        private async UniTaskVoid FlashKeyTextAsync(string message)
        {
            _isFlashingKey = true;
            var token = this.GetCancellationTokenOnDestroy();

            // _keyCountText를 빨간색 "열쇠 부족!" 으로 변경 (보조 피드백)
            string originalText = _keyCountText != null ? _keyCountText.text : "";
            Color originalColor = _keyCountText != null ? _keyCountText.color : Color.white;
            float originalSize = _keyCountText != null ? _keyCountText.fontSize : 20f;

            if (_keyCountText != null)
            {
                Debug.Log($"[DungeonPanel] FlashKeyTextAsync — _keyCountText BEFORE: text='{_keyCountText.text}', color={_keyCountText.color}, fontSize={_keyCountText.fontSize}, gameObject={_keyCountText.gameObject.name}, active={_keyCountText.gameObject.activeInHierarchy}");
                _keyCountText.text = message;
                _keyCountText.color = new Color(1f, 0.2f, 0.2f, 1f);
                _keyCountText.fontSize = originalSize * 2.0f;
                _keyCountText.ForceMeshUpdate();
                Debug.Log($"[DungeonPanel] FlashKeyTextAsync — _keyCountText AFTER: text='{_keyCountText.text}', color={_keyCountText.color}, fontSize={_keyCountText.fontSize}");
                _keyCountText.transform.DOKill();
                _keyCountText.transform.DOPunchScale(Vector3.one * 0.15f, 0.5f, 3, 0.5f).SetUpdate(true).SetLink(gameObject);
            }
            else
            {
                Debug.LogError("[DungeonPanel] FlashKeyTextAsync — _keyCountText is NULL!");
            }

            // _recommendDungeonText 플래시 (연결된 경우에만)
            string originalRecommend = "";
            Color originalRecommendColor = Color.white;
            float originalRecommendSize = 16f;

            if (_recommendDungeonText != null)
            {
                originalRecommend = _recommendDungeonText.text;
                originalRecommendColor = _recommendDungeonText.color;
                originalRecommendSize = _recommendDungeonText.fontSize;
                _recommendDungeonText.text = message;
                _recommendDungeonText.color = new Color(1f, 0.25f, 0.25f, 1f);
                _recommendDungeonText.fontSize = originalRecommendSize * 1.5f;
                _recommendDungeonText.ForceMeshUpdate();
            }

            // 0.5초 후 상태 검증 (다른 곳에서 덮어쓰는지 확인)
            VerifyFlashState().Forget();

            // 2.5초 대기 후 복원
            try
            {
                await UniTask.Delay(2500, cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                _isFlashingKey = false;
                return;
            }

            if (_keyCountText != null)
            {
                _keyCountText.color = originalColor;
                _keyCountText.fontSize = originalSize;
                int keys = DungeonManager.Instance != null ? DungeonManager.Instance.CurrentKeys : 0;
                int max = DungeonManager.Instance != null ? DungeonManager.Instance.MaxKeys : 10;
                _keyCountText.text = $"{keys} / {max}";
            }
            if (_recommendDungeonText != null)
            {
                _recommendDungeonText.text = originalRecommend;
                _recommendDungeonText.color = originalRecommendColor;
                _recommendDungeonText.fontSize = originalRecommendSize;
            }

            _isFlashingKey = false;
        }

        private async UniTaskVoid VerifyFlashState()
        {
            try
            {
                await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            if (_keyCountText != null)
                Debug.Log($"[DungeonPanel] VerifyFlash 0.5s — _keyCountText: text='{_keyCountText.text}', color={_keyCountText.color}, _isFlashingKey={_isFlashingKey}");
            if (_recommendDungeonText != null)
                Debug.Log($"[DungeonPanel] VerifyFlash 0.5s — _recommendDungeonText: text='{_recommendDungeonText.text}', color={_recommendDungeonText.color}");
        }

        // ── 월드보스 탭 (제거됨) ──

        private void RefreshWorldBossTab()
        {
            // WorldBossSystem 제거됨 — 플레이스홀더만 표시
            if (_worldBossStageText != null)
                _worldBossStageText.text = "준비 중";
            if (_worldBossHpText != null)
                _worldBossHpText.text = "체력: ---";
            if (_worldBossBestDamageText != null)
                _worldBossBestDamageText.text = "최고 기록: ---";
            if (_worldBossResultText != null && string.IsNullOrEmpty(_worldBossResultText.text))
                _worldBossResultText.text = "월드보스 시스템 준비 중";
            if (_worldBossChallengeButton != null)
                _worldBossChallengeButton.interactable = false;
        }

        private void OnWorldBossChallenge()
        {
            // WorldBossSystem 제거됨 — 동작 없음
        }

        // ── 보스 레이드 탭 ──

        private void RefreshBossRaidTab()
        {
            if (BossRaidSystem.Instance == null)
            {
                // 시스템이 아직 초기화되지 않았을 때 플레이스홀더 표시
                if (_raidAttemptsText != null)
                    _raidAttemptsText.text = "0 / 3";
                if (_raidResultText != null && string.IsNullOrEmpty(_raidResultText.text))
                    _raidResultText.text = "난이도를 선택하고 레이드를 시작하세요!";
                if (_raidExecuteButton != null)
                    _raidExecuteButton.interactable = false;
                UpdateRaidDifficultyVisuals();
                return;
            }

            int remaining = BossRaidSystem.Instance.RemainingAttempts;
            int max = BossRaidSystem.Instance.MaxWeeklyAttempts;

            if (_raidAttemptsText != null)
                _raidAttemptsText.text = $"{remaining} / {max}";

            bool canRaid = BossRaidSystem.Instance.CanRaid() && _combatStats != null;

            if (_raidExecuteButton != null)
                _raidExecuteButton.interactable = canRaid;

            UpdateRaidDifficultyVisuals();
        }

        private void SelectRaidDifficulty(int index)
        {
            _selectedRaidDifficulty = Mathf.Clamp(index, 0, 2);
            UpdateRaidDifficultyVisuals();
        }

        private void UpdateRaidDifficultyVisuals()
        {
            if (_raidDifficultyBgs == null) return;

            for (int i = 0; i < _raidDifficultyBgs.Length; i++)
            {
                if (_raidDifficultyBgs[i] == null) continue;
                _raidDifficultyBgs[i].color = i == _selectedRaidDifficulty ? _subTabSelected : _subTabNormal;
            }
        }

        private void OnRaidExecute()
        {
            if (BossRaidSystem.Instance == null || _combatStats == null) return;

            var difficulty = (BossRaidSystem.Difficulty)_selectedRaidDifficulty;
            int atk = _combatStats.Atk;
            int hp = _combatStats.MaxHp;

            var result = BossRaidSystem.Instance.ExecuteRaid(difficulty, atk, hp);

            if (_raidResultText != null)
            {
                if (result.Success)
                {
                    _raidResultText.text = $"<color=#FFD700>성공!</color>\n골드 +{NumberFormatter.FormatKorean(result.GoldReward)}  루비 +{NumberFormatter.FormatKorean(result.RubyReward)}";
                }
                else
                {
                    _raidResultText.text = "<color=#FF4444>FAILED</color>\n전투력이 부족합니다.";
                }
            }

            RefreshBossRaidTab();
        }

        // ── 타워 탭 ──

        private void RefreshTowerTab()
        {
            RefreshTowerFloorInfo();
            RefreshTowerGimmickDisplay();
            RefreshTowerPrestigeInfo();
            RefreshTowerRankingInfo();
        }

        private void RefreshTowerFloorInfo()
        {
            var save = SaveManager.Instance?.CurrentData?.progress;
            int current = save?.currentFloor ?? 1;
            int highest = save?.maxFloor ?? 1;

            if (_towerFloorText != null)
                _towerFloorText.text = $"{current}층";
            if (_towerHighestText != null)
                _towerHighestText.text = $"최고: {highest}층";
            if (_towerZoneText != null)
                _towerZoneText.text = "";
            if (_towerTitleText != null)
                _towerTitleText.text = "";

            // 구간 진행도 (10층 단위)
            int zoneStart = (current / 10) * 10 + 1;
            int zoneEnd = zoneStart + 9;
            float progress = Mathf.Clamp01((float)(current - zoneStart) / (zoneEnd - zoneStart));

            if (_towerProgressSlider != null)
            {
                _towerProgressSlider.value = progress;

                // 테마 슬라이더 스프라이트 적용 (1회)
                var sliderTheme = UIThemeManager.Instance;
                if (sliderTheme != null && _towerProgressSlider.fillRect != null)
                {
                    var fillImg = _towerProgressSlider.fillRect.GetComponent<Image>();
                    if (fillImg != null)
                    {
                        var (fill, handle, bg) = sliderTheme.GetSliderSprites();
                        if (fill != null) { fillImg.sprite = fill; fillImg.type = Image.Type.Sliced; }
                    }
                }
            }
            if (_towerProgressText != null)
                _towerProgressText.text = $"{current - zoneStart + 1} / {zoneEnd - zoneStart + 1}";

        }

        private void RefreshTowerGimmickDisplay()
        {
            // TowerGimmickSystem 제거됨
            if (_towerGimmickText != null)
                _towerGimmickText.text = "";
        }

        private void RefreshTowerPrestigeInfo()
        {
            var prestige = PrestigeSystem.Instance;
            if (prestige == null)
            {
                if (_towerPrestigeText != null)
                    _towerPrestigeText.text = "";
                if (_towerPrestigeButton != null)
                    _towerPrestigeButton.interactable = false;
                return;
            }

            if (_towerPrestigeText != null)
            {
                int count = prestige.PrestigeCount;
                float bonus = prestige.TotalBonus;
                int floorsLeft = prestige.FloorsUntilPrestige();

                if (prestige.CanPrestige)
                {
                    _towerPrestigeText.text = $"환생 #{count}  |  보너스: {bonus:P0}\n<color=#FFD700>환생 가능!</color>";
                }
                else
                {
                    _towerPrestigeText.text = $"환생 #{count}  |  보너스: {bonus:P0}\n환생까지 {floorsLeft}층 남음";
                }
            }

            if (_towerPrestigeButton != null)
                _towerPrestigeButton.interactable = prestige.CanPrestige;
        }

        private void OnPrestigeClicked()
        {
            var prestige = PrestigeSystem.Instance;
            if (prestige == null || !prestige.CanPrestige) return;

            prestige.ExecutePrestige();
            // 갱신은 PrestigeExecutedEvent를 통해 자동으로 트리거된다
        }

        private void RefreshTowerRankingInfo()
        {
            var ranking = FloorRankingSystem.Instance;
            if (ranking == null)
            {
                if (_towerOverallRankText != null)
                    _towerOverallRankText.text = "";
                if (_towerWeeklyRankText != null)
                    _towerWeeklyRankText.text = "";
                if (_towerWeeklyRewardButton != null)
                    _towerWeeklyRewardButton.interactable = false;
                if (_towerWeeklyRewardText != null)
                    _towerWeeklyRewardText.text = "";
                return;
            }

            int overallRank = ranking.GetPlayerRank(RankingType.Overall);
            int weeklyRank = ranking.GetPlayerRank(RankingType.Weekly);

            if (_towerOverallRankText != null)
                _towerOverallRankText.text = overallRank > 0 ? $"전체 순위: {overallRank}위" : "전체 순위: -";

            if (_towerWeeklyRankText != null)
                _towerWeeklyRankText.text = weeklyRank > 0 ? $"주간 순위: {weeklyRank}위" : "주간 순위: -";

            bool canClaim = !ranking.IsWeeklyRewardClaimed && weeklyRank > 0;

            if (_towerWeeklyRewardButton != null)
                _towerWeeklyRewardButton.interactable = canClaim;

            if (_towerWeeklyRewardText != null)
            {
                if (ranking.IsWeeklyRewardClaimed)
                {
                    _towerWeeklyRewardText.text = "<color=#888888>수령 완료</color>";
                }
                else if (weeklyRank > 0)
                {
                    FloorRankingSystem.GetWeeklyReward(weeklyRank, out int ruby, out int weaponTicket);
                    _towerWeeklyRewardText.text = $"루비 {ruby} / 무기권 {weaponTicket}";
                }
                else
                {
                    _towerWeeklyRewardText.text = "";
                }
            }
        }

        private void OnWeeklyRewardClicked()
        {
            var ranking = FloorRankingSystem.Instance;
            if (ranking == null || ranking.IsWeeklyRewardClaimed) return;

            ranking.ClaimWeeklyReward();
            RefreshTowerRankingInfo();
        }

        // ── 이벤트 핸들러 ──

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            if (IsVisible)
            {
                Refresh();
            }
        }

        private void OnTowerFloorReached(TowerFloorReachedEvent evt)
        {
            if (IsVisible && _currentSubTab == 3)
            {
                RefreshTowerTab();
            }
        }

        private void OnPrestigeExecuted(PrestigeExecutedEvent evt)
        {
            if (IsVisible && _currentSubTab == 3)
            {
                RefreshTowerTab();
            }
        }

        private void WireCloseButton()
        {
            var closeBtns = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < closeBtns.Length; i++)
            {
                if (closeBtns[i].name == "CloseBtn")
                {
                    closeBtns[i].onClick.RemoveAllListeners();
                    closeBtns[i].onClick.AddListener(CloseViaTabController);

                    var img = closeBtns[i].GetComponent<Image>();
                    if (img != null && img.sprite == null)
                    {
                        var spr = Resources.Load<Sprite>("UI/Stone/Popup/popup_btn_close");
                        if (spr != null)
                        {
                            img.sprite = spr;
                            img.type = Image.Type.Simple;
                            img.preserveAspect = true;
                        }
                    }
                }
            }
        }

        private void CloseViaTabController()
        {
            // TabController를 통해 닫으면 탭 버튼 상태도 정상 동기화됨
            var tabCtrl = FindFirstObjectByType<TabController>();
            if (tabCtrl != null)
            {
                tabCtrl.CloseAll();
            }
            else
            {
                Hide();
            }
        }

        private void EnsureComponents()
        {
            // 에디터가 이미 패널을 구성했는지 확인 (Header 자식이 있으면 에디터 구성됨)
            bool editorBuilt = transform.Find("Header") != null;

            if (editorBuilt)
            {
                // 에디터 레이아웃 기반 — 탭 확장만 수행
                EnsureTabExtension();
                return;
            }

            // === 아래는 에디터 셋업 없이 런타임에서 처음부터 생성하는 경우 ===

            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            Color textWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
            Color textGold = new Color(1f, 0.85f, 0.1f, 1f);
            Color darkBg = new Color(0.1f, 0.1f, 0.18f, 0.95f);
            Color sectionBg = new Color(0.12f, 0.12f, 0.18f, 0.6f);
            var tm = UIThemeManager.Instance;

            // Root overlay background (full-screen dim)
            var rootBg = GetComponent<Image>();
            if (rootBg == null)
            {
                rootBg = gameObject.AddComponent<Image>();
                rootBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
                rootBg.raycastTarget = true;
            }
            var rootRT = GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.sizeDelta = Vector2.zero;
            rootRT.anchoredPosition = Vector2.zero;

            // Inner content panel (~90% centered)
            Transform contentPanel = transform.Find("ContentPanel");
            RectTransform contentRT;
            if (contentPanel == null)
            {
                var cpGo = new GameObject("ContentPanel", typeof(RectTransform));
                cpGo.transform.SetParent(transform, false);
                contentRT = cpGo.GetComponent<RectTransform>();
                var cpBgImg = cpGo.AddComponent<Image>();
                if (tm != null) tm.ApplyPanelBackground(cpBgImg);
                else cpBgImg.color = darkBg;
                contentPanel = cpGo.transform;
            }
            else
            {
                contentRT = contentPanel.GetComponent<RectTransform>();
            }
            contentRT.anchorMin = new Vector2(0.05f, 0.05f);
            contentRT.anchorMax = new Vector2(0.95f, 0.95f);
            contentRT.sizeDelta = Vector2.zero;
            contentRT.anchoredPosition = Vector2.zero;

            // Title text — Header에서 이미 표시하므로 비활성화
            Transform titleTf = contentPanel.Find("TitleText");
            if (titleTf != null) titleTf.gameObject.SetActive(false);

            // Close button — Header에서 이미 표시하므로 비활성화
            Transform closeTf = contentPanel.Find("CloseBtn");
            if (closeTf != null) closeTf.gameObject.SetActive(false);

            // Tab bar (below title, height 44)
            Transform tabBar = contentPanel.Find("TabBar");
            RectTransform tabBarRT;
            if (tabBar == null)
            {
                var tabBarGo = new GameObject("TabBar", typeof(RectTransform));
                tabBarGo.transform.SetParent(contentPanel, false);
                tabBarRT = tabBarGo.GetComponent<RectTransform>();
                var hlg = tabBarGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 4;
                hlg.padding = new RectOffset(4, 4, 2, 2);
                tabBar = tabBarGo.transform;
            }
            else
            {
                tabBarRT = tabBar.GetComponent<RectTransform>();
            }
            tabBarRT.anchorMin = new Vector2(0f, 1f);
            tabBarRT.anchorMax = new Vector2(1f, 1f);
            tabBarRT.pivot = new Vector2(0.5f, 1f);
            tabBarRT.anchoredPosition = new Vector2(0f, -5f);
            tabBarRT.sizeDelta = new Vector2(0f, 44f);

            // Content area (below tab bar)
            Transform contentArea = contentPanel.Find("ContentArea");
            RectTransform contentAreaRT;
            if (contentArea == null)
            {
                var caGo = new GameObject("ContentArea", typeof(RectTransform));
                caGo.transform.SetParent(contentPanel, false);
                contentAreaRT = caGo.GetComponent<RectTransform>();
                contentArea = caGo.transform;
            }
            else
            {
                contentAreaRT = contentArea.GetComponent<RectTransform>();
            }
            contentAreaRT.anchorMin = new Vector2(0f, 0f);
            contentAreaRT.anchorMax = new Vector2(1f, 1f);
            contentAreaRT.pivot = new Vector2(0.5f, 0.5f);
            contentAreaRT.offsetMin = new Vector2(8f, 8f);
            contentAreaRT.offsetMax = new Vector2(-8f, -55f);

            // ── Sub tabs (5: 성장던전/월드보스/보스레이드/타워/챌린지) ──
            int tabCount = 5;
            string[] tabLabels = { "성장던전", "월드보스", "보스레이드", "타워", "챌린지" };

            if (_subTabButtons == null || _subTabButtons.Length < tabCount)
            {
                // 에디터에서 이미 일부 버튼을 생성한 경우 보존
                var oldBtns = _subTabButtons;
                var oldBgs = _subTabBgs;
                int existBtnCount = oldBtns != null ? oldBtns.Length : 0;

                _subTabButtons = new Button[tabCount];
                _subTabBgs = new Image[tabCount];

                for (int i = 0; i < tabCount; i++)
                {
                    if (i < existBtnCount && oldBtns[i] != null)
                    {
                        _subTabButtons[i] = oldBtns[i];
                        _subTabBgs[i] = oldBgs != null && i < oldBgs.Length ? oldBgs[i] : oldBtns[i].GetComponent<Image>();
                    }
                    else
                    {
                        var go = new GameObject($"SubTabBtn_{i}", typeof(RectTransform));
                        go.transform.SetParent(tabBar, false);
                        var btnBg = go.AddComponent<Image>();
                        if (tm != null)
                        {
                            var (tabN, tabS) = tm.GetTabSprites();
                            UIThemeManager.ApplySpriteOrColor(btnBg, tabN, _subTabNormal);
                        }
                        else btnBg.color = _subTabNormal;
                        _subTabBgs[i] = btnBg;
                        _subTabButtons[i] = go.AddComponent<Button>();
                        _subTabButtons[i].targetGraphic = btnBg;

                        var lbl = new GameObject("Label", typeof(RectTransform));
                        lbl.transform.SetParent(go.transform, false);
                        var lblRT = lbl.GetComponent<RectTransform>();
                        lblRT.anchorMin = Vector2.zero;
                        lblRT.anchorMax = Vector2.one;
                        lblRT.sizeDelta = Vector2.zero;
                        lblRT.anchoredPosition = Vector2.zero;
                        var tmp = lbl.AddComponent<TextMeshProUGUI>();
                        tmp.text = tabLabels[i];
                        tmp.fontSize = 13f;
                        tmp.fontStyle = FontStyles.Bold;
                        tmp.color = textWhite;
                        tmp.alignment = TextAlignmentOptions.Center;
                    }
                }
            }
            else if (_subTabBgs == null || _subTabBgs.Length < tabCount)
            {
                _subTabBgs = new Image[tabCount];
                for (int i = 0; i < tabCount; i++)
                {
                    if (_subTabButtons[i] != null)
                        _subTabBgs[i] = _subTabButtons[i].GetComponent<Image>();
                }
            }

            // Sub tab content containers (each fills contentArea)
            if (_subTabContents == null || _subTabContents.Length < tabCount)
            {
                // 에디터에서 이미 일부 탭을 생성한 경우, 기존 항목을 보존하고 부족한 탭만 추가
                var oldContents = _subTabContents;
                _subTabContents = new GameObject[tabCount];
                int existingCount = oldContents != null ? oldContents.Length : 0;

                for (int i = 0; i < tabCount; i++)
                {
                    if (i < existingCount && oldContents[i] != null)
                    {
                        // 에디터가 생성한 기존 탭 보존 + contentArea로 re-parent
                        _subTabContents[i] = oldContents[i];
                        if (oldContents[i].transform.parent != contentArea)
                            oldContents[i].transform.SetParent(contentArea, false);
                        var existRT = oldContents[i].GetComponent<RectTransform>();
                        existRT.anchorMin = Vector2.zero;
                        existRT.anchorMax = Vector2.one;
                        existRT.sizeDelta = Vector2.zero;
                        existRT.anchoredPosition = Vector2.zero;
                    }
                    else
                    {
                        // 새 탭 컨테이너 생성
                        var go = new GameObject($"SubTabContent_{i}", typeof(RectTransform));
                        go.transform.SetParent(contentArea, false);
                        var rt = go.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.sizeDelta = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                        go.SetActive(false);
                        _subTabContents[i] = go;
                    }
                }
            }

            // ════════════════════════════════════════
            // TAB 0: 성장 던전
            // ════════════════════════════════════════
            Transform tab0 = _subTabContents[0].transform;
            {
                // Ensure VLG on tab content
                if (tab0.GetComponent<VerticalLayoutGroup>() == null)
                {
                    var vlg = tab0.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 6;
                    vlg.padding = new RectOffset(8, 8, 8, 8);
                }

                // Key count header
                if (_keyCountText == null)
                {
                    var go = new GameObject("KeyCountText", typeof(RectTransform));
                    go.transform.SetParent(tab0, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
                    var bg = go.AddComponent<Image>();
                    bg.color = sectionBg;
                    _keyCountText = CreateTextChild(go.transform, "KeyLabel", 18f, textWhite, TextAlignmentOptions.Center);
                }

                // Dungeon rows
                if (_dungeonRows == null || _dungeonRows.Length == 0)
                {
                    int rowCount = _dungeonCatalog != null ? _dungeonCatalog.Length : 5;
                    _dungeonRows = new DungeonRowUI[rowCount];
                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = new GameObject($"DungeonRow_{i}", typeof(RectTransform));
                        row.transform.SetParent(tab0, false);
                        var rowRT = row.GetComponent<RectTransform>();
                        rowRT.sizeDelta = new Vector2(0f, 72f);
                        var rowBg = row.AddComponent<Image>();
                        if (tm != null && tm.FrameBackground != null)
                        {
                            rowBg.sprite = tm.FrameBackground;
                            rowBg.type = Image.Type.Sliced;
                            rowBg.color = (i % 2 == 0)
                                ? new Color(0.85f, 0.85f, 0.85f, 1f)
                                : new Color(0.75f, 0.75f, 0.75f, 1f);
                        }
                        else rowBg.color = new Color(0.14f, 0.14f, 0.2f, 0.8f);
                        var hlg = row.AddComponent<HorizontalLayoutGroup>();
                        hlg.childAlignment = TextAnchor.MiddleLeft;
                        hlg.childControlWidth = true;
                        hlg.childControlHeight = false;
                        hlg.childForceExpandWidth = false;
                        hlg.spacing = 8;
                        hlg.padding = new RectOffset(8, 8, 4, 4);

                        // Icon
                        var iconGo = new GameObject("Icon", typeof(RectTransform));
                        iconGo.transform.SetParent(row.transform, false);
                        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);
                        var iconImg = iconGo.AddComponent<Image>();
                        iconImg.preserveAspect = true;
                        var iconLE = iconGo.AddComponent<LayoutElement>();
                        iconLE.preferredWidth = 56f;
                        iconLE.minWidth = 56f;

                        // Info column
                        var infoGo = new GameObject("Info", typeof(RectTransform));
                        infoGo.transform.SetParent(row.transform, false);
                        infoGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 60f);
                        var infoLE = infoGo.AddComponent<LayoutElement>();
                        infoLE.flexibleWidth = 1f;
                        infoLE.preferredWidth = 200f;
                        infoLE.minWidth = 150f;
                        var infoVlg = infoGo.AddComponent<VerticalLayoutGroup>();
                        infoVlg.childAlignment = TextAnchor.MiddleLeft;
                        infoVlg.childControlWidth = true;
                        infoVlg.childControlHeight = true;
                        infoVlg.childForceExpandWidth = true;
                        infoVlg.childForceExpandHeight = true;
                        infoVlg.spacing = 2;

                        var nameTmp = CreateTextChild(infoGo.transform, "Name", 16f, textWhite, TextAlignmentOptions.Left);
                        nameTmp.fontStyle = FontStyles.Bold;
                        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
                        nameTmp.rectTransform.sizeDelta = new Vector2(180f, 28f);
                        var rewardTmp = CreateTextChild(infoGo.transform, "Reward", 13f, new Color(0.8f, 0.8f, 0.6f), TextAlignmentOptions.Left);
                        rewardTmp.textWrappingMode = TextWrappingModes.NoWrap;
                        rewardTmp.rectTransform.sizeDelta = new Vector2(180f, 24f);

                        // Enter button
                        var btnGo = new GameObject("EnterBtn", typeof(RectTransform));
                        btnGo.transform.SetParent(row.transform, false);
                        btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 40f);
                        var btnLE = btnGo.AddComponent<LayoutElement>();
                        btnLE.preferredWidth = 70f;
                        btnLE.minWidth = 70f;
                        var btnBg = btnGo.AddComponent<Image>();
                        var btn = btnGo.AddComponent<Button>();
                        btn.targetGraphic = btnBg;
                        if (tm != null) tm.ApplyConfirmButton(btnBg);
                        else btnBg.color = new Color(0.25f, 0.45f, 0.30f, 1f);
                        CreateTextChild(btnGo.transform, "BtnLabel", 15f, textWhite, TextAlignmentOptions.Center, "입장");

                        _dungeonRows[i] = new DungeonRowUI
                        {
                            nameText = nameTmp,
                            rewardText = rewardTmp,
                            icon = iconImg,
                            enterButton = btn
                        };
                    }
                }

                // Recommend text
                if (_recommendDungeonText == null)
                {
                    var go = new GameObject("RecommendDungeonText", typeof(RectTransform));
                    go.transform.SetParent(tab0, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
                    _recommendDungeonText = go.AddComponent<TextMeshProUGUI>();
                    _recommendDungeonText.fontSize = 14f;
                    _recommendDungeonText.color = textGold;
                    _recommendDungeonText.alignment = TextAlignmentOptions.Center;
                }
            }

            // ════════════════════════════════════════
            // TAB 1: 월드보스
            // ════════════════════════════════════════
            Transform tab1 = _subTabContents[1].transform;
            {
                if (tab1.GetComponent<VerticalLayoutGroup>() == null)
                {
                    var vlg = tab1.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10;
                    vlg.padding = new RectOffset(12, 12, 12, 12);
                }

                if (_worldBossStageText == null)
                {
                    var go = CreateSectionRow(tab1, "WorldBossStageText", 40f, sectionBg);
                    _worldBossStageText = CreateTextChild(go.transform, "Label", 22f, textWhite, TextAlignmentOptions.Center);
                    _worldBossStageText.fontStyle = FontStyles.Bold;
                }
                if (_worldBossHpText == null)
                {
                    var go = CreateSectionRow(tab1, "WorldBossHpText", 32f, sectionBg);
                    _worldBossHpText = CreateTextChild(go.transform, "Label", 18f, textWhite, TextAlignmentOptions.Center);
                }
                if (_worldBossBestDamageText == null)
                {
                    var go = CreateSectionRow(tab1, "WorldBossBestDmgText", 28f, sectionBg);
                    _worldBossBestDamageText = CreateTextChild(go.transform, "Label", 16f, textGold, TextAlignmentOptions.Center);
                }
                if (_worldBossResultText == null)
                {
                    var go = CreateSectionRow(tab1, "WorldBossResultText", 50f, sectionBg);
                    _worldBossResultText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_worldBossChallengeButton == null)
                {
                    var go = new GameObject("WorldBossChallengeBtn", typeof(RectTransform));
                    go.transform.SetParent(tab1, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 50f);
                    var btnBg = go.AddComponent<Image>();
                    _worldBossChallengeButton = go.AddComponent<Button>();
                    _worldBossChallengeButton.targetGraphic = btnBg;
                    if (tm != null) tm.ApplyButtonFull(_worldBossChallengeButton);
                    else btnBg.color = new Color(0.4f, 0.3f, 0.3f, 0.9f);
                    CreateTextChild(go.transform, "BtnLabel", 18f, textWhite, TextAlignmentOptions.Center, "도전");
                }
            }

            // ════════════════════════════════════════
            // TAB 2: 보스 레이드
            // ════════════════════════════════════════
            Transform tab2 = _subTabContents[2].transform;
            {
                if (tab2.GetComponent<VerticalLayoutGroup>() == null)
                {
                    var vlg = tab2.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 10;
                    vlg.padding = new RectOffset(12, 12, 12, 12);
                }

                if (_raidAttemptsText == null)
                {
                    var go = CreateSectionRow(tab2, "RaidAttemptsText", 32f, sectionBg);
                    _raidAttemptsText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }

                // Difficulty buttons row
                if (_raidDifficultyButtons == null || _raidDifficultyButtons.Length == 0)
                {
                    var diffRow = new GameObject("RaidDiffRow", typeof(RectTransform));
                    diffRow.transform.SetParent(tab2, false);
                    diffRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);
                    var hlg = diffRow.AddComponent<HorizontalLayoutGroup>();
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = true;
                    hlg.childForceExpandHeight = true;
                    hlg.spacing = 8;
                    hlg.padding = new RectOffset(4, 4, 4, 4);

                    _raidDifficultyButtons = new Button[3];
                    _raidDifficultyBgs = new Image[3];
                    string[] labels = { "쉬움", "보통", "어려움" };
                    for (int i = 0; i < 3; i++)
                    {
                        var go = new GameObject($"RaidDiffBtn_{i}", typeof(RectTransform));
                        go.transform.SetParent(diffRow.transform, false);
                        _raidDifficultyBgs[i] = go.AddComponent<Image>();
                        if (tm != null)
                        {
                            var (tabN2, tabS2) = tm.GetTabSprites();
                            UIThemeManager.ApplySpriteOrColor(_raidDifficultyBgs[i], tabN2, _subTabNormal);
                        }
                        else _raidDifficultyBgs[i].color = _subTabNormal;
                        _raidDifficultyButtons[i] = go.AddComponent<Button>();
                        _raidDifficultyButtons[i].targetGraphic = _raidDifficultyBgs[i];
                        CreateTextChild(go.transform, "Label", 15f, textWhite, TextAlignmentOptions.Center, labels[i]);
                    }
                }

                if (_raidResultText == null)
                {
                    var go = CreateSectionRow(tab2, "RaidResultText", 50f, sectionBg);
                    _raidResultText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }

                if (_raidExecuteButton == null)
                {
                    var go = new GameObject("RaidExecuteBtn", typeof(RectTransform));
                    go.transform.SetParent(tab2, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 50f);
                    var btnBg = go.AddComponent<Image>();
                    _raidExecuteButton = go.AddComponent<Button>();
                    _raidExecuteButton.targetGraphic = btnBg;
                    if (tm != null) tm.ApplyButtonFull(_raidExecuteButton);
                    else btnBg.color = new Color(0.4f, 0.3f, 0.3f, 0.9f);
                    CreateTextChild(go.transform, "BtnLabel", 18f, textWhite, TextAlignmentOptions.Center, "레이드 실행");
                }
            }

            // ════════════════════════════════════════
            // TAB 3: 타워
            // ════════════════════════════════════════
            Transform tab3 = _subTabContents[3].transform;
            {
                // Scrollable tower tab
                ScrollRect tab3Scroll = tab3.GetComponent<ScrollRect>();
                Transform towerContent;
                if (tab3Scroll == null)
                {
                    tab3.gameObject.AddComponent<Image>().color = Color.clear;
                    tab3Scroll = tab3.gameObject.AddComponent<ScrollRect>();
                    tab3Scroll.horizontal = false;
                    tab3Scroll.vertical = true;
                    tab3Scroll.movementType = ScrollRect.MovementType.Clamped;

                    var viewport = new GameObject("Viewport", typeof(RectTransform));
                    viewport.transform.SetParent(tab3, false);
                    var vpRT = viewport.GetComponent<RectTransform>();
                    vpRT.anchorMin = Vector2.zero;
                    vpRT.anchorMax = Vector2.one;
                    vpRT.sizeDelta = Vector2.zero;
                    vpRT.anchoredPosition = Vector2.zero;
                    var vpMask = viewport.AddComponent<Mask>();
                    vpMask.showMaskGraphic = false;
                    viewport.AddComponent<Image>().color = Color.white;

                    var content = new GameObject("TowerContent", typeof(RectTransform));
                    content.transform.SetParent(viewport.transform, false);
                    var cRT = content.GetComponent<RectTransform>();
                    cRT.anchorMin = new Vector2(0f, 1f);
                    cRT.anchorMax = new Vector2(1f, 1f);
                    cRT.pivot = new Vector2(0.5f, 1f);
                    cRT.sizeDelta = new Vector2(0f, 0f);
                    var csf = content.AddComponent<ContentSizeFitter>();
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var vlg = content.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 8;
                    vlg.padding = new RectOffset(8, 8, 8, 8);

                    tab3Scroll.viewport = vpRT;
                    tab3Scroll.content = cRT;
                    towerContent = content.transform;
                }
                else
                {
                    towerContent = tab3Scroll.content;
                }

                // Floor info
                if (_towerFloorText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerFloorText", 44f, sectionBg);
                    _towerFloorText = CreateTextChild(go.transform, "Label", 24f, textWhite, TextAlignmentOptions.Center);
                    _towerFloorText.fontStyle = FontStyles.Bold;
                }
                if (_towerHighestText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerHighestText", 28f, sectionBg);
                    _towerHighestText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerZoneText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerZoneText", 28f, sectionBg);
                    _towerZoneText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerTitleText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerTitleText", 28f, sectionBg);
                    _towerTitleText = CreateTextChild(go.transform, "Label", 16f, textGold, TextAlignmentOptions.Center);
                    _towerTitleText.fontStyle = FontStyles.Bold;
                }

                // Progress slider
                if (_towerProgressSlider == null)
                {
                    var sliderGo = new GameObject("TowerProgressSlider", typeof(RectTransform));
                    sliderGo.transform.SetParent(towerContent, false);
                    sliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
                    var sliderBg = sliderGo.AddComponent<Image>();
                    sliderBg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

                    var fillArea = new GameObject("FillArea", typeof(RectTransform));
                    fillArea.transform.SetParent(sliderGo.transform, false);
                    var faRT = fillArea.GetComponent<RectTransform>();
                    faRT.anchorMin = new Vector2(0f, 0.15f);
                    faRT.anchorMax = new Vector2(1f, 0.85f);
                    faRT.sizeDelta = Vector2.zero;
                    faRT.anchoredPosition = Vector2.zero;

                    var fill = new GameObject("Fill", typeof(RectTransform));
                    fill.transform.SetParent(fillArea.transform, false);
                    var fillRT = fill.GetComponent<RectTransform>();
                    fillRT.anchorMin = Vector2.zero;
                    fillRT.anchorMax = new Vector2(0f, 1f);
                    fillRT.sizeDelta = Vector2.zero;
                    fillRT.anchoredPosition = Vector2.zero;
                    fill.AddComponent<Image>().color = new Color(0.3f, 0.6f, 0.3f, 1f);

                    _towerProgressSlider = sliderGo.AddComponent<Slider>();
                    _towerProgressSlider.interactable = false;
                    _towerProgressSlider.fillRect = fillRT;
                }
                if (_towerProgressText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerProgressText", 24f, Color.clear);
                    _towerProgressText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerGimmickText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerGimmickText", 36f, sectionBg);
                    _towerGimmickText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Left);
                }
                // Prestige section
                if (_towerPrestigeText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerPrestigeText", 44f, sectionBg);
                    _towerPrestigeText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerPrestigeButton == null)
                {
                    var go = new GameObject("TowerPrestigeBtn", typeof(RectTransform));
                    go.transform.SetParent(towerContent, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
                    var btnBg = go.AddComponent<Image>();
                    _towerPrestigeButton = go.AddComponent<Button>();
                    _towerPrestigeButton.targetGraphic = btnBg;
                    if (tm != null) tm.ApplyButtonFull(_towerPrestigeButton);
                    else btnBg.color = new Color(0.5f, 0.35f, 0.2f, 0.9f);
                    CreateTextChild(go.transform, "BtnLabel", 17f, textWhite, TextAlignmentOptions.Center, "환생");
                }

                // Ranking section
                if (_towerOverallRankText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerOverallRankText", 28f, sectionBg);
                    _towerOverallRankText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerWeeklyRankText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerWeeklyRankText", 28f, sectionBg);
                    _towerWeeklyRankText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
                }
                if (_towerWeeklyRewardButton == null)
                {
                    var go = new GameObject("TowerWeeklyRewardBtn", typeof(RectTransform));
                    go.transform.SetParent(towerContent, false);
                    go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
                    var btnBg = go.AddComponent<Image>();
                    _towerWeeklyRewardButton = go.AddComponent<Button>();
                    _towerWeeklyRewardButton.targetGraphic = btnBg;
                    if (tm != null) tm.ApplyButtonFull(_towerWeeklyRewardButton);
                    else btnBg.color = new Color(0.3f, 0.4f, 0.3f, 0.9f);
                    CreateTextChild(go.transform, "BtnLabel", 15f, textWhite, TextAlignmentOptions.Center, "주간 보상 수령");
                }
                if (_towerWeeklyRewardText == null)
                {
                    var go = CreateSectionRow(towerContent, "TowerWeeklyRewardText", 28f, Color.clear);
                    _towerWeeklyRewardText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Center);
                }

            }
        }

        private static GameObject CreateSectionRow(Transform parent, string name, float height, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);
            if (bgColor.a > 0.01f)
            {
                var img = go.AddComponent<Image>();
                var tm = UIThemeManager.Instance;
                if (tm != null && tm.FrameBackground != null)
                {
                    img.sprite = tm.FrameBackground;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.75f, 0.75f, 0.7f, 1f);
                }
                else img.color = bgColor;
            }
            return go;
        }

        private static TextMeshProUGUI CreateTextChild(Transform parent, string name, float fontSize, Color color,
            TextAlignmentOptions alignment, string text = "")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            return tmp;
        }

        /// <summary>
        /// 에디터가 이미 3개 서브탭(성장던전/월드보스/보스레이드)을 구성한 경우,
        /// 타워(3) 탭만 추가한다.
        /// 에디터가 와이어링한 _dungeonRows, _keyCountText 등은 그대로 유지.
        /// </summary>
        private void EnsureTabExtension()
        {
            int tabCount = 4;
            string[] tabLabels = { "성장던전", "월드보스", "보스레이드", "타워" };

            // ContentArea 찾기 (에디터가 생성한 것 — 계층 구조에서 재귀 탐색)
            Transform contentArea = transform.Find("ContentArea");
            if (contentArea == null)
                contentArea = FindChildRecursive(transform, "ContentArea");
            Transform tabBar = transform.Find("SubTabBar");
            if (tabBar == null)
                tabBar = FindChildRecursive(transform, "SubTabBar");

            // _subTabButtons 확장 (3 → 5)
            if (_subTabButtons == null || _subTabButtons.Length < tabCount)
            {
                var oldBtns = _subTabButtons;
                var oldBgs = _subTabBgs;
                int existCount = oldBtns != null ? oldBtns.Length : 0;
                _subTabButtons = new Button[tabCount];
                _subTabBgs = new Image[tabCount];

                for (int i = 0; i < existCount; i++)
                {
                    _subTabButtons[i] = oldBtns[i];
                    if (oldBgs != null && i < oldBgs.Length)
                        _subTabBgs[i] = oldBgs[i];
                }

                Color textWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
                for (int i = existCount; i < tabCount; i++)
                {
                    if (tabBar == null) break;
                    var go = new GameObject($"SubTabBtn_{i}", typeof(RectTransform));
                    go.transform.SetParent(tabBar, false);
                    var btnBg = go.AddComponent<Image>();
                    btnBg.color = _subTabNormal;
                    _subTabBgs[i] = btnBg;
                    _subTabButtons[i] = go.AddComponent<Button>();
                    _subTabButtons[i].targetGraphic = btnBg;

                    var lbl = new GameObject("Label", typeof(RectTransform));
                    lbl.transform.SetParent(go.transform, false);
                    var lblRT = lbl.GetComponent<RectTransform>();
                    lblRT.anchorMin = Vector2.zero;
                    lblRT.anchorMax = Vector2.one;
                    lblRT.sizeDelta = Vector2.zero;
                    var tmp = lbl.AddComponent<TextMeshProUGUI>();
                    tmp.text = tabLabels[i];
                    tmp.fontSize = 13f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = textWhite;
                    tmp.alignment = TextAlignmentOptions.Center;
                }
            }

            // _subTabContents 확장 + null 엔트리 복구
            bool needsExpansion = _subTabContents == null || _subTabContents.Length < tabCount;
            bool hasNullEntries = false;
            if (_subTabContents != null)
            {
                for (int i = 0; i < _subTabContents.Length; i++)
                {
                    if (_subTabContents[i] == null)
                    {
                        hasNullEntries = true;
                        break;
                    }
                }
            }

            if (needsExpansion || hasNullEntries)
            {
                // contentArea가 없으면 생성
                if (contentArea == null)
                {
                    var caGo = new GameObject("ContentArea", typeof(RectTransform));
                    caGo.transform.SetParent(transform, false);
                    var caRT = caGo.GetComponent<RectTransform>();
                    caRT.anchorMin = Vector2.zero;
                    caRT.anchorMax = Vector2.one;
                    caRT.sizeDelta = Vector2.zero;
                    caRT.anchoredPosition = Vector2.zero;
                    contentArea = caGo.transform;
                }

                var oldContents = _subTabContents;
                int existContentCount = oldContents != null ? oldContents.Length : 0;
                if (needsExpansion)
                    _subTabContents = new GameObject[tabCount];

                for (int i = 0; i < tabCount; i++)
                {
                    // 기존 유효한 엔트리는 보존
                    if (i < existContentCount && oldContents[i] != null)
                    {
                        if (needsExpansion)
                            _subTabContents[i] = oldContents[i];

                        // RectTransform이 올바르게 스트레치되는지 확인
                        var existRT = _subTabContents[i].GetComponent<RectTransform>();
                        if (existRT != null)
                        {
                            existRT.anchorMin = Vector2.zero;
                            existRT.anchorMax = Vector2.one;
                            existRT.sizeDelta = Vector2.zero;
                            existRT.anchoredPosition = Vector2.zero;
                        }
                        continue;
                    }

                    // null이거나 새로 추가해야 하는 엔트리 — 새 컨테이너 생성
                    var go = new GameObject($"SubTabContent_{i}", typeof(RectTransform));
                    go.transform.SetParent(contentArea, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    go.SetActive(false);
                    _subTabContents[i] = go;
                    Debug.Log($"[DungeonPanel] 서브탭 콘텐츠 {i} 생성됨 (null 복구 또는 확장)");
                }
            }

            // 월드보스 탭 (index 1) — 콘텐츠가 비어있으면 기본 UI 생성
            if (_subTabContents.Length > 1 && _subTabContents[1] != null
                && _subTabContents[1].transform.childCount == 0)
            {
                BuildWorldBossTabContent(_subTabContents[1].transform);
            }

            // 보스 레이드 탭 (index 2) — 콘텐츠가 비어있으면 기본 UI 생성
            if (_subTabContents.Length > 2 && _subTabContents[2] != null
                && _subTabContents[2].transform.childCount == 0)
            {
                BuildBossRaidTabContent(_subTabContents[2].transform);
            }

            // 타워 탭 (index 3) UI 구성
            if (_subTabContents.Length > 3 && _subTabContents[3] != null)
            {
                BuildTowerTabContent(_subTabContents[3].transform);
            }

            Debug.Log($"[DungeonPanel] EnsureTabExtension 완료: buttons={_subTabButtons?.Length}, contents={_subTabContents?.Length}");
        }

        /// <summary>
        /// 월드보스 탭 콘텐츠를 런타임에서 생성한다 (null 복구 또는 에디터 미구성 시).
        /// </summary>
        private void BuildWorldBossTabContent(Transform tab1)
        {
            Color textWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
            Color textGold = new Color(1f, 0.85f, 0.1f, 1f);
            Color sectionBg = new Color(0.12f, 0.12f, 0.18f, 0.6f);

            if (tab1.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = tab1.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 10;
                vlg.padding = new RectOffset(12, 12, 12, 12);
            }

            if (_worldBossStageText == null)
            {
                var go = CreateSectionRow(tab1, "WorldBossStageText", 40f, sectionBg);
                _worldBossStageText = CreateTextChild(go.transform, "Label", 22f, textWhite, TextAlignmentOptions.Center);
                _worldBossStageText.fontStyle = FontStyles.Bold;
            }
            if (_worldBossHpText == null)
            {
                var go = CreateSectionRow(tab1, "WorldBossHpText", 32f, sectionBg);
                _worldBossHpText = CreateTextChild(go.transform, "Label", 18f, textWhite, TextAlignmentOptions.Center);
            }
            if (_worldBossBestDamageText == null)
            {
                var go = CreateSectionRow(tab1, "WorldBossBestDmgText", 28f, sectionBg);
                _worldBossBestDamageText = CreateTextChild(go.transform, "Label", 16f, textGold, TextAlignmentOptions.Center);
            }
            if (_worldBossResultText == null)
            {
                var go = CreateSectionRow(tab1, "WorldBossResultText", 50f, sectionBg);
                _worldBossResultText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_worldBossChallengeButton == null)
            {
                var go = new GameObject("WorldBossChallengeBtn", typeof(RectTransform));
                go.transform.SetParent(tab1, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 50f);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = new Color(0.4f, 0.3f, 0.3f, 0.9f);
                _worldBossChallengeButton = go.AddComponent<Button>();
                _worldBossChallengeButton.targetGraphic = btnBg;
                CreateTextChild(go.transform, "BtnLabel", 18f, textWhite, TextAlignmentOptions.Center, "도전");
                // onClick은 Awake()에서 일괄 연결
            }
        }

        /// <summary>
        /// 보스 레이드 탭 콘텐츠를 런타임에서 생성한다 (null 복구 또는 에디터 미구성 시).
        /// </summary>
        private void BuildBossRaidTabContent(Transform tab2)
        {
            Color textWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
            Color sectionBg = new Color(0.12f, 0.12f, 0.18f, 0.6f);

            if (tab2.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = tab2.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 10;
                vlg.padding = new RectOffset(12, 12, 12, 12);
            }

            if (_raidAttemptsText == null)
            {
                var go = CreateSectionRow(tab2, "RaidAttemptsText", 32f, sectionBg);
                _raidAttemptsText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }

            if (_raidDifficultyButtons == null || _raidDifficultyButtons.Length == 0)
            {
                var diffRow = new GameObject("RaidDiffRow", typeof(RectTransform));
                diffRow.transform.SetParent(tab2, false);
                diffRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);
                var hlg = diffRow.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 8;
                hlg.padding = new RectOffset(4, 4, 4, 4);

                _raidDifficultyButtons = new Button[3];
                _raidDifficultyBgs = new Image[3];
                string[] labels = { "쉬움", "보통", "어려움" };
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject($"RaidDiffBtn_{i}", typeof(RectTransform));
                    go.transform.SetParent(diffRow.transform, false);
                    _raidDifficultyBgs[i] = go.AddComponent<Image>();
                    _raidDifficultyBgs[i].color = _subTabNormal;
                    _raidDifficultyButtons[i] = go.AddComponent<Button>();
                    _raidDifficultyButtons[i].targetGraphic = _raidDifficultyBgs[i];
                    CreateTextChild(go.transform, "Label", 15f, textWhite, TextAlignmentOptions.Center, labels[i]);
                    // onClick은 Awake()에서 일괄 연결
                }
            }

            if (_raidResultText == null)
            {
                var go = CreateSectionRow(tab2, "RaidResultText", 50f, sectionBg);
                _raidResultText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }

            if (_raidExecuteButton == null)
            {
                var go = new GameObject("RaidExecuteBtn", typeof(RectTransform));
                go.transform.SetParent(tab2, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 50f);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = new Color(0.4f, 0.3f, 0.3f, 0.9f);
                _raidExecuteButton = go.AddComponent<Button>();
                _raidExecuteButton.targetGraphic = btnBg;
                CreateTextChild(go.transform, "BtnLabel", 18f, textWhite, TextAlignmentOptions.Center, "레이드 실행");
                // onClick은 Awake()에서 일괄 연결
            }
        }

        /// <summary>
        /// 재귀적으로 자식을 탐색하여 이름이 일치하는 Transform을 찾는다.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void BuildTowerTabContent(Transform tab3)
        {
            Color textWhite = new Color(0.95f, 0.93f, 0.88f, 1f);
            Color textGold = new Color(1f, 0.85f, 0.1f, 1f);
            Color sectionBg = new Color(0.12f, 0.12f, 0.18f, 0.6f);

            // Scrollable tower tab
            ScrollRect tab3Scroll = tab3.GetComponent<ScrollRect>();
            Transform towerContent;
            if (tab3Scroll == null)
            {
                tab3.gameObject.AddComponent<Image>().color = Color.clear;
                tab3Scroll = tab3.gameObject.AddComponent<ScrollRect>();
                tab3Scroll.horizontal = false;
                tab3Scroll.vertical = true;
                tab3Scroll.movementType = ScrollRect.MovementType.Clamped;

                var viewport = new GameObject("Viewport", typeof(RectTransform));
                viewport.transform.SetParent(tab3, false);
                var vpRT = viewport.GetComponent<RectTransform>();
                vpRT.anchorMin = Vector2.zero;
                vpRT.anchorMax = Vector2.one;
                vpRT.sizeDelta = Vector2.zero;
                var vpMask = viewport.AddComponent<Mask>();
                vpMask.showMaskGraphic = false;
                viewport.AddComponent<Image>().color = Color.white;

                var content = new GameObject("TowerContent", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                var cRT = content.GetComponent<RectTransform>();
                cRT.anchorMin = new Vector2(0f, 1f);
                cRT.anchorMax = new Vector2(1f, 1f);
                cRT.pivot = new Vector2(0.5f, 1f);
                cRT.sizeDelta = Vector2.zero;
                var csf = content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var vlg = content.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 8;
                vlg.padding = new RectOffset(8, 8, 8, 8);

                tab3Scroll.viewport = vpRT;
                tab3Scroll.content = cRT;
                towerContent = content.transform;
            }
            else
            {
                towerContent = tab3Scroll.content;
            }

            // 타워 UI 요소들 (기존 EnsureComponents의 TAB 3 로직 재사용)
            if (_towerFloorText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerFloorText", 44f, sectionBg);
                _towerFloorText = CreateTextChild(go.transform, "Label", 24f, textWhite, TextAlignmentOptions.Center);
                _towerFloorText.fontStyle = FontStyles.Bold;
            }
            if (_towerHighestText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerHighestText", 28f, sectionBg);
                _towerHighestText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerZoneText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerZoneText", 28f, sectionBg);
                _towerZoneText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerTitleText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerTitleText", 28f, sectionBg);
                _towerTitleText = CreateTextChild(go.transform, "Label", 16f, textGold, TextAlignmentOptions.Center);
                _towerTitleText.fontStyle = FontStyles.Bold;
            }
            if (_towerProgressSlider == null)
            {
                var sliderGo = new GameObject("TowerProgressSlider", typeof(RectTransform));
                sliderGo.transform.SetParent(towerContent, false);
                sliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
                sliderGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
                var fillArea = new GameObject("FillArea", typeof(RectTransform));
                fillArea.transform.SetParent(sliderGo.transform, false);
                var faRT = fillArea.GetComponent<RectTransform>();
                faRT.anchorMin = new Vector2(0f, 0.15f);
                faRT.anchorMax = new Vector2(1f, 0.85f);
                faRT.sizeDelta = Vector2.zero;
                var fill = new GameObject("Fill", typeof(RectTransform));
                fill.transform.SetParent(fillArea.transform, false);
                var fillRT = fill.GetComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0f, 1f);
                fillRT.sizeDelta = Vector2.zero;
                fill.AddComponent<Image>().color = new Color(0.3f, 0.6f, 0.3f, 1f);
                _towerProgressSlider = sliderGo.AddComponent<Slider>();
                _towerProgressSlider.interactable = false;
                _towerProgressSlider.fillRect = fillRT;
            }
            if (_towerProgressText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerProgressText", 24f, Color.clear);
                _towerProgressText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerGimmickText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerGimmickText", 36f, sectionBg);
                _towerGimmickText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Left);
            }
            if (_towerPrestigeText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerPrestigeText", 44f, sectionBg);
                _towerPrestigeText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerPrestigeButton == null)
            {
                var go = new GameObject("TowerPrestigeBtn", typeof(RectTransform));
                go.transform.SetParent(towerContent, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = new Color(0.5f, 0.35f, 0.2f, 0.9f);
                _towerPrestigeButton = go.AddComponent<Button>();
                _towerPrestigeButton.targetGraphic = btnBg;
                CreateTextChild(go.transform, "BtnLabel", 17f, textWhite, TextAlignmentOptions.Center, "환생");
            }
            if (_towerOverallRankText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerOverallRankText", 28f, sectionBg);
                _towerOverallRankText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerWeeklyRankText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerWeeklyRankText", 28f, sectionBg);
                _towerWeeklyRankText = CreateTextChild(go.transform, "Label", 16f, textWhite, TextAlignmentOptions.Center);
            }
            if (_towerWeeklyRewardButton == null)
            {
                var go = new GameObject("TowerWeeklyRewardBtn", typeof(RectTransform));
                go.transform.SetParent(towerContent, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
                var btnBg = go.AddComponent<Image>();
                btnBg.color = new Color(0.3f, 0.4f, 0.3f, 0.9f);
                _towerWeeklyRewardButton = go.AddComponent<Button>();
                _towerWeeklyRewardButton.targetGraphic = btnBg;
                CreateTextChild(go.transform, "BtnLabel", 15f, textWhite, TextAlignmentOptions.Center, "주간 보상 수령");
            }
            if (_towerWeeklyRewardText == null)
            {
                var go = CreateSectionRow(towerContent, "TowerWeeklyRewardText", 28f, Color.clear);
                _towerWeeklyRewardText = CreateTextChild(go.transform, "Label", 14f, textWhite, TextAlignmentOptions.Center);
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void OnDestroy()
        {
            // UniTask flash 정리
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = null;

            // DOTween 정리
            if (_subTabBgs != null)
            {
                for (int i = 0; i < _subTabBgs.Length; i++)
                {
                    if (_subTabBgs[i] != null)
                        _subTabBgs[i].transform.DOKill();
                }
            }

            // _raidDifficultyBgs는 DOTween을 사용하지 않으므로 Kill 불필요

            if (_subTabContents != null)
            {
                for (int i = 0; i < _subTabContents.Length; i++)
                {
                    if (_subTabContents[i] != null)
                    {
                        var cg = _subTabContents[i].GetComponent<CanvasGroup>();
                        if (cg != null)
                            DOTween.Kill(cg);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 던전 목록의 한 행 UI 요소.
    /// </summary>
    [System.Serializable]
    public struct DungeonRowUI
    {
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI rewardText;
        public Button enterButton;
    }
}
