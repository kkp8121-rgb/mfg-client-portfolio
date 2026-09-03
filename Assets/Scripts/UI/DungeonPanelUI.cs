using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Dungeon;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 던전 패널 컨트롤러.
    /// 2개 서브탭: 성장던전(0) / 보스레이드(1)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DungeonPanelUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 서브탭 ──
        private static readonly string[] TAB_KEYS = { "growth", "bossraid", "event" };
        private VisualElement[] _tabElements;
        private VisualElement[] _contentElements;
        private int _currentTab = -1;

        // ── 성장 던전 ──
        private Label _keyCountLabel;
        private Label _recommendLabel;
        private ScrollView _dungeonScroll;
        private DungeonDataSO[] _dungeonCatalog;

        // ── 보스 레이드 ──
        private Label _raidAttemptsLabel;
        private Label _raidResultLabel;
        private Button[] _raidDiffButtons;
        private Button _raidExecuteBtn;
        private int _selectedRaidDifficulty;

        // ── 이벤트 던전 (Phase 13) ──
        // EventDungeon 관련 필드 제거 — 시스템 삭제

        // ── 게임 시스템 참조 ──
        private CombatStats _combatStats;

        // ═══ 라이프사이클 ═══

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 55; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[DungeonPanelUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindTabs();
            BindButtons();
            FindPlayerRefs();
            LoadDungeonCatalog();

            // 이벤트 구독
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus.Subscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);

            // 초기 탭
            SwitchTab(0);

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus.Unsubscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);
        }

        private void OnDungeonBattleEnded(DungeonBattleEndedEvent evt)
        {
            string result = evt.IsSuccess ? "클리어!" : "실패...";
            string killInfo = $"{evt.KillCount}/{evt.TargetKills}킬";
            string scorePercent = $"{Mathf.RoundToInt(evt.Score * 100)}%";
            ToastUI.Show($"{evt.DungeonName} {result} ({killInfo}, {scorePercent})",
                evt.IsSuccess ? "\u2714" : "\u2718");
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ═══ 요소 캐싱 ═══

        private void CacheElements()
        {
            // 서브탭
            _tabElements = new VisualElement[TAB_KEYS.Length];
            _contentElements = new VisualElement[TAB_KEYS.Length];
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                _tabElements[i] = _root.Q<VisualElement>($"tab-{TAB_KEYS[i]}");
                _contentElements[i] = _root.Q<VisualElement>($"content-{TAB_KEYS[i]}");
            }

            // 성장 던전
            _keyCountLabel = _root.Q<Label>("key-count");
            _recommendLabel = _root.Q<Label>("recommend-text");
            _dungeonScroll = _root.Q<ScrollView>("dungeon-scroll");

            // 보스 레이드
            _raidAttemptsLabel = _root.Q<Label>("raid-attempts");
            _raidResultLabel = _root.Q<Label>("raid-result");
            _raidExecuteBtn = _root.Q<Button>("raid-execute-btn");
            _raidDiffButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                _raidDiffButtons[i] = _root.Q<Button>($"raid-diff-{i}");
            }

            // 이벤트 던전 (Phase 13)
        }

        // ═══ 바인딩 ═══

        private void BindTabs()
        {
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                int idx = i;
                _tabElements[i]?.RegisterCallback<ClickEvent>(_ => SwitchTab(idx));
            }
        }

        private void BindButtons()
        {
            // 보스 레이드 난이도
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _raidDiffButtons[i]?.RegisterCallback<ClickEvent>(_ => SelectRaidDifficulty(idx));
            }
            _raidExecuteBtn?.RegisterCallback<ClickEvent>(_ => OnRaidExecute());
        }

        // ═══ 탭 전환 ═══

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= TAB_KEYS.Length) return;

            // 탭 시각 전환
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                if (_tabElements[i] == null) continue;

                if (i == index)
                    _tabElements[i].AddToClassList("dg-subtab--active");
                else
                    _tabElements[i].RemoveFromClassList("dg-subtab--active");
            }

            // 콘텐츠 전환
            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                if (_contentElements[i] == null) continue;
                _contentElements[i].style.display = i == index
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            _currentTab = index;
            Refresh();
        }

        private void Refresh()
        {
            switch (_currentTab)
            {
                case 0:
                    RefreshGrowthDungeonTab();
                    break;
                case 1:
                    RefreshBossRaidTab();
                    break;
                // case 2: 이벤트 던전 탭 제거
            }
        }

        // ═══ 게임 시스템 참조 ═══

        private void FindPlayerRefs()
        {
            if (_combatStats != null) return;
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                _combatStats = player.GetComponent<CombatStats>();
            }
        }

        // 2026-04-23 정적 캐시: OnEnable마다 Resources.LoadAll 재호출 방지 (세션 1회만 디스크 스캔)
        private static DungeonDataSO[] _cachedDungeonCatalog;

        private void LoadDungeonCatalog()
        {
            if (_cachedDungeonCatalog != null && _cachedDungeonCatalog.Length > 0)
            {
                _dungeonCatalog = _cachedDungeonCatalog;
                return;
            }

            _cachedDungeonCatalog = Resources.LoadAll<DungeonDataSO>("Data/Dungeons");
            if (_cachedDungeonCatalog == null || _cachedDungeonCatalog.Length == 0)
            {
                _cachedDungeonCatalog = Resources.LoadAll<DungeonDataSO>("Data/Dungeon");
            }

            _dungeonCatalog = _cachedDungeonCatalog;
            if (_dungeonCatalog != null && _dungeonCatalog.Length > 0)
            {
                Debug.Log($"[DungeonPanelUI] 던전 카탈로그 로드: {_dungeonCatalog.Length}개");
            }
        }

        // ═══ 성장 던전 탭 ═══

        private void RefreshGrowthDungeonTab()
        {
            // 열쇠 표시
            int currentKeys = DungeonManager.Instance != null ? DungeonManager.Instance.CurrentKeys : 0;
            int maxKeys = DungeonManager.Instance != null ? DungeonManager.Instance.MaxKeys : 10;
            if (_keyCountLabel != null)
            {
                _keyCountLabel.text = $"{currentKeys} / {maxKeys}";
            }

            // 추천 던전
            RefreshDungeonRecommendation();

            // 던전 행 동적 생성
            BuildDungeonRows();
        }

        private void RefreshDungeonRecommendation()
        {
            if (_recommendLabel == null) return;

            if (RecommendationManager.Instance == null)
            {
                _recommendLabel.text = "";
                return;
            }

            var rec = RecommendationManager.Instance.GetRecommendedDungeon();
            if (string.IsNullOrEmpty(rec.Reason)) return;
            _recommendLabel.text = $"추천: {rec.Reason}";
        }

        private void BuildDungeonRows()
        {
            if (_dungeonScroll == null) return;
            _dungeonScroll.Clear();

            if (_dungeonCatalog == null || _dungeonCatalog.Length == 0)
            {
                var emptyLabel = new Label("던전 데이터 없음 — SO를 Resources/Data/Dungeons에 추가하세요");
                emptyLabel.AddToClassList("empty-placeholder");
                _dungeonScroll.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < _dungeonCatalog.Length; i++)
            {
                var dungeon = _dungeonCatalog[i];
                if (dungeon == null) continue;

                int dungeonIndex = i;
                bool isAlt = i % 2 == 1;

                // 행 컨테이너
                var row = new VisualElement();
                row.AddToClassList("dg-dungeon-row");
                if (isAlt) row.AddToClassList("dg-dungeon-row--alt");

                // 아이콘
                var iconEl = new VisualElement();
                iconEl.AddToClassList("dg-dungeon-row__icon");
                if (dungeon.icon != null)
                {
                    iconEl.style.backgroundImage = new StyleBackground(dungeon.icon);
                }
                row.Add(iconEl);

                // 텍스트 영역
                var textsEl = new VisualElement();
                textsEl.AddToClassList("dg-dungeon-row__texts");

                var nameLabel = new Label(dungeon.displayName);
                nameLabel.AddToClassList("dg-dungeon-row__name");
                textsEl.Add(nameLabel);

                string rewardName = DisplayNameUtils.GetCurrencyDisplayName(dungeon.mainRewardType);
                var rewardLabel = new Label($"{rewardName} x{dungeon.baseRewardAmount}");
                rewardLabel.AddToClassList("dg-dungeon-row__reward");
                textsEl.Add(rewardLabel);

                row.Add(textsEl);

                // 버튼 영역
                var btnArea = new VisualElement();
                btnArea.AddToClassList("dg-dungeon-row__btn-area");

                // 소탕 버튼 (클리어한 던전만 표시)
                bool isCleared = DungeonManager.Instance != null
                    && DungeonManager.Instance.HasCleared(dungeon.id);
                if (isCleared)
                {
                    var sweepBtn = new Button();
                    sweepBtn.AddToClassList("dg-dungeon-row__sweep-btn");
                    var sweepLabel = new Label("소탕");
                    sweepBtn.Add(sweepLabel);
                    sweepBtn.RegisterCallback<ClickEvent>(_ => OnSweepDungeon(dungeonIndex));
                    btnArea.Add(sweepBtn);
                }

                // 입장 버튼
                var enterBtn = new Button();
                enterBtn.AddToClassList("dg-dungeon-row__enter-btn");
                var enterLabel = new Label("입장");
                enterBtn.Add(enterLabel);
                enterBtn.RegisterCallback<ClickEvent>(_ => OnEnterDungeon(dungeonIndex));
                btnArea.Add(enterBtn);

                row.Add(btnArea);

                _dungeonScroll.Add(row);
            }
        }

        private void OnEnterDungeon(int index)
        {
            if (_dungeonCatalog == null || index < 0 || index >= _dungeonCatalog.Length) return;

            var dungeon = _dungeonCatalog[index];
            if (dungeon == null) return;
            if (DungeonManager.Instance == null) return;

            // 던전 전투 진행 중이면 무시
            if (DungeonBattleController.Instance != null && DungeonBattleController.Instance.IsActive) return;

            // 입장 불가 시 피드백
            if (!DungeonManager.Instance.CanEnter(dungeon))
            {
                string reason = DungeonManager.Instance.GetDenyReason(dungeon);
                if (string.IsNullOrEmpty(reason)) reason = "입장 불가";
                Debug.Log($"[DungeonPanelUI] {reason}");

                if (FloatingTextManager.Instance != null)
                    FloatingTextManager.Instance.ShowSystemMessage(reason);
                return;
            }

            if (!DungeonManager.Instance.EnterDungeon(dungeon)) return;

            // 열쇠 소모 후 즉시 UI 갱신
            Refresh();

            // 던전 전투 시작 (실제 전투!)
            if (DungeonBattleController.Instance != null)
            {
                DungeonBattleController.Instance.StartBattle(dungeon);
            }
            else
            {
                // fallback: 컨트롤러 없으면 즉시 완료
                Debug.LogWarning("[DungeonPanelUI] DungeonBattleController 없음 — 즉시 완료 fallback");
                DungeonManager.Instance.CompleteDungeon(dungeon, 1f);
                string rewardName = DisplayNameUtils.GetCurrencyDisplayName(dungeon.mainRewardType);
                ToastUI.Show($"{dungeon.displayName} 클리어! {rewardName} x{dungeon.baseRewardAmount}", "\u2714");
            }
        }

        /// <summary>
        /// 소탕: 클리어한 던전을 전투 없이 즉시 풀 보상으로 완료한다.
        /// </summary>
        private void OnSweepDungeon(int index)
        {
            if (_dungeonCatalog == null || index < 0 || index >= _dungeonCatalog.Length) return;

            var dungeon = _dungeonCatalog[index];
            if (dungeon == null) return;
            if (DungeonManager.Instance == null) return;

            // 클리어 기록 재확인
            if (!DungeonManager.Instance.HasCleared(dungeon.id))
            {
                ToastUI.Show("먼저 던전을 클리어해야 소탕할 수 있습니다.", "\u2718");
                return;
            }

            // 던전 전투 진행 중이면 무시
            if (DungeonBattleController.Instance != null && DungeonBattleController.Instance.IsActive) return;

            // 입장 가능 여부 (열쇠 등)
            if (!DungeonManager.Instance.CanEnter(dungeon))
            {
                string reason = DungeonManager.Instance.GetDenyReason(dungeon);
                if (string.IsNullOrEmpty(reason)) reason = "입장 불가";

                if (FloatingTextManager.Instance != null)
                    FloatingTextManager.Instance.ShowSystemMessage(reason);
                return;
            }

            // 열쇠 소모 (EnterDungeon 내부에서 처리)
            if (!DungeonManager.Instance.EnterDungeon(dungeon)) return;

            // 즉시 풀 보상 완료 (score 1.0)
            DungeonManager.Instance.CompleteDungeon(dungeon, 1f);

            string rewardName = DisplayNameUtils.GetCurrencyDisplayName(dungeon.mainRewardType);
            ToastUI.Show($"소탕 완료! {dungeon.displayName} — {rewardName} x{dungeon.baseRewardAmount}", "\u2714");

            // UI 갱신
            Refresh();
        }

        // ═══ 보스 레이드 탭 ═══

        private void RefreshBossRaidTab()
        {
            if (BossRaidSystem.Instance == null)
            {
                if (_raidAttemptsLabel != null) _raidAttemptsLabel.text = "0 / 3";
                if (_raidResultLabel != null && string.IsNullOrEmpty(_raidResultLabel.text))
                    _raidResultLabel.text = "난이도를 선택하고 레이드를 시작하세요!";
                if (_raidExecuteBtn != null) _raidExecuteBtn.SetEnabled(false);
                UpdateRaidDifficultyVisuals();
                return;
            }

            int remaining = BossRaidSystem.Instance.RemainingAttempts;
            int max = BossRaidSystem.Instance.MaxWeeklyAttempts;

            if (_raidAttemptsLabel != null)
                _raidAttemptsLabel.text = $"{remaining} / {max}";

            bool canRaid = BossRaidSystem.Instance.CanRaid() && _combatStats != null;
            if (_raidExecuteBtn != null) _raidExecuteBtn.SetEnabled(canRaid);

            UpdateRaidDifficultyVisuals();
        }

        private static readonly string[] RAID_DIFFICULTY_NAMES = { "쉬움", "보통", "어려움" };

        private void SelectRaidDifficulty(int index)
        {
            _selectedRaidDifficulty = Mathf.Clamp(index, 0, 2);
            UpdateRaidDifficultyVisuals();
            // 2026-04-23 침묵 액션 보완: 난이도 선택 Toast
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"난이도: {RAID_DIFFICULTY_NAMES[_selectedRaidDifficulty]}",
                shakeIntensity: 0);
        }

        private void UpdateRaidDifficultyVisuals()
        {
            for (int i = 0; i < _raidDiffButtons.Length; i++)
            {
                if (_raidDiffButtons[i] == null) continue;
                if (i == _selectedRaidDifficulty)
                    _raidDiffButtons[i].AddToClassList("dg-diff-btn--selected");
                else
                    _raidDiffButtons[i].RemoveFromClassList("dg-diff-btn--selected");
            }
        }

        private void OnRaidExecute()
        {
            if (BossRaidSystem.Instance == null || _combatStats == null) return;

            var difficulty = (BossRaidSystem.Difficulty)_selectedRaidDifficulty;
            int atk = _combatStats.Atk;
            int hp = _combatStats.MaxHp;

            var result = BossRaidSystem.Instance.ExecuteRaid(difficulty, atk, hp);

            if (_raidResultLabel != null)
            {
                if (result.Success)
                {
                    _raidResultLabel.text = $"성공!\n골드 +{HudPanel.FormatGold(result.GoldReward)}  루비 +{result.RubyReward}";
                    ToastUI.Show($"레이드 성공! 골드 +{HudPanel.FormatGold(result.GoldReward)}  루비 +{result.RubyReward}", "\u2694");
                }
                else
                {
                    _raidResultLabel.text = "실패\n전투력이 부족합니다.";
                }
            }

            RefreshBossRaidTab();
        }

        // ═══ 이벤트 던전 탭 제거 — 시스템 완전 삭제 ═══

        // ═══ 이벤트 핸들러 ═══

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            Debug.Log($"[DungeonPanelUI] 던전 완료: {evt.DungeonId}");
            Refresh();
        }

    }
}
