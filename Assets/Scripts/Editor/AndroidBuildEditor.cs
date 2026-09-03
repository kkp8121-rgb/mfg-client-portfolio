using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace MkLike.Editor
{
    public static class AndroidBuildEditor
    {
        private const string BUILD_PATH = "Build/Android";
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";

        [MenuItem("mkLike/Build/Android APK (Dev)", false, 410)]
        public static void BuildAPK_Dev()
        {
            BuildAPK(isDevelopment: true);
        }

        [MenuItem("mkLike/Build/Android APK (Release)", false, 411)]
        public static void BuildAPK_Release()
        {
            BuildAPK(isDevelopment: false);
        }

        private static void BuildAPK(bool isDevelopment)
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string fullPath = Path.GetFullPath(BUILD_PATH);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            string suffix = isDevelopment ? "dev" : "release";
            string apkPath = Path.Combine(BUILD_PATH, $"mkLike-{suffix}.apk");

            // APK 빌드 (AAB 아님)
            EditorUserBuildSettings.buildAppBundle = false;

            var buildOptions = BuildOptions.None;
            if (isDevelopment)
                buildOptions |= BuildOptions.Development | BuildOptions.ConnectWithProfiler;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = buildOptions
            };

            Debug.Log($"[Android] APK 빌드 시작 ({suffix})...");
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long sizeMB = (long)(report.summary.totalSize / 1024 / 1024);
                Debug.Log($"[Android] 빌드 성공! {Path.GetFullPath(apkPath)} ({sizeMB}MB)");
                EditorUtility.RevealInFinder(Path.GetFullPath(apkPath));
            }
            else
            {
                Debug.LogError($"[Android] 빌드 실패: {report.summary.totalErrors}개 에러");
            }
        }

        /// <summary>CLI 배치 모드용 — Unity -executeMethod MkLike.Editor.AndroidBuildEditor.BuildFromCLI</summary>
        public static void BuildFromCLI()
        {
            BuildAPK(isDevelopment: false);
        }
    }
}
