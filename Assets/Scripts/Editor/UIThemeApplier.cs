using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using MkLike.UI;
using System.Collections.Generic;
using System.IO;

namespace MkLike.Editor
{
    /// <summary>
    /// 선택한 GameObject 또는 씬 전체 UI에 Dark Geo 테마 색상을 일괄 적용하는 에디터 도구.
    /// Stone GUI Kit 스프라이트를 Resources 폴더로 복사하는 기능도 포함.
    /// </summary>
    public static class UIThemeApplier
    {
        private static readonly UITheme DefaultTheme = new UITheme();

        private const string StoneSpriteRoot = "Assets/Folder_Assets/GUI_Kit_Stone/Sprites";
        private const string DstUIRoot = "Assets/Resources/UI/Stone";
        private const string DstIconRoot = "Assets/Resources/Icons/Stone";

        [MenuItem("mkLike/UI/Apply Dark Theme (Selected)")]
        private static void ApplyToSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[UIThemeApplier] 선택된 GameObject가 없습니다.");
                return;
            }

            Undo.RecordObject(selected, "Apply Dark Theme");

            var theme = GetActiveTheme();
            ApplyToHierarchy(selected, theme);

            Debug.Log($"[UIThemeApplier] '{selected.name}' 하위에 Dark Theme 적용 완료.");
        }

        [MenuItem("mkLike/UI/Apply Dark Theme (All Canvas)")]
        private static void ApplyToAllCanvas()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length == 0)
            {
                Debug.LogWarning("[UIThemeApplier] 씬에 Canvas가 없습니다.");
                return;
            }

            var theme = GetActiveTheme();
            int count = 0;

            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                Undo.RecordObject(canvas.gameObject, "Apply Dark Theme All");
                ApplyToHierarchy(canvas.gameObject, theme);
                count++;
            }

            Debug.Log($"[UIThemeApplier] {count}개 Canvas에 Dark Theme 적용 완료.");
        }

        private static UITheme GetActiveTheme()
        {
            var manager = Object.FindFirstObjectByType<UIThemeManager>();
            if (manager != null && manager.Theme != null)
                return manager.Theme;
            return DefaultTheme;
        }

        private static void ApplyToHierarchy(GameObject root, UITheme theme)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                Undo.RecordObject(img, "Theme Color");

                if (img.GetComponent<Button>() != null)
                    img.color = theme.buttonNormal;
                else if (img.CompareTag("Untagged") || img.CompareTag("ThemePanel"))
                    img.color = theme.panelBackground;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Undo.RecordObject(texts[i], "Theme Color");
                texts[i].color = theme.textPrimary;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Stone GUI Kit → Resources 복사
        // ─────────────────────────────────────────────────────────

        [MenuItem("mkLike/UI/Copy Stone Kit Sprites")]
        public static void CopyStoneKitToResources()
        {
            int copied = 0;

            // 1. Buttons — 19_Function_Button/ButtonBg 에 5색 모두 있음
            copied += CopySprites(
                $"{StoneSpriteRoot}/19_Function_Button/ButtonBg",
                $"{DstUIRoot}/Button",
                new Dictionary<string, string>
                {
                    { "common_btn_color_brown.png", "common_btn_color_brown.png" },
                    { "common_btn_color_green.png", "common_btn_color_green.png" },
                    { "common_btn_color_red.png", "common_btn_color_red.png" },
                    { "common_btn_color_blue.png", "common_btn_color_blue.png" },
                    { "common_btn_color_purple.png", "common_btn_color_purple.png" },
                });

            // 2. Status bar frames
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Frame",
                new Dictionary<string, string>
                {
                    { "status_bg_color.png", "status_bg_color.png" },
                });
            copied += CopySprites(
                $"{StoneSpriteRoot}/00_Title",
                $"{DstUIRoot}/Frame",
                new Dictionary<string, string>
                {
                    { "status_bg_common.png", "status_bg_common.png" },
                });

            // 3. Skill frames — 17_Components (6 colors)
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Skill",
                new Dictionary<string, string>
                {
                    { "skill_frame_blue.png", "skill_frame_blue.png" },
                    { "skill_frame_green.png", "skill_frame_green.png" },
                    { "skill_frame_purple.png", "skill_frame_purple.png" },
                    { "skill_frame_red.png", "skill_frame_red.png" },
                    { "skill_frame_sliver.png", "skill_frame_sliver.png" },
                    { "skill_frame_yellow.png", "skill_frame_yellow.png" },
                });

            // 4. Skill bars — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Skill",
                new Dictionary<string, string>
                {
                    { "skill_bar_bg.png", "skill_bar_bg.png" },
                    { "skill_bar_blue.png", "skill_bar_blue.png" },
                    { "skill_bar_green.png", "skill_bar_green.png" },
                    { "skill_bar_purple.png", "skill_bar_purple.png" },
                    { "skill_bar_red.png", "skill_bar_red.png" },
                    { "skill_bar_yellow.png", "skill_bar_yellow.png" },
                    { "skill_bar_gray.png", "skill_bar_gray.png" },
                    { "skill_bar_n.png", "skill_bar_n.png" },
                    { "skill_bar_n_white.png", "skill_bar_n_white.png" },
                    { "skill_bar_line.png", "skill_bar_line.png" },
                });

            // 5. Popup — 20_Popup
            copied += CopySprites(
                $"{StoneSpriteRoot}/20_Popup",
                $"{DstUIRoot}/Popup",
                new Dictionary<string, string>
                {
                    { "popup_bg.png", "popup_bg.png" },
                    { "popup_btn_close.png", "popup_btn_close.png" },
                    { "common_popup_title_00.png", "popup_title_00.png" },
                    { "common_popup_title_01.png", "popup_title_01.png" },
                    { "common_popup_title_02.png", "popup_title_02.png" },
                    { "common_popup_title_03.png", "popup_title_03.png" },
                });

            // 6. Item frames — 11_Item
            copied += CopySprites(
                $"{StoneSpriteRoot}/11_Item",
                $"{DstUIRoot}/Frame",
                new Dictionary<string, string>
                {
                    { "item_frame.png", "item_frame.png" },
                    { "item_frame_white.png", "item_frame_white.png" },
                });

            // 7. Mission frames — 07_Mission
            copied += CopySprites(
                $"{StoneSpriteRoot}/07_Mission",
                $"{DstUIRoot}/Frame",
                new Dictionary<string, string>
                {
                    { "mission_frame_brown.png", "mission_frame_brown.png" },
                    { "mission_frame_green.png", "mission_frame_green.png" },
                    { "mission_frame_silver.png", "mission_frame_silver.png" },
                });

            // 8. Common UI elements — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Frame",
                new Dictionary<string, string>
                {
                    { "common_background.png", "common_background.png" },
                    { "common_ribbon_brown.png", "common_ribbon_brown.png" },
                    { "common_ribbon_green.png", "common_ribbon_green.png" },
                    { "label_brown.png", "label_brown.png" },
                    { "label_orange.png", "label_orange.png" },
                    { "popup_btn_close.png", "popup_btn_close.png" },
                });

            // 9. Gauge — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Gage",
                new Dictionary<string, string>
                {
                    { "common_gage_bg.png", "common_gage_bg.png" },
                    { "common_gage_brown.png", "common_gage_brown.png" },
                    { "common_gage_orange.png", "common_gage_orange.png" },
                    { "common_loading_bar.png", "common_loading_bar.png" },
                    { "common_loading_bg.png", "common_loading_bg.png" },
                });

            // 10. Tab — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Tab",
                new Dictionary<string, string>
                {
                    { "shop_tab_n.png", "shop_tab_n.png" },
                    { "shop_tab_s.png", "shop_tab_s.png" },
                    { "shop_label_purple.png", "shop_label_purple.png" },
                    { "shop_label_red.png", "shop_label_red.png" },
                });

            // 11. Navigation/misc — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Misc",
                new Dictionary<string, string>
                {
                    { "notification_alert.png", "notification_alert.png" },
                    { "notification_new.png", "notification_new.png" },
                    { "notification_bg_01.png", "notification_bg_01.png" },
                    { "notification_bg_02.png", "notification_bg_02.png" },
                    { "page_next.png", "page_next.png" },
                    { "page_prev.png", "page_prev.png" },
                    { "check_off_tiny.png", "check_off_tiny.png" },
                    { "check_on_tiny.png", "check_on_tiny.png" },
                    { "radiobtn_off.png", "radiobtn_off.png" },
                    { "radiobtn_on.png", "radiobtn_on.png" },
                    { "switch_bg.png", "switch_bg.png" },
                    { "switch_off.png", "switch_off.png" },
                    { "switch_on.png", "switch_on.png" },
                    { "stage_frame_complete.png", "stage_frame_complete.png" },
                    { "stage_frame_default.png", "stage_frame_default.png" },
                    { "stage_frame_lock.png", "stage_frame_lock.png" },
                    { "stage_star.png", "stage_star.png" },
                });

            // 12. Slider — 17_Components
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Slider",
                new Dictionary<string, string>
                {
                    { "slidebar_bar.png", "slidebar_bar.png" },
                    { "slidebar_bg.png", "slidebar_bg.png" },
                    { "slidebar_point.png", "slidebar_point.png" },
                    { "slidebar_point_first.png", "slidebar_point_first.png" },
                });

            // 13. User Info — 17_Components / 00_Title
            copied += CopySprites(
                $"{StoneSpriteRoot}/17_Components",
                $"{DstUIRoot}/Status",
                new Dictionary<string, string>
                {
                    { "user_info_gage_bg.png", "user_info_gage_bg.png" },
                    { "user_info_gage_01_bar_orange.png", "user_info_gage_01_bar_orange.png" },
                    { "user_info_gage_01_bar_white.png", "user_info_gage_01_bar_white.png" },
                    { "user_info_gage_02_bar_green.png", "user_info_gage_02_bar_green.png" },
                    { "user_info_gage_02_bar_white.png", "user_info_gage_02_bar_white.png" },
                    { "user_info_lv_bg.png", "user_info_lv_bg.png" },
                    { "user_info_profile.png", "user_info_profile.png" },
                    { "user_info_profile_bg.png", "user_info_profile_bg.png" },
                    { "user_info_profile_default.png", "user_info_profile_default.png" },
                    { "status_icon_coin.png", "status_icon_coin.png" },
                    { "status_icon_gem.png", "status_icon_gem.png" },
                    { "status_icon_soul_gem.png", "status_icon_soul_gem.png" },
                });

            // 14. Title screen elements
            copied += CopySprites(
                $"{StoneSpriteRoot}/00_Title",
                $"{DstUIRoot}/Title",
                new Dictionary<string, string>
                {
                    { "menu_bg.png", "menu_bg.png" },
                    { "menu_item.png", "menu_item.png" },
                    { "menu_message.png", "menu_message.png" },
                    { "menu_mission.png", "menu_mission.png" },
                    { "menu_ranking.png", "menu_ranking.png" },
                    { "menu_setting.png", "menu_setting.png" },
                    { "menu_shop.png", "menu_shop.png" },
                    { "btn_start_n.png", "btn_start_n.png" },
                    { "btn_start_f.png", "btn_start_f.png" },
                });

            // 15. Mission extras — 07_Mission
            copied += CopySprites(
                $"{StoneSpriteRoot}/07_Mission",
                $"{DstUIRoot}/Mission",
                new Dictionary<string, string>
                {
                    { "mission_clear_star.png", "mission_clear_star.png" },
                    { "mission_clear_star_bg.png", "mission_clear_star_bg.png" },
                    { "mission_img_collect.png", "mission_img_collect.png" },
                    { "mission_img_crown.png", "mission_img_crown.png" },
                    { "mission_img_goal.png", "mission_img_goal.png" },
                    { "mission_img_key.png", "mission_img_key.png" },
                    { "mission_img_kill.png", "mission_img_kill.png" },
                    { "mission_btn_icon_coin.png", "mission_btn_icon_coin.png" },
                    { "mission_btn_icon_gem.png", "mission_btn_icon_gem.png" },
                    { "mission_btn_icon_soulgem.png", "mission_btn_icon_soulgem.png" },
                });

            // 16. Icons — 16_Icons (모든 icon_*.png)
            copied += CopyAllIconSprites();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (copied > 0)
                Debug.Log($"<color=lime>[UIThemeApplier] Stone Kit 스프라이트 {copied}개 복사 완료 → Resources</color>");
            else
                Debug.Log("[UIThemeApplier] Stone Kit 스프라이트: 이미 최신 상태 (복사 0개)");
        }

        /// <summary>
        /// 16_Icons 폴더의 모든 icon_*.png를 Resources/Icons/Stone/으로 복사.
        /// AssetDatabase.FindAssets로 열거하여 실제 파일만 복사한다.
        /// </summary>
        private static int CopyAllIconSprites()
        {
            string srcFolder = $"{StoneSpriteRoot}/16_Icons";
            if (!AssetDatabase.IsValidFolder(srcFolder)) return 0;

            EnsureFolder(DstIconRoot);

            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { srcFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string srcPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileName(srcPath);

                // Thumbs.db 등 비이미지 파일 제외
                if (!fileName.EndsWith(".png") && !fileName.EndsWith(".jpg")) continue;

                string dstPath = $"{DstIconRoot}/{fileName}";
                if (AssetDatabase.LoadAssetAtPath<Object>(dstPath) != null) continue;

                if (AssetDatabase.CopyAsset(srcPath, dstPath))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 지정 매핑에 따라 개별 스프라이트를 복사한다.
        /// </summary>
        /// <param name="srcFolder">소스 폴더 (Unity asset 경로)</param>
        /// <param name="dstFolder">대상 폴더 (Unity asset 경로)</param>
        /// <param name="fileMap">소스파일명 → 대상파일명 매핑</param>
        /// <returns>복사된 파일 수</returns>
        private static int CopySprites(string srcFolder, string dstFolder,
            Dictionary<string, string> fileMap)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder)) return 0;

            EnsureFolder(dstFolder);

            int count = 0;
            foreach (var kvp in fileMap)
            {
                string srcPath = $"{srcFolder}/{kvp.Key}";
                string dstPath = $"{dstFolder}/{kvp.Value}";

                // 소스 파일 존재 확인
                if (AssetDatabase.LoadAssetAtPath<Object>(srcPath) == null) continue;

                // 대상에 이미 존재하면 스킵
                if (AssetDatabase.LoadAssetAtPath<Object>(dstPath) != null) continue;

                if (AssetDatabase.CopyAsset(srcPath, dstPath))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Unity 에셋 폴더 경로를 재귀적으로 생성한다.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
