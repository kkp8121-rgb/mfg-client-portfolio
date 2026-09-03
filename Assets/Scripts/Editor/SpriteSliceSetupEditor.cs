using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MkLike.Editor
{
    /// <summary>
    /// Stone GUI Kit 스프라이트에 9-slice Border를 자동 설정하는 에디터.
    /// TextureImporter.spriteBorder를 프로그래밍 방식으로 설정한다.
    /// </summary>
    public static class SpriteSliceSetupEditor
    {
        private const string STONE_UI_PATH = "Assets/Resources/UI/Stone";
        private const string STONE_ICONS_PATH = "Assets/Resources/Icons/Stone";

        /// <summary>
        /// 스프라이트 경로 → Border (Left, Bottom, Right, Top) 매핑.
        /// 각 스프라이트의 실제 픽셀 크기와 시각적 특성에 맞춰 수동 튜닝.
        /// </summary>
        private static readonly Dictionary<string, Vector4> BorderMap = new()
        {
            // ── Button (180x133) — 둥근 모서리 + 장식 테두리 ──
            { "Button/btn_normal", new Vector4(22, 22, 22, 22) },
            { "Button/btn_highlight", new Vector4(22, 22, 22, 22) },
            { "Button/btn_confirm", new Vector4(22, 22, 22, 22) },
            { "Button/btn_danger", new Vector4(22, 22, 22, 22) },
            { "Button/btn_special", new Vector4(22, 22, 22, 22) },

            // ── Frame ──
            // panel_bg (328x304) — 두꺼운 돌 테두리 + 모서리 장식
            { "Frame/panel_bg", new Vector4(40, 40, 40, 40) },
            // frame_brown/green/silver (133x145) — 중간 크기 프레임
            { "Frame/frame_brown", new Vector4(20, 20, 20, 20) },
            { "Frame/frame_green", new Vector4(20, 20, 20, 20) },
            { "Frame/frame_silver", new Vector4(20, 20, 20, 20) },
            // common_bg (1920x1080) — 전체 배경, 9-slice 불필요 (Simple)
            // label_brown (60x50) — 작은 라벨
            { "Frame/label_brown", new Vector4(10, 8, 10, 8) },
            // label_orange (133x62) — 중간 라벨
            { "Frame/label_orange", new Vector4(18, 10, 18, 10) },

            // ── Gage ──
            // gage_bg/brown/orange (6x24) — 아주 얇은 게이지 바
            { "Gage/gage_bg", new Vector4(2, 2, 2, 2) },
            { "Gage/gage_brown", new Vector4(2, 2, 2, 2) },
            { "Gage/gage_orange", new Vector4(2, 2, 2, 2) },
            // exp_fill (17x14) — 경험치 바 채우기
            { "Gage/exp_fill", new Vector4(3, 3, 3, 3) },
            // hp_bg (246x98) — HP 바 프레임
            { "Gage/hp_bg", new Vector4(30, 15, 30, 15) },
            // hp_fill (21x22) — HP 바 채우기
            { "Gage/hp_fill", new Vector4(4, 4, 4, 4) },

            // ── Popup ──
            // title_bar_00 (377x113) — 날개 장식 타이틀 바
            { "Popup/title_bar_00", new Vector4(85, 15, 85, 15) },
            // title_bar_01 (235x113) — 작은 타이틀 바
            { "Popup/title_bar_01", new Vector4(55, 15, 55, 15) },
            // title_bar_02 (87x61) — 미니 타이틀
            { "Popup/title_bar_02", new Vector4(15, 10, 15, 10) },

            // ── Skill ──
            // bar_bg (49x46) — 스킬 바 배경
            { "Skill/bar_bg", new Vector4(8, 8, 8, 8) },
            // bar_normal (14x22) — 스킬 바 채우기
            { "Skill/bar_normal", new Vector4(3, 3, 3, 3) },
            // frame_* (76x87) — 스킬 프레임 (팔각형, 균등 border)
            { "Skill/frame_blue", new Vector4(12, 14, 12, 14) },
            { "Skill/frame_green", new Vector4(12, 14, 12, 14) },
            { "Skill/frame_purple", new Vector4(12, 14, 12, 14) },
            { "Skill/frame_red", new Vector4(12, 14, 12, 14) },
            { "Skill/frame_silver", new Vector4(12, 14, 12, 14) },
            { "Skill/frame_yellow", new Vector4(12, 14, 12, 14) },

            // ── Tab (224x124) — 육각형 탭 (넓은 좌우 border) ──
            { "Tab/tab_normal", new Vector4(50, 15, 50, 15) },
            { "Tab/tab_selected", new Vector4(50, 15, 50, 15) },

            // ── Slider ──
            // 슬라이더 배경/채우기 (있으면)
        };

        /// <summary>
        /// Simple 타입으로 유지해야 하는 스프라이트 (9-slice 비적합).
        /// 아이콘, 원형, 전체 배경 등.
        /// </summary>
        private static readonly HashSet<string> SimpleSprites = new()
        {
            "Frame/common_bg",
            "Icon/btn_close",
            "Status/icon_coin",
            "Status/icon_gem",
            "Status/icon_soul_gem",
        };

        public static void SetupAllBorders()
        {
            int modified = 0;
            int skipped = 0;

            // Stone UI 스프라이트 처리
            modified += ProcessSpritesInPath(STONE_UI_PATH, ref skipped);

            // Stone 아이콘 — 모두 Simple (9-slice 불필요)
            SetAllSpritesSimple(STONE_ICONS_PATH, ref skipped);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=cyan>[SpriteSliceSetup] 9-slice Border 설정 완료: " +
                      $"{modified}개 수정, {skipped}개 스킵</color>");
        }

        private static int ProcessSpritesInPath(string basePath, ref int skipped)
        {
            int modified = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { basePath });

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                // basePath 기준 상대 경로 (확장자 제거)
                string relativePath = assetPath
                    .Replace(basePath + "/", "")
                    .Replace(".png", "")
                    .Replace(".jpg", "");

                // Simple 타입 강제
                if (SimpleSprites.Contains(relativePath))
                {
                    if (EnsureSpriteType(importer, assetPath, SpriteImportMode.Single, Vector4.zero))
                        modified++;
                    else
                        skipped++;
                    continue;
                }

                // Border 매핑 확인
                if (BorderMap.TryGetValue(relativePath, out var border))
                {
                    if (ApplyBorder(importer, assetPath, border))
                        modified++;
                    else
                        skipped++;
                }
                else
                {
                    // 매핑에 없는 스프라이트 — 경고만 출력
                    Debug.LogWarning($"[SpriteSliceSetup] Border 매핑 없음: {relativePath} ({assetPath})");
                    skipped++;
                }
            }

            return modified;
        }

        private static bool ApplyBorder(TextureImporter importer, string assetPath, Vector4 border)
        {
            bool changed = false;

            // Sprite 모드 확인
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            // 텍스처 타입 확인
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            // Border 설정
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                changed = true;
            }

            // Mesh Type: FullRect (Sliced 사용 시 필수)
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                return true;
            }

            return false;
        }

        private static bool EnsureSpriteType(TextureImporter importer, string assetPath,
            SpriteImportMode mode, Vector4 border)
        {
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != mode)
            {
                importer.spriteImportMode = mode;
                changed = true;
            }

            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                return true;
            }

            return false;
        }

        private static void SetAllSpritesSimple(string path, ref int skipped)
        {
            if (!AssetDatabase.IsValidFolder(path)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                EnsureSpriteType(importer, assetPath, SpriteImportMode.Single, Vector4.zero);
                skipped++;
            }
        }
    }
}
