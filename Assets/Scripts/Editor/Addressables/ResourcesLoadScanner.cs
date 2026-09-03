using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor.Addressables
{
    /// <summary>
    /// 모든 .cs 파일에서 Resources.Load / Resources.LoadAll 호출 위치를 스캔하여
    /// Addressables 마이그레이션 리포트를 생성한다.
    /// 산출물: client/Docs/Reports/addressables-migration.md
    /// </summary>
    public static class ResourcesLoadScanner
    {
        private static readonly Regex _pattern = new Regex(
            @"Resources\.(Load|LoadAll|LoadAsync)\s*(?:<[^>]+>)?\s*\(""([^""]*)""",
            RegexOptions.Compiled);

        public static void Run()
        {
            var scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            if (!Directory.Exists(scriptsRoot))
            {
                Debug.LogError($"[ResourcesLoadScanner] 스크립트 폴더 없음: {scriptsRoot}");
                return;
            }

            var files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            var hits = new List<Hit>();

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = _pattern.Match(lines[i]);
                    if (m.Success)
                    {
                        hits.Add(new Hit
                        {
                            File = file.Replace('\\', '/'),
                            Line = i + 1,
                            Method = m.Groups[1].Value,
                            Path = m.Groups[2].Value,
                            Snippet = lines[i].Trim(),
                        });
                    }
                }
            }

            WriteReport(hits);
            Debug.Log($"[ResourcesLoadScanner] {hits.Count}건 발견 → Docs/Reports/addressables-migration.md");
        }

        private static void WriteReport(List<Hit> hits)
        {
            var reportDir = Path.Combine(Application.dataPath, "..", "Docs", "Reports");
            if (!Directory.Exists(reportDir))
                Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, "addressables-migration.md");

            var sb = new StringBuilder();
            sb.AppendLine("# Addressables Migration Report");
            sb.AppendLine();
            sb.AppendLine($"생성 시각: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"총 호출 수: {hits.Count}");
            sb.AppendLine();
            sb.AppendLine("## 파일별 Resources.Load 호출");
            sb.AppendLine();

            var grouped = new SortedDictionary<string, List<Hit>>();
            foreach (var h in hits)
            {
                if (!grouped.ContainsKey(h.File))
                    grouped[h.File] = new List<Hit>();
                grouped[h.File].Add(h);
            }

            int repoRootLen = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..")).Length;
            foreach (var kv in grouped)
            {
                var display = kv.Key.Length > repoRootLen
                    ? kv.Key.Substring(repoRootLen).TrimStart('/', '\\')
                    : kv.Key;
                sb.AppendLine($"### {display}");
                sb.AppendLine();
                sb.AppendLine("| Line | Method | Path | Snippet |");
                sb.AppendLine("|------|--------|------|---------|");
                foreach (var h in kv.Value)
                {
                    var snip = h.Snippet.Replace("|", "\\|");
                    if (snip.Length > 120) snip = snip.Substring(0, 117) + "...";
                    sb.AppendLine($"| {h.Line} | `{h.Method}` | `{h.Path}` | `{snip}` |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 추천 마이그레이션 패턴");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine("// Before");
            sb.AppendLine("var clip = Resources.Load<AudioClip>(\"Audio/BGM/Main\");");
            sb.AppendLine();
            sb.AppendLine("// After (동기 유지 — 호환 레이어)");
            sb.AppendLine("var clip = ResourcesCompatLoader.Load<AudioClip>(\"Audio/BGM/Main\");");
            sb.AppendLine();
            sb.AppendLine("// After (권장 — 비동기)");
            sb.AppendLine("var handle = Addressables.LoadAssetAsync<AudioClip>(\"Assets/Resources/Audio/BGM/Main.mp3\");");
            sb.AppendLine("var clip = await handle.ToUniTask();");
            sb.AppendLine("```");

            File.WriteAllText(reportPath, sb.ToString());
            AssetDatabase.Refresh();
        }

        private struct Hit
        {
            public string File;
            public int Line;
            public string Method;
            public string Path;
            public string Snippet;
        }
    }
}
