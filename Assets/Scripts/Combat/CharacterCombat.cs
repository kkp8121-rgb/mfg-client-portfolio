using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 캐릭터의 전투 로직을 담당하는 컴포넌트.
    /// 공격 타이밍, 데미지 계산, 사망/부활 처리를 관리한다.
    /// 전직 티어별 기본공격 스케일링, 직업 메카닉, 히트스톱을 통합한다.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    [RequireComponent(typeof(CombatStats))]
    public class CharacterCombat : MonoBehaviour
    {
        [Header("부활 설정")]
        [SerializeField] private float reviveDelay = 3.0f;

        [Header("기본공격 스케일링")]
        [SerializeField, Tooltip("전직 티어별 기본공격 파라미터 SO")]
        private BasicAttackScalingSO _attackScaling;

        private PlayerCharacter _controller;
        private CombatStats _stats;
        private SkillSystem _skillSystem;
        private JobMechanicSystem _jobMechanic;
        private StageManager _stageManager;

        private const float MIN_ATTACK_INTERVAL = 0.5f;
        private const int MIN_COMBO_FOR_TEXT = 3;
        private const float COMBO_RESET_TIME = 2f;

        private float _attackCooldown;
        private float _attackAnimTimer;
        private float _reviveTimer;
        private bool _isReviving;
        private bool _pendingDamage;
        private CombatStats _pendingTarget;
        private DamageResult _pendingResult;
        private bool _pendingAreaAttack;
        private float _pendingMultiplier;
        private float _pendingRange;
        private int _pendingHitCount;
        private int _pendingMaxTargets;
        /// <summary>
        /// 범위 공격의 중심점. 전사/궁수는 플레이어 위치, 마법사는 타겟(가까운 몬스터) 위치를 사용.
        /// 타겟 위치 폭발로 "가까운 적 + 주변 집단" 타격 — 맵 전체 AoE를 방지한다.
        /// </summary>
        private Vector3 _pendingAoeCenter;

        private int _comboCount;
        private float _lastHitTime;

        private void Awake()
        {
            _controller = GetComponent<PlayerCharacter>();
            _stats = GetComponent<CombatStats>();
            _skillSystem = GetComponent<SkillSystem>();
            _jobMechanic = GetComponent<JobMechanicSystem>();
            _stageManager = FindFirstObjectByType<StageManager>();
        }

        private void Update()
        {
            // 부활 타이머
            if (_isReviving)
            {
                _reviveTimer -= Time.deltaTime;
                if (_reviveTimer <= 0f)
                {
                    _isReviving = false;
                    _controller.Revive();
                }
                return;
            }

            // 콤보 타이머 리셋
            if (_comboCount > 0 && Time.time - _lastHitTime > COMBO_RESET_TIME)
                _comboCount = 0;

            // 공격 쿨다운 감소
            if (_attackCooldown > 0f)
                _attackCooldown -= Time.deltaTime;

            // 공격 애니메이션 후 데미지 적용 (딜레이)
            if (_pendingDamage || _pendingAreaAttack)
            {
                _attackAnimTimer -= Time.deltaTime;
                if (_attackAnimTimer <= 0f)
                {
                    if (_pendingAreaAttack)
                    {
                        _pendingAreaAttack = false;
                        DealAreaDamage(_pendingMultiplier, _pendingRange, _pendingHitCount, _pendingAoeCenter);

                        // 범위 공격 스킬 SFX
                        PlaySkillSfx();

                        // 범위 공격 VFX는 AoE 중심(마법사=타겟 위치, 전사/궁수=플레이어 위치)에서 생성
                        var activeSkill = _skillSystem != null ? _skillSystem.CurrentActiveSkill : null;
                        if (activeSkill != null && activeSkill.ActiveVfxSO != null)
                        {
                            bool flipX = transform.localScale.x > 0f;
                            SpriteSheetVfx.Spawn(activeSkill.ActiveVfxSO, _pendingAoeCenter,
                                Mathf.Max(_pendingRange * 2f, 3f), flipX: flipX);
                        }
                        else
                        {
                            SkillVfx.SpawnActiveAttackVfx(
                                _pendingAoeCenter,
                                _controller.CurrentTarget,
                                activeSkill,
                                _pendingRange,
                                transform.localScale.x);
                        }
                    }
                    else if (_pendingDamage)
                    {
                        _pendingDamage = false;
                        if (_pendingTarget != null && !_pendingTarget.IsDead)
                        {
                            _pendingTarget.TakeDamage(_pendingResult.damage);

                            // 크리티컬 히트 SFX
                            if (_pendingResult.isCritical)
                                AudioManager.Instance?.PlaySfx(SfxType.CritHit);

                            if (DamageTextManager.Instance != null)
                            {
                                var mc = _pendingTarget.GetComponent<MonsterController>();
                                DamageTextManager.Instance.Spawn(
                                    _pendingTarget.transform.position,
                                    _pendingResult.damage,
                                    _pendingResult.isCritical,
                                    isBoss: mc != null && mc.IsBoss,
                                    isPenetrating: _pendingResult.isPenetrating
                                );
                            }

                            // 콤보 카운터
                            _comboCount++;
                            _lastHitTime = Time.time;
                            if (_comboCount >= MIN_COMBO_FOR_TEXT)
                            {
                                DamageTextManager.Instance?.ShowComboText(
                                    _pendingTarget.transform.position, _comboCount);
                            }

                            ApplyOnHitEffects(_pendingTarget, _pendingResult.damage);

                            // 단일 타겟 VFX (캐릭터 facing 방향 전달)
                            var activeSkill = _skillSystem != null ? _skillSystem.CurrentActiveSkill : null;
                            if (activeSkill != null && activeSkill.ActiveVfxSO != null)
                            {
                                bool flipX = transform.localScale.x > 0f;
                                SpriteSheetVfx.Spawn(activeSkill.ActiveVfxSO, transform.position,
                                    3f, flipX: flipX);
                            }
                            else
                            {
                                SkillVfx.SpawnActiveAttackVfx(
                                    transform.position,
                                    _pendingTarget,
                                    activeSkill,
                                    0f,
                                    transform.localScale.x);
                            }

                            // 히트 VFX
                            if (activeSkill != null && activeSkill.HitVfxSO != null)
                            {
                                SpriteSheetVfx.Spawn(activeSkill.HitVfxSO,
                                    _pendingTarget.transform.position, 1.5f);
                            }

                            // 원거리 직업 투사체 트레일
                            SkillVfx.SpawnRangedTrailIfNeeded(
                                transform.position, _pendingTarget, activeSkill);
                        }
                    }
                }
            }

            // 스턴 중에는 공격 불가
            if (_controller.CurrentState == CharacterState.Stun)
                return;

            // Attack 상태일 때 공격 실행
            if (_controller.CurrentState == CharacterState.Attack)
                TryAttack();
        }

        /// <summary>
        /// 쿨다운이 끝났으면 타겟에게 공격을 실행한다.
        /// </summary>
        /// <summary>
        /// 공격 애니메이션 재생 후 딜레이를 두고 데미지를 적용한다.
        /// </summary>
        private void TryAttack()
        {
            if (_attackCooldown > 0f) return;

            CombatStats target = _controller.CurrentTarget;
            if (target == null || target.IsDead) return;

            // 공격 쿨다운 설정 (최소 간격 보장)
            float attackInterval = 1f / Mathf.Max(0.1f, _stats.AttackSpeed);
            _attackCooldown = Mathf.Max(attackInterval, MIN_ATTACK_INTERVAL);

            // 공격 애니메이션 재생
            var animBridge = GetComponent<CharacterAnimBridge>();
            if (animBridge != null)
                animBridge.PlayState(CharacterState.Attack);

            // 공격 SFX
            AudioManager.Instance?.PlaySfx(SfxType.SwordSwing, 0.1f);

            // 스킬 배율 적용
            float multiplier = _skillSystem != null ? _skillSystem.GetActiveMultiplier() : 1f;
            float areaRange = _skillSystem != null ? _skillSystem.GetActiveRange() : 0f;
            int hitCount = _skillSystem != null ? _skillSystem.GetActiveHitCount() : 1;

            // Active 스킬 사용 이벤트 발행 (퀘스트 SkillUse 추적용)
            if (_skillSystem?.CurrentActiveSkill != null)
            {
                var activeSkill = _skillSystem.CurrentActiveSkill;
                EventBus.Publish(new SkillUsedEvent
                {
                    SkillId = activeSkill.id,
                    SkillName = activeSkill.displayName,
                    SkillType = activeSkill.skillType
                });
            }
            int maxTargets = int.MaxValue;

            // 전직 티어별 기본공격 스케일링 적용
            if (_attackScaling != null)
            {
                int tier = SaveManager.Instance?.CurrentData?.player?.jobTier ?? 0;
                var scaling = _attackScaling.GetScaling(tier);
                maxTargets = scaling.maxTargets;
                hitCount = Mathf.Max(hitCount, scaling.hitCount);
                multiplier *= scaling.damageMultiplierBonus;
            }

            if (areaRange > 0f)
            {
                // 범위 공격: 딜레이 후 범위 데미지
                _pendingAreaAttack = true;
                _pendingMultiplier = multiplier;
                _pendingRange = areaRange;
                _pendingHitCount = hitCount;
                _pendingMaxTargets = maxTargets;
                _pendingDamage = false;
                // 마법사는 타겟 몬스터 위치를 폭발 중심으로 → 플레이어 주변 전체 타격이 아닌 "타겟 주변 집단"만 타격.
                // 전사/궁수는 기존대로 플레이어 위치 (근접 휘두르기/원거리 오리진).
                _pendingAoeCenter = (IsCurrentJobMage() && target != null)
                    ? target.transform.position
                    : transform.position;
            }
            else
            {
                // 단일 타겟 — 직업 메카닉 보너스 적용
                float mechanicMult = _jobMechanic != null
                    ? _jobMechanic.OnHitAndGetMultiplier(target.gameObject.GetInstanceID())
                    : 1f;

                // 보스 여부 확인 → BossDamage 적용
                var targetMc = target.GetComponent<MonsterController>();
                bool isTargetBoss = targetMc != null && targetMc.IsBoss;
                float bossDmg = isTargetBoss ? _stats.BossDamage : 0f;
                float targetDmgRed = target.DamageTakenDecrease;

                _pendingResult = DamageCalculator.CalculateWithResult(
                    _stats.Atk,
                    target.Def,
                    multiplier * mechanicMult,
                    _stats.CritRate,
                    _stats.CritDmg,
                    _stats.DefensePenetration,
                    _stats.FinalDamage,
                    bossDmg,
                    targetDmgRed
                );
                _pendingTarget = target;
                _pendingDamage = true;
                _pendingAreaAttack = false;

                // 히트스톱 (단일 타겟에서도 고배율 시 발동)
                if (HitStopSystem.Instance != null)
                    HitStopSystem.Instance.TriggerHitStop(multiplier * mechanicMult);
            }

            _attackAnimTimer = DAMAGE_DELAY;
        }

        private const float DAMAGE_DELAY = 0.2f;

        // 2026-04-23 캐시: LayerMask 문자열 조회는 매 공격마다 GC 없지만 느림. 1회 계산.
        private static int _monsterLayerMask = -1;
        private static int MonsterLayerMask
        {
            get
            {
                if (_monsterLayerMask == -1) _monsterLayerMask = LayerMask.GetMask("Monster");
                return _monsterLayerMask;
            }
        }

        /// <summary>
        /// 지정 중심점 기준 범위 데미지. 마법사는 타겟 몬스터 위치, 그 외는 플레이어 위치를 center로 받는다.
        /// </summary>
        private void DealAreaDamage(float multiplier, float range, int hitCount, Vector3 center)
        {
            var hits = Physics2D.OverlapCircleAll(center, range, MonsterLayerMask);

            int targetCount = 0;

            // 히트스톱 (범위 공격)
            if (HitStopSystem.Instance != null)
                HitStopSystem.Instance.TriggerHitStop(multiplier);

            foreach (var hit in hits)
            {
                // 최대 타겟 수 제한
                if (targetCount >= _pendingMaxTargets) break;

                var targetStats = hit.GetComponent<CombatStats>();
                if (targetStats == null || targetStats.IsDead) continue;

                targetCount++;

                // 직업 메카닉 보너스
                float mechanicMult = _jobMechanic != null
                    ? _jobMechanic.OnHitAndGetMultiplier(hit.gameObject.GetInstanceID())
                    : 1f;
                float finalMultiplier = multiplier * mechanicMult;

                int totalDamage = 0;
                for (int i = 0; i < hitCount; i++)
                {
                    var areaMc = targetStats.GetComponent<MonsterController>();
                    bool isBossTarget = areaMc != null && areaMc.IsBoss;
                    float bDmg = isBossTarget ? _stats.BossDamage : 0f;
                    float tDmgRed = targetStats.DamageTakenDecrease;

                    var result = DamageCalculator.CalculateWithResult(
                        _stats.Atk, targetStats.Def, finalMultiplier, _stats.CritRate, _stats.CritDmg,
                        _stats.DefensePenetration, _stats.FinalDamage, bDmg, tDmgRed);

                    targetStats.TakeDamage(result.damage);
                    totalDamage += result.damage;

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

                // 범위 공격 콤보
                _comboCount += hitCount;
                _lastHitTime = Time.time;
                if (_comboCount >= MIN_COMBO_FOR_TEXT)
                {
                    DamageTextManager.Instance?.ShowComboText(
                        targetStats.transform.position, _comboCount);
                }

                ApplyOnHitEffects(targetStats, totalDamage);

                // 히트 VFX (범위 공격)
                var areaSkill = _skillSystem != null ? _skillSystem.CurrentActiveSkill : null;
                if (areaSkill != null && areaSkill.HitVfxSO != null)
                {
                    SpriteSheetVfx.Spawn(areaSkill.HitVfxSO,
                        targetStats.transform.position, 1.5f);
                }
            }
        }

        /// <summary>
        /// 공격 적중 후 특수 효과 (흡혈, 넉백) 적용.
        /// </summary>
        private void ApplyOnHitEffects(CombatStats target, int damageDealt)
        {
            if (_skillSystem == null) return;

            // 흡혈
            float lifestealRate = _skillSystem.LifestealRate;
            if (lifestealRate > 0f)
            {
                int healAmount = Mathf.RoundToInt(damageDealt * lifestealRate);
                if (healAmount > 0)
                    _stats.Heal(healAmount);
            }

            // 넉백 제거됨 (2026-04-23 정책): idle-control.md 참조 — 몬스터가 접근 못 해 전투 정체 유발
        }

        /// <summary>
        /// 현재 활성 스킬의 특수효과에 맞는 SFX를 재생한다.
        /// </summary>
        private void PlaySkillSfx()
        {
            if (_skillSystem == null || AudioManager.Instance == null) return;
            var skill = _skillSystem.CurrentActiveSkill;
            if (skill == null) return;

            SfxType sfx = skill.specialEffect switch
            {
                SkillEffect.Burn => SfxType.SkillFire,
                SkillEffect.Freeze => SfxType.SkillIce,
                _ when skill.range > 0f => SfxType.SkillSlash,
                _ => SfxType.SwordSwing
            };
            AudioManager.Instance?.PlaySfx(sfx);
        }

        /// <summary>
        /// 사망 처리. PlayerCharacter의 OnDeath에서 호출 대신,
        /// 상태 변화를 감지하여 부활 타이머를 시작한다.
        /// </summary>
        public void StartReviveTimer()
        {
            _isReviving = true;
            _reviveTimer = reviveDelay;
        }

        /// <summary>
        /// 부활 타이머를 취소한다. 외부에서 즉시 부활 시 호출.
        /// </summary>
        public void CancelRevive()
        {
            _isReviving = false;
            _reviveTimer = 0f;
        }

        private void OnEnable()
        {
            _stats = GetComponent<CombatStats>();
            if (_stats != null)
            {
                _stats.OnDeath += OnDeath;
                _stats.OnDodge += OnDodged;
            }
        }

        private void OnDisable()
        {
            if (_stats != null)
            {
                _stats.OnDeath -= OnDeath;
                _stats.OnDodge -= OnDodged;
            }
        }

        private void OnDodged()
        {
            DamageTextManager.Instance?.ShowMissText(transform.position);
        }

        /// <summary>
        /// 사망 이벤트 핸들러.
        /// 불멸의 의지 패시브가 있으면 즉시 부활.
        /// StageManager가 존재하면 자동 부활하지 않음 (StageManager가 파밍 모드/재도전을 관리).
        /// </summary>
        private void OnDeath()
        {
            if (_skillSystem != null && _skillSystem.HasRevive)
            {
                float healRatio = _skillSystem.ConsumeRevive();
                _stats.ReviveWithRatio(healRatio);
                _controller.Revive();
#if UNITY_EDITOR
                Debug.Log($"[SkillSystem] 불멸의 의지 발동! HP {healRatio * 100}% 회복");
#endif
                return;
            }

            // StageManager가 사망을 관리하므로 자동 부활 타이머를 시작하지 않는다.
            // StageManager.OnPlayerDeath → EnterFarmingMode → 재도전 버튼 대기.
            // StageManager가 없는 경우(테스트 등)에만 자동 부활.
            if (_stageManager == null)
                StartReviveTimer();
        }

        /// <summary>
        /// 현재 직업이 마법사인지 판별한다. SaveData.player.jobId 기반 (Growth 순환 참조 방지).
        /// 마법사는 타겟 위치 AoE(근거리 집중 폭발)를 사용하여 "맵 전체 쓸기"를 방지한다.
        /// </summary>
        private static bool IsCurrentJobMage()
        {
            var id = SaveManager.Instance?.CurrentData?.player?.jobId;
            if (string.IsNullOrEmpty(id)) return false;
            return string.Equals(id, "mage", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
