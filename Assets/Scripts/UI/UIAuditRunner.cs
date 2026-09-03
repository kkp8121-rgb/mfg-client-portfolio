using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 전체 UI 자동 캡처 시스템.
    /// MkLike > Screenshot > Run UI Audit 메뉴로 실행.
    /// HUD → [UITK] Char_* 서브탭 7개 → [UITK] Dungeon → [UITK] Shop → 요약 리포트 생성.
    /// </summary>
    public class UIAuditRunner : MonoBehaviour
    {
        public static UIAuditRunner Instance { get; private set; }

        private string _outputDir;
        private readonly List<string> _files = new();
        private readonly List<string> _warnings = new();
        private StreamWriter _log;
        private int _idx;

        /// <summary>[UITK] 패널 이름 → 캡처 라벨 매핑</summary>
        private static readonly (string goName, string label)[] UITK_PANELS =
        {
            ("[UITK] Char_Stat",    "02_Char_Stat"),
            ("[UITK] Char_Equip",   "03_Char_Equip"),
            ("[UITK] Char_Skill",   "04_Char_Skill"),
            ("[UITK] Char_Job",     "05_Char_Job"),
            // Char_Relic: 2026-04-20 유물 시스템 완전 제거
            ("[UITK] Char_Climber", "07_Char_Climber"),
            ("[UITK] Char_Ability", "08_Char_Ability"),
            ("[UITK] Dungeon",      "09_Dungeon"),
            ("[UITK] Shop",         "10_Shop"),
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RunAsync().Forget();
        }

        private void Log(string msg)
        {
            Debug.Log($"[UIAuditRunner] {msg}");
            try { _log?.WriteLine(msg); } catch { }
        }

        private async UniTaskVoid RunAsync()
        {
            var token = destroyCancellationToken;

            try
            {
                // 출력 폴더 생성
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                _outputDir = Path.Combine(root, "Screenshots", $"audit_{ts}");
                Directory.CreateDirectory(_outputDir);

                _log = new StreamWriter(Path.Combine(_outputDir, "_log.txt"), false, Encoding.UTF8) { AutoFlush = true };
                Application.logMessageReceived += OnUnityLog;

                Log("[UIAudit] === 감사 시작 ===");
                Log($"[UIAudit] 출력: {_outputDir}");
                Log($"[UIAudit] 해상도: {Screen.width}x{Screen.height}");

                // 초기화 대기 (GameManager 등)
                await UniTask.Delay(3000, cancellationToken: token);
                _idx = 1;

                // [UITK] 오브젝트 인덱스 구축
                var uitkMap = BuildUITKMap();
                Log($"[UIAudit] [UITK] 오브젝트 {uitkMap.Count}개 발견");

                // ── 1. HUD (기본 상태 — 패널만 비활성, HUD/TabBar 유지) ──
                DeactivateAllUITK(uitkMap, preserveAlwaysActive: true);
                await UniTask.Delay(500, cancellationToken: token);
                Log("[UIAudit] >>> HUD");
                await Capture("01_HUD", token);

                // ── 2~10. UITK 패널 순회 ──
                foreach (var (goName, label) in UITK_PANELS)
                {
                    Log($"[UIAudit] >>> {label} ({goName})");

                    if (!uitkMap.TryGetValue(goName, out var go))
                    {
                        string warn = $"{goName} 없음 — 스킵";
                        Log($"[UIAudit] !!! {warn}");
                        _warnings.Add(warn);
                        continue;
                    }

                    try
                    {
                        // 다른 패널 비활성 → 이 패널만 활성 (HUD/TabBar 유지)
                        DeactivateAllUITK(uitkMap, preserveAlwaysActive: true);
                        go.SetActive(true);
                        await UniTask.Delay(600, cancellationToken: token);

                        await Capture(label, token);

                        go.SetActive(false);
                    }
                    catch (Exception ex)
                    {
                        Log($"[UIAudit] !!! {label} 예외: {ex.Message}");
                        _warnings.Add($"{label}: {ex.Message}");
                    }

                    await UniTask.Delay(300, cancellationToken: token);
                }

                // 패널 비활성 복원 (HUD/TabBar 유지)
                DeactivateAllUITK(uitkMap, preserveAlwaysActive: true);

                // 요약
                WriteSummary();
                Log($"[UIAudit] === 감사 완료! {_files.Count}개 파일, {_warnings.Count}개 경고 ===");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"[UIAudit] 치명적 오류: {ex}");
            }
            finally
            {
                Application.logMessageReceived -= OnUnityLog;
                _log?.Flush();
                _log?.Dispose();
                _log = null;
            }

#if UNITY_EDITOR
            await UniTask.Delay(500, cancellationToken: token);
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ═══════════════════════════════════════════════
        //  UITK 오브젝트 관리
        // ═══════════════════════════════════════════════

        /// <summary>
        /// UIDocument 컴포넌트가 있고 이름이 "[UITK]"로 시작하는 GameObject를 맵으로 수집.
        /// </summary>
        private Dictionary<string, GameObject> BuildUITKMap()
        {
            var map = new Dictionary<string, GameObject>();
            var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var doc in docs)
            {
                if (doc == null) continue;
                string name = doc.gameObject.name;
                if (!name.StartsWith("[UITK]")) continue;

                if (!map.ContainsKey(name))
                {
                    map[name] = doc.gameObject;
                    Log($"[UIAudit]   발견: {name} (active={doc.gameObject.activeSelf})");
                }
            }

            return map;
        }

        /// <summary>항상 활성 상태를 유지해야 하는 [UITK] 오브젝트 이름</summary>
        private static readonly HashSet<string> ALWAYS_ACTIVE_UITK = new()
        {
            "[UITK] HUD",
            "[UITK] TabBar",
            "[UITK] Toast"
        };

        /// <summary>맵 내 모든 [UITK] 패널 비활성화</summary>
        /// <param name="preserveAlwaysActive">true이면 HUD/TabBar/Toast는 활성 유지</param>
        private static void DeactivateAllUITK(Dictionary<string, GameObject> map, bool preserveAlwaysActive = false)
        {
            foreach (var kv in map)
            {
                if (kv.Value != null && kv.Value.activeSelf)
                {
                    if (preserveAlwaysActive && ALWAYS_ACTIVE_UITK.Contains(kv.Key))
                        continue;
                    kv.Value.SetActive(false);
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  캡처
        // ═══════════════════════════════════════════════

        private async UniTask Capture(string name, CancellationToken token)
        {
            await UniTask.DelayFrame(2, cancellationToken: token);

            string prefix = $"{_idx:D2}_{name}";
            _idx++;

            // 스크린샷
            string relDir = Path.Combine("Screenshots", Path.GetFileName(_outputDir));
            string relPath = Path.Combine(relDir, $"{prefix}.png").Replace('\\', '/');
            ScreenCapture.CaptureScreenshot(relPath);
            _files.Add($"{prefix}.png");

            await UniTask.DelayFrame(5, cancellationToken: token);

            Log($"[UIAudit] 캡처 완료: {prefix}");
        }

        // ═══════════════════════════════════════════════
        //  요약 리포트
        // ═══════════════════════════════════════════════

        private void WriteSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== UI Audit Summary === {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Resolution: {Screen.width}x{Screen.height}");
            sb.AppendLine($"Captured: {_idx - 1} screens");
            sb.AppendLine();

            sb.AppendLine("--- Files ---");
            foreach (var f in _files)
                sb.AppendLine($"  {f}");

            sb.AppendLine();
            sb.AppendLine($"--- Warnings ({_warnings.Count}) ---");
            foreach (var w in new HashSet<string>(_warnings))
                sb.AppendLine($"  [!] {w}");

            File.WriteAllText(Path.Combine(_outputDir, "_summary.txt"), sb.ToString(), Encoding.UTF8);
        }

        // ═══════════════════════════════════════════════
        //  로그 핸들러
        // ═══════════════════════════════════════════════

        private void OnUnityLog(string msg, string stack, LogType type)
        {
            try
            {
                _log?.WriteLine($"[{type}] {msg}");
                if (type == LogType.Exception || type == LogType.Error)
                    _log?.WriteLine($"  {stack}");
            }
            catch { }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            _log?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
