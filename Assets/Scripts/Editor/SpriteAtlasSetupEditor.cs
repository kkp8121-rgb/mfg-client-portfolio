using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;

namespace MkLike.Editor
{
    public static class SpriteAtlasSetupEditor
    {
        public static void SetupAtlases()
        {
            CreateAtlasIfNeeded("Atlas_UI_HUD",
                new[] { "Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Icon_ButtonIcon_(x2)" },
                2048);

            CreateAtlasIfNeeded("Atlas_UI_Popup",
                new[] { "Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Frame" },
                2048);

            CreateAtlasIfNeeded("Atlas_VFX",
                new[] { "Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component/Icon_ItemIcon_(x2)" },
                1024);

            Debug.Log("[SpriteAtlasSetup] 스프라이트 아틀라스 설정 완료!");
        }

        private static void CreateAtlasIfNeeded(string atlasName, string[] folderPaths, int maxSize)
        {
            string atlasPath = $"Assets/Settings/SpriteAtlases/{atlasName}.spriteatlas";

            var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (existing != null)
            {
                Debug.Log($"[SpriteAtlasSetup] 이미 존재: {atlasName}");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Settings/SpriteAtlases"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "SpriteAtlases");
            }

            var atlas = new SpriteAtlas();

            var packSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                enableRotation = false,
                enableTightPacking = false,
                padding = 4
            };
            atlas.SetPackingSettings(packSettings);

            var texSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear
            };
            atlas.SetTextureSettings(texSettings);

            var platformSettings = atlas.GetPlatformSettings("DefaultTexturePlatform");
            platformSettings.overridden = true;
            platformSettings.maxTextureSize = maxSize;
            platformSettings.textureCompression = TextureImporterCompression.Compressed;
            platformSettings.format = TextureImporterFormat.Automatic;
            atlas.SetPlatformSettings(platformSettings);

            var objects = new Object[folderPaths.Length];
            for (int i = 0; i < folderPaths.Length; i++)
            {
                objects[i] = AssetDatabase.LoadAssetAtPath<Object>(folderPaths[i]);
            }
            atlas.Add(objects);

            AssetDatabase.CreateAsset(atlas, atlasPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpriteAtlasSetup] 아틀라스 생성: {atlasName} (maxSize={maxSize})");
        }
    }
}
