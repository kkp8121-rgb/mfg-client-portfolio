using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace MkLike.Editor
{
    public static class WebGLBuildEditor
    {
        private const string BUILD_PATH = "Build/WebGL";
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";

        [MenuItem("mkLike/Build/WebGL Build", false, 400)]
        public static void BuildWebGL()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string fullPath = Path.GetFullPath(BUILD_PATH);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            Directory.CreateDirectory(fullPath);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = BUILD_PATH,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler
            };

            Debug.Log("[WebGL] 빌드 시작...");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGL] 빌드 성공! {fullPath} ({report.summary.totalSize / 1024 / 1024}MB)");
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                Debug.LogError($"[WebGL] 빌드 실패: {report.summary.totalErrors}개 에러");
            }
        }

        [MenuItem("mkLike/Build/WebGL Build + Run", false, 401)]
        public static void BuildAndRun()
        {
            BuildWebGL();

            string indexPath = Path.GetFullPath(Path.Combine(BUILD_PATH, "index.html"));
            if (File.Exists(indexPath))
            {
                StartLocalServer();
            }
        }

        [MenuItem("mkLike/Build/Start Local Server (기존 빌드)", false, 402)]
        public static void StartLocalServer()
        {
            string fullPath = Path.GetFullPath(BUILD_PATH);
            string indexPath = Path.Combine(fullPath, "index.html");

            if (!File.Exists(indexPath))
            {
                Debug.LogError("[WebGL] 빌드 결과 없음. 먼저 WebGL Build 실행하세요.");
                return;
            }

            int port = 8080;
            string url = $"http://localhost:{port}";

            try
            {
                var existingProcesses = Process.GetProcessesByName("python");
                foreach (var p in existingProcesses)
                {
                    try
                    {
                        if (p.StartInfo.Arguments.Contains(port.ToString()))
                            p.Kill();
                    }
                    catch { }
                }
            }
            catch { }

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m http.server {port} --directory \"{fullPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                Process.Start(psi);
                Debug.Log($"[WebGL] 로컬 서버 시작: {url}");
            }
            catch
            {
                psi.FileName = "python3";
                try
                {
                    Process.Start(psi);
                    Debug.Log($"[WebGL] 로컬 서버 시작: {url}");
                }
                catch
                {
                    Debug.LogError("[WebGL] Python을 찾을 수 없습니다. python -m http.server를 수동으로 실행하세요.");
                    return;
                }
            }

            // 브라우저 자동 열기 비활성화 — 크롬 Claude 테스트 시 수동으로 접속
            // Application.OpenURL(url);
            Debug.Log($"[WebGL] 브라우저에서 {url} 로 접속하세요.");
        }
    }
}
