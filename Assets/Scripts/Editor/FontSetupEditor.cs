using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

namespace MkLike.Editor
{
    /// <summary>
    /// 한글 TMP 폰트 에셋을 자동 생성하는 에디터 도구.
    /// 메뉴: MkLike > Scene Setup > Setup Korean Font
    ///
    /// Windows 시스템 폰트(맑은 고딕)를 소스로 사용하여
    /// Dynamic TMP 폰트 에셋을 생성한다.
    /// </summary>
    public static class FontSetupEditor
    {
        public const string FontAssetPath = "Assets/Folder_Assets/Fonts/KoreanFont SDF.asset";
        public const string FontFolderPath = "Assets/Fonts";

        // Windows 시스템 한글 폰트 경로 후보
        private static readonly string[] SystemFontPaths = new[]
        {
            "C:/Windows/Fonts/malgun.ttf",      // 맑은 고딕
            "C:/Windows/Fonts/NanumGothic.ttf",  // 나눔고딕
            "C:/Windows/Fonts/gulim.ttc",        // 굴림
            "C:/Windows/Fonts/batang.ttc",       // 바탕
        };

        // MenuItem은 MasterSetupEditor에서 호출
        public static void SetupKoreanFont()
        {
            var fontAsset = GetOrCreateKoreanFont();
            if (fontAsset != null)
            {
                SetAsTMPDefault(fontAsset);
                Debug.Log($"<color=cyan>[FontSetup] 한글 폰트 설정 완료: {FontAssetPath}</color>");
            }
        }

        /// <summary>
        /// 한글 TMP 폰트 에셋을 가져오거나 생성한다.
        /// 다른 에디터 스크립트에서 호출 가능.
        /// </summary>
        public static TMP_FontAsset GetOrCreateKoreanFont()
        {
            // 이미 존재하면 재사용
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                Debug.Log("[FontSetup] 한글 폰트 에셋 이미 존재 — 재사용");
                return existing;
            }

            // 소스 폰트 찾기
            Font sourceFont = FindKoreanSystemFont();
            if (sourceFont == null)
            {
                Debug.LogError("[FontSetup] 한글 시스템 폰트를 찾을 수 없습니다!");
                return null;
            }

            // 폴더 생성
            if (!AssetDatabase.IsValidFolder(FontFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Fonts");
            }

            // 소스 폰트를 프로젝트에 복사
            string sourceFontProjectPath = $"{FontFolderPath}/KoreanFont.ttf";
            if (!File.Exists(Path.GetFullPath(sourceFontProjectPath)))
            {
                string systemPath = GetSystemFontPath();
                if (systemPath != null)
                {
                    File.Copy(systemPath, Path.GetFullPath(sourceFontProjectPath), true);
                    AssetDatabase.Refresh();
                }
            }

            // 프로젝트 내 폰트 로드
            var projectFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontProjectPath);
            if (projectFont == null)
            {
                // 복사 실패 시 시스템 폰트 직접 사용
                projectFont = sourceFont;
            }

            // TMP 폰트 에셋 생성 (Dynamic)
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                projectFont,
                90,                          // samplingPointSize
                9,                           // atlasPadding
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,                        // atlasWidth
                1024                         // atlasHeight
            );

            if (fontAsset == null)
            {
                Debug.LogError("[FontSetup] TMP 폰트 에셋 생성 실패!");
                return null;
            }

            // Dynamic 모드 설정 (런타임에 글리프 자동 로드)
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Atlas 텍스처도 서브에셋으로 저장
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = "KoreanFont SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            // Material도 서브에셋으로 저장
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "KoreanFont SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FontSetup] 한글 TMP 폰트 에셋 생성 완료");
            return fontAsset;
        }

        /// <summary>
        /// TMP Settings의 기본 폰트와 Fallback에 한글 폰트를 설정한다.
        /// </summary>
        private static void SetAsTMPDefault(TMP_FontAsset koreanFont)
        {
            string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[FontSetup] TMP Settings를 찾을 수 없습니다. 수동으로 Fallback을 설정해주세요.");
                return;
            }

            var so = new SerializedObject(settings);

            // Fallback 폰트 리스트에 추가
            var fallbackProp = so.FindProperty("m_fallbackFontAssets");
            if (fallbackProp != null)
            {
                // 이미 추가되어 있는지 확인
                bool alreadyAdded = false;
                for (int i = 0; i < fallbackProp.arraySize; i++)
                {
                    if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == koreanFont)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    fallbackProp.arraySize++;
                    fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1)
                        .objectReferenceValue = koreanFont;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[FontSetup] TMP Settings Fallback에 한글 폰트 추가 완료");
        }

        private static Font FindKoreanSystemFont()
        {
            // 시스템 폰트에서 한글 폰트 검색
            string[] fontNames = new[] { "Malgun Gothic", "NanumGothic", "Gulim", "맑은 고딕" };
            foreach (string fontName in fontNames)
            {
                Font font = Font.CreateDynamicFontFromOSFont(fontName, 16);
                if (font != null) return font;
            }
            return null;
        }

        private static string GetSystemFontPath()
        {
            foreach (string path in SystemFontPaths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
