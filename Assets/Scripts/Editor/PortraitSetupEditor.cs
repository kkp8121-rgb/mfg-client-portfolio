using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// 모바일 세로 모드(Portrait) 기본 설정을 적용한다.
    /// </summary>
    public static class PortraitSetupEditor
    {
        [MenuItem("mkLike/Set Portrait Mode")]
        public static void SetPortraitMode()
        {
            // 기본 방향: Portrait
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // 허용 방향: Portrait만
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Game 뷰 해상도 힌트 (에디터에서 세로로 보이도록)
            Debug.Log("[PortraitSetup] Portrait 모드 설정 완료. Game 뷰에서 9:16 (1080x1920) 해상도를 선택하세요.");
        }
    }
}
