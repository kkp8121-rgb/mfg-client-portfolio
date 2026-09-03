using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Quest;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 배틀패스 패널 컨트롤러.
    /// 배틀패스(보상 트랙) + 시즌상점 2개 서브탭을 관리한다.
    /// BattlePassSystem, SeasonManager, SeasonShopSystem과 연동.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BattlePassPanelUI : MonoBehaviour
    {
        // ── 상수 ──
        private static readonly string[] TAB_KEYS = { "pass", "shop" };
        private const int MAX_LEVEL = 50;

        // ── 문서/루트 ──
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // ── 서브탭 ──
        private VisualElement[] _tabElements;
        private VisualElement[] _contentElements;
        private int _currentTab = -1;

        // ── 배틀패스 탭 요소 ──
        private Label _seasonInfoLabel;
        private Label _bxpLevelLabel;
        private Label _bxpExpLabel;
        private VisualElement _bxpFill;
        private VisualElement _rewardTrack;
        private Label _premiumStatusLabel;
        private Button _btnPremium;
        private Button _btnClose;

        // ── 시즌상점 탭 요소 ──
        private Label _seasonPointValue;
        private VisualElement _shopGrid;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 57; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 분리
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[BattlePassPanelUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            BindTabs();
            BindButtons();

            // 이벤트 구독 (중복 방지: Unsubscribe 먼저)
            EventBus.Unsubscribe<BattlePassLevelUpEvent>(OnBattlePassLevelUp);
            EventBus.Subscribe<BattlePassLevelUpEvent>(OnBattlePassLevelUp);
            EventBus.Unsubscribe<BattlePassRewardClaimedEvent>(OnRewardClaimed);
            EventBus.Subscribe<BattlePassRewardClaimedEvent>(OnRewardClaimed);
            EventBus.Unsubscribe<SeasonPointChangedEvent>(OnSeasonPointChanged);
            EventBus.Subscribe<SeasonPointChangedEvent>(OnSeasonPointChanged);
            EventBus.Unsubscribe<BattlePassPremiumActivatedEvent>(OnPremiumActivated);
            EventBus.Subscribe<BattlePassPremiumActivatedEvent>(OnPremiumActivated);

            // 기본 탭 선택
            SwitchTab(0);

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<BattlePassLevelUpEvent>(OnBattlePassLevelUp);
            EventBus.Unsubscribe<BattlePassRewardClaimedEvent>(OnRewardClaimed);
            EventBus.Unsubscribe<SeasonPointChangedEvent>(OnSeasonPointChanged);
            EventBus.Unsubscribe<BattlePassPremiumActivatedEvent>(OnPremiumActivated);
        }

        // ── 열기 애니메이션 ──

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        // ── 요소 캐싱 ──

        private void CacheElements()
        {
            _seasonInfoLabel = _root.Q<Label>("season-info");
            _bxpLevelLabel = _root.Q<Label>("bxp-level");
            _bxpExpLabel = _root.Q<Label>("bxp-exp");
            _bxpFill = _root.Q<VisualElement>("bxp-fill");
            _rewardTrack = _root.Q<VisualElement>("reward-track");
            _premiumStatusLabel = _root.Q<Label>("premium-status");
            _btnPremium = _root.Q<Button>("btn-premium");
            _btnClose = _root.Q<Button>("btn-close");
            _seasonPointValue = _root.Q<Label>("season-point-value");
            _shopGrid = _root.Q<VisualElement>("shop-grid");

            int tabCount = TAB_KEYS.Length;
            _tabElements = new VisualElement[tabCount];
            _contentElements = new VisualElement[tabCount];

            for (int i = 0; i < tabCount; i++)
            {
                string key = TAB_KEYS[i];
                _tabElements[i] = _root.Q<VisualElement>($"tab-{key}");
                _contentElements[i] = _root.Q<VisualElement>($"content-{key}");
            }
        }

        // ── 탭 바인딩 ──

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
            _btnClose?.RegisterCallback<ClickEvent>(_ => ClosePanel());
            _btnPremium?.RegisterCallback<ClickEvent>(_ => OnPremiumPurchaseClicked());
        }

        private void SwitchTab(int index)
        {
            if (index == _currentTab) return;
            if (index < 0 || index >= TAB_KEYS.Length) return;

            for (int i = 0; i < TAB_KEYS.Length; i++)
            {
                bool isActive = i == index;

                if (_tabElements[i] != null)
                {
                    if (isActive)
                        _tabElements[i].AddToClassList("bp-subtab--active");
                    else
                        _tabElements[i].RemoveFromClassList("bp-subtab--active");
                }

                if (_contentElements[i] != null)
                {
                    _contentElements[i].style.display = isActive
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }
            }

            _currentTab = index;

            if (index == 0)
                RefreshPassTab();
            else
                RefreshShopTab();

            Debug.Log($"[BattlePassPanelUI] 탭 전환: {TAB_KEYS[index]}");
        }

        // ── 패스 탭 갱신 ──

        private void RefreshPassTab()
        {
            RefreshSeasonInfo();
            RefreshBxpBar();
            BuildRewardTrack();
            RefreshPremiumStatus();
        }

        private void RefreshSeasonInfo()
        {
            if (_seasonInfoLabel == null) return;

            if (SeasonManager.Instance != null && SeasonManager.Instance.IsSeasonActive)
            {
                string seasonId = SeasonManager.Instance.CurrentSeasonId ?? "?";
                int daysRemaining = Mathf.Max(0, (int)SeasonManager.Instance.RemainingTime.TotalDays);
                _seasonInfoLabel.text = $"시즌 {seasonId} · {daysRemaining}일 남음";
            }
            else if (BattlePassSystem.Instance != null)
            {
                // SeasonManager 없을 때 BattlePassSystem 폴백
                _seasonInfoLabel.text = $"{BattlePassSystem.Instance.CurrentSeasonName} · {BattlePassSystem.Instance.RemainingDays}일 남음";
            }
            else
            {
                _seasonInfoLabel.text = "시즌 정보 없음";
            }
        }

        private void RefreshBxpBar()
        {
            if (BattlePassSystem.Instance == null)
            {
                if (_bxpLevelLabel != null) _bxpLevelLabel.text = "Lv. 0 / 50";
                if (_bxpExpLabel != null) _bxpExpLabel.text = "0 / 0 BXP";
                if (_bxpFill != null) _bxpFill.style.width = new Length(0, LengthUnit.Percent);
                return;
            }

            int level = BattlePassSystem.Instance.CurrentLevel;
            int currentExp = BattlePassSystem.Instance.CurrentBxp;
            int requiredExp = BattlePassSystem.Instance.BxpPerLevel;

            if (_bxpLevelLabel != null)
                _bxpLevelLabel.text = $"Lv. {level} / {MAX_LEVEL}";

            if (_bxpExpLabel != null)
                _bxpExpLabel.text = $"{currentExp:N0} / {requiredExp:N0} BXP";

            if (_bxpFill != null)
            {
                float fillPercent = requiredExp > 0 ? (float)currentExp / requiredExp * 100f : 0f;
                fillPercent = Mathf.Clamp(fillPercent, 0f, 100f);
                _bxpFill.style.width = new Length(fillPercent, LengthUnit.Percent);
            }
        }

        private void BuildRewardTrack()
        {
            if (_rewardTrack == null) return;
            _rewardTrack.Clear();

            bool hasBattlePass = BattlePassSystem.Instance != null;
            int currentLevel = hasBattlePass ? BattlePassSystem.Instance.CurrentLevel : 0;
            bool isPremium = hasBattlePass && BattlePassSystem.Instance.IsPremium;

            for (int lvl = 1; lvl <= MAX_LEVEL; lvl++)
            {
                var column = new VisualElement();
                column.AddToClassList("reward-column");

                bool isCurrent = lvl == currentLevel;
                if (isCurrent)
                    column.AddToClassList("reward-column--current");

                // 레벨 번호
                var levelLabel = new Label();
                levelLabel.AddToClassList("reward-column__level");
                levelLabel.text = $"{lvl}";
                column.Add(levelLabel);

                // 무료 보상 카드
                var freeLabel = new Label();
                freeLabel.AddToClassList("reward-track-label");
                freeLabel.AddToClassList("reward-track-label--free");
                freeLabel.text = "무료";
                column.Add(freeLabel);

                var freeCard = CreateRewardCard(lvl, false, currentLevel, isPremium);
                column.Add(freeCard);

                // 프리미엄 보상 카드
                var premLabel = new Label();
                premLabel.AddToClassList("reward-track-label");
                premLabel.AddToClassList("reward-track-label--premium");
                premLabel.text = "프리미엄";
                column.Add(premLabel);

                var premiumCard = CreateRewardCard(lvl, true, currentLevel, isPremium);
                column.Add(premiumCard);

                _rewardTrack.Add(column);
            }
        }

        private VisualElement CreateRewardCard(int level, bool isPremiumTrack, int currentLevel, bool hasPass)
        {
            var card = new VisualElement();
            card.AddToClassList("reward-card");
            card.AddToClassList(isPremiumTrack ? "reward-card--premium" : "reward-card--free");

            // 상태 판별
            RewardState state = GetRewardState(level, isPremiumTrack, currentLevel, hasPass);

            switch (state)
            {
                case RewardState.Claimed:
                    card.AddToClassList("reward-card--claimed");
                    var checkIcon = new VisualElement();
                    checkIcon.AddToClassList("reward-card__check");
                    card.Add(checkIcon);
                    break;

                case RewardState.Unlocked:
                    card.AddToClassList("reward-card--unlocked");
                    break;

                case RewardState.Locked:
                    card.AddToClassList("reward-card--locked");
                    if (isPremiumTrack && !hasPass)
                    {
                        var lockIcon = new VisualElement();
                        lockIcon.AddToClassList("reward-card__lock-icon");
                        card.Add(lockIcon);
                    }
                    break;
            }

            // 보상 아이콘 (CurrencyType 기반 자동 매핑)
            var icon = new VisualElement();
            icon.AddToClassList("reward-card__icon");
            var rewardSO = BattlePassSystem.Instance?.GetReward(level);
            if (rewardSO != null)
            {
                var currType = isPremiumTrack ? rewardSO.PremiumRewardType : rewardSO.FreeRewardType;
                string iconPath = GetCurrencyIconPath(currType);
                var sprite = Resources.Load<Sprite>(iconPath);
                if (sprite != null)
                    icon.style.backgroundImage = new StyleBackground(sprite);
            }
            card.Add(icon);

            // 보상 설명
            string rewardDesc = GetRewardDescription(level, isPremiumTrack);
            var label = new Label();
            label.AddToClassList("reward-card__label");
            label.text = rewardDesc;
            card.Add(label);

            // 클릭 이벤트 (수령 가능 시)
            if (state == RewardState.Unlocked)
            {
                int capturedLevel = level;
                bool capturedPremium = isPremiumTrack;
                card.RegisterCallback<ClickEvent>(_ => OnRewardCardClicked(capturedLevel, capturedPremium));
            }

            return card;
        }

        private enum RewardState { Locked, Unlocked, Claimed }

        private RewardState GetRewardState(int level, bool isPremiumTrack, int currentLevel, bool hasPass)
        {
            if (BattlePassSystem.Instance == null)
                return RewardState.Locked;

            // 프리미엄 트랙은 패스 구매 필요
            if (isPremiumTrack && !hasPass)
                return RewardState.Locked;

            // 레벨 미도달
            if (level > currentLevel)
                return RewardState.Locked;

            // 이미 수령
            bool isClaimed = isPremiumTrack
                ? BattlePassSystem.Instance.IsPremiumRewardClaimed(level)
                : BattlePassSystem.Instance.IsFreeRewardClaimed(level);
            if (isClaimed)
                return RewardState.Claimed;

            return RewardState.Unlocked;
        }

        private string GetRewardDescription(int level, bool isPremiumTrack)
        {
            if (BattlePassSystem.Instance != null)
            {
                string desc = isPremiumTrack
                    ? BattlePassSystem.Instance.GetPremiumRewardDescription(level)
                    : BattlePassSystem.Instance.GetFreeRewardDescription(level);
                if (!string.IsNullOrEmpty(desc) && desc != "-")
                    return desc;
            }

            // 폴백: 기본 이름
            return isPremiumTrack ? $"프리미엄 {level}" : $"보상 {level}";
        }

        private void OnRewardCardClicked(int level, bool isPremiumTrack)
        {
            if (BattlePassSystem.Instance == null)
            {
                Debug.LogWarning("[BattlePassPanelUI] BattlePassSystem.Instance가 null");
                return;
            }

            bool success = isPremiumTrack
                ? BattlePassSystem.Instance.ClaimPremiumReward(level)
                : BattlePassSystem.Instance.ClaimFreeReward(level);

            if (success)
            {
                Debug.Log($"[BattlePassPanelUI] 보상 수령: Lv.{level} (프리미엄={isPremiumTrack})");
                RefreshPassTab();
            }
            else
            {
                Debug.Log($"[BattlePassPanelUI] 보상 수령 실패: Lv.{level} (프리미엄={isPremiumTrack})");
            }
        }

        // ── 프리미엄 패스 ──

        private void RefreshPremiumStatus()
        {
            bool isPremium = BattlePassSystem.Instance != null && BattlePassSystem.Instance.IsPremium;

            if (_premiumStatusLabel != null)
            {
                if (isPremium)
                {
                    _premiumStatusLabel.text = "프리미엄 패스 활성화";
                    _premiumStatusLabel.AddToClassList("premium-info__status--active");
                }
                else
                {
                    _premiumStatusLabel.text = "프리미엄 패스 미구매";
                    _premiumStatusLabel.RemoveFromClassList("premium-info__status--active");
                }
            }

            if (_btnPremium != null)
            {
                if (isPremium)
                {
                    _btnPremium.AddToClassList("btn-premium--purchased");
                    _btnPremium.SetEnabled(false);
                }
                else
                {
                    _btnPremium.RemoveFromClassList("btn-premium--purchased");
                    _btnPremium.SetEnabled(true);
                }
            }
        }

        private void OnPremiumPurchaseClicked()
        {
            if (BattlePassSystem.Instance == null)
            {
                Debug.LogWarning("[BattlePassPanelUI] BattlePassSystem.Instance가 null");
                return;
            }

            if (BattlePassSystem.Instance.IsPremium)
            {
                Debug.Log("[BattlePassPanelUI] 이미 프리미엄 구매 완료");
                return;
            }

            bool success = BattlePassSystem.Instance.PurchasePremium();
            if (success)
            {
                Debug.Log("[BattlePassPanelUI] 프리미엄 패스 구매 성공");
            }
            else
            {
                Debug.Log("[BattlePassPanelUI] 프리미엄 패스 구매 실패 (루비 부족)");
            }

            RefreshPassTab();
        }

        // ── 시즌상점 탭 갱신 ──

        private void RefreshShopTab()
        {
            RefreshSeasonPoints();
            BuildShopItems();
        }

        private void RefreshSeasonPoints()
        {
            if (_seasonPointValue == null) return;

            if (SeasonShopSystem.Instance != null)
            {
                int points = SeasonShopSystem.Instance.SeasonPoints;
                _seasonPointValue.text = $"{points:N0}";
            }
            else
            {
                _seasonPointValue.text = "0";
            }
        }

        private void BuildShopItems()
        {
            if (_shopGrid == null) return;
            _shopGrid.Clear();

            if (SeasonShopSystem.Instance == null)
            {
                Debug.Log("[BattlePassPanelUI] SeasonShopSystem 없음 — 상점 아이템 미표시");
                return;
            }

            var shopItems = SeasonShopSystem.Instance.Items;
            if (shopItems == null) return;

            int currentPoints = SeasonShopSystem.Instance.SeasonPoints;

            for (int i = 0; i < shopItems.Length; i++)
            {
                var item = shopItems[i];
                if (item == null) continue;

                var card = CreateShopItemCard(item, currentPoints);
                _shopGrid.Add(card);
            }
        }

        private VisualElement CreateShopItemCard(SeasonShopItem item, int currentPoints)
        {
            var card = new VisualElement();
            card.AddToClassList("shop-item-card");

            // 품절 여부: maxPurchase > 0이고 구매 횟수가 maxPurchase에 도달
            bool isSoldOut = item.maxPurchase > 0
                && SeasonShopSystem.Instance != null
                && SeasonShopSystem.Instance.GetRemainingPurchases(item.id) <= 0;

            if (isSoldOut)
                card.AddToClassList("shop-item-card--sold-out");

            // 아이콘 — 보상 통화 아이콘으로 대응
            var icon = new VisualElement();
            icon.AddToClassList("shop-item-card__icon");
            string shopIconPath = GetCurrencyIconPath(item.rewardType);
            var shopIconSprite = Resources.Load<Sprite>(shopIconPath);
            if (shopIconSprite != null)
                icon.style.backgroundImage = new StyleBackground(shopIconSprite);
            card.Add(icon);

            // 이름
            var nameLabel = new Label();
            nameLabel.AddToClassList("shop-item-card__name");
            nameLabel.text = !string.IsNullOrEmpty(item.displayName) ? item.displayName : "아이템";
            card.Add(nameLabel);

            // 보상 설명
            string desc = $"{item.rewardType} x{item.rewardAmount}";
            var descLabel = new Label();
            descLabel.AddToClassList("shop-item-card__desc");
            descLabel.text = desc;
            card.Add(descLabel);

            // 남은 구매 횟수
            if (item.maxPurchase > 0 && SeasonShopSystem.Instance != null)
            {
                int remaining = SeasonShopSystem.Instance.GetRemainingPurchases(item.id);
                var remainLabel = new Label();
                remainLabel.AddToClassList("shop-item-card__desc");
                remainLabel.text = $"남은 횟수: {remaining}/{item.maxPurchase}";
                card.Add(remainLabel);
            }

            // 가격
            var costRow = new VisualElement();
            costRow.AddToClassList("shop-item-card__cost");

            var costIcon = new VisualElement();
            costIcon.AddToClassList("shop-item-card__cost-icon");
            var spPointSprite = Resources.Load<Sprite>("Icons/Stone/icon_star");
            if (spPointSprite != null)
                costIcon.style.backgroundImage = new StyleBackground(spPointSprite);
            costRow.Add(costIcon);

            var costLabel = new Label();
            costLabel.AddToClassList("shop-item-card__cost-label");
            costLabel.text = $"{item.seasonPointCost:N0}";
            costRow.Add(costLabel);

            card.Add(costRow);

            // 구매 버튼
            var buyBtn = new Button();
            buyBtn.AddToClassList("shop-item-card__buy-btn");

            bool canAfford = currentPoints >= item.seasonPointCost && !isSoldOut;
            if (!canAfford)
            {
                buyBtn.AddToClassList("shop-item-card__buy-btn--disabled");
                buyBtn.SetEnabled(false);
            }

            var buyLabel = new Label();
            buyLabel.AddToClassList("shop-item-card__buy-label");
            buyLabel.text = isSoldOut ? "품절" : "구매";
            buyBtn.Add(buyLabel);

            string capturedId = item.id;
            buyBtn.RegisterCallback<ClickEvent>(_ => OnShopItemBuyClicked(capturedId));

            card.Add(buyBtn);

            return card;
        }

        private void OnShopItemBuyClicked(string itemId)
        {
            if (SeasonShopSystem.Instance == null)
            {
                Debug.LogWarning("[BattlePassPanelUI] SeasonShopSystem.Instance가 null");
                return;
            }

            bool success = SeasonShopSystem.Instance.Purchase(itemId);
            if (success)
            {
                Debug.Log($"[BattlePassPanelUI] 시즌상점 구매 성공: {itemId}");
                RefreshShopTab();
            }
            else
            {
                Debug.Log($"[BattlePassPanelUI] 시즌상점 구매 실패: {itemId}");
            }
        }

        // ── 패널 닫기 ──

        private void ClosePanel()
        {
            Debug.Log("[BattlePassPanelUI] 패널 닫기");
            gameObject.SetActive(false);
        }

        // ── 이벤트 핸들러 ──

        private void OnBattlePassLevelUp(BattlePassLevelUpEvent evt)
        {
            if (_currentTab == 0)
                RefreshPassTab();
        }

        private void OnRewardClaimed(BattlePassRewardClaimedEvent evt)
        {
            if (_currentTab == 0)
                RefreshPassTab();
        }

        private void OnSeasonPointChanged(SeasonPointChangedEvent evt)
        {
            if (_currentTab == 1)
                RefreshShopTab();
        }

        private void OnPremiumActivated(BattlePassPremiumActivatedEvent evt)
        {
            if (_currentTab == 0)
                RefreshPassTab();
        }

        /// <summary>
        /// 통화 아이콘 경로. Resources/Icons/Stone에 없는 파일은 존재하는 파일로 fallback.
        /// (실파일: icon_gold, icon_gem, icon_star, icon_hammer, icon_gift)
        /// </summary>
        private static string GetCurrencyIconPath(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => "Icons/Stone/icon_gold",
                CurrencyType.Ruby => "Icons/Stone/icon_gem",
                CurrencyType.BlueDiamond => "Icons/Stone/icon_gem",
                CurrencyType.RuneFragment => "Icons/Stone/icon_star",
                CurrencyType.StarCrystal => "Icons/Stone/icon_star",
                CurrencyType.WeaponStone => "Icons/Stone/icon_hammer",
                CurrencyType.PotentialStone => "Icons/Stone/icon_gift",
                CurrencyType.SuperPotentialStone => "Icons/Stone/icon_gift",
                CurrencyType.QuickHuntTicket => "Icons/Stone/icon_gift",
                CurrencyType.ArenaTicket => "Icons/Stone/icon_gift",
                CurrencyType.WeaponTicket => "Icons/Stone/icon_gift",
                CurrencyType.ClimbToken => "Icons/Stone/icon_star",
                CurrencyType.HuntPoint => "Icons/Stone/icon_hammer",
                _ => "Icons/Stone/icon_gold",
            };
        }
    }
}
