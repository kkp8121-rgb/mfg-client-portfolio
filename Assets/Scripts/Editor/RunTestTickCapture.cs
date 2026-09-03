using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MkLike.Combat;
using MkLike.Quest;

namespace MkLike.Editor
{
    /// <summary>
    /// /run-tick 전용 스크린샷 캡처 유틸. 틱 인덱스 자동 증가 + 메타데이터 JSONL 기록.
    /// Screenshots/tick_{NNN}.png 패턴으로 저장. /run 시작 시 ResetCounter 호출.
    /// </summary>
    public static class RunTestTickCapture
    {
        private const string SCREENSHOT_DIR = "Screenshots";
        private const string COUNTER_FILE = "Screenshots/tick_counter.txt";
        private const string INDEX_FILE = "Screenshots/index.jsonl";

        private static string ScreenshotRoot => Path.Combine(Application.dataPath, "..", SCREENSHOT_DIR);
        private static string CounterPath => Path.Combine(Application.dataPath, "..", COUNTER_FILE);
        private static string IndexPath => Path.Combine(Application.dataPath, "..", INDEX_FILE);

        /// <summary>/run 시작 시 호출 — 카운터/인덱스 초기화.</summary>
        public static void ResetCounter()
        {
            EnsureDir();
            File.WriteAllText(CounterPath, "0");
            if (File.Exists(IndexPath)) File.Delete(IndexPath);
            Debug.Log("[RunTestTickCapture] Counter reset.");
        }

        public static int GetCurrentTick()
        {
            if (!File.Exists(CounterPath)) return 0;
            int n;
            return int.TryParse(File.ReadAllText(CounterPath).Trim(), out n) ? n : 0;
        }

        /// <summary>다음 tick으로 증가 + 메인 스크린샷 캡처 + 메타데이터 기록.</summary>
        public static int CaptureTick()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[RunTestTickCapture] Play 모드에서만 캡처 가능.");
                return 0;
            }

            EnsureDir();
            int tickIdx = GetCurrentTick() + 1;
            File.WriteAllText(CounterPath, tickIdx.ToString());

            string filename = $"tick_{tickIdx:D3}.png";
            string fullPath = Path.Combine(ScreenshotRoot, filename);
            ScreenCapture.CaptureScreenshot(fullPath);

            AppendIndexEntry(tickIdx, filename);
            Debug.Log($"[RunTestTickCapture] tick_{tickIdx:D3} captured (timeScale={Time.timeScale:F1})");
            return tickIdx;
        }

        /// <summary>특정 tag로 보조 캡처 (ex: issue 발생 시 증빙). tick 카운터는 증가시키지 않음.</summary>
        public static string CaptureExtra(string tag)
        {
            if (!Application.isPlaying) return null;
            EnsureDir();
            int tickIdx = GetCurrentTick();
            string filename = $"tick_{tickIdx:D3}_{tag}.png";
            string fullPath = Path.Combine(ScreenshotRoot, filename);
            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.Log($"[RunTestTickCapture] extra: {filename}");
            return fullPath;
        }

        private static void EnsureDir()
        {
            if (!Directory.Exists(ScreenshotRoot))
                Directory.CreateDirectory(ScreenshotRoot);
        }

        private static void AppendIndexEntry(int tickIdx, string filename)
        {
            try
            {
                var quest = Object.FindFirstObjectByType<QuestManager>();
                var stage = Object.FindFirstObjectByType<StageManager>();
                var player = Object.FindFirstObjectByType<PlayerCharacter>();
                var stats = player != null ? player.GetComponent<CombatStats>() : null;
                var levelSys = Object.FindFirstObjectByType<LevelSystem>();

                int chapter = stage != null ? stage.CurrentChapter : 0;
                int stageIdx = stage != null ? stage.CurrentStageIndex : 0;
                string guideId = quest != null ? (quest.CurrentGuideQuestId ?? "-") : "-";
                int level = levelSys != null ? levelSys.CurrentLevel : 0;
                long cp = stats != null ? stats.PowerScore : 0L;

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"tick\":{tickIdx},");
                sb.Append($"\"file\":\"{filename}\",");
                sb.Append($"\"time\":\"{System.DateTime.Now:HH:mm:ss}\",");
                sb.Append($"\"chapter\":{chapter},");
                sb.Append($"\"stage\":{stageIdx},");
                sb.Append($"\"level\":{level},");
                sb.Append($"\"cp\":{cp},");
                sb.Append($"\"guide\":\"{guideId}\",");
                sb.Append($"\"timeScale\":{Time.timeScale:F2}");
                sb.Append("}\n");
                File.AppendAllText(IndexPath, sb.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RunTestTickCapture] 인덱스 기록 실패: {e.Message}");
            }
        }
    }
}
