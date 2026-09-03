using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 플레이어 스킬 관리. 레벨업 시 스킬 해금, 자동 시전.
    /// 액티브 = 기본공격(쿨타임 없음), 패시브 = 영구, 버프/각성기 = 쿨타임 자동 시전.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        [Header("직업 스킬 목록 (레벨순)")]
        [SerializeField] private SkillDataSO[] _allSkills;

        private CombatStats _stats;
        private PlayerCharacter _player;
        private CharacterAnimBridge _animBridge;

        // 해금된 스킬
        private readonly List<SkillDataSO> _learnedSkills = new();

        // 현재 기본공격 (가장 최근 Active)
        private SkillDataSO _currentActiveSkill;

        // 쿨다운 관리
        private const float MIN_SKILL_COOLDOWN = 4f;
        private readonly Dictionary<string, float> _cooldowns = new();
        private readonly List<string> _cdKeyCache = new();
        private bool _cdKeysDirty = true;

        // 활성 버프 관리
        private readonly Dictionary<string, BuffState> _activeBuffs = new();
        private readonly List<string> _buffKeyCache = new();
        private bool _buffKeysDirty = true;
        private readonly List<string> _expiredBuffs = new();

        // 스킬 레벨 관리
        private readonly Dictionary<string, int> _skillLevels = new();

        public SkillDataSO CurrentActiveSkill => _currentActiveSkill;
        public IReadOnlyList<SkillDataSO> LearnedSkills => _learnedSkills;
        public SkillDataSO[] AllSkills => _allSkills;

        // 특수 효과 플래그
        private bool _hasRevive;
        private bool _reviveUsed;

        public bool HasRevive => _hasRevive && !_reviveUsed;
        // HasKnockback 제거됨 (2026-04-23): idle-control.md 참조 — 넉백 전면 비활성화

        /// <summary>
        /// 스킬의 남은 쿨타임을 반환한다. 등록 안 됐으면 0.
        /// </summary>
        public float GetCooldownRemaining(string skillId)
        {
            return _cooldowns.TryGetValue(skillId, out float cd) ? Mathf.Max(cd, 0f) : 0f;
        }

        /// <summary>
        /// 버프가 활성 중이면 남은 시간을 반환한다. 비활성이면 0.
        /// </summary>
        public float GetBuffRemaining(string skillId)
        {
            return _activeBuffs.TryGetValue(skillId, out var buff) ? Mathf.Max(buff.remainingTime, 0f) : 0f;
        }

        /// <summary>
        /// 버프가 활성 중인지 여부.
        /// </summary>
        public bool IsBuffActive(string skillId)
        {
            return _activeBuffs.ContainsKey(skillId);
        }

        /// <summary>
        /// 활성 흡혈 비율 (0이면 비활성).
        /// </summary>
        public float LifestealRate
        {
            get
            {
                foreach (var buff in _activeBuffs.Values)
                {
                    if (buff.skill.specialEffect == SkillEffect.Lifesteal)
                        return buff.skill.effectValue;
                }
                return 0f;
            }
        }

        /// <summary>
        /// 부활 1회 사용 처리. HP 비율을 반환한다.
        /// </summary>
        public float ConsumeRevive()
        {
            if (!_hasRevive || _reviveUsed) return 0f;
            _reviveUsed = true;

            foreach (var skill in _learnedSkills)
            {
                if (skill.specialEffect == SkillEffect.Revive)
                    return skill.healRatio;
            }
            return 0.3f;
        }

        private struct BuffState
        {
            public SkillDataSO skill;
            public float remainingTime;
            public int bonusAtk;
            public int bonusDef;
            public float bonusCritRate;
            public float bonusAtkSpd;
        }

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            _player = GetComponent<PlayerCharacter>();
            _animBridge = GetComponent<CharacterAnimBridge>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
        }

        private void Start()
        {
            // 세이브에서 스킬 레벨 복원 (LoadCompletedEvent 놓침 방지)
            LoadState();

            // 초기 스킬 체크 (Lv1 액티브)
            var levelSystem = GetComponent<LevelSystem>();
            int currentLevel = levelSystem != null ? levelSystem.CurrentLevel : 1;
            CheckSkillUnlocks(currentLevel);
        }

        private void Update()
        {
            // 쿨다운 감소
            UpdateCooldowns();

            // 버프 지속시간 관리
            UpdateBuffs();

            // 자동 시전 (Attack 상태가 아닐 때도 버프/각성기는 시전)
            AutoCastSkills();
        }

        /// <summary>
        /// 기본공격 데미지 배율을 반환한다.
        /// </summary>
        public float GetActiveMultiplier()
        {
            return _currentActiveSkill != null ? _currentActiveSkill.damageMultiplier : 1f;
        }

        /// <summary>
        /// 기본공격 범위를 반환한다. 0이면 단일 타겟.
        /// </summary>
        public float GetActiveRange()
        {
            return _currentActiveSkill != null ? _currentActiveSkill.range : 0f;
        }

        /// <summary>
        /// 기본공격 히트 수를 반환한다.
        /// </summary>
        public int GetActiveHitCount()
        {
            return _currentActiveSkill != null ? _currentActiveSkill.hitCount : 1;
        }

        #region 스킬 해금

        private void OnLevelUp(LevelUpEvent evt)
        {
            CheckSkillUnlocks(evt.CurrentLevel);
        }

        private void OnJobChanged(JobChangedEvent evt)
        {
            // 직업 변경 시 다른 직업 스킬 제거 (전직 시는 동일 직업 다른 tier만 추가되므로 무영향)
            string currentJobId = GetCurrentJobId();
            _learnedSkills.RemoveAll(s => s == null || !IsSkillForJob(s.id, currentJobId));
            if (_currentActiveSkill != null && !IsSkillForJob(_currentActiveSkill.id, currentJobId))
                _currentActiveSkill = null;

            // 새 직업/티어 스킬 해금
            var levelSystem = GetComponent<LevelSystem>();
            int currentLevel = levelSystem != null ? levelSystem.CurrentLevel : 1;
            CheckSkillUnlocks(currentLevel);
        }

        private void CheckSkillUnlocks(int level)
        {
            EnsureAllSkillsHasJobSkills();
            if (_allSkills == null) return;

            int playerJobTier = SaveManager.Instance?.CurrentData?.player?.jobTier ?? 0;
            string currentJobId = GetCurrentJobId();

            foreach (var skill in _allSkills)
            {
                if (skill == null) continue;
                if (!IsSkillForJob(skill.id, currentJobId)) continue;
                if (level >= skill.learnLevel
                    && playerJobTier >= skill.jobTier
                    && !_learnedSkills.Contains(skill))
                {
                    LearnSkill(skill);
                }
            }
        }

        // 직업별 스킬 ID prefix 매핑 (tier 0~4)
        // SkillDataSO에 직업 필드가 없어 ID prefix로 필터링 — 추후 SO 필드 추가 시 이 헬퍼 제거 가능.
        private static readonly Dictionary<string, string[]> _jobSkillPrefixes = new()
        {
            { "warrior", new[] { "warrior_", "knight_", "warlord_", "titan_", "dragonslayer_" } },
            { "archer",  new[] { "archer_", "scout_", "windwalker_", "hawkeye_", "stormbringer_" } },
            { "mage",    new[] { "mage_", "sorcerer_", "sage_", "runemaster_", "archmage_" } },
        };

        private static string GetCurrentJobId()
        {
            var id = SaveManager.Instance?.CurrentData?.player?.jobId;
            return string.IsNullOrEmpty(id) ? "warrior" : id;
        }

        private static bool IsSkillForJob(string skillId, string jobId)
        {
            if (string.IsNullOrEmpty(skillId) || string.IsNullOrEmpty(jobId)) return false;
            if (!_jobSkillPrefixes.TryGetValue(jobId, out var prefixes)) return false;
            for (int i = 0; i < prefixes.Length; i++)
                if (skillId.StartsWith(prefixes[i])) return true;
            return false;
        }

        /// <summary>
        /// _allSkills에 현재 직업 스킬이 1개도 없으면 Resources/Data/Skills에서 폴백 로드.
        /// PlayerCharacter prefab의 SerializeField가 특정 직업으로만 채워진 경우를 보완.
        /// </summary>
        private void EnsureAllSkillsHasJobSkills()
        {
            if (_allSkills == null) _allSkills = System.Array.Empty<SkillDataSO>();
            string jobId = GetCurrentJobId();
            bool hasAny = false;
            for (int i = 0; i < _allSkills.Length; i++)
            {
                var s = _allSkills[i];
                if (s != null && IsSkillForJob(s.id, jobId)) { hasAny = true; break; }
            }
            if (hasAny) return;

            var loaded = Resources.LoadAll<SkillDataSO>("Data/Skills");
            if (loaded == null || loaded.Length == 0) return;

            var set = new HashSet<SkillDataSO>(_allSkills);
            for (int i = 0; i < loaded.Length; i++) if (loaded[i] != null) set.Add(loaded[i]);
            _allSkills = new SkillDataSO[set.Count];
            set.CopyTo(_allSkills);
#if UNITY_EDITOR
            Debug.Log($"[SkillSystem] _allSkills 폴백 로드: {_allSkills.Length}개 (직업={jobId})");
#endif
        }

        private void LearnSkill(SkillDataSO skill)
        {
            _learnedSkills.Add(skill);
            // 세이브에서 복원된 레벨이 있으면 보존, 없으면 1
            if (!_skillLevels.ContainsKey(skill.id))
                _skillLevels[skill.id] = 1;

            switch (skill.skillType)
            {
                case SkillType.Active:
                    _currentActiveSkill = skill;
#if UNITY_EDITOR
                    Debug.Log($"[SkillSystem] 기본공격 변경: {skill.displayName} (배율 {skill.damageMultiplier}x)");
#endif
                    break;

                case SkillType.Passive:
                    ApplyPassive(skill);
#if UNITY_EDITOR
                    Debug.Log($"[SkillSystem] 패시브 습득: {skill.displayName}");
#endif
                    break;

                case SkillType.Buff:
                case SkillType.Awakening:
                    _cooldowns[skill.id] = 0f; // 즉시 사용 가능
                    _cdKeysDirty = true;
#if UNITY_EDITOR
                    Debug.Log($"[SkillSystem] 스킬 습득: {skill.displayName} (쿨타임 {skill.cooldown}초)");
#endif
                    break;
            }

            EventBus.Publish(new SkillLearnedEvent
            {
                SkillId = skill.id,
                SkillName = skill.displayName,
                SkillType = skill.skillType
            });

            SaveState();
        }

        /// <summary>
        /// 스킬 레벨을 반환한다. 미습득이면 0.
        /// </summary>
        public int GetSkillLevel(string skillId)
        {
            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        /// <summary>
        /// 스킬 레벨업. 골드 비용은 CombatFormula.SkillLevelUpCost로 계산.
        /// 호출자가 비용 차감을 처리한 후 호출한다.
        /// </summary>
        public bool LevelUpSkill(string skillId)
        {
            // 습득된 스킬인지 확인
            bool found = false;
            for (int i = 0; i < _learnedSkills.Count; i++)
            {
                if (_learnedSkills[i].id == skillId)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;

            if (!_skillLevels.ContainsKey(skillId))
                _skillLevels[skillId] = 1;

            _skillLevels[skillId]++;
            int newLevel = _skillLevels[skillId];

#if UNITY_EDITOR
            Debug.Log($"[SkillSystem] 스킬 레벨업: {skillId} → Lv.{newLevel}");
#endif

            EventBus.Publish(new SkillLevelUpEvent
            {
                SkillId = skillId,
                NewLevel = newLevel
            });

            SaveState();
            return true;
        }

        // ══════════════════════════════════════════════
        //  2026-04-23 Phase B 슬롯 기반 API (단계 1)
        //  기존 _skillLevels Dictionary(skillId 기반)는 유지.
        //  slot 0: 기본공격 (_currentActiveSkill)
        //  slot 1~3: 학습한 Buff/Awakening 스킬 (학습 순서대로)
        //  기획: systems/skill-slot-rework.md
        // ══════════════════════════════════════════════

        private const int SLOT_COUNT = 4;

        /// <summary>슬롯 인덱스(0~3)에 매핑된 SkillDataSO를 반환. 없으면 null.</summary>
        public SkillDataSO GetSkillAtSlot(int slot)
        {
            if (slot < 0 || slot >= SLOT_COUNT) return null;

            // slot 0 = 기본공격
            if (slot == 0) return _currentActiveSkill;

            // slot 1~3 = 학습된 비패시브 스킬 (기본공격 제외한 순서)
            int nonPassiveIdx = 0;
            for (int i = 0; i < _learnedSkills.Count; i++)
            {
                var s = _learnedSkills[i];
                if (s == null) continue;
                if (s.skillType == SkillType.Passive) continue;
                if (s == _currentActiveSkill) continue; // slot 0 중복 제외
                nonPassiveIdx++;
                if (nonPassiveIdx == slot) return s;
            }
            return null;
        }

        /// <summary>슬롯 인덱스(0~3)의 레벨을 반환. 슬롯 비었으면 0.</summary>
        public int GetSlotLevel(int slot)
        {
            var skill = GetSkillAtSlot(slot);
            if (skill == null) return 0;
            return GetSkillLevel(skill.id);
        }

        /// <summary>슬롯 인덱스(0~3) 기반 레벨업. 내부적으로 LevelUpSkill(id) 위임.</summary>
        public bool LevelUpSlot(int slot)
        {
            var skill = GetSkillAtSlot(slot);
            if (skill == null) return false;
            return LevelUpSkill(skill.id);
        }

        /// <summary>SkillDataSO → 슬롯 인덱스 역매핑. 매핑 안 되면 -1.</summary>
        public int GetSlotForSkill(SkillDataSO skill)
        {
            if (skill == null) return -1;
            for (int slot = 0; slot < SLOT_COUNT; slot++)
            {
                if (GetSkillAtSlot(slot) == skill) return slot;
            }
            return -1;
        }

        /// <summary>SaveData.slotLevels 배열을 현재 학습 상태 기반으로 재계산하여 동기화.</summary>
        public void SyncSlotLevelsToSave()
        {
            var save = SaveManager.Instance?.CurrentData?.skillLevel;
            if (save == null) return;
            if (save.slotLevels == null || save.slotLevels.Length < SLOT_COUNT)
                save.slotLevels = new int[SLOT_COUNT];

            for (int slot = 0; slot < SLOT_COUNT; slot++)
                save.slotLevels[slot] = GetSlotLevel(slot);
        }

        #endregion

        #region 저장/로드

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            SaveState();
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            LoadState();

            // 로드 직후 현재 level/tier 기준으로 재학습 (learned/active 리셋 후 재구성)
            var levelSystem = GetComponent<LevelSystem>();
            int currentLevel = levelSystem != null ? levelSystem.CurrentLevel : 1;
            CheckSkillUnlocks(currentLevel);
        }

        /// <summary>
        /// SaveData에서 스킬 레벨을 복원한다.
        /// 로드 시 learned/active/cooldown 캐시를 전부 초기화하여 이전 세션 잔재를 제거한다
        /// (Lv1에서 Knight_Charge가 active로 남는 버그 방지).
        /// 이후 CheckSkillUnlocks가 현재 level/tier 기준으로 재학습한다.
        /// </summary>
        public void LoadState()
        {
            // ── 이전 세션 런타임 상태 전면 리셋 ──
            _learnedSkills.Clear();
            _currentActiveSkill = null;
            _cooldowns.Clear();
            _activeBuffs.Clear();
            _skillLevels.Clear();
            _cdKeysDirty = true;

            var save = SaveManager.Instance?.CurrentData?.skillLevel;
            if (save?.entries != null)
            {
                for (int i = 0; i < save.entries.Count; i++)
                {
                    var entry = save.entries[i];
                    if (!string.IsNullOrEmpty(entry.skillId) && entry.level > 1)
                    {
                        _skillLevels[entry.skillId] = entry.level;
                    }
                }
            }

            // 2026-04-23 Phase B 스킬 슬롯 재설계 마이그레이션:
            // 기존 entries(skillId 기반) → slotLevels[4] (슬롯 기반) 이전.
            // 1회만 실행(slotMigrated 플래그). 기존 entries는 유지 (완전 이행 전까지).
            // 향후 SkillSystem이 slotLevels를 참조하면 기획 B-1 완료.
            if (save != null && !save.slotMigrated)
            {
                save.MigrateToSlotLevels();
#if UNITY_EDITOR
                Debug.Log($"[SkillSystem] 슬롯 마이그레이션 1회 실행: slotLevels=[{save.slotLevels[0]},{save.slotLevels[1]},{save.slotLevels[2]},{save.slotLevels[3]}]");
#endif
            }

#if UNITY_EDITOR
            int savedCount = save?.entries?.Count ?? 0;
            Debug.Log($"[SkillSystem] 로드: learned/active 리셋 + 스킬 레벨 {savedCount}개 복원");
#endif
        }

        /// <summary>
        /// 현재 스킬 레벨을 SaveData에 동기화한다.
        /// </summary>
        public void SaveState()
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;

            save.skillLevel.entries.Clear();
            foreach (var kvp in _skillLevels)
            {
                save.skillLevel.entries.Add(new SkillLevelEntry
                {
                    skillId = kvp.Key,
                    level = kvp.Value
                });
            }

            // 2026-04-23 Phase B 슬롯 동기화: 신규 slotLevels 배열을 현재 매핑으로 갱신
            SyncSlotLevelsToSave();
        }

        #endregion

        #region 패시브

        private void ApplyPassive(SkillDataSO skill)
        {
            if (_stats == null) return;

            int atkBonus = Mathf.RoundToInt(_stats.Atk * skill.buffAtkRate);
            int defBonus = Mathf.RoundToInt(_stats.Def * skill.buffDefRate);

            _stats.SetCpReason("스킬 버프");

            string keyBase = "skill_passive_" + skill.id;

            if (atkBonus != 0)
                _stats.AddModifier(keyBase + "_atk",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.Atk, atkBonus, 0f));

            if (defBonus != 0)
                _stats.AddModifier(keyBase + "_def",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.Def, defBonus, 0f));

            if (skill.buffCritRate != 0f)
                _stats.AddModifier(keyBase + "_critrate",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.CritRate, skill.buffCritRate, 0f));

            switch (skill.specialEffect)
            {
                case SkillEffect.Revive:
                    _hasRevive = true;
                    _reviveUsed = false;
                    break;

                case SkillEffect.Dodge:
                    if (skill.effectValue != 0f)
                        _stats.AddModifier(keyBase + "_dodge",
                            new StatModifier(ModifierSource.Buff, keyBase, StatType.DodgeRate, skill.effectValue / 100f, 0f));
                    break;
            }
        }

        #endregion

        #region 쿨다운

        private void UpdateCooldowns()
        {
            if (_cdKeysDirty)
            {
                _cdKeyCache.Clear();
                _cdKeyCache.AddRange(_cooldowns.Keys);
                _cdKeysDirty = false;
            }

            for (int i = 0; i < _cdKeyCache.Count; i++)
            {
                string key = _cdKeyCache[i];
                if (_cooldowns[key] > 0f)
                    _cooldowns[key] -= Time.deltaTime;
            }
        }

        private bool IsReady(string skillId)
        {
            return _cooldowns.ContainsKey(skillId) && _cooldowns[skillId] <= 0f;
        }

        private void StartCooldown(SkillDataSO skill)
        {
            // 기본공격(Active)은 쿨다운 캡 미적용, 스킬만 최소 4초 보장
            float cd = skill.cooldown;
            if (skill.skillType != SkillType.Active)
                cd = Mathf.Max(MIN_SKILL_COOLDOWN, cd);

            _cooldowns[skill.id] = cd;
        }

        #endregion

        #region 버프

        private void UpdateBuffs()
        {
            if (_buffKeysDirty)
            {
                _buffKeyCache.Clear();
                _buffKeyCache.AddRange(_activeBuffs.Keys);
                _buffKeysDirty = false;
            }

            _expiredBuffs.Clear();

            for (int i = 0; i < _buffKeyCache.Count; i++)
            {
                string key = _buffKeyCache[i];
                if (!_activeBuffs.TryGetValue(key, out var buff)) continue;

                buff.remainingTime -= Time.deltaTime;
                _activeBuffs[key] = buff;

                if (buff.remainingTime <= 0f)
                    _expiredBuffs.Add(key);
            }

            for (int i = 0; i < _expiredBuffs.Count; i++)
            {
                RemoveBuff(_expiredBuffs[i]);
            }
        }

        private void ApplyBuff(SkillDataSO skill)
        {
            if (_stats == null) return;

            // 같은 버프가 이미 있으면 갱신 (제거 후 재적용)
            if (_activeBuffs.ContainsKey(skill.id))
                RemoveBuff(skill.id);

            int atkBonus = Mathf.RoundToInt(_stats.Atk * skill.buffAtkRate);
            int defBonus = Mathf.RoundToInt(_stats.Def * skill.buffDefRate);
            float atkSpdBonus = _stats.AttackSpeed * skill.buffAtkSpdRate;

            string keyBase = "skill_buff_" + skill.id;

            _stats.SetCpReason("스킬 버프");

            if (atkBonus != 0)
                _stats.AddModifier(keyBase + "_atk",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.Atk, atkBonus, 0f));

            if (defBonus != 0)
                _stats.AddModifier(keyBase + "_def",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.Def, defBonus, 0f));

            if (skill.buffCritRate != 0f)
                _stats.AddModifier(keyBase + "_critrate",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.CritRate, skill.buffCritRate, 0f));

            if (atkSpdBonus != 0f)
                _stats.AddModifier(keyBase + "_atkspd",
                    new StatModifier(ModifierSource.Buff, keyBase, StatType.AttackSpeed, atkSpdBonus, 0f));

            _activeBuffs[skill.id] = new BuffState
            {
                skill = skill,
                remainingTime = skill.duration,
                bonusAtk = atkBonus,
                bonusDef = defBonus,
                bonusCritRate = skill.buffCritRate,
                bonusAtkSpd = atkSpdBonus
            };
            _buffKeysDirty = true;
        }

        private void RemoveBuff(string skillId)
        {
            if (!_activeBuffs.TryGetValue(skillId, out var buff)) return;

            string keyBase = "skill_buff_" + skillId;
            _stats.SetCpReason("스킬 버프 해제");
            _stats.RemoveModifier(keyBase + "_atk");
            _stats.RemoveModifier(keyBase + "_def");
            _stats.RemoveModifier(keyBase + "_critrate");
            _stats.RemoveModifier(keyBase + "_atkspd");

            _activeBuffs.Remove(skillId);
            _buffKeysDirty = true;
        }

        #endregion

        #region 자동 시전

        private void AutoCastSkills()
        {
            if (_stats.IsDead) return;

            // for 루프 사용 (CastSkill이 컬렉션을 수정할 수 있음)
            for (int i = 0; i < _learnedSkills.Count; i++)
            {
                var skill = _learnedSkills[i];
                if (skill.skillType == SkillType.Active || skill.skillType == SkillType.Passive)
                    continue;

                if (!IsReady(skill.id))
                    continue;

                CastSkill(skill);
            }
        }

        private void CastSkill(SkillDataSO skill)
        {
            StartCooldown(skill);

            switch (skill.skillType)
            {
                case SkillType.Buff:
                    ApplyBuff(skill);
                    // 버프 활성화 VFX
                    SkillVfx.SpawnBuffVfx(transform.position, skill, transform);
                    break;

                case SkillType.Awakening:
                    ExecuteAwakening(skill);
                    break;
            }

            // 스킬 사용 애니메이션
            if (_animBridge != null)
                _animBridge.PlayState(CharacterState.Skill);

            EventBus.Publish(new SkillUsedEvent
            {
                SkillId = skill.id,
                SkillName = skill.displayName,
                SkillType = skill.skillType
            });
        }

        #endregion

        #region 각성기 실행

        private void ExecuteAwakening(SkillDataSO skill)
        {
            // 각성기 VFX (타겟 방향으로 회전)
            CombatStats target = _player != null ? _player.CurrentTarget : null;
            SkillVfx.SpawnAwakeningVfx(transform.position, skill,
                transform.localScale.x, target);

            // 범위 데미지
            if (skill.damageMultiplier > 0f && skill.range > 0f)
            {
                DealAreaDamage(skill);
            }

            // 자힐
            if (skill.healRatio > 0f && _stats != null)
            {
                int healAmount = Mathf.RoundToInt(_stats.MaxHp * skill.healRatio);
                _stats.Heal(healAmount);
            }

            // 버프 효과 (각성기에 버프가 포함된 경우)
            if (skill.duration > 0f && (skill.buffAtkRate > 0f || skill.buffDefRate > 0f || skill.buffCritRate > 0f))
            {
                ApplyBuff(skill);
            }
        }

        // 2026-04-23 캐시: LayerMask 조회 1회만
        private static int _monsterLayerMask = -1;
        private static int MonsterLayerMask
        {
            get
            {
                if (_monsterLayerMask == -1) _monsterLayerMask = LayerMask.GetMask("Monster");
                return _monsterLayerMask;
            }
        }

        private void DealAreaDamage(SkillDataSO skill)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, skill.range, MonsterLayerMask);

            foreach (var hit in hits)
            {
                var targetStats = hit.GetComponent<CombatStats>();
                if (targetStats == null || targetStats.IsDead) continue;

                for (int i = 0; i < skill.hitCount; i++)
                {
                    var result = DamageCalculator.CalculateWithResult(
                        _stats.Atk,
                        targetStats.Def,
                        skill.damageMultiplier,
                        _stats.CritRate,
                        _stats.CritDmg,
                        _stats.DefensePenetration,
                        _stats.FinalDamage
                    );

                    targetStats.TakeDamage(result.damage);

                    if (DamageTextManager.Instance != null)
                    {
                        var mc = hit.GetComponent<MonsterController>();
                        DamageTextManager.Instance.Spawn(
                            targetStats.transform.position,
                            result.damage,
                            result.isCritical,
                            i,
                            isBoss: mc != null && mc.IsBoss,
                            isPenetrating: result.isPenetrating
                        );
                    }
                }

                // CC 효과 적용
                var monster = hit.GetComponent<MonsterController>();
                if (monster != null)
                {
                    switch (skill.specialEffect)
                    {
                        case SkillEffect.Stun:
                            if (skill.effectDuration > 0f)
                                monster.Stun(skill.effectDuration);
                            break;

                        case SkillEffect.Burn:
                            if (skill.dotDamageRate > 0f && skill.ccDuration > 0f)
                            {
                                int dotTotal = Mathf.RoundToInt(_stats.Atk * skill.dotDamageRate);
                                monster.ApplyBurn(dotTotal, skill.ccDuration);
                            }
                            break;

                        case SkillEffect.Freeze:
                            if (skill.ccDuration > 0f)
                                monster.ApplyFreeze(skill.ccDuration);
                            break;

                        case SkillEffect.Slow:
                            if (skill.slowRate > 0f && skill.ccDuration > 0f)
                                monster.ApplySlow(skill.slowRate, skill.ccDuration);
                            break;

                        case SkillEffect.Knockback:
                            // 넉백 제거됨 (2026-04-23): idle-control.md 참조 — 몬스터 접근 정체 유발
                            break;

                        // Penetrate: OverlapCircleAll이 이미 범위 내 모든 적을 타격하므로
                        // 적 수 제한 없이 풀 데미지가 적용됨 (기본 동작과 동일)
                        case SkillEffect.Penetrate:
                            break;

                        // MultiShot: hitCount 필드로 다단히트 처리됨 (위 for 루프)
                        case SkillEffect.MultiShot:
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        #endregion
    }
}
