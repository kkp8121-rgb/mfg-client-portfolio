using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Growth;
using MkLike.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// Title 씬의 온보딩 플로우 상태머신.
    /// Title → Login → (신규) Nickname → JobSelect → Main 전환 순서.
    /// 재접속 시 dev-uid 기반 자동 로그인 → 바로 Main 전환.
    /// </summary>
    public class OnboardingFlow : MonoBehaviour
    {
        [SerializeField] private string _mainSceneName = "Main";
        [SerializeField] private bool _autoStart = true;
        [Tooltip("Tap to start 게이트. 할당되어 있으면 탭 전까지 로그인 플로우를 대기시킨다.")]
        [SerializeField] private TitleTapToStartController _tapToStart;

        private string _pendingNickname;
        private JobType? _pendingJob;

        private void Start()
        {
            if (_autoStart)
                Run().Forget();
        }

        public async UniTaskVoid Run()
        {
            Debug.Log("[OnboardingFlow] 시작");

            // 0) Tap to start 게이트 — 사용자 입력 전까지 로그인 로직 지연
            await WaitForTapToStartAsync();

            // 1) 기존 완료 유저: 자동 로그인 시도 → 성공 시 바로 Main
            if (SettingsManager.Instance != null && SettingsManager.Instance.HasCompletedOnboarding)
            {
                if (LoginManager.Instance != null)
                {
                    var ok = await LoginManager.Instance.TryAutoLoginAsync();
                    if (ok)
                    {
                        Debug.Log("[OnboardingFlow] 자동 로그인 성공 → Main 전환");
                        LoadMain();
                        return;
                    }
                    Debug.LogWarning("[OnboardingFlow] 자동 로그인 실패 → 로그인 팝업 진행");
                }
            }

            // 2) 로그인 팝업
            var loginPopup = UIManager.Instance?.OpenPopup<LoginPopup>("LoginPopup");
            if (loginPopup == null)
            {
                Debug.LogError("[OnboardingFlow] LoginPopup 오픈 실패");
                return;
            }

            // 3) LoginCompletedEvent 대기
            var loginResult = await WaitForLoginAsync();

            // 4) 신규 유저: 닉네임 → 직업 → Submit
            if (loginResult.IsNewPlayer)
            {
                await RunNicknameStep();
                await RunJobSelectStep();

                // 서버 동기화 (실패해도 무시 — 로컬 진행)
                if (LoginManager.Instance != null && !string.IsNullOrEmpty(_pendingNickname) && _pendingJob.HasValue)
                {
                    var jobId = JobIdFromType(_pendingJob.Value);
                    await LoginManager.Instance.SubmitOnboardingAsync(_pendingNickname, jobId);
                }

                CompleteOnboarding();
            }
            else
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.HasCompletedOnboarding = true;
            }

            LoadMain();
        }

        private UniTask<LoginCompletedEvent> WaitForLoginAsync()
        {
            var tcs = new UniTaskCompletionSource<LoginCompletedEvent>();
            void Handler(LoginCompletedEvent evt)
            {
                EventBus<LoginCompletedEvent>.Unsubscribe(Handler);
                tcs.TrySetResult(evt);
            }
            EventBus<LoginCompletedEvent>.Subscribe(Handler);
            return tcs.Task;
        }

        /// <summary>
        /// Tap to start 게이트가 할당돼 있으면 사용자 입력을 대기한다.
        /// 탭 감지 후 라벨을 비활성화하여 애니메이션을 정지한다.
        /// </summary>
        private UniTask WaitForTapToStartAsync()
        {
            if (_tapToStart == null) return UniTask.CompletedTask;
            if (!_tapToStart.gameObject.activeInHierarchy) return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();
            void Handler()
            {
                _tapToStart.OnTapped -= Handler;
                _tapToStart.gameObject.SetActive(false);
                tcs.TrySetResult();
            }
            _tapToStart.OnTapped += Handler;
            return tcs.Task;
        }

        private async UniTask RunNicknameStep()
        {
            var popup = UIManager.Instance?.OpenPopup<NicknamePopup>("NicknamePopup");
            if (popup == null)
            {
                Debug.LogError("[OnboardingFlow] NicknamePopup 오픈 실패");
                return;
            }

            var tcs = new UniTaskCompletionSource<string>();
            void OnConfirmed(string nickname) => tcs.TrySetResult(nickname);
            popup.Confirmed += OnConfirmed;

            _pendingNickname = await tcs.Task;
            popup.Confirmed -= OnConfirmed;
            Debug.Log($"[OnboardingFlow] 닉네임 확정: {_pendingNickname}");
        }

        private async UniTask RunJobSelectStep()
        {
            var popup = UIManager.Instance?.OpenPopup<JobSelectPopup>("JobSelectPopup");
            if (popup == null)
            {
                Debug.LogError("[OnboardingFlow] JobSelectPopup 오픈 실패");
                return;
            }

            var tcs = new UniTaskCompletionSource<JobType>();
            void OnConfirmed(JobType job) => tcs.TrySetResult(job);
            popup.Confirmed += OnConfirmed;

            _pendingJob = await tcs.Task;
            popup.Confirmed -= OnConfirmed;
            Debug.Log($"[OnboardingFlow] 직업 확정: {_pendingJob}");
        }

        private void CompleteOnboarding()
        {
            // Title 씬에는 SaveManager/JobSystem이 없을 수 있으므로 Main 씬 로드 후 적용한다.
            SettingsManager.Instance.HasCompletedOnboarding = true;

            SceneManager.sceneLoaded += OnMainSceneLoaded;
            Debug.Log($"[OnboardingFlow] 온보딩 완료 예약 → Main 씬 로드 (nickname={_pendingNickname}, job={_pendingJob})");
        }

        private void OnMainSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != _mainSceneName) return;
            SceneManager.sceneLoaded -= OnMainSceneLoaded;

            // 2026-04-23 OnboardingFlow Destroy된 상태에서 scene 콜백 진입 시 가드
            if (this == null) return;

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                SaveManager.Instance.CurrentData.player.nickname = _pendingNickname ?? string.Empty;
            }

            if (_pendingJob.HasValue && JobSystem.Instance != null)
            {
                JobSystem.Instance.ChangeJob(_pendingJob.Value);
            }

            SaveManager.Instance?.Save();

            EventBus.Publish(new OnboardingCompletedEvent
            {
                Nickname = _pendingNickname,
                JobId = _pendingJob.HasValue ? JobIdFromType(_pendingJob.Value) : string.Empty,
            });

            Debug.Log($"[OnboardingFlow] Main 로드 완료 — 적용: nickname={_pendingNickname}, jobId={(_pendingJob.HasValue ? JobIdFromType(_pendingJob.Value) : "")}, JobSystem.Current={JobSystem.Instance?.CurrentJob}");

            // 2026-04-23 LoadingScreen 숨김 (Main 로드 완료)
            LoadingScreen.Instance?.Hide();
        }

        private void LoadMain()
        {
            // 2026-04-23 로딩 스크린 표시 (Title → Main 전환 시 유저에게 로딩 중 안내)
            LoadingScreen.Instance?.Show();
            SceneManager.LoadScene(_mainSceneName);
        }

        private void OnDestroy()
        {
            // 2026-04-23 누락된 OnDestroy — 씬 콜백이 destroyed 객체 참조 유지 방지
            SceneManager.sceneLoaded -= OnMainSceneLoaded;
        }

        private static string JobIdFromType(JobType job)
        {
            switch (job)
            {
                case JobType.Warrior: return "warrior";
                case JobType.Archer: return "archer";
                case JobType.Mage: return "mage";
                default: return "warrior";
            }
        }
    }
}
