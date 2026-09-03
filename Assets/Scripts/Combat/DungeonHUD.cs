using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 던전 전투 HUD. 화면 상단에 타이머 바 + 킬 카운터를 표시한다.
    /// DungeonBattleStartedEvent → 표시, DungeonBattleProgressEvent → 킬카운터/타이머 동기화,
    /// DungeonBattleEndedEvent → 숨김.
    /// Update()에서 로컬 카운트다운으로 타이머 바를 매 프레임 갱신한다.
    /// (Combat.asmdef 소속 — Dungeon 어셈블리 참조 없이 이벤트만 사용)
    /// </summary>
    public class DungeonHUD : MonoBehaviour
    {
        [Header("레이아웃")]
        [SerializeField] private float _barWidth = 600f;
        [SerializeField] private float _barHeight = 24f;
        [SerializeField] private float _topOffset = 60f;
        [SerializeField] private int _sortingOrder = 310;

        [Header("색상")]
        [SerializeField] private Color _barBgColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        [SerializeField] private Color _barFillColor = new Color(0.2f, 0.75f, 0.95f, 1f);
        [SerializeField] private Color _barFillUrgentColor = new Color(0.95f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color _panelBgColor = new Color(0.05f, 0.05f, 0.1f, 0.75f);

        [Header("폰트 크기")]
        [SerializeField] private int _nameFontSize = 20;
        [SerializeField] private int _killFontSize = 22;
        [SerializeField] private int _timeFontSize = 18;

        [Header("긴급 임계값 (초)")]
        [SerializeField] private float _urgentThreshold = 10f;

        // UI 요소
        private Canvas _canvas;
        private RectTransform _panelRect;
        private Image _panelBg;
        private TMP_Text _dungeonNameText;
        private RectTransform _barBgRect;
        private Image _barBgImage;
        private RectTransform _barFillRect;
        private Image _barFillImage;
        private TMP_Text _killCountText;
        private TMP_Text _remainingTimeText;

        // 상태 (로컬 카운트다운 — Dungeon 어셈블리 참조 불필요)
        private bool _isVisible;
        private float _remainingTime;
        private float _totalDuration;
        private int _killCount;
        private int _targetKills;

        private void Awake()
        {
            EnsureComponents();
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus<DungeonBattleStartedEvent>.Subscribe(OnBattleStarted);
            EventBus<DungeonBattleProgressEvent>.Subscribe(OnBattleProgress);
            EventBus<DungeonBattleEndedEvent>.Subscribe(OnBattleEnded);
        }

        private void OnDisable()
        {
            EventBus<DungeonBattleStartedEvent>.Unsubscribe(OnBattleStarted);
            EventBus<DungeonBattleProgressEvent>.Unsubscribe(OnBattleProgress);
            EventBus<DungeonBattleEndedEvent>.Unsubscribe(OnBattleEnded);
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (_panelRect != null)
                _panelRect.DOKill();
        }

        private void Update()
        {
            if (!_isVisible) return;

            // 로컬 카운트다운
            _remainingTime -= Time.deltaTime;
            if (_remainingTime < 0f) _remainingTime = 0f;

            float ratio = _totalDuration > 0f ? Mathf.Clamp01(_remainingTime / _totalDuration) : 0f;

            // 타이머 바 갱신
            UpdateTimerBar(ratio, _remainingTime);

            // 남은 시간 텍스트
            if (_remainingTimeText != null)
                _remainingTimeText.text = $"{_remainingTime:F1}초";
        }

        // ── 이벤트 핸들러 ──

        private void OnBattleStarted(DungeonBattleStartedEvent evt)
        {
            _totalDuration = evt.Duration;
            _remainingTime = evt.Duration;
            _targetKills = evt.TargetKills;
            _killCount = 0;

            if (_dungeonNameText != null)
                _dungeonNameText.text = evt.DungeonName;

            UpdateKillText();
            UpdateTimerBar(1f, _totalDuration);

            if (_remainingTimeText != null)
                _remainingTimeText.text = $"{_totalDuration:F1}초";

            SetVisible(true);
        }

        private void OnBattleProgress(DungeonBattleProgressEvent evt)
        {
            _killCount = evt.KillCount;
            _targetKills = evt.TargetKills;

            // 서버(컨트롤러) 시간과 동기화
            _remainingTime = evt.RemainingTime;

            UpdateKillText();
        }

        private void OnBattleEnded(DungeonBattleEndedEvent evt)
        {
            SetVisible(false);
        }

        // ── 내부 갱신 ──

        private void UpdateKillText()
        {
            if (_killCountText != null)
                _killCountText.text = $"{_killCount} / {_targetKills}";
        }

        private void UpdateTimerBar(float ratio, float remaining)
        {
            if (_barFillRect == null) return;

            // width 비율로 바 크기 조절
            var size = _barFillRect.sizeDelta;
            size.x = _barWidth * ratio;
            _barFillRect.sizeDelta = size;

            // 긴급 색상 전환
            if (_barFillImage != null)
            {
                _barFillImage.color = remaining <= _urgentThreshold
                    ? _barFillUrgentColor
                    : _barFillColor;
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_panelRect != null)
                _panelRect.gameObject.SetActive(visible);
        }

        // ── UI 동적 생성 ──

        private void EnsureComponents()
        {
            // Canvas 확인/생성
            if (GetComponentInParent<Canvas>() == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = _sortingOrder;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            BuildUI();
        }

        private void BuildUI()
        {
            // ── 패널 (상단 중앙) ──
            var panelObj = new GameObject("DungeonHUD_Panel");
            panelObj.transform.SetParent(transform, false);
            _panelRect = panelObj.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 1f);
            _panelRect.anchorMax = new Vector2(0.5f, 1f);
            _panelRect.pivot = new Vector2(0.5f, 1f);
            _panelRect.anchoredPosition = new Vector2(0f, -_topOffset);
            _panelRect.sizeDelta = new Vector2(_barWidth + 40f, 100f);

            // 패널 배경
            _panelBg = panelObj.AddComponent<Image>();
            _panelBg.color = _panelBgColor;
            _panelBg.raycastTarget = false;

            // ── 던전 이름 ──
            var nameObj = CreateTextObject("DungeonName", _panelRect);
            _dungeonNameText = nameObj.GetComponent<TMP_Text>();
            _dungeonNameText.fontSize = _nameFontSize;
            _dungeonNameText.fontStyle = FontStyles.Bold;
            _dungeonNameText.alignment = TextAlignmentOptions.Center;
            _dungeonNameText.color = Color.white;
            _dungeonNameText.text = "";

            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(0f, 28f);

            // ── 타이머 바 배경 ──
            var barBgObj = new GameObject("TimerBarBg");
            barBgObj.transform.SetParent(_panelRect, false);
            _barBgRect = barBgObj.AddComponent<RectTransform>();
            _barBgRect.anchorMin = new Vector2(0.5f, 1f);
            _barBgRect.anchorMax = new Vector2(0.5f, 1f);
            _barBgRect.pivot = new Vector2(0.5f, 1f);
            _barBgRect.anchoredPosition = new Vector2(0f, -34f);
            _barBgRect.sizeDelta = new Vector2(_barWidth, _barHeight);

            _barBgImage = barBgObj.AddComponent<Image>();
            _barBgImage.color = _barBgColor;
            _barBgImage.raycastTarget = false;

            // ── 타이머 바 전경 (좌측 정렬, width로 비율 조절) ──
            var barFillObj = new GameObject("TimerBarFill");
            barFillObj.transform.SetParent(_barBgRect, false);
            _barFillRect = barFillObj.AddComponent<RectTransform>();
            _barFillRect.anchorMin = new Vector2(0f, 0f);
            _barFillRect.anchorMax = new Vector2(0f, 1f);
            _barFillRect.pivot = new Vector2(0f, 0.5f);
            _barFillRect.anchoredPosition = Vector2.zero;
            _barFillRect.sizeDelta = new Vector2(_barWidth, 0f);

            _barFillImage = barFillObj.AddComponent<Image>();
            _barFillImage.color = _barFillColor;
            _barFillImage.raycastTarget = false;

            // ── 남은 시간 텍스트 (바 위에 중앙) ──
            var timeObj = CreateTextObject("RemainingTime", _barBgRect);
            _remainingTimeText = timeObj.GetComponent<TMP_Text>();
            _remainingTimeText.fontSize = _timeFontSize;
            _remainingTimeText.fontStyle = FontStyles.Bold;
            _remainingTimeText.alignment = TextAlignmentOptions.Center;
            _remainingTimeText.color = Color.white;
            _remainingTimeText.text = "";

            var timeRect = timeObj.GetComponent<RectTransform>();
            timeRect.anchorMin = Vector2.zero;
            timeRect.anchorMax = Vector2.one;
            timeRect.offsetMin = Vector2.zero;
            timeRect.offsetMax = Vector2.zero;

            // ── 킬 카운터 (바 아래) ──
            var killObj = CreateTextObject("KillCount", _panelRect);
            _killCountText = killObj.GetComponent<TMP_Text>();
            _killCountText.fontSize = _killFontSize;
            _killCountText.fontStyle = FontStyles.Bold;
            _killCountText.alignment = TextAlignmentOptions.Center;
            _killCountText.color = new Color(1f, 0.9f, 0.3f, 1f);
            _killCountText.text = "0 / 0";

            var killRect = killObj.GetComponent<RectTransform>();
            killRect.anchorMin = new Vector2(0f, 1f);
            killRect.anchorMax = new Vector2(1f, 1f);
            killRect.pivot = new Vector2(0.5f, 1f);
            killRect.anchoredPosition = new Vector2(0f, -62f);
            killRect.sizeDelta = new Vector2(0f, 30f);
        }

        private GameObject CreateTextObject(string name, RectTransform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            return obj;
        }
    }
}
