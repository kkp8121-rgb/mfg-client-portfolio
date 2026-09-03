using UnityEngine;

namespace MkLike.Core
{
    /// <summary>
    /// 게임 설정 관리 매니저.
    /// 사운드, 알림, 그래픽 설정을 관리하며, PlayerPrefs로 저장한다.
    /// 게임 데이터(SaveManager)와 분리된 독립 저장 체계.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // --- PlayerPrefs 키 ---
        private const string KEY_MASTER_VOLUME = "Settings_MasterVolume";
        private const string KEY_BGM_VOLUME = "Settings_BgmVolume";
        private const string KEY_SFX_VOLUME = "Settings_SfxVolume";
        private const string KEY_IS_MUTED = "Settings_IsMuted";
        private const string KEY_IS_BGM_MUTED = "Settings_IsBgmMuted";
        private const string KEY_IS_SFX_MUTED = "Settings_IsSfxMuted";
        private const string KEY_PUSH_NOTIFICATION = "Settings_PushNotification";
        private const string KEY_QUALITY_LEVEL = "Settings_QualityLevel";
        private const string KEY_BATTERY_SAVER = "Settings_BatterySaver";
        private const string KEY_HAS_COMPLETED_ONBOARDING = "Settings_HasCompletedOnboarding";

        // --- 사운드 ---

        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private float _bgmVolume = 0.7f;
        [SerializeField] private float _sfxVolume = 1f;
        [SerializeField] private bool _isMuted;
        [SerializeField] private bool _isBgmMuted;
        [SerializeField] private bool _isSfxMuted;

        /// <summary>마스터 볼륨 (0~1)</summary>
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                ApplyVolume();
                SaveSettings();
            }
        }

        /// <summary>BGM 볼륨 (0~1)</summary>
        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                AudioManager.Instance?.SetBgmVolume(_isBgmMuted ? 0f : _bgmVolume);
                ApplyVolume();
                SaveSettings();
            }
        }

        /// <summary>SFX 볼륨 (0~1)</summary>
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                AudioManager.Instance?.SetSfxVolume(_isSfxMuted ? 0f : _sfxVolume);
                ApplyVolume();
                SaveSettings();
            }
        }

        /// <summary>전체 음소거 여부</summary>
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                ApplyVolume();
                SaveSettings();
            }
        }

        /// <summary>BGM 음소거 여부</summary>
        public bool IsBgmMuted
        {
            get => _isBgmMuted;
            set
            {
                _isBgmMuted = value;
                AudioManager.Instance?.SetBgmVolume(value ? 0f : _bgmVolume);
                SaveSettings();
            }
        }

        /// <summary>SFX 음소거 여부</summary>
        public bool IsSfxMuted
        {
            get => _isSfxMuted;
            set
            {
                _isSfxMuted = value;
                AudioManager.Instance?.SetSfxVolume(value ? 0f : _sfxVolume);
                SaveSettings();
            }
        }

        // --- 알림 ---

        [SerializeField] private bool _pushNotification = true;

        /// <summary>푸시 알림 여부</summary>
        public bool PushNotification
        {
            get => _pushNotification;
            set
            {
                _pushNotification = value;
                SaveSettings();
            }
        }

        // --- 그래픽 ---

        [SerializeField] private int _qualityLevel = 1;

        /// <summary>그래픽 품질 (0=Low, 1=Medium, 2=High)</summary>
        public int QualityLevel
        {
            get => _qualityLevel;
            set
            {
                _qualityLevel = Mathf.Clamp(value, 0, 2);
                ApplyQuality();
                SaveSettings();
            }
        }

        // --- 배터리 절약 ---

        [SerializeField] private bool _isBatterySaver;

        /// <summary>
        /// 온보딩(로그인/닉네임/직업 선택) 완료 여부.
        /// PlayerPrefs에 저장되며, true면 Title 씬에서 Main 씬으로 자동 전환한다.
        /// </summary>
        public bool HasCompletedOnboarding
        {
            get => PlayerPrefs.GetInt(KEY_HAS_COMPLETED_ONBOARDING, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KEY_HAS_COMPLETED_ONBOARDING, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>배터리 절약 모드 여부. PerformanceManager 연동.</summary>
        public bool IsBatterySaver
        {
            get => _isBatterySaver;
            set
            {
                _isBatterySaver = value;
                PerformanceManager.Instance?.SetBatterySaver(value);
                SaveSettings();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            // Start에서 재적용: Awake 시점에는 AudioManager/PerformanceManager가 아직 초기화되지 않았을 수 있음
            ApplyVolume();
            PerformanceManager.Instance?.SetBatterySaver(_isBatterySaver);
        }

        /// <summary>
        /// 설정을 PlayerPrefs에 저장한다.
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, _masterVolume);
            PlayerPrefs.SetFloat(KEY_BGM_VOLUME, _bgmVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, _sfxVolume);
            PlayerPrefs.SetInt(KEY_IS_MUTED, _isMuted ? 1 : 0);
            PlayerPrefs.SetInt(KEY_IS_BGM_MUTED, _isBgmMuted ? 1 : 0);
            PlayerPrefs.SetInt(KEY_IS_SFX_MUTED, _isSfxMuted ? 1 : 0);
            PlayerPrefs.SetInt(KEY_PUSH_NOTIFICATION, _pushNotification ? 1 : 0);
            PlayerPrefs.SetInt(KEY_QUALITY_LEVEL, _qualityLevel);
            PlayerPrefs.SetInt(KEY_BATTERY_SAVER, _isBatterySaver ? 1 : 0);
            PlayerPrefs.Save();

            Debug.Log("[SettingsManager] 설정 저장 완료");
        }

        /// <summary>
        /// PlayerPrefs에서 설정을 로드한다. 저장된 값이 없으면 기본값 사용.
        /// </summary>
        public void LoadSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, 1f);
            _bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 0.7f);
            _sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
            _isMuted = PlayerPrefs.GetInt(KEY_IS_MUTED, 0) == 1;
            _isBgmMuted = PlayerPrefs.GetInt(KEY_IS_BGM_MUTED, 0) == 1;
            _isSfxMuted = PlayerPrefs.GetInt(KEY_IS_SFX_MUTED, 0) == 1;
            _pushNotification = PlayerPrefs.GetInt(KEY_PUSH_NOTIFICATION, 1) == 1;
            _qualityLevel = PlayerPrefs.GetInt(KEY_QUALITY_LEVEL, 1);
            _isBatterySaver = PlayerPrefs.GetInt(KEY_BATTERY_SAVER, 0) == 1;

            ApplyVolume();
            ApplyQuality();

            Debug.Log($"[SettingsManager] 설정 로드 완료 — 마스터:{_masterVolume}, BGM:{_bgmVolume}, SFX:{_sfxVolume}, 음소거:{_isMuted}, BGM뮤트:{_isBgmMuted}, SFX뮤트:{_isSfxMuted}, 품질:{_qualityLevel}, 배터리절약:{_isBatterySaver}");
        }

        /// <summary>
        /// 모든 설정을 기본값으로 초기화한다.
        /// </summary>
        public void ResetToDefaults()
        {
            _masterVolume = 1f;
            _bgmVolume = 0.7f;
            _sfxVolume = 1f;
            _isMuted = false;
            _isBgmMuted = false;
            _isSfxMuted = false;
            _pushNotification = true;
            _qualityLevel = 1;
            _isBatterySaver = false;

            ApplyVolume();
            ApplyQuality();
            SaveSettings();

            Debug.Log("[SettingsManager] 설정 기본값 복원 완료");
        }

        /// <summary>
        /// 실제 오디오 볼륨을 적용한다.
        /// AudioListener.volume으로 전역 마스터 볼륨을 조절하고,
        /// AudioManager에 개별 BGM/SFX 볼륨을 전달한다.
        /// </summary>
        private void ApplyVolume()
        {
            if (_isMuted)
            {
                AudioListener.volume = 0f;
            }
            else
            {
                AudioListener.volume = _masterVolume;
            }

            // AudioManager에 개별 채널 볼륨 전달
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmVolume(_isBgmMuted ? 0f : _bgmVolume);
                AudioManager.Instance.SetSfxVolume(_isSfxMuted ? 0f : _sfxVolume);
            }
        }

        /// <summary>
        /// 그래픽 품질을 적용한다.
        /// </summary>
        private void ApplyQuality()
        {
            QualitySettings.SetQualityLevel(_qualityLevel, true);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                SaveSettings();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
