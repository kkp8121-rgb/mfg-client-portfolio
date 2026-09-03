using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Dungeon;
using MkLike.Quest;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.UI
{
    /// <summary>
    /// 접속 시 순차 플로우 관리.
    /// 출석 체크 → 오프라인 보상(자동) → 던전 열쇠 충전 → 일일 체크리스트 리셋.
    /// Start()에서 자동 실행되며, 각 단계 사이에 딜레이를 두어 팝업이 겹치지 않게 한다.
    /// </summary>
    public class LoginFlowManager : MonoBehaviour
    {
        public static LoginFlowManager Instance { get; private set; }

        [Header("딜레이 설정 (초)")]
        [SerializeField] private float _initialDelay = 2f;
        [SerializeField] private float _betweenPopupDelay = 3f;

        private bool _hasRunThisSession;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RunLoginFlow().Forget();
        }

        /// <summary>
        /// 접속 시 순차 플로우를 실행한다.
        /// 출석 체크 → 오프라인 보상(자동) → 던전 충전 → 일일 리셋.
        /// </summary>
        private async UniTaskVoid RunLoginFlow()
        {
            if (_hasRunThisSession) return;
            _hasRunThisSession = true;

            var token = this.GetCancellationTokenOnDestroy();

            // 시스템 초기화 대기
            await UniTask.Delay(
                (int)(_initialDelay * 1000f),
                ignoreTimeScale: true,
                cancellationToken: token);

            Debug.Log("[LoginFlowManager] 접속 플로우 시작");

            // 1. 출석 체크 (AttendanceEffect가 이벤트를 구독하여 자동 팝업)
            bool attendanceChecked = false;
            if (AttendanceSystem.Instance != null)
            {
                attendanceChecked = AttendanceSystem.Instance.CheckAttendance();
                if (attendanceChecked)
                {
                    Debug.Log("[LoginFlowManager] 출석 체크 완료 — 팝업 대기");
                    await UniTask.Delay(
                        (int)(_betweenPopupDelay * 1000f),
                        ignoreTimeScale: true,
                        cancellationToken: token);
                }
            }

            // 2. 오프라인 보상 — OfflineRewardSystem.Start()에서 자동 처리됨
            // OfflineRewardPopup이 이벤트를 구독하여 자동 팝업
            // 출석 팝업과 겹치지 않도록 딜레이 이미 적용됨

            // 3. 던전 열쇠 일일 충전
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.RechargeKeys();
                Debug.Log("[LoginFlowManager] 던전 열쇠 충전 완료");
            }

            // 4. 일일 체크리스트 리셋 (이미 Awake에서 호출되지만 안전하게 재호출)
            if (DailyChecklistManager.Instance != null)
            {
                DailyChecklistManager.Instance.ResetIfNewDay();
                Debug.Log("[LoginFlowManager] 일일 체크리스트 리셋 확인");
            }

            Debug.Log("[LoginFlowManager] 접속 플로우 완료");
        }

        /// <summary>
        /// 앱 복귀 시 일일 리셋을 재확인한다.
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) return;

            // 포그라운드 복귀 시 날짜 변경 확인
            if (DailyChecklistManager.Instance != null)
                DailyChecklistManager.Instance.ResetIfNewDay();

            if (DungeonManager.Instance != null)
                DungeonManager.Instance.RechargeKeys();

            // 출석은 세션당 1회만
            if (AttendanceSystem.Instance != null && !AttendanceSystem.Instance.HasCheckedToday)
                AttendanceSystem.Instance.CheckAttendance();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
