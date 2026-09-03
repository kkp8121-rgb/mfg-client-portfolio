using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 설정 팝업.
    /// 사운드(BGM/SFX), 그래픽 품질, 배터리 절약 토글을 제공한다.
    /// SettingsManager와 연동하여 값을 읽고 쓴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsPopupUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _dimOverlay;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Label _bgmValueText;
        private Label _sfxValueText;
        private Toggle _bgmMuteToggle;
        private Toggle _sfxMuteToggle;
        private Button _qualityLowBtn;
        private Button _qualityMidBtn;
        private Button _qualityHighBtn;
        private Toggle _batterySaverToggle;
        private Toggle _pushNotificationToggle;
        private Label _accountInfoText;
        private Button _closeBtn;

        private bool _isRefreshing;
        private int _currentQuality;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100; // HUD(0)/TabBar(10) 위에 렌더링
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[SettingsPopupUI] rootVisualElement == null, UI 초기화 스킵");
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
            _bgmSlider = _root.Q<Slider>("bgm-slider");
            _sfxSlider = _root.Q<Slider>("sfx-slider");
            _bgmValueText = _root.Q<Label>("bgm-value-text");
            _sfxValueText = _root.Q<Label>("sfx-value-text");
            _bgmMuteToggle = _root.Q<Toggle>("bgm-mute-toggle");
            _sfxMuteToggle = _root.Q<Toggle>("sfx-mute-toggle");
            _qualityLowBtn = _root.Q<Button>("quality-low-btn");
            _qualityMidBtn = _root.Q<Button>("quality-mid-btn");
            _qualityHighBtn = _root.Q<Button>("quality-high-btn");
            _batterySaverToggle = _root.Q<Toggle>("battery-saver-toggle");
            _pushNotificationToggle = _root.Q<Toggle>("push-notification-toggle");
            _accountInfoText = _root.Q<Label>("account-info-text");
            _closeBtn = _root.Q<Button>("close-btn");
        }

        private void BindCallbacks()
        {
            if (_bgmSlider != null)
                _bgmSlider.RegisterValueChangedCallback(OnBgmSliderChanged);

            if (_sfxSlider != null)
                _sfxSlider.RegisterValueChangedCallback(OnSfxSliderChanged);

            if (_bgmMuteToggle != null)
                _bgmMuteToggle.RegisterValueChangedCallback(OnBgmMuteChanged);

            if (_sfxMuteToggle != null)
                _sfxMuteToggle.RegisterValueChangedCallback(OnSfxMuteChanged);

            if (_qualityLowBtn != null)
                _qualityLowBtn.RegisterCallback<ClickEvent>(_ => SetQuality(0));

            if (_qualityMidBtn != null)
                _qualityMidBtn.RegisterCallback<ClickEvent>(_ => SetQuality(1));

            if (_qualityHighBtn != null)
                _qualityHighBtn.RegisterCallback<ClickEvent>(_ => SetQuality(2));

            if (_batterySaverToggle != null)
                _batterySaverToggle.RegisterValueChangedCallback(OnBatterySaverChanged);

            if (_pushNotificationToggle != null)
                _pushNotificationToggle.RegisterValueChangedCallback(OnPushNotificationChanged);

            if (_closeBtn != null)
                _closeBtn.RegisterCallback<ClickEvent>(OnCloseClicked);

            // 딤 오버레이 클릭 시 닫기
            if (_dimOverlay != null)
            {
                _dimOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _dimOverlay)
                        SetVisible(false);
                });
            }
        }

        /// <summary>
        /// 팝업을 표시하고 현재 설정값을 반영한다.
        /// </summary>
        public void Show()
        {
            RefreshSettings();
            SetVisible(true);
        }

        private void RefreshSettings()
        {
            if (SettingsManager.Instance == null) return;

            _isRefreshing = true;

            var settings = SettingsManager.Instance;

            if (_bgmSlider != null)
                _bgmSlider.value = settings.BgmVolume;

            if (_sfxSlider != null)
                _sfxSlider.value = settings.SfxVolume;

            UpdateBgmValueText(settings.BgmVolume);
            UpdateSfxValueText(settings.SfxVolume);

            if (_bgmMuteToggle != null)
                _bgmMuteToggle.value = settings.IsBgmMuted;

            if (_sfxMuteToggle != null)
                _sfxMuteToggle.value = settings.IsSfxMuted;

            _currentQuality = settings.QualityLevel;
            UpdateQualityButtons(_currentQuality);

            if (_batterySaverToggle != null)
                _batterySaverToggle.value = settings.IsBatterySaver;

            if (_pushNotificationToggle != null)
                _pushNotificationToggle.value = settings.PushNotification;

            _isRefreshing = false;
        }

        // ── 사운드 콜백 ──

        private void OnBgmSliderChanged(ChangeEvent<float> evt)
        {
            UpdateBgmValueText(evt.newValue);
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.BgmVolume = evt.newValue;
        }

        private void OnSfxSliderChanged(ChangeEvent<float> evt)
        {
            UpdateSfxValueText(evt.newValue);
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.SfxVolume = evt.newValue;
        }

        private void OnBgmMuteChanged(ChangeEvent<bool> evt)
        {
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsBgmMuted = evt.newValue;
        }

        private void OnSfxMuteChanged(ChangeEvent<bool> evt)
        {
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsSfxMuted = evt.newValue;
        }

        // ── 그래픽 콜백 ──

        private static readonly string[] QUALITY_LABELS = { "저품질", "보통", "고품질" };

        private void SetQuality(int level)
        {
            _currentQuality = level;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.QualityLevel = level;
            UpdateQualityButtons(level);
            // 2026-04-23 침묵 액션 보완: 그래픽 품질 변경 Toast
            int clamped = Mathf.Clamp(level, 0, QUALITY_LABELS.Length - 1);
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"그래픽 품질: {QUALITY_LABELS[clamped]}",
                shakeIntensity: 0);
        }

        private void OnBatterySaverChanged(ChangeEvent<bool> evt)
        {
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.IsBatterySaver = evt.newValue;
        }

        private void OnPushNotificationChanged(ChangeEvent<bool> evt)
        {
            if (_isRefreshing) return;
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.PushNotification = evt.newValue;
        }

        // ── UI 갱신 ──

        private void UpdateBgmValueText(float value)
        {
            if (_bgmValueText != null)
                _bgmValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void UpdateSfxValueText(float value)
        {
            if (_sfxValueText != null)
                _sfxValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void UpdateQualityButtons(int selectedIndex)
        {
            Button[] buttons = { _qualityLowBtn, _qualityMidBtn, _qualityHighBtn };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                if (i == selectedIndex)
                    buttons[i].AddToClassList("quality-btn--active");
                else
                    buttons[i].RemoveFromClassList("quality-btn--active");
            }
        }

        private void OnCloseClicked(ClickEvent evt)
        {
            SetVisible(false);
        }

        private void OnDisable()
        {
            _root?.RemoveFromClassList("popup--open");
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
