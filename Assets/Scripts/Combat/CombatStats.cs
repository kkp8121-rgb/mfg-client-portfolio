using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    // ── 스탯 수정자 시스템 ──

    /// <summary>
    /// 수정자의 출처를 구분하는 열거형.
    /// ClearModifiers(source)로 특정 출처의 수정자를 일괄 제거할 수 있다.
    /// </summary>
    public enum ModifierSource
    {
        Equipment,
        Relic,
        Inscription,
        TowerGimmick,
        Buff,
        Job,
        HunterRank,
        Prestige,
        Blessing,
        Collection,
        Pet,
        Mastery,
        Level,
        StatAllocation,
        HeroPower,
        Artifact,
        Guild
    }

    /// <summary>
    /// 개별 스탯 수정자. flatBonus는 고정값, percentBonus는 비율(0.1 = +10%).
    /// base 스탯에 flat을 더한 후 percent를 곱하여 최종 스탯을 산출한다.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public ModifierSource source;
        public string sourceId;
        public StatType statType;
        public float flatBonus;
        public float percentBonus;

        public StatModifier(ModifierSource source, string sourceId, StatType statType, float flatBonus, float percentBonus)
        {
            this.source = source;
            this.sourceId = sourceId;
            this.statType = statType;
            this.flatBonus = flatBonus;
            this.percentBonus = percentBonus;
        }
    }

    /// <summary>
    /// 전투 유닛(캐릭터/몬스터)의 런타임 스탯을 관리하는 컴포넌트.
    /// SO에서 기본 스탯을 로드하고, 보정치 + 수정자를 적용하여 최종 스탯을 산출한다.
    /// </summary>
    public class CombatStats : MonoBehaviour
    {
        private const float ATTACK_SPEED_CAP = 150f;
        // 기본 스탯 (SO에서 로드)
        [SerializeField] private int _baseHp;
        [SerializeField] private int _baseAtk;
        [SerializeField] private int _baseDef;
        [SerializeField] private float _baseCritRate;
        [SerializeField] private float _baseAttackSpeed;
        [SerializeField] private float _baseMoveSpeed;

        // 보정 스탯 (레벨 성장치 등 — 기존 AddBonus/RemoveBonus 호환용)
        private int _bonusHp;
        private int _bonusAtk;
        private int _bonusDef;
        private float _bonusCritRate;
        private float _bonusAttackSpeed;
        private float _bonusDodgeRate;
        private float _bonusCritDmg;
        private float _bonusMoveSpeed;
        private float _bonusFinalDmg;
        private float _bonusDefPen;

        // 수정자 시스템 (탑 기믹, 각인, 버프 등)
        private readonly Dictionary<string, StatModifier> _modifiers = new();
        // RecalculateModifiers 재사용 리스트 (GC 방지)
        private readonly List<float> _atkSpdPctSources = new(8);
        private readonly List<float> _finalDmgSources = new(8);
        private readonly List<float> _defPenSources = new(8);
        private readonly List<float> _dmgTakenDecSources = new(8);
        private readonly List<string> _keysToRemoveCache = new(16);
        // 수정자에 의한 최종 보정값 (RecalculateModifiers에서 산출)
        private int _modHp;
        private int _modAtk;
        private int _modDef;
        private float _modCritRate;
        private float _modAttackSpeed;
        private float _modDodgeRate;
        private float _modCritDmg;
        private float _modMoveSpeed;
        private float _modFinalDmg;
        private float _modDefPen;
        private float _modBossDmg;
        private float _modDamageTakenDecrease;

        // 현재 HP
        private int _currentHp;

        // CP 변화 추적
        private long _previousPowerScore;

        /// <summary>무적 상태 (파밍 모드 등에서 사용)</summary>
        public bool IsInvincible { get; set; }

        // 최종 스탯 프로퍼티: base + bonus(레벨) + mod(수정자)
        public int MaxHp => _baseHp + _bonusHp + _modHp;
        public int Atk => _baseAtk + _bonusAtk + _modAtk;
        public int Def => _baseDef + _bonusDef + _modDef;
        private const float CRIT_RATE_HARD_CAP = 0.60f;
        private const float ATTACK_SPEED_HARD_CAP = 5f;

        public float CritRate => Mathf.Clamp(_baseCritRate + _bonusCritRate + _modCritRate, 0f, CRIT_RATE_HARD_CAP);
        public float AttackSpeed => Mathf.Clamp(_baseAttackSpeed + _bonusAttackSpeed + _modAttackSpeed, 0.1f, ATTACK_SPEED_HARD_CAP);
        public float MoveSpeed => _baseMoveSpeed + _bonusMoveSpeed + _modMoveSpeed;
        public float DodgeRate => Mathf.Clamp01(_bonusDodgeRate + _modDodgeRate);
        public float CritDmg => 1.5f + _bonusCritDmg + _modCritDmg;
        /// <summary>최종 데미지 곱연산 결과 (1.0 기준, 예: 1.3 = +30%). RecalculateModifiers에서 곱연산 산출.</summary>
        public float FinalDamage => _modFinalDmg;
        /// <summary>방어력 관통 비율 (0.0~1.0, 곱연산 체감). RecalculateModifiers에서 산출.</summary>
        public float DefensePenetration => _modDefPen;
        /// <summary>보스 추가 데미지 (0.3 = +30%). 합산.</summary>
        public float BossDamage => _modBossDmg;
        /// <summary>피해 감소 비율 (0.0~0.95, 곱연산 체감). RecalculateModifiers에서 산출.</summary>
        public float DamageTakenDecrease => _modDamageTakenDecrease;
        public int CurrentHp => _currentHp;
        public bool IsDead => _currentHp <= 0;

        /// <summary>현재 등록된 수정자 수 (디버그/UI 용)</summary>
        public int ModifierCount => _modifiers.Count;

        /// <summary>
        /// 전투력(PowerScore/CP). CombatFormula.CalculateCP(stats)와 동일한 결과.
        /// MaxHp * 0.5 + Atk * 3 + Def * 2 + CritRate * 500 + AttackSpeed * 100
        /// </summary>
        public long PowerScore => CombatFormula.CalculateCP(this);

        private void Awake()
        {
            if (_currentHp <= 0 && _baseHp > 0)
            {
                _currentHp = MaxHp;
            }
        }

        // 이벤트
        public event Action<int, int> OnHpChanged;  // (current, max)
        public event Action OnDeath;
        public event Action OnDodge;
        public event Action OnStatsChanged;

        // ── 수정자 API ──

        /// <summary>
        /// 수정자를 추가/갱신한다. 같은 key로 호출하면 덮어쓴다.
        /// </summary>
        public void AddModifier(string key, StatModifier modifier)
        {
            _modifiers[key] = modifier;
            RecalculateModifiers();
        }

        /// <summary>
        /// 지정 key의 수정자를 제거한다.
        /// </summary>
        public void RemoveModifier(string key)
        {
            if (_modifiers.Remove(key))
            {
                RecalculateModifiers();
            }
        }

        /// <summary>
        /// 특정 출처의 수정자를 모두 제거한다.
        /// </summary>
        public void ClearModifiers(ModifierSource source)
        {
            _keysToRemoveCache.Clear();
            foreach (var kvp in _modifiers)
            {
                if (kvp.Value.source == source)
                    _keysToRemoveCache.Add(kvp.Key);
            }
            for (int i = 0; i < _keysToRemoveCache.Count; i++)
            {
                _modifiers.Remove(_keysToRemoveCache[i]);
            }
            if (_keysToRemoveCache.Count > 0)
            {
                RecalculateModifiers();
            }
        }

        /// <summary>
        /// 모든 수정자를 제거한다.
        /// </summary>
        public void ClearAllModifiers()
        {
            if (_modifiers.Count == 0) return;
            _modifiers.Clear();
            RecalculateModifiers();
        }

        /// <summary>
        /// 지정 key의 수정자가 존재하는지 확인한다.
        /// </summary>
        public bool HasModifier(string key)
        {
            return _modifiers.ContainsKey(key);
        }

        /// <summary>
        /// 수정자를 기반으로 최종 보정값을 재계산한다.
        /// 계산 순서: (base + bonus + flatSum) * (1 + percentSum)
        /// _mod 필드에는 flat + percent에 의한 증감분만 저장된다.
        /// </summary>
        private void RecalculateModifiers()
        {
            int prevMaxHp = MaxHp;

            // StatType별 flat/percent 합산
            float flatAtk = 0f, pctAtk = 0f;
            float flatDef = 0f, pctDef = 0f;
            float flatHp = 0f, pctHp = 0f;
            float flatCritRate = 0f, pctCritRate = 0f;
            float flatCritDmg = 0f, pctCritDmg = 0f;
            float flatAtkSpd = 0f;
            // AttackSpeed는 체감 공식 적용: 개별 percent 소스를 리스트로 수집
            _atkSpdPctSources.Clear();
            float flatMoveSpd = 0f, pctMoveSpd = 0f;
            float flatDodge = 0f, pctDodge = 0f;
            // FinalDamage: 각 소스별 곱연산 (메이플 키우기 방식)
            _finalDmgSources.Clear();
            // DefensePenetration: 곱연산 체감 (1 - (1-a)(1-b)...)
            _defPenSources.Clear();
            // DamageTakenDecrease: 곱연산 체감 (최대 95%)
            _dmgTakenDecSources.Clear();
            float flatBossDmg = 0f;

            foreach (var kvp in _modifiers)
            {
                var mod = kvp.Value;
                switch (mod.statType)
                {
                    case StatType.Atk:
                        flatAtk += mod.flatBonus;
                        pctAtk += mod.percentBonus;
                        break;
                    case StatType.Def:
                        flatDef += mod.flatBonus;
                        pctDef += mod.percentBonus;
                        break;
                    case StatType.MaxHp:
                        flatHp += mod.flatBonus;
                        pctHp += mod.percentBonus;
                        break;
                    case StatType.CritRate:
                        flatCritRate += mod.flatBonus;
                        pctCritRate += mod.percentBonus;
                        break;
                    case StatType.CritDamage:
                        flatCritDmg += mod.flatBonus;
                        pctCritDmg += mod.percentBonus;
                        break;
                    case StatType.AttackSpeed:
                        flatAtkSpd += mod.flatBonus;
                        if (mod.percentBonus != 0f)
                            _atkSpdPctSources.Add(mod.percentBonus);
                        break;
                    case StatType.MoveSpeed:
                        flatMoveSpd += mod.flatBonus;
                        pctMoveSpd += mod.percentBonus;
                        break;
                    case StatType.DodgeRate:
                        flatDodge += mod.flatBonus;
                        pctDodge += mod.percentBonus;
                        break;
                    case StatType.FinalDamage:
                        // flatBonus를 곱연산 소스로 수집 (0.1 = +10% → 곱연산 1.1)
                        if (mod.flatBonus != 0f) _finalDmgSources.Add(mod.flatBonus);
                        if (mod.percentBonus != 0f) _finalDmgSources.Add(mod.percentBonus);
                        break;
                    case StatType.DefensePenetration:
                        // 곱연산 체감 소스로 수집
                        if (mod.flatBonus != 0f) _defPenSources.Add(mod.flatBonus);
                        if (mod.percentBonus != 0f) _defPenSources.Add(mod.percentBonus);
                        break;
                    case StatType.BossDamage:
                        flatBossDmg += mod.flatBonus + mod.percentBonus;
                        break;
                    case StatType.DamageTakenDecrease:
                        if (mod.flatBonus != 0f) _dmgTakenDecSources.Add(mod.flatBonus);
                        if (mod.percentBonus != 0f) _dmgTakenDecSources.Add(mod.percentBonus);
                        break;
                    case StatType.AllStats:
                        flatAtk += mod.flatBonus;
                        pctAtk += mod.percentBonus;
                        flatDef += mod.flatBonus;
                        pctDef += mod.percentBonus;
                        flatHp += mod.flatBonus;
                        pctHp += mod.percentBonus;
                        break;
                    default:
                        // GoldBonus, ExpBonus, AbyssResistance 등은 CombatStats에서 처리하지 않음
                        break;
                }
            }

            // _mod = flat분 + (base + bonus + flat분) * percent분
            // 최종 스탯 = base + bonus + _mod
            // 따라서 _mod = flat + (base + bonus + flat) * pct
            int baseHpTotal = _baseHp + _bonusHp;
            int baseAtkTotal = _baseAtk + _bonusAtk;
            int baseDefTotal = _baseDef + _bonusDef;
            float baseCritTotal = _baseCritRate + _bonusCritRate;
            float baseCritDmgTotal = _bonusCritDmg; // base CritDmg 1.5f는 프로퍼티에서 더함
            float baseAtkSpdTotal = _baseAttackSpeed + _bonusAttackSpeed;
            float baseMoveSpdTotal = _baseMoveSpeed + _bonusMoveSpeed;
            float baseDodgeTotal = _bonusDodgeRate;

            _modHp = Mathf.RoundToInt(flatHp + (baseHpTotal + flatHp) * pctHp);
            _modAtk = Mathf.RoundToInt(flatAtk + (baseAtkTotal + flatAtk) * pctAtk);
            _modDef = Mathf.RoundToInt(flatDef + (baseDefTotal + flatDef) * pctDef);
            _modCritRate = flatCritRate + (baseCritTotal + flatCritRate) * pctCritRate;
            _modCritDmg = flatCritDmg + (baseCritDmgTotal + flatCritDmg) * pctCritDmg;
            // AttackSpeed 체감 공식: CAP * (1 - product(1 - source_i / CAP))
            // 각 percent 소스를 개별 적용하여 수확체감 효과 구현
            if (_atkSpdPctSources.Count > 0)
            {
                float product = 1f;
                for (int i = 0; i < _atkSpdPctSources.Count; i++)
                {
                    product *= (1f - _atkSpdPctSources[i] / ATTACK_SPEED_CAP);
                }
                float diminishedPct = ATTACK_SPEED_CAP * (1f - product);
                _modAttackSpeed = flatAtkSpd + (baseAtkSpdTotal + flatAtkSpd) * (diminishedPct / ATTACK_SPEED_CAP);
            }
            else
            {
                _modAttackSpeed = flatAtkSpd;
            }
            _modMoveSpeed = flatMoveSpd + (baseMoveSpdTotal + flatMoveSpd) * pctMoveSpd;
            _modDodgeRate = flatDodge + (baseDodgeTotal + flatDodge) * pctDodge;

            // FinalDamage: 곱연산 — (1+a) × (1+b) × ... - 1 (메이플 키우기 방식)
            // bonus 값도 곱연산 소스로 포함
            if (_bonusFinalDmg != 0f) _finalDmgSources.Add(_bonusFinalDmg);
            float finalDmgProduct = 1f;
            for (int i = 0; i < _finalDmgSources.Count; i++)
                finalDmgProduct *= (1f + _finalDmgSources[i]);
            _modFinalDmg = finalDmgProduct - 1f; // DamageCalculator에서 (1 + FinalDamage) 로 사용

            // DefensePenetration: 곱연산 체감 — 1 - (1-a)(1-b)... (메이플 키우기 방식)
            if (_bonusDefPen != 0f) _defPenSources.Add(_bonusDefPen);
            float defPenProduct = 1f;
            for (int i = 0; i < _defPenSources.Count; i++)
                defPenProduct *= (1f - Mathf.Clamp01(_defPenSources[i]));
            _modDefPen = Mathf.Clamp01(1f - defPenProduct);

            // BossDamage: 합산
            _modBossDmg = flatBossDmg;

            // DamageTakenDecrease: 곱연산 체감, 최대 95%
            float dtdProduct = 1f;
            for (int i = 0; i < _dmgTakenDecSources.Count; i++)
                dtdProduct *= (1f - Mathf.Clamp01(_dmgTakenDecSources[i]));
            _modDamageTakenDecrease = Mathf.Clamp(1f - dtdProduct, 0f, 0.95f);

            // MaxHp가 변경되면 currentHp도 비례 조정
            int newMaxHp = MaxHp;
            if (prevMaxHp > 0 && newMaxHp != prevMaxHp)
            {
                if (newMaxHp > prevMaxHp)
                {
                    // MaxHp 증가 시 차이만큼 currentHp도 증가
                    _currentHp += (newMaxHp - prevMaxHp);
                }
                else
                {
                    // MaxHp 감소 시 클램프
                    _currentHp = Mathf.Clamp(_currentHp, 0, newMaxHp);
                }
            }

            OnHpChanged?.Invoke(_currentHp, MaxHp);
            OnStatsChanged?.Invoke();
            PublishCpChangeIfNeeded();
        }

        /// <summary>
        /// CP 변경 사유를 설정한다. 다음 PublishCpChangeIfNeeded 호출에서 사용된다.
        /// </summary>
        private string _pendingCpReason = "";

        /// <summary>
        /// 다음 CP 변경 이벤트에 사유를 설정한다.
        /// RecalculateModifiers/AddBonus 호출 전에 설정하면 이벤트에 반영된다.
        /// </summary>
        public void SetCpReason(string reason)
        {
            _pendingCpReason = reason ?? "";
        }

        /// <summary>
        /// CP가 변했으면 CpChangedEvent를 발행한다.
        /// </summary>
        private void PublishCpChangeIfNeeded()
        {
            long currentCp = PowerScore;
            long delta = currentCp - _previousPowerScore;
            if (delta == 0)
            {
                _pendingCpReason = "";
                return;
            }

            EventBus.Publish(new CpChangedEvent
            {
                PreviousCp = _previousPowerScore,
                CurrentCp = currentCp,
                Delta = delta,
                Reason = _pendingCpReason
            });

            _previousPowerScore = currentCp;
            _pendingCpReason = "";
        }

        // ── 초기화 ──

        /// <summary>
        /// 기본 스탯을 직접 지정하여 초기화한다.
        /// </summary>
        public void Initialize(int hp, int atk, int def, float critRate, float atkSpd, float moveSpd)
        {
            _baseHp = hp;
            _baseAtk = atk;
            _baseDef = def;
            _baseCritRate = critRate;
            _baseAttackSpeed = atkSpd;
            _baseMoveSpeed = moveSpd;

            _bonusHp = 0;
            _bonusAtk = 0;
            _bonusDef = 0;
            _bonusCritRate = 0f;

            _modifiers.Clear();
            ResetModValues();

            _currentHp = MaxHp;
            _previousPowerScore = PowerScore;
            OnHpChanged?.Invoke(_currentHp, MaxHp);
        }

        /// <summary>
        /// CharacterDataSO와 레벨 정보로 캐릭터 스탯을 초기화한다.
        /// 레벨에 따른 성장치가 보너스로 적용된다.
        /// </summary>
        public void InitFromCharacterData(CharacterDataSO data, int level)
        {
            _baseHp = data.baseHp;
            _baseAtk = data.baseAtk;
            _baseDef = data.baseDef;
            _baseCritRate = data.baseCritRate;
            _baseAttackSpeed = data.attackSpeed;
            _baseMoveSpeed = data.moveSpeed;

            // 레벨에 따른 성장치를 보너스로 적용 (레벨 1 기준이므로 level - 1)
            int growthLevel = Mathf.Max(0, level - 1);
            _bonusHp = data.hpPerLevel * growthLevel;
            _bonusAtk = data.atkPerLevel * growthLevel;
            _bonusDef = data.defPerLevel * growthLevel;
            _bonusCritRate = 0f;

            _modifiers.Clear();
            ResetModValues();

            _currentHp = MaxHp;
            _previousPowerScore = PowerScore;
            OnHpChanged?.Invoke(_currentHp, MaxHp);
        }

        /// <summary>
        /// MonsterDataSO와 층수 배율로 몬스터 스탯을 초기화한다.
        /// floorMultiplier가 1보다 크면 스탯에 배율이 곱해진다.
        /// </summary>
        public void InitFromMonsterData(MonsterDataSO data, int floorMultiplier = 1)
        {
            int multiplier = Mathf.Max(1, floorMultiplier);
            _baseHp = data.hp * multiplier;
            _baseAtk = data.atk * multiplier;
            _baseDef = data.def * multiplier;
            _baseCritRate = 0f;
            _baseAttackSpeed = data.attackSpeed;
            _baseMoveSpeed = data.moveSpeed;

            _bonusHp = 0;
            _bonusAtk = 0;
            _bonusDef = 0;
            _bonusCritRate = 0f;

            _modifiers.Clear();
            ResetModValues();

            _currentHp = MaxHp;
            OnHpChanged?.Invoke(_currentHp, MaxHp);
        }

        /// <summary>
        /// _mod 필드를 모두 0으로 리셋한다.
        /// </summary>
        private void ResetModValues()
        {
            _modHp = 0;
            _modAtk = 0;
            _modDef = 0;
            _modCritRate = 0f;
            _modAttackSpeed = 0f;
            _modDodgeRate = 0f;
            _modCritDmg = 0f;
            _modMoveSpeed = 0f;
            _modFinalDmg = 0f;
            _modDefPen = 0f;
            _modBossDmg = 0f;
            _modDamageTakenDecrease = 0f;
        }

        // ── 전투 동작 ──

        /// <summary>
        /// 회피 판정을 시도한다. 성공 시 OnDodge 이벤트를 발행하고 true를 반환한다.
        /// </summary>
        public bool TryDodge()
        {
            if (DodgeRate > 0f && UnityEngine.Random.value < DodgeRate)
            {
                OnDodge?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 데미지를 받아 HP를 감소시킨다. HP가 0 이하가 되면 OnDeath 이벤트를 발행한다.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (IsDead) return;
            if (IsInvincible) return;

            if (damage <= 0)
            {
                DamageTextManager.Instance?.ShowBlockedText(transform.position);
                return;
            }

            int actualDamage = Mathf.Max(1, damage);
            _currentHp = Mathf.Max(0, _currentHp - actualDamage);
            OnHpChanged?.Invoke(_currentHp, MaxHp);

            if (IsDead)
            {
                // WebGL 안전: delegate를 개별 호출하여 파괴된 구독자 보호
                var deathHandler = OnDeath;
                if (deathHandler != null)
                {
                    var delegates = deathHandler.GetInvocationList();
                    for (int i = 0; i < delegates.Length; i++)
                    {
                        try
                        {
                            ((Action)delegates[i]).Invoke();
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[CombatStats] OnDeath 핸들러 예외: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// HP를 회복한다. MaxHp를 초과하지 않는다.
        /// </summary>
        public void Heal(int amount)
        {
            if (IsDead) return;

            int healAmount = Mathf.Max(0, amount);
            _currentHp = Mathf.Min(MaxHp, _currentHp + healAmount);
            OnHpChanged?.Invoke(_currentHp, MaxHp);

            if (healAmount > 0)
                DamageTextManager.Instance?.ShowHealText(transform.position, healAmount);
        }

        /// <summary>
        /// HP를 최대치로 회복한다. 부활 시 사용.
        /// </summary>
        public void ResetHp()
        {
            _currentHp = MaxHp;
            OnHpChanged?.Invoke(_currentHp, MaxHp);
        }

        /// <summary>
        /// 사망 상태에서 지정 비율로 부활한다.
        /// </summary>
        public void ReviveWithRatio(float hpRatio)
        {
            _currentHp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * hpRatio));
            OnHpChanged?.Invoke(_currentHp, MaxHp);
        }

        // ── 기존 보정 API (하위 호환) ──

        /// <summary>
        /// 보정 스탯을 추가한다 (장비, 버프 등).
        /// 기존 호환용. 새 시스템은 AddModifier()를 사용할 것을 권장한다.
        /// </summary>
        [System.Obsolete("Use AddModifier() instead. This method will be removed in a future version.")]
        public void AddBonus(int hp = 0, int atk = 0, int def = 0, float critRate = 0f, float atkSpd = 0f, float dodgeRate = 0f, float critDmg = 0f, float moveSpeed = 0f)
        {
            _bonusHp += hp;
            _bonusAtk += atk;
            _bonusDef += def;
            _bonusCritRate += critRate;
            _bonusAttackSpeed += atkSpd;
            _bonusDodgeRate += dodgeRate;
            _bonusCritDmg += critDmg;
            _bonusMoveSpeed += moveSpeed;

            // HP 보너스가 추가되면 현재 HP도 같이 증가
            if (hp > 0)
            {
                _currentHp += hp;
            }

            // 수정자가 있으면 percent 보정 재계산 필요
            if (_modifiers.Count > 0)
            {
                RecalculateModifiers();
            }
            else
            {
                OnHpChanged?.Invoke(_currentHp, MaxHp);
                OnStatsChanged?.Invoke();
                PublishCpChangeIfNeeded();
            }
        }

        /// <summary>
        /// 보정 스탯을 제거한다.
        /// 기존 호환용. 새 시스템은 ClearModifiers()를 사용할 것을 권장한다.
        /// </summary>
        [System.Obsolete("Use ClearModifiers() instead. This method will be removed in a future version.")]
        public void RemoveBonus(int hp = 0, int atk = 0, int def = 0, float critRate = 0f, float atkSpd = 0f, float dodgeRate = 0f, float critDmg = 0f, float moveSpeed = 0f)
        {
            _bonusHp -= hp;
            _bonusAtk -= atk;
            _bonusDef -= def;
            _bonusCritRate -= critRate;
            _bonusAttackSpeed -= atkSpd;
            _bonusDodgeRate -= dodgeRate;
            _bonusCritDmg -= critDmg;
            _bonusMoveSpeed -= moveSpeed;

            // MaxHp가 줄어들면 currentHp도 클램프
            _currentHp = Mathf.Clamp(_currentHp, 0, MaxHp);

            if (_modifiers.Count > 0)
            {
                RecalculateModifiers();
            }
            else
            {
                OnHpChanged?.Invoke(_currentHp, MaxHp);
                OnStatsChanged?.Invoke();
                PublishCpChangeIfNeeded();
            }
        }
    }
}
