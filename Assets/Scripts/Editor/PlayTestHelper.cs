using UnityEditor;
using UnityEngine;
using MkLike.Quest;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Equipment;
using MkLike.Combat;
using MkLike.Utils;
using System.IO;

namespace MkLike.Editor
{
    /// <summary>
    /// Play 모드 테스트 헬퍼 통합.
    /// MCP execute_menu_item으로 호출 가능.
    /// </summary>
    public static class PlayTestHelper
    {
        // ════════════════════════════════════════
        //  탭 조작
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Tab/Open Tab 0 (소환)", false, 100)]
        public static void OpenTab0() => OpenTab(0);

        [MenuItem("mkLike/Test/Tab/Open Tab 1 (캐릭터)", false, 101)]
        public static void OpenTab1() => OpenTab(1);

        [MenuItem("mkLike/Test/Tab/Open Tab 2 (장비)", false, 102)]
        public static void OpenTab2() => OpenTab(2);

        [MenuItem("mkLike/Test/Tab/Open Tab 3 (스킬)", false, 103)]
        public static void OpenTab3() => OpenTab(3);

        [MenuItem("mkLike/Test/Tab/Open Tab 4 (무기)", false, 104)]
        public static void OpenTab4() => OpenTab(4);

        [MenuItem("mkLike/Test/Tab/Close All Tabs", false, 120)]
        public static void CloseAllTabs()
        {
            var tabBar = Object.FindFirstObjectByType<UI.TabBarUI>();
            if (tabBar != null) tabBar.CloseAll();
            else Debug.LogWarning("[PlayTest] TabBarUI not found");
        }

        public static void OpenTab(int index)
        {
            if (!RequirePlayMode()) return;

            var tabBar = Object.FindFirstObjectByType<UI.TabBarUI>();
            if (tabBar != null)
            {
                tabBar.SelectTab(index);
                Log($"[PlayTest] Tab {index} opened, panel active={tabBar.IsAnyPanelOpen}");
            }
            else
            {
                Log("[PlayTest] TabBarUI not found");
            }
        }

        // ════════════════════════════════════════
        //  퀵 패널 토글
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Panel/Toggle Dungeon", false, 130)]
        public static void ToggleDungeon() => ToggleQuickPanel("[UITK] Dungeon");

        [MenuItem("mkLike/Test/Panel/Toggle Shop", false, 131)]
        public static void ToggleShop() => ToggleQuickPanel("[UITK] Shop");

        [MenuItem("mkLike/Test/Panel/Toggle CollectionBook", false, 132)]
        public static void ToggleCollection() => ToggleQuickPanel("[UITK] CollectionBook");

        [MenuItem("mkLike/Test/Panel/Toggle BattlePass", false, 133)]
        public static void ToggleBattlePass() => ToggleQuickPanel("[UITK] BattlePass");

        [MenuItem("mkLike/Test/Panel/Toggle EliteSummon", false, 134)]
        public static void ToggleElite() => ToggleQuickPanel("[UITK] Popup_EliteSummon");

        [MenuItem("mkLike/Test/Panel/Toggle Costume", false, 135)]
        public static void ToggleCostume() => ToggleQuickPanel("[UITK] Costume");

        public static void ToggleQuickPanel(string goName)
        {
            if (!RequirePlayMode()) return;
            var docs = Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (doc.gameObject.name == goName)
                {
                    bool next = !doc.gameObject.activeSelf;
                    doc.gameObject.SetActive(next);
                    Log($"[PlayTest] {goName} → {(next ? "ON" : "OFF")}");
                    return;
                }
            }
            Log($"[PlayTest] {goName} not found");
        }

        // ════════════════════════════════════════
        //  스폰 / 세이브
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Spawn Boss", false, 200)]
        public static void SpawnBoss()
        {
            if (!RequirePlayMode()) return;

            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            if (spawner != null)
            {
                spawner.SpawnChapterBoss(10);
                Debug.Log("[PlayTest] 챕터 보스 스폰 (Floor 10)");
            }
            else
            {
                Debug.LogWarning("[PlayTest] MonsterSpawner not found");
            }
        }

        /// <summary>
        /// 특정 챕터의 보스 스테이지(10)로 이동 후 보스 스폰.
        /// chapter: 1~8
        /// </summary>
        public static void SpawnChapterBoss(int chapter)
        {
            if (!RequirePlayMode()) return;

            var stageManager = Object.FindFirstObjectByType<StageManager>();
            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            if (stageManager == null || spawner == null)
            {
                Debug.LogWarning("[PlayTest] StageManager 또는 MonsterSpawner 없음");
                return;
            }

            stageManager.StartStage(chapter, 10);
            spawner.SpawnChapterBoss(chapter * 10);
            Debug.Log($"[PlayTest] 챕터 {chapter} 보스 스폰");
        }

        [MenuItem("mkLike/Test/Boss/Chapter 1", false, 201)]
        public static void SpawnBossChapter1() => SpawnChapterBoss(1);
        [MenuItem("mkLike/Test/Boss/Chapter 2", false, 202)]
        public static void SpawnBossChapter2() => SpawnChapterBoss(2);
        [MenuItem("mkLike/Test/Boss/Chapter 3", false, 203)]
        public static void SpawnBossChapter3() => SpawnChapterBoss(3);
        [MenuItem("mkLike/Test/Boss/Chapter 4", false, 204)]
        public static void SpawnBossChapter4() => SpawnChapterBoss(4);
        [MenuItem("mkLike/Test/Boss/Chapter 5", false, 205)]
        public static void SpawnBossChapter5() => SpawnChapterBoss(5);
        [MenuItem("mkLike/Test/Boss/Chapter 6", false, 206)]
        public static void SpawnBossChapter6() => SpawnChapterBoss(6);
        [MenuItem("mkLike/Test/Boss/Chapter 7", false, 207)]
        public static void SpawnBossChapter7() => SpawnChapterBoss(7);
        [MenuItem("mkLike/Test/Boss/Chapter 8", false, 208)]
        public static void SpawnBossChapter8() => SpawnChapterBoss(8);

        [MenuItem("mkLike/Test/Reset Save (신규 게임)", false, 210)]
        public static void ResetSave()
        {
            if (!RequirePlayMode()) return;

            var saveManager = Object.FindFirstObjectByType<SaveManager>();
            if (saveManager != null)
            {
                saveManager.DeleteSave();
                Debug.Log("[PlayTest] 세이브 초기화 — 다시 Play해주세요");
            }
            else
            {
                Debug.LogWarning("[PlayTest] SaveManager not found");
            }
        }

        // ════════════════════════════════════════
        //  가이드 퀘스트 — 상태 조회
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Guide/Status (현재 상태)", false, 300)]
        public static void GuideStatus()
        {
            if (!RequirePlayMode()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[PlayTest] QuestManager not found"); return; }

            var data = qm.GetCurrentGuideData();
            var progress = qm.GetCurrentGuideProgress();

            if (data == null)
            {
                Log($"[Guide] 모든 가이드 퀘스트 완료 (completedIndex={qm.CompletedGuideIndex})");
                return;
            }

            var tm = TutorialManager.Instance;
            int step = tm != null ? (int)tm.CurrentStep : -1;

            Log($"[Guide] #{data.guideChainIndex + 1} \"{data.displayName}\" | " +
                      $"조건: {data.condition} {progress?.currentAmount ?? 0}/{data.requiredAmount} | " +
                      $"완료: {progress?.isCompleted ?? false} | 수령: {progress?.isRewardClaimed ?? false} | " +
                      $"TutorialStep: {step}");
        }

        // ════════════════════════════════════════
        //  가이드 퀘스트 — 보상 수령
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Guide/Claim Reward (보상 수령)", false, 310)]
        public static void ClaimGuideReward()
        {
            if (!RequirePlayMode()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[PlayTest] QuestManager not found"); return; }

            var data = qm.GetCurrentGuideData();
            var progress = qm.GetCurrentGuideProgress();

            if (data == null) { Log("[Guide] 활성 가이드 퀘스트 없음"); return; }

            Log($"[Guide] 현재: #{data.guideChainIndex + 1} \"{data.displayName}\" " +
                      $"진행 {progress?.currentAmount ?? 0}/{data.requiredAmount} " +
                      $"완료={progress?.isCompleted ?? false}");

            if (progress != null && progress.isCompleted && !progress.isRewardClaimed)
            {
                bool ok = qm.ClaimGuideRewardAndAdvance();
                Log($"[Guide] 보상 수령 {(ok ? "성공" : "실패")}");

                var next = qm.GetCurrentGuideData();
                if (next != null)
                    Log($"[Guide] 다음: #{next.guideChainIndex + 1} \"{next.displayName}\"");
                else
                    Log("[Guide] 모든 가이드 퀘스트 완료!");
            }
            else
            {
                Log("[Guide] 보상 수령 불가 (미완료 또는 이미 수령)");
            }
        }

        // ════════════════════════════════════════
        //  가이드 퀘스트 — 현재 퀘스트 강제 완료
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Guide/Force Complete Current (강제 완료)", false, 320)]
        public static void ForceCompleteCurrent()
        {
            if (!RequirePlayMode()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[PlayTest] QuestManager not found"); return; }

            var data = qm.GetCurrentGuideData();
            if (data == null) { Log("[Guide] 활성 가이드 퀘스트 없음"); return; }

            SimulateCondition(data.condition, data.requiredAmount);

            var progress = qm.GetCurrentGuideProgress();
            Log($"[Guide] #{data.guideChainIndex + 1} \"{data.displayName}\" — " +
                      $"시뮬레이션 후 {progress?.currentAmount ?? 0}/{data.requiredAmount} " +
                      $"완료={progress?.isCompleted ?? false}");
        }

        // ════════════════════════════════════════
        //  가이드 퀘스트 — 강제 완료 + 수령 (한 번에)
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Guide/Force Complete + Claim (완료+수령)", false, 330)]
        public static void ForceCompleteAndClaim()
        {
            if (!RequirePlayMode()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[PlayTest] QuestManager not found"); return; }

            var data = qm.GetCurrentGuideData();
            if (data == null) { Log("[Guide] 활성 가이드 퀘스트 없음"); return; }

            // 시뮬레이션
            SimulateCondition(data.condition, data.requiredAmount);

            // 수령
            var progress = qm.GetCurrentGuideProgress();
            if (progress != null && progress.isCompleted)
            {
                bool ok = qm.ClaimGuideRewardAndAdvance();
                string tabs = GetTabUnlockStatus();
                Log($"[Guide] #{data.guideChainIndex + 1} \"{data.displayName}\" — " +
                          $"완료+수령 {(ok ? "✅" : "❌")} | {tabs}");

                var next = qm.GetCurrentGuideData();
                if (next != null)
                    Log($"[Guide] 다음: #{next.guideChainIndex + 1} \"{next.displayName}\" ({next.condition} x{next.requiredAmount})");
            }
            else
            {
                Log($"[Guide] #{data.guideChainIndex + 1} 시뮬레이션 후에도 미완료");
            }
        }

        // ════════════════════════════════════════
        //  가이드 퀘스트 — N까지 자동 진행
        // ════════════════════════════════════════

        [MenuItem("mkLike/Test/Guide/Auto Run to 5", false, 340)]
        public static void AutoRunTo5() => AutoRunToIndex(5);

        [MenuItem("mkLike/Test/Guide/Auto Run to 10", false, 341)]
        public static void AutoRunTo10() => AutoRunToIndex(10);

        [MenuItem("mkLike/Test/Guide/Auto Run to 20", false, 342)]
        public static void AutoRunTo20() => AutoRunToIndex(20);

        [MenuItem("mkLike/Test/Guide/Auto Run ALL", false, 350)]
        public static void AutoRunAll() => AutoRunToIndex(999);

        /// <summary>가이드 퀘스트를 targetIndex번까지 자동 진행 (1-based)</summary>
        public static void AutoRunToIndex(int targetIndex)
        {
            if (!RequirePlayMode()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[PlayTest] QuestManager not found"); return; }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"═══════ 가이드 퀘스트 자동 진행 (→ #{targetIndex}) ═══════");

            int passed = 0;
            int failed = 0;

            for (int i = 0; i < targetIndex; i++)
            {
                var data = qm.GetCurrentGuideData();
                if (data == null)
                {
                    log.AppendLine($"[완료] 모든 가이드 퀘스트 완료 (#{i}에서 종료)");
                    break;
                }

                string questName = $"#{data.guideChainIndex + 1} \"{data.displayName}\" ({data.condition} x{data.requiredAmount})";

                // 이미 완료 → 수령만
                var progress = qm.GetCurrentGuideProgress();
                if (progress != null && progress.isCompleted && !progress.isRewardClaimed)
                {
                    qm.ClaimGuideRewardAndAdvance();
                    string tabs = GetTabUnlockStatus();
                    log.AppendLine($"  ✅ {questName} — 이미 완료, 수령 | {tabs}");
                    passed++;
                    continue;
                }

                // 시뮬레이션
                bool simulated = SimulateCondition(data.condition, data.requiredAmount);
                if (!simulated)
                {
                    log.AppendLine($"  ❌ {questName} — 시뮬레이션 불가");
                    failed++;
                    break;
                }

                // 진행도 재확인
                progress = qm.GetCurrentGuideProgress();
                if (progress == null || !progress.isCompleted)
                {
                    log.AppendLine($"  ❌ {questName} — 미완료 ({progress?.currentAmount ?? 0}/{data.requiredAmount})");
                    failed++;
                    break;
                }

                // 수령
                bool claimed = qm.ClaimGuideRewardAndAdvance();
                if (claimed)
                {
                    string tabs = GetTabUnlockStatus();
                    log.AppendLine($"  ✅ {questName} — 완료+수령 | {tabs}");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  ❌ {questName} — 수령 실패");
                    failed++;
                    break;
                }
            }

            log.AppendLine();
            log.AppendLine($"═══════ 결과: {passed} 성공, {failed} 실패 ═══════");

            // 파일 저장 + 콘솔 + 로그파일
            string result = log.ToString();
            string path = LOG_PATH;
            ClearLog();
            Log(result);
        }

        // ════════════════════════════════════════
        //  조건 시뮬레이션
        // ════════════════════════════════════════

        /// <summary>퀘스트 조건을 이벤트로 시뮬레이션</summary>
        public static bool SimulateCondition(QuestCondition condition, int amount)
        {
            switch (condition)
            {
                case QuestCondition.KillMonsters:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new MonsterDiedEvent());
                    return true;

                case QuestCondition.LevelUp:
                    EventBus.Publish(new LevelUpEvent
                    {
                        PreviousLevel = amount - 1,
                        CurrentLevel = amount
                    });
                    ForceSetProgress(QuestCondition.LevelUp, amount);
                    return true;

                case QuestCondition.ClearStage:
                    // 스테이지 이벤트 발행 (QuestManager.OnStageChanged 경유)
                    int chapter = amount / 10 + 1;
                    int stageIdx = amount % 10;
                    EventBus.Publish(new StageChangedEvent
                    {
                        Chapter = chapter,
                        StageIndex = stageIdx,
                        DisplayName = $"{chapter}-{stageIdx}"
                    });
                    ForceSetProgress(QuestCondition.ClearStage, amount);
                    return true;

                case QuestCondition.AllocateStats:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new StatAllocatedEvent { StatName = "atk", PointsSpent = 1 });
                    return true;

                case QuestCondition.GachaPull:
                    string[] weaponIds = { "weapon_steel_sword", "weapon_flame_blade", "weapon_ice_bow",
                        "weapon_dragon_blade", "weapon_ancient_staff", "weapon_storm_staff" };
                    string[] grades = { "Normal", "Rare", "Epic", "Legendary" };
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new GachaResultEvent
                        {
                            PoolName = "Weapon",
                            Grade = grades[i % grades.Length],
                            ItemId = weaponIds[i % weaponIds.Length]
                        });
                    return true;

                case QuestCondition.EquipItem:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new EquipmentChangedEvent
                        {
                            Slot = EquipmentSlot.Weapon,
                            IsEquipped = true
                        });
                    return true;

                case QuestCondition.EnhanceEquipment:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new EquipmentChangedEvent
                        {
                            Slot = EquipmentSlot.Weapon,
                            IsEquipped = true
                        });
                    return true;

                case QuestCondition.ClearDungeon:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new DungeonCompletedEvent());
                    return true;

                case QuestCondition.SpendGold:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new CurrencyChangedEvent
                        {
                            Type = CurrencyType.Gold,
                            PreviousAmount = 1000,
                            CurrentAmount = 999
                        });
                    return true;

                case QuestCondition.SkillLevelUp:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new SkillLevelUpEvent());
                    return true;

                case QuestCondition.ReceiveOfflineReward:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new OfflineRewardClaimedEvent());
                    return true;

                case QuestCondition.ReceiveBlessing:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new BlessingChangedEvent());
                    return true;

                case QuestCondition.ReceiveAttendance:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new AttendanceCheckedEvent());
                    return true;

                case QuestCondition.SkillUse:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new SkillUsedEvent());
                    return true;

                case QuestCondition.JobAdvance:
                    ForceSetProgress(QuestCondition.JobAdvance, amount);
                    return true;

                case QuestCondition.QuestComplete:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new QuestCompletedEvent
                        {
                            QuestId = $"test_{i}",
                            QuestType = QuestType.Daily
                        });
                    return true;

                case QuestCondition.ArenaMatch:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new ArenaMatchEvent());
                    return true;

                case QuestCondition.TowerFloorClear:
                    ForceSetProgress(QuestCondition.TowerFloorClear, amount);
                    return true;

                case QuestCondition.AchievementClear:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new Core.AchievementCompletedEvent());
                    return true;

                case QuestCondition.CollectionCount:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new CollectionEntryRegisteredEvent());
                    return true;

                case QuestCondition.CostumeSetComplete:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new CostumeSetCompletedEvent());
                    return true;

                // QuestCondition.RelicEquip: 2026-04-20 유물 시스템 완전 제거

                case QuestCondition.QuickHunt:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new QuickHuntCompletedEvent { TicketsUsed = 1, GoldEarned = 5000, ExpEarned = 2000 });
                    return true;

                case QuestCondition.HeroPowerMilestone:
                    EventBus.Publish(new HeroPowerMilestoneEvent
                    {
                        MilestoneIndex = amount - 1,
                        RequiredCp = 5000,
                        BonusStat = StatType.Atk,
                        BonusValue = 2f,
                        TotalMilestones = amount
                    });
                    ForceSetProgress(QuestCondition.HeroPowerMilestone, amount);
                    return true;

                case QuestCondition.UseBooster:
                    for (int i = 0; i < amount; i++)
                        EventBus.Publish(new BoosterUsedEvent
                        {
                            BoosterId = "booster_exp",
                            Type = BoosterType.ExpBoost,
                            Multiplier = 2f,
                            DurationSeconds = 1800f
                        });
                    return true;

                case QuestCondition.UnlockAbility:
                    EventBus.Publish(new AbilityUnlockedEvent
                    {
                        NodeId = "sim_ability",
                        BranchIndex = 0,
                        TotalUnlocked = amount
                    });
                    ForceSetProgress(QuestCondition.UnlockAbility, amount);
                    return true;

                case QuestCondition.StarForceReach:
                    EventBus.Publish(new StarForceEvent
                    {
                        InstanceId = "sim_slot",
                        IsSuccess = true,
                        NewStarForce = amount,
                        Result = StarForceResult.Success
                    });
                    ForceSetProgress(QuestCondition.StarForceReach, amount);
                    return true;

                case QuestCondition.EquipArtifact:
                    EventBus.Publish(new ArtifactEquippedEvent
                    {
                        ArtifactId = "sim_artifact",
                        SlotIndex = 0,
                        TotalEquipped = amount
                    });
                    ForceSetProgress(QuestCondition.EquipArtifact, amount);
                    return true;

                case QuestCondition.PotentialSet:
                    EventBus.Publish(new PotentialChangedEvent
                    {
                        Slot = EquipmentSlot.Weapon,
                        NewGrade = PotentialGrade.Rare,
                        Result = "Upgraded",
                        TotalPotentialSets = amount
                    });
                    ForceSetProgress(QuestCondition.PotentialSet, amount);
                    return true;

                case QuestCondition.WeaponGacha:
                    for (int w = 0; w < amount; w++)
                    {
                        EventBus.Publish(new GachaResultEvent
                        {
                            ItemId = "test_weapon",
                            Grade = "Epic",
                            PoolName = "Weapon"
                        });
                    }
                    ForceSetProgress(QuestCondition.WeaponGacha, amount);
                    return true;

                case QuestCondition.CostumeEquip:
                    EventBus.Publish(new CostumeEquippedEvent
                    {
                        CostumeId = "test_costume",
                        TotalEquipped = amount
                    });
                    ForceSetProgress(QuestCondition.CostumeEquip, amount);
                    return true;

                case QuestCondition.StarGradeEnhance:
                    // 성급 강화 — ScrollEnhanceEvent로 시뮬
                    EventBus.Publish(new ScrollEnhanceEvent
                    {
                        InstanceId = "test_slot",
                        IsSuccess = true,
                        NewScrollLevel = amount
                    });
                    ForceSetProgress(QuestCondition.StarGradeEnhance, amount);
                    return true;

                case QuestCondition.ClimberPower:
                    EventBus.Publish(new ClimberPowerUpEvent { NewLevel = amount });
                    ForceSetProgress(QuestCondition.ClimberPower, amount);
                    return true;

                case QuestCondition.ArenaTierReach:
                    // 아레나 티어 달성 시뮬: ArenaMatchEvent로 티어 값 전달
                    EventBus.Publish(new ArenaMatchEvent
                    {
                        IsVictory = true,
                        ArenaRank = amount - 1 // tierIndex = amount - 1
                    });
                    return true;

                case QuestCondition.GuildJoin:
                    EventBus.Publish(new GuildJoinedEvent { GuildName = "테스트길드" });
                    return true;

                // 신규 14종 순환 조건 — ForceSetProgress로 시뮬
                case QuestCondition.EliteSummon:
                    for (int es = 0; es < amount; es++)
                        EventBus.Publish(new GachaResultEvent { ItemId = "elite_test", Grade = "Legendary", PoolName = "Elite" });
                    ForceSetProgress(QuestCondition.EliteSummon, amount);
                    return true;

                case QuestCondition.BossRaidEntry:
                    ForceSetProgress(QuestCondition.BossRaidEntry, amount);
                    return true;

                case QuestCondition.WeaponSummonLevelReach:
                    ForceSetProgress(QuestCondition.WeaponSummonLevelReach, amount);
                    return true;

                case QuestCondition.EliteSummonLevelReach:
                    ForceSetProgress(QuestCondition.EliteSummonLevelReach, amount);
                    return true;

                // 서버 의존 시스템 — ForceSetProgress로 우회 (더미)
                case QuestCondition.CollectionRate:
                    ForceSetProgress(condition, amount);
                    return true;

                default:
                    Debug.LogWarning($"[PlayTest] 미지원 조건: {condition}");
                    return false;
            }
        }

        /// <summary>SetProgress 방식 퀘스트의 진행도를 리플렉션으로 직접 설정</summary>
        private static void ForceSetProgress(QuestCondition condition, int value)
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;

            // 1차: 리플렉션으로 SetProgress 호출 (일반 퀘스트)
            var method = typeof(QuestManager).GetMethod("SetProgress",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
                method.Invoke(qm, new object[] { condition, value });

            // 2차: 현재 가이드 퀘스트의 진행도를 직접 확인/설정
            var guideData = qm.GetCurrentGuideData();
            if (guideData != null && guideData.condition == condition)
            {
                var progress = qm.GetCurrentGuideProgress();
                if (progress != null && !progress.isCompleted)
                {
                    if (value > progress.currentAmount)
                    {
                        progress.currentAmount = value;
                        if (progress.currentAmount >= guideData.requiredAmount)
                        {
                            progress.currentAmount = guideData.requiredAmount;
                            progress.isCompleted = true;
                            Debug.Log($"[PlayTest] 가이드 퀘스트 직접 완료: {guideData.displayName}");
                        }
                    }
                }
            }
        }

        /// <summary>현재 탭 해금 상태를 문자열로</summary>
        private static string GetTabUnlockStatus()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return "QM없음";

            int idx = qm.CompletedGuideIndex;
            string[] tabs = { "소환", "캐릭터", "장비", "스킬", "무기" };
            int[] required = { 4, -1, 5, 9, 14 };

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tabs.Length; i++)
            {
                bool open = required[i] < 0 || idx >= required[i];
                sb.Append(open ? $"[{tabs[i]}]" : $" {tabs[i]} ");
            }
            return sb.ToString();
        }

        private static bool RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                Log("[PlayTest] Play 모드에서만 사용 가능");
                return false;
            }
            return true;
        }

        // ════════════════════════════════════════
        //  로그 — 콘솔 + 파일 동시 기록
        // ════════════════════════════════════════

        private const string LOG_PATH = "Assets/playtest_log.txt";

        /// <summary>콘솔 + 파일에 동시 기록. MCP read_console이 안 될 때 파일로 확인 가능.</summary>
        public static void Log(string msg)
        {
            Debug.Log(msg);
            try
            {
                File.AppendAllText(LOG_PATH,
                    $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n",
                    System.Text.Encoding.UTF8);
            }
            catch { /* 에디터 모드에서 파일 쓰기 실패 시 무시 */ }
        }

        /// <summary>로그 파일 초기화</summary>
        public static void ClearLog()
        {
            try { File.WriteAllText(LOG_PATH, "", System.Text.Encoding.UTF8); }
            catch { }
        }
    }
}
