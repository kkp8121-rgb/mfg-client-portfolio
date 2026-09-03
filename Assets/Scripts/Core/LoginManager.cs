using System;
using Cysharp.Threading.Tasks;
using MkLike.Core.Net;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.Core
{
    /// <summary>
    /// 로그인/세션 관리 매니저.
    /// 현재는 Mock 로그인(X-Dev-Uid 헤더) 기반. Firebase Auth SDK 실제 연동은 후속 Phase.
    /// PlayerPrefs에 dev-uid 저장 → 재기동 시 자동 로그인.
    /// </summary>
    public class LoginManager : MonoBehaviour
    {
        public static LoginManager Instance { get; private set; }

        private const string KEY_DEV_UID = "mklike_dev_uid";
        private const string DEFAULT_BASE_URL = "http://localhost:5035/api/v1";

        [SerializeField] private string _baseUrl = DEFAULT_BASE_URL;
        [Tooltip("서버 미구동 시 오프라인 로그인으로 fallback할지 여부 (로컬 개발/데모용)")]
        [SerializeField] private bool _allowOfflineFallback = true;

        /// <summary>마지막 로그인 응답 (성공 시).</summary>
        public LoginResponse LastLogin { get; private set; }

        /// <summary>로그인 성공 여부.</summary>
        public bool IsLoggedIn { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!string.IsNullOrEmpty(_baseUrl))
                ApiClient.SetBaseUrl(_baseUrl);
        }

        /// <summary>
        /// Mock 로그인 시도. devUid가 비어있으면 PlayerPrefs에서 불러오고,
        /// 없으면 새로 생성한다. 성공 시 LoginCompletedEvent 발행.
        /// </summary>
        public async UniTask<bool> TryAutoLoginAsync()
        {
            var storedUid = PlayerPrefs.GetString(KEY_DEV_UID, string.Empty);
            if (string.IsNullOrEmpty(storedUid))
            {
                Debug.Log("[LoginManager] 저장된 dev-uid 없음 → 신규 로그인 필요");
                return false;
            }

            return await MockLoginAsync(storedUid);
        }

        /// <summary>
        /// Mock 로그인 (dev-uid 기반). 서버 /auth/login 호출 → LoginResponse 반환.
        /// </summary>
        public async UniTask<bool> MockLoginAsync(string devUid = null)
        {
            if (string.IsNullOrEmpty(devUid))
            {
                devUid = $"guest-{Guid.NewGuid():N}".Substring(0, 20);
                Debug.Log($"[LoginManager] 신규 devUid 생성: {devUid}");
            }

            ApiClient.SetDevAuth(devUid);

            var response = await ApiClient.PostAsync<LoginResponse>("auth/login", new { });

            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[LoginManager] 로그인 서버 호출 실패: {response.error}");
                if (!_allowOfflineFallback)
                {
                    IsLoggedIn = false;
                    return false;
                }
                return OfflineLogin(devUid);
            }

            LastLogin = response.data;
            IsLoggedIn = true;
            PlayerPrefs.SetString(KEY_DEV_UID, devUid);
            PlayerPrefs.Save();

            Debug.Log($"[LoginManager] 로그인 성공 — playerId={LastLogin.playerId}, nickname={LastLogin.nickname}, isNew={LastLogin.isNewPlayer}");

            EventBus.Publish(new LoginCompletedEvent
            {
                PlayerId = LastLogin.playerId,
                Nickname = LastLogin.nickname,
                IsNewPlayer = LastLogin.isNewPlayer,
            });

            return true;
        }

        /// <summary>
        /// 서버가 없을 때 로컬 전용 로그인. SaveManager의 로컬 세이브만으로 게임 진행.
        /// </summary>
        private bool OfflineLogin(string devUid)
        {
            bool hasStoredUid = PlayerPrefs.HasKey(KEY_DEV_UID);
            LastLogin = new LoginResponse
            {
                playerId = devUid,
                nickname = string.Empty,
                isNewPlayer = !hasStoredUid,
                serverTime = DateTime.UtcNow.ToString("O"),
            };
            IsLoggedIn = true;
            PlayerPrefs.SetString(KEY_DEV_UID, devUid);
            PlayerPrefs.Save();

            Debug.Log($"[LoginManager] 오프라인 로그인 — devUid={devUid}, isNew={LastLogin.isNewPlayer}");

            EventBus.Publish(new LoginCompletedEvent
            {
                PlayerId = LastLogin.playerId,
                Nickname = LastLogin.nickname,
                IsNewPlayer = LastLogin.isNewPlayer,
            });

            return true;
        }

        /// <summary>
        /// 로그아웃. 저장된 devUid도 제거.
        /// </summary>
        public void Logout()
        {
            PlayerPrefs.DeleteKey(KEY_DEV_UID);
            PlayerPrefs.Save();
            ApiClient.SetDevAuth(null);
            ApiClient.SetAuthToken(null);
            IsLoggedIn = false;
            LastLogin = null;
            Debug.Log("[LoginManager] 로그아웃 완료");
        }

        /// <summary>
        /// 온보딩 완료 후 서버에 닉네임 + 직업 저장.
        /// 서버(2026-04-20 S23-08)에서 0/null 값은 기존값 유지하도록 스킵 처리하므로 그대로 전송 가능.
        /// 응답의 jobId/nickname/level/combatPower를 받아 로컬 SaveData에 반영한다.
        /// </summary>
        public async UniTask<bool> SubmitOnboardingAsync(string nickname, string jobId)
        {
            if (!IsLoggedIn)
            {
                Debug.LogWarning("[LoginManager] 서버 미로그인 상태 — onboarding 서버 동기화 스킵");
                return false;
            }

            var request = new PlayerProfileUpdateRequest
            {
                nickname = nickname,
                jobId = jobId,
                // level/combatPower 기본값(0) 전송 — 서버가 0 값은 스킵(S23-08).
            };

            var response = await ApiClient.PatchAsync<PlayerProfileResponse>("auth/profile", request);
            if (!response.success)
            {
                Debug.LogWarning($"[LoginManager] 프로필 동기화 실패(무시 가능): {response.error}");
                return false;
            }

            // 서버 응답값을 로컬 SaveData에 반영 (서버가 권위 — jobId/level/CP 서버 확정값)
            var data = Save.SaveManager.Instance?.CurrentData;
            if (data != null && response.data != null)
            {
                if (!string.IsNullOrEmpty(response.data.nickname))
                    data.player.nickname = response.data.nickname;
                if (!string.IsNullOrEmpty(response.data.jobId))
                    data.player.jobId = response.data.jobId;
                if (response.data.level > 0)
                    data.player.level = response.data.level;
            }

            Debug.Log($"[LoginManager] 프로필 동기화 — nickname={response.data.nickname}, jobId={response.data.jobId}, lv={response.data.level}");
            return true;
        }
    }
}
