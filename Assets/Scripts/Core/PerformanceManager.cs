using UnityEngine;

namespace MkLike.Core
{
    public class PerformanceManager : MonoBehaviour
    {
        public static PerformanceManager Instance { get; private set; }

        private const int FPS_BATTERY_SAVER = 30;
        private const int FPS_PERFORMANCE = 60;

        [SerializeField] private bool _isBatterySaver;

        public bool IsBatterySaver => _isBatterySaver;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var settings = SettingsManager.Instance;
            if (settings != null)
            {
                _isBatterySaver = false;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: vSyncCount=1 → requestAnimationFrame 사용 (브라우저 최적 경로)
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
#else
            SetFrameRate(_isBatterySaver ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);
            QualitySettings.vSyncCount = 0;
#endif
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void SetFrameRate(int fps)
        {
            Application.targetFrameRate = fps;
        }

        public void SetBatterySaver(bool enabled)
        {
            _isBatterySaver = enabled;
            SetFrameRate(enabled ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);

            if (enabled)
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            Debug.Log($"[PerformanceManager] 절전 모드: {enabled}, FPS: {Application.targetFrameRate}");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Application.targetFrameRate = 5;
                Time.timeScale = 0.1f;
                Save.SaveManager.Instance?.Save();
            }
            else
            {
                SetFrameRate(_isBatterySaver ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);
                Time.timeScale = 1f;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                SetFrameRate(_isBatterySaver ? FPS_BATTERY_SAVER : FPS_PERFORMANCE);
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
