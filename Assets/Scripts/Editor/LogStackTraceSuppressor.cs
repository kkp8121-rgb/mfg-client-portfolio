#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// Editor 진입 시 Debug.Log 의 스택 트레이스를 억제한다.
    ///
    /// 배경: /run fresh 12k tick 세션 중 MCP-FOR-UNITY 패키지의 `Client handler exited`
    /// Info 로그가 수만 번 반복 + 매번 풀 스택트레이스가 Editor.log/콘솔 메모리에 누적 →
    /// 34GB 로그 파일 + OOM 크래시 (2026-04-15).
    ///
    /// 조치: LogType.Log 의 stack trace를 None 으로 (Warning/Error는 유지).
    /// 경고/에러는 여전히 stack trace 포함 — 디버깅 정보 손실 최소화.
    /// </summary>
    [InitializeOnLoad]
    internal static class LogStackTraceSuppressor
    {
        static LogStackTraceSuppressor()
        {
            // 일반 Log 의 스택트레이스만 억제. Warning/Error/Exception/Assert 는 유지.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            // Play 모드 진입/종료마다 다시 적용 (Unity가 일부 상황에서 초기화)
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }
    }
}
#endif
