using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 어빌리티 탭 컨트롤러.
    /// 3갈래 트리(공격/방어/유틸) 노드 리스트 + 해금/리셋 기능.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AbilityTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        private Label _pointsLabel;
        private Button[] _branchBtns = new Button[3];
        private VisualElement _nodeList;
        private Button _resetBtn;
        private Label _resetCostLabel;

        private int _currentBranch;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[AbilityTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("ability-root");

            CacheElements();
            BindButtons();

            EventBus.Subscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Subscribe<AbilityResetEvent>(OnAbilityReset);

            SwitchBranch(0);
            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Unsubscribe<AbilityResetEvent>(OnAbilityReset);
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
            _pointsLabel = _root.Q<Label>("ability-points-label");
            _branchBtns[0] = _root.Q<Button>("ability-branch-0");
            _branchBtns[1] = _root.Q<Button>("ability-branch-1");
            _branchBtns[2] = _root.Q<Button>("ability-branch-2");
            _nodeList = _root.Q<VisualElement>("ability-node-list");
            _resetBtn = _root.Q<Button>("ability-reset-btn");
            _resetCostLabel = _root.Q<Label>("ability-reset-cost");
        }

        private void BindButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                if (_branchBtns[i] != null)
                    _branchBtns[i].RegisterCallback<ClickEvent>(_ => SwitchBranch(idx));
            }

            if (_resetBtn != null)
                _resetBtn.RegisterCallback<ClickEvent>(_ => OnResetClicked());
        }

        private void SwitchBranch(int branchIndex)
        {
            _currentBranch = branchIndex;

            for (int i = 0; i < 3; i++)
            {
                if (_branchBtns[i] == null) continue;
                if (i == branchIndex)
                    _branchBtns[i].AddToClassList("ability__branch-btn--active");
                else
                    _branchBtns[i].RemoveFromClassList("ability__branch-btn--active");
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            var mgr = AbilityManager.Instance;
            if (mgr == null) return;

            if (_pointsLabel != null)
                _pointsLabel.text = $"포인트: {mgr.AvailablePoints}";

            if (_resetCostLabel != null && mgr.Config != null)
                _resetCostLabel.text = $"{mgr.Config.ResetCostGold:N0}G";

            RefreshNodeList(mgr);
        }

        private void RefreshNodeList(AbilityManager mgr)
        {
            if (_nodeList == null || mgr.Config == null) return;
            _nodeList.Clear();

            var branchNodes = new System.Collections.Generic.List<AbilityNodeSO>();
            mgr.Config.GetBranchNodes(_currentBranch, branchNodes);

            // depth 순으로 정렬
            branchNodes.Sort((a, b) => a.depth.CompareTo(b.depth));

            for (int i = 0; i < branchNodes.Count; i++)
            {
                var node = branchNodes[i];
                bool isUnlocked = mgr.IsNodeUnlocked(node.id);
                bool canUnlock = !isUnlocked
                    && mgr.AvailablePoints >= node.cost
                    && (string.IsNullOrEmpty(node.prerequisiteNodeId) || mgr.IsNodeUnlocked(node.prerequisiteNodeId));

                var row = new VisualElement();
                row.AddToClassList("ability__node");
                if (isUnlocked) row.AddToClassList("ability__node--unlocked");
                else if (!canUnlock) row.AddToClassList("ability__node--locked");

                // 아이콘
                var icon = new VisualElement();
                icon.AddToClassList("ability__node-icon");
                if (node.icon != null)
                    icon.style.backgroundImage = new StyleBackground(node.icon);
                row.Add(icon);

                // 정보
                var info = new VisualElement();
                info.AddToClassList("ability__node-info");

                var nameLabel = new Label(node.displayName);
                nameLabel.AddToClassList("ability__node-name");
                info.Add(nameLabel);

                string statName = GetStatName(node.bonusStat);
                string effectText = node.bonusFlat > 0
                    ? $"{statName} +{node.bonusFlat:F0}"
                    : $"{statName} +{node.bonusPercent}%";
                var effectLabel = new Label(effectText);
                effectLabel.AddToClassList("ability__node-effect");
                info.Add(effectLabel);

                row.Add(info);

                // 해금 버튼 또는 체크
                if (isUnlocked)
                {
                    var check = new Label("\u2713");
                    check.AddToClassList("ability__node-check");
                    row.Add(check);
                }
                else
                {
                    var btn = new Button();
                    btn.AddToClassList("ability__node-btn");
                    btn.text = $"{node.cost}P";
                    btn.SetEnabled(canUnlock);

                    string nodeId = node.id;
                    btn.RegisterCallback<ClickEvent>(_ => OnUnlockClicked(nodeId));
                    row.Add(btn);
                }

                _nodeList.Add(row);
            }
        }

        private void OnUnlockClicked(string nodeId)
        {
            var mgr = AbilityManager.Instance;
            if (mgr == null) return;

            if (mgr.UnlockNode(nodeId))
                RefreshAll();
        }

        private void OnResetClicked()
        {
            var mgr = AbilityManager.Instance;
            if (mgr == null) return;

            if (mgr.ResetAll())
                RefreshAll();
        }

        private void OnAbilityUnlocked(AbilityUnlockedEvent evt) => RefreshAll();
        private void OnAbilityReset(AbilityResetEvent evt) => RefreshAll();

        private static string GetStatName(StatType stat)
        {
            return stat switch
            {
                StatType.Atk => "공격력",
                StatType.Def => "방어력",
                StatType.MaxHp => "최대 HP",
                StatType.CritRate => "치명타율",
                StatType.CritDamage => "치명타 데미지",
                StatType.AttackSpeed => "공격 속도",
                StatType.MoveSpeed => "이동 속도",
                _ => stat.ToString()
            };
        }
    }
}
