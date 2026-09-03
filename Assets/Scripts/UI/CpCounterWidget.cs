using DG.Tweening;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace MkLike.UI
{
    /// <summary>
    /// 메인 화면 CP(전투력) 위젯.
    /// CP 변동 시 카운트업 애니메이션 + 증감 화살표 + 색상 플래시를 재생한다.
    /// EquipmentChangedEvent, LevelUpEvent 등 성장 관련 이벤트를 구독.
    /// </summary>
    public class CpCounterWidget : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private TMP_Text _cpValueText;
        [SerializeField] private TMP_Text _cpDeltaText;
        [SerializeField] private Image _arrowImage;
        [SerializeField] private RectTransform _cpContainer;

        [Header("설정")]
        [SerializeField] private float _countUpDuration = 0.6f;
        [SerializeField] private float _deltaDisplayDuration = 2f;

        [Header("마일스톤 연출")]
        [SerializeField] private TMP_Text _reasonText;
        [SerializeField] private int _milestoneInterval = 1000;

        [Header("색상")]
        [SerializeField] private Color _increaseColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _decreaseColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _milestoneColor = new Color(1f, 0.85f, 0.1f);

        private CombatStats _playerStats;
        private long _displayedCp;
        private long _previousCp;
        private Tween _countTween;
        private Tween _deltaTween;
        private Sequence _deltaFadeSeq;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EventBus<EquipmentChangedEvent>.Subscribe(OnStatChanged);
            EventBus<LevelUpEvent>.Subscribe(OnStatChanged);
            EventBus<WeaponChangedEvent>.Subscribe(OnStatChanged);
            EventBus<JobChangedEvent>.Subscribe(OnStatChanged);
            EventBus<StatAllocatedEvent>.Subscribe(OnStatChanged);
            EventBus<InscriptionChangedEvent>.Subscribe(OnStatChanged);
            EventBus<CpChangedEvent>.Subscribe(OnCpChanged);
        }

        private void OnDisable()
        {
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnStatChanged);
            EventBus<LevelUpEvent>.Unsubscribe(OnStatChanged);
            EventBus<WeaponChangedEvent>.Unsubscribe(OnStatChanged);
            EventBus<JobChangedEvent>.Unsubscribe(OnStatChanged);
            EventBus<StatAllocatedEvent>.Unsubscribe(OnStatChanged);
            EventBus<InscriptionChangedEvent>.Unsubscribe(OnStatChanged);
            EventBus<CpChangedEvent>.Unsubscribe(OnCpChanged);
        }

        public void SetPlayerStats(CombatStats stats)
        {
            _playerStats = stats;
            if (_playerStats != null)
            {
                _displayedCp = CombatFormula.CalculateCP(_playerStats);
                _previousCp = _displayedCp;
                UpdateText(_displayedCp);
            }
        }

        /// <summary>
        /// 즉시 CP를 갱신한다 (초기화용).
        /// </summary>
        public void RefreshImmediate()
        {
            if (_playerStats == null) return;
            _displayedCp = CombatFormula.CalculateCP(_playerStats);
            _previousCp = _displayedCp;
            UpdateText(_displayedCp);
            HideDelta();
        }

        private void OnStatChanged<T>(T evt) where T : struct, IEvent
        {
            RecalculateAndAnimate();
        }

        private void RecalculateAndAnimate()
        {
            if (_playerStats == null) return;

            long newCp = CombatFormula.CalculateCP(_playerStats);
            if (newCp == _displayedCp) return;

            _previousCp = _displayedCp;
            long delta = newCp - _previousCp;

            // 카운트업 애니메이션
            _countTween?.Kill();
            long startCp = _displayedCp;
            _countTween = DOVirtual.Float(0f, 1f, _countUpDuration, t =>
            {
                long current = (long)Mathf.Lerp(startCp, newCp, t);
                UpdateText(current);
            })
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _displayedCp = newCp;
                UpdateText(newCp);
            });

            // 증감 표시
            ShowDelta(delta);

            // 컨테이너 펀치 스케일
            if (_cpContainer != null)
            {
                _cpContainer.DOKill();
                _cpContainer.localScale = Vector3.one;
                _cpContainer.DOPunchScale(Vector3.one * 0.15f, 0.4f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // CP 텍스트 색상 플래시
            if (_cpValueText != null)
            {
                Color flashColor = delta > 0 ? _increaseColor : _decreaseColor;
                ColorTweenHelper.To(_cpValueText, flashColor, 0.15f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (_cpValueText != null)
                        {
                            ColorTweenHelper.To(_cpValueText, _normalColor, 0.4f)
                                .SetUpdate(true)
                                .SetLink(gameObject);
                        }
                    });
            }
        }

        private void ShowDelta(long delta)
        {
            if (_cpDeltaText == null) return;

            _deltaFadeSeq?.Kill();

            bool isIncrease = delta > 0;
            Color color = isIncrease ? _increaseColor : _decreaseColor;
            string sign = isIncrease ? "+" : "";

            _cpDeltaText.text = $"{sign}{NumberFormatter.FormatKorean(System.Math.Abs(delta))}";
            _cpDeltaText.color = color;
            _cpDeltaText.gameObject.SetActive(true);
            _cpDeltaText.transform.localScale = Vector3.zero;

            // 화살표
            if (_arrowImage != null)
            {
                _arrowImage.gameObject.SetActive(true);
                _arrowImage.color = color;
                // 위(증가) 또는 아래(감소) 회전
                _arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, isIncrease ? 0f : 180f);
            }

            var cg = _cpDeltaText.GetComponent<CanvasGroup>();
            if (cg == null) cg = _cpDeltaText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            _deltaFadeSeq = DOTween.Sequence()
                .Append(_cpDeltaText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                .AppendInterval(_deltaDisplayDuration)
                .Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.3f))
                .OnComplete(() => HideDelta())
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HideDelta()
        {
            if (_cpDeltaText != null)
                _cpDeltaText.gameObject.SetActive(false);
            if (_arrowImage != null)
                _arrowImage.gameObject.SetActive(false);
        }

        private void UpdateText(long cp)
        {
            if (_cpValueText != null)
                _cpValueText.text = NumberFormatter.FormatKorean(cp);
        }

        private void OnCpChanged(CpChangedEvent evt)
        {
            // 마일스톤 검사: 이전 CP와 현재 CP가 1000 경계를 넘었는지
            if (_milestoneInterval > 0 && evt.Delta > 0)
            {
                long prevMilestone = evt.PreviousCp / _milestoneInterval;
                long currMilestone = evt.CurrentCp / _milestoneInterval;

                if (currMilestone > prevMilestone)
                {
                    PlayMilestoneEffect(evt.CurrentCp).Forget();
                }
            }

            // CP 변화 원인 텍스트 표시
            if (_reasonText != null && !string.IsNullOrEmpty(evt.Reason))
            {
                ShowReasonText(evt.Reason, evt.Delta);
            }
        }

        private async UniTaskVoid PlayMilestoneEffect(long cp)
        {
            var token = this.GetCancellationTokenOnDestroy();

            AudioManager.Instance?.PlaySfx(SfxType.UiReward);

            // CP 텍스트 골드 플래시 + 강한 펀치
            if (_cpValueText != null)
            {
                ColorTweenHelper.To(_cpValueText, _milestoneColor, 0.2f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_cpContainer != null)
            {
                _cpContainer.DOKill();
                _cpContainer.localScale = Vector3.one;
                _cpContainer.DOPunchScale(Vector3.one * 0.3f, 0.5f, 8, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            await UniTask.Delay(800, cancellationToken: token, ignoreTimeScale: true);

            // 색상 복귀
            if (_cpValueText != null)
            {
                ColorTweenHelper.To(_cpValueText, _normalColor, 0.4f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private void ShowReasonText(string reason, long delta)
        {
            if (_reasonText == null) return;

            string sign = delta > 0 ? "+" : "";
            _reasonText.text = $"{reason} CP {sign}{NumberFormatter.FormatKorean(System.Math.Abs(delta))}";
            _reasonText.color = delta > 0 ? _increaseColor : _decreaseColor;
            _reasonText.gameObject.SetActive(true);
            _reasonText.transform.localScale = Vector3.zero;

            var cg = _reasonText.GetComponent<CanvasGroup>();
            if (cg == null) cg = _reasonText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            DOTween.Sequence()
                .Append(_reasonText.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                .AppendInterval(1.5f)
                .Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.3f))
                .OnComplete(() =>
                {
                    if (_reasonText != null)
                        _reasonText.gameObject.SetActive(false);
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void EnsureComponents()
        {
            if (_cpContainer == null)
            {
                var canvas = GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 5;
                }
                if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                _cpContainer = GetComponent<RectTransform>();
                if (_cpContainer == null)
                    _cpContainer = gameObject.AddComponent<RectTransform>();
            }

            if (_cpValueText == null)
            {
                var go = new GameObject("CpValue", typeof(RectTransform));
                go.transform.SetParent(_cpContainer, false);
                _cpValueText = go.AddComponent<TextMeshProUGUI>();
                _cpValueText.fontSize = 28;
                _cpValueText.alignment = TextAlignmentOptions.Center;
                _cpValueText.color = _normalColor;
                _cpValueText.text = "0";
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(200f, 40f);
            }

            if (_cpDeltaText == null)
            {
                var go = new GameObject("CpDelta", typeof(RectTransform));
                go.transform.SetParent(_cpContainer, false);
                _cpDeltaText = go.AddComponent<TextMeshProUGUI>();
                _cpDeltaText.fontSize = 18;
                _cpDeltaText.alignment = TextAlignmentOptions.Center;
                _cpDeltaText.color = _increaseColor;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -30f);
                rt.sizeDelta = new Vector2(160f, 30f);
                go.SetActive(false);
            }

            if (_arrowImage == null)
            {
                var go = new GameObject("Arrow", typeof(RectTransform));
                go.transform.SetParent(_cpContainer, false);
                _arrowImage = go.AddComponent<Image>();
                _arrowImage.color = _increaseColor;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-90f, -30f);
                rt.sizeDelta = new Vector2(16f, 16f);
                go.SetActive(false);
            }

            if (_reasonText == null)
            {
                var go = new GameObject("Reason", typeof(RectTransform));
                go.transform.SetParent(_cpContainer, false);
                _reasonText = go.AddComponent<TextMeshProUGUI>();
                _reasonText.fontSize = 14;
                _reasonText.alignment = TextAlignmentOptions.Center;
                _reasonText.color = _normalColor;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -55f);
                rt.sizeDelta = new Vector2(200f, 24f);
                go.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _countTween?.Kill();
            _deltaTween?.Kill();
            _deltaFadeSeq?.Kill();
            if (_cpContainer != null) _cpContainer.DOKill();
            if (_cpValueText != null) DOTween.Kill(_cpValueText);
            if (_cpDeltaText != null) _cpDeltaText.transform.DOKill();
            if (_reasonText != null) _reasonText.transform.DOKill();
        }
    }
}
