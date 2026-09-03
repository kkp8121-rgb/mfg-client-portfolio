using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// SPUM 캐릭터 비주얼 관리자.
    /// 직업별 SPUM 프리팹을 인스턴스화하고, 전직 시 파츠를 교체한다.
    /// SPUM은 Assembly-CSharp에 있으므로 리플렉션으로 접근한다.
    /// </summary>
    public class SpumCharacterManager : MonoBehaviour
    {
        public static SpumCharacterManager Instance { get; private set; }

        [Header("SPUM 프리팹 (직접 참조 — 런타임 호환)")]
        [SerializeField] private GameObject _warriorPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private GameObject _magePrefab;

        [Header("SPUM 프리팹 경로 (에디터 fallback)")]
        [SerializeField] private string _warriorPrefabPath = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab";
        [SerializeField] private string _archerPrefabPath = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/BowMan.prefab";
        [SerializeField] private string _magePrefabPath = "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/MagicianMan.prefab";

        /// <summary>직업별 SPUM 프리팹 캐시</summary>
        private readonly Dictionary<JobType, GameObject> _jobPrefabCache = new();

        /// <summary>tier별 외형 DB (0=기본, 4=최종 전직). 우선순위 1.</summary>
        private JobOutfitDatabaseSO _outfitDb;

        /// <summary>현재 플레이어에 적용된 SPUM 인스턴스</summary>
        private GameObject _currentSpumInstance;

        /// <summary>외부에서 현재 SPUM 인스턴스 읽기 (프리뷰 렌더링 등)</summary>
        public GameObject CurrentSpumInstance => _currentSpumInstance;

        /// <summary>현재 적용된 직업</summary>
        private JobType _currentVisualJob = (JobType)(-1);

        /// <summary>현재 적용된 전직 티어</summary>
        private int _currentVisualTier = -1;

        /// <summary>전직 티어별 비주얼 틴트 (갑옷 색상 변화)</summary>
        private static readonly Color[] TierTints =
        {
            new Color(0.85f, 0.85f, 0.85f), // T0: 회색 (견습)
            new Color(1.0f, 1.0f, 1.0f),    // T1: 흰색 (기본)
            new Color(0.7f, 0.85f, 1.0f),   // T2: 파란색 (숙련)
            new Color(1.0f, 0.85f, 0.5f),   // T3: 금색 (마스터)
            new Color(1.0f, 0.4f, 0.3f),    // T4: 붉은색 (전설)
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _outfitDb = JobOutfitDatabaseSO.Load();
            CacheJobPrefabs();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>직업→Resources 경로 매핑</summary>
        private static readonly Dictionary<JobType, string> JobResourcePaths = new()
        {
            { JobType.Warrior, "Prefabs/SPUM/SwordMan" },
            { JobType.Archer,  "Prefabs/SPUM/BowMan" },
            { JobType.Mage,    "Prefabs/SPUM/MagicianMan" },
        };

        /// <summary>
        /// SPUM 프리팹을 캐시한다.
        /// 1순위: SerializeField 직접 참조
        /// 2순위: Resources.Load (런타임 호환)
        /// 3순위: 에디터 전용 AssetDatabase (개발용 fallback)
        /// </summary>
        private void CacheJobPrefabs()
        {
            // 1순위: 직접 참조된 프리팹 사용
            if (_warriorPrefab != null)
                _jobPrefabCache[JobType.Warrior] = _warriorPrefab;
            if (_archerPrefab != null)
                _jobPrefabCache[JobType.Archer] = _archerPrefab;
            if (_magePrefab != null)
                _jobPrefabCache[JobType.Mage] = _magePrefab;

            // 2순위: Resources.Load (런타임 + 빌드 호환)
            foreach (var kvp in JobResourcePaths)
            {
                if (!_jobPrefabCache.ContainsKey(kvp.Key))
                {
                    var loaded = Resources.Load<GameObject>(kvp.Value);
                    if (loaded != null)
                        _jobPrefabCache[kvp.Key] = loaded;
                }
            }

            // 3순위: 에디터에서 경로로 로드 (개발용 fallback)
#if UNITY_EDITOR
            if (!_jobPrefabCache.ContainsKey(JobType.Warrior))
                _jobPrefabCache[JobType.Warrior] = LoadPrefabAtPath(_warriorPrefabPath);
            if (!_jobPrefabCache.ContainsKey(JobType.Archer))
                _jobPrefabCache[JobType.Archer] = LoadPrefabAtPath(_archerPrefabPath);
            if (!_jobPrefabCache.ContainsKey(JobType.Mage))
                _jobPrefabCache[JobType.Mage] = LoadPrefabAtPath(_magePrefabPath);
#endif

            int cached = _jobPrefabCache.Count;
            if (cached == 0)
                Debug.LogWarning("[SpumCharacterManager] SPUM 프리팹이 하나도 캐시되지 않음!");
            else
                Debug.Log($"[SpumCharacterManager] SPUM 프리팹 캐시 완료: {cached}종");
        }

#if UNITY_EDITOR
        private GameObject LoadPrefabAtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[SpumCharacterManager] SPUM 프리팹 로드 실패: {path}");
            return prefab;
        }
#endif

        /// <summary>
        /// 플레이어 캐릭터에 직업에 맞는 SPUM 비주얼을 적용한다.
        /// </summary>
        public void ApplyJobVisual(Transform playerRoot, JobType job, int tier)
        {
            if (playerRoot == null) return;
            if (job == _currentVisualJob && tier == _currentVisualTier && _currentSpumInstance != null) return;

            // 직업 or tier가 바뀌면 SPUM 인스턴스 교체 (DB 기반 tier 외형 적용)
            bool needReplace = job != _currentVisualJob
                               || tier != _currentVisualTier
                               || _currentSpumInstance == null;
            if (needReplace)
            {
                ReplaceSpumInstance(playerRoot, job, tier);
            }

            // 전직 티어 비주얼 적용 (색상 틴트) — DB 외형에 추가 연출로 유지
            ApplyTierVisual(tier);

            // CharacterAnimBridge 재초기화 (새 SPUM 인스턴스 인식)
            var animBridge = playerRoot.GetComponent<CharacterAnimBridge>();
            if (animBridge != null)
            {
                SetAttackTypeForJob(animBridge, job);
                ReinitializeAnimBridge(animBridge);
            }

            // 2026-04-23 P0 #8: CharacterVisual.designId 동기화
            // 기존엔 프리팹 값(warrior)에 고정돼 있어 JobSystem과 불일치 (invariant CharacterVisualJobMatch fail).
            // SPUM 교체와 함께 designId도 갱신하여 CharacterDesignLoader 기반 애니메이션 타입도 직업별 적용되게.
            var cv = playerRoot.GetComponent<CharacterVisual>();
            if (cv != null)
            {
                string designId = job switch
                {
                    JobType.Archer => "archer",
                    JobType.Mage => "mage",
                    _ => "warrior"
                };
                cv.SetDesignId(designId);
            }

            _currentVisualJob = job;
            _currentVisualTier = tier;

#if UNITY_EDITOR
            Debug.Log($"[SpumCharacterManager] 비주얼 적용: {job} T{tier}");
#endif
        }

        /// <summary>
        /// 기존 SPUM 인스턴스를 제거하고 새 직업+tier의 프리팹을 인스턴스화한다.
        /// </summary>
        private void ReplaceSpumInstance(Transform playerRoot, JobType job, int tier)
        {
            // 기존 SPUM 자식 모두 제거 (SPUMVisual 등 씬에 있던 인스턴스 포함)
            for (int i = playerRoot.childCount - 1; i >= 0; i--)
            {
                var child = playerRoot.GetChild(i);
                foreach (var comp in child.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "SPUM_Prefabs")
                    {
                        child.gameObject.SetActive(false); // Destroy는 프레임 끝 실행 → 즉시 비활성화하여 검색 제외
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }
            _currentSpumInstance = null;

            // 1순위: tier DB 조회 (전직 차수별 외형)
            GameObject prefab = _outfitDb != null ? _outfitDb.GetPrefab(job, tier) : null;

            // 2순위: 직업별 기본 캐시
            if (prefab == null && _jobPrefabCache.TryGetValue(job, out var cached))
                prefab = cached;

            if (prefab == null)
            {
                Debug.LogWarning($"[SpumCharacterManager] 직업 {job} T{tier}의 SPUM 프리팹 없음");
                return;
            }

            // 인스턴스 생성 (플레이어의 자식으로)
            _currentSpumInstance = Instantiate(prefab, playerRoot);
            _currentSpumInstance.name = $"SPUM_{job}_T{tier}";
            _currentSpumInstance.transform.localPosition = Vector3.zero;
            _currentSpumInstance.transform.localScale = Vector3.one;

            // SPUM 프리팹이 레거시 Sprites/Default 셰이더를 사용하므로
            // Android URP 빌드에서 렌더링되려면 URP 호환 머티리얼로 교체 필요
            EnsureUrpMaterials(_currentSpumInstance);
        }

        /// <summary>URP 2D Sprite-Lit 머티리얼 캐시</summary>
        private static Material _urpSpriteMaterial;

        /// <summary>
        /// 모든 SpriteRenderer의 머티리얼을 URP 호환 Sprite-Lit-Default로 교체한다.
        /// 레거시 Sprites/Default는 Android URP 빌드에서 렌더링되지 않는다.
        /// </summary>
        private static void EnsureUrpMaterials(GameObject root)
        {
            if (_urpSpriteMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default"); // 폴백
                _urpSpriteMaterial = new Material(shader);
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sharedMaterial = _urpSpriteMaterial;
            }
        }

        /// <summary>
        /// 전직 티어에 따른 비주얼 변경 (갑옷/무기 색상 틴트).
        /// </summary>
        private void ApplyTierVisual(int tier)
        {
            if (_currentSpumInstance == null) return;

            int clampedTier = Mathf.Clamp(tier, 0, TierTints.Length - 1);
            Color tint = TierTints[clampedTier];

            // SPUM_SpriteList를 리플렉션으로 접근하여 갑옷/무기 틴트 적용
            var spriteList = FindSpumSpriteList(_currentSpumInstance);
            if (spriteList != null)
            {
                TintSpriteRendererList(spriteList, "_armorList", tint);
                TintSpriteRendererList(spriteList, "_weaponList", tint);
                TintSpriteRendererList(spriteList, "_clothList", tint);
            }
            else
            {
                // fallback: 모든 SpriteRenderer에 틴트 적용
                var renderers = _currentSpumInstance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].color = tint;
                }
            }
        }

        /// <summary>
        /// SPUM_SpriteList 컴포넌트를 리플렉션으로 찾는다.
        /// </summary>
        private Component FindSpumSpriteList(GameObject root)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "SPUM_SpriteList")
                    return comp;
            }
            return null;
        }

        /// <summary>
        /// SPUM_SpriteList의 특정 SpriteRenderer 리스트에 색상 틴트를 적용한다.
        /// </summary>
        private void TintSpriteRendererList(Component spriteList, string fieldName, Color tint)
        {
            var field = spriteList.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            var renderers = field.GetValue(spriteList) as System.Collections.IList;
            if (renderers == null) return;

            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i] as SpriteRenderer;
                if (sr != null)
                    sr.color = tint;
            }
        }

        /// <summary>
        /// 직업에 맞는 공격 애니메이션 타입을 설정한다.
        /// </summary>
        private void SetAttackTypeForJob(CharacterAnimBridge animBridge, JobType job)
        {
            var attackType = job switch
            {
                JobType.Warrior => AttackAnimType.Normal,
                JobType.Archer => AttackAnimType.Bow,
                JobType.Mage => AttackAnimType.Magic,
                _ => AttackAnimType.Normal
            };
            animBridge.SetAttackType(attackType);
        }

        /// <summary>
        /// CharacterAnimBridge의 SPUM 참조를 강제 재초기화한다.
        /// </summary>
        private void ReinitializeAnimBridge(CharacterAnimBridge animBridge)
        {
            animBridge.ForceReinitialize();
        }

        /// <summary>
        /// PlayerCharacter에서 호출 — SaveData 기반으로 초기 비주얼을 적용한다.
        /// Growth 어셈블리 참조 없이 SaveData로 직업/티어를 읽는다.
        /// </summary>
        public void InitializePlayerVisual(Transform playerRoot)
        {
            JobType job = JobType.Warrior;
            int tier = 0;

            var saveData = SaveManager.Instance?.CurrentData;
            if (saveData != null)
            {
                job = saveData.player.jobId switch
                {
                    "archer" => JobType.Archer,
                    "mage" => JobType.Mage,
                    _ => JobType.Warrior
                };
                tier = saveData.player.jobTier;
            }

            ApplyJobVisual(playerRoot, job, tier);
        }

        /// <summary>
        /// JobChangedEvent 핸들러 — 전직 시 비주얼 자동 업데이트.
        /// 2026-04-23 P0 #9 안전 가드: CurrentJobId가 null/empty면 _currentVisualJob 유지.
        /// (QuestStateRefresh 등에서 CurrentJobId 세팅 누락된 이벤트가 올 수 있어 Warrior 기본값으로 떨어지는 버그 방지)
        /// </summary>
        private void OnJobChanged(JobChangedEvent evt)
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            // jobId 비어있으면 현재 직업 유지 (Warrior default 회귀 방지)
            if (string.IsNullOrEmpty(evt.CurrentJobId))
            {
                ApplyJobVisual(player.transform, _currentVisualJob, evt.CurrentTier);
                return;
            }

            // jobId → JobType 변환 (default는 _currentVisualJob 유지)
            JobType job = evt.CurrentJobId switch
            {
                "warrior" => JobType.Warrior,
                "archer" => JobType.Archer,
                "mage" => JobType.Mage,
                _ => _currentVisualJob
            };

            ApplyJobVisual(player.transform, job, evt.CurrentTier);
        }

        /// <summary>
        /// 몬스터에 SPUM 비주얼을 적용한다.
        /// </summary>
        public void ApplyMonsterVisual(Transform monsterRoot, MonsterDataSO data)
        {
            if (monsterRoot == null || data == null) return;

            // SPUM 프리팹 로드 — data.prefab (GUID) 단일 원천 우선 (런타임 빌드 안전)
            GameObject prefab = data.prefab;
#if UNITY_EDITOR
            if (prefab == null && !string.IsNullOrEmpty(data.spumPrefabPath))
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(data.spumPrefabPath);
#endif

            if (prefab == null)
                _jobPrefabCache.TryGetValue(JobType.Warrior, out prefab);

            ApplySpumPrefab(monsterRoot, prefab, data.visualTint, data.visualScale, data.attackAnimType, data.id);
        }

        /// <summary>
        /// 챕터 기반 몬스터 비주얼 적용. 프리팹을 직접 전달받는다.
        /// </summary>
        public void ApplyMonsterVisual(Transform monsterRoot, GameObject spumPrefab,
            Color tint, float scale, int attackAnimType, string label = "chapter")
        {
            ApplySpumPrefab(monsterRoot, spumPrefab, tint, scale, attackAnimType, label);
        }

        private void ApplySpumPrefab(Transform monsterRoot, GameObject prefab,
            Color tint, float scale, int attackAnimType, string label)
        {
            if (monsterRoot == null) return;

            string expectedName = $"SPUM_{label}";

            // 풀 리스폰 시 동일 비주얼이면 Instantiate 스킵 (GC 방지)
            for (int i = monsterRoot.childCount - 1; i >= 0; i--)
            {
                var child = monsterRoot.GetChild(i);
                if (child.name == expectedName)
                {
                    // 동일 비주얼 — 활성화만 하고 틴트/스케일 재적용
                    child.gameObject.SetActive(true);
                    child.localScale = Vector3.one * scale;
                    // tint 적용 — Color.white여도 alpha=1 복원 목적으로 항상 적용 (풀 재사용 시 이전 Die fade로 alpha=0 잔류 방지)
                    var renderers = child.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                        if (renderers[r] != null) renderers[r].color = tint;
                    var bridge = monsterRoot.GetComponent<CharacterAnimBridge>();
                    if (bridge != null)
                    {
                        bridge.SetAttackType((AttackAnimType)attackAnimType);
                        ReinitializeAnimBridge(bridge);
                    }
                    // 프리팹 기본 비주얼(SPUMVisual)이 남아있으면 비활성화 (겹침 방지)
                    for (int j = monsterRoot.childCount - 1; j >= 0; j--)
                    {
                        var other = monsterRoot.GetChild(j);
                        if (other != child && other.name == "SPUMVisual")
                            other.gameObject.SetActive(false);
                    }
                    // MonsterController가 런타임 SPUM 자식을 fade/tint 대상에 포함하도록 캐시 갱신
                    var mcReuse = monsterRoot.GetComponent<MonsterController>();
                    if (mcReuse != null) mcReuse.RefreshSpriteRenderers();
                    return;
                }
            }

            // 다른 비주얼의 SPUM 자식 제거 (SPUM_ 및 SPUMVisual 포함)
            for (int i = monsterRoot.childCount - 1; i >= 0; i--)
            {
                var child = monsterRoot.GetChild(i);
                if (child.name.StartsWith("SPUM_") || child.name == "SPUMVisual")
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            if (prefab == null)
            {
                _jobPrefabCache.TryGetValue(JobType.Warrior, out prefab);
                if (prefab == null) return;
            }

            var instance = Instantiate(prefab, monsterRoot);
            instance.name = expectedName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one * scale;
            instance.SetActive(true);

            // 중복 액터 스크립트 비활성화 — data.prefab이 MonsterController 부착 actor인 경우
            // 자기 자신을 자식으로 Instantiate하면 중첩 MonsterController가 UpdateManager에 등록되어
            // 자식이 비활성화되거나 부작용 발생. [RequireComponent] 체인으로 Destroy는 차단되므로
            // enabled=false로 로직 실행만 차단. Collider/Rigidbody는 물리 간섭 방지 위해 Destroy 가능.
            var dupMc = instance.GetComponent<MonsterController>();
            if (dupMc != null) dupMc.enabled = false;
            var dupCombat = instance.GetComponent<MonsterCombat>();
            if (dupCombat != null) dupCombat.enabled = false;
            var dupStats = instance.GetComponent<CombatStats>();
            if (dupStats != null) dupStats.enabled = false;
            var dupAnimBridge = instance.GetComponent<CharacterAnimBridge>();
            if (dupAnimBridge != null) dupAnimBridge.enabled = false;
            var dupHitFlash = instance.GetComponent<HitFlashEffect>();
            if (dupHitFlash != null) dupHitFlash.enabled = false;
            var dupCollider = instance.GetComponent<Collider2D>();
            if (dupCollider != null) Destroy(dupCollider);
            var dupRb = instance.GetComponent<Rigidbody2D>();
            if (dupRb != null) Destroy(dupRb);

            // URP 호환 머티리얼 적용
            EnsureUrpMaterials(instance);

            // 색상 틴트 적용 — 항상 적용 (alpha 복원 보장)
            {
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].color = tint;
                }
            }

            // CharacterAnimBridge 재초기화
            var animBridge = monsterRoot.GetComponent<CharacterAnimBridge>();
            if (animBridge != null)
            {
                animBridge.SetAttackType((AttackAnimType)attackAnimType);
                ReinitializeAnimBridge(animBridge);
            }
            // MonsterController가 런타임 SPUM 자식을 fade/tint 대상에 포함하도록 캐시 갱신
            var mc = monsterRoot.GetComponent<MonsterController>();
            if (mc != null) mc.RefreshSpriteRenderers();
        }

        /// <summary>
        /// 몬스터에 간단한 색상 틴트만 적용 (SPUM 없이).
        /// 이미 SPUM 자식이 있는 프리팹에 사용.
        /// </summary>
        public static void ApplyTintToExisting(Transform root, Color tint)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = tint;
            }
        }

        /// <summary>
        /// 몬스터에 스케일 적용 (SPUM 자식에만).
        /// </summary>
        public static void ApplyScaleToSpumChild(Transform root, float scale)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("SPUM_"))
                {
                    child.localScale = Vector3.one * scale;
                    return;
                }
            }
        }
    }
}
