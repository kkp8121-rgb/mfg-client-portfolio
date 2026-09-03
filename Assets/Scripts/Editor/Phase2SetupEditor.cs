using UnityEngine;
using UnityEditor;

namespace MkLike.Editor
{
    /// <summary>
    /// Phase 2 전투 루프에 필요한 씬 오브젝트를 자동 생성하는 에디터 도구.
    /// 캐릭터별 특색있는 SPUM 비주얼 + 애니메이션 설정 포함.
    /// </summary>
    public static class Phase2SetupEditor
    {
        // SPUM 프리팹 경로
        private const string SPUM_SWORDMAN = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab";
        private const string SPUM_BOWMAN = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/BowMan.prefab";
        private const string SPUM_MAGICIANMAN = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/MagicianMan.prefab";

        // VFX: 스프라이트 시트 기반 (SpriteSheetVfxSO). 에셋 추가 후 스킬 SO에서 수동 할당.

        public static void SetupAll()
        {
            // VFX SO 자동 생성 (스킬 SO보다 먼저)
            SlashVfxSetupEditor.GenerateSlashVfxSOs();

            FontSetupEditor.SetupKoreanFont();
            SetupLayers();

            var charData = CreateCharacterDataSOs();
            CreateMonsterDataSOs();

            var monsterData = AssetDatabase.LoadAssetAtPath<Data.MonsterDataSO>("Assets/Data/SO/Monster_Bandit.asset");
            if (monsterData == null)
                monsterData = AssetDatabase.LoadAssetAtPath<Data.MonsterDataSO>("Assets/Data/SO/Monster_Slime.asset");

            CreateMonsterPrefabs();
            var monsterPrefab = monsterData != null ? monsterData.prefab : null;
            if (monsterPrefab == null)
                monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/Monster_Bandit.prefab");

            var playerObj = CreatePlayerInScene(charData);
            var arenaMap = EnsureArenaMap();
            EnsureCamera();
            CreateGachaPoolAssets();
            CreateEquipmentDataSOs();
            CreateArcherCharacterDataSO();
            CreateMageCharacterDataSO();
            CreateWeaponDataSOs();
            CreateDungeonDataSOs();
            CreateQuestDataSOs();
            // CreateRelicDataSOs: 2026-04-20 유물 시스템 완전 제거
            SetupPhase2Managers(playerObj, monsterData, monsterPrefab, arenaMap);
            SetupHUD(playerObj);
            SetupLootVisuals(playerObj);
            var warriorSkills = CreateWarriorSkills();
            var archerSkills = CreateArcherSkills();
            var mageSkills = CreateMageSkills();
            ConnectJobSkills(charData, warriorSkills, archerSkills, mageSkills);
            CreateDamageTextManager();
            CreateTestManager();
            CreateDebugLogger();
            CreateSceneTransitionManager();
            CreateComboLogHUD();
            CreateCombatFeedHUD();
            CreateCollectionBookUI();
            CreateGuideQuestDataSOs();
            CreateUserCycleWidgets();
            LinkReferences(playerObj, charData);

            Debug.Log("<color=cyan>[Phase2Setup] Phase 2 전체 셋업 완료!</color>");
            Debug.Log("[Phase2Setup] Play 버튼을 눌러 자동 전투를 확인하세요.");
        }

        // ── 1. 레이어 설정 ──

        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );
            SerializedProperty layers = tagManager.FindProperty("layers");

            AddLayerIfMissing(layers, "Player", 6);
            AddLayerIfMissing(layers, "Monster", 7);

            tagManager.ApplyModifiedProperties();

            int playerLayer = LayerMask.NameToLayer("Player");
            int monsterLayer = LayerMask.NameToLayer("Monster");

            if (playerLayer >= 0 && monsterLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, playerLayer, true);
                Physics2D.IgnoreLayerCollision(monsterLayer, monsterLayer, true);
                Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);
            }

            Debug.Log("[Phase2Setup] 레이어 설정 완료 (Player=6, Monster=7, 키네마틱 모드)");
        }

        private static void AddLayerIfMissing(SerializedProperty layers, string layerName, int index)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                    return;
            }

            if (index < layers.arraySize)
            {
                var prop = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = layerName;
                }
                else
                {
                    for (int i = 8; i < layers.arraySize; i++)
                    {
                        if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                        {
                            layers.GetArrayElementAtIndex(i).stringValue = layerName;
                            return;
                        }
                    }
                    Debug.LogWarning($"[Phase2Setup] '{layerName}' 레이어를 추가할 빈 슬롯이 없습니다!");
                }
            }
        }

        // ── 2. ScriptableObject 에셋 생성 ──

        private static Data.CharacterDataSO CreateCharacterDataSOs()
        {
            EnsureDirectory("Assets/Data/SO");

            // 전사
            var warrior = CreateSingleCharacterDataSO(
                path: "Assets/Data/SO/Character_Warrior.asset",
                id: "warrior", displayName: "전사",
                description: "높은 생존력과 안정적 자동 사냥",
                baseHp: 300, baseAtk: 35, baseDef: 10,
                baseCritRate: 0.15f, attackSpeed: 3.0f, moveSpeed: 4.0f,
                hpPerLevel: 25, atkPerLevel: 4, defPerLevel: 2,
                spumPrefab: SPUM_SWORDMAN, attackAnimType: 0,
                visualTint: new Color(0.9f, 0.95f, 1.0f)
            );

            // 궁수
            CreateSingleCharacterDataSO(
                path: "Assets/Data/SO/Character_Archer.asset",
                id: "archer", displayName: "궁수",
                description: "빠른 공격속도와 높은 치명타로 사냥",
                baseHp: 220, baseAtk: 40, baseDef: 6,
                baseCritRate: 0.25f, attackSpeed: 4.0f, moveSpeed: 4.5f,
                hpPerLevel: 18, atkPerLevel: 5, defPerLevel: 1,
                spumPrefab: SPUM_BOWMAN, attackAnimType: 1,
                visualTint: new Color(0.85f, 1.0f, 0.85f)
            );

            // 마법사
            CreateSingleCharacterDataSO(
                path: "Assets/Data/SO/Character_Mage.asset",
                id: "mage", displayName: "마법사",
                description: "강력한 범위 공격과 높은 ATK",
                baseHp: 180, baseAtk: 50, baseDef: 4,
                baseCritRate: 0.20f, attackSpeed: 2.5f, moveSpeed: 3.5f,
                hpPerLevel: 14, atkPerLevel: 6, defPerLevel: 1,
                spumPrefab: SPUM_MAGICIANMAN, attackAnimType: 2,
                visualTint: new Color(0.9f, 0.85f, 1.0f)
            );

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase2Setup] CharacterDataSO 3종(전사/궁수/마법사) 생성/업데이트 완료");
            return warrior;
        }

        private static Data.CharacterDataSO CreateSingleCharacterDataSO(
            string path, string id, string displayName, string description,
            int baseHp, int baseAtk, int baseDef,
            float baseCritRate, float attackSpeed, float moveSpeed,
            int hpPerLevel, int atkPerLevel, int defPerLevel,
            string spumPrefab, int attackAnimType, Color visualTint)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Data.CharacterDataSO>(path);
            if (existing != null)
            {
                existing.id = id;
                existing.displayName = displayName;
                existing.description = description;
                existing.baseHp = baseHp;
                existing.baseAtk = baseAtk;
                existing.baseDef = baseDef;
                existing.baseCritRate = baseCritRate;
                existing.attackSpeed = attackSpeed;
                existing.moveSpeed = moveSpeed;
                existing.hpPerLevel = hpPerLevel;
                existing.atkPerLevel = atkPerLevel;
                existing.defPerLevel = defPerLevel;
                existing.spumPrefabPath = spumPrefab;
                existing.attackAnimType = attackAnimType;
                existing.visualTint = visualTint;
                EditorUtility.SetDirty(existing);
                Debug.Log($"[Phase2Setup] CharacterDataSO({displayName}) 업데이트 완료");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<Data.CharacterDataSO>();
            so.id = id;
            so.displayName = displayName;
            so.description = description;
            so.baseHp = baseHp;
            so.baseAtk = baseAtk;
            so.baseDef = baseDef;
            so.baseCritRate = baseCritRate;
            so.attackSpeed = attackSpeed;
            so.moveSpeed = moveSpeed;
            so.hpPerLevel = hpPerLevel;
            so.atkPerLevel = atkPerLevel;
            so.defPerLevel = defPerLevel;
            so.spumPrefabPath = spumPrefab;
            so.attackAnimType = attackAnimType;
            so.visualTint = visualTint;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[Phase2Setup] CharacterDataSO({displayName}) 생성 완료");
            return so;
        }

        private static void CreateMonsterDataSOs()
        {
            EnsureDirectory("Assets/Data/SO");

            // 1. 도적 (근접 몬스터 - SwordMan 기반)
            CreateMonsterDataSO(
                path: "Assets/Data/SO/Monster_Bandit.asset",
                id: "bandit",
                displayName: "도적",
                hp: 40, atk: 6, def: 2,
                attackSpeed: 1.2f, moveSpeed: 2.5f,
                gold: 8, exp: 4,
                spumPrefab: SPUM_SWORDMAN,
                tint: new Color(0.6f, 0.35f, 0.35f), // 어두운 붉은색
                scale: 0.85f,
                attackAnimType: 0,
                isBoss: false
            );

            // 2. 해골궁수 (원거리 몬스터 - BowMan 기반)
            CreateMonsterDataSO(
                path: "Assets/Data/SO/Monster_SkeletonArcher.asset",
                id: "skeleton_archer",
                displayName: "해골궁수",
                hp: 30, atk: 8, def: 1,
                attackSpeed: 0.8f, moveSpeed: 2.0f,
                gold: 12, exp: 6,
                spumPrefab: SPUM_BOWMAN,
                tint: new Color(0.75f, 0.75f, 0.65f), // 뼈 같은 누런 회색
                scale: 0.9f,
                attackAnimType: 1,
                isBoss: false
            );

            // 3. 다크메이지 (보스 - MagicianMan 기반)
            CreateMonsterDataSO(
                path: "Assets/Data/SO/Monster_DarkMage.asset",
                id: "dark_mage",
                displayName: "다크메이지",
                hp: 200, atk: 15, def: 5,
                attackSpeed: 0.6f, moveSpeed: 1.5f,
                gold: 100, exp: 50,
                spumPrefab: SPUM_MAGICIANMAN,
                tint: new Color(0.5f, 0.3f, 0.7f), // 보라색
                scale: 1.3f,
                attackAnimType: 2,
                isBoss: true
            );

            // 기존 슬라임 SO가 있으면 삭제하지 않고 유지
        }

        private static void CreateMonsterDataSO(
            string path, string id, string displayName,
            int hp, int atk, int def, float attackSpeed, float moveSpeed,
            long gold, long exp, string spumPrefab, Color tint, float scale,
            int attackAnimType, bool isBoss)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Data.MonsterDataSO>(path);
            if (existing != null)
            {
                // 비주얼 필드 업데이트
                existing.spumPrefabPath = spumPrefab;
                existing.visualTint = tint;
                existing.visualScale = scale;
                existing.attackAnimType = attackAnimType;
                EditorUtility.SetDirty(existing);
                Debug.Log($"[Phase2Setup] MonsterDataSO({displayName}) 비주얼 업데이트");
                return;
            }

            var so = ScriptableObject.CreateInstance<Data.MonsterDataSO>();
            so.id = id;
            so.displayName = displayName;
            so.hp = hp;
            so.atk = atk;
            so.def = def;
            so.attackSpeed = attackSpeed;
            so.moveSpeed = moveSpeed;
            so.goldReward = gold;
            so.expReward = exp;
            so.spumPrefabPath = spumPrefab;
            so.visualTint = tint;
            so.visualScale = scale;
            so.attackAnimType = attackAnimType;
            so.isBoss = isBoss;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[Phase2Setup] MonsterDataSO({displayName}) 생성 완료");
        }

        // ── 3. 몬스터 프리팹 생성 ──

        private static void CreateMonsterPrefabs()
        {
            EnsureDirectory("Assets/Prefabs/Combat");

            CreateSingleMonsterPrefab(
                "Assets/Data/SO/Monster_Bandit.asset",
                "Assets/Prefabs/Combat/Monster_Bandit.prefab"
            );
            CreateSingleMonsterPrefab(
                "Assets/Data/SO/Monster_SkeletonArcher.asset",
                "Assets/Prefabs/Combat/Monster_SkeletonArcher.prefab"
            );
            CreateSingleMonsterPrefab(
                "Assets/Data/SO/Monster_DarkMage.asset",
                "Assets/Prefabs/Combat/Monster_DarkMage.prefab"
            );
        }

        private static void CreateSingleMonsterPrefab(string soPath, string prefabPath)
        {
            var monsterSO = AssetDatabase.LoadAssetAtPath<Data.MonsterDataSO>(soPath);
            if (monsterSO == null)
            {
                Debug.LogWarning($"[Phase2Setup] MonsterDataSO를 찾을 수 없음: {soPath}");
                return;
            }

            // 기존 프리팹이 있으면 삭제 후 재생성 (디자인/애니메이션 업데이트 보장)
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Debug.Log($"[Phase2Setup] 기존 몬스터 프리팹 삭제 → 재생성: {prefabPath}");
            }

            var obj = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));

            // SPUM 비주얼 부착 (기본 SwordMan 사용 — 디자인은 별도 적용)
            string spumPath = string.IsNullOrEmpty(monsterSO.spumPrefabPath) ? SPUM_SWORDMAN : monsterSO.spumPrefabPath;
            AttachSPUMVisual(obj, spumPath, 0);

            // JSON 디자인 적용 (에디터 타임에 스프라이트 베이킹)
            var spumChild = obj.transform.Find("SPUMVisual");
            if (spumChild != null)
                CharacterDesignSetupEditor.ApplyDesignById(spumChild.gameObject, monsterSO.id);

            // 물리
            var rb = obj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = obj.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(1f, 1.5f);
            col.offset = new Vector2(0f, 0.1f);

            // 컴포넌트
            obj.AddComponent<Combat.CombatStats>();
            obj.AddComponent<Combat.MonsterCombat>();
            obj.AddComponent<Combat.MonsterController>();

            var animBridge = obj.AddComponent<Combat.CharacterAnimBridge>();

            // CharacterVisual — 런타임에 JSON 디자인 적용
            var charVisual = obj.AddComponent<Combat.CharacterVisual>();

            obj.layer = 7; // Monster

            // MonsterController의 playerLayer 설정
            SetMonsterControllerLayer(obj);

            // CharacterAnimBridge의 attackType 설정
            SetAnimBridgeAttackType(animBridge, monsterSO.attackAnimType);

            // CharacterVisual designId 설정
            SetCharacterVisualDesignId(charVisual, monsterSO.id);

            // 프리팹 저장
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
            Object.DestroyImmediate(obj);

            // SO에 프리팹 링크
            monsterSO.prefab = prefab;
            EditorUtility.SetDirty(monsterSO);

            Debug.Log($"[Phase2Setup] 몬스터 프리팹 생성: {prefabPath}");
        }

        private static void SetMonsterControllerLayer(GameObject obj)
        {
            var controller = obj.GetComponent<Combat.MonsterController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);
            var prop = so.FindProperty("playerLayer");
            if (prop != null)
            {
                prop.intValue = 1 << 6; // Player layer
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetAnimBridgeAttackType(Combat.CharacterAnimBridge bridge, int attackAnimType)
        {
            var so = new SerializedObject(bridge);
            var prop = so.FindProperty("attackType");
            if (prop != null)
            {
                prop.enumValueIndex = attackAnimType;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetCharacterVisualDesignId(Combat.CharacterVisual visual, string designId)
        {
            var so = new SerializedObject(visual);
            var prop = so.FindProperty("designId");
            if (prop != null)
            {
                prop.stringValue = designId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── 4. 플레이어 씬 오브젝트 생성 ──

        private static GameObject CreatePlayerInScene(Data.CharacterDataSO charData)
        {
            var existingPlayer = Object.FindFirstObjectByType<Combat.PlayerCharacter>();
            if (existingPlayer != null)
            {
                // 기존 SPUM 비주얼을 삭제하고 재생성 (디자인/애니메이션 업데이트 보장)
                var oldVisual = existingPlayer.transform.Find("SPUMVisual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

                string spumPath = string.IsNullOrEmpty(charData.spumPrefabPath) ? SPUM_SWORDMAN : charData.spumPrefabPath;
                AttachSPUMVisual(existingPlayer.gameObject, spumPath, 1);

                // JSON 디자인 적용
                var visual = existingPlayer.transform.Find("SPUMVisual");
                if (visual != null)
                    CharacterDesignSetupEditor.ApplyDesignById(visual.gameObject, charData.id);

                if (existingPlayer.GetComponent<Combat.CharacterAnimBridge>() == null)
                    existingPlayer.gameObject.AddComponent<Combat.CharacterAnimBridge>();

                if (existingPlayer.GetComponent<Combat.SkillSystem>() == null)
                    existingPlayer.gameObject.AddComponent<Combat.SkillSystem>();

                SetAnimBridgeAttackType(
                    existingPlayer.GetComponent<Combat.CharacterAnimBridge>(),
                    charData.attackAnimType
                );

                // CharacterVisual designId 설정
                var existingVisual = existingPlayer.GetComponent<Combat.CharacterVisual>();
                if (existingVisual == null)
                    existingVisual = existingPlayer.gameObject.AddComponent<Combat.CharacterVisual>();
                SetCharacterVisualDesignId(existingVisual, charData.id);

                Debug.Log("[Phase2Setup] PlayerCharacter SPUM 비주얼 재생성 완료");
                return existingPlayer.gameObject;
            }

            var obj = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(obj, "Create Player");

            string playerSpumPath = string.IsNullOrEmpty(charData.spumPrefabPath) ? SPUM_SWORDMAN : charData.spumPrefabPath;
            AttachSPUMVisual(obj, playerSpumPath, 1);

            // JSON 디자인 적용 (에디터 타임에 스프라이트 베이킹)
            var playerVisual = obj.transform.Find("SPUMVisual");
            if (playerVisual != null)
                CharacterDesignSetupEditor.ApplyDesignById(playerVisual.gameObject, charData.id);

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = obj.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.8f, 2f);
            col.offset = new Vector2(0f, 0.2f);

            obj.AddComponent<Combat.CombatStats>();
            obj.AddComponent<Combat.CharacterCombat>();
            obj.AddComponent<Combat.PlayerCharacter>();
            var animBridge = obj.AddComponent<Combat.CharacterAnimBridge>();
            obj.AddComponent<Combat.LevelSystem>();
            obj.AddComponent<Combat.SkillSystem>();

            // CharacterVisual — 런타임에 JSON 디자인 적용
            var charVisual = obj.AddComponent<Combat.CharacterVisual>();
            SetCharacterVisualDesignId(charVisual, charData.id);

            obj.layer = 6; // Player

            // 공격 애니메이션 타입 설정
            SetAnimBridgeAttackType(animBridge, charData.attackAnimType);

            // 위치: ArenaMap의 PlayerSpawnPoint 또는 원점
            var arenaMap = Object.FindFirstObjectByType<Combat.ArenaMap>();
            if (arenaMap != null)
                obj.transform.position = (Vector3)arenaMap.GetPlayerSpawnWorld();
            else
                obj.transform.position = Vector3.zero;

            // PlayerCharacter의 monsterLayer + arenaMap 설정
            var playerComp = obj.GetComponent<Combat.PlayerCharacter>();
            var pso = new SerializedObject(playerComp);
            var monsterLayerProp = pso.FindProperty("monsterLayer");
            if (monsterLayerProp != null)
                monsterLayerProp.intValue = 1 << 7;

            if (arenaMap != null)
            {
                var arenaMapProp = pso.FindProperty("arenaMap");
                if (arenaMapProp != null)
                    arenaMapProp.objectReferenceValue = arenaMap;
            }
            pso.ApplyModifiedPropertiesWithoutUndo();

            // LevelSystem 연결
            var levelSys = obj.GetComponent<Combat.LevelSystem>();
            var lso = new SerializedObject(levelSys);
            lso.FindProperty("_combatStats").objectReferenceValue = obj.GetComponent<Combat.CombatStats>();
            lso.FindProperty("_characterData").objectReferenceValue = charData;
            lso.ApplyModifiedPropertiesWithoutUndo();

            // CombatStats 초기화
            var stats = obj.GetComponent<Combat.CombatStats>();
            var sso = new SerializedObject(stats);
            sso.FindProperty("_baseHp").intValue = charData.baseHp;
            sso.FindProperty("_baseAtk").intValue = charData.baseAtk;
            sso.FindProperty("_baseDef").intValue = charData.baseDef;
            sso.FindProperty("_baseCritRate").floatValue = charData.baseCritRate;
            sso.FindProperty("_baseAttackSpeed").floatValue = charData.attackSpeed;
            sso.FindProperty("_baseMoveSpeed").floatValue = charData.moveSpeed;
            sso.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Phase2Setup] Player 씬 오브젝트 생성 완료 (전사 비주얼)");
            return obj;
        }

        // ── 5. Phase 2 매니저 생성 ──

        private static void SetupPhase2Managers(
            GameObject player,
            Data.MonsterDataSO monsterData,
            GameObject monsterPrefab,
            Combat.ArenaMap arenaMap)
        {
            // LootManager
            if (Object.FindFirstObjectByType<Combat.LootManager>() == null)
            {
                var lootObj = new GameObject("LootManager");
                Undo.RegisterCreatedObjectUndo(lootObj, "Create LootManager");
                lootObj.AddComponent<Combat.LootManager>();
                Debug.Log("[Phase2Setup] LootManager 생성");
            }

            // FloatingTextManager
            if (Object.FindFirstObjectByType<Combat.FloatingTextManager>() == null)
            {
                var ftObj = new GameObject("FloatingTextManager");
                Undo.RegisterCreatedObjectUndo(ftObj, "Create FloatingTextManager");
                var ftMgr = ftObj.AddComponent<Combat.FloatingTextManager>();
                if (player != null)
                {
                    var ftSo = new SerializedObject(ftMgr);
                    ftSo.FindProperty("_playerTransform").objectReferenceValue = player.transform;
                    ftSo.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] FloatingTextManager 생성");
            }

            // GachaManager
            if (Object.FindFirstObjectByType<Economy.GachaManager>() == null)
            {
                var gachaObj = new GameObject("GachaManager");
                Undo.RegisterCreatedObjectUndo(gachaObj, "Create GachaManager");
                var gachaMgr = gachaObj.AddComponent<Economy.GachaManager>();

                // 가챠 풀 SO 연결
                var equipmentPool = AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>("Assets/Data/SO/GachaPool_Equipment.asset");
                var weaponPool = AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>("Assets/Data/SO/GachaPool_Weapon.asset");
                // GachaPool_Relic: 2026-04-20 유물 시스템 완전 제거

                var gachaSo = new SerializedObject(gachaMgr);
                if (equipmentPool != null)
                    gachaSo.FindProperty("_equipmentPool").objectReferenceValue = equipmentPool;
                if (weaponPool != null)
                    gachaSo.FindProperty("_weaponPool").objectReferenceValue = weaponPool;
                gachaSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Phase2Setup] GachaManager 생성");
            }

            // EquipmentManager
            if (Object.FindFirstObjectByType<Equipment.EquipmentManager>() == null)
            {
                var equipObj = new GameObject("EquipmentManager");
                Undo.RegisterCreatedObjectUndo(equipObj, "Create EquipmentManager");
                var equipMgr = equipObj.AddComponent<Equipment.EquipmentManager>();
                ConnectCatalog<Data.EquipmentDataSO>(equipMgr, "_catalog", "Assets/Data/SO/Equipment");
                Debug.Log("[Phase2Setup] EquipmentManager 생성");
            }

            // MonsterSpawner
            Combat.MonsterSpawner spawner = Object.FindFirstObjectByType<Combat.MonsterSpawner>();
            if (spawner == null)
            {
                var spawnerObj = new GameObject("MonsterSpawner");
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create MonsterSpawner");
                spawner = spawnerObj.AddComponent<Combat.MonsterSpawner>();
            }

            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_monsterData").objectReferenceValue = monsterData;
            spawnerSo.FindProperty("_spawnInterval").floatValue = 3f;
            spawnerSo.FindProperty("_spawnBatchSize").intValue = 5;
            spawnerSo.FindProperty("_maxAliveCount").intValue = 40;
            spawnerSo.FindProperty("_poolInitialSize").intValue = 50;
            if (arenaMap != null)
                spawnerSo.FindProperty("_arenaMap").objectReferenceValue = arenaMap;

            // 보스 데이터 설정
            var bossData = AssetDatabase.LoadAssetAtPath<Data.MonsterDataSO>("Assets/Data/SO/Monster_DarkMage.asset");
            if (bossData != null)
                spawnerSo.FindProperty("_bossData").objectReferenceValue = bossData;

            spawnerSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Phase2Setup] MonsterSpawner 설정 완료");

            // SpumCharacterManager
            Combat.SpumCharacterManager spumMgr = Object.FindFirstObjectByType<Combat.SpumCharacterManager>();
            if (spumMgr == null)
            {
                var spumObj = new GameObject("SpumCharacterManager");
                Undo.RegisterCreatedObjectUndo(spumObj, "Create SpumCharacterManager");
                spumMgr = spumObj.AddComponent<Combat.SpumCharacterManager>();
                Debug.Log("[Phase2Setup] SpumCharacterManager 생성");
            }

            // CharacterPreviewRenderer
            UI.CharacterPreviewRenderer previewRenderer = Object.FindFirstObjectByType<UI.CharacterPreviewRenderer>();
            if (previewRenderer == null)
            {
                var previewObj = new GameObject("CharacterPreviewRenderer");
                Undo.RegisterCreatedObjectUndo(previewObj, "Create CharacterPreviewRenderer");
                previewRenderer = previewObj.AddComponent<UI.CharacterPreviewRenderer>();
                Debug.Log("[Phase2Setup] CharacterPreviewRenderer 생성");
            }

            // StageManager
            Combat.StageManager stageMgr = Object.FindFirstObjectByType<Combat.StageManager>();
            if (stageMgr == null)
            {
                var stageObj = new GameObject("StageManager");
                Undo.RegisterCreatedObjectUndo(stageObj, "Create StageManager");
                stageMgr = stageObj.AddComponent<Combat.StageManager>();
            }

            var stageSo = new SerializedObject(stageMgr);
            stageSo.FindProperty("_monsterSpawner").objectReferenceValue = spawner;
            stageSo.FindProperty("_player").objectReferenceValue = player.GetComponent<Combat.PlayerCharacter>();
            if (arenaMap != null)
                stageSo.FindProperty("_arenaMap").objectReferenceValue = arenaMap;
            stageSo.FindProperty("_monstersPerStage").intValue = 100;
            stageSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Phase2Setup] StageManager 설정 완료");

            // CameraController 연결
            var camCtrl = Object.FindFirstObjectByType<Combat.CameraController>();
            if (camCtrl != null)
            {
                var camSo = new SerializedObject(camCtrl);
                camSo.FindProperty("target").objectReferenceValue = player.transform;
                if (arenaMap != null)
                    camSo.FindProperty("arenaMap").objectReferenceValue = arenaMap;
                camSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Phase2Setup] CameraController → Player + ArenaMap 연결 완료");
            }

            // ── 추가 매니저 36개 ──

            // 플레이어 컴포넌트 참조 (필드 연결용)
            Combat.CombatStats playerStats = player != null ? player.GetComponent<Combat.CombatStats>() : null;
            Combat.LevelSystem levelSystem = player != null ? player.GetComponent<Combat.LevelSystem>() : null;

            // 1. StatAllocationSystem
            if (Object.FindFirstObjectByType<Growth.StatAllocationSystem>() == null)
            {
                var obj = new GameObject("StatAllocationSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create StatAllocationSystem");
                var mgr = obj.AddComponent<Growth.StatAllocationSystem>();
                if (playerStats != null || levelSystem != null)
                {
                    var so = new SerializedObject(mgr);
                    if (levelSystem != null)
                        so.FindProperty("_levelSystem").objectReferenceValue = levelSystem;
                    if (playerStats != null)
                        so.FindProperty("_combatStats").objectReferenceValue = playerStats;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] StatAllocationSystem 생성");
            }

            // 2. JobSystem
            if (Object.FindFirstObjectByType<Growth.JobSystem>() == null)
            {
                var obj = new GameObject("JobSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create JobSystem");
                obj.AddComponent<Growth.JobSystem>();
                Debug.Log("[Phase2Setup] JobSystem 생성");
            }

            // 3. HunterRankSystem
            if (Object.FindFirstObjectByType<Growth.HunterRankSystem>() == null)
            {
                var obj = new GameObject("HunterRankSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create HunterRankSystem");
                var mgr = obj.AddComponent<Growth.HunterRankSystem>();
                if (playerStats != null)
                {
                    var so = new SerializedObject(mgr);
                    so.FindProperty("_combatStats").objectReferenceValue = playerStats;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] HunterRankSystem 생성");
            }

            // 4. ClimberPowerSystem
            if (Object.FindFirstObjectByType<Growth.ClimberPowerSystem>() == null)
            {
                var obj = new GameObject("ClimberPowerSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create ClimberPowerSystem");
                var mgr = obj.AddComponent<Growth.ClimberPowerSystem>();
                if (playerStats != null)
                {
                    var so = new SerializedObject(mgr);
                    so.FindProperty("_combatStats").objectReferenceValue = playerStats;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] ClimberPowerSystem 생성");
            }

            // 9. RelicManager: 2026-04-20 유물 시스템 완전 제거

            // 10. WeaponManager
            if (Object.FindFirstObjectByType<Equipment.WeaponManager>() == null)
            {
                var obj = new GameObject("WeaponManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create WeaponManager");
                var mgr = obj.AddComponent<Equipment.WeaponManager>();
                ConnectCatalog<Data.WeaponDataSO>(mgr, "_catalog", "Assets/Data/SO/Weapon");
                Debug.Log("[Phase2Setup] WeaponManager 생성");
            }

            // 12. PotentialSystem
            if (Object.FindFirstObjectByType<Equipment.PotentialSystem>() == null)
            {
                var obj = new GameObject("PotentialSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create PotentialSystem");
                obj.AddComponent<Equipment.PotentialSystem>();
                Debug.Log("[Phase2Setup] PotentialSystem 생성");
            }

            // 12-a1. QuickHuntManager (QuickHunt 퀘스트용 — 스테이지 75 해금)
            if (Object.FindFirstObjectByType<Combat.QuickHuntManager>() == null)
            {
                var obj = new GameObject("QuickHuntManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create QuickHuntManager");
                obj.AddComponent<Combat.QuickHuntManager>();
                Debug.Log("[Phase2Setup] QuickHuntManager 생성");
            }

            // 12-a2. HeroPowerManager (HeroPowerMilestone 퀘스트용 — 레벨 79 해금)
            if (Object.FindFirstObjectByType<Combat.HeroPowerManager>() == null)
            {
                var obj = new GameObject("HeroPowerManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create HeroPowerManager");
                var mgr = obj.AddComponent<Combat.HeroPowerManager>();
                // 기본 SO 자동 생성/연결
                var config = EnsureDefaultSO<Data.HeroPowerDataSO>("Assets/Data/SO/HeroPower/HeroPowerConfig.asset");
                if (config != null)
                {
                    var so = new SerializedObject(mgr);
                    so.FindProperty("_config").objectReferenceValue = config;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] HeroPowerManager 생성");
            }

            // 12-a3. AbilityManager (UnlockAbility 퀘스트용 — 레벨 113 해금)
            if (Object.FindFirstObjectByType<Combat.AbilityManager>() == null)
            {
                var obj = new GameObject("AbilityManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create AbilityManager");
                var mgr = obj.AddComponent<Combat.AbilityManager>();
                // 기본 SO 자동 생성/연결 + 최소 1개 노드 보장
                var config = EnsureDefaultSO<Data.AbilityTreeConfigSO>("Assets/Data/SO/Ability/AbilityTreeConfig.asset");
                if (config != null)
                {
                    EnsureAbilityDefaultNodes(config);
                    var so = new SerializedObject(mgr);
                    so.FindProperty("_config").objectReferenceValue = config;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] AbilityManager 생성");
            }

            // 12-a4. EliteSummonManager (EliteSummon 퀘스트용 — Stage 96+ 가이드 퀘스트)
            if (Object.FindFirstObjectByType<Combat.EliteSummonManager>() == null)
            {
                var obj = new GameObject("EliteSummonManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create EliteSummonManager");
                var mgr = obj.AddComponent<Combat.EliteSummonManager>();
                // 기존 EliteSummon.asset 로드
                var config = AssetDatabase.LoadAssetAtPath<Data.EliteSummonSO>("Assets/Data/SO/EliteSummon.asset");
                if (config == null)
                    config = EnsureDefaultSO<Data.EliteSummonSO>("Assets/Data/SO/EliteSummon.asset");
                var eliteSpawner = Object.FindFirstObjectByType<Combat.MonsterSpawner>();
                var so = new SerializedObject(mgr);
                if (config != null) so.FindProperty("_config").objectReferenceValue = config;
                if (eliteSpawner != null) so.FindProperty("_spawner").objectReferenceValue = eliteSpawner;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Phase2Setup] EliteSummonManager 생성");
            }

            // 12-b. ArtifactManager (EquipArtifact 퀘스트용 — 해금은 Stage 347)
            if (Object.FindFirstObjectByType<Combat.ArtifactManager>() == null)
            {
                var obj = new GameObject("ArtifactManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create ArtifactManager");
                var mgr = obj.AddComponent<Combat.ArtifactManager>();
                // 카탈로그 폴더가 존재할 때만 연결 (Artifact SO는 아직 미작성 상태일 수 있음)
                if (AssetDatabase.IsValidFolder("Assets/Data/SO/Artifact"))
                {
                    ConnectCatalog<Data.ArtifactDataSO>(mgr, "_artifactCatalog", "Assets/Data/SO/Artifact");
                    ConnectCatalog<Data.ArtifactSetSO>(mgr, "_setCatalog", "Assets/Data/SO/Artifact");
                }
                Debug.Log("[Phase2Setup] ArtifactManager 생성");
            }

            // 13. DungeonManager
            if (Object.FindFirstObjectByType<Dungeon.DungeonManager>() == null)
            {
                var obj = new GameObject("DungeonManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create DungeonManager");
                obj.AddComponent<Dungeon.DungeonManager>();
                Debug.Log("[Phase2Setup] DungeonManager 생성");
            }

            // 14. DungeonContentHandler
            if (Object.FindFirstObjectByType<Dungeon.DungeonContentHandler>() == null)
            {
                var obj = new GameObject("DungeonContentHandler");
                Undo.RegisterCreatedObjectUndo(obj, "Create DungeonContentHandler");
                obj.AddComponent<Dungeon.DungeonContentHandler>();
                Debug.Log("[Phase2Setup] DungeonContentHandler 생성");
            }

            // 16. BossRaidSystem
            if (Object.FindFirstObjectByType<Dungeon.BossRaidSystem>() == null)
            {
                var obj = new GameObject("BossRaidSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create BossRaidSystem");
                obj.AddComponent<Dungeon.BossRaidSystem>();
                Debug.Log("[Phase2Setup] BossRaidSystem 생성");
            }

            // 17. QuestManager
            {
                var existing = Object.FindFirstObjectByType<Quest.QuestManager>();
                if (existing == null)
                {
                    var obj = new GameObject("QuestManager");
                    Undo.RegisterCreatedObjectUndo(obj, "Create QuestManager");
                    existing = obj.AddComponent<Quest.QuestManager>();
                    Debug.Log("[Phase2Setup] QuestManager 생성");
                }
                // 디스크 최신 상태로 catalog 재동기화 (기존 매니저도 대상)
                ConnectCatalog<Data.QuestDataSO>(existing, "_questCatalog", "Assets/Data/SO/Quest");
            }

            // 18. AttendanceSystem
            if (Object.FindFirstObjectByType<Quest.AttendanceSystem>() == null)
            {
                var obj = new GameObject("AttendanceSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create AttendanceSystem");
                obj.AddComponent<Quest.AttendanceSystem>();
                Debug.Log("[Phase2Setup] AttendanceSystem 생성");
            }

            // 19. MailboxSystem
            if (Object.FindFirstObjectByType<Quest.MailboxSystem>() == null)
            {
                var obj = new GameObject("MailboxSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create MailboxSystem");
                obj.AddComponent<Quest.MailboxSystem>();
                Debug.Log("[Phase2Setup] MailboxSystem 생성");
            }

            // 20. AchievementSystem
            if (Object.FindFirstObjectByType<Quest.AchievementSystem>() == null)
            {
                var obj = new GameObject("AchievementSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create AchievementSystem");
                obj.AddComponent<Quest.AchievementSystem>();
                Debug.Log("[Phase2Setup] AchievementSystem 생성");
            }

            // 21. ShopSystem
            if (Object.FindFirstObjectByType<Economy.ShopSystem>() == null)
            {
                var obj = new GameObject("ShopSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create ShopSystem");
                obj.AddComponent<Economy.ShopSystem>();
                Debug.Log("[Phase2Setup] ShopSystem 생성");
            }

            // 22. OfflineRewardSystem
            if (Object.FindFirstObjectByType<Combat.OfflineRewardSystem>() == null)
            {
                var obj = new GameObject("OfflineRewardSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create OfflineRewardSystem");
                obj.AddComponent<Combat.OfflineRewardSystem>();
                Debug.Log("[Phase2Setup] OfflineRewardSystem 생성");
            }

            // 23. SettingsManager
            if (Object.FindFirstObjectByType<Core.SettingsManager>() == null)
            {
                var obj = new GameObject("SettingsManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create SettingsManager");
                obj.AddComponent<Core.SettingsManager>();
                Debug.Log("[Phase2Setup] SettingsManager 생성");
            }

            // 24. SeasonManager
            if (Object.FindFirstObjectByType<Core.SeasonManager>() == null)
            {
                var obj = new GameObject("SeasonManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create SeasonManager");
                obj.AddComponent<Core.SeasonManager>();
                Debug.Log("[Phase2Setup] SeasonManager 생성");
            }

            // 25. BattlePassSystem
            if (Object.FindFirstObjectByType<Quest.BattlePassSystem>() == null)
            {
                var obj = new GameObject("BattlePassSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create BattlePassSystem");
                obj.AddComponent<Quest.BattlePassSystem>();
                Debug.Log("[Phase2Setup] BattlePassSystem 생성");
            }

            // 26. SeasonShopSystem
            if (Object.FindFirstObjectByType<Quest.SeasonShopSystem>() == null)
            {
                var obj = new GameObject("SeasonShopSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create SeasonShopSystem");
                obj.AddComponent<Quest.SeasonShopSystem>();
                Debug.Log("[Phase2Setup] SeasonShopSystem 생성");
            }

            // 32. CostumeManager
            if (Object.FindFirstObjectByType<Costume.CostumeManager>() == null)
            {
                var obj = new GameObject("CostumeManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create CostumeManager");
                obj.AddComponent<Costume.CostumeManager>();
                Debug.Log("[Phase2Setup] CostumeManager 생성");
            }

            // 36. UpdateManager
            if (Object.FindFirstObjectByType<Utils.UpdateManager>() == null)
            {
                var obj = new GameObject("UpdateManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create UpdateManager");
                obj.AddComponent<Utils.UpdateManager>();
                Debug.Log("[Phase2Setup] UpdateManager 생성");
            }

            // 37. TutorialManager
            if (Object.FindFirstObjectByType<Core.TutorialManager>() == null)
            {
                var obj = new GameObject("TutorialManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create TutorialManager");
                obj.AddComponent<Core.TutorialManager>();
                Debug.Log("[Phase2Setup] TutorialManager 생성");
            }

            // 38. AudioManager
            if (Object.FindFirstObjectByType<Core.AudioManager>() == null)
            {
                var obj = new GameObject("AudioManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create AudioManager");
                obj.AddComponent<Core.AudioManager>();
                Debug.Log("[Phase2Setup] AudioManager 생성");
            }

            // 39. IAPManager
            if (Object.FindFirstObjectByType<Economy.IAPManager>() == null)
            {
                var obj = new GameObject("IAPManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create IAPManager");
                obj.AddComponent<Economy.IAPManager>();
                Debug.Log("[Phase2Setup] IAPManager 생성");
            }

            // 40. BundleSystem
            if (Object.FindFirstObjectByType<Economy.BundleSystem>() == null)
            {
                var obj = new GameObject("BundleSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create BundleSystem");
                obj.AddComponent<Economy.BundleSystem>();
                Debug.Log("[Phase2Setup] BundleSystem 생성");
            }

            // 40b. LoginFlowManager
            if (Object.FindFirstObjectByType<UI.LoginFlowManager>() == null)
            {
                var obj = new GameObject("LoginFlowManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create LoginFlowManager");
                obj.AddComponent<UI.LoginFlowManager>();
                Debug.Log("[Phase2Setup] LoginFlowManager 생성");
            }

            // 41. PerformanceManager
            if (Object.FindFirstObjectByType<Core.PerformanceManager>() == null)
            {
                var obj = new GameObject("PerformanceManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create PerformanceManager");
                obj.AddComponent<Core.PerformanceManager>();
                Debug.Log("[Phase2Setup] PerformanceManager 생성");
            }

            // 43. InscriptionManager
            if (Object.FindFirstObjectByType<Growth.InscriptionManager>() == null)
            {
                var obj = new GameObject("InscriptionManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create InscriptionManager");
                obj.AddComponent<Growth.InscriptionManager>();
                Debug.Log("[Phase2Setup] InscriptionManager 생성");
            }

            // 45. PrestigeSystem
            if (Object.FindFirstObjectByType<Dungeon.PrestigeSystem>() == null)
            {
                var obj = new GameObject("PrestigeSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create PrestigeSystem");
                obj.AddComponent<Dungeon.PrestigeSystem>();
                Debug.Log("[Phase2Setup] PrestigeSystem 생성");
            }

            // 46. AdRewardManager
            if (Object.FindFirstObjectByType<Economy.AdRewardManager>() == null)
            {
                var obj = new GameObject("AdRewardManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create AdRewardManager");
                obj.AddComponent<Economy.AdRewardManager>();
                Debug.Log("[Phase2Setup] AdRewardManager 생성");
            }

            // 47. DailyBlessingSystem
            if (Object.FindFirstObjectByType<Combat.DailyBlessingSystem>() == null)
            {
                var obj = new GameObject("DailyBlessingSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create DailyBlessingSystem");
                obj.AddComponent<Combat.DailyBlessingSystem>();
                Debug.Log("[Phase2Setup] DailyBlessingSystem 생성");
            }

            // 48. EventContentManager
            if (Object.FindFirstObjectByType<Economy.EventContentManager>() == null)
            {
                var obj = new GameObject("EventContentManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create EventContentManager");
                obj.AddComponent<Economy.EventContentManager>();
                Debug.Log("[Phase2Setup] EventContentManager 생성");
            }

            // 49. FloorRankingSystem
            if (Object.FindFirstObjectByType<Dungeon.FloorRankingSystem>() == null)
            {
                var obj = new GameObject("FloorRankingSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create FloorRankingSystem");
                obj.AddComponent<Dungeon.FloorRankingSystem>();
                Debug.Log("[Phase2Setup] FloorRankingSystem 생성");
            }

            // 53. CollectionBookManager
            if (Object.FindFirstObjectByType<Combat.CollectionBookManager>() == null)
            {
                var obj = new GameObject("CollectionBookManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create CollectionBookManager");
                obj.AddComponent<Combat.CollectionBookManager>();
                Debug.Log("[Phase2Setup] CollectionBookManager 생성");
            }

            // 56. UISoundTrigger
            if (Object.FindFirstObjectByType<UI.UISoundTrigger>() == null)
            {
                var obj = new GameObject("UISoundTrigger");
                Undo.RegisterCreatedObjectUndo(obj, "Create UISoundTrigger");
                obj.AddComponent<UI.UISoundTrigger>();
                Debug.Log("[Phase2Setup] UISoundTrigger 생성");
            }

            // 57. ScreenShakeManager
            if (Object.FindFirstObjectByType<Combat.ScreenShakeManager>() == null)
            {
                var obj = new GameObject("ScreenShakeManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create ScreenShakeManager");
                obj.AddComponent<Combat.ScreenShakeManager>();
                Debug.Log("[Phase2Setup] ScreenShakeManager 생성");
            }

            // 58. IconRegistry
            if (Object.FindFirstObjectByType<Core.IconRegistry>() == null)
            {
                var obj = new GameObject("IconRegistry");
                Undo.RegisterCreatedObjectUndo(obj, "Create IconRegistry");
                obj.AddComponent<Core.IconRegistry>();
                Debug.Log("[Phase2Setup] IconRegistry 생성");
            }

            // 59. SkillVfxRegistry
            if (Object.FindFirstObjectByType<Combat.SkillVfxRegistry>() == null)
            {
                var obj = new GameObject("SkillVfxRegistry");
                Undo.RegisterCreatedObjectUndo(obj, "Create SkillVfxRegistry");
                var registry = obj.AddComponent<Combat.SkillVfxRegistry>();

                // 직업별 VFX SO 연결 — VFX_Imported 우선, 없으면 기본 Slash/Cartoon 폴백
                var warriorAtk = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_Warrior_Warrior_Warrior_1.asset", "Assets/Data/SO/VFX/Vfx_Slash_1.asset");
                var archerAtk = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_Archer_MagicArrows_1.asset", "Assets/Data/SO/VFX/Vfx_Slash_3.asset");
                var mageAtk = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_Mage_FireMage_1.asset", "Assets/Data/SO/VFX/Vfx_Slash_5.asset");
                var warriorHit = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_Warrior_Impacts_1_COLOR.asset", "Assets/Data/SO/VFX/Vfx_Cartoon_1.asset");
                var archerHit = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_ImpactsExplosions_MiniPack_1.asset", "Assets/Data/SO/VFX/Vfx_Cartoon_3.asset");
                var mageHit = LoadVfxWithFallback("Assets/Data/SO/VFX_Imported/Vfx_ImpactsExplosions_Explosions_1.asset", "Assets/Data/SO/VFX/Vfx_Cartoon_5.asset");

                var regSo = new SerializedObject(registry);
                if (warriorAtk != null) regSo.FindProperty("_warriorAttackVfx").objectReferenceValue = warriorAtk;
                if (archerAtk != null) regSo.FindProperty("_archerAttackVfx").objectReferenceValue = archerAtk;
                if (mageAtk != null) regSo.FindProperty("_mageAttackVfx").objectReferenceValue = mageAtk;
                if (warriorHit != null) regSo.FindProperty("_warriorHitVfx").objectReferenceValue = warriorHit;
                if (archerHit != null) regSo.FindProperty("_archerHitVfx").objectReferenceValue = archerHit;
                if (mageHit != null) regSo.FindProperty("_mageHitVfx").objectReferenceValue = mageHit;
                regSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Phase2Setup] SkillVfxRegistry 생성 + VFX SO 연결");
            }

            // 60. UIThemeManager
            if (Object.FindFirstObjectByType<UI.UIThemeManager>() == null)
            {
                var obj = new GameObject("UIThemeManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create UIThemeManager");
                obj.AddComponent<UI.UIThemeManager>();
                Debug.Log("[Phase2Setup] UIThemeManager 생성");
            }

            // 61. ZoneVisualManager
            if (Object.FindFirstObjectByType<Combat.ZoneVisualManager>() == null)
            {
                var obj = new GameObject("ZoneVisualManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create ZoneVisualManager");
                obj.AddComponent<Combat.ZoneVisualManager>();
                Debug.Log("[Phase2Setup] ZoneVisualManager 생성");
            }

            // 62. LoadingScreen
            if (Object.FindFirstObjectByType<UI.LoadingScreen>() == null)
            {
                var obj = new GameObject("LoadingScreen");
                Undo.RegisterCreatedObjectUndo(obj, "Create LoadingScreen");
                obj.AddComponent<UI.LoadingScreen>();
                Debug.Log("[Phase2Setup] LoadingScreen 생성");
            }

            // 63. MasteryManager
            if (Object.FindFirstObjectByType<Growth.MasteryManager>() == null)
            {
                var obj = new GameObject("MasteryManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create MasteryManager");
                var mgr = obj.AddComponent<Growth.MasteryManager>();
                var masteryCatalog = CreateMasteryDataSOs();
                if (masteryCatalog != null && masteryCatalog.Length > 0)
                {
                    var so = new SerializedObject(mgr);
                    var prop = so.FindProperty("_catalog");
                    prop.arraySize = masteryCatalog.Length;
                    for (int i = 0; i < masteryCatalog.Length; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = masteryCatalog[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log("[Phase2Setup] MasteryManager 생성");
            }

            // 64. ReturneeGuideManager
            if (Object.FindFirstObjectByType<Combat.ReturneeGuideManager>() == null)
            {
                var obj = new GameObject("ReturneeGuideManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create ReturneeGuideManager");
                obj.AddComponent<Combat.ReturneeGuideManager>();
                Debug.Log("[Phase2Setup] ReturneeGuideManager 생성");
            }

            // 65. RecommendationManager
            if (Object.FindFirstObjectByType<UI.RecommendationManager>() == null)
            {
                var obj = new GameObject("RecommendationManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create RecommendationManager");
                obj.AddComponent<UI.RecommendationManager>();
                Debug.Log("[Phase2Setup] RecommendationManager 생성");
            }

            // ── 카테고리 26: 체감/재미 강화 시스템 ──

            // 67. EquipFlashFeedback (장착 글로우 연출)
            if (Object.FindFirstObjectByType<Combat.EquipFlashFeedback>() == null)
            {
                var obj = new GameObject("EquipFlashFeedback");
                Undo.RegisterCreatedObjectUndo(obj, "Create EquipFlashFeedback");
                obj.AddComponent<Combat.EquipFlashFeedback>();
                Debug.Log("[Phase2Setup] EquipFlashFeedback 생성");
            }

            // 68. GachaQuickCompare (가챠 즉시 비교 오버레이)
            if (Object.FindFirstObjectByType<UI.GachaQuickCompare>() == null)
            {
                var obj = new GameObject("GachaQuickCompare");
                Undo.RegisterCreatedObjectUndo(obj, "Create GachaQuickCompare");
                obj.AddComponent<UI.GachaQuickCompare>();
                Debug.Log("[Phase2Setup] GachaQuickCompare 생성");
            }

            // 69. RedDotManager (빨간 점 관리)
            if (Object.FindFirstObjectByType<UI.RedDotManager>() == null)
            {
                var obj = new GameObject("RedDotManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create RedDotManager");
                obj.AddComponent<UI.RedDotManager>();
                Debug.Log("[Phase2Setup] RedDotManager 생성");
            }

            // 71. ContextRecommendBanner (상황별 추천 배너)
            if (Object.FindFirstObjectByType<UI.ContextRecommendBanner>() == null)
            {
                var obj = new GameObject("ContextRecommendBanner");
                Undo.RegisterCreatedObjectUndo(obj, "Create ContextRecommendBanner");
                obj.AddComponent<UI.ContextRecommendBanner>();
                Debug.Log("[Phase2Setup] ContextRecommendBanner 생성");
            }

            // 73. BattlePassLevelUpEffect (배틀패스 레벨업 연출)
            if (Object.FindFirstObjectByType<UI.BattlePassLevelUpEffect>() == null)
            {
                var obj = new GameObject("BattlePassLevelUpEffect");
                Undo.RegisterCreatedObjectUndo(obj, "Create BattlePassLevelUpEffect");
                obj.AddComponent<UI.BattlePassLevelUpEffect>();
                Debug.Log("[Phase2Setup] BattlePassLevelUpEffect 생성");
            }

            // 74. SkillUnlockEffect (스킬 해금 연출)
            if (Object.FindFirstObjectByType<UI.SkillUnlockEffect>() == null)
            {
                var obj = new GameObject("SkillUnlockEffect");
                Undo.RegisterCreatedObjectUndo(obj, "Create SkillUnlockEffect");
                obj.AddComponent<UI.SkillUnlockEffect>();
                Debug.Log("[Phase2Setup] SkillUnlockEffect 생성");
            }

            // 75. AchievementToast (업적 토스트)
            if (Object.FindFirstObjectByType<UI.AchievementToast>() == null)
            {
                var obj = new GameObject("AchievementToast");
                Undo.RegisterCreatedObjectUndo(obj, "Create AchievementToast");
                obj.AddComponent<UI.AchievementToast>();
                Debug.Log("[Phase2Setup] AchievementToast 생성");
            }

            // 76. CpGatingSystem (CP 게이팅 시스템)
            if (Object.FindFirstObjectByType<Core.CpGatingSystem>() == null)
            {
                var obj = new GameObject("CpGatingSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create CpGatingSystem");
                obj.AddComponent<Core.CpGatingSystem>();
                Debug.Log("[Phase2Setup] CpGatingSystem 생성");
            }

            // 77. ProceduralBackground (프로시져럴 바닥/장식/파티클)
            if (Object.FindFirstObjectByType<Combat.ProceduralBackground>() == null)
            {
                var obj = new GameObject("ProceduralBackground");
                Undo.RegisterCreatedObjectUndo(obj, "Create ProceduralBackground");
                obj.AddComponent<Combat.ProceduralBackground>();
                Debug.Log("[Phase2Setup] ProceduralBackground 생성");
            }

            // 78. DailyChecklistManager (일일 체크리스트)
            if (Object.FindFirstObjectByType<Quest.DailyChecklistManager>() == null)
            {
                var obj = new GameObject("DailyChecklistManager");
                Undo.RegisterCreatedObjectUndo(obj, "Create DailyChecklistManager");
                obj.AddComponent<Quest.DailyChecklistManager>();
                Debug.Log("[Phase2Setup] DailyChecklistManager 생성");
            }

        }

        // ── 직업별 스킬 연결 ──

        private static void ConnectJobSkills(
            Data.CharacterDataSO charData,
            Data.SkillDataSO[] warriorSkills,
            Data.SkillDataSO[] archerSkills,
            Data.SkillDataSO[] mageSkills)
        {
            var skillSystem = Object.FindFirstObjectByType<Combat.SkillSystem>();
            if (skillSystem == null)
            {
                Debug.LogWarning("[Phase2Setup] SkillSystem을 찾을 수 없음 — 스킬 연결 건너뜀");
                return;
            }

            // 캐릭터 ID로 직업 판별
            Data.SkillDataSO[] jobSkills = warriorSkills; // 기본값
            string jobName = "전사";

            if (charData != null && !string.IsNullOrEmpty(charData.id))
            {
                string id = charData.id.ToLower();
                if (id.Contains("archer") || id.Contains("scout") || id.Contains("windwalker") || id.Contains("hawkeye"))
                {
                    jobSkills = archerSkills;
                    jobName = "궁수";
                }
                else if (id.Contains("mage") || id.Contains("sorcerer") || id.Contains("sage") || id.Contains("runemaster"))
                {
                    jobSkills = mageSkills;
                    jobName = "마법사";
                }
            }

            var so = new SerializedObject(skillSystem);
            var prop = so.FindProperty("_allSkills");
            prop.arraySize = jobSkills.Length;
            for (int i = 0; i < jobSkills.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = jobSkills[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Phase2Setup] SkillSystem에 {jobName} 스킬 {jobSkills.Length}개 연결 완료");
        }

        // ── 마스터리 데이터 SO 생성 ──

        private static Data.MasteryDataSO[] CreateMasteryDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Mastery");

            var list = new System.Collections.Generic.List<Data.MasteryDataSO>();

            // 전사 — 3분기 x 3단계
            // 분기 0: 파괴 (공격 특화)
            list.Add(CreateMasterySO("mastery_w_0_0", "파괴의 힘", "ATK +5%", Core.JobType.Warrior, 0, 0, "", Core.StatType.Atk, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_w_0_1", "파괴의 의지", "ATK +8%", Core.JobType.Warrior, 0, 1, "mastery_w_0_0", Core.StatType.Atk, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_w_0_2", "파괴의 극의", "ATK +12%", Core.JobType.Warrior, 0, 2, "mastery_w_0_1", Core.StatType.Atk, 0f, 0.12f));
            // 분기 1: 수호 (방어 특화)
            list.Add(CreateMasterySO("mastery_w_1_0", "수호의 갑옷", "DEF +5%", Core.JobType.Warrior, 1, 0, "", Core.StatType.Def, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_w_1_1", "수호의 방패", "MaxHP +8%", Core.JobType.Warrior, 1, 1, "mastery_w_1_0", Core.StatType.MaxHp, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_w_1_2", "수호의 극의", "DEF +12%", Core.JobType.Warrior, 1, 2, "mastery_w_1_1", Core.StatType.Def, 0f, 0.12f));
            // 분기 2: 광전사 (치명타 특화)
            list.Add(CreateMasterySO("mastery_w_2_0", "광기의 칼날", "CritRate +5%", Core.JobType.Warrior, 2, 0, "", Core.StatType.CritRate, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_w_2_1", "광기의 일격", "CritDamage +10%", Core.JobType.Warrior, 2, 1, "mastery_w_2_0", Core.StatType.CritDamage, 0f, 0.1f));
            list.Add(CreateMasterySO("mastery_w_2_2", "광기의 극의", "CritRate +10%", Core.JobType.Warrior, 2, 2, "mastery_w_2_1", Core.StatType.CritRate, 0f, 0.1f));

            // 궁수 — 3분기 x 3단계
            // 분기 0: 속사 (공격속도 특화)
            list.Add(CreateMasterySO("mastery_a_0_0", "속사의 감각", "AttackSpeed +5%", Core.JobType.Archer, 0, 0, "", Core.StatType.AttackSpeed, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_a_0_1", "속사의 흐름", "AttackSpeed +8%", Core.JobType.Archer, 0, 1, "mastery_a_0_0", Core.StatType.AttackSpeed, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_a_0_2", "속사의 극의", "ATK +10%, AttackSpeed +5%", Core.JobType.Archer, 0, 2, "mastery_a_0_1", Core.StatType.Atk, 0f, 0.1f));
            // 분기 1: 정밀 (치명타 특화)
            list.Add(CreateMasterySO("mastery_a_1_0", "정밀 사격", "CritRate +5%", Core.JobType.Archer, 1, 0, "", Core.StatType.CritRate, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_a_1_1", "급소 간파", "CritDamage +12%", Core.JobType.Archer, 1, 1, "mastery_a_1_0", Core.StatType.CritDamage, 0f, 0.12f));
            list.Add(CreateMasterySO("mastery_a_1_2", "정밀의 극의", "CritRate +10%", Core.JobType.Archer, 1, 2, "mastery_a_1_1", Core.StatType.CritRate, 0f, 0.1f));
            // 분기 2: 회피 (생존 특화)
            list.Add(CreateMasterySO("mastery_a_2_0", "바람의 몸놀림", "DodgeRate +5%", Core.JobType.Archer, 2, 0, "", Core.StatType.DodgeRate, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_a_2_1", "질풍의 발걸음", "MoveSpeed +8%", Core.JobType.Archer, 2, 1, "mastery_a_2_0", Core.StatType.MoveSpeed, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_a_2_2", "회피의 극의", "DodgeRate +10%", Core.JobType.Archer, 2, 2, "mastery_a_2_1", Core.StatType.DodgeRate, 0f, 0.1f));

            // 마법사 — 3분기 x 3단계
            // 분기 0: 파멸 (공격 특화)
            list.Add(CreateMasterySO("mastery_m_0_0", "마력 증폭", "ATK +6%", Core.JobType.Mage, 0, 0, "", Core.StatType.Atk, 0f, 0.06f));
            list.Add(CreateMasterySO("mastery_m_0_1", "원소 폭발", "ATK +10%", Core.JobType.Mage, 0, 1, "mastery_m_0_0", Core.StatType.Atk, 0f, 0.1f));
            list.Add(CreateMasterySO("mastery_m_0_2", "파멸의 극의", "ATK +14%", Core.JobType.Mage, 0, 2, "mastery_m_0_1", Core.StatType.Atk, 0f, 0.14f));
            // 분기 1: 시간 (공격속도 특화)
            list.Add(CreateMasterySO("mastery_m_1_0", "시간 가속", "AttackSpeed +5%", Core.JobType.Mage, 1, 0, "", Core.StatType.AttackSpeed, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_m_1_1", "시간 왜곡", "AttackSpeed +8%", Core.JobType.Mage, 1, 1, "mastery_m_1_0", Core.StatType.AttackSpeed, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_m_1_2", "시간의 극의", "CritDamage +12%", Core.JobType.Mage, 1, 2, "mastery_m_1_1", Core.StatType.CritDamage, 0f, 0.12f));
            // 분기 2: 보호 (생존 특화)
            list.Add(CreateMasterySO("mastery_m_2_0", "마력 보호막", "MaxHP +5%", Core.JobType.Mage, 2, 0, "", Core.StatType.MaxHp, 0f, 0.05f));
            list.Add(CreateMasterySO("mastery_m_2_1", "마력 재생", "DEF +8%", Core.JobType.Mage, 2, 1, "mastery_m_2_0", Core.StatType.Def, 0f, 0.08f));
            list.Add(CreateMasterySO("mastery_m_2_2", "보호의 극의", "MaxHP +12%", Core.JobType.Mage, 2, 2, "mastery_m_2_1", Core.StatType.MaxHp, 0f, 0.12f));

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 마스터리 SO {list.Count}개 생성/업데이트 완료");
            return list.ToArray();
        }

        private static Data.MasteryDataSO CreateMasterySO(
            string id, string displayName, string description,
            Core.JobType job, int branchIndex, int nodeLevel, string prerequisiteId,
            Core.StatType bonusStat, float flatBonus, float percentBonus, int pointCost = 1)
        {
            string path = $"Assets/Data/SO/Mastery/Mastery_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.MasteryDataSO>(path);
            if (existing != null)
            {
                existing.displayName = displayName;
                existing.description = description;
                existing.requiredJob = job;
                existing.branchIndex = branchIndex;
                existing.nodeLevel = nodeLevel;
                existing.prerequisiteId = prerequisiteId;
                existing.bonusStat = bonusStat;
                existing.flatBonus = flatBonus;
                existing.percentBonus = percentBonus;
                existing.pointCost = pointCost;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<Data.MasteryDataSO>();
            so.id = id;
            so.displayName = displayName;
            so.description = description;
            so.requiredJob = job;
            so.branchIndex = branchIndex;
            so.nodeLevel = nodeLevel;
            so.prerequisiteId = prerequisiteId;
            so.bonusStat = bonusStat;
            so.flatBonus = flatBonus;
            so.percentBonus = percentBonus;
            so.pointCost = pointCost;

            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        // ── 전사 스킬 SO 생성 ──

        private static Data.SkillDataSO[] CreateWarriorSkills()
        {
            EnsureDirectory("Assets/Data/SO/Skills");

            var skills = new Data.SkillDataSO[]
            {
                // Tier 0: 전사 (Lv1/10/20/30) — Warrior VFX
                CreateSkillSO("warrior_strike", "휘두르기", "기본 검 베기 ATK 150%",
                    Core.SkillType.Active, 1, 0, 0f, 0f, 1.5f, 0f, 1,
                    iconName: "Bonus/Bonus_skills/Nobg/24_heavy_blow_nobg",
                    vfxSheetName: "Vfx_Warrior_Warrior_Warrior_1"),

                CreateSkillSO("warrior_training", "단련", "꾸준한 수련으로 공격력 +10%",
                    Core.SkillType.Passive, 10, 0, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.1f, iconName: "Skill_nobg/skill_190_noBG"),

                CreateSkillSO("warrior_warcry", "전투 함성", "8초간 공격속도 +30%",
                    Core.SkillType.Buff, 20, 0, 10f, 8f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.3f, iconName: "Skill_nobg/skill_45_noBG",
                    vfxSheetName: "Vfx_Warrior_Warrior_Warrior_2"),

                CreateSkillSO("warrior_fury", "분노의 일격", "주변 적에게 ATK 500% + 스턴 2초",
                    Core.SkillType.Awakening, 30, 0, 20f, 0f, 5f, 4f, 1,
                    effect: Core.SkillEffect.Stun, effectDuration: 2f,
                    iconName: "Bonus/Bonus_skills/Nobg/49_red_wave_nobg",
                    vfxSheetName: "Vfx_Warrior_Warrior_Warrior_3"),

                // Tier 1: 나이트 (Lv40/50/60/70) — FrostKnight VFX
                CreateSkillSO("knight_charge", "돌진", "전방 돌진 ATK 250% + 넉백",
                    Core.SkillType.Active, 40, 1, 0f, 0f, 2.5f, 2f, 1,
                    effect: Core.SkillEffect.Knockback, effectValue: 3f,
                    iconName: "Bonus/Bonus_skills/Nobg/11_bull_nobg",
                    vfxSheetName: "Vfx_Warrior_FrostKnight_1"),

                CreateSkillSO("knight_weakness", "약점 간파", "적의 약점을 간파하여 치명타 확률 +15%",
                    Core.SkillType.Passive, 50, 1, 0f, 0f, 0f, 0f, 1,
                    buffCritRate: 0.15f, iconName: "Bonus/Bonus_skills/Nobg/79_headshot_nobg"),

                CreateSkillSO("knight_rally", "전의 고양", "10초간 ATK +25%, 공격속도 +15%",
                    Core.SkillType.Buff, 60, 1, 12f, 10f, 0f, 0f, 1,
                    buffAtkRate: 0.25f, buffAtkSpdRate: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/54_Holy_power_nobg",
                    vfxSheetName: "Vfx_Warrior_FrostKnight_2"),

                CreateSkillSO("knight_judgment", "심판의 검", "넓은 범위 ATK 800% + 스턴 3초 + 자힐 15%",
                    Core.SkillType.Awakening, 70, 1, 20f, 0f, 8f, 6f, 1,
                    effect: Core.SkillEffect.Stun, effectDuration: 3f, healRatio: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/67_swords_of_light_nobg",
                    vfxSheetName: "Vfx_Warrior_FrostKnight_3"),

                // Tier 2: 워로드 (Lv80/90/100/110) — Paladin VFX
                CreateSkillSO("warlord_whirlwind", "선풍참", "360도 ATK 300% 데미지",
                    Core.SkillType.Active, 80, 2, 0f, 0f, 3f, 3f, 1,
                    iconName: "Bonus/Bonus_skills/Nobg/30_vortex_nobg",
                    vfxSheetName: "Vfx_Warrior_Paladin_1"),

                CreateSkillSO("warlord_frenzy", "전투 광기", "HP 50% 이하 시 ATK +25%, CritRate +15%",
                    Core.SkillType.Passive, 90, 2, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.25f, buffCritRate: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/73_unholy_energy_nobg"),

                CreateSkillSO("warlord_bloodpact", "피의 서약", "12초간 공격 시 10% 흡혈",
                    Core.SkillType.Buff, 100, 2, 15f, 12f, 0f, 0f, 1,
                    effect: Core.SkillEffect.Lifesteal, effectValue: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/34_wound_nobg",
                    vfxSheetName: "Vfx_Warrior_Paladin_2"),

                CreateSkillSO("warlord_earthshatter", "대지 분쇄", "전체 ATK 1200% + 스턴 3초 + 자힐 20%",
                    Core.SkillType.Awakening, 110, 2, 20f, 0f, 12f, 8f, 1,
                    effect: Core.SkillEffect.Stun, effectDuration: 3f, healRatio: 0.2f,
                    iconName: "Skill_nobg/skill_68_noBG",
                    vfxSheetName: "Vfx_Warrior_Paladin_3"),

                // Tier 3: 타이탄 (Lv120/130/140/150) — Impacts + Paladin VFX
                CreateSkillSO("titan_annihilate", "멸살의 칼날", "전방 ATK 400% x 3타",
                    Core.SkillType.Active, 120, 3, 0f, 0f, 4f, 3f, 3,
                    iconName: "Bonus/Bonus_skills/Nobg/58_thousand_hits_nobg",
                    vfxSheetName: "Vfx_Warrior_Impacts_1_COLOR"),

                CreateSkillSO("titan_immortal", "불멸의 의지", "사망 시 1회 부활 (HP 30%)",
                    Core.SkillType.Passive, 130, 3, 0f, 0f, 0f, 0f, 1,
                    effect: Core.SkillEffect.Revive, healRatio: 0.3f,
                    iconName: "Bonus/Bonus_skills/Nobg/14_phoenix_nobg"),

                CreateSkillSO("titan_might", "타이탄의 힘", "15초간 ATK +50%",
                    Core.SkillType.Buff, 140, 3, 15f, 15f, 0f, 0f, 1,
                    buffAtkRate: 0.5f, iconName: "Skill_nobg/skill_210_noBG",
                    vfxSheetName: "Vfx_Warrior_Impacts_2_COLOR"),

                CreateSkillSO("titan_cataclysm", "천지개벽", "전체 ATK 2000% + 전스탯 +20% + 자힐 10%",
                    Core.SkillType.Awakening, 150, 3, 20f, 10f, 20f, 10f, 1,
                    buffAtkRate: 0.2f, buffCritRate: 0.2f,
                    effect: Core.SkillEffect.AllStatUp, healRatio: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/75_Sun_nobg",
                    vfxSheetName: "Vfx_Warrior_Paladin_5"),

                // Tier 4: 드래곤 슬레이어 (Lv160/170/180/190) — Impacts VFX
                CreateSkillSO("dragonslayer_dragonblade", "용살의 검", "전방 ATK 600% x 4타 + 관통",
                    Core.SkillType.Active, 160, 4, 0f, 0f, 6f, 5f, 4,
                    effect: Core.SkillEffect.Penetrate,
                    iconName: "Bonus/Bonus_skills/Nobg/24_heavy_blow_nobg",
                    vfxSheetName: "Vfx_Warrior_Impacts_3_COLOR"),

                CreateSkillSO("dragonslayer_dragonheart", "용의 심장", "사망 시 2회 부활 (HP 50%) + ATK +20%",
                    Core.SkillType.Passive, 170, 4, 0f, 0f, 0f, 0f, 1,
                    effect: Core.SkillEffect.Revive, healRatio: 0.5f, buffAtkRate: 0.2f,
                    iconName: "Bonus/Bonus_skills/Nobg/14_phoenix_nobg"),

                CreateSkillSO("dragonslayer_dragonrage", "용의 분노", "20초간 ATK +60%, 치명타 +30%, 흡혈 15%",
                    Core.SkillType.Buff, 180, 4, 18f, 20f, 0f, 0f, 1,
                    buffAtkRate: 0.6f, buffCritRate: 0.3f,
                    effect: Core.SkillEffect.Lifesteal, effectValue: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/73_unholy_energy_nobg",
                    vfxSheetName: "Vfx_Warrior_Impacts_5_COLOR"),

                CreateSkillSO("dragonslayer_apocalypse", "용살 멸세", "전체 ATK 3000% + 스턴 4초 + 자힐 25% + 전스탯 +25%",
                    Core.SkillType.Awakening, 190, 4, 25f, 15f, 30f, 12f, 1,
                    buffAtkRate: 0.25f, buffCritRate: 0.25f,
                    effect: Core.SkillEffect.Stun, effectDuration: 4f, healRatio: 0.25f,
                    iconName: "Skill_nobg/skill_68_noBG",
                    vfxSheetName: "Vfx_Warrior_Impacts_7_COLOR"),

                // Tier 4 전용 패시브 (드래곤 슬레이어)
                CreateSkillSO("dragonslayer_dragonscale", "용의 비늘", "DEF +20%, 받는 피해 -10%",
                    Core.SkillType.Passive, 165, 4, 0f, 0f, 0f, 0f, 1,
                    buffDefRate: 0.2f, iconName: "Bonus/Bonus_skills/Nobg/54_Holy_power_nobg"),

                CreateSkillSO("dragonslayer_dragonblood", "용의 피", "공격 시 8% 확률로 ATK 300% 추가 타격",
                    Core.SkillType.Passive, 175, 4, 0f, 0f, 0f, 0f, 1,
                    buffCritDmgRate: 0.25f, iconName: "Bonus/Bonus_skills/Nobg/34_wound_nobg"),

                CreateSkillSO("dragonslayer_dragonaura", "용의 기운", "MaxHP +15%, 공격속도 +10%",
                    Core.SkillType.Passive, 185, 4, 0f, 0f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.1f, iconName: "Bonus/Bonus_skills/Nobg/73_unholy_energy_nobg"),
            };

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 전사 스킬 SO {skills.Length}개 생성/업데이트 완료");
            return skills;
        }

        // ── 궁수 스킬 SO 생성 ──

        private static Data.SkillDataSO[] CreateArcherSkills()
        {
            EnsureDirectory("Assets/Data/SO/Skills");

            var skills = new Data.SkillDataSO[]
            {
                // Tier 0: 궁수 (Lv1/10/20/30)
                // Tier 0: 궁수 — MagicArrows VFX
                CreateSkillSO("archer_aimshot", "정조준", "정확한 원거리 사격 ATK 180%",
                    Core.SkillType.Active, 1, 0, 0f, 0f, 1.8f, 0f, 1,
                    iconName: "Bonus/Skills_upgrates/Nobg/25_bow_shot_nobg",
                    vfxSheetName: "Vfx_Archer_MagicArrows_1"),

                CreateSkillSO("archer_keeneye", "명사수의 눈", "치명타 확률 +10%",
                    Core.SkillType.Passive, 20, 0, 0f, 0f, 0f, 0f, 1,
                    buffCritRate: 0.1f, iconName: "Skill_nobg/skill_80_noBG"),

                CreateSkillSO("archer_rapidfire", "연사", "8초간 공격속도 +40%",
                    Core.SkillType.Buff, 20, 0, 10f, 8f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.4f, iconName: "Bonus/Skills_upgrates/Nobg/28_bow_shot_nobg",
                    vfxSheetName: "Vfx_Archer_MagicArrows_2"),

                CreateSkillSO("archer_arrowrain", "화살비", "범위 5에 ATK 300% x 3히트",
                    Core.SkillType.Awakening, 30, 0, 20f, 0f, 3f, 5f, 3,
                    iconName: "Skill_nobg/skill_150_noBG",
                    vfxSheetName: "Vfx_Archer_MagicArrows_3"),

                // Tier 1: 스카우트 — Rogue VFX
                CreateSkillSO("scout_pierceshot", "관통 사격", "적을 관통하는 ATK 220% 사격",
                    Core.SkillType.Active, 40, 1, 0f, 0f, 2.2f, 2f, 1,
                    effect: Core.SkillEffect.Penetrate,
                    iconName: "Bonus/Skills_upgrates/Nobg/26_bow_shot_nobg",
                    vfxSheetName: "Vfx_Archer_Rogue_1"),

                CreateSkillSO("scout_agility", "민첩", "공격속도 +15%, 이동속도 +10%",
                    Core.SkillType.Passive, 50, 1, 0f, 0f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.15f, buffMoveSpeedRate: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/57_run_nobg"),

                CreateSkillSO("scout_windblessing", "바람의 가호", "10초간 치명타 +20%, 이동속도 +20%",
                    Core.SkillType.Buff, 60, 1, 12f, 10f, 0f, 0f, 1,
                    buffCritRate: 0.2f, buffMoveSpeedRate: 0.2f,
                    iconName: "Skill_nobg/skill_91_noBG",
                    vfxSheetName: "Vfx_Archer_Rogue_2"),

                CreateSkillSO("scout_stormshot", "폭풍 사격", "범위 6에 ATK 600% x 5히트 + 넉백",
                    Core.SkillType.Awakening, 70, 1, 20f, 0f, 6f, 6f, 5,
                    effect: Core.SkillEffect.Knockback, effectValue: 2f,
                    iconName: "Skill_nobg/skill_430_noBG",
                    vfxSheetName: "Vfx_Archer_Rogue_3"),

                // Tier 2: 윈드워커 — Lightning VFX
                CreateSkillSO("windwalker_windpierce", "바람 꿰뚫기", "관통 ATK 280% x 2히트",
                    Core.SkillType.Active, 80, 2, 0f, 0f, 2.8f, 3f, 2,
                    effect: Core.SkillEffect.Penetrate,
                    iconName: "Bonus/Skills_upgrates/Nobg/27_bow_shot_nobg",
                    vfxSheetName: "Vfx_Archer_Lightning_1"),

                CreateSkillSO("windwalker_afterimage", "잔상", "피격 시 15% 확률로 회피",
                    Core.SkillType.Passive, 90, 2, 0f, 0f, 0f, 0f, 1,
                    effect: Core.SkillEffect.Dodge, effectValue: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/35_clones_nobg"),

                CreateSkillSO("windwalker_gale", "질풍", "12초간 ATK +30%, 공격속도 +25%",
                    Core.SkillType.Buff, 100, 2, 15f, 12f, 0f, 0f, 1,
                    buffAtkRate: 0.3f, buffAtkSpdRate: 0.25f,
                    iconName: "Skill_nobg/skill_105_noBG",
                    vfxSheetName: "Vfx_Archer_Lightning_2"),

                CreateSkillSO("windwalker_typhon", "태풍의 눈", "범위 8에 ATK 1000% x 7히트 + 넉백",
                    Core.SkillType.Awakening, 110, 2, 20f, 0f, 10f, 8f, 7,
                    effect: Core.SkillEffect.Knockback, effectValue: 4f,
                    iconName: "Skill_nobg/skill_100_noBG",
                    vfxSheetName: "Vfx_Archer_Lightning_3"),

                // Tier 3: 호크아이 — Fireballs VFX
                CreateSkillSO("hawkeye_extinction", "멸절의 화살", "관통 ATK 350% x 3히트",
                    Core.SkillType.Active, 120, 3, 0f, 0f, 3.5f, 4f, 3,
                    effect: Core.SkillEffect.Penetrate,
                    iconName: "Skill_nobg/skill_470_noBG",
                    vfxSheetName: "Vfx_Archer_Fireballs_13_1"),

                CreateSkillSO("hawkeye_eagleeye", "매의 눈", "치명타 +25%, 치명타 데미지 +30%",
                    Core.SkillType.Passive, 130, 3, 0f, 0f, 0f, 0f, 1,
                    buffCritRate: 0.25f, buffCritDmgRate: 0.3f,
                    iconName: "Skill_nobg/skill_320_noBG"),

                CreateSkillSO("hawkeye_huntingtime", "사냥의 시간", "15초간 ATK +40%, 치명타 +20%",
                    Core.SkillType.Buff, 140, 3, 15f, 15f, 0f, 0f, 1,
                    buffAtkRate: 0.4f, buffCritRate: 0.2f,
                    iconName: "Bonus/Bonus_skills/Nobg/40_timechanging_nobg",
                    vfxSheetName: "Vfx_Archer_Fireballs_13_2"),

                CreateSkillSO("hawkeye_judgment", "천벌의 비", "범위 10에 ATK 1800% x 10히트 + 스턴 2초 + 자힐 10%",
                    Core.SkillType.Awakening, 150, 3, 20f, 0f, 18f, 10f, 10,
                    effect: Core.SkillEffect.Stun, effectDuration: 2f, healRatio: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/69_starfall_nobg",
                    vfxSheetName: "Vfx_Archer_Fireballs_13_3"),

                // Tier 4: 스톰브링어 (Lv160/170/180/190) — Lightning VFX
                CreateSkillSO("stormbringer_thunderpierce", "뇌전 관통", "관통 ATK 500% x 5히트 + 감속 40%",
                    Core.SkillType.Active, 160, 4, 0f, 0f, 5f, 5f, 5,
                    effect: Core.SkillEffect.Slow, slowRate: 0.4f, ccDuration: 3f,
                    iconName: "Bonus/Skills_upgrates/Nobg/27_bow_shot_nobg",
                    vfxSheetName: "Vfx_Archer_Lightning_4"),

                CreateSkillSO("stormbringer_stormmastery", "폭풍 지배", "치명타 +30%, 치명타 데미지 +40%, 회피 +20%",
                    Core.SkillType.Passive, 170, 4, 0f, 0f, 0f, 0f, 1,
                    buffCritRate: 0.3f, buffCritDmgRate: 0.4f,
                    effect: Core.SkillEffect.Dodge, effectValue: 0.2f,
                    iconName: "Skill_nobg/skill_320_noBG"),

                CreateSkillSO("stormbringer_tempest", "템페스트", "20초간 ATK +55%, 공격속도 +35%, 이동속도 +25%",
                    Core.SkillType.Buff, 180, 4, 18f, 20f, 0f, 0f, 1,
                    buffAtkRate: 0.55f, buffAtkSpdRate: 0.35f, buffMoveSpeedRate: 0.25f,
                    iconName: "Skill_nobg/skill_100_noBG",
                    vfxSheetName: "Vfx_Archer_Lightning_5"),

                CreateSkillSO("stormbringer_ragnarok", "라그나로크", "범위 12에 ATK 2800% x 12히트 + 스턴 3초 + 넉백 + 자힐 15%",
                    Core.SkillType.Awakening, 190, 4, 25f, 0f, 28f, 12f, 12,
                    effect: Core.SkillEffect.Stun, effectDuration: 3f, healRatio: 0.15f,
                    iconName: "Bonus/Bonus_skills/Nobg/69_starfall_nobg",
                    vfxSheetName: "Vfx_Archer_Lightning_6"),

                // Tier 4 전용 패시브 (스톰브링어)
                CreateSkillSO("stormbringer_windwalking", "바람걸음", "이동속도 +15%, 회피 +10%",
                    Core.SkillType.Passive, 165, 4, 0f, 0f, 0f, 0f, 1,
                    buffMoveSpeedRate: 0.15f, effect: Core.SkillEffect.Dodge, effectValue: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/57_run_nobg"),

                CreateSkillSO("stormbringer_chainlightning", "연쇄 번개 체질", "공격속도 +15%, 치명타 데미지 +20%",
                    Core.SkillType.Passive, 175, 4, 0f, 0f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.15f, buffCritDmgRate: 0.2f,
                    iconName: "Bonus/Skills_upgrates/Nobg/26_bow_shot_nobg"),

                CreateSkillSO("stormbringer_stormshield", "폭풍의 가호", "DEF +12%, MaxHP +10%",
                    Core.SkillType.Passive, 185, 4, 0f, 0f, 0f, 0f, 1,
                    buffDefRate: 0.12f, iconName: "Skill_nobg/skill_91_noBG"),
            };

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 궁수 스킬 SO {skills.Length}개 생성/업데이트 완료");
            return skills;
        }

        // ── 마법사 스킬 SO 생성 ──

        private static Data.SkillDataSO[] CreateMageSkills()
        {
            EnsureDirectory("Assets/Data/SO/Skills");

            var skills = new Data.SkillDataSO[]
            {
                // Tier 0: 마법사 — FireMage VFX
                CreateSkillSO("mage_magicbolt", "마력탄", "마력을 응축한 원거리 공격 ATK 220%",
                    Core.SkillType.Active, 1, 0, 0f, 0f, 2.2f, 0f, 1,
                    iconName: "Bonus/Skills_upgrates/Nobg/33_wand_shot_nobg",
                    vfxSheetName: "Vfx_Mage_FireMage_1"),

                CreateSkillSO("mage_concentration", "마력 집중", "ATK +12%",
                    Core.SkillType.Passive, 10, 0, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.12f, iconName: "Bonus/Bonus_skills/Nobg/62_light_nobg"),

                CreateSkillSO("mage_manacharge", "마력 충전", "8초간 ATK +25%",
                    Core.SkillType.Buff, 20, 0, 10f, 8f, 0f, 0f, 1,
                    buffAtkRate: 0.25f, iconName: "Skill_nobg/skill_240_noBG",
                    vfxSheetName: "Vfx_Mage_FireMage_2"),

                CreateSkillSO("mage_fireburst", "화염 폭발", "범위 5에 ATK 450% + 화상 3초(50%)",
                    Core.SkillType.Awakening, 30, 0, 20f, 0f, 4.5f, 5f, 1,
                    effect: Core.SkillEffect.Burn, dotDamageRate: 0.5f, ccDuration: 3f,
                    iconName: "Skill_nobg/skill_22_noBG",
                    vfxSheetName: "Vfx_Mage_FireMage_3"),

                // Tier 1: 소서러 — Necromancer VFX
                CreateSkillSO("sorcerer_fireball", "화염구", "범위 3 화상 기본공격 ATK 260%",
                    Core.SkillType.Active, 40, 1, 0f, 0f, 2.6f, 3f, 1,
                    effect: Core.SkillEffect.Burn, dotDamageRate: 0.3f, ccDuration: 3f,
                    iconName: "Skill_nobg/skill_390_noBG",
                    vfxSheetName: "Vfx_Mage_Necromancer_1"),

                CreateSkillSO("sorcerer_elemental", "원소 친화", "ATK +15%, 공격속도 +10%",
                    Core.SkillType.Passive, 50, 1, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.15f, buffAtkSpdRate: 0.1f,
                    iconName: "Skill_nobg/skill_360_noBG"),

                CreateSkillSO("sorcerer_chainlightning", "연쇄 번개", "10초간 ATK +20%, 치명타 +15%",
                    Core.SkillType.Buff, 60, 1, 12f, 10f, 0f, 0f, 1,
                    buffAtkRate: 0.2f, buffCritRate: 0.15f,
                    iconName: "Bonus/Skills_upgrates/Nobg/34_wand_shot_nobg",
                    vfxSheetName: "Vfx_Mage_Necromancer_2"),

                CreateSkillSO("sorcerer_froststorm", "빙결 폭풍", "범위 7에 ATK 700% + 빙결 3초",
                    Core.SkillType.Awakening, 70, 1, 20f, 0f, 7f, 7f, 1,
                    effect: Core.SkillEffect.Freeze, ccDuration: 3f,
                    iconName: "Skill_nobg/skill_50_noBG",
                    vfxSheetName: "Vfx_Mage_Necromancer_3"),

                // Tier 2: 세이지 — Starcaller VFX
                CreateSkillSO("sage_arcanemissile", "비전 화살", "범위 3 감속 기본공격 ATK 320% x 2히트",
                    Core.SkillType.Active, 80, 2, 0f, 0f, 3.2f, 3f, 2,
                    effect: Core.SkillEffect.Slow, slowRate: 0.3f, ccDuration: 2f,
                    iconName: "Skill_nobg/skill_120_noBG",
                    vfxSheetName: "Vfx_Mage_Starcaller_1"),

                CreateSkillSO("sage_timewarp", "시간 왜곡", "공격속도 +20%, 쿨타임 감소 +10%",
                    Core.SkillType.Passive, 90, 2, 0f, 0f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.2f, buffCooldownReduceRate: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/44_obsession_nobg"),

                CreateSkillSO("sage_dimensionrift", "차원 균열", "12초간 ATK +35%, 치명타 +20%",
                    Core.SkillType.Buff, 100, 2, 15f, 12f, 0f, 0f, 1,
                    buffAtkRate: 0.35f, buffCritRate: 0.2f,
                    iconName: "Bonus/Bonus_skills/Nobg/28_portal_nobg",
                    vfxSheetName: "Vfx_Mage_Starcaller_2"),

                CreateSkillSO("sage_meteorshower", "유성우", "범위 9에 ATK 1200% + 화상 5초(80%)",
                    Core.SkillType.Awakening, 110, 2, 20f, 0f, 12f, 9f, 1,
                    effect: Core.SkillEffect.Burn, dotDamageRate: 0.8f, ccDuration: 5f,
                    iconName: "Skill_nobg/skill_460_noBG",
                    vfxSheetName: "Vfx_Mage_Starcaller_3"),

                // Tier 3: 룬마스터 — Warlock VFX
                CreateSkillSO("runemaster_runeblast", "룬 폭발", "범위 4 룬 기본공격 ATK 400% x 2히트 + 화상",
                    Core.SkillType.Active, 120, 3, 0f, 0f, 4f, 4f, 2,
                    effect: Core.SkillEffect.Burn, dotDamageRate: 0.4f, ccDuration: 3f,
                    iconName: "Bonus/Skills_upgrates/Nobg/35_wand_shot_nobg",
                    vfxSheetName: "Vfx_Mage_Warlock_1"),

                CreateSkillSO("runemaster_runemastery", "룬 마스터리", "ATK +30%, 쿨타임 감소 +15%",
                    Core.SkillType.Passive, 130, 3, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.3f, buffCooldownReduceRate: 0.15f,
                    iconName: "Bonus/Skills_upgrates/Nobg/36_wand_shot_nobg"),

                CreateSkillSO("runemaster_absolutedomain", "절대 영역", "15초간 ATK +50%, 치명타 +25%",
                    Core.SkillType.Buff, 140, 3, 15f, 15f, 0f, 0f, 1,
                    buffAtkRate: 0.5f, buffCritRate: 0.25f,
                    iconName: "Skill_nobg/skill_280_noBG",
                    vfxSheetName: "Vfx_Mage_Warlock_2"),

                CreateSkillSO("runemaster_apocalypse", "종말의 룬", "범위 10에 ATK 2200% + 빙결 4초 + 자힐 15%",
                    Core.SkillType.Awakening, 150, 3, 20f, 10f, 22f, 10f, 1,
                    buffAtkRate: 0.25f, buffCritRate: 0.25f,
                    effect: Core.SkillEffect.Freeze, ccDuration: 4f, healRatio: 0.15f,
                    iconName: "Skill_nobg/skill_200_noBG",
                    vfxSheetName: "Vfx_Mage_Warlock_3"),

                // Tier 4: 아크메이지 (Lv160/170/180/190) — Gothicvania/ImpactsExplosions VFX
                CreateSkillSO("archmage_voidbolt", "공허의 화살", "범위 5 관통 ATK 550% x 3히트 + 빙결 2초",
                    Core.SkillType.Active, 160, 4, 0f, 0f, 5.5f, 5f, 3,
                    effect: Core.SkillEffect.Freeze, ccDuration: 2f,
                    iconName: "Skill_nobg/skill_120_noBG",
                    vfxSheetName: "Vfx_Common_Gothicvania_1"),

                CreateSkillSO("archmage_transcendence", "마력 초월", "ATK +35%, 쿨타임 감소 +20%, 공격속도 +20%",
                    Core.SkillType.Passive, 170, 4, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.35f, buffCooldownReduceRate: 0.2f, buffAtkSpdRate: 0.2f,
                    iconName: "Bonus/Bonus_skills/Nobg/62_light_nobg"),

                CreateSkillSO("archmage_cosmicfield", "우주의 영역", "20초간 ATK +60%, 치명타 +25%, 치명타 데미지 +35%",
                    Core.SkillType.Buff, 180, 4, 18f, 20f, 0f, 0f, 1,
                    buffAtkRate: 0.6f, buffCritRate: 0.25f, buffCritDmgRate: 0.35f,
                    iconName: "Bonus/Bonus_skills/Nobg/28_portal_nobg",
                    vfxSheetName: "Vfx_Common_Gothicvania_2"),

                CreateSkillSO("archmage_bigbang", "빅뱅", "범위 12에 ATK 3200% + 화상 6초(100%) + 빙결 3초 + 자힐 20%",
                    Core.SkillType.Awakening, 190, 4, 25f, 12f, 32f, 12f, 1,
                    effect: Core.SkillEffect.Burn, dotDamageRate: 1f, ccDuration: 6f, healRatio: 0.2f,
                    iconName: "Skill_nobg/skill_460_noBG",
                    vfxSheetName: "Vfx_ImpactsExplosions_Explosions_1"),

                // Tier 4 전용 패시브 (아크메이지)
                CreateSkillSO("archmage_manashield", "마나 실드", "MaxHP +12%, DEF +10%",
                    Core.SkillType.Passive, 165, 4, 0f, 0f, 0f, 0f, 1,
                    buffDefRate: 0.1f, iconName: "Bonus/Bonus_skills/Nobg/62_light_nobg"),

                CreateSkillSO("archmage_arcanemind", "비전의 정신", "ATK +15%, 치명타 +10%",
                    Core.SkillType.Passive, 175, 4, 0f, 0f, 0f, 0f, 1,
                    buffAtkRate: 0.15f, buffCritRate: 0.1f,
                    iconName: "Bonus/Bonus_skills/Nobg/44_obsession_nobg"),

                CreateSkillSO("archmage_elementalmastery", "원소 마스터리", "공격속도 +12%, 쿨타임 감소 +10%",
                    Core.SkillType.Passive, 185, 4, 0f, 0f, 0f, 0f, 1,
                    buffAtkSpdRate: 0.12f, buffCooldownReduceRate: 0.1f,
                    iconName: "Skill_nobg/skill_360_noBG"),
            };

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 마법사 스킬 SO {skills.Length}개 생성/업데이트 완료");
            return skills;
        }

        private static Data.SkillDataSO CreateSkillSO(
            string id, string displayName, string description,
            Core.SkillType type, int learnLevel, int jobTier,
            float cooldown, float duration, float dmgMult, float range, int hitCount,
            float buffAtkRate = 0f, float buffDefRate = 0f, float buffCritRate = 0f, float buffAtkSpdRate = 0f,
            Core.SkillEffect effect = Core.SkillEffect.None,
            float effectDuration = 0f, float effectValue = 0f, float healRatio = 0f,
            float buffCritDmgRate = 0f, float buffMoveSpeedRate = 0f, float buffCooldownReduceRate = 0f,
            float dotDamageRate = 0f, float ccDuration = 0f, float slowRate = 0f,
            string iconName = null, string vfxSheetName = null)
        {
            string path = $"Assets/Data/SO/Skills/Skill_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.SkillDataSO>(path);
            if (existing != null)
            {
                existing.displayName = displayName;
                existing.description = description;
                existing.skillType = type;
                existing.learnLevel = learnLevel;
                existing.jobTier = jobTier;
                existing.cooldown = cooldown;
                existing.duration = duration;
                existing.damageMultiplier = dmgMult;
                existing.range = range;
                existing.hitCount = hitCount;
                existing.buffAtkRate = buffAtkRate;
                existing.buffDefRate = buffDefRate;
                existing.buffCritRate = buffCritRate;
                existing.buffAtkSpdRate = buffAtkSpdRate;
                existing.specialEffect = effect;
                existing.effectDuration = effectDuration;
                existing.effectValue = effectValue;
                existing.buffCritDmgRate = buffCritDmgRate;
                existing.buffMoveSpeedRate = buffMoveSpeedRate;
                existing.buffCooldownReduceRate = buffCooldownReduceRate;
                existing.dotDamageRate = dotDamageRate;
                existing.ccDuration = ccDuration;
                existing.slowRate = slowRate;
                existing.healRatio = healRatio;
                existing.icon = LoadSkillIcon(iconName);
                existing.vfxSheet = LoadVfxSheet(vfxSheetName);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var so = ScriptableObject.CreateInstance<Data.SkillDataSO>();
            so.id = id;
            so.displayName = displayName;
            so.description = description;
            so.skillType = type;
            so.learnLevel = learnLevel;
            so.jobTier = jobTier;
            so.cooldown = cooldown;
            so.duration = duration;
            so.damageMultiplier = dmgMult;
            so.range = range;
            so.hitCount = hitCount;
            so.buffAtkRate = buffAtkRate;
            so.buffDefRate = buffDefRate;
            so.buffCritRate = buffCritRate;
            so.buffAtkSpdRate = buffAtkSpdRate;
            so.specialEffect = effect;
            so.effectDuration = effectDuration;
            so.effectValue = effectValue;
            so.buffCritDmgRate = buffCritDmgRate;
            so.buffMoveSpeedRate = buffMoveSpeedRate;
            so.buffCooldownReduceRate = buffCooldownReduceRate;
            so.dotDamageRate = dotDamageRate;
            so.ccDuration = ccDuration;
            so.slowRate = slowRate;
            so.healRatio = healRatio;
            so.icon = LoadSkillIcon(iconName);
            so.vfxSheet = LoadVfxSheet(vfxSheetName);

            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static Data.SpriteSheetVfxSO LoadVfxSheet(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return null;
            // VFX_Imported 우선, 기본 VFX 폴백
            var vfx = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(
                $"Assets/Data/SO/VFX_Imported/{sheetName}.asset");
            if (vfx != null) return vfx;
            return AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(
                $"Assets/Data/SO/VFX/{sheetName}.asset");
        }


        private static Sprite LoadSkillIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;

            // iconName이 경로를 포함하면 (Bonus/, Skill_nobg/) 직접 로드
            if (iconName.Contains("/"))
            {
                string[] extensions = { ".png", ".PNG" };
                foreach (var ext in extensions)
                {
                    string fullPath = $"Assets/Folder_Assets/500_skill_icons/{iconName}{ext}";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
                    if (sprite != null) return sprite;
                }
                Debug.LogWarning($"[Phase2Setup] 스킬 아이콘 '{iconName}' 을 찾을 수 없음");
                return null;
            }

            // 레거시: 단순 이름만 전달된 경우
            string[] searchPaths = new[]
            {
                $"Assets/Folder_Assets/500_skill_icons/Skill_nobg/{iconName}_noBG.png",
                $"Assets/Folder_Assets/500_skill_icons/Skill_nobg/{char.ToUpper(iconName[0])}{iconName.Substring(1)}_noBG.png",
                $"Assets/Folder_Assets/500_skill_icons/Skill_standart/{iconName}.png",
                $"Assets/Folder_Assets/500_skill_icons/Skill_standart/{char.ToUpper(iconName[0])}{iconName.Substring(1)}.png",
            };

            foreach (var path in searchPaths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            Debug.LogWarning($"[Phase2Setup] 스킬 아이콘 '{iconName}' 을 찾을 수 없음");
            return null;
        }

        // ── DamageTextManager ──

        private static void CreateDamageTextManager()
        {
            if (Object.FindFirstObjectByType<Combat.DamageTextManager>() != null)
            {
                Debug.Log("[Phase2Setup] DamageTextManager 이미 존재 — 건너뜀");
                return;
            }

            var obj = new GameObject("DamageTextManager");
            Undo.RegisterCreatedObjectUndo(obj, "Create DamageTextManager");
            obj.AddComponent<Combat.DamageTextManager>();
            Debug.Log("[Phase2Setup] DamageTextManager 생성");
        }

        // ── Debug Logger ──

        private static void CreateTestManager()
        {
            if (Object.FindFirstObjectByType<Economy.TestManager>() != null)
            {
                Debug.Log("[Phase2Setup] TestManager 이미 존재 — 건너뜀");
                return;
            }

            var obj = new GameObject("TestManager");
            Undo.RegisterCreatedObjectUndo(obj, "Create TestManager");
            obj.AddComponent<Economy.TestManager>();
            Debug.Log("[Phase2Setup] TestManager 생성");
        }

        private static void CreateDebugLogger()
        {
            if (Object.FindFirstObjectByType<Combat.CombatDebugLogger>() != null)
            {
                Debug.Log("[Phase2Setup] CombatDebugLogger 이미 존재 — 건너뜀");
                return;
            }

            var obj = new GameObject("CombatDebugLogger");
            Undo.RegisterCreatedObjectUndo(obj, "Create CombatDebugLogger");
            obj.AddComponent<Combat.CombatDebugLogger>();
            Debug.Log("[Phase2Setup] CombatDebugLogger 생성");
        }

        // ── #61. 씬 전환 이펙트 ──

        private static void CreateSceneTransitionManager()
        {
            if (Object.FindFirstObjectByType<UI.SceneTransitionManager>() != null)
            {
                Debug.Log("[Phase2Setup] SceneTransitionManager 이미 존재 — 건너뜀");
                return;
            }

            // Canvas 생성 (Overlay, 최상위 sortOrder)
            var obj = new GameObject("SceneTransitionManager");
            Undo.RegisterCreatedObjectUndo(obj, "Create SceneTransitionManager");

            var canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = obj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            obj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 페이드 오버레이 (검은 배경)
            var fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(obj.transform, false);
            var fadeImg = fadeObj.AddComponent<UnityEngine.UI.Image>();
            fadeImg.color = Color.black;
            var fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.sizeDelta = Vector2.zero;
            var fadeGroup = fadeObj.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeObj.SetActive(false);

            // 와이프 바
            var wipeObj = new GameObject("WipeBar");
            wipeObj.transform.SetParent(obj.transform, false);
            var wipeImg = wipeObj.AddComponent<UnityEngine.UI.Image>();
            wipeImg.color = Color.black;
            var wipeRect = wipeObj.GetComponent<RectTransform>();
            wipeRect.anchorMin = new Vector2(0.5f, 0f);
            wipeRect.anchorMax = new Vector2(0.5f, 1f);
            wipeRect.sizeDelta = new Vector2(1080, 0f);
            wipeRect.anchoredPosition = new Vector2(-1080, 0f);
            wipeObj.SetActive(false);

            // 원형 마스크
            var circleObj = new GameObject("CircleMask");
            circleObj.transform.SetParent(obj.transform, false);
            var circleImg = circleObj.AddComponent<UnityEngine.UI.Image>();
            circleImg.color = Color.black;
            var circleRect = circleObj.GetComponent<RectTransform>();
            circleRect.anchorMin = new Vector2(0.5f, 0.5f);
            circleRect.anchorMax = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = Vector2.zero;
            circleObj.SetActive(false);

            var manager = obj.AddComponent<UI.SceneTransitionManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("_fadeOverlay").objectReferenceValue = fadeGroup;
            so.FindProperty("_wipeBar").objectReferenceValue = wipeRect;
            so.FindProperty("_circleMask").objectReferenceValue = circleRect;
            so.FindProperty("_defaultDuration").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Phase2Setup] SceneTransitionManager 생성 (Fade/Wipe/Circle)");
        }

        // ── 6. HUD 참조 연결 ──

        private static void SetupHUD(GameObject player)
        {
            var hudPanel = Object.FindFirstObjectByType<UI.HudPanel>();
            if (hudPanel == null)
            {
                Debug.LogWarning("[Phase2Setup] HudPanel이 없습니다.");
                return;
            }

            var playerStats = player.GetComponent<Combat.CombatStats>();
            if (playerStats != null)
            {
                var hso = new SerializedObject(hudPanel);
                hso.FindProperty("_playerStats").objectReferenceValue = playerStats;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log("[Phase2Setup] HUD 참조 연결 완료");
        }

        // ── 7. 전리품 드롭 연출 ──

        private const string COIN_GEMS_PATH = "Assets/Art/Coin_Gems/";
        private const string COIN_SFX_PATH = "Assets/Audio/SFX/Dime1.ogg";

        private static void SetupLootVisuals(GameObject player)
        {
            var existing = Object.FindFirstObjectByType<Combat.LootVisualManager>();
            if (existing != null)
            {
                // 플레이어 참조만 업데이트
                var eso = new SerializedObject(existing);
                eso.FindProperty("_playerTransform").objectReferenceValue = player.transform;
                eso.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Phase2Setup] LootVisualManager 이미 존재 — 플레이어 참조 업데이트");
                return;
            }

            var obj = new GameObject("LootVisualManager");
            Undo.RegisterCreatedObjectUndo(obj, "Create LootVisualManager");
            var manager = obj.AddComponent<Combat.LootVisualManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("_playerTransform").objectReferenceValue = player.transform;
            so.FindProperty("_poolSize").intValue = 40;
            so.FindProperty("_coinVolume").floatValue = 0.3f;
            so.FindProperty("_soundCooldown").floatValue = 0.05f;

            // 사운드
            var coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(COIN_SFX_PATH);
            if (coinClip != null)
                so.FindProperty("_coinSound").objectReferenceValue = coinClip;

            // 재화별 연출 설정
            var visualsProp = so.FindProperty("_visuals");
            var entries = BuildCurrencyVisualEntries();
            visualsProp.arraySize = entries.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var elem = visualsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("type").enumValueIndex = (int)entry.type;
                elem.FindPropertyRelative("scale").floatValue = entry.scale;
                elem.FindPropertyRelative("waitTime").floatValue = entry.waitTime;
                elem.FindPropertyRelative("skipVisual").boolValue = entry.skipVisual;

                var framesProp = elem.FindPropertyRelative("frames");
                if (entry.sprites != null)
                {
                    framesProp.arraySize = entry.sprites.Length;
                    for (int j = 0; j < entry.sprites.Length; j++)
                        framesProp.GetArrayElementAtIndex(j).objectReferenceValue = entry.sprites[j];
                }
                else
                {
                    framesProp.arraySize = 0;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Phase2Setup] LootVisualManager 생성 — {entries.Count}종 재화 연출 등록");
        }

        private struct VisualEntry
        {
            public Core.CurrencyType type;
            public Sprite[] sprites;
            public float scale;
            public float waitTime;
            public bool skipVisual;
        }

        private static System.Collections.Generic.List<VisualEntry> BuildCurrencyVisualEntries()
        {
            var list = new System.Collections.Generic.List<VisualEntry>();

            // Gold: 금화 (MonedaD) - 빠른 흡수
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.Gold,
                sprites = LoadSubSprites("MonedaD.png"),
                scale = 1.2f, waitTime = 0.15f, skipVisual = false
            });

            // Ruby: 빨간 보석 - 중간 체공
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.Ruby,
                sprites = LoadSubSprites("spr_coin_roj.png"),
                scale = 1.5f, waitTime = 0.5f, skipVisual = false
            });

            // BlueDiamond: 파란 보석
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.BlueDiamond,
                sprites = LoadSubSprites("spr_coin_azu.png"),
                scale = 1.5f, waitTime = 0.5f, skipVisual = false
            });

            // RuneFragment: 초록 보석
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.RuneFragment,
                sprites = LoadSubSprites("spr_coin_strip4.png"),
                scale = 1.3f, waitTime = 0.4f, skipVisual = false
            });

            // StarCrystal: 노란 보석
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.StarCrystal,
                sprites = LoadSubSprites("spr_coin_ama.png"),
                scale = 1.3f, waitTime = 0.4f, skipVisual = false
            });

            // PotentialStone: 회색 보석 - 긴 체공 (희귀)
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.PotentialStone,
                sprites = LoadSubSprites("spr_coin_gri.png"),
                scale = 1.6f, waitTime = 0.8f, skipVisual = false
            });

            // SuperPotentialStone: 회색 보석 크게
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.SuperPotentialStone,
                sprites = LoadSubSprites("spr_coin_gri.png"),
                scale = 2.0f, waitTime = 1.0f, skipVisual = false
            });

            // WeaponTicket: 빨간 코인
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.WeaponTicket,
                sprites = LoadSubSprites("MonedaR.png"),
                scale = 1.4f, waitTime = 0.5f, skipVisual = false
            });

            // ClimbToken: 초록 보석
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.ClimbToken,
                sprites = LoadSubSprites("spr_coin_strip4.png"),
                scale = 1.3f, waitTime = 0.5f, skipVisual = false
            });

            // WeaponStone: 파란 보석
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.WeaponStone,
                sprites = LoadSubSprites("spr_coin_azu.png"),
                scale = 1.3f, waitTime = 0.4f, skipVisual = false
            });

            // HuntPoint: 비표시 (매 킬마다 발생, 시각 노이즈 방지)
            list.Add(new VisualEntry
            {
                type = Core.CurrencyType.HuntPoint,
                sprites = null,
                scale = 1f, waitTime = 0f, skipVisual = true
            });

            return list;
        }

        /// <summary>
        /// 스프라이트 시트에서 모든 서브 스프라이트를 로드한다.
        /// </summary>
        private static Sprite[] LoadSubSprites(string fileName)
        {
            string path = COIN_GEMS_PATH + fileName;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var sprites = new System.Collections.Generic.List<Sprite>();

            foreach (var asset in allAssets)
            {
                if (asset is Sprite spr)
                    sprites.Add(spr);
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[Phase2Setup] 스프라이트를 찾을 수 없음: {path}");
                return null;
            }

            // 이름순 정렬 (_0, _1, _2...)
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return sprites.ToArray();
        }

        // ── #62. 콤보 로그 HUD ──

        private static void CreateComboLogHUD()
        {
            if (Object.FindFirstObjectByType<UI.ComboLogHUD>() != null)
            {
                Debug.Log("[Phase2Setup] ComboLogHUD 이미 존재 — 건너뜀");
                return;
            }

            // HUD Canvas 찾기 또는 생성
            var canvas = FindOrCreateHUDCanvas();

            var obj = new GameObject("ComboLogHUD");
            Undo.RegisterCreatedObjectUndo(obj, "Create ComboLogHUD");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 우측 상단, TopSection(118px) 아래에 배치 — 좌측 상단 UI와 겹치지 않도록
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -125f);
            rect.sizeDelta = new Vector2(200f, 200f);

            obj.AddComponent<UI.ComboLogHUD>();

            Debug.Log("[Phase2Setup] ComboLogHUD 생성 (우측 상단 콤보 스크롤)");
        }

        // ── #63. 전투 로그 HUD ──

        private static void CreateCombatFeedHUD()
        {
            if (Object.FindFirstObjectByType<UI.CombatFeedHUD>() != null)
            {
                Debug.Log("[Phase2Setup] CombatFeedHUD 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreateHUDCanvas();

            var obj = new GameObject("CombatFeedHUD");
            Undo.RegisterCreatedObjectUndo(obj, "Create CombatFeedHUD");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 스킬바(262px) 바로 위에 배치 — 스킬 아이콘과 겹치지 않도록
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.45f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(8f, 270f);
            rect.sizeDelta = new Vector2(0f, 120f);

            obj.AddComponent<UI.CombatFeedHUD>();

            Debug.Log("[Phase2Setup] CombatFeedHUD 생성 (스킬바 위 전투 로그)");
        }

        /// <summary>
        /// HUD Canvas를 찾거나 없으면 새로 생성한다.
        /// </summary>
        private static Canvas FindOrCreateHUDCanvas()
        {
            // 기존 HUD 캔버스 찾기 (HudPanel이 있는 캔버스)
            var hudPanel = Object.FindFirstObjectByType<UI.HudPanel>();
            if (hudPanel != null)
            {
                var canvas = hudPanel.GetComponentInParent<Canvas>();
                if (canvas != null) return canvas;
            }

            // 기존 Canvas 중 sortingOrder가 낮은 것 찾기 (HUD용)
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder < 9000)
                    return c;
            }

            // 새로 생성
            var canvasObj = new GameObject("HUDCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create HUDCanvas");
            var newCanvas = canvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 1;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            Debug.Log("[Phase2Setup] HUDCanvas 생성");
            return newCanvas;
        }

        // ── CollectionBookPanel UI ──

        private static void CreateCollectionBookUI()
        {
            if (Object.FindFirstObjectByType<UI.CollectionBookPanel>() != null) return;

            var canvas = FindOrCreateHUDCanvas();

            var panelObj = new GameObject("CollectionBookPanel");
            Undo.RegisterCreatedObjectUndo(panelObj, "Create CollectionBookPanel");
            panelObj.transform.SetParent(canvas.transform, false);

            var rect = panelObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panelObj.AddComponent<CanvasGroup>();
            panelObj.AddComponent<UI.CollectionBookPanel>();
            panelObj.SetActive(false);

            Debug.Log("[Phase2Setup] CollectionBookPanel UI 생성");
        }

        // ── 8. 참조 연결 (최종) ──

        private static void LinkReferences(GameObject player, Data.CharacterDataSO charData)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
        }

        // ── 카메라 확인/생성 ──

        private static void EnsureCamera()
        {
            if (Camera.main == null)
            {
                var camObj = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
                camObj.tag = "MainCamera";
                var cam = camObj.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
                camObj.AddComponent<AudioListener>();
                Debug.Log("[Phase2Setup] Main Camera 생성");
            }

            CameraSetupEditor.SetupCamera();
        }

        // ── ArenaMap 확인 ──

        private static Combat.ArenaMap EnsureArenaMap()
        {
            var arenaMap = Object.FindFirstObjectByType<Combat.ArenaMap>();
            if (arenaMap == null)
            {
                Debug.Log("[Phase2Setup] ArenaMap이 없습니다. 자동 생성합니다...");
                var go = new GameObject("ArenaMap");
                arenaMap = go.AddComponent<Combat.ArenaMap>();
            }
            return arenaMap;
        }

        // ── 유틸리티 ──

        /// <summary>
        /// 매니저의 [SerializeField] 카탈로그 배열에 SO 에셋을 자동 연결한다.
        /// </summary>
        /// <summary>
        /// QuestManager의 _questCatalog 필드를 디스크 최신 상태로 재동기화.
        /// 가이드 퀘스트 생성/삭제 후 Scene 재로드 없이 반영하기 위한 메뉴.
        /// </summary>
        [MenuItem("mkLike/Quest/Sync Catalog")]
        public static void SyncQuestCatalogMenu()
        {
            var qm = Object.FindFirstObjectByType<Quest.QuestManager>();
            if (qm == null)
            {
                Debug.LogWarning("[Phase2Setup] QuestManager가 씬에 없음. 먼저 Phase2Setup을 실행하세요.");
                return;
            }

            ConnectCatalog<Data.QuestDataSO>(qm, "_questCatalog", "Assets/Data/SO/Quest");

            var so = new SerializedObject(qm);
            var prop = so.FindProperty("_questCatalog");
            int count = prop != null ? prop.arraySize : 0;

            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(qm.gameObject.scene);

            Debug.Log($"[Phase2Setup] QuestManager catalog 동기화 완료 — {count}개");
        }

        private static void ConnectCatalog<T>(Component manager, string fieldName, string assetFolder) where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { assetFolder });
            if (guids.Length == 0) return;

            var so = new SerializedObject(manager);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Phase2Setup] {manager.GetType().Name}에 '{fieldName}' 필드를 찾을 수 없음");
                return;
            }

            prop.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 지정 경로에 ScriptableObject가 없으면 기본값으로 생성하고 로드한다.
        /// </summary>
        private static T EnsureDefaultSO<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            // 부모 디렉토리 생성
            string folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EnsureDirectory(folder);

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 기본 SO 생성: {assetPath}");
            return instance;
        }

        /// <summary>
        /// AbilityTreeConfigSO에 최소 1개의 루트 노드를 자동 생성하여 UnlockNode가 항상 성공할 수 있도록 한다.
        /// </summary>
        private static void EnsureAbilityDefaultNodes(Data.AbilityTreeConfigSO config)
        {
            if (config == null) return;
            if (config.Nodes != null && config.Nodes.Length > 0)
            {
                // 이미 노드가 있으면 스킵
                bool hasAny = false;
                for (int i = 0; i < config.Nodes.Length; i++)
                {
                    if (config.Nodes[i] != null) { hasAny = true; break; }
                }
                if (hasAny) return;
            }

            // 3갈래 각 1개씩 루트 노드 생성 (공격/방어/유틸)
            string folder = "Assets/Data/SO/Ability/Nodes";
            EnsureDirectory(folder);

            var nodes = new System.Collections.Generic.List<Data.AbilityNodeSO>();
            CreateAbilityNode(folder, "ability_atk_root",   "공격 특성 I",   0, MkLike.Core.StatType.Atk,    5f, nodes);
            CreateAbilityNode(folder, "ability_def_root",   "방어 특성 I",   1, MkLike.Core.StatType.Def,    5f, nodes);
            CreateAbilityNode(folder, "ability_util_root",  "유틸 특성 I",   2, MkLike.Core.StatType.MaxHp,  5f, nodes);

            var so = new SerializedObject(config);
            var arr = so.FindProperty("_nodes");
            arr.arraySize = nodes.Count;
            for (int i = 0; i < nodes.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] AbilityTreeConfig 기본 노드 {nodes.Count}개 생성");
        }

        private static void CreateAbilityNode(string folder, string id, string displayName, int branchIndex, MkLike.Core.StatType stat, float bonusPercent,
            System.Collections.Generic.List<Data.AbilityNodeSO> outList)
        {
            string path = $"{folder}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.AbilityNodeSO>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<Data.AbilityNodeSO>();
                existing.id = id;
                existing.displayName = displayName;
                existing.branchIndex = branchIndex;
                existing.depth = 0;
                existing.prerequisiteNodeId = string.Empty;
                existing.cost = 1;
                existing.bonusStat = stat;
                existing.bonusPercent = bonusPercent;
                existing.bonusFlat = 0f;
                AssetDatabase.CreateAsset(existing, path);
            }
            outList.Add(existing);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        private const string ANIM_NEW_CONTROLLER = "Assets/Folder_Assets/SPUM/Res/Animation/AnimationNewController.controller";

        private static void AttachSPUMVisual(GameObject parent, string spumPrefabPath, int sortingOrder)
        {
            var spumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spumPrefabPath);
            if (spumPrefab == null)
            {
                Debug.LogWarning($"[Phase2Setup] SPUM 프리팹을 찾을 수 없음: {spumPrefabPath} → 기본 사각형 사용");
                var fallback = new GameObject("SPUMVisual");
                fallback.transform.SetParent(parent.transform, false);
                var sr = fallback.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.sortingOrder = sortingOrder;
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(spumPrefab);
            visual.name = "SPUMVisual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // Animator Controller를 AnimationNewController로 교체 (클립 이름 기반)
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                var newController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM_NEW_CONTROLLER);
                if (newController != null)
                {
                    animator.runtimeAnimatorController = newController;
                    Debug.Log($"[Phase2Setup] AnimatorController → AnimationNewController 교체 완료");
                }
            }

            // 소팅 오더 조정
            foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>())
                renderer.sortingOrder += sortingOrder;
        }

        private static Sprite CreateSquareSprite()
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        // ── 무기 SO 생성 ──

        private static void CreateWeaponDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Weapon");

            var defs = new (string id, string name, string desc,
                int baseAtk, float critRate, float atkSpeed,
                float passiveAtk, string passiveDesc, float equipAtk, float equipCrit)[]
            {
                ("weapon_steel_sword", "강철검", "평범하지만 든든한 강철검",
                    20, 0.03f, 1.0f, 0.02f, "보유 시 ATK +2%", 0.05f, 0.02f),
                ("weapon_flame_blade", "화염검", "불꽃이 깃든 마검",
                    30, 0.05f, 1.1f, 0.03f, "보유 시 ATK +3%", 0.08f, 0.03f),
                ("weapon_ice_bow", "빙결궁", "얼음의 힘이 깃든 활",
                    25, 0.08f, 1.3f, 0.02f, "보유 시 ATK +2%", 0.06f, 0.05f),
                ("weapon_storm_staff", "폭풍지팡이", "폭풍을 불러오는 지팡이",
                    35, 0.06f, 0.9f, 0.04f, "보유 시 ATK +4%", 0.10f, 0.04f),
                ("weapon_dragon_blade", "용검", "용의 비늘로 만든 전설의 검",
                    50, 0.10f, 1.2f, 0.05f, "보유 시 ATK +5%", 0.15f, 0.06f),
                ("weapon_ancient_staff", "태고의 지팡이", "태초의 마력이 응축된 지팡이",
                    60, 0.12f, 1.0f, 0.06f, "보유 시 ATK +6%", 0.18f, 0.08f),
            };

            foreach (var d in defs)
            {
                string path = $"Assets/Data/SO/Weapon/{d.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Data.WeaponDataSO>(path);

                Data.WeaponDataSO so;
                bool isNew = false;

                if (existing != null)
                {
                    so = existing;
                }
                else
                {
                    so = ScriptableObject.CreateInstance<Data.WeaponDataSO>();
                    isNew = true;
                }

                so.id = d.id;
                so.displayName = d.name;
                so.description = d.desc;
                so.icon = null;
                so.baseAtk = d.baseAtk;
                so.baseCritRate = d.critRate;
                so.baseAtkSpeed = d.atkSpeed;
                so.passiveAtkPercent = d.passiveAtk;
                so.passiveEffectDesc = d.passiveDesc;
                so.equipAtkPercent = d.equipAtk;
                so.equipCritRate = d.equipCrit;

                if (isNew)
                    AssetDatabase.CreateAsset(so, path);
                else
                    EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] WeaponDataSO {defs.Length}종 생성/갱신 완료");
        }

        // ── 던전 SO 생성 ──

        private static void CreateDungeonDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Dungeon");

            var defs = new (string id, string name, string desc,
                Data.DungeonType type, int keys, int floor,
                Core.CurrencyType mainReward, int mainAmount,
                Core.CurrencyType bonusReward, int bonusAmount,
                float time, int monsters, float difficulty)[]
            {
                ("dungeon_weapon", "무기 던전", "무기 소환권과 강화석을 획득하는 던전",
                    Data.DungeonType.Weapon, 1, 5,
                    Core.CurrencyType.WeaponTicket, 3, Core.CurrencyType.WeaponStone, 5,
                    60f, 30, 1.0f),
                ("dungeon_exp", "경험치 던전", "대량의 골드를 획득하는 던전",
                    Data.DungeonType.Experience, 1, 1,
                    Core.CurrencyType.Gold, 500, Core.CurrencyType.Gold, 0,
                    60f, 50, 0.8f),
                ("dungeon_equipment", "장비 던전", "사냥 포인트와 골드를 획득하는 던전",
                    Data.DungeonType.Equipment, 1, 10,
                    Core.CurrencyType.HuntPoint, 100, Core.CurrencyType.Gold, 200,
                    60f, 40, 1.2f),
                ("dungeon_climber", "등반자의 시련", "등반의 증표를 획득하는 고난도 던전",
                    Data.DungeonType.Climber, 2, 20,
                    Core.CurrencyType.ClimbToken, 5, Core.CurrencyType.Gold, 300,
                    90f, 25, 1.5f),
                ("dungeon_enhance", "강화 던전", "룬 조각과 별의 결정을 획득하는 던전",
                    Data.DungeonType.Enhancement, 1, 5,
                    Core.CurrencyType.RuneFragment, 10, Core.CurrencyType.StarCrystal, 3,
                    60f, 35, 1.0f),
            };

            foreach (var d in defs)
            {
                string path = $"Assets/Data/SO/Dungeon/{d.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Data.DungeonDataSO>(path);

                Data.DungeonDataSO so;
                bool isNew = false;

                if (existing != null)
                {
                    so = existing;
                }
                else
                {
                    so = ScriptableObject.CreateInstance<Data.DungeonDataSO>();
                    isNew = true;
                }

                so.id = d.id;
                so.displayName = d.name;
                so.description = d.desc;
                so.dungeonType = d.type;
                so.icon = null;
                so.requiredKeys = d.keys;
                so.requiredFloor = d.floor;
                so.mainRewardType = d.mainReward;
                so.baseRewardAmount = d.mainAmount;
                so.bonusRewardType = d.bonusReward;
                so.bonusRewardAmount = d.bonusAmount;
                so.timeLimit = d.time;
                so.monsterCount = d.monsters;
                so.difficultyMultiplier = d.difficulty;

                if (isNew)
                    AssetDatabase.CreateAsset(so, path);
                else
                    EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] DungeonDataSO {defs.Length}종 생성/갱신 완료");
        }

        // ── 퀘스트 SO 생성 ──

        private static void CreateQuestDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Quest");

            var defs = new (string id, string name, string desc,
                Core.QuestType qType, Core.QuestCondition condition, int required,
                Core.CurrencyType reward, int rewardAmt,
                Core.CurrencyType bonusReward, int bonusAmt)[]
            {
                // 메인 퀘스트
                ("quest_main_kill100", "첫 사냥", "몬스터 100마리를 처치하라",
                    Core.QuestType.Main, Core.QuestCondition.KillMonsters, 100,
                    Core.CurrencyType.Ruby, 50, Core.CurrencyType.Gold, 0),
                ("quest_main_stage10", "모험의 시작", "스테이지 10을 클리어하라",
                    Core.QuestType.Main, Core.QuestCondition.ClearStage, 10,
                    Core.CurrencyType.Ruby, 100, Core.CurrencyType.Gold, 0),
                ("quest_main_level20", "성장의 증거", "레벨 20을 달성하라",
                    Core.QuestType.Main, Core.QuestCondition.LevelUp, 20,
                    Core.CurrencyType.Ruby, 200, Core.CurrencyType.Gold, 5000),
                ("quest_main_enhance5", "장비 마스터", "장비를 5회 강화하라",
                    Core.QuestType.Main, Core.QuestCondition.EnhanceEquipment, 5,
                    Core.CurrencyType.Ruby, 150, Core.CurrencyType.Gold, 0),

                // 일일 퀘스트 (7항목 — 일일 체크리스트)
                ("quest_daily_dungeon1", "던전 1회", "던전을 1회 클리어하라",
                    Core.QuestType.Daily, Core.QuestCondition.ClearDungeon, 1,
                    Core.CurrencyType.Gold, 1000, Core.CurrencyType.Gold, 0),
                ("quest_daily_enhance1", "장비 강화 1회", "장비를 1회 강화하라",
                    Core.QuestType.Daily, Core.QuestCondition.EnhanceEquipment, 1,
                    Core.CurrencyType.Gold, 2000, Core.CurrencyType.Gold, 0),
                ("quest_daily_gacha1", "가챠 1회", "가챠를 1회 실행하라",
                    Core.QuestType.Daily, Core.QuestCondition.GachaPull, 1,
                    Core.CurrencyType.Ruby, 30, Core.CurrencyType.Gold, 0),
                ("quest_daily_skill1", "스킬 레벨업 1회", "스킬을 1회 레벨업하라",
                    Core.QuestType.Daily, Core.QuestCondition.SkillLevelUp, 1,
                    Core.CurrencyType.Gold, 1000, Core.CurrencyType.Gold, 0),
                ("quest_daily_kill100", "몬스터 100처치", "몬스터 100마리를 처치하라",
                    Core.QuestType.Daily, Core.QuestCondition.KillMonsters, 100,
                    Core.CurrencyType.Gold, 1500, Core.CurrencyType.Gold, 0),
                ("quest_daily_quest3", "퀘스트 3개 완료", "퀘스트를 3개 완료하라",
                    Core.QuestType.Daily, Core.QuestCondition.QuestComplete, 3,
                    Core.CurrencyType.Gold, 1000, Core.CurrencyType.Gold, 0),
                ("quest_daily_stat5", "스탯 분배", "스탯 포인트를 5 분배하라",
                    Core.QuestType.Daily, Core.QuestCondition.AllocateStats, 5,
                    Core.CurrencyType.Gold, 500, Core.CurrencyType.Gold, 0),

                // 주간 퀘스트
                ("quest_weekly_kill500", "주간 사냥", "몬스터 500마리를 처치하라",
                    Core.QuestType.Weekly, Core.QuestCondition.KillMonsters, 500,
                    Core.CurrencyType.Ruby, 200, Core.CurrencyType.Gold, 0),
                ("quest_weekly_enhance10", "주간 강화", "장비를 10회 강화하라",
                    Core.QuestType.Weekly, Core.QuestCondition.EnhanceEquipment, 10,
                    Core.CurrencyType.Ruby, 150, Core.CurrencyType.StarCrystal, 5),
                ("quest_weekly_dungeon5", "주간 던전", "던전을 5회 클리어하라",
                    Core.QuestType.Weekly, Core.QuestCondition.ClearDungeon, 5,
                    Core.CurrencyType.Ruby, 100, Core.CurrencyType.RuneFragment, 10),
                ("quest_weekly_gold10000", "주간 소비", "골드를 10000 소비하라",
                    Core.QuestType.Weekly, Core.QuestCondition.SpendGold, 10000,
                    Core.CurrencyType.Ruby, 250, Core.CurrencyType.Gold, 0),
            };

            foreach (var d in defs)
            {
                string path = $"Assets/Data/SO/Quest/{d.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Data.QuestDataSO>(path);

                Data.QuestDataSO so;
                bool isNew = false;

                if (existing != null)
                {
                    so = existing;
                }
                else
                {
                    so = ScriptableObject.CreateInstance<Data.QuestDataSO>();
                    isNew = true;
                }

                so.id = d.id;
                so.displayName = d.name;
                so.description = d.desc;
                so.questType = d.qType;
                so.condition = d.condition;
                so.requiredAmount = d.required;
                so.rewardType = d.reward;
                so.rewardAmount = d.rewardAmt;
                so.bonusRewardType = d.bonusReward;
                so.bonusRewardAmount = d.bonusAmt;

                if (isNew)
                    AssetDatabase.CreateAsset(so, path);
                else
                    EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] QuestDataSO {defs.Length}종 생성/갱신 완료");
        }

        // ── 유물 SO 생성: 2026-04-20 유물 시스템 완전 제거 ──

        // ── 궁수/마법사 CharacterDataSO 에셋 생성 ──

        private static Data.CharacterDataSO CreateArcherCharacterDataSO()
        {
            string path = "Assets/Data/SO/Character_Archer.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.CharacterDataSO>(path);
            if (existing != null)
            {
                existing.baseHp = 200;
                existing.baseAtk = 45;
                existing.baseDef = 6;
                existing.baseCritRate = 0.20f;
                existing.attackSpeed = 2.5f;
                existing.moveSpeed = 4.5f;
                existing.hpPerLevel = 18;
                existing.atkPerLevel = 5;
                existing.defPerLevel = 1;
                existing.spumPrefabPath = SPUM_BOWMAN;
                existing.attackAnimType = 1;
                existing.visualTint = new Color(0.85f, 1.0f, 0.85f);
                EditorUtility.SetDirty(existing);
                Debug.Log("[Phase2Setup] CharacterDataSO(궁수) 스탯 업데이트 완료");
                return existing;
            }

            EnsureDirectory("Assets/Data/SO");

            var so = ScriptableObject.CreateInstance<Data.CharacterDataSO>();
            so.id = "archer";
            so.displayName = "궁수";
            so.description = "높은 공격력과 치명타, 빠른 이동속도";
            so.baseHp = 200;
            so.baseAtk = 45;
            so.baseDef = 6;
            so.baseCritRate = 0.20f;
            so.attackSpeed = 2.5f;
            so.moveSpeed = 4.5f;
            so.hpPerLevel = 18;
            so.atkPerLevel = 5;
            so.defPerLevel = 1;
            so.spumPrefabPath = SPUM_BOWMAN;
            so.attackAnimType = 1;
            so.visualTint = new Color(0.85f, 1.0f, 0.85f);

            AssetDatabase.CreateAsset(so, path);
            Debug.Log("[Phase2Setup] CharacterDataSO(궁수) 생성 완료");
            return so;
        }

        private static Data.CharacterDataSO CreateMageCharacterDataSO()
        {
            string path = "Assets/Data/SO/Character_Mage.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.CharacterDataSO>(path);
            if (existing != null)
            {
                existing.baseHp = 180;
                existing.baseAtk = 55;
                existing.baseDef = 4;
                existing.baseCritRate = 0.10f;
                existing.attackSpeed = 2.0f;
                existing.moveSpeed = 3.5f;
                existing.hpPerLevel = 15;
                existing.atkPerLevel = 6;
                existing.defPerLevel = 1;
                existing.spumPrefabPath = SPUM_MAGICIANMAN;
                existing.attackAnimType = 2;
                existing.visualTint = new Color(0.9f, 0.85f, 1.0f);
                EditorUtility.SetDirty(existing);
                Debug.Log("[Phase2Setup] CharacterDataSO(마법사) 스탯 업데이트 완료");
                return existing;
            }

            EnsureDirectory("Assets/Data/SO");

            var so = ScriptableObject.CreateInstance<Data.CharacterDataSO>();
            so.id = "mage";
            so.displayName = "마법사";
            so.description = "최고의 화력, 낮은 방어력과 체력";
            so.baseHp = 180;
            so.baseAtk = 55;
            so.baseDef = 4;
            so.baseCritRate = 0.10f;
            so.attackSpeed = 2.0f;
            so.moveSpeed = 3.5f;
            so.hpPerLevel = 15;
            so.atkPerLevel = 6;
            so.defPerLevel = 1;
            so.spumPrefabPath = SPUM_MAGICIANMAN;
            so.attackAnimType = 2;
            so.visualTint = new Color(0.9f, 0.85f, 1.0f);

            AssetDatabase.CreateAsset(so, path);
            Debug.Log("[Phase2Setup] CharacterDataSO(마법사) 생성 완료");
            return so;
        }

        // ── 가챠 풀 SO 생성 ──

        /// <summary>
        /// 장비 EquipmentDataSO 에셋 14종 생성.
        /// </summary>
        // Admurin's Pixel Items 기본 경로
        private const string ADMURIN = "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/";
        private const string ADMURIN_WEAPON = ADMURIN + "Armory/Singles/Weapon Singles/";
        private const string ADMURIN_ARMOR = ADMURIN + "Armory/Singles/Armor Singles/";
        private const string ADMURIN_RING = ADMURIN + "Rings/Singles/Variants/";

        private static void CreateEquipmentDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Equipment");

            var equipDefs = new (string id, string name, Core.EquipmentSlot slot, int atk, int hp, int def, float crit, float atkSpd, string iconPath)[]
            {
                // 무기 (ATK 중심)
                ("weapon_iron_sword",       "철검",       Core.EquipmentSlot.Weapon,     8,  0,  0,  0f,     0f,    ADMURIN_WEAPON + "Iron/Iron_Weapon1.png"),
                ("weapon_steel_blade",      "강철 대검",  Core.EquipmentSlot.Weapon,    14,  0,  0,  0.01f,  0f,    ADMURIN_WEAPON + "Steel/Steel_Weapon7.png"),
                ("weapon_mithril_sword",    "미스릴 검",  Core.EquipmentSlot.Weapon,    22,  0,  0,  0.02f,  0f,    ADMURIN_WEAPON + "Platinum/Platinum_Weapon1.png"),
                // 상의 (HP + DEF)
                ("top_leather",             "가죽 갑옷",  Core.EquipmentSlot.Top,        0, 30,  4,  0f,     0f,    ADMURIN_ARMOR + "Copper/Copper_Chestplate1.png"),
                ("top_chain",               "사슬 갑옷",  Core.EquipmentSlot.Top,        0, 50,  8,  0f,     0f,    ADMURIN_ARMOR + "Iron/Iron_Chestplate1.png"),
                // 투구 (DEF + HP)
                ("helmet_leather_cap",      "가죽 모자",  Core.EquipmentSlot.Helmet,     0, 15,  3,  0f,     0f,    ADMURIN_ARMOR + "Copper/Copper_Helmet1.png"),
                ("helmet_iron_helm",        "철 투구",    Core.EquipmentSlot.Helmet,     0, 25,  6,  0f,     0f,    ADMURIN_ARMOR + "Iron/Iron_Helmet1.png"),
                // 장갑 (ATK + CritRate)
                ("gloves_cloth",            "천 장갑",    Core.EquipmentSlot.Gloves,     3,  0,  0,  0.02f,  0f,    ADMURIN_ARMOR + "Copper/Copper_Gloves1.png"),
                ("gloves_leather",          "가죽 장갑",  Core.EquipmentSlot.Gloves,     5,  0,  0,  0.03f,  0f,    ADMURIN_ARMOR + "Iron/Iron_Gloves1.png"),
                // 신발 (DEF + HP)
                ("boots_sandals",           "가죽 부츠",  Core.EquipmentSlot.Boots,      0, 10,  2,  0f,     0f,    ADMURIN_ARMOR + "Copper/Copper_Boots1.png"),
                ("boots_iron",              "철 장화",    Core.EquipmentSlot.Boots,      0, 20,  4,  0f,     0f,    ADMURIN_ARMOR + "Iron/Iron_Boots1.png"),
                // 반지 (CritRate + AtkSpd)
                ("ring_silver",             "은 반지",    Core.EquipmentSlot.Ring,        2,  0,  0,  0.03f,  0.05f, ADMURIN_RING + "Iron/529.png"),
                // 목걸이 (HP + CritRate)
                ("necklace_amulet",         "부적 목걸이", Core.EquipmentSlot.Necklace,    0, 20,  0,  0.02f,  0.08f, ADMURIN_RING + "Gold/100.png"),
                // 얼굴장식 (ATK + CritRate)
                ("face_earring",            "마력 귀걸이", Core.EquipmentSlot.FaceAccessory, 4, 0, 0, 0.04f,  0.03f, ADMURIN_RING + "Cobalt/1585.png"),
            };

            foreach (var def in equipDefs)
            {
                string path = $"Assets/Data/SO/Equipment/Equipment_{def.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Data.EquipmentDataSO>(path);

                Data.EquipmentDataSO so;
                bool isNew = false;

                if (existing != null)
                {
                    so = existing;
                }
                else
                {
                    so = ScriptableObject.CreateInstance<Data.EquipmentDataSO>();
                    isNew = true;
                }

                so.id = def.id;
                so.displayName = def.name;
                so.slot = def.slot;
                so.baseAtk = def.atk;
                so.baseHp = def.hp;
                so.baseDef = def.def;
                so.baseCritRate = def.crit;
                so.baseAtkSpd = def.atkSpd;

                // 아이콘 로드
                var iconSpr = AssetDatabase.LoadAssetAtPath<Sprite>(def.iconPath);
                if (iconSpr == null)
                {
                    // 텍스처 임포터 설정 후 재시도
                    var imp = AssetImporter.GetAtPath(def.iconPath) as TextureImporter;
                    if (imp != null)
                    {
                        imp.textureType = TextureImporterType.Sprite;
                        imp.spritePixelsPerUnit = 32;
                        imp.filterMode = FilterMode.Point;
                        imp.SaveAndReimport();
                        iconSpr = AssetDatabase.LoadAssetAtPath<Sprite>(def.iconPath);
                    }
                }
                so.icon = iconSpr;

                if (isNew)
                    AssetDatabase.CreateAsset(so, path);
                else
                    EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] EquipmentDataSO {equipDefs.Length}종 생성/갱신 완료 (아이콘 포함)");
        }

        /// <summary>가챠 풀 SO 생성 (장비/무기/유물). 외부에서 재생성 시 호출 가능.</summary>
        public static void CreateGachaPools() => CreateGachaPoolAssets();

        private static void CreateGachaPoolAssets()
        {
            EnsureDirectory("Assets/Data/SO");

            // 장비 가챠 풀 (14종 장비 × 등급별 가중치)
            if (AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>("Assets/Data/SO/GachaPool_Equipment.asset") == null)
            {
                var pool = ScriptableObject.CreateInstance<Data.GachaPoolSO>();

                // 14종 장비 ID (무기3 + 투구2 + 상의2 + 장갑2 + 신발2 + 반지1 + 목걸이1 + 귀걸이1)
                string[] equipIds =
                {
                    "weapon_iron_sword", "weapon_steel_blade", "weapon_mithril_sword",
                    "helmet_leather_cap", "helmet_iron_helm",
                    "top_leather", "top_chain",
                    "gloves_cloth", "gloves_leather",
                    "boots_sandals", "boots_iron",
                    "ring_silver", "necklace_amulet", "face_earring"
                };

                // 등급별 가중치 (전체 확률: Normal 45%, Rare 30%, Epic 15%, Unique 7%, Legendary 2.5%, Mythic 0.5%)
                var gradeWeights = new (string grade, float weight)[]
                {
                    ("Normal", 45f), ("Rare", 30f), ("Epic", 15f),
                    ("Unique", 7f), ("Legendary", 2.5f), ("Mythic", 0.5f)
                };

                var entries = new System.Collections.Generic.List<Data.GachaEntry>();
                foreach (var id in equipIds)
                {
                    foreach (var gw in gradeWeights)
                    {
                        entries.Add(new Data.GachaEntry
                        {
                            itemId = id,
                            grade = gw.grade,
                            weight = gw.weight / equipIds.Length
                        });
                    }
                }

                pool.entries = entries.ToArray();
                pool.tenPullGuaranteeGrade = "Rare";
                pool.summonLevels = CreateDefaultSummonLevels();

                AssetDatabase.CreateAsset(pool, "Assets/Data/SO/GachaPool_Equipment.asset");
                Debug.Log($"[Phase2Setup] GachaPool_Equipment SO 생성 ({entries.Count} 항목)");
            }

            // 무기 가챠 풀 (6종 무기 × 7등급 + 17레벨 확률 테이블)
            if (AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>("Assets/Data/SO/GachaPool_Weapon.asset") == null)
            {
                var pool = ScriptableObject.CreateInstance<Data.GachaPoolSO>();

                string[] weaponIds =
                {
                    "weapon_steel_sword", "weapon_flame_blade", "weapon_ice_bow",
                    "weapon_storm_staff", "weapon_dragon_blade", "weapon_ancient_staff"
                };

                string[] grades = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic", "Ancient" };

                var weaponEntries = new System.Collections.Generic.List<Data.GachaEntry>();
                foreach (var id in weaponIds)
                {
                    foreach (var g in grades)
                    {
                        weaponEntries.Add(new Data.GachaEntry
                        {
                            itemId = id,
                            grade = g,
                            weight = 1f // 등급 내 균등 분배
                        });
                    }
                }

                pool.entries = weaponEntries.ToArray();
                pool.tenPullGuaranteeGrade = "Rare";
                pool.summonLevels = CreateWeaponGachaSummonLevels();

                AssetDatabase.CreateAsset(pool, "Assets/Data/SO/GachaPool_Weapon.asset");
                Debug.Log($"[Phase2Setup] GachaPool_Weapon SO 생성 ({weaponEntries.Count} 항목, 17 소환 레벨)");
            }

            // 유물 가챠 풀 (8종 유물 × 3등급 가중치)
            if (AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>("Assets/Data/SO/GachaPool_Relic.asset") == null)
            {
                var pool = ScriptableObject.CreateInstance<Data.GachaPoolSO>();

                // Epic 70% (4종, 각 17.5), Unique 25% (2종, 각 12.5), Legendary 5% (2종, 각 2.5)
                var relicEntries = new System.Collections.Generic.List<Data.GachaEntry>
                {
                    // Epic (70%)
                    new Data.GachaEntry { itemId = "relic_flame_ring",      grade = "Epic", weight = 17.5f },
                    new Data.GachaEntry { itemId = "relic_guardian_shield", grade = "Epic", weight = 17.5f },
                    new Data.GachaEntry { itemId = "relic_hunters_eye",    grade = "Epic", weight = 17.5f },
                    new Data.GachaEntry { itemId = "relic_shadow_cloak",   grade = "Epic", weight = 17.5f },
                    // Unique (25%)
                    new Data.GachaEntry { itemId = "relic_star_pendant",   grade = "Unique", weight = 12.5f },
                    new Data.GachaEntry { itemId = "relic_storm_crown",    grade = "Unique", weight = 12.5f },
                    // Legendary (5%)
                    new Data.GachaEntry { itemId = "relic_ancient_sword",  grade = "Legendary", weight = 2.5f },
                    new Data.GachaEntry { itemId = "relic_dragon_heart",   grade = "Legendary", weight = 2.5f },
                };

                pool.entries = relicEntries.ToArray();
                pool.tenPullGuaranteeGrade = "Epic";
                pool.summonLevels = CreateDefaultSummonLevels();

                AssetDatabase.CreateAsset(pool, "Assets/Data/SO/GachaPool_Relic.asset");
                Debug.Log($"[Phase2Setup] GachaPool_Relic SO 생성 ({relicEntries.Count} 항목)");
            }

            AssetDatabase.SaveAssets();
        }

        private static Data.SummonLevelData[] CreateDefaultSummonLevels()
        {
            return new Data.SummonLevelData[]
            {
                new Data.SummonLevelData { requiredPulls = 0,   gradeWeights = new float[] { 50f, 30f, 15f, 4f, 0.9f, 0.1f, 0f } },
                new Data.SummonLevelData { requiredPulls = 30,  gradeWeights = new float[] { 45f, 30f, 18f, 5.5f, 1.3f, 0.2f, 0f } },
                new Data.SummonLevelData { requiredPulls = 80,  gradeWeights = new float[] { 38f, 28f, 22f, 8f, 3f, 1f, 0f } },
                new Data.SummonLevelData { requiredPulls = 150, gradeWeights = new float[] { 30f, 25f, 25f, 12f, 5.5f, 2.5f, 0f } },
                new Data.SummonLevelData { requiredPulls = 300, gradeWeights = new float[] { 20f, 20f, 28f, 18f, 9f, 5f, 0f } },
            };
        }

        /// <summary>
        /// 무기 가챠 전용 17레벨 확률 테이블.
        /// 레퍼런스: 메이플 키우기 무기 가챠 (10,000,000 기준 → /10000 스케일).
        /// </summary>
        private static Data.SummonLevelData[] CreateWeaponGachaSummonLevels()
        {
            return new Data.SummonLevelData[]
            {
                // Lv1: 0 pulls — Normal + Rare(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 0,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 368f), GT("Normal", 3, 276f), GT("Normal", 2, 184f), GT("Normal", 1, 92f),
                        GT("Rare", 4, 56f), GT("Rare", 3, 24f),
                    }
                },
                // Lv2: 30 pulls — Normal + Rare(T4~T1)
                new Data.SummonLevelData
                {
                    requiredPulls = 30,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 352f), GT("Normal", 3, 264f), GT("Normal", 2, 176f), GT("Normal", 1, 88f),
                        GT("Rare", 4, 60f), GT("Rare", 3, 36f), GT("Rare", 2, 18f), GT("Rare", 1, 6f),
                    }
                },
                // Lv3: 70 pulls — +Epic(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 70,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 336.8f), GT("Normal", 3, 252.6f), GT("Normal", 2, 168.4f), GT("Normal", 1, 84.2f),
                        GT("Rare", 4, 60f), GT("Rare", 3, 45f), GT("Rare", 2, 30f), GT("Rare", 1, 15f),
                        GT("Epic", 4, 5.6f), GT("Epic", 3, 2.4f),
                    }
                },
                // Lv4: 120 pulls — +Epic(T4~T1)
                new Data.SummonLevelData
                {
                    requiredPulls = 120,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 322.4f), GT("Normal", 3, 241.8f), GT("Normal", 2, 161.2f), GT("Normal", 1, 80.6f),
                        GT("Rare", 4, 70.4f), GT("Rare", 3, 52.8f), GT("Rare", 2, 35.2f), GT("Rare", 1, 17.6f),
                        GT("Epic", 4, 7.2f), GT("Epic", 3, 5.4f), GT("Epic", 2, 3.6f), GT("Epic", 1, 1.8f),
                    }
                },
                // Lv5: 200 pulls — +Unique(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 200,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 309.4f), GT("Normal", 3, 232.05f), GT("Normal", 2, 154.7f), GT("Normal", 1, 77.35f),
                        GT("Rare", 4, 80.6f), GT("Rare", 3, 60.45f), GT("Rare", 2, 40.3f), GT("Rare", 1, 20.15f),
                        GT("Epic", 4, 9.2f), GT("Epic", 3, 6.9f), GT("Epic", 2, 4.6f), GT("Epic", 1, 2.3f),
                        GT("Unique", 4, 1.4f), GT("Unique", 3, 0.6f),
                    }
                },
                // Lv6: 300 pulls — +Unique(T4~T1)
                new Data.SummonLevelData
                {
                    requiredPulls = 300,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 296.4f), GT("Normal", 3, 222.3f), GT("Normal", 2, 148.2f), GT("Normal", 1, 74.1f),
                        GT("Rare", 4, 90.8f), GT("Rare", 3, 68.1f), GT("Rare", 2, 45.4f), GT("Rare", 1, 22.7f),
                        GT("Epic", 4, 10.4f), GT("Epic", 3, 7.8f), GT("Epic", 2, 5.2f), GT("Epic", 1, 2.6f),
                        GT("Unique", 4, 3f), GT("Unique", 3, 1.8f), GT("Unique", 2, 0.9f), GT("Unique", 1, 0.3f),
                    }
                },
                // Lv7: 420 pulls — +Legendary(T4)
                new Data.SummonLevelData
                {
                    requiredPulls = 420,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 281.08f), GT("Normal", 3, 210.81f), GT("Normal", 2, 140.54f), GT("Normal", 1, 70.27f),
                        GT("Rare", 4, 102.8f), GT("Rare", 3, 77.1f), GT("Rare", 2, 51.4f), GT("Rare", 1, 25.7f),
                        GT("Epic", 4, 12.4f), GT("Epic", 3, 9.3f), GT("Epic", 2, 6.2f), GT("Epic", 1, 3.1f),
                        GT("Unique", 4, 3.6f), GT("Unique", 3, 2.7f), GT("Unique", 2, 1.8f), GT("Unique", 1, 0.9f),
                        GT("Legendary", 4, 0.3f),
                    }
                },
                // Lv8: 560 pulls — +Legendary(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 560,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 265.68f), GT("Normal", 3, 199.26f), GT("Normal", 2, 132.84f), GT("Normal", 1, 66.42f),
                        GT("Rare", 4, 114.8f), GT("Rare", 3, 86.1f), GT("Rare", 2, 57.4f), GT("Rare", 1, 28.7f),
                        GT("Epic", 4, 14.4f), GT("Epic", 3, 10.8f), GT("Epic", 2, 7.2f), GT("Epic", 1, 3.6f),
                        GT("Unique", 4, 4.8f), GT("Unique", 3, 3.6f), GT("Unique", 2, 2.4f), GT("Unique", 1, 1.2f),
                        GT("Legendary", 4, 0.56f), GT("Legendary", 3, 0.24f),
                    }
                },
                // Lv9: 720 pulls — +Legendary(T4~T2)
                new Data.SummonLevelData
                {
                    requiredPulls = 720,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 250.16f), GT("Normal", 3, 187.62f), GT("Normal", 2, 125.08f), GT("Normal", 1, 62.54f),
                        GT("Rare", 4, 126.8f), GT("Rare", 3, 95.1f), GT("Rare", 2, 63.4f), GT("Rare", 1, 31.7f),
                        GT("Epic", 4, 16.4f), GT("Epic", 3, 12.3f), GT("Epic", 2, 8.2f), GT("Epic", 1, 4.1f),
                        GT("Unique", 4, 6f), GT("Unique", 3, 4.5f), GT("Unique", 2, 3f), GT("Unique", 1, 1.5f),
                        GT("Legendary", 4, 0.96f), GT("Legendary", 3, 0.48f), GT("Legendary", 2, 0.16f),
                    }
                },
                // Lv10: 900 pulls — +Legendary(T4~T1)
                new Data.SummonLevelData
                {
                    requiredPulls = 900,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 234.64f), GT("Normal", 3, 175.98f), GT("Normal", 2, 117.32f), GT("Normal", 1, 58.66f),
                        GT("Rare", 4, 138.8f), GT("Rare", 3, 104.1f), GT("Rare", 2, 69.4f), GT("Rare", 1, 34.7f),
                        GT("Epic", 4, 18.4f), GT("Epic", 3, 13.8f), GT("Epic", 2, 9.2f), GT("Epic", 1, 4.6f),
                        GT("Unique", 4, 7.2f), GT("Unique", 3, 5.4f), GT("Unique", 2, 3.6f), GT("Unique", 1, 1.8f),
                        GT("Legendary", 4, 1.2f), GT("Legendary", 3, 0.72f), GT("Legendary", 2, 0.36f), GT("Legendary", 1, 0.12f),
                    }
                },
                // Lv11: 1100 pulls — +Mythic(T4)
                new Data.SummonLevelData
                {
                    requiredPulls = 1100,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 219.08f), GT("Normal", 3, 164.31f), GT("Normal", 2, 109.54f), GT("Normal", 1, 54.77f),
                        GT("Rare", 4, 150.8f), GT("Rare", 3, 113.1f), GT("Rare", 2, 75.4f), GT("Rare", 1, 37.7f),
                        GT("Epic", 4, 20.4f), GT("Epic", 3, 15.3f), GT("Epic", 2, 10.2f), GT("Epic", 1, 5.1f),
                        GT("Unique", 4, 8.4f), GT("Unique", 3, 6.3f), GT("Unique", 2, 4.2f), GT("Unique", 1, 2.1f),
                        GT("Legendary", 4, 1.28f), GT("Legendary", 3, 1.056f), GT("Legendary", 2, 0.64f), GT("Legendary", 1, 0.224f),
                        GT("Mythic", 4, 0.1f),
                    }
                },
                // Lv12: 1350 pulls — +Mythic(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 1350,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 203.68f), GT("Normal", 3, 152.76f), GT("Normal", 2, 101.84f), GT("Normal", 1, 50.92f),
                        GT("Rare", 4, 162.8f), GT("Rare", 3, 122.1f), GT("Rare", 2, 81.4f), GT("Rare", 1, 40.7f),
                        GT("Epic", 4, 22.4f), GT("Epic", 3, 16.8f), GT("Epic", 2, 11.2f), GT("Epic", 1, 5.6f),
                        GT("Unique", 4, 9.6f), GT("Unique", 3, 7.2f), GT("Unique", 2, 4.8f), GT("Unique", 1, 2.4f),
                        GT("Legendary", 4, 1.44f), GT("Legendary", 3, 1.188f), GT("Legendary", 2, 0.72f), GT("Legendary", 1, 0.252f),
                        GT("Mythic", 4, 0.14f), GT("Mythic", 3, 0.06f),
                    }
                },
                // Lv13: 1650 pulls — +Mythic(T4~T2)
                new Data.SummonLevelData
                {
                    requiredPulls = 1650,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 188.2f), GT("Normal", 3, 141.15f), GT("Normal", 2, 94.1f), GT("Normal", 1, 47.05f),
                        GT("Rare", 4, 174.8f), GT("Rare", 3, 131.1f), GT("Rare", 2, 87.4f), GT("Rare", 1, 43.7f),
                        GT("Epic", 4, 24.4f), GT("Epic", 3, 18.3f), GT("Epic", 2, 12.2f), GT("Epic", 1, 6.1f),
                        GT("Unique", 4, 10.8f), GT("Unique", 3, 8.1f), GT("Unique", 2, 5.4f), GT("Unique", 1, 2.7f),
                        GT("Legendary", 4, 1.6f), GT("Legendary", 3, 1.32f), GT("Legendary", 2, 0.8f), GT("Legendary", 1, 0.28f),
                        GT("Mythic", 4, 0.3f), GT("Mythic", 3, 0.15f), GT("Mythic", 2, 0.05f),
                    }
                },
                // Lv14: 2000 pulls — +Mythic(T4~T1)
                new Data.SummonLevelData
                {
                    requiredPulls = 2000,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 172.92f), GT("Normal", 3, 129.69f), GT("Normal", 2, 86.46f), GT("Normal", 1, 43.23f),
                        GT("Rare", 4, 186.8f), GT("Rare", 3, 140.1f), GT("Rare", 2, 93.4f), GT("Rare", 1, 46.7f),
                        GT("Epic", 4, 26.4f), GT("Epic", 3, 19.8f), GT("Epic", 2, 13.2f), GT("Epic", 1, 6.6f),
                        GT("Unique", 4, 12f), GT("Unique", 3, 9f), GT("Unique", 2, 6f), GT("Unique", 1, 3f),
                        GT("Legendary", 4, 1.6f), GT("Legendary", 3, 1.32f), GT("Legendary", 2, 0.8f), GT("Legendary", 1, 0.28f),
                        GT("Mythic", 4, 0.35f), GT("Mythic", 3, 0.21f), GT("Mythic", 2, 0.105f), GT("Mythic", 1, 0.035f),
                    }
                },
                // Lv15: 2500 pulls — +Ancient(T4)
                new Data.SummonLevelData
                {
                    requiredPulls = 2500,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 157.548f), GT("Normal", 3, 118.161f), GT("Normal", 2, 78.774f), GT("Normal", 1, 39.387f),
                        GT("Rare", 4, 198.8f), GT("Rare", 3, 149.1f), GT("Rare", 2, 99.4f), GT("Rare", 1, 49.7f),
                        GT("Epic", 4, 28.4f), GT("Epic", 3, 21.3f), GT("Epic", 2, 14.2f), GT("Epic", 1, 7.1f),
                        GT("Unique", 4, 13.2f), GT("Unique", 3, 9.9f), GT("Unique", 2, 6.6f), GT("Unique", 1, 3.3f),
                        GT("Legendary", 4, 1.6f), GT("Legendary", 3, 1.32f), GT("Legendary", 2, 0.8f), GT("Legendary", 1, 0.28f),
                        GT("Mythic", 4, 0.55f), GT("Mythic", 3, 0.33f), GT("Mythic", 2, 0.143f), GT("Mythic", 1, 0.077f),
                        GT("Ancient", 4, 0.03f),
                    }
                },
                // Lv16: 3200 pulls — +Ancient(T4,T3)
                new Data.SummonLevelData
                {
                    requiredPulls = 3200,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 157.368f), GT("Normal", 3, 118.026f), GT("Normal", 2, 78.684f), GT("Normal", 1, 39.342f),
                        GT("Rare", 4, 198.8f), GT("Rare", 3, 149.1f), GT("Rare", 2, 99.4f), GT("Rare", 1, 49.7f),
                        GT("Epic", 4, 28.4f), GT("Epic", 3, 21.3f), GT("Epic", 2, 14.2f), GT("Epic", 1, 7.1f),
                        GT("Unique", 4, 13.2f), GT("Unique", 3, 9.9f), GT("Unique", 2, 6.6f), GT("Unique", 1, 3.3f),
                        GT("Legendary", 4, 1.6f), GT("Legendary", 3, 1.2f), GT("Legendary", 2, 0.8f), GT("Legendary", 1, 0.4f),
                        GT("Mythic", 4, 0.6f), GT("Mythic", 3, 0.495f), GT("Mythic", 2, 0.3f), GT("Mythic", 1, 0.105f),
                        GT("Ancient", 4, 0.06f), GT("Ancient", 3, 0.02f),
                    }
                },
                // Lv17: 4200 pulls (최대) — +Ancient(T4~T2)
                new Data.SummonLevelData
                {
                    requiredPulls = 4200,
                    gradeTierWeights = new Data.GradeTierEntry[]
                    {
                        GT("Normal", 4, 157.188f), GT("Normal", 3, 117.891f), GT("Normal", 2, 78.594f), GT("Normal", 1, 39.297f),
                        GT("Rare", 4, 198.8f), GT("Rare", 3, 149.1f), GT("Rare", 2, 99.4f), GT("Rare", 1, 49.7f),
                        GT("Epic", 4, 28.4f), GT("Epic", 3, 21.3f), GT("Epic", 2, 14.2f), GT("Epic", 1, 7.1f),
                        GT("Unique", 4, 13.2f), GT("Unique", 3, 9.9f), GT("Unique", 2, 6.6f), GT("Unique", 1, 3.3f),
                        GT("Legendary", 4, 1.6f), GT("Legendary", 3, 1.2f), GT("Legendary", 2, 0.8f), GT("Legendary", 1, 0.4f),
                        GT("Mythic", 4, 0.76f), GT("Mythic", 3, 0.627f), GT("Mythic", 2, 0.38f), GT("Mythic", 1, 0.133f),
                        GT("Ancient", 4, 0.0845f), GT("Ancient", 3, 0.0325f), GT("Ancient", 2, 0.013f),
                    }
                },
            };
        }

        /// <summary>GradeTierEntry 생성 헬퍼</summary>
        private static Data.GradeTierEntry GT(string grade, int tier, float weight)
        {
            return new Data.GradeTierEntry { grade = grade, tier = tier, weight = weight };
        }

        private static Data.SpriteSheetVfxSO LoadVfxWithFallback(string primaryPath, string fallbackPath)
        {
            var vfx = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(primaryPath);
            if (vfx != null) return vfx;
            return AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(fallbackPath);
        }

        // ── 가이드 퀘스트 SO 50종 생성 ──

        private static void CreateGuideQuestDataSOs()
        {
            EnsureDirectory("Assets/Data/SO/Quest/Guide");

            // (id, name, desc, condition, required, rewardType, rewardAmt, bonusType, bonusAmt, chainIndex)
            var defs = new (string id, string name, string desc,
                Core.QuestCondition condition, int required,
                Core.CurrencyType reward, int rewardAmt,
                Core.CurrencyType bonusReward, int bonusAmt, int chainIndex)[]
            {
                // 초반 10개: 파격적 보상
                ("guide_g01", "첫 전투 승리", "몬스터 1마리를 처치하라", Core.QuestCondition.KillMonsters, 1, Core.CurrencyType.Gold, 200, Core.CurrencyType.Gold, 0, 0),
                ("guide_g02", "Lv.5 달성", "레벨 5에 도달하라", Core.QuestCondition.LevelUp, 5, Core.CurrencyType.Gold, 1000, Core.CurrencyType.Ruby, 50, 1),
                ("guide_g03", "장비 1개 장착", "장비를 1개 장착하라", Core.QuestCondition.EquipItem, 1, Core.CurrencyType.Gold, 500, Core.CurrencyType.Gold, 0, 2),
                ("guide_g04", "스킬 1개 레벨업", "스킬을 1회 레벨업하라", Core.QuestCondition.SkillLevelUp, 1, Core.CurrencyType.Gold, 500, Core.CurrencyType.Gold, 0, 3),
                ("guide_g05", "1-5 스테이지 클리어", "스테이지 1-5를 클리어하라", Core.QuestCondition.ClearStage, 5, Core.CurrencyType.RuneFragment, 20, Core.CurrencyType.Gold, 1000, 4),
                ("guide_g06", "장비 강화 1회", "장비를 1회 강화하라", Core.QuestCondition.EnhanceEquipment, 1, Core.CurrencyType.RuneFragment, 10, Core.CurrencyType.Gold, 500, 5),
                ("guide_g07", "Lv.10 달성", "레벨 10에 도달하라", Core.QuestCondition.LevelUp, 10, Core.CurrencyType.Ruby, 200, Core.CurrencyType.Gold, 2000, 6),
                ("guide_g08", "가챠 1회 소환", "동료 또는 무기를 1회 소환하라", Core.QuestCondition.GachaPull, 1, Core.CurrencyType.Ruby, 100, Core.CurrencyType.WeaponTicket, 10, 7),
                ("guide_g10", "챕터 1 보스 클리어", "챕터 1 보스를 클리어하라", Core.QuestCondition.ClearStage, 10, Core.CurrencyType.Ruby, 300, Core.CurrencyType.Gold, 3000, 9),

                // 중반 10개: 던전/강화 유도
                ("guide_g11", "던전 1회 클리어", "던전을 1회 클리어하라", Core.QuestCondition.ClearDungeon, 1, Core.CurrencyType.Gold, 3000, Core.CurrencyType.RuneFragment, 15, 10),
                ("guide_g12", "Lv.20 달성", "레벨 20에 도달하라", Core.QuestCondition.LevelUp, 20, Core.CurrencyType.Ruby, 300, Core.CurrencyType.Gold, 5000, 11),
                ("guide_g13", "장비 강화 5회", "장비를 총 5회 강화하라", Core.QuestCondition.EnhanceEquipment, 5, Core.CurrencyType.StarCrystal, 10, Core.CurrencyType.Gold, 2000, 12),
                ("guide_g14", "가챠 5회 소환", "가챠를 총 5회 소환하라", Core.QuestCondition.GachaPull, 5, Core.CurrencyType.Ruby, 200, Core.CurrencyType.Gold, 3000, 13),
                ("guide_g15", "스킬 3개 레벨업", "스킬을 총 3회 레벨업하라", Core.QuestCondition.SkillLevelUp, 3, Core.CurrencyType.Gold, 5000, Core.CurrencyType.Ruby, 100, 14),
                ("guide_g16", "코스튬 1개 장착", "코스튬을 1개 장착하라", Core.QuestCondition.CostumeEquip, 1, Core.CurrencyType.Ruby, 150, Core.CurrencyType.Gold, 2000, 15),
                ("guide_g17", "Lv.30 달성", "레벨 30에 도달하라", Core.QuestCondition.LevelUp, 30, Core.CurrencyType.Ruby, 500, Core.CurrencyType.Gold, 8000, 16),
                ("guide_g18", "몬스터 500마리 처치", "몬스터를 500마리 처치하라", Core.QuestCondition.KillMonsters, 500, Core.CurrencyType.Gold, 10000, Core.CurrencyType.Ruby, 100, 17),
                ("guide_g19", "던전 5회 클리어", "던전을 총 5회 클리어하라", Core.QuestCondition.ClearDungeon, 5, Core.CurrencyType.RuneFragment, 30, Core.CurrencyType.Gold, 5000, 18),
                ("guide_g20", "챕터 3 보스 클리어", "챕터 3 보스를 클리어하라", Core.QuestCondition.ClearStage, 30, Core.CurrencyType.Ruby, 500, Core.CurrencyType.WeaponTicket, 5, 19),

                // 중기 10개: 전직/스테이지 유도
                ("guide_g21", "Lv.40 달성 (1차 전직)", "레벨 40에 도달하라", Core.QuestCondition.LevelUp, 40, Core.CurrencyType.Ruby, 500, Core.CurrencyType.Gold, 10000, 20),
                ("guide_g22", "스테이지 30 돌파", "스테이지 30을 클리어하라", Core.QuestCondition.ClearStage, 30, Core.CurrencyType.Ruby, 300, Core.CurrencyType.Gold, 5000, 21),
                ("guide_g23", "가챠 10회 소환", "가챠를 총 10회 소환하라", Core.QuestCondition.GachaPull, 10, Core.CurrencyType.Ruby, 300, Core.CurrencyType.WeaponTicket, 5, 22),
                ("guide_g24", "장비 강화 10회", "장비를 총 10회 강화하라", Core.QuestCondition.EnhanceEquipment, 10, Core.CurrencyType.StarCrystal, 20, Core.CurrencyType.RuneFragment, 30, 23),
                ("guide_g25", "던전 10회 클리어", "던전을 총 10회 클리어하라", Core.QuestCondition.ClearDungeon, 10, Core.CurrencyType.Ruby, 200, Core.CurrencyType.Gold, 5000, 24),
                ("guide_g26", "Lv.50 달성", "레벨 50에 도달하라", Core.QuestCondition.LevelUp, 50, Core.CurrencyType.Ruby, 800, Core.CurrencyType.Gold, 15000, 25),
                ("guide_g27", "던전 5회 클리어", "던전을 총 5회 클리어하라", Core.QuestCondition.ClearDungeon, 5, Core.CurrencyType.Ruby, 500, Core.CurrencyType.Gold, 10000, 26),
                ("guide_g28", "골드 50000 소비", "골드를 총 50000 소비하라", Core.QuestCondition.SpendGold, 50000, Core.CurrencyType.Ruby, 300, Core.CurrencyType.Gold, 8000, 27),
                ("guide_g29", "몬스터 2000마리 처치", "몬스터를 2000마리 처치하라", Core.QuestCondition.KillMonsters, 2000, Core.CurrencyType.Gold, 20000, Core.CurrencyType.Ruby, 200, 28),
                ("guide_g30", "Lv.60 달성", "레벨 60에 도달하라", Core.QuestCondition.LevelUp, 60, Core.CurrencyType.Ruby, 1000, Core.CurrencyType.Gold, 20000, 29),

                // 후기 10개: 고급 콘텐츠
                ("guide_g31", "스테이지 50 돌파", "스테이지 50을 클리어하라", Core.QuestCondition.ClearStage, 50, Core.CurrencyType.Ruby, 1000, Core.CurrencyType.Gold, 30000, 30),
                ("guide_g32", "Lv.80 달성 (2차 전직)", "레벨 80에 도달하라", Core.QuestCondition.LevelUp, 80, Core.CurrencyType.Ruby, 1500, Core.CurrencyType.Gold, 50000, 31),
                ("guide_g33", "던전 20회 클리어", "던전을 총 20회 클리어하라", Core.QuestCondition.ClearDungeon, 20, Core.CurrencyType.RuneFragment, 50, Core.CurrencyType.StarCrystal, 20, 32),
                ("guide_g34", "장비 강화 30회", "장비를 총 30회 강화하라", Core.QuestCondition.EnhanceEquipment, 30, Core.CurrencyType.Ruby, 500, Core.CurrencyType.RuneFragment, 40, 33),
                ("guide_g35", "스테이지 70 돌파", "스테이지 70을 클리어하라", Core.QuestCondition.ClearStage, 70, Core.CurrencyType.Ruby, 500, Core.CurrencyType.Gold, 15000, 34),
                ("guide_g36", "Lv.100 달성", "레벨 100에 도달하라", Core.QuestCondition.LevelUp, 100, Core.CurrencyType.Ruby, 2000, Core.CurrencyType.Gold, 80000, 35),
                ("guide_g37", "가챠 30회 소환", "가챠를 총 30회 소환하라", Core.QuestCondition.GachaPull, 30, Core.CurrencyType.Ruby, 1500, Core.CurrencyType.Gold, 50000, 36),
                ("guide_g38", "몬스터 10000마리 처치", "몬스터를 10000마리 처치하라", Core.QuestCondition.KillMonsters, 10000, Core.CurrencyType.Gold, 100000, Core.CurrencyType.Ruby, 500, 37),
                ("guide_g39", "골드 500000 소비", "골드를 총 500000 소비하라", Core.QuestCondition.SpendGold, 500000, Core.CurrencyType.Ruby, 800, Core.CurrencyType.Gold, 30000, 38),
                ("guide_g40", "Lv.120 달성 (3차 전직)", "레벨 120에 도달하라", Core.QuestCondition.LevelUp, 120, Core.CurrencyType.Ruby, 5000, Core.CurrencyType.Gold, 150000, 39),

                // 엔드게임 10개
                ("guide_g41", "스테이지 100 돌파", "스테이지 100을 클리어하라", Core.QuestCondition.ClearStage, 100, Core.CurrencyType.Ruby, 3000, Core.CurrencyType.Gold, 80000, 40),
                ("guide_g42", "장비 강화 50회", "장비를 총 50회 강화하라", Core.QuestCondition.EnhanceEquipment, 50, Core.CurrencyType.Ruby, 1000, Core.CurrencyType.StarCrystal, 30, 41),
                ("guide_g43", "가챠 50회 소환", "가챠를 총 50회 소환하라", Core.QuestCondition.GachaPull, 50, Core.CurrencyType.Ruby, 2000, Core.CurrencyType.WeaponTicket, 10, 42),
                ("guide_g44", "던전 50회 클리어", "던전을 총 50회 클리어하라", Core.QuestCondition.ClearDungeon, 50, Core.CurrencyType.Ruby, 1500, Core.CurrencyType.RuneFragment, 60, 43),
                ("guide_g45", "Lv.140 달성", "레벨 140에 도달하라", Core.QuestCondition.LevelUp, 140, Core.CurrencyType.Ruby, 5000, Core.CurrencyType.Gold, 200000, 44),
                ("guide_g46", "스테이지 130 돌파", "스테이지 130을 클리어하라", Core.QuestCondition.ClearStage, 130, Core.CurrencyType.Ruby, 2000, Core.CurrencyType.Gold, 100000, 45),
                ("guide_g47", "던전 30회 클리어", "던전을 총 30회 클리어하라", Core.QuestCondition.ClearDungeon, 30, Core.CurrencyType.Ruby, 5000, Core.CurrencyType.RuneFragment, 80, 46),
                ("guide_g48", "몬스터 50000마리 처치", "몬스터를 50000마리 처치하라", Core.QuestCondition.KillMonsters, 50000, Core.CurrencyType.Gold, 500000, Core.CurrencyType.Ruby, 2000, 47),
                ("guide_g49", "Lv.160 달성 (4차 전직)", "레벨 160에 도달하라", Core.QuestCondition.LevelUp, 160, Core.CurrencyType.Ruby, 10000, Core.CurrencyType.Gold, 500000, 48),
                ("guide_g50", "몬스터 100000마리 처치", "몬스터를 100000마리 처치하라", Core.QuestCondition.KillMonsters, 100000, Core.CurrencyType.Ruby, 20000, Core.CurrencyType.Gold, 1000000, 49),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                string path = $"Assets/Data/SO/Quest/Guide/{d.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Data.QuestDataSO>(path);

                Data.QuestDataSO so;
                bool isNew = false;

                if (existing != null)
                {
                    so = existing;
                }
                else
                {
                    so = ScriptableObject.CreateInstance<Data.QuestDataSO>();
                    isNew = true;
                }

                so.id = d.id;
                so.displayName = d.name;
                so.description = d.desc;
                so.questType = Core.QuestType.Guide;
                so.condition = d.condition;
                so.requiredAmount = d.required;
                so.rewardType = d.reward;
                so.rewardAmount = d.rewardAmt;
                so.bonusRewardType = d.bonusReward;
                so.bonusRewardAmount = d.bonusAmt;
                so.guideChainIndex = d.chainIndex;

                // 체인 연결: 다음 퀘스트 ID
                if (i + 1 < defs.Length)
                    so.nextGuideQuestId = defs[i + 1].id;
                else
                    so.nextGuideQuestId = "";

                if (isNew)
                    AssetDatabase.CreateAsset(so, path);
                else
                    EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase2Setup] 가이드 퀘스트 SO {defs.Length}종 생성/갱신 완료");
        }

        // ── 유저 사이클 위젯 5종 생성 ──

        private static void CreateUserCycleWidgets()
        {
            CreateCpCounterWidgetInHUD();
            CreateGoalGuideWidgetInHUD();
            CreateDailyChecklistWidgetInHUD();
            CreateAcquisitionShortcutPopupInPopupParent();
            CreateEquipComparePopupInPopupParent();
            CreateReturneeGuidePopupInPopupParent();
            CreateStatCompareViewInPopupParent();
            CreateCurrencyShortagePopupInPopupParent();
        }

        private static void CreateCpCounterWidgetInHUD()
        {
            if (Object.FindFirstObjectByType<UI.CpCounterWidget>() != null)
            {
                Debug.Log("[Phase2Setup] CpCounterWidget 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreateHUDCanvas();

            var obj = new GameObject("CpCounterWidget");
            Undo.RegisterCreatedObjectUndo(obj, "Create CpCounterWidget");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 상단 중앙, TopSection 아래
            rect.anchorMin = new Vector2(0.3f, 1f);
            rect.anchorMax = new Vector2(0.7f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -60f);
            rect.sizeDelta = new Vector2(0f, 50f);

            obj.AddComponent<UI.CpCounterWidget>();

            Debug.Log("[Phase2Setup] CpCounterWidget 생성 (상단 중앙 CP 표시)");
        }

        private static void CreateGoalGuideWidgetInHUD()
        {
            if (Object.FindFirstObjectByType<UI.GoalGuideWidget>() != null)
            {
                Debug.Log("[Phase2Setup] GoalGuideWidget 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreateHUDCanvas();

            var obj = new GameObject("GoalGuideWidget");
            Undo.RegisterCreatedObjectUndo(obj, "Create GoalGuideWidget");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 우측 중앙 약간 위
            rect.anchorMin = new Vector2(1f, 0.6f);
            rect.anchorMax = new Vector2(1f, 0.6f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-8f, 0f);
            rect.sizeDelta = new Vector2(260f, 120f);

            obj.AddComponent<UI.GoalGuideWidget>();

            Debug.Log("[Phase2Setup] GoalGuideWidget 생성 (우측 가이드 퀘스트 위젯)");
        }

        private static void CreateDailyChecklistWidgetInHUD()
        {
            if (Object.FindFirstObjectByType<UI.DailyChecklistWidget>() != null)
            {
                Debug.Log("[Phase2Setup] DailyChecklistWidget 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreateHUDCanvas();

            var obj = new GameObject("DailyChecklistWidget");
            Undo.RegisterCreatedObjectUndo(obj, "Create DailyChecklistWidget");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 좌측 중앙, 접이식
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(8f, 0f);
            rect.sizeDelta = new Vector2(240f, 400f);

            obj.AddComponent<UI.DailyChecklistWidget>();

            Debug.Log("[Phase2Setup] DailyChecklistWidget 생성 (좌측 일일 체크리스트)");
        }

        private static Canvas FindOrCreatePopupCanvas()
        {
            // 기존 PopupParent Canvas 찾기 (sortingOrder 높은 것)
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.gameObject.name.Contains("Popup") && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return c;
            }

            // HUD Canvas보다 위에 팝업 캔버스 생성
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 9000)
                    return c;
            }

            var canvasObj = new GameObject("PopupCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create PopupCanvas");
            var newCanvas = canvasObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 9000;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            Debug.Log("[Phase2Setup] PopupCanvas 생성 (sortingOrder=9000)");
            return newCanvas;
        }

        private static void CreateAcquisitionShortcutPopupInPopupParent()
        {
            if (Object.FindFirstObjectByType<UI.AcquisitionShortcutPopup>() != null)
            {
                Debug.Log("[Phase2Setup] AcquisitionShortcutPopup 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreatePopupCanvas();

            var obj = new GameObject("AcquisitionShortcutPopup");
            Undo.RegisterCreatedObjectUndo(obj, "Create AcquisitionShortcutPopup");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 하단 중앙
            rect.anchorMin = new Vector2(0.1f, 0.05f);
            rect.anchorMax = new Vector2(0.9f, 0.25f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            obj.AddComponent<CanvasGroup>();
            obj.AddComponent<UI.AcquisitionShortcutPopup>();

            Debug.Log("[Phase2Setup] AcquisitionShortcutPopup 생성 (획득 바로가기 팝업)");
        }

        private static void CreateEquipComparePopupInPopupParent()
        {
            if (Object.FindFirstObjectByType<UI.EquipComparePopup>() != null)
            {
                Debug.Log("[Phase2Setup] EquipComparePopup 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreatePopupCanvas();

            var obj = new GameObject("EquipComparePopup");
            Undo.RegisterCreatedObjectUndo(obj, "Create EquipComparePopup");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            // 화면 중앙
            rect.anchorMin = new Vector2(0.1f, 0.3f);
            rect.anchorMax = new Vector2(0.9f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            obj.AddComponent<CanvasGroup>();
            obj.AddComponent<UI.EquipComparePopup>();

            Debug.Log("[Phase2Setup] EquipComparePopup 생성 (장비 비교 팝업)");
        }

        private static void CreateReturneeGuidePopupInPopupParent()
        {
            if (Object.FindFirstObjectByType<UI.ReturneeGuidePopup>() != null)
            {
                Debug.Log("[Phase2Setup] ReturneeGuidePopup 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreatePopupCanvas();

            var obj = new GameObject("ReturneeGuidePopup");
            Undo.RegisterCreatedObjectUndo(obj, "Create ReturneeGuidePopup");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.1f);
            rect.anchorMax = new Vector2(0.95f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            obj.AddComponent<CanvasGroup>();
            obj.AddComponent<UI.ReturneeGuidePopup>();
            obj.SetActive(false);

            Debug.Log("[Phase2Setup] ReturneeGuidePopup 생성 (복귀 유저 가이드)");
        }

        private static void CreateStatCompareViewInPopupParent()
        {
            if (Object.FindFirstObjectByType<UI.StatCompareView>() != null)
            {
                Debug.Log("[Phase2Setup] StatCompareView 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreatePopupCanvas();

            var obj = new GameObject("StatCompareView");
            Undo.RegisterCreatedObjectUndo(obj, "Create StatCompareView");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.3f);
            rect.anchorMax = new Vector2(0.9f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            obj.AddComponent<CanvasGroup>();
            obj.AddComponent<UI.StatCompareView>();

            Debug.Log("[Phase2Setup] StatCompareView 생성 (스탯 비교 뷰)");
        }

        private static void CreateCurrencyShortagePopupInPopupParent()
        {
            if (Object.FindFirstObjectByType<UI.CurrencyShortagePopup>() != null)
            {
                Debug.Log("[Phase2Setup] CurrencyShortagePopup 이미 존재 — 건너뜀");
                return;
            }

            var canvas = FindOrCreatePopupCanvas();

            var obj = new GameObject("CurrencyShortagePopup");
            Undo.RegisterCreatedObjectUndo(obj, "Create CurrencyShortagePopup");
            obj.transform.SetParent(canvas.transform, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.3f);
            rect.anchorMax = new Vector2(0.85f, 0.65f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var cg = obj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            obj.AddComponent<UI.CurrencyShortagePopup>();
            // SetActive(true) — Awake가 호출되어야 이벤트 구독이 등록됨
            // CanvasGroup alpha=0으로 시각적으로 숨김

            Debug.Log("[Phase2Setup] CurrencyShortagePopup 생성 (재화 부족 팝업)");
        }
    }
}
