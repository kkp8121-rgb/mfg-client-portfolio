using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 오프라인 보상 복귀 팝업.
    /// OfflineRewardClaimedEvent 구독 → 자동 Show, 카운트업 연출, 광고 2배 버튼.
    /// </summary>
    public class OfflineRewardPopup : BasePopup
    {
        [SerializeField] private TMP_Text _elapsedText;
        [SerializeField] private TMP_Text _killCountText;
        [SerializeField] private TMP_Text _goldCountText;
        [SerializeField] private TMP_Text _expCountText;
        [SerializeField] private TMP_Text _itemDropText;
        [SerializeField] private Button _ad2xButton;
        [SerializeField] private Button _claimButton;

        [SerializeField] private Image _glowBackground;
        [SerializeField] private float _countUpDuration = 1.5f;

        private long _targetGold;
        private long _targetExp;
        private int _targetKills;
        private OfflineRewardClaimedEvent _lastEvent;
        private Tween _goldTween;
        private Tween _expTween;
        private Tween _killTween;
        private Tween _glowTween;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
            EventBus<OfflineRewardClaimedEvent>.Subscribe(OnOfflineRewardClaimed);

            if (_ad2xButton != null)
                _ad2xButton.onClick.AddListener(OnAd2xClicked);

            if (_claimButton != null)
                _claimButton.onClick.AddListener(OnClaimClicked);

            // 초기에는 숨김 — 이벤트 수신 시에만 Show()
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventBus<OfflineRewardClaimedEvent>.Unsubscribe(OnOfflineRewardClaimed);
            KillCountTweens();
        }

        private void OnOfflineRewardClaimed(OfflineRewardClaimedEvent evt)
        {
            _targetGold = evt.GoldAmount;
            _targetExp = evt.ExpAmount;
            _targetKills = evt.TotalKills;
            _lastEvent = evt;

            int hours = evt.ElapsedMinutes / 60;
            int minutes = evt.ElapsedMinutes % 60;

            if (_elapsedText != null)
                _elapsedText.text = hours > 0
                    ? $"{hours}시간 {minutes}분"
                    : $"{minutes}분";

            if (_killCountText != null)
                _killCountText.text = "0";

            if (_goldCountText != null)
                _goldCountText.text = "0";

            if (_expCountText != null)
                _expCountText.text = "0";

            // 아이템 드롭 요약 텍스트
            PopulateItemDropText(evt);

            // Phase 13: 배율 표시
            if (_elapsedText != null && evt.LevelMultiplier > 1f)
            {
                string baseText = _elapsedText.text;
                _elapsedText.text = $"{baseText} (x{evt.LevelMultiplier:F1} 보너스)";
            }

            Show();
            AudioManager.Instance?.PlaySfx("sfx_reward_open");
            PlayCountUp();
            PlayGlowEffect();
            SpawnCoinScatter();
        }

        private void PopulateItemDropText(OfflineRewardClaimedEvent evt)
        {
            if (_itemDropText == null) return;

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

        private void PlayCountUp()
        {
            KillCountTweens();

            if (_killCountText != null && _targetKills > 0)
            {
                _killTween = DOVirtual.Float(0f, _targetKills, _countUpDuration * 0.6f, v =>
                {
                    _killCountText.text = $"{NumberFormatter.FormatKorean((long)v)}마리 처치";
                }).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            }

            if (_goldCountText != null && _targetGold > 0)
            {
                _goldTween = DOVirtual.Float(0f, _targetGold, _countUpDuration, v =>
                {
                    _goldCountText.text = NumberFormatter.FormatKorean((long)v);
                }).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            }

            if (_expCountText != null && _targetExp > 0)
            {
                _expTween = DOVirtual.Float(0f, _targetExp, _countUpDuration, v =>
                {
                    _expCountText.text = NumberFormatter.FormatKorean((long)v);
                }).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            }

            // 카운트업 완료 시 SFX
            DOVirtual.DelayedCall(_countUpDuration, () =>
            {
                AudioManager.Instance?.PlaySfx("sfx_coin_count");
            }).SetUpdate(true).SetLink(gameObject);
        }

        private void OnAd2xClicked()
        {
            if (AdRewardManager.Instance == null) return;
            if (!AdRewardManager.Instance.CanWatchAd(AdRewardType.OfflineReward2x)) return;

            AdRewardManager.Instance.WatchAd(AdRewardType.OfflineReward2x);

            // 광고 2배: 동일 보상을 추가 지급
            if (AdRewardManager.Instance.HasOfflineReward2x)
            {
                CurrencyManager cm = CurrencyManager.Instance;
                if (cm != null)
                {
                    if (_targetGold > 0) cm.Add(CurrencyType.Gold, _targetGold);
                    if (_lastEvent.HuntPoint > 0) cm.Add(CurrencyType.HuntPoint, _lastEvent.HuntPoint);
                    if (_lastEvent.RuneFragment > 0) cm.Add(CurrencyType.RuneFragment, _lastEvent.RuneFragment);
                    if (_lastEvent.StarCrystal > 0) cm.Add(CurrencyType.StarCrystal, _lastEvent.StarCrystal);
                    if (_lastEvent.Ruby > 0) cm.Add(CurrencyType.Ruby, _lastEvent.Ruby);
                }

                if (_targetExp > 0)
                    EventBus.Publish(new ExpGainedEvent { Amount = _targetExp });

                AdRewardManager.Instance.ConsumeOfflineReward2x();

                // UI 갱신: 2배 금액으로 카운트업 재생
                _targetGold *= 2;
                _targetExp *= 2;
                PlayCountUp();

                // 버튼 비활성화 (중복 방지)
                if (_ad2xButton != null)
                    _ad2xButton.interactable = false;

                Debug.Log($"[OfflineRewardPopup] 광고 2배 보상 지급 — 골드: {_targetGold / 2}, EXP: {_targetExp / 2}");
            }
        }

        private void OnClaimClicked()
        {
            if (_claimButton != null)
            {
                _claimButton.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(Hide);
            }
            else
            {
                Hide();
            }
        }

        private void PlayGlowEffect()
        {
            _glowTween?.Kill();
            if (_glowBackground == null) return;

            _glowBackground.color = new Color(
                _glowBackground.color.r,
                _glowBackground.color.g,
                _glowBackground.color.b,
                0.15f);

            _glowTween = DOTween.To(() => _glowBackground.color.a, x => { var c = _glowBackground.color; c.a = x; _glowBackground.color = c; }, 0.4f, 1.2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 재화 아이콘이 위에서 쏟아지는 연출. Show 시 호출.
        /// </summary>
        private void SpawnCoinScatter()
        {
            int count = 10;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"CoinScatter_{i}", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);

                var img = go.AddComponent<Image>();
                img.raycastTarget = false;

                // IconRegistry에서 골드 아이콘 사용, 없으면 황금 사각형
                if (IconRegistry.Instance != null)
                {
                    var coinSprite = IconRegistry.Instance.GetCurrencyIcon(CurrencyType.Gold);
                    if (coinSprite != null) img.sprite = coinSprite;
                }
                img.color = new Color(1f, Random.Range(0.8f, 1f), Random.Range(0.1f, 0.4f), 1f);

                var rt = go.GetComponent<RectTransform>();
                float size = Random.Range(18f, 32f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchorMin = new Vector2(0.5f, 0.7f);
                rt.anchorMax = new Vector2(0.5f, 0.7f);

                // 위에서 시작
                float startX = Random.Range(-150f, 150f);
                float startY = Random.Range(50f, 150f);
                rt.anchoredPosition = new Vector2(startX, startY);

                // 아래 중앙으로 DOMove + 회전 + 페이드
                float delay = Random.Range(0f, 0.3f);
                float duration = Random.Range(0.5f, 0.9f);

                Vector2 endPos = new Vector2(
                    Random.Range(-80f, 80f),
                    Random.Range(-100f, -30f)
                );

                var seq = DOTween.Sequence().SetDelay(delay);
                seq.Append(DOTween.To(
                    () => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    endPos, duration
                ).SetEase(Ease.OutBounce));
                seq.Join(go.transform.DOScale(Random.Range(0.5f, 1f), duration));
                seq.Join(DOTween.To(
                    () => go.transform.eulerAngles.z,
                    z => go.transform.eulerAngles = new Vector3(0, 0, z),
                    Random.Range(-180f, 180f), duration
                ));
                seq.AppendInterval(0.3f);
                seq.Append(DOTween.To(
                    () => img.color.a,
                    x => { var c = img.color; c.a = x; img.color = c; },
                    0f, 0.25f
                ));
                seq.OnComplete(() => Destroy(go));
                seq.SetUpdate(true).SetLink(go);
            }
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // --- Root: full-screen dimmed overlay ---
            var rootRt = GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            var rootImg = GetComponent<Image>();
            if (rootImg == null)
            {
                rootImg = gameObject.AddComponent<Image>();
                rootImg.color = new Color(0f, 0f, 0f, 0.7f);
                rootImg.raycastTarget = true;
            }

            // --- Popup panel: centered dark background ---
            Transform panelTransform;
            var existingPanel = transform.Find("PopupPanel");
            if (existingPanel != null)
            {
                panelTransform = existingPanel;
            }
            else
            {
                var panelGo = new GameObject("PopupPanel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                var panelImg = panelGo.AddComponent<Image>();
                panelImg.color = Color.white;
                var tm = UIThemeManager.Instance;
                if (tm != null) tm.ApplyPanelBackground(panelImg);
                else panelImg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);
                panelImg.raycastTarget = true;
                var panelRt = panelGo.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                panelRt.pivot = new Vector2(0.5f, 0.5f);
                panelRt.anchoredPosition = Vector2.zero;
                panelRt.sizeDelta = new Vector2(420f, 440f);
                panelTransform = panelGo.transform;
            }

            // --- Glow background: fills popup panel, behind content ---
            if (_glowBackground == null)
            {
                var go = new GameObject("GlowBackground", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _glowBackground = go.AddComponent<Image>();
                _glowBackground.color = new Color(1f, 0.85f, 0.1f, 0.15f);
                _glowBackground.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.transform.SetAsFirstSibling();
            }

            // --- Title ---
            var titleTransform = panelTransform.Find("TitleText");
            if (titleTransform == null)
            {
                var go = new GameObject("TitleText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = "오프라인 보상";
                tmp.fontSize = 28f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f);
                rt.sizeDelta = new Vector2(0f, 50f);
            }

            // --- Elapsed time text ---
            if (_elapsedText == null)
            {
                var go = new GameObject("ElapsedText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _elapsedText = go.AddComponent<TextMeshProUGUI>();
                _elapsedText.fontSize = 18f;
                _elapsedText.alignment = TextAlignmentOptions.Center;
                _elapsedText.color = new Color(0.7f, 0.7f, 0.8f);
                _elapsedText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -70f);
                rt.sizeDelta = new Vector2(0f, 36f);
            }

            // --- Kill count text ---
            if (_killCountText == null)
            {
                var go = new GameObject("KillCountText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _killCountText = go.AddComponent<TextMeshProUGUI>();
                _killCountText.fontSize = 16f;
                _killCountText.alignment = TextAlignmentOptions.Center;
                _killCountText.color = new Color(0.7f, 0.9f, 0.7f);
                _killCountText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -100f);
                rt.sizeDelta = new Vector2(0f, 30f);
            }

            // --- Gold count text ---
            if (_goldCountText == null)
            {
                var go = new GameObject("GoldCountText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _goldCountText = go.AddComponent<TextMeshProUGUI>();
                _goldCountText.fontSize = 30f;
                _goldCountText.fontStyle = FontStyles.Bold;
                _goldCountText.alignment = TextAlignmentOptions.Center;
                _goldCountText.color = new Color(1f, 0.85f, 0.1f);
                _goldCountText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -135f);
                rt.sizeDelta = new Vector2(0f, 44f);
            }

            // --- Exp count text ---
            if (_expCountText == null)
            {
                var go = new GameObject("ExpCountText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _expCountText = go.AddComponent<TextMeshProUGUI>();
                _expCountText.fontSize = 30f;
                _expCountText.fontStyle = FontStyles.Bold;
                _expCountText.alignment = TextAlignmentOptions.Center;
                _expCountText.color = new Color(0.3f, 0.8f, 1f);
                _expCountText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -185f);
                rt.sizeDelta = new Vector2(0f, 44f);
            }

            // --- Item drop text ---
            if (_itemDropText == null)
            {
                var go = new GameObject("ItemDropText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _itemDropText = go.AddComponent<TextMeshProUGUI>();
                _itemDropText.fontSize = 14f;
                _itemDropText.alignment = TextAlignmentOptions.Center;
                _itemDropText.color = new Color(0.8f, 0.75f, 0.6f);
                _itemDropText.raycastTarget = false;
                _itemDropText.enableWordWrapping = true;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -230f);
                rt.sizeDelta = new Vector2(-30f, 40f);
            }

            // --- Ad 2x button: bottom-left of panel ---
            if (_ad2xButton == null)
            {
                var go = new GameObject("Ad2xButton", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                var btnImg = go.AddComponent<Image>();
                btnImg.color = Color.white;
                _ad2xButton = go.AddComponent<Button>();
                var tmAd = UIThemeManager.Instance;
                if (tmAd != null) tmAd.ApplyConfirmButton(btnImg);
                else btnImg.color = new Color(0.2f, 0.6f, 0.3f);
                var btnRt = go.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0f, 0f);
                btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0f);
                btnRt.anchoredPosition = new Vector2(0f, 20f);
                btnRt.sizeDelta = new Vector2(-30f, 50f);

                var txt = new GameObject("Text", typeof(RectTransform));
                txt.transform.SetParent(go.transform, false);
                var tmp = txt.AddComponent<TextMeshProUGUI>();
                tmp.text = "광고 2배";
                tmp.fontSize = 18f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                var txtRt = txt.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
            }

            // --- Claim button: bottom-right of panel ---
            if (_claimButton == null)
            {
                var go = new GameObject("ClaimButton", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                var btnImg = go.AddComponent<Image>();
                btnImg.color = Color.white;
                _claimButton = go.AddComponent<Button>();
                var tmClaim = UIThemeManager.Instance;
                if (tmClaim != null) tmClaim.ApplyButtonFull(_claimButton);
                else btnImg.color = new Color(0.3f, 0.5f, 0.8f);
                var btnRt = go.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0f);
                btnRt.anchorMax = new Vector2(1f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0f);
                btnRt.anchoredPosition = new Vector2(0f, 20f);
                btnRt.sizeDelta = new Vector2(-30f, 50f);

                var txt = new GameObject("Text", typeof(RectTransform));
                txt.transform.SetParent(go.transform, false);
                var tmp = txt.AddComponent<TextMeshProUGUI>();
                tmp.text = "수령";
                tmp.fontSize = 20f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                var txtRt = txt.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void KillCountTweens()
        {
            _killTween?.Kill();
            _killTween = null;
            _goldTween?.Kill();
            _goldTween = null;
            _expTween?.Kill();
            _expTween = null;
            _glowTween?.Kill();
            _glowTween = null;
        }
    }
}
