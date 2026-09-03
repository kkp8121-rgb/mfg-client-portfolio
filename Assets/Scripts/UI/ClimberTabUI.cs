using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Growth;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 동반자의 힘 탭 컨트롤러.
    /// UIDocument에 연결하여 ClimberPowerSystem과 바인딩한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ClimberTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // 캐싱된 요소
        private Label _levelLabel;
        private Label _costLabel;
        private Label _slotsLabel;
        private Label _bonusAtkLabel;
        private Label _bonusHpLabel;
        private Label _bonusDefLabel;
        private Button _upgradeBtn;

        // 보너스 상수 (ClimberPowerSystem과 동일)
        private const int ATK_PER_LEVEL = 3;
        private const int HP_PER_LEVEL = 20;
        private const int DEF_PER_LEVEL = 1;
        private const int MAX_SLOTS = 10;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50; // HUD(0)/TabBar(10) 위에 렌더링
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[ClimberTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButtonAsSubtab(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindButtons();

            // 이벤트 구독
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);

            RefreshAll();

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);

            // 버튼 콜백 해제
            if (_upgradeBtn != null)
                _upgradeBtn.UnregisterCallback<ClickEvent>(OnUpgradeClicked);
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
            _levelLabel = _root.Q<Label>("climber-level");
            _costLabel = _root.Q<Label>("climber-cost");
            _slotsLabel = _root.Q<Label>("climber-slots");
            _bonusAtkLabel = _root.Q<Label>("climber-bonus-atk");
            _bonusHpLabel = _root.Q<Label>("climber-bonus-hp");
            _bonusDefLabel = _root.Q<Label>("climber-bonus-def");
            _upgradeBtn = _root.Q<Button>("climber-upgrade-btn");
        }

        private void BindButtons()
        {
            if (_upgradeBtn != null)
                _upgradeBtn.RegisterCallback<ClickEvent>(OnUpgradeClicked);
        }

        private void OnUpgradeClicked(ClickEvent evt)
        {
            if (ClimberPowerSystem.Instance == null)
            {
                Debug.LogWarning("[ClimberTabUI] ClimberPowerSystem이 없습니다.");
                return;
            }

            if (ClimberPowerSystem.Instance.Upgrade())
            {
                Debug.Log($"[ClimberTabUI] 강화 성공 → Lv.{ClimberPowerSystem.Instance.CurrentLevel}");
                RefreshAll();
            }
            else
            {
                Debug.Log("[ClimberTabUI] 강화 실패 (재화 부족 또는 최대 레벨)");
            }
        }

        // ── 갱신 ──

        private void RefreshAll()
        {
            var system = ClimberPowerSystem.Instance;
            if (system == null)
            {
                // 시스템 미초기화 시 기본값 표시
                SetDefaultDisplay();
                return;
            }

            int level = system.CurrentLevel;
            int slots = system.AbilitySlots;
            long cost = system.GetUpgradeCost();
            bool canUpgrade = system.CanUpgrade();
            bool isMaxLevel = cost < 0;

            // 레벨
            if (_levelLabel != null)
                _levelLabel.text = $"동반자의 힘 Lv.{level}";

            // 비용
            if (_costLabel != null)
                _costLabel.text = isMaxLevel ? "최대 레벨" : $"등반의 증표 x{cost}";

            // 슬롯
            if (_slotsLabel != null)
                _slotsLabel.text = $"{slots} / {MAX_SLOTS}";

            // 보너스 스탯
            if (_bonusAtkLabel != null)
                _bonusAtkLabel.text = $"+{ATK_PER_LEVEL * level}";

            if (_bonusHpLabel != null)
                _bonusHpLabel.text = $"+{HP_PER_LEVEL * level}";

            if (_bonusDefLabel != null)
                _bonusDefLabel.text = $"+{DEF_PER_LEVEL * level}";

            // 버튼 활성화 상태
            if (_upgradeBtn != null)
                _upgradeBtn.SetEnabled(canUpgrade);
        }

        private void SetDefaultDisplay()
        {
            if (_levelLabel != null) _levelLabel.text = "동반자의 힘 Lv.0";
            if (_costLabel != null) _costLabel.text = "등반의 증표 x5";
            if (_slotsLabel != null) _slotsLabel.text = $"0 / {MAX_SLOTS}";
            if (_bonusAtkLabel != null) _bonusAtkLabel.text = "+0";
            if (_bonusHpLabel != null) _bonusHpLabel.text = "+0";
            if (_bonusDefLabel != null) _bonusDefLabel.text = "+0";
            if (_upgradeBtn != null) _upgradeBtn.SetEnabled(false);
        }

        // ── 이벤트 핸들러 ──

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            // ClimbToken 변경 시에만 갱신
            if (evt.Type == CurrencyType.ClimbToken)
                RefreshAll();
        }
    }
}
