using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 가챠 결과 데이터. 팝업에 표시할 아이템 목록을 전달한다.
    /// </summary>
    public struct GachaResultItem
    {
        public string ItemId;
        public string ItemName;
        public string Grade;
    }

    /// <summary>
    /// UI Toolkit 기반 가챠 결과 팝업.
    /// 카드 뒤집기 애니메이션 + 등급별 글로우 + 순차 공개 연출.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GachaResultPopupUI : MonoBehaviour
    {
        [SerializeField] private int _revealIntervalMs = 200;
        [SerializeField] private int _flashDurationMs = 400;

        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _dimOverlay;
        private VisualElement _flashOverlay;
        private Label _titleText;
        private VisualElement _cardContainer;
        private Button _confirmBtn;

        // 순차 공개용
        private readonly List<VisualElement> _cards = new();
        private CancellationTokenSource _revealCts;
        private bool _isRevealing;

        // 등급 → USS 클래스 매핑
        private static readonly Dictionary<string, string> GradeCardClass = new()
        {
            { "Normal", "gacha-card--normal" },
            { "Rare", "gacha-card--rare" },
            { "Epic", "gacha-card--epic" },
            { "Unique", "gacha-card--unique" },
            { "Legendary", "gacha-card--legendary" },
            { "Mythic", "gacha-card--mythic" },
        };

        private static readonly Dictionary<string, string> GradeTextClass = new()
        {
            { "Normal", "gacha-card__grade--normal" },
            { "Rare", "gacha-card__grade--rare" },
            { "Epic", "gacha-card__grade--epic" },
            { "Unique", "gacha-card__grade--unique" },
            { "Legendary", "gacha-card__grade--legendary" },
            { "Mythic", "gacha-card__grade--mythic" },
        };

        private static readonly Dictionary<string, string> GradeDisplayName = new()
        {
            { "Normal", "일반" },
            { "Rare", "희귀" },
            { "Epic", "에픽" },
            { "Unique", "유니크" },
            { "Legendary", "전설" },
            { "Mythic", "신화" },
        };

        private static readonly HashSet<string> HighGrades = new() { "Legendary", "Mythic" };

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[GachaResultPopupUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindCallbacks();

            // 초기 숨김
            SetVisible(false);
        }

        private void CacheElements()
        {
            _dimOverlay = _root.Q<VisualElement>("dim-overlay");
            _flashOverlay = _root.Q<VisualElement>("gacha-flash");
            _titleText = _root.Q<Label>("title-text");
            _cardContainer = _root.Q<VisualElement>("card-container");
            _confirmBtn = _root.Q<Button>("confirm-btn");
        }

        private void BindCallbacks()
        {
            if (_confirmBtn != null)
                _confirmBtn.RegisterCallback<ClickEvent>(OnConfirmClicked);

            // 딤 오버레이 클릭: 공개 중이면 스킵, 완료 후에는 닫기
            if (_dimOverlay != null)
            {
                _dimOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _dimOverlay)
                    {
                        if (_isRevealing)
                            SkipReveal();
                        else
                            SetVisible(false);
                    }
                });
            }
        }

        /// <summary>
        /// 가챠 결과를 표시한다.
        /// </summary>
        public void Show(List<GachaResultItem> results, string poolName = null)
        {
            if (_titleText != null)
                _titleText.text = string.IsNullOrEmpty(poolName) ? "뽑기 결과" : $"{poolName} 결과";

            PopulateCards(results);
            SetVisible(true);

            // 순차 공개 시작
            StartRevealSequence(results).Forget();
        }

        private void PopulateCards(List<GachaResultItem> results)
        {
            if (_cardContainer == null) return;

            _cardContainer.Clear();
            _cards.Clear();

            if (results == null) return;

            for (int i = 0; i < results.Count; i++)
            {
                var card = CreateCard(results[i]);
                // 초기: 뒤집힌 상태
                card.AddToClassList("gacha-card--hidden");
                _cardContainer.Add(card);
                _cards.Add(card);
            }

            // 확인 버튼 비활성화 (공개 전)
            SetConfirmEnabled(false);
        }

        private VisualElement CreateCard(GachaResultItem item)
        {
            var card = new VisualElement();
            card.AddToClassList("gacha-card");

            // 등급별 보더 클래스
            if (GradeCardClass.TryGetValue(item.Grade, out string cardClass))
                card.AddToClassList(cardClass);
            else
                card.AddToClassList("gacha-card--normal");

            // 아이콘 영역
            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("gacha-card__icon");

            // 실제 장비/무기 스프라이트 찾기 (DataManager → SO.icon)
            Sprite itemSprite = ResolveItemSprite(item.ItemId);
            if (itemSprite != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(itemSprite);
            }
            else
            {
                // fallback: 등급 이니셜
                var iconText = new Label(GetIconChar(item.Grade));
                iconText.AddToClassList("gacha-card__icon-text");
                iconContainer.Add(iconText);
            }

            card.Add(iconContainer);

            // 아이템 이름
            string displayName = string.IsNullOrEmpty(item.ItemName) ? item.ItemId : item.ItemName;
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("gacha-card__name");
            card.Add(nameLabel);

            // 등급 텍스트
            string gradeName = GradeDisplayName.TryGetValue(item.Grade, out string gn) ? gn : item.Grade;
            var gradeLabel = new Label(gradeName);
            gradeLabel.AddToClassList("gacha-card__grade");
            if (GradeTextClass.TryGetValue(item.Grade, out string gradeClass))
                gradeLabel.AddToClassList(gradeClass);
            card.Add(gradeLabel);

            // 등급 정보를 userData에 저장 (플래시 판정용)
            card.userData = item.Grade;

            return card;
        }

        /// <summary>
        /// 카드를 0.2초 간격으로 순차 공개한다.
        /// Legendary/Mythic 등장 시 배경 플래시.
        /// </summary>
        private async UniTaskVoid StartRevealSequence(List<GachaResultItem> results)
        {
            CancelReveal();
            _revealCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _revealCts.Token,
                this.GetCancellationTokenOnDestroy()
            );
            var ct = linkedCts.Token;

            _isRevealing = true;

            try
            {
                // 팝업 열림 애니메이션 대기
                await UniTask.Delay(300, cancellationToken: ct);

                for (int i = 0; i < _cards.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var card = _cards[i];
                    string grade = (string)card.userData;

                    // 카드 뒤집기 (scaleX 0→1)
                    card.RemoveFromClassList("gacha-card--hidden");
                    card.AddToClassList("gacha-card--revealed");

                    // Legendary/Mythic이면 배경 플래시
                    if (HighGrades.Contains(grade))
                    {
                        PlayFlash(grade, ct).Forget();
                    }

                    // 다음 카드까지 대기
                    if (i < _cards.Count - 1)
                    {
                        await UniTask.Delay(_revealIntervalMs, cancellationToken: ct);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // 스킵됨 — 모든 카드 즉시 공개
                RevealAllImmediate();
            }
            finally
            {
                _isRevealing = false;
                SetConfirmEnabled(true);
                linkedCts.Dispose();
            }
        }

        /// <summary>
        /// 배경 플래시 연출 (Legendary: 금색, Mythic: 적색)
        /// </summary>
        private async UniTaskVoid PlayFlash(string grade, CancellationToken ct)
        {
            if (_flashOverlay == null) return;

            _flashOverlay.RemoveFromClassList("gacha-flash--mythic");

            if (grade == "Mythic")
                _flashOverlay.AddToClassList("gacha-flash--mythic");

            _flashOverlay.AddToClassList("gacha-flash--active");

            try
            {
                await UniTask.Delay(_flashDurationMs, cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 무시
            }

            _flashOverlay.RemoveFromClassList("gacha-flash--active");
        }

        /// <summary>
        /// 모든 카드를 즉시 공개한다 (스킵 시 사용).
        /// </summary>
        private void RevealAllImmediate()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].RemoveFromClassList("gacha-card--hidden");
                _cards[i].AddToClassList("gacha-card--revealed");
            }
        }

        private void SetConfirmEnabled(bool isEnabled)
        {
            if (_confirmBtn == null) return;

            if (isEnabled)
            {
                _confirmBtn.RemoveFromClassList("confirm-btn--disabled");
                _confirmBtn.SetEnabled(true);
            }
            else
            {
                _confirmBtn.AddToClassList("confirm-btn--disabled");
                _confirmBtn.SetEnabled(false);
            }
        }

        /// <summary>
        /// itemId로 실제 장비/무기/기타 아이콘 스프라이트를 찾는다.
        /// 우선순위: EquipmentDataSO.icon → WeaponDataSO.icon → IconRegistry → null
        /// </summary>
        private static Sprite ResolveItemSprite(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            var dm = DataManager.Instance;
            if (dm != null)
            {
                var eq = dm.GetEquipmentData(itemId);
                if (eq != null && eq.icon != null) return eq.icon;

                var wp = dm.GetWeaponData(itemId);
                if (wp != null && wp.icon != null) return wp.icon;
            }

            var reg = IconRegistry.Instance;
            if (reg != null)
            {
                var sprite = reg.GetItemIcon(itemId);
                if (sprite != null) return sprite;
            }

            return null;
        }

        private static string GetIconChar(string grade)
        {
            return grade switch
            {
                "Mythic" => "M",
                "Legendary" => "L",
                "Unique" => "U",
                "Epic" => "E",
                "Rare" => "R",
                _ => "N"
            };
        }

        private void OnConfirmClicked(ClickEvent evt)
        {
            if (_isRevealing) return;
            SetVisible(false);
        }

        /// <summary>
        /// 팝업 영역 클릭 시 연출 스킵 (공개 중일 때)
        /// </summary>
        public void SkipReveal()
        {
            if (!_isRevealing) return;
            CancelReveal();
        }

        private void CancelReveal()
        {
            if (_revealCts != null)
            {
                _revealCts.Cancel();
                _revealCts.Dispose();
                _revealCts = null;
            }
        }

        private void OnDisable()
        {
            CancelReveal();
            _root?.RemoveFromClassList("popup--open");
        }

        private void OnDestroy()
        {
            CancelReveal();
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
                CancelReveal();
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
            // 플래시 초기화
            if (_flashOverlay != null)
            {
                _flashOverlay.RemoveFromClassList("gacha-flash--active");
                _flashOverlay.RemoveFromClassList("gacha-flash--mythic");
            }
            await UniTask.Delay(150, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (_dimOverlay != null)
                _dimOverlay.style.display = DisplayStyle.None;
        }
    }
}
