using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core.Net;
using MkLike.Utils;

namespace MkLike.Core.Save
{
    /// <summary>
    /// 세이브/로드 시스템 매니저.
    /// ISaveProvider를 통해 저장소를 추상화하며, 자동 저장 기능을 제공한다.
    /// DefaultExecutionOrder(-10000)으로 다른 시스템보다 먼저 Awake하여 CurrentData를 준비한다.
    ///
    /// Provider 전환 (P1-10):
    ///   Awake 시 LocalSaveProvider로 초기화 (인증 전이므로).
    ///   LoginCompletedEvent 수신 → ApiClient.HasAuth==true면 ServerSaveProvider로 교체 + MigrateAsync(1회) 호출.
    ///   HasAuth==false (오프라인 fallback)면 LocalSaveProvider 유지.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        /// <summary>현재 메모리에 올라와 있는 세이브 데이터</summary>
        public SaveData CurrentData { get; private set; }

        private ISaveProvider _provider;

        /// <summary>자동 저장 주기 (초)</summary>
        private const float AutoSaveInterval = 60f;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 기본 프로바이더로 로컬 파일 저장소 사용
            _provider = new LocalSaveProvider();

            Initialize();

            // 로그인 완료 → Server Provider 전환 + Migrate 1회 시도 (P1-10)
            EventBus<LoginCompletedEvent>.Subscribe(OnLoginCompleted);

            // 자동 저장 시작 (UniTask — WebGL IL2CPP 호환)
            AutoSaveLoopAsync().Forget();
        }

        /// <summary>
        /// 로그인 완료 시 ServerSaveProvider로 전환하고 최초 1회 Migrate 시도.
        /// HasAuth가 false면(오프라인 fallback 로그인) LocalSaveProvider 유지.
        /// </summary>
        private void OnLoginCompleted(LoginCompletedEvent evt)
        {
            if (!ApiClient.HasAuth)
            {
                Debug.Log("[SaveManager] 로그인 완료 — 인증 토큰 없음(오프라인 fallback), LocalSaveProvider 유지");
                return;
            }

            // 이미 ServerSaveProvider면 재전환 불필요 (재로그인 케이스)
            if (_provider is not ServerSaveProvider)
            {
                SetProvider(new ServerSaveProvider());
            }

            TryMigrateAsync().Forget();
        }

        /// <summary>
        /// ServerSaveProvider가 활성화된 상태에서 Migrate를 1회 시도.
        /// CurrentData.migratedToServer가 이미 true면 내부에서 스킵됨.
        /// </summary>
        private async UniTaskVoid TryMigrateAsync()
        {
            if (_provider is not ServerSaveProvider serverProvider) return;
            if (CurrentData == null) return;

            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                var result = await serverProvider.MigrateAsync(CurrentData, ct);
                if (result == null) return;

                bool swapped = !ReferenceEquals(result, CurrentData);
                CurrentData = result;

                if (swapped)
                {
                    // 서버본이 내려와 교체된 경우 (already_exists 분기)
                    EventBus.PublishSticky(new LoadCompletedEvent());
                    Debug.Log("[SaveManager] 서버본으로 교체됨 — LoadCompletedEvent 재발행");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Migrate 실패: {ex.Message} — 로컬 fallback 유지");
            }
        }

        /// <summary>
        /// 초기화: 기존 세이브를 로드하거나 새 데이터를 생성한다.
        /// </summary>
        public void Initialize()
        {
            // 로컬 세이브 로드 → 없으면 새 데이터 생성
            if (_provider.HasSave())
            {
                CurrentData = _provider.Load();
                if (CurrentData != null)
                {
                    Debug.Log("[SaveManager] 로컬 세이브 로드 성공");
                    EventBus.PublishSticky(new LoadCompletedEvent());
                    return;
                }
            }
            CurrentData = new SaveData();
            Debug.Log("[SaveManager] 새 세이브 데이터 생성");
        }

        /// <summary>
        /// 현재 데이터를 저장하고 SaveCompletedEvent를 발행한다.
        /// </summary>
        public void Save()
        {
            if (CurrentData == null || _provider == null) return;
            // BeforeSaveEvent: 상태 수집 이벤트 → sticky (늦은 시스템도 최초 저장 전 1회 동기화 가능)
            EventBus.PublishSticky(new BeforeSaveEvent());
            _provider.Save(CurrentData);
            // SaveCompletedEvent: 일회성 알림 → 일반 Publish 유지
            EventBus.Publish(new SaveCompletedEvent());
        }

        /// <summary>
        /// 데이터를 로드하고 LoadCompletedEvent를 발행한다.
        /// </summary>
        public void Load()
        {
            SaveData loaded = _provider.Load();
            if (loaded != null)
            {
                CurrentData = loaded;
                EventBus.PublishSticky(new LoadCompletedEvent());
                Debug.Log("[SaveManager] 로드 완료");
            }
            else
            {
                Debug.LogWarning("[SaveManager] 로드할 데이터가 없음");
            }
        }

        /// <summary>
        /// 세이브 프로바이더를 교체한다.
        /// 서버 연동 등 다른 저장소로 전환할 때 사용한다.
        /// </summary>
        /// <param name="provider">새로운 세이브 프로바이더</param>
        public void SetProvider(ISaveProvider provider)
        {
            _provider = provider;
            Debug.Log($"[SaveManager] 프로바이더 변경: {provider.GetType().Name}");
        }

        /// <summary>
        /// 세이브 데이터를 삭제하고 새 데이터로 초기화한다.
        /// </summary>
        public void DeleteSave()
        {
            _provider.Delete();
            CurrentData = new SaveData();
            Debug.Log("[SaveManager] 세이브 데이터 삭제 및 초기화");
        }

        private async UniTaskVoid AutoSaveLoopAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(AutoSaveInterval), cancellationToken: token);
                Save();
                Debug.Log("[SaveManager] 자동 저장 실행");
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                Save();
                Debug.Log("[SaveManager] 앱 일시정지 — 자동 저장");
            }
        }

        private void OnApplicationQuit()
        {
            Save();
            Debug.Log("[SaveManager] 앱 종료 — 자동 저장");
        }

        private void OnDestroy()
        {
            EventBus<LoginCompletedEvent>.Unsubscribe(OnLoginCompleted);

            // 종료 시 싱글톤 참조 정리
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
