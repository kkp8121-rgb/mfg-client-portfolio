using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Core;
using MkLike.Growth;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 마스터리 트리 UI 패널.
    /// 3분기 x 3단계 노드를 표시하고, 해금/리스펙 기능을 제공한다.
    /// </summary>
    public class MasteryPanel : BasePopup
    {
        [Header("마스터리 노드")]
        [SerializeField] private MasteryNodeUI[] _nodeSlots; // 9개 (3분기 x 3단계)

        [Header("포인트 정보")]
        [SerializeField] private TMP_Text _availablePointsText;
        [SerializeField] private TMP_Text _totalPointsText;

        [Header("직업 표시")]
        [SerializeField] private TMP_Text _jobNameText;
        [SerializeField] private TMP_Text _tierText;

        [Header("리스펙")]
        [SerializeField] private Button _respecButton;
        [SerializeField] private TMP_Text _respecCostText;

        [Header("선택 노드 상세")]
        [SerializeField] private TMP_Text _selectedNameText;
        [SerializeField] private TMP_Text _selectedDescText;
        [SerializeField] private TMP_Text _selectedStatusText;
        [SerializeField] private Button _unlockButton;

        private string _selectedMasteryId;

        private void OnEnable()
        {
            EventBus.Subscribe<MasteryUnlockedEvent>(OnMasteryUnlocked);
            EventBus.Subscribe<MasteryRespecEvent>(OnMasteryRespec);
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);

            if (_respecButton != null)
                _respecButton.onClick.AddListener(OnRespecClicked);
            if (_unlockButton != null)
                _unlockButton.onClick.AddListener(OnUnlockClicked);

            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MasteryUnlockedEvent>(OnMasteryUnlocked);
            EventBus.Unsubscribe<MasteryRespecEvent>(OnMasteryRespec);
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);

            if (_respecButton != null)
                _respecButton.onClick.RemoveListener(OnRespecClicked);
            if (_unlockButton != null)
                _unlockButton.onClick.RemoveListener(OnUnlockClicked);
        }

        /// <summary>
        /// 전체 UI를 갱신한다.
        /// </summary>
        public void Refresh()
        {
            var mgr = MasteryManager.Instance;
            if (mgr == null) return;

            // 포인트 표시
            if (_availablePointsText != null)
                _availablePointsText.text = $"{mgr.AvailablePoints}";
            if (_totalPointsText != null)
                _totalPointsText.text = $"총 {mgr.TotalEarnedPoints}";

            // 직업 표시
            if (JobSystem.Instance != null)
            {
                if (_jobNameText != null)
                    _jobNameText.text = JobSystem.Instance.GetCurrentDisplayName();
                if (_tierText != null)
                    _tierText.text = $"T{JobSystem.Instance.CurrentTier}";
            }

            // 리스펙 비용
            if (_respecCostText != null)
                _respecCostText.text = $"등반의 증표 {mgr.RespecCost}";

            // 4차 전직 미달성 시 잠금 표시
            bool isTier4 = JobSystem.Instance != null && JobSystem.Instance.CurrentTier >= 4;

            // 노드 갱신
            var catalog = mgr.Catalog;
            if (catalog == null || _nodeSlots == null) return;

            var currentJob = JobSystem.Instance?.CurrentJob ?? JobType.Warrior;

            int slotIdx = 0;
            for (int i = 0; i < catalog.Length && slotIdx < _nodeSlots.Length; i++)
            {
                var data = catalog[i];
                if (data.requiredJob != currentJob) continue;
                if (slotIdx >= _nodeSlots.Length) break;

                var slot = _nodeSlots[slotIdx];
                if (slot != null)
                {
                    bool isUnlocked = mgr.IsUnlocked(data.id);
                    bool canUnlock = isTier4 && mgr.CanUnlock(data.id);
                    slot.Setup(data.id, data.displayName, data.description,
                        isUnlocked, canUnlock, data.branchIndex, data.nodeLevel);
                    slot.OnClicked = OnNodeClicked;
                }
                slotIdx++;
            }

            // 남은 슬롯 비활성화
            for (int i = slotIdx; i < _nodeSlots.Length; i++)
            {
                if (_nodeSlots[i] != null)
                    _nodeSlots[i].gameObject.SetActive(false);
            }

            RefreshSelectedDetail();
        }

        private void RefreshSelectedDetail()
        {
            var mgr = MasteryManager.Instance;
            if (mgr == null || string.IsNullOrEmpty(_selectedMasteryId))
            {
                if (_selectedNameText != null) _selectedNameText.text = "";
                if (_selectedDescText != null) _selectedDescText.text = "노드를 선택하세요";
                if (_selectedStatusText != null) _selectedStatusText.text = "";
                if (_unlockButton != null) _unlockButton.interactable = false;
                return;
            }

            var catalog = mgr.Catalog;
            MasteryDataSO selected = null;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].id == _selectedMasteryId)
                {
                    selected = catalog[i];
                    break;
                }
            }

            if (selected == null) return;

            if (_selectedNameText != null)
                _selectedNameText.text = selected.displayName;
            if (_selectedDescText != null)
                _selectedDescText.text = selected.description;

            bool isUnlocked = mgr.IsUnlocked(_selectedMasteryId);
            if (_selectedStatusText != null)
                _selectedStatusText.text = isUnlocked ? "<color=#00FF00>해금됨</color>" : "미해금";

            if (_unlockButton != null)
                _unlockButton.interactable = !isUnlocked && mgr.CanUnlock(_selectedMasteryId);
        }

        // ── 버튼 핸들러 ──

        private void OnNodeClicked(string masteryId)
        {
            _selectedMasteryId = masteryId;
            RefreshSelectedDetail();
        }

        private void OnUnlockClicked()
        {
            if (string.IsNullOrEmpty(_selectedMasteryId)) return;
            var mgr = MasteryManager.Instance;
            if (mgr == null) return;

            mgr.UnlockNode(_selectedMasteryId);
        }

        private void OnRespecClicked()
        {
            var mgr = MasteryManager.Instance;
            if (mgr == null) return;

            mgr.Respec();
        }

        // ── 이벤트 핸들러 ──

        private void OnMasteryUnlocked(MasteryUnlockedEvent evt)
        {
            Refresh();
        }

        private void OnMasteryRespec(MasteryRespecEvent evt)
        {
            _selectedMasteryId = null;
            Refresh();
        }

        private void OnJobChanged(JobChangedEvent evt)
        {
            _selectedMasteryId = null;
            Refresh();
        }
    }

    /// <summary>
    /// 마스터리 노드 UI 슬롯.
    /// </summary>
    public class MasteryNodeUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;

        [Header("상태 색상")]
        [SerializeField] private Color _unlockedColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _availableColor = new Color(0.8f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        public System.Action<string> OnClicked;

        private string _masteryId;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => OnClicked?.Invoke(_masteryId));
        }

        public void Setup(string id, string name, string desc,
            bool isUnlocked, bool canUnlock, int branch, int level)
        {
            gameObject.SetActive(true);
            _masteryId = id;

            if (_nameText != null)
                _nameText.text = name;

            if (_background != null)
            {
                if (isUnlocked)
                    _background.color = _unlockedColor;
                else if (canUnlock)
                    _background.color = _availableColor;
                else
                    _background.color = _lockedColor;
            }

            if (_button != null)
                _button.interactable = true;
        }
    }
}
