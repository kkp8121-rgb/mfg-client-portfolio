using Cysharp.Threading.Tasks;
using MkLike.Core.Save;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.Core.Net
{
    /// <summary>
    /// 서버 연결 초기화 + 프로필 동기화.
    /// Play 모드 시작 시 자동으로 dev 인증 → 로그인 → SaveProvider 전환을 수행한다.
    /// 레벨업/CP 변경 시 서버 프로필을 자동 업데이트한다.
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private string _serverUrl = "http://localhost:5035/api/v1";
        [SerializeField] private string _devUid = "dev-player-001";
        [SerializeField] private bool _useServerSave = true;

        /// <summary>서버 연결 완료 여부</summary>
        public static bool IsConnected { get; private set; }

        /// <summary>프로필 동기화 디바운스 간격 (밀리초)</summary>
        private const int PROFILE_SYNC_DEBOUNCE_MS = 3000;

        private bool _profileSyncPending;
        private int _lastSyncedLevel;
        private long _lastSyncedCp;
        private int _currentLevel;
        private long _currentCp;

        private void Start()
        {
            InitializeAsync().Forget();
        }

        private void OnEnable()
        {
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<CpChangedEvent>.Subscribe(OnCpChanged);
        }

        private void OnDisable()
        {
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<CpChangedEvent>.Unsubscribe(OnCpChanged);
        }

        private async UniTaskVoid InitializeAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            // 1) 서버 URL 설정
            ApiClient.SetBaseUrl(_serverUrl);

            // 2) 인증 설정 (에디터: dev 바이패스, 빌드: Firebase)
#if UNITY_EDITOR
            ApiClient.SetDevAuth(_devUid);
            Debug.Log($"[ServerBootstrap] Dev 인증 설정: {_devUid}");
#else
            // TODO: Firebase Auth → ApiClient.SetAuthToken(firebaseIdToken)
            Debug.Log("[ServerBootstrap] 프로덕션 인증 — Firebase 연동 필요");
            return;
#endif

            // 3) 로그인 요청
            var loginResponse = await ApiClient.PostAsync<LoginResponse>("auth/login", new LoginRequest(), ct);

            if (!loginResponse.success)
            {
                Debug.LogWarning($"[ServerBootstrap] 로그인 실패: {loginResponse.error}");
                return;
            }

            Debug.Log($"[ServerBootstrap] 로그인 성공: {loginResponse.data.nickname} (신규: {loginResponse.data.isNewPlayer})");

            // 4) SaveProvider 전환
            if (_useServerSave && SaveManager.Instance != null)
            {
                var serverProvider = new ServerSaveProvider();
                SaveManager.Instance.SetProvider(serverProvider);

                // 서버에서 세이브 데이터 로드 시도
                var serverData = await serverProvider.LoadFromServerAsync(ct);
                if (serverData != null)
                {
                    SaveManager.Instance.Initialize();
                }
            }

            IsConnected = true;
            Debug.Log("[ServerBootstrap] 서버 연결 완료");

            // 5) 서버 연결 이벤트 발행 (CurrencyManager 등이 구독하여 동기화)
            EventBus.Publish(new ServerConnectedEvent());
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            _currentLevel = evt.CurrentLevel;
            ScheduleProfileSync();
        }

        private void OnCpChanged(CpChangedEvent evt)
        {
            _currentCp = evt.CurrentCp;
            ScheduleProfileSync();
        }

        /// <summary>
        /// 프로필 동기화를 디바운스 예약한다.
        /// 연속 이벤트(레벨업+CP 동시 변경 등)를 묶어 1회만 호출.
        /// </summary>
        private void ScheduleProfileSync()
        {
            if (!IsConnected || !ApiClient.HasAuth) return;
            if (_profileSyncPending) return;

            _profileSyncPending = true;
            DebouncedProfileSync().Forget();
        }

        private async UniTaskVoid DebouncedProfileSync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            await UniTask.Delay(PROFILE_SYNC_DEBOUNCE_MS, ignoreTimeScale: true, cancellationToken: ct);

            _profileSyncPending = false;

            int level = _currentLevel;
            long cp = _currentCp;

            // 변경 없으면 스킵
            if (level == _lastSyncedLevel && cp == _lastSyncedCp) return;

            var request = new PlayerProfileUpdateRequest
            {
                level = level,
                combatPower = cp
            };

            var response = await ApiClient.PatchAsync<PlayerProfileResponse>("auth/profile", request, ct);

            if (response.success)
            {
                _lastSyncedLevel = level;
                _lastSyncedCp = cp;
                Debug.Log($"[ServerBootstrap] 프로필 동기화: Lv.{level}, CP:{cp}");
            }
        }

        private void OnDestroy()
        {
            IsConnected = false;
        }
    }
}
