using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// 온보딩 상태 리셋 유틸.
    /// /run QA 테스트 시 세이브 + PlayerPrefs(dev-uid, HasCompletedOnboarding)를 한 번에 초기화.
    /// </summary>
    public static class OnboardingResetEditor
    {
        private const string MENU_ROOT = "mkLike/Save/";

        [MenuItem(MENU_ROOT + "Reset Onboarding (dev-uid + 플래그)", priority = 100)]
        public static void ResetOnboarding()
        {
            PlayerPrefs.DeleteKey("mklike_dev_uid");
            PlayerPrefs.SetInt("Settings_HasCompletedOnboarding", 0);
            PlayerPrefs.Save();
            Debug.Log("[OnboardingReset] dev-uid + HasCompletedOnboarding 제거 완료");
            EditorUtility.DisplayDialog("온보딩 리셋",
                "dev-uid 및 HasCompletedOnboarding 플래그 제거 완료.\n다음 Play 시 Title → Login → Nickname → JobSelect 플로우가 실행됩니다.",
                "확인");
        }

        [MenuItem(MENU_ROOT + "Reset Onboarding + Save (완전 신규 유저)", priority = 101)]
        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            var savePath = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
                Debug.Log($"[OnboardingReset] 세이브 파일 삭제: {savePath}");
            }

            var backupPath = savePath + ".bak";
            if (System.IO.File.Exists(backupPath)) System.IO.File.Delete(backupPath);
            var backup2Path = savePath + ".bak2";
            if (System.IO.File.Exists(backup2Path)) System.IO.File.Delete(backup2Path);

            Debug.Log("[OnboardingReset] 전체 초기화 완료 (PlayerPrefs + 세이브 파일 + 백업 2종)");
            EditorUtility.DisplayDialog("완전 리셋",
                "PlayerPrefs, 세이브 파일(save.json/.bak/.bak2) 모두 제거.\n완전한 신규 유저 상태로 Play 가능.",
                "확인");
        }
    }
}
