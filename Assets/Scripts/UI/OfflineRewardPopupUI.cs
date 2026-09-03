using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 오프라인 보상 팝업.
    /// OfflineRewardClaimedEvent를 구독하여 자동 표시한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OfflineRewardPopupUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _dimOverlay;
        private Label _titleText;
        private Label _awayTimeText;
        private Label _killCountText;
        private Label _goldAmountText;
        private Label _expAmountText;
        private Label _itemDropText;
        private Button _startBtn;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100; // HUD(0)/TabBar(10) 위에 렌더링
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[OfflineRewardPopupUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindCallbacks();

            EventBus<OfflineRewardClaimedEvent>.Subscribe(OnOfflineRewardClaimed);

            // 초기 숨김
            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus<OfflineRewardClaimedEvent>.Unsubscribe(OnOfflineRewardClaimed);
            _root?.RemoveFromClassList("popup--open");
        }

        private void CacheElements()
        {
            _dimOverlay = _root.Q<VisualElement>("dim-overlay");
            _titleText = _root.Q<Label>("title-text");
            _awayTimeText = _root.Q<Label>("away-time-text");
            _killCountText = _root.Q<Label>("kill-count-text");
            _goldAmountText = _root.Q<Label>("gold-amount-text");
            _expAmountText = _root.Q<Label>("exp-amount-text");
            _itemDropText = _root.Q<Label>("item-drop-text");
            _startBtn = _root.Q<Button>("start-btn");
        }

        private void BindCallbacks()
        {
            if (_startBtn != null)
                _startBtn.RegisterCallback<ClickEvent>(OnStartClicked);

            // 딤 오버레이 클릭 시 닫기
            if (_dimOverlay != null)
            {
                _dimOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    // 패널 내부 클릭은 무시
                    if (evt.target == _dimOverlay)
                        SetVisible(false);
                });
            }
        }

        private void OnOfflineRewardClaimed(OfflineRewardClaimedEvent evt)
        {
            PopulateUI(evt);
            SetVisible(true);

            // 2026-04-23 이슈 15 FeedbackBus: 오프라인 보상 수령 시 Toast + 약한 쉐이크
            // 팝업은 별도로 뜨므로 여기선 Toast 중복 피하기 위해 쉐이크만 적용
            if (evt.GoldAmount > 0)
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    $"오프라인 {evt.ElapsedMinutes}분 보상 수령",
                    shakeIntensity: 1);
            }
        }

        private void PopulateUI(OfflineRewardClaimedEvent evt)
        {
            int hours = evt.ElapsedMinutes / 60;
            int minutes = evt.ElapsedMinutes % 60;

            if (_titleText != null)
                _titleText.text = "돌아오셨군요!";

            string awayStr = hours > 0
                ? $"미접속: {hours}시간 {minutes}분"
                : $"미접속: {minutes}분";

            if (evt.LevelMultiplier > 1f)
                awayStr += $" (x{evt.LevelMultiplier:F1} 보너스)";

            if (_awayTimeText != null)
                _awayTimeText.text = awayStr;

            if (_killCountText != null)
                _killCountText.text = $"{NumberFormatter.FormatKorean(evt.TotalKills)}마리 처치";

            if (_goldAmountText != null)
                _goldAmountText.text = NumberFormatter.FormatKorean(evt.GoldAmount);

            if (_expAmountText != null)
                _expAmountText.text = NumberFormatter.FormatKorean(evt.ExpAmount);

            if (_itemDropText != null)
            {
                var sb = new System.Text.StringBuilder();
                if (evt.HuntPoint > 0)
                    sb.Append($"사냥 포인트 +{NumberFormatter.FormatKorean(evt.HuntPoint)}  ");
                if (evt.RuneFragment > 0)
                    sb.Append($"룬 조각 +{evt.RuneFragment}  ");
                if (evt.StarCrystal > 0)
                    sb.Append($"별의 결정 +{evt.StarCrystal}  ");
                if (evt.Ruby > 0)
                    sb.Append($"루비 +{evt.Ruby}  ");
                if (evt.EquipDropCount > 0)
                    sb.Append($"장비 +{evt.EquipDropCount}개  ");
                _itemDropText.text = sb.Length > 0 ? sb.ToString().TrimEnd() : "";
            }
        }

        private void OnStartClicked(ClickEvent evt)
        {
            SetVisible(false);
        }

        /// <summary>
        /// 팝업 표시 여부를 설정한다.
        /// </summary>
        public void SetVisible(bool isVisible)
        {
            if (isVisible)
            {
                if (_dimOverlay != null)
                    _dimOverlay.style.display = DisplayStyle.Flex;
                PlayOpenAnimation();
            }
            else
            {
                PlayCloseAnimation();
            }
        }

        private async void PlayOpenAnimation()
        {
            if (_root == null) return;
            _root.RemoveFromClassList("popup--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _root.AddToClassList("popup--open");
        }

        private async void PlayCloseAnimation()
        {
            if (_root == null) return;
            _root.RemoveFromClassList("popup--open");
            await UniTask.Delay(150, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (_dimOverlay != null)
                _dimOverlay.style.display = DisplayStyle.None;
        }
    }
}
