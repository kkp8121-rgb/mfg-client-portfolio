using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MkLike.Core.Net
{
    /// <summary>
    /// UniTask 기반 HTTP 클라이언트.
    /// Firebase JWT 토큰 자동 첨부, JSON 직렬화/역직렬화.
    /// </summary>
    public static class ApiClient
    {
        private static string _baseUrl = "http://localhost:5035/api/v1";
        private static string _authToken;
        private static string _devUid;

        /// <summary>서버 베이스 URL 설정</summary>
        public static void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

        /// <summary>Firebase ID Token 설정 (로그인 후 호출)</summary>
        public static void SetAuthToken(string token) => _authToken = token;

        /// <summary>개발용 인증 설정 (X-Dev-Uid 헤더 사용)</summary>
        public static void SetDevAuth(string uid) => _devUid = uid;

        /// <summary>인증 토큰 보유 여부 (dev 인증 포함)</summary>
        public static bool HasAuth => !string.IsNullOrEmpty(_authToken) || !string.IsNullOrEmpty(_devUid);

        // ── GET ──

        public static async UniTask<ApiResponse<T>> GetAsync<T>(string endpoint,
            System.Threading.CancellationToken ct = default)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

            using var request = UnityWebRequest.Get(url);
            AttachHeaders(request);

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                return ParseResponse<T>(request);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogWarning($"[ApiClient] GET {endpoint} 실패: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<T>.Fail("요청 취소됨");
            }
        }

        // ── POST ──

        public static async UniTask<ApiResponse<T>> PostAsync<T>(string endpoint, object body,
            System.Threading.CancellationToken ct = default)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            var json = JsonUtility.ToJson(body);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            AttachHeaders(request);

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                return ParseResponse<T>(request);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogWarning($"[ApiClient] POST {endpoint} 실패: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<T>.Fail("요청 취소됨");
            }
        }

        // ── POST (응답 불필요) ──

        public static async UniTask<ApiResponse<string>> PostAsync(string endpoint, object body,
            System.Threading.CancellationToken ct = default)
        {
            return await PostAsync<string>(endpoint, body, ct);
        }

        // ── PATCH ──

        public static async UniTask<ApiResponse<T>> PatchAsync<T>(string endpoint, object body,
            System.Threading.CancellationToken ct = default)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            var json = JsonUtility.ToJson(body);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, "PATCH");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            AttachHeaders(request);

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                return ParseResponse<T>(request);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogWarning($"[ApiClient] PATCH {endpoint} 실패: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
            catch (OperationCanceledException)
            {
                return ApiResponse<T>.Fail("요청 취소됨");
            }
        }

        // ── 내부 ──

        private static void AttachHeaders(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(_authToken))
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");

            if (!string.IsNullOrEmpty(_devUid))
                request.SetRequestHeader("X-Dev-Uid", _devUid);
        }

        private static ApiResponse<T> ParseResponse<T>(UnityWebRequest request)
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorBody = request.downloadHandler?.text ?? request.error;
                Debug.LogWarning($"[ApiClient] HTTP {request.responseCode}: {errorBody}");
                return ApiResponse<T>.Fail(errorBody);
            }

            var responseText = request.downloadHandler.text;

            try
            {
                var response = JsonUtility.FromJson<ApiResponse<T>>(responseText);
                return response ?? ApiResponse<T>.Fail("응답 파싱 실패");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApiClient] JSON 파싱 실패: {ex.Message}");
                return ApiResponse<T>.Fail(ex.Message);
            }
        }
    }

    /// <summary>
    /// 서버 API 공통 응답 래퍼 (서버 ApiResponse<T>와 동일 구조)
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;

        public static ApiResponse<T> Ok(T data) => new() { success = true, data = data };
        public static ApiResponse<T> Fail(string error) => new() { success = false, error = error };
    }
}
