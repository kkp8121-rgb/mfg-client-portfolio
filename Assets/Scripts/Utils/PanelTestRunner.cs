using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace MkLike.Utils
{
    /// <summary>
    /// 런타임 패널 순회 테스트. Play 모드에서 모든 UITK 패널을 1초 간격으로 켜고 끄며
    /// 콘솔 에러를 유발하는 패널을 찾는다.
    /// TestManager 또는 단독 실행: PanelTestRunner.RunAll()
    /// </summary>
    public class PanelTestRunner : MonoBehaviour
    {
        private static readonly string[] PANEL_NAMES =
        {
            "[UITK] Char_Stat",
            "[UITK] Char_Equip",
            "[UITK] Char_Skill",
            "[UITK] Char_Job",
            // "[UITK] Char_Relic" — 2026-04-20 유물 시스템 완전 제거
            "[UITK] Char_Climber",
            "[UITK] Char_Ability",
            "[UITK] Dungeon",
            "[UITK] Shop",
            "[UITK] Companion",
            "[UITK] CollectionBook",
            "[UITK] BattlePass",
            "[UITK] Costume",
            "[UITK] Arena",
            "[UITK] ArenaBattle",
            "[UITK] Guild",
            "[UITK] GuildBoss",
            "[UITK] Popup_OfflineReward",
            "[UITK] Popup_Settings",
            "[UITK] Popup_GachaResult",
            "[UITK] Popup_EquipCompare",
            "[UITK] Popup_CurrencyShortage",
            "[UITK] Popup_ArenaResult",
            "[UITK] Popup_GuildBoss",
        };

        /// <summary>씬에 PanelTestRunner 컴포넌트가 있으면 자동 실행</summary>
        private void Start()
        {
            RunAllAsync().Forget();
        }

        public async UniTaskVoid RunAllAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            // 2초 대기 (시스템 초기화)
            await UniTask.Delay(2000, cancellationToken: ct);

            Debug.Log("[PanelTestRunner] ═══ 패널 순회 테스트 시작 ═══");

            var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var panelMap = new Dictionary<string, GameObject>();

            foreach (var doc in docs)
            {
                panelMap[doc.gameObject.name] = doc.gameObject;
            }

            int passed = 0;
            int failed = 0;
            var failedPanels = new List<string>();

            foreach (var panelName in PANEL_NAMES)
            {
                if (!panelMap.TryGetValue(panelName, out var go))
                {
                    Debug.LogWarning($"[PanelTestRunner] ✗ {panelName} — 씬에 없음");
                    failed++;
                    failedPanels.Add($"{panelName} (미배치)");
                    continue;
                }

                // 패널 활성화
                bool hadError = false;
                Application.logMessageReceived += OnLog;

                void OnLog(string msg, string stack, LogType type)
                {
                    if (type == LogType.Exception || type == LogType.Error)
                        hadError = true;
                }

                go.SetActive(true);

                // 0.5초 대기 (OnEnable 처리)
                await UniTask.Delay(500, cancellationToken: ct);

                // 비활성화
                go.SetActive(false);
                await UniTask.Delay(100, cancellationToken: ct);

                Application.logMessageReceived -= OnLog;

                if (hadError)
                {
                    Debug.LogWarning($"[PanelTestRunner] ✗ {panelName} — 에러 발생");
                    failed++;
                    failedPanels.Add(panelName);
                }
                else
                {
                    Debug.LogWarning($"[PanelTestRunner] ✓ {panelName}");
                    passed++;
                }
            }

            Debug.LogWarning($"[PanelTestRunner] ═══ 결과: {passed} 통과 / {failed} 실패 ═══");
            if (failedPanels.Count > 0)
            {
                Debug.LogWarning($"[PanelTestRunner] 실패 패널: {string.Join(", ", failedPanels)}");
            }

            // 결과를 파일로 저장
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[PanelTestRunner] 결과: {passed} 통과 / {failed} 실패");
            if (failedPanels.Count > 0)
                sb.AppendLine($"실패 패널: {string.Join(", ", failedPanels)}");
            else
                sb.AppendLine("모든 패널 통과!");

            string path = System.IO.Path.Combine(Application.dataPath, "../Logs/PanelTestResult.txt");
            System.IO.File.WriteAllText(path, sb.ToString());

            // 테스트 완료 후 자기 제거
            Destroy(gameObject);
        }
    }
}
