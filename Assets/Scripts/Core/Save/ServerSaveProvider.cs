using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MkLike.Core.Net;
using UnityEngine;

namespace MkLike.Core.Save
{
    /// <summary>
    /// 서버 기반 세이브 프로바이더.
    /// ISaveProvider 동기 인터페이스를 유지하면서 백그라운드에서 서버와 동기화한다.
    /// 로컬 캐시를 항상 유지하여 오프라인 시에도 동작한다.
    ///
    /// 플로우 (P1-10):
    ///   최초 1회: MigrateAsync — 로컬 SaveData → POST /save/migrate → migratedToServer=true 마킹
    ///   이후: Save() — 로컬 저장 + POST /save/sync (30초 쓰로틀은 서버측)
    ///   로드: LoadFromServerAsync — GET /save/load → 로컬 덮어쓰기. 실패 시 로컬 fallback.
    /// </summary>
    public class ServerSaveProvider : ISaveProvider
    {
        private readonly LocalSaveProvider _localProvider = new();
        private int _serverVersion;
        private bool _isSyncing;
        private bool _pendingUpload; // 네트워크 실패로 업로드 지연된 상태

        /// <summary>내부 로컬 프로바이더 접근 (테스트/마이그레이션용)</summary>
        public LocalSaveProvider LocalProvider => _localProvider;

        /// <summary>
        /// 로컬 저장 + 서버 동기화 (비동기, fire-and-forget)
        /// </summary>
        public void Save(SaveData data)
        {
            // 항상 로컬에 먼저 저장 (오프라인 안전)
            _localProvider.Save(data);

            // 서버 동기화 (인증 있을 때만)
            if (ApiClient.HasAuth && !_isSyncing)
            {
                SyncToServerAsync(data).Forget();
            }
            else if (!ApiClient.HasAuth)
            {
                _pendingUpload = true;
            }
        }

        /// <summary>
        /// 로컬에서 로드. 서버 로드는 LoadFromServerAsync()를 별도 호출.
        /// </summary>
        public SaveData Load()
        {
            return _localProvider.Load();
        }

        public bool HasSave()
        {
            return _localProvider.HasSave();
        }

        public void Delete()
        {
            _localProvider.Delete();
        }

        /// <summary>
        /// 서버에서 세이브 데이터를 로드한다. 로그인 직후 1회 호출.
        /// 서버 데이터가 있으면 로컬을 덮어쓴다.
        /// </summary>
        public async UniTask<SaveData> LoadFromServerAsync(CancellationToken ct)
        {
            if (!ApiClient.HasAuth)
            {
                Debug.LogWarning("[ServerSaveProvider] 인증 토큰 없음 — 로컬 데이터 사용");
                return Load();
            }

            var response = await ApiClient.GetAsync<SaveLoadResponse>("save/load", ct);

            if (!response.success || response.data == null || string.IsNullOrEmpty(response.data.saveData) || response.data.saveData == "{}")
            {
                Debug.Log("[ServerSaveProvider] 서버에 세이브 없음 — 로컬 데이터 사용");
                return Load();
            }

            _serverVersion = response.data.version;

            SaveData serverData = null;
            try { serverData = JsonUtility.FromJson<SaveData>(response.data.saveData); }
            catch (Exception ex) { Debug.LogWarning($"[ServerSaveProvider] 서버 데이터 파싱 예외: {ex.Message}"); }

            if (serverData != null)
            {
                // 서버 데이터를 로컬에도 저장
                _localProvider.Save(serverData);
                Debug.Log($"[ServerSaveProvider] 서버 세이브 로드 성공 (v{_serverVersion})");
                return serverData;
            }

            Debug.LogWarning("[ServerSaveProvider] 서버 데이터 파싱 실패 — 로컬 데이터 사용");
            return Load();
        }

        /// <summary>
        /// 최초 로그인 시 로컬 SaveData를 서버에 1회 업로드한다.
        /// data.migratedToServer=true면 재호출하지 않음.
        /// 서버가 already_exists를 반환하면 플래그만 true로 마킹하고 서버본을 로드한다.
        /// </summary>
        /// <param name="data">업로드할 로컬 SaveData. 성공 시 migratedToServer=true로 마킹되어 로컬 저장됨.</param>
        /// <param name="ct">취소 토큰</param>
        /// <returns>마이그레이션이 완료된(또는 이미 완료된) SaveData. 실패 시 입력 data 그대로.</returns>
        public async UniTask<SaveData> MigrateAsync(SaveData data, CancellationToken ct)
        {
            if (data == null)
            {
                Debug.LogWarning("[ServerSaveProvider] MigrateAsync: data==null");
                return null;
            }

            if (data.migratedToServer)
            {
                Debug.Log("[ServerSaveProvider] 이미 마이그레이션 완료됨 — 스킵");
                return data;
            }

            if (!ApiClient.HasAuth)
            {
                Debug.LogWarning("[ServerSaveProvider] MigrateAsync: 인증 토큰 없음 — 스킵");
                return data;
            }

            try
            {
                var json = JsonUtility.ToJson(data);
                var request = new SaveMigrateRequest
                {
                    saveData = json,
                    clientTimestamp = DateTime.UtcNow.ToString("o")
                };

                var response = await ApiClient.PostAsync<SaveMigrateResponse>("save/migrate", request, ct);

                if (!response.success || response.data == null)
                {
                    Debug.LogWarning($"[ServerSaveProvider] Migrate 실패: {response.error} — 로컬 유지");
                    return data;
                }

                // migrated=true: 최초 업로드 성공
                // migrated=false, reason="already_exists": 서버에 이미 있음 → 플래그만 마킹하고 서버본 로드
                if (response.data.migrated)
                {
                    _serverVersion = response.data.version;
                    data.migratedToServer = true;
                    _localProvider.Save(data);
                    Debug.Log($"[ServerSaveProvider] Migrate 성공 (v{_serverVersion}) — 서버에 로컬 데이터 업로드 완료");
                    return data;
                }
                else
                {
                    Debug.Log($"[ServerSaveProvider] Migrate 스킵됨 (reason={response.data.reason}) — 서버본 로드 시도");
                    data.migratedToServer = true;
                    _localProvider.Save(data);

                    // 서버본이 이미 있으므로 로드하여 로컬 덮어쓰기
                    var serverData = await LoadFromServerAsync(ct);
                    if (serverData != null)
                    {
                        serverData.migratedToServer = true;
                        _localProvider.Save(serverData);
                        return serverData;
                    }
                    return data;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[ServerSaveProvider] Migrate 취소됨");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerSaveProvider] Migrate 예외: {ex.Message} — 로컬 유지");
                return data;
            }
        }

        private async UniTaskVoid SyncToServerAsync(SaveData data)
        {
            _isSyncing = true;

            try
            {
                var json = JsonUtility.ToJson(data);
                var request = new SaveSyncRequest
                {
                    saveData = json,
                    clientTimestamp = DateTime.UtcNow.ToString("o")
                };

                var response = await ApiClient.PostAsync<SaveSyncResponse>("save/sync", request);

                if (response.success && response.data != null)
                {
                    _serverVersion = response.data.versionUpdated;
                    _pendingUpload = false;
                    Debug.Log($"[ServerSaveProvider] 서버 동기화 완료 (v{_serverVersion})");
                }
                else
                {
                    // 30초 쓰로틀 등 서버 거부 → 로컬은 이미 저장됨. 다음 주기에 재시도.
                    _pendingUpload = true;
                    Debug.LogWarning($"[ServerSaveProvider] 서버 동기화 실패: {response.error} — 로컬만 유지, 다음 주기 재시도");
                }
            }
            catch (Exception ex)
            {
                _pendingUpload = true;
                Debug.LogWarning($"[ServerSaveProvider] 서버 동기화 예외: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
