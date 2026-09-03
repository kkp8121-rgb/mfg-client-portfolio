using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Growth;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 전직 탭 컨트롤러.
    /// UIDocument에 연결하여 JobSystem과 바인딩한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class JobTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // 캐싱된 요소 — 직업 정보
        private Label _jobCurrentText;
        private Label _jobNextText;

        // 카드 컨테이너
        private VisualElement _cardWarrior;
        private VisualElement _cardArcher;
        private VisualElement _cardMage;

        // 선택 버튼
        private Button _btnWarrior;
        private Button _btnArcher;
        private Button _btnMage;

        // 전직 경로 라벨 (Tier 0~4)
        private Label[] _pathLabels;

        // 게임 시스템 참조
        private LevelSystem _levelSystem;

        /// <summary>전직 필요 레벨 (JobSystem과 동일)</summary>
        private static readonly int[] ADVANCEMENT_LEVELS = { 40, 80, 120, 160 };

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 53; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[JobTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButtonAsSubtab(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("job-root");

            CacheElements();
            BindButtons();

            // 게임 시스템 탐색
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                _levelSystem = player.GetComponent<LevelSystem>();
            }

            // 이벤트 구독
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);

            RefreshAll();

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        private void CacheElements()
        {
            _jobCurrentText = _root.Q<Label>("job-current-text");
            _jobNextText = _root.Q<Label>("job-next-text");

            _cardWarrior = _root.Q<VisualElement>("card-warrior");
            _cardArcher = _root.Q<VisualElement>("card-archer");
            _cardMage = _root.Q<VisualElement>("card-mage");

            _btnWarrior = _root.Q<Button>("btn-warrior");
            _btnArcher = _root.Q<Button>("btn-archer");
            _btnMage = _root.Q<Button>("btn-mage");

            _pathLabels = new Label[5];
            for (int i = 0; i < 5; i++)
            {
                _pathLabels[i] = _root.Q<Label>($"path-tier{i}");
            }
        }

        private void BindButtons()
        {
            _btnWarrior?.RegisterCallback<ClickEvent>(_ => OnSelectJob(JobType.Warrior));
            _btnArcher?.RegisterCallback<ClickEvent>(_ => OnSelectJob(JobType.Archer));
            _btnMage?.RegisterCallback<ClickEvent>(_ => OnSelectJob(JobType.Mage));
        }

        // ── 직업 선택 ──

        private void OnSelectJob(JobType jobType)
        {
            if (JobSystem.Instance == null)
            {
                Debug.LogWarning("[JobTabUI] JobSystem 인스턴스 없음");
                return;
            }

            // 이미 같은 직업이면 무시
            if (JobSystem.Instance.CurrentJob == jobType && JobSystem.Instance.CurrentTier > 0)
                return;

            JobSystem.Instance.ChangeJob(jobType);
            RefreshAll();
        }

        // ── 갱신 ──

        private void RefreshAll()
        {
            RefreshJobInfo();
            RefreshCards();
            RefreshPath();
        }

        private void RefreshJobInfo()
        {
            if (JobSystem.Instance == null) return;

            JobType currentJob = JobSystem.Instance.CurrentJob;
            int currentTier = JobSystem.Instance.CurrentTier;
            string displayName = JobSystem.GetJobDisplayName(currentJob, currentTier);

            if (_jobCurrentText != null)
                _jobCurrentText.text = $"{displayName} (Tier {currentTier})";

            if (_jobNextText != null)
            {
                int nextLevel = JobSystem.Instance.NextAdvancementLevel;
                if (nextLevel < 0)
                    _jobNextText.text = "최대 전직 완료!";
                else
                    _jobNextText.text = $"다음 전직: Lv.{nextLevel}";
            }
        }

        private void RefreshCards()
        {
            if (JobSystem.Instance == null) return;

            JobType currentJob = JobSystem.Instance.CurrentJob;
            int currentTier = JobSystem.Instance.CurrentTier;

            // 현재는 전사에 집중 — 다른 직업 카드 숨김
            _cardWarrior?.RemoveFromClassList("job__card--hidden");
            UpdateCardSelection(_cardWarrior, currentJob == JobType.Warrior);
            if (_cardArcher != null) _cardArcher.style.display = DisplayStyle.None;
            if (_cardMage != null) _cardMage.style.display = DisplayStyle.None;

            // 직업 선택 확정 후 버튼 비활성화
            bool canChangeJob = !JobSystem.Instance.IsJobLocked;
            _btnWarrior?.SetEnabled(canChangeJob && currentJob != JobType.Warrior);
        }

        private void UpdateCardSelection(VisualElement card, bool isSelected)
        {
            if (card == null) return;

            if (isSelected)
                card.AddToClassList("job__card--selected");
            else
                card.RemoveFromClassList("job__card--selected");
        }

        private void RefreshPath()
        {
            if (JobSystem.Instance == null) return;

            JobType currentJob = JobSystem.Instance.CurrentJob;
            int currentTier = JobSystem.Instance.CurrentTier;

            for (int i = 0; i < 5; i++)
            {
                if (_pathLabels[i] == null) continue;

                string tierName = JobSystem.GetJobDisplayName(currentJob, i);
                string levelReq = i > 0 && i <= ADVANCEMENT_LEVELS.Length
                    ? $" (Lv.{ADVANCEMENT_LEVELS[i - 1]})"
                    : "";

                _pathLabels[i].text = $"{i}단계: {tierName}{levelReq}";

                // 스타일 클래스 갱신
                _pathLabels[i].RemoveFromClassList("job__path-item--active");
                _pathLabels[i].RemoveFromClassList("job__path-item--completed");
                _pathLabels[i].RemoveFromClassList("job__path-item--locked");

                if (i == currentTier)
                    _pathLabels[i].AddToClassList("job__path-item--active");
                else if (i < currentTier)
                    _pathLabels[i].AddToClassList("job__path-item--completed");
                else
                    _pathLabels[i].AddToClassList("job__path-item--locked");
            }
        }

        // ── 이벤트 핸들러 ──

        private void OnJobChanged(JobChangedEvent evt) => RefreshAll();
        private void OnLevelUp(LevelUpEvent evt) => RefreshAll();
    }
}
