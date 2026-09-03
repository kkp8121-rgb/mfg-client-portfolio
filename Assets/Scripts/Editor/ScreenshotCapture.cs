using UnityEngine;
using UnityEditor;
using System.IO;

namespace MkLike.Editor
{
    /// <summary>
    /// Play 모드에서 스크린샷을 캡처하는 에디터 도구.
    /// 메뉴 또는 단축키(F12)로 캡처하며, Screenshots/ 폴더에 저장한다.
    /// Claude Code에서 Read 도구로 바로 확인 가능.
    /// </summary>
    public static class ScreenshotCapture
    {
        private const string SCREENSHOT_DIR = "Screenshots";

        [MenuItem("mkLike/Screenshot/Capture (F12) #F12", false, 300)]
        public static void CaptureNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Screenshot] Play 모드에서만 캡처 가능합니다.");
                return;
            }

            string dir = Path.Combine(Application.dataPath, "..", SCREENSHOT_DIR);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"screenshot_{timestamp}.png";
            string fullPath = Path.Combine(dir, filename);

            ScreenCapture.CaptureScreenshot(fullPath);

            Debug.Log($"<color=cyan>[Screenshot] 캡처 완료: {SCREENSHOT_DIR}/{filename}</color>");

            // 1초 후 AssetDatabase 리프레시 (파일 저장 대기)
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (File.Exists(fullPath))
                        Debug.Log($"<color=lime>[Screenshot] 파일 확인됨: {fullPath} ({new FileInfo(fullPath).Length / 1024}KB)</color>");
                };
            };
        }

        /// <summary>
        /// 5장 연속 캡처 (2초 간격). HUD, 탭 전환, 팝업 등을 자동으로 캡처.
        /// </summary>
        [MenuItem("mkLike/Screenshot/Auto Capture (5장, 2초 간격)", false, 301)]
        public static void AutoCaptureSeries()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Screenshot] Play 모드에서만 캡처 가능합니다.");
                return;
            }

            string dir = Path.Combine(Application.dataPath, "..", SCREENSHOT_DIR);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int count = 0;
            const int total = 5;
            const float interval = 2f;

            string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            void CaptureNext()
            {
                if (count >= total || !Application.isPlaying) return;

                count++;
                string filename = $"auto_{sessionId}_{count}of{total}.png";
                string fullPath = Path.Combine(dir, filename);

                ScreenCapture.CaptureScreenshot(fullPath);
                Debug.Log($"<color=cyan>[Screenshot] 자동 캡처 {count}/{total}: {filename}</color>");

                if (count < total)
                {
                    float nextTime = (float)EditorApplication.timeSinceStartup + interval;
                    EditorApplication.update += CheckTime;

                    void CheckTime()
                    {
                        if (EditorApplication.timeSinceStartup >= nextTime)
                        {
                            EditorApplication.update -= CheckTime;
                            CaptureNext();
                        }
                    }
                }
                else
                {
                    Debug.Log($"<color=lime>[Screenshot] 자동 캡처 완료! {SCREENSHOT_DIR}/ 폴더 확인</color>");
                }
            }

            CaptureNext();
        }

        /// <summary>
        /// Screenshots 폴더의 최신 파일 경로를 반환한다.
        /// Claude Code에서 이 경로를 Read 도구로 읽으면 스크린샷을 볼 수 있다.
        /// </summary>
        [MenuItem("mkLike/Screenshot/Show Latest Path", false, 302)]
        public static void ShowLatestPath()
        {
            string dir = Path.Combine(Application.dataPath, "..", SCREENSHOT_DIR);
            if (!Directory.Exists(dir))
            {
                Debug.Log("[Screenshot] Screenshots 폴더가 없습니다.");
                return;
            }

            var files = Directory.GetFiles(dir, "*.png");
            if (files.Length == 0)
            {
                Debug.Log("[Screenshot] 스크린샷이 없습니다.");
                return;
            }

            System.Array.Sort(files);
            string latest = files[files.Length - 1];
            string absPath = Path.GetFullPath(latest);

            Debug.Log($"<color=yellow>[Screenshot] 최신 스크린샷: {absPath}</color>");
            Debug.Log($"[ScreenshotCapture] Claude Code에서 확인: Read tool → {absPath}");
        }
    }
}
