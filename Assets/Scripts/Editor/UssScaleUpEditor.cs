using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// USS 파일의 폰트 크기와 요소 크기를 모바일 터치 기준으로 스케일업한다.
    /// 세로 모드(Portrait) 모바일 앱 기준.
    /// </summary>
    public static class UssScaleUpEditor
    {
        private const float FONT_SCALE = 1.6f;
        private const float SIZE_SCALE = 1.4f;
        private const float MIN_BTN_HEIGHT = 48f;
        private const float MIN_FONT = 14f;

        [MenuItem("mkLike/USS Scale Up — Mobile Portrait")]
        public static void ScaleUpAll()
        {
            string ussDir = "Assets/UI Toolkit/Styles";
            string[] ussFiles = Directory.GetFiles(ussDir, "*.uss");

            int totalChanges = 0;

            foreach (string filePath in ussFiles)
            {
                string content = File.ReadAllText(filePath);
                string original = content;

                // font-size: Npx → N * FONT_SCALE px (최소 MIN_FONT)
                content = Regex.Replace(content, @"font-size:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    float scaled = Mathf.Max(val * FONT_SCALE, MIN_FONT);
                    return $"font-size: {Mathf.RoundToInt(scaled)}px";
                });

                // height: Npx → N * SIZE_SCALE px (button/element heights)
                content = Regex.Replace(content, @"(?<!min-)(?<!max-)height:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    if (val < 10f) return match.Value; // 1-2px border 등 스킵
                    float scaled = Mathf.Max(val * SIZE_SCALE, MIN_BTN_HEIGHT * 0.5f);
                    return $"height: {Mathf.RoundToInt(scaled)}px";
                });

                // min-height → scale
                content = Regex.Replace(content, @"min-height:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    float scaled = Mathf.Max(val * SIZE_SCALE, MIN_BTN_HEIGHT);
                    return $"min-height: {Mathf.RoundToInt(scaled)}px";
                });

                // padding: Npx → scale (값이 4px 이상인 경우만)
                content = Regex.Replace(content, @"padding(?:-(?:top|bottom|left|right))?:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    if (val < 4f) return match.Value;
                    float scaled = val * SIZE_SCALE;
                    return match.Value.Replace(match.Groups[1].Value + "px", $"{Mathf.RoundToInt(scaled)}px");
                });

                // margin: Npx → scale (값이 4px 이상인 경우만)
                content = Regex.Replace(content, @"margin(?:-(?:top|bottom|left|right))?:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    if (val < 4f) return match.Value;
                    float scaled = val * SIZE_SCALE;
                    return match.Value.Replace(match.Groups[1].Value + "px", $"{Mathf.RoundToInt(scaled)}px");
                });

                // width: Npx → scale (대형 요소만, 20px 이상)
                content = Regex.Replace(content, @"(?<!min-)(?<!max-)width:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    if (val < 20f) return match.Value; // 작은 값은 스킵 (border 등)
                    float scaled = val * SIZE_SCALE;
                    return $"width: {Mathf.RoundToInt(scaled)}px";
                });

                // border-radius: Npx → scale
                content = Regex.Replace(content, @"border-radius:\s*(\d+(?:\.\d+)?)px", match =>
                {
                    float val = float.Parse(match.Groups[1].Value);
                    if (val < 4f) return match.Value;
                    float scaled = val * SIZE_SCALE;
                    return $"border-radius: {Mathf.RoundToInt(scaled)}px";
                });

                if (content != original)
                {
                    File.WriteAllText(filePath, content);
                    totalChanges++;
                    Debug.Log($"[UssScaleUp] 스케일업 완료: {Path.GetFileName(filePath)}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UssScaleUp] === 전체 {totalChanges}개 USS 파일 스케일업 완료 ===");
        }
    }
}
