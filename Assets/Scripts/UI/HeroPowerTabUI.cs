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
    /// UI Toolkit 기반 영웅의 힘 탭 컨트롤러.
    /// CP 마일스톤 리스트와 진행 게이지를 표시한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HeroPowerTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // 캐싱된 요소
        private Label _cpLabel;
        private VisualElement _gaugeFill;
        private Label _nextLabel;
        private Label _summaryLabel;
        private Label _unlockLabel;
        private VisualElement _milestoneList;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[HeroPowerTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("heropower-root");

            CacheElements();

            EventBus.Subscribe<CpChangedEvent>(OnCpChanged);
            EventBus.Subscribe<HeroPowerMilestoneEvent>(OnMilestoneAchieved);

            RefreshAll();
            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<CpChangedEvent>(OnCpChanged);
            EventBus.Unsubscribe<HeroPowerMilestoneEvent>(OnMilestoneAchieved);
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
            _cpLabel = _root.Q<Label>("heropower-cp-label");
            _gaugeFill = _root.Q<VisualElement>("heropower-gauge-fill");
            _nextLabel = _root.Q<Label>("heropower-next-label");
            _summaryLabel = _root.Q<Label>("heropower-summary-label");
            _unlockLabel = _root.Q<Label>("heropower-unlock-label");
            _milestoneList = _root.Q<VisualElement>("heropower-list");
        }

        private void RefreshAll()
        {
            var mgr = HeroPowerManager.Instance;
            if (mgr == null || mgr.Config == null)
            {
                if (_unlockLabel != null) _unlockLabel.text = "Lv.79 해금";
                return;
            }

            // 해금 상태
            if (_unlockLabel != null)
                _unlockLabel.text = mgr.IsUnlocked ? "해금됨" : $"Lv.{mgr.Config.UnlockLevel} 해금";

            // 현재 CP
            long currentCp = GetCurrentCp();
            if (_cpLabel != null)
                _cpLabel.text = $"전투력: {currentCp:N0}";

            // 게이지
            long nextCp = mgr.GetNextMilestoneCp();
            if (_gaugeFill != null && nextCp > 0)
            {
                float progress = Mathf.Clamp01((float)currentCp / nextCp);
                _gaugeFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
            }
            else if (_gaugeFill != null)
            {
                _gaugeFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            }

            if (_nextLabel != null)
            {
                if (nextCp > 0)
                    _nextLabel.text = $"다음 마일스톤: CP {nextCp:N0}";
                else
                    _nextLabel.text = "모든 마일스톤 달성!";
            }

            // 요약
            if (_summaryLabel != null)
                _summaryLabel.text = $"달성 마일스톤: {mgr.UnlockedMilestones} / {mgr.Config.MilestoneCount}";

            // 마일스톤 리스트
            RefreshMilestoneList(mgr, currentCp);
        }

        private void RefreshMilestoneList(HeroPowerManager mgr, long currentCp)
        {
            if (_milestoneList == null) return;
            _milestoneList.Clear();

            for (int i = 0; i < mgr.Config.MilestoneCount; i++)
            {
                var milestone = mgr.Config.GetMilestone(i);
                bool isUnlocked = i < mgr.UnlockedMilestones;

                var row = new VisualElement();
                row.AddToClassList("heropower__milestone");
                if (i % 2 == 1) row.AddToClassList("heropower__milestone--alt");
                if (isUnlocked) row.AddToClassList("heropower__milestone--unlocked");
                else row.AddToClassList("heropower__milestone--locked");

                // 체크 아이콘
                var check = new Label(isUnlocked ? "\u2713" : "\u2022");
                check.AddToClassList("heropower__milestone-check");
                row.Add(check);

                // CP 요구치
                var cpLabel = new Label($"CP {milestone.requiredCp:N0}");
                cpLabel.AddToClassList("heropower__milestone-cp");
                row.Add(cpLabel);

                // 보너스 설명
                string statName = GetStatDisplayName(milestone.bonusStat);
                var bonusLabel = new Label($"{statName} +{milestone.bonusPercent}%");
                bonusLabel.AddToClassList("heropower__milestone-bonus");
                row.Add(bonusLabel);

                _milestoneList.Add(row);
            }
        }

        private long GetCurrentCp()
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return 0;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return 0;

            return stats.PowerScore;
        }

        private void OnCpChanged(CpChangedEvent evt)
        {
            RefreshAll();
        }

        private void OnMilestoneAchieved(HeroPowerMilestoneEvent evt)
        {
            RefreshAll();
        }

        private static string GetStatDisplayName(StatType stat)
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
                StatType.GoldBonus => "골드 보너스",
                StatType.ExpBonus => "경험치 보너스",
                _ => stat.ToString()
            };
        }
    }
}
