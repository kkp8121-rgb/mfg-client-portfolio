using UnityEngine;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// 게임 전체 상태를 나타내는 열거형.
    /// </summary>
    public enum GameState
    {
        /// <summary>초기 로딩</summary>
        Loading,
        /// <summary>메인 전투 화면</summary>
        Main,
        /// <summary>일시정지</summary>
        Pause,
        /// <summary>UI 메뉴 (장비, 동료 등 팝업 열림)</summary>
        Menu
    }

    /// <summary>
    /// 게임 전체 수명주기를 관리하는 싱글톤 매니저.
    /// DontDestroyOnLoad로 씬 전환 시에도 유지된다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>싱글톤 인스턴스</summary>
        public static GameManager Instance { get; private set; }

        /// <summary>현재 게임 상태</summary>
        public GameState CurrentState { get; private set; }

        /// <summary>
        /// 게임 상태를 변경한다.
        /// 이전 상태와 동일하면 무시하고, 변경 시 GameStateChangedEvent를 발행한다.
        /// </summary>
        /// <param name="newState">전환할 새로운 상태</param>
        public void ChangeState(GameState newState)
        {
            // 이전 상태와 같으면 무시
            if (CurrentState == newState)
                return;

            GameState previousState = CurrentState;
            CurrentState = newState;

            // EventBus를 통해 상태 변경 이벤트 발행
            EventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = previousState.ToString(),
                CurrentState = newState.ToString()
            });
        }

        private void Awake()
        {
            // 싱글톤 패턴: 이미 인스턴스가 존재하면 중복 오브젝트를 파괴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 세로 고정 (Android 일부 기기에서 뒤집힘 방지)
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            // 비에디터 빌드에서 Debug.Log 비활성화 (WebGL 성능 최적화)
            // Warning/Error는 유지
#if !UNITY_EDITOR
            Debug.unityLogger.filterLogType = LogType.Warning;
#endif
        }

        private void Start()
        {
            // 초기 상태를 Loading으로 설정
            ChangeState(GameState.Loading);

            // TODO: 추후 다른 매니저 초기화 순서 관리
        }
    }
}
