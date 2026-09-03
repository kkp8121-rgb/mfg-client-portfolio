#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using MkLike.Combat;
using MkLike.Core.Save;
using MkLike.Dungeon;
using MkLike.Growth;
using MkLike.Quest;
using MkLike.Utils;
using MkLike.UI;

namespace MkLike.Editor
{
    public struct InvariantResult
    {
        public string Name;
        public bool Pass;
        public string Message;
        public Severity Level;

        public enum Severity { Critical, Warning, Info }
    }

    /// <summary>
    /// 봇/QA 자동 검증용 invariant 모음. AutoPlayBot이 시작/주기/로드 후에 호출한다.
    /// 각 invariant는 사용자 관점 결함 (UI 누락/데이터 없음/중복/불일치)을 즉시 드러낸다.
    /// </summary>
    public static class BotInvariants
    {
        /// <summary>모든 invariant를 실행하고 결과 리스트를 반환.</summary>
        public static List<InvariantResult> RunAll()
        {
            var results = new List<InvariantResult>
            {
                Check_UniqueUitkPanels(),
                Check_PlayerCoreComponents(),
                Check_MonsterPrefabsUnique(),
                Check_SpawnedMonsterStats(),
                Check_LevelSystemRange(),
                Check_QuestManagerIndex(),
                Check_UitkDocumentUnique(),
                Check_CharacterVisualJobMatch(),
                Check_ActiveSkillTierMatch(),
                Check_BattlePassRewards(),
                Check_CollectionCategoryTotals(),
                Check_QuickMenuButtons(),
                Check_UpdateManagerInstance(),
                Check_NoRuntimeMonsterResidue(),
                Check_NoLegacyCanvasWidgets(),
                // 2026-04-23 Phase B 피드백 invariant 4종 — 씬 설정 회귀 방지
                Check_FeedbackBusSubscriberPresent(),
                Check_MonsterVisualEffectPresent(),
                Check_JoystickUIPresent(),
                Check_ToastUIRuntimeHealth(),
                // 2026-04-23 5번째: UIDocument layout 건강 (Char_Equip/Char_Skill NaN worldBound 버그 감지)
                Check_ActiveUIDocumentLayout(),
                // 2026-04-23 6번째: 스킬 슬롯 레벨 정합 (slotLevels vs SkillSystem.GetSlotLevel)
                Check_SkillSlotLevelsSync(),
                // 2026-04-23 7~9번째: 런타임 건강성 추가
                Check_NoNegativeCurrency(),
                Check_PoolManagerPresent(),
                Check_SortingOrderUnique(),
                // 2026-04-23 10~11번째: 싱글턴/세이브 정합
                Check_PersistentSingletonsNotNull(),
                Check_BossRaidSaveSync(),
            };
            return results;
        }

        /// <summary>실패 항목만 반환 (AutoPlayBot 리포트용).</summary>
        public static List<InvariantResult> RunFailuresOnly()
        {
            return RunAll().Where(r => !r.Pass).ToList();
        }

        // ──────────────────────────────────────────
        //  개별 invariant 구현
        // ──────────────────────────────────────────

        /// 1. [UITK]* 동명 GameObject 개수 ≤ 1
        static InvariantResult Check_UniqueUitkPanels()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("UniqueUitkPanels", "scene not loaded");

            var nameCount = new Dictionary<string, int>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                if (!root.name.StartsWith("[UITK]")) continue;
                nameCount.TryGetValue(root.name, out int c);
                nameCount[root.name] = c + 1;
            }
            var dup = nameCount.Where(kv => kv.Value > 1).Select(kv => $"{kv.Key}×{kv.Value}").ToList();
            return dup.Count == 0
                ? Pass("UniqueUitkPanels", $"{nameCount.Count} unique UITK panels")
                : Fail("UniqueUitkPanels", $"중복: {string.Join(", ", dup)}", InvariantResult.Severity.Critical);
        }

        /// 2. Player는 PlayerCharacter/CombatStats/CharacterCombat 각 1개
        static InvariantResult Check_PlayerCoreComponents()
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            if (player == null) return Skip("PlayerCoreComponents", "Player not in scene");

            int pcCount = player.GetComponents<PlayerCharacter>().Length;
            int csCount = player.GetComponents<CombatStats>().Length;
            int ccCount = player.GetComponents<CharacterCombat>().Length;
            if (pcCount == 1 && csCount == 1 && ccCount == 1)
                return Pass("PlayerCoreComponents", "all singular");
            return Fail("PlayerCoreComponents",
                $"PlayerCharacter×{pcCount}, CombatStats×{csCount}, CharacterCombat×{ccCount}",
                InvariantResult.Severity.Critical);
        }

        /// 3. 모든 몬스터 프리팹 에셋에 MonsterController 정확히 1개
        static InvariantResult Check_MonsterPrefabsUnique()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab Monster_",
                new[] { "Assets/Prefabs" });
            var bad = new List<string>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                int count = prefab.GetComponentsInChildren<MonsterController>(true).Length;
                if (count != 1) bad.Add($"{prefab.name}×{count}");
            }
            return bad.Count == 0
                ? Pass("MonsterPrefabsUnique", $"{guids.Length} prefabs clean")
                : Fail("MonsterPrefabsUnique", string.Join(", ", bad), InvariantResult.Severity.Critical);
        }

        /// 4. 활성 몬스터 스탯 정상.
        /// zeroHp: 스폰됐지만 Initialize 안 됨. LeanPool.Spawn이 SetActive(true) 반환 직후 같은 프레임에
        ///         MonsterSpawner.SpawnMonster가 아직 CombatStats.Initialize를 호출 전일 수 있음 —
        ///         일시적 상태(배치당 일부) 허용. 치명 임계: zeroHp > 30% 또는 zeroHp > 5 (절대치).
        /// stuckDead: IsDead인데 _isDying=false (진짜 stuck — dieTimer 카운트다운 경로 단절)
        /// isDying=true with timer>0은 사망 애니 중 — 정상, 제외.
        static InvariantResult Check_SpawnedMonsterStats()
        {
            var mcs = Object.FindObjectsByType<MonsterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int zeroHp = 0, stuckDead = 0;
            var isDyingField = typeof(MonsterController).GetField("_isDying",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var m in mcs)
            {
                var s = m.GetComponent<CombatStats>();
                if (s == null) continue;
                if (s.MaxHp <= 0) zeroHp++;
                if (s.IsDead && m.gameObject.activeInHierarchy)
                {
                    bool isDying = isDyingField != null && (bool)isDyingField.GetValue(m);
                    if (!isDying) stuckDead++; // dieTimer 카운트다운 안 하는 진짜 stuck
                }
            }
            if (stuckDead == 0 && zeroHp == 0)
                return Pass("SpawnedMonsterStats", $"{mcs.Length} active monsters ok");
            // 일시적 zeroHp는 경고, stuckDead는 항상 critical, zeroHp 비율 초과 시 critical
            bool criticalZero = mcs.Length > 0 && (zeroHp * 100 / mcs.Length > 30 || zeroHp > 5);
            if (stuckDead > 0 || criticalZero)
                return Fail("SpawnedMonsterStats",
                    $"zeroHp={zeroHp} stuckDead={stuckDead} (총 {mcs.Length})",
                    InvariantResult.Severity.Critical);
            return Fail("SpawnedMonsterStats",
                $"zeroHp={zeroHp} (일시적 pool spawn frame, 총 {mcs.Length})",
                InvariantResult.Severity.Warning);
        }

        /// 5. LevelSystem CurrentLevel ∈ [1,159] && RequiredExp 기대범위
        static InvariantResult Check_LevelSystemRange()
        {
            var lv = Object.FindFirstObjectByType<LevelSystem>(FindObjectsInactive.Include);
            if (lv == null) return Skip("LevelSystemRange", "no LevelSystem");

            int cur = lv.CurrentLevel;
            long req = lv.RequiredExp;
            if (cur < 1 || cur > 159)
                return Fail("LevelSystemRange", $"Level={cur} out of [1,159]", InvariantResult.Severity.Critical);

            // Lv1 required 기대: 25 (ExpTable[0]). Lv < 50 이면 req < 1e8 정도.
            if (cur <= 10 && req > 100_000)
                return Fail("LevelSystemRange", $"Lv{cur} RequiredExp={req:N0} — too high",
                    InvariantResult.Severity.Critical);
            if (req <= 0)
                return Fail("LevelSystemRange", $"Lv{cur} RequiredExp={req} — non-positive",
                    InvariantResult.Severity.Critical);
            return Pass("LevelSystemRange", $"Lv{cur} req={req:N0}");
        }

        /// 6. QuestManager.CompletedGuideIndex ∈ [-1, 400]
        static InvariantResult Check_QuestManagerIndex()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return Skip("QuestManagerIndex", "QuestManager null");
            int idx = qm.CompletedGuideIndex;
            if (idx < -1 || idx > 400)
                return Fail("QuestManagerIndex", $"index={idx}", InvariantResult.Severity.Critical);
            return Pass("QuestManagerIndex", $"index={idx}");
        }

        /// 7. 각 [UITK]* GameObject 당 UIDocument 정확히 1개
        static InvariantResult Check_UitkDocumentUnique()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("UitkDocumentUnique", "scene not loaded");

            var bad = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                if (!root.name.StartsWith("[UITK]")) continue;
                int docs = root.GetComponents<UIDocument>().Length;
                if (docs != 1) bad.Add($"{root.name} UIDoc×{docs}");
            }
            return bad.Count == 0
                ? Pass("UitkDocumentUnique", "all 1 UIDocument each")
                : Fail("UitkDocumentUnique", string.Join(", ", bad), InvariantResult.Severity.Warning);
        }

        /// 8. Player.CharacterVisual.DesignId가 JobSystem.CurrentJob (lower)과 매치
        static InvariantResult Check_CharacterVisualJobMatch()
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            if (player == null) return Skip("CharacterVisualJobMatch", "no Player");
            var cv = player.GetComponent<CharacterVisual>();
            var js = JobSystem.Instance;
            if (cv == null) return Skip("CharacterVisualJobMatch", "no CharacterVisual");
            if (js == null) return Skip("CharacterVisualJobMatch", "no JobSystem");
            string expected = js.CurrentJob.ToString().ToLowerInvariant();
            if (cv.DesignId != expected)
                return Fail("CharacterVisualJobMatch",
                    $"DesignId='{cv.DesignId}' expected='{expected}'",
                    InvariantResult.Severity.Warning);
            return Pass("CharacterVisualJobMatch", expected);
        }

        /// 9. SkillSystem.CurrentActiveSkill.jobTier ≤ SaveData.player.jobTier
        static InvariantResult Check_ActiveSkillTierMatch()
        {
            var ss = Object.FindFirstObjectByType<SkillSystem>(FindObjectsInactive.Include);
            if (ss == null) return Skip("ActiveSkillTierMatch", "no SkillSystem");
            var active = ss.CurrentActiveSkill;
            if (active == null) return Pass("ActiveSkillTierMatch", "no active skill (ok at fresh start)");

            int playerTier = SaveManager.Instance?.CurrentData?.player?.jobTier ?? 0;
            if (active.jobTier > playerTier)
                return Fail("ActiveSkillTierMatch",
                    $"'{active.id}' tier={active.jobTier} > playerTier={playerTier}",
                    InvariantResult.Severity.Critical);
            return Pass("ActiveSkillTierMatch", $"{active.id} (tier {active.jobTier} ≤ {playerTier})");
        }

        /// 10. BattlePassSystem.GetReward(lv) != null for lv ∈ [1,50]
        static InvariantResult Check_BattlePassRewards()
        {
            var bp = BattlePassSystem.Instance;
            if (bp == null) return Skip("BattlePassRewards", "no BattlePassSystem");
            int missing = 0;
            for (int lv = 1; lv <= 50; lv++)
                if (bp.GetReward(lv) == null) missing++;
            return missing == 0
                ? Pass("BattlePassRewards", "1..50 all populated")
                : Fail("BattlePassRewards", $"{missing}/50 missing", InvariantResult.Severity.Critical);
        }

        /// 11. CollectionBookManager 카테고리별 total > 0
        static InvariantResult Check_CollectionCategoryTotals()
        {
            var cbm = CollectionBookManager.Instance;
            if (cbm == null) return Skip("CollectionCategoryTotals", "no CBM");
            var bad = new List<string>();
            foreach (MkLike.Core.CollectionCategory cat in System.Enum.GetValues(typeof(MkLike.Core.CollectionCategory)))
            {
                // _Removed_* 카테고리는 ordinal 보존용 폐기 항목 — skip
                if (cat.ToString().StartsWith("_Removed_")) continue;
                var (_, total) = cbm.GetProgress(cat);
                if (total <= 0) bad.Add(cat.ToString());
            }
            return bad.Count == 0
                ? Pass("CollectionCategoryTotals", "all active categories > 0")
                : Fail("CollectionCategoryTotals", $"total≤0: {string.Join(",", bad)}",
                    InvariantResult.Severity.Warning);
        }

        /// 12. 퀵메뉴 버튼 UXML에 모두 존재 (Q<Button>() null 없음)
        static InvariantResult Check_QuickMenuButtons()
        {
            var hud = Object.FindFirstObjectByType<HudUI>(FindObjectsInactive.Include);
            if (hud == null) return Skip("QuickMenuButtons", "no HudUI");
            var doc = hud.GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
                return Skip("QuickMenuButtons", "no UIDocument rootVisualElement");
            // 2026-04-20 HUD 퀵메뉴 컷 이후 유지되는 5개 버튼만 체크
            string[] btnNames =
            {
                "quick-elite-btn","quick-hunt-btn","booster-btn",
                "quick-arena-btn","quick-guild-btn",
            };
            var missing = btnNames.Where(n => doc.rootVisualElement.Q<Button>(n) == null).ToList();
            return missing.Count == 0
                ? Pass("QuickMenuButtons", "all present")
                : Fail("QuickMenuButtons", $"missing: {string.Join(",", missing)}",
                    InvariantResult.Severity.Warning);
        }

        /// 13. Play 중 UpdateManager.Instance != null
        static InvariantResult Check_UpdateManagerInstance()
        {
            if (!EditorApplication.isPlaying)
                return Skip("UpdateManagerInstance", "edit mode");
            // UpdateManager는 Main 씬에만 존재. Title 씬 진행 중에는 정상적으로 null.
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "Main")
                return Skip("UpdateManagerInstance", $"scene={sceneName} (only checked in Main)");
            if (UpdateManager.Instance == null)
                return Fail("UpdateManagerInstance", "null in play mode — timing bug",
                    InvariantResult.Severity.Critical);
            return Pass("UpdateManagerInstance", "ok");
        }

        /// 14. 씬 루트에 Monster_* 잔재 없음.
        /// Edit 모드: 절대 존재하면 안 됨 (씬 저장 사고)
        /// Play 모드: LeanPool이 루트에 스폰 — 모두 활성이어야 정상. 비활성 잔재 X.
        static InvariantResult Check_NoRuntimeMonsterResidue()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("NoRuntimeMonsterResidue", "scene not loaded");
            var badList = new List<string>();
            bool isPlay = EditorApplication.isPlaying;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null || !root.name.StartsWith("Monster_")) continue;
                if (!isPlay)
                {
                    badList.Add(root.name); // edit 모드면 무조건 불량
                }
                else
                {
                    // Play 모드: 풀 반환 후 활성(=비정상 방치) or 사망+_isDying=false 인 진짜 stuck.
                    // _isDying=true + 활성 = 사망 애니 재생 중(정상) 이라 제외.
                    var stats = root.GetComponent<CombatStats>();
                    var mc = root.GetComponent<MonsterController>();
                    bool isDying = false;
                    if (mc != null)
                    {
                        var f = typeof(MonsterController).GetField("_isDying",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (f != null) isDying = (bool)f.GetValue(mc);
                    }
                    bool isStuckDead = stats != null && stats.IsDead && root.activeInHierarchy && !isDying;
                    if (isStuckDead)
                        badList.Add($"{root.name}(stuckDead)");
                }
            }
            if (badList.Count == 0)
                return Pass("NoRuntimeMonsterResidue", isPlay ? "no stuck" : "clean");
            return Fail("NoRuntimeMonsterResidue",
                $"{badList.Count} residue: {string.Join(",", badList.Take(5))}",
                InvariantResult.Severity.Critical);
        }

        /// 15. 레거시 uGUI 위젯 이름 (GoalGuideWidget/CpCounterWidget/ComboLogHUD 등) 없음
        static InvariantResult Check_NoLegacyCanvasWidgets()
        {
            string[] legacy = {
                "GoalGuideWidget","CpCounterWidget","ComboLogHUD","CombatFeedHUD",
                "DailyChecklistWidget","HudRoot","BottomBarBackground",
                "ContextRecommendBanner"
            };
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("NoLegacyCanvasWidgets", "scene not loaded");
            var found = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (System.Array.IndexOf(legacy, rt.gameObject.name) >= 0)
                        found.Add(rt.gameObject.name);
                }
            }
            return found.Count == 0
                ? Pass("NoLegacyCanvasWidgets", "clean")
                : Fail("NoLegacyCanvasWidgets", $"{found.Count} widgets: {string.Join(",", found.Take(5))}",
                    InvariantResult.Severity.Warning);
        }

        // ──────────────────────────────────────────
        //  2026-04-23 Phase B 피드백 invariant 4종
        //  씬 설정 회귀 방지 (Toast/VFX 씬 누락으로 기능이 침묵하는 버그 조기 감지)
        // ──────────────────────────────────────────

        /// <summary>FeedbackBusSubscriber가 씬에 배치됐는지 검증. 없으면 FeedbackBus.Emit가 침묵.</summary>
        static InvariantResult Check_FeedbackBusSubscriberPresent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("FeedbackBusSubscriberPresent", "scene not loaded");

            var subs = Object.FindObjectsByType<MkLike.UI.FeedbackBusSubscriber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (subs.Length == 0)
                return Fail("FeedbackBusSubscriberPresent",
                    "씬에 FeedbackBusSubscriber 없음 — FeedbackBus.Emit가 Toast/Shake/DamageText 발화 못 함",
                    InvariantResult.Severity.Critical);
            if (subs.Length > 1)
                return Fail("FeedbackBusSubscriberPresent",
                    $"중복 {subs.Length}개 — 이벤트가 중복 발화됨",
                    InvariantResult.Severity.Warning);
            return Pass("FeedbackBusSubscriberPresent", "1 subscriber in scene");
        }

        /// <summary>MonsterVisualEffect가 씬에 배치됐는지 검증. 없으면 보스/정예 스폰 연출 침묵.</summary>
        static InvariantResult Check_MonsterVisualEffectPresent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("MonsterVisualEffectPresent", "scene not loaded");

            var mves = Object.FindObjectsByType<MkLike.Combat.MonsterVisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (mves.Length == 0)
                return Fail("MonsterVisualEffectPresent",
                    "씬에 MonsterVisualEffect 없음 — 보스/정예 스폰 연출(PlayBossSpawn/BossAura) 미발화. MonsterSpawner/EliteSummonManager VFX 호출이 무시됨",
                    InvariantResult.Severity.Critical);
            return Pass("MonsterVisualEffectPresent", $"{mves.Length} instance");
        }

        /// <summary>FloatingJoystick + JoystickInputProvider가 씬에 배치됐는지. 이슈 6 회귀 방지.</summary>
        static InvariantResult Check_JoystickUIPresent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("JoystickUIPresent", "scene not loaded");

            var joystickType = System.Type.GetType("FloatingJoystick, Assembly-CSharp");
            if (joystickType == null)
                return Skip("JoystickUIPresent", "FloatingJoystick type not found");

            var joysticks = Object.FindObjectsByType(joystickType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            var providerType = System.Type.GetType("JoystickInputProvider, Assembly-CSharp");
            var providers = providerType != null
                ? Object.FindObjectsByType(providerType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                : new Object[0];

            if (joysticks.Length == 0)
                return Fail("JoystickUIPresent",
                    "FloatingJoystick 씬 누락 — 수동 이동 UI 없음 (이슈 6)",
                    InvariantResult.Severity.Critical);
            if (providers.Length == 0)
                return Fail("JoystickUIPresent",
                    "JoystickInputProvider 없음 — FloatingJoystick → JoystickBridge 연결 끊김",
                    InvariantResult.Severity.Critical);
            return Pass("JoystickUIPresent", $"{joysticks.Length} joystick + {providers.Length} provider");
        }

        /// <summary>활성 상태의 UIDocument들이 layout 완료됐는지 검증 (Play 모드 한정).
        /// 2026-04-23 Char_Equip/Char_Skill rootVisualElement.worldBound width/height=NaN 이슈 감지.
        /// NaN은 UIDocument가 PanelSettings에 attach 못 했거나 UXML 구조 오류.
        /// </summary>
        static InvariantResult Check_ActiveUIDocumentLayout()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("ActiveUIDocumentLayout", "edit mode");

            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("ActiveUIDocumentLayout", "scene not loaded");

            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var bad = new System.Collections.Generic.List<string>();
            foreach (var d in docs)
            {
                if (d.rootVisualElement == null) continue;
                var wb = d.rootVisualElement.worldBound;
                if (float.IsNaN(wb.width) || float.IsNaN(wb.height))
                    bad.Add(d.gameObject.name);
            }
            if (bad.Count > 0)
                return Fail("ActiveUIDocumentLayout",
                    $"NaN worldBound: {string.Join(", ", bad)} — PanelSettings/UXML 구조 오류 의심",
                    InvariantResult.Severity.Warning);
            return Pass("ActiveUIDocumentLayout", $"{docs.Length} active UIDocuments OK");
        }

        /// <summary>
        /// 스킬 슬롯 레벨 정합 (Phase B).
        /// SaveData.skillLevel.slotLevels[] vs SkillSystem.GetSlotLevel(slot) 일치 여부.
        /// 두 값이 달라지면 슬롯 마이그레이션 또는 SyncSlotLevelsToSave 누락 의심.
        /// </summary>
        static InvariantResult Check_SkillSlotLevelsSync()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("SkillSlotLevelsSync", "edit mode");

            var ss = Object.FindFirstObjectByType<SkillSystem>(FindObjectsInactive.Include);
            if (ss == null) return Skip("SkillSlotLevelsSync", "no SkillSystem");
            var save = SaveManager.Instance?.CurrentData?.skillLevel;
            if (save == null || save.slotLevels == null) return Skip("SkillSlotLevelsSync", "no save.skillLevel");
            if (!save.slotMigrated) return Pass("SkillSlotLevelsSync", "not yet migrated (ok at fresh)");

            var diffs = new System.Collections.Generic.List<string>();
            for (int slot = 0; slot < 4; slot++)
            {
                int saved = save.slotLevels[slot];
                int live = ss.GetSlotLevel(slot);
                if (saved != live)
                    diffs.Add($"slot[{slot}]: save={saved} live={live}");
            }
            if (diffs.Count > 0)
                return Fail("SkillSlotLevelsSync",
                    string.Join(", ", diffs) + " — SaveState 누락 또는 수동 변조 의심",
                    InvariantResult.Severity.Warning);
            return Pass("SkillSlotLevelsSync",
                $"[{save.slotLevels[0]},{save.slotLevels[1]},{save.slotLevels[2]},{save.slotLevels[3]}]");
        }

        /// <summary>CurrencyManager 모든 재화 >= 0 검증 (underflow 버그 감지).</summary>
        static InvariantResult Check_NoNegativeCurrency()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("NoNegativeCurrency", "edit mode");
            var cm = MkLike.Economy.CurrencyManager.Instance;
            if (cm == null) return Skip("NoNegativeCurrency", "no CurrencyManager");

            var negatives = new System.Collections.Generic.List<string>();
            foreach (var type in System.Enum.GetValues(typeof(MkLike.Core.CurrencyType)))
            {
                var ct = (MkLike.Core.CurrencyType)type;
                var amount = cm.GetAmount(ct);
                if (amount.IsNegative)
                    negatives.Add($"{ct}={amount}");
            }
            if (negatives.Count > 0)
                return Fail("NoNegativeCurrency", string.Join(", ", negatives), InvariantResult.Severity.Critical);
            return Pass("NoNegativeCurrency", "all non-negative");
        }

        /// <summary>PoolManager 존재 + 초기화 여부 검증. 몬스터/VFX/DamageText/드롭 필수.</summary>
        static InvariantResult Check_PoolManagerPresent()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("PoolManagerPresent", "edit mode");
            var pmType = System.Type.GetType("MkLike.Utils.PoolManager, Utils");
            if (pmType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    pmType = asm.GetType("MkLike.Utils.PoolManager");
                    if (pmType != null) break;
                }
            }
            if (pmType == null) return Skip("PoolManagerPresent", "PoolManager type not found");

            var instanceProp = pmType.GetProperty("Instance");
            if (instanceProp == null) return Skip("PoolManagerPresent", "no Instance property");
            var inst = instanceProp.GetValue(null);
            if (inst == null)
                return Fail("PoolManagerPresent",
                    "PoolManager.Instance null — 몬스터/VFX/DamageText/드롭 풀링 실패",
                    InvariantResult.Severity.Critical);
            return Pass("PoolManagerPresent", "OK");
        }

        /// <summary>활성 UIDocument들의 sortingOrder가 중복되지 않는지 검증 (탭별 고유값 정책).</summary>
        static InvariantResult Check_SortingOrderUnique()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("SortingOrderUnique", "edit mode");
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var seen = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<string>>();
            foreach (var d in docs)
            {
                if (d == null) continue;
                int so = Mathf.RoundToInt(d.sortingOrder);
                if (!seen.ContainsKey(so))
                    seen[so] = new System.Collections.Generic.List<string>();
                seen[so].Add(d.gameObject.name);
            }
            var dups = new System.Collections.Generic.List<string>();
            foreach (var kv in seen)
            {
                // HUD/TabBar/기타 레거시 0/10은 공용 허용, 50+ 탭들만 고유해야
                if (kv.Key < 50) continue;
                if (kv.Value.Count > 1)
                    dups.Add($"{kv.Key}={string.Join("|", kv.Value)}");
            }
            if (dups.Count > 0)
                return Fail("SortingOrderUnique",
                    $"중복 sortingOrder: {string.Join(", ", dups)}",
                    InvariantResult.Severity.Warning);
            return Pass("SortingOrderUnique", $"{seen.Count} distinct orders");
        }

        /// <summary>ToastUI 런타임 설정 건강성. UIDocument + sortingOrder + root 가시성.</summary>
        static InvariantResult Check_ToastUIRuntimeHealth()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return Skip("ToastUIRuntimeHealth", "scene not loaded");

            var toasts = Object.FindObjectsByType<MkLike.UI.ToastUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (toasts.Length == 0)
                return Fail("ToastUIRuntimeHealth",
                    "ToastUI 씬 누락 — HudPanel.ShowToast() 호출이 침묵",
                    InvariantResult.Severity.Critical);
            if (toasts.Length > 1)
                return Fail("ToastUIRuntimeHealth",
                    $"중복 {toasts.Length}개",
                    InvariantResult.Severity.Warning);

            var doc = toasts[0].GetComponent<UIDocument>();
            if (doc == null)
                return Fail("ToastUIRuntimeHealth",
                    "ToastUI에 UIDocument 없음",
                    InvariantResult.Severity.Critical);
            if (doc.sortingOrder < 100)
                return Fail("ToastUIRuntimeHealth",
                    $"ToastUI sortingOrder={doc.sortingOrder} — HUD(0)/팝업 뒤로 숨을 수 있음. 권장: >=100",
                    InvariantResult.Severity.Warning);
            return Pass("ToastUIRuntimeHealth", $"sortingOrder={doc.sortingOrder}");
        }

        /// <summary>주요 영구 싱글턴들이 null이 아닌지 검증. 초기화 누락/파괴 감지.</summary>
        static InvariantResult Check_PersistentSingletonsNotNull()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("PersistentSingletonsNotNull", "edit mode");

            var missing = new List<string>();
            string[] expected =
            {
                "MkLike.Core.Save.SaveManager",
                "MkLike.Economy.CurrencyManager",
                "MkLike.Combat.LevelSystem",
                "MkLike.Combat.SkillSystem",
                "MkLike.Combat.StageManager",
                "MkLike.Quest.QuestManager",
                "MkLike.Growth.JobSystem",
                "MkLike.Quest.BattlePassSystem",
                "MkLike.Dungeon.DungeonManager",
                "MkLike.Dungeon.BossRaidSystem",
            };
            foreach (var typeName in expected)
            {
                System.Type t = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(typeName);
                    if (t != null) break;
                }
                if (t == null) { missing.Add($"{typeName}(type?)"); continue; }
                var prop = t.GetProperty("Instance");
                if (prop == null) { missing.Add($"{typeName}(noInstance)"); continue; }
                var inst = prop.GetValue(null);
                if (inst == null) missing.Add(typeName);
            }

            if (missing.Count > 0)
                return Fail("PersistentSingletonsNotNull",
                    $"null 싱글턴: {string.Join(", ", missing)}",
                    InvariantResult.Severity.Critical);
            return Pass("PersistentSingletonsNotNull", $"{expected.Length} singletons OK");
        }

        /// <summary>BossRaidSystem 주간 사용 횟수가 SaveData.dungeon.bossRaidWeeklyAttemptsUsed와 동기화되어 있는지 검증.</summary>
        static InvariantResult Check_BossRaidSaveSync()
        {
            if (!UnityEngine.Application.isPlaying) return Skip("BossRaidSaveSync", "edit mode");

            var brs = MkLike.Dungeon.BossRaidSystem.Instance;
            if (brs == null) return Skip("BossRaidSaveSync", "BossRaidSystem.Instance null");

            var sm = MkLike.Core.Save.SaveManager.Instance;
            if (sm == null || sm.CurrentData == null || sm.CurrentData.dungeon == null)
                return Skip("BossRaidSaveSync", "SaveManager/CurrentData/dungeon null");

            // 실시간 증분 중엔 일시적 불일치 가능. OnBeforeSave 호출 후의 sync만 검증.
            // 따라서 여기서는 save 값이 >= runtime 값을 벗어나지 않는지만 체크
            int runtime = brs.WeeklyAttemptsUsed;
            int saved = sm.CurrentData.dungeon.bossRaidWeeklyAttemptsUsed;
            if (saved > runtime + 1)
                return Fail("BossRaidSaveSync",
                    $"save({saved}) >> runtime({runtime}) — 비정상 상태",
                    InvariantResult.Severity.Warning);
            if (runtime < 0 || saved < 0)
                return Fail("BossRaidSaveSync",
                    $"음수값 runtime={runtime} saved={saved}",
                    InvariantResult.Severity.Critical);
            return Pass("BossRaidSaveSync", $"runtime={runtime} saved={saved}");
        }

        // ──────────────────────────────────────────
        //  helpers
        // ──────────────────────────────────────────

        static InvariantResult Pass(string name, string msg) =>
            new() { Name = name, Pass = true, Message = msg, Level = InvariantResult.Severity.Info };
        static InvariantResult Fail(string name, string msg, InvariantResult.Severity sev) =>
            new() { Name = name, Pass = false, Message = msg, Level = sev };
        static InvariantResult Skip(string name, string msg) =>
            new() { Name = name, Pass = true, Message = $"(skip) {msg}", Level = InvariantResult.Severity.Info };

        /// <summary>리포트용 요약 문자열.</summary>
        public static string FormatReport(List<InvariantResult> results)
        {
            var sb = new System.Text.StringBuilder();
            int pass = results.Count(r => r.Pass);
            sb.AppendLine($"Invariants: {pass}/{results.Count} pass");
            foreach (var r in results)
            {
                string sym = r.Pass ? "✅" : (r.Level == InvariantResult.Severity.Critical ? "🔴" : "🟡");
                sb.AppendLine($"  {sym} {r.Name}: {r.Message}");
            }
            return sb.ToString();
        }
    }
}
#endif
