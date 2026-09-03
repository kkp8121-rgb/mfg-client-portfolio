using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;

namespace MkLike.Editor
{
    /// <summary>
    /// Play 시작부터 Stop까지 모든 로그를 파일로 자동 기록한다.
    /// 저장 경로: Logs/PlayLog_{timestamp}.txt
    /// 별도 설정 없이 자동 동작 (InitializeOnLoad).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeLogRecorder
    {
        private static StringBuilder _logBuffer;
        private static string _sessionStartTime;
        private static bool _isRecording;

        static PlayModeLogRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    StartRecording();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    StopRecording();
                    break;
            }
        }

        private static void StartRecording()
        {
            _logBuffer = new StringBuilder();
            _sessionStartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _isRecording = true;

            _logBuffer.AppendLine($"=== mkLike Play Session Log ===");
            _logBuffer.AppendLine($"Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logBuffer.AppendLine($"Unity: {Application.unityVersion}");
            _logBuffer.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _logBuffer.AppendLine("================================");
            _logBuffer.AppendLine();

            Application.logMessageReceived += OnLogReceived;

            Debug.Log("[LogRecorder] 로그 기록 시작");
        }

        private static void StopRecording()
        {
            if (!_isRecording) return;

            Application.logMessageReceived -= OnLogReceived;
            _isRecording = false;

            _logBuffer.AppendLine();
            _logBuffer.AppendLine("================================");
            _logBuffer.AppendLine($"Stop: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            SaveToFile();
        }

        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            if (_logBuffer == null) return;

            string prefix = type switch
            {
                LogType.Error => "[ERROR]",
                LogType.Exception => "[EXCEPTION]",
                LogType.Warning => "[WARN]",
                LogType.Assert => "[ASSERT]",
                _ => "[LOG]"
            };

            string time = DateTime.Now.ToString("HH:mm:ss.fff");
            _logBuffer.AppendLine($"{time} {prefix} {message}");

            if (type == LogType.Exception || type == LogType.Error)
            {
                _logBuffer.AppendLine($"  StackTrace: {stackTrace}");
            }
        }

        private static void SaveToFile()
        {
            string logsDir = Path.Combine(Application.dataPath, "..", "Logs", "PlaySessions");
            Directory.CreateDirectory(logsDir);

            string filePath = Path.Combine(logsDir, $"PlayLog_{_sessionStartTime}.txt");
            File.WriteAllText(filePath, _logBuffer.ToString(), Encoding.UTF8);

            Debug.Log($"[LogRecorder] 로그 저장 완료: {filePath}");
        }
    }
}
