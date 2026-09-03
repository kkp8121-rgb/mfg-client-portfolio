using UnityEngine;
using DG.Tweening;
using Lean.Pool;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 탑다운 몬스터 AI.
    /// 플레이어 추적 + separation force (겹침 방지).
    /// 키네마틱 이동 (transform.position 직접 변경).
    /// </summary>
    [RequireComponent(typeof(CombatStats))]
    [RequireComponent(typeof(MonsterCombat))]
    public class MonsterController : MonoBehaviour, IPoolable, IUpdatable
    {
        #region 설정값

        [Header("탐색 설정")]
        [SerializeField] private float detectionRange = 20.0f;
        [SerializeField] private float attackRange = 1.5f;

        [Header("레이어 설정")]
        [SerializeField] private LayerMask playerLayer;

        [Header("히트 설정")]
        [SerializeField] private float hitDuration = 0.2f;

        [Header("사망 설정")]
        [Tooltip("Die 애니메이션 시작부터 Despawn까지 총 시간 (초). SPUM Die 애니 전체 + 페이드 구간을 포함.")]
        [SerializeField] private float dieDelay = 1.4f;
        [Tooltip("Die 애니 재생 시간 (초). 이 시간 경과 후 페이드아웃 시작. dieDelay - _deathFadeDelay = 페이드 길이.")]
        [SerializeField] private float _deathFadeDelay = 1.0f;

        [Header("Separation")]
        [SerializeField] private float separationRadius = 1.5f;
        [SerializeField] private float separationForce = 3.5f;

        #endregion

        #region 컴포넌트 참조

        private CombatStats _stats;
        private MonsterCombat _combat;
        private Collider2D _collider;
        private CharacterAnimBridge _animBridge;
        private HitFlashEffect _hitFlash;
        private DeathEffect _deathEffect;

        #endregion

        #region 상태

        [Header("디버그")]
        [SerializeField] private MonsterState _currentState = MonsterState.Idle;

        public MonsterState CurrentState => _currentState;
        public CombatStats CurrentTarget => _currentTarget;

        /// <summary>보스 여부 (스포너에서 설정)</summary>
        public bool IsBoss { get; set; }

        /// <summary>챕터 보스 여부 (미니보스와 구분, 스포너에서 설정)</summary>
        public bool IsChapterBoss { get; set; }

        /// <summary>엘리트 몬스터 여부 (EliteSummonManager에서 설정)</summary>
        public bool IsElite { get; set; }

        /// <summary>엘리트 소환 레벨 (드롭 등급 결정용)</summary>
        public int EliteSummonLevel { get; set; }

        /// <summary>ArenaMap 참조 (스포너에서 설정)</summary>
        public ArenaMap ArenaMap { get; set; }

        private CombatStats _currentTarget;
        private float _hitTimer;
        private float _dieTimer;
        private bool _isDying;

        // 플레이어 캐싱 (물리 쿼리 제거)
        private Transform _playerTransform;
        private CombatStats _playerStats;

        // 탐색 주기 분산
        private float _targetSearchTimer;
        private const float TARGET_SEARCH_INTERVAL = 0.15f;

        private SpriteRenderer[] _spriteRenderers;
        private Tween _fadeTween;

        /// <summary>
        /// 런타임에 SPUM 비주얼이 교체된 후 호출 — 새 자식 SpriteRenderer까지 fade/tint 대상에 포함.
        /// SpumCharacterManager.ApplySpumPrefab 마지막에서 호출한다.
        /// </summary>
        public void RefreshSpriteRenderers()
        {
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        // CC 상태
        private float _burnTimer;
        private float _burnTickTimer;
        private int _burnDamagePerTick;
        private float _slowTimer;
        private float _speedMultiplier = 1f;

        // 원킬/오버킬 판정
        private int _hitCount;
        private int _maxHpOnSpawn;

        // UpdateManager에서 전달받은 deltaTime 캐시
        private float _deltaTime;

        // separation 캐시
        private static readonly Collider2D[] _nearbyBuffer = new Collider2D[20];

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            _combat = GetComponent<MonsterCombat>();
            _collider = GetComponent<Collider2D>();
            _animBridge = GetComponent<CharacterAnimBridge>();
            _hitFlash = GetComponent<HitFlashEffect>();
            _deathEffect = GetComponent<DeathEffect>();
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnEnable()
        {
            if (_stats != null)
            {
                _stats.OnDeath += OnDeath;
                _maxHpOnSpawn = _stats.MaxHp;
            }
            _hitCount = 0;
            // Instance가 null이면 GetOrCreate로 확보 (Awake 타이밍 버그 방어)
            UpdateManager.GetOrCreate().Register(this);
        }

        private void OnDisable()
        {
            if (_stats != null)
                _stats.OnDeath -= OnDeath;
            UpdateManager.Instance?.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            _deltaTime = deltaTime;

            if (_isDying)
            {
                _dieTimer -= deltaTime;
                if (_dieTimer <= 0f)
                {
                    _isDying = false;
                    ReturnToPool();
                }
                return;
            }

            if (_currentState != MonsterState.Die && _stats.IsDead)
            {
                ChangeState(MonsterState.Die);
                return;
            }

            // CC 업데이트
            UpdateBurn();
            UpdateSlow();

            switch (_currentState)
            {
                case MonsterState.Idle:
                    UpdateIdle();
                    break;
                case MonsterState.Chase:
                    UpdateChase();
                    break;
                case MonsterState.Attack:
                    UpdateAttack();
                    break;
                case MonsterState.Hit:
                    UpdateHit();
                    break;
                case MonsterState.Die:
                    break;
            }
        }

        #endregion

        #region 상태별 Update

        private void UpdateIdle()
        {
            _targetSearchTimer -= _deltaTime;
            if (_targetSearchTimer <= 0f)
            {
                _targetSearchTimer = TARGET_SEARCH_INTERVAL + UnityEngine.Random.Range(0f, 0.05f);
                FindTarget();
            }

            if (_currentTarget != null && !_currentTarget.IsDead)
            {
                float sqrDist = GetSqrDistanceToTarget();
                if (sqrDist <= attackRange * attackRange)
                    ChangeState(MonsterState.Attack);
                else
                    ChangeState(MonsterState.Chase);
            }
        }

        private void UpdateChase()
        {
            _targetSearchTimer -= _deltaTime;
            if (_targetSearchTimer <= 0f)
            {
                _targetSearchTimer = TARGET_SEARCH_INTERVAL + UnityEngine.Random.Range(0f, 0.05f);
                FindTarget();
            }

            if (_currentTarget == null || _currentTarget.IsDead)
            {
                _currentTarget = null;
                ChangeState(MonsterState.Idle);
                return;
            }

            float sqrDist = GetSqrDistanceToTarget();
            if (sqrDist <= attackRange * attackRange)
            {
                ChangeState(MonsterState.Attack);
                return;
            }

            MoveTowardsTarget();
            UpdateFacing();
        }

        private void UpdateAttack()
        {
            if (_currentTarget == null || _currentTarget.IsDead)
            {
                _currentTarget = null;
                ChangeState(MonsterState.Idle);
                return;
            }

            float sqrDist = GetSqrDistanceToTarget();
            if (sqrDist > attackRange * attackRange)
            {
                ChangeState(MonsterState.Chase);
                return;
            }

            UpdateFacing();
        }

        private void UpdateHit()
        {
            _hitTimer -= _deltaTime;
            if (_hitTimer <= 0f)
                ChangeState(MonsterState.Idle);
        }

        #endregion

        #region 이동

        private void MoveTowardsTarget()
        {
            if (_currentTarget == null) return;

            // 플레이어 방향
            Vector2 toPlayer = ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).normalized;

            // separation force (주변 몬스터와 겹침 방지)
            Vector2 separation = CalculateSeparation();

            Vector2 moveDir = (toPlayer + separation).normalized;
            Vector2 newPos = (Vector2)transform.position + moveDir * _stats.MoveSpeed * _speedMultiplier * _deltaTime;

            // 아레나 경계 제한
            if (ArenaMap != null)
                newPos = ArenaMap.ClampPosition(newPos);

            transform.position = new Vector3(newPos.x, newPos.y, 0f);
        }

        private Vector2 CalculateSeparation()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, separationRadius, _nearbyBuffer);
            Vector2 separationDir = Vector2.zero;
            int neighborCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (_nearbyBuffer[i] == null || _nearbyBuffer[i].gameObject == gameObject) continue;
                if (_nearbyBuffer[i].GetComponent<MonsterController>() == null) continue;

                Vector2 away = (Vector2)transform.position - (Vector2)_nearbyBuffer[i].transform.position;
                float dist = away.magnitude;
                if (dist > 0.01f && dist < separationRadius)
                {
                    // 가까울수록 강한 반발 (거리 기반 감쇄)
                    float strength = 1f - (dist / separationRadius);
                    separationDir += (away / dist) * strength;
                    neighborCount++;
                }
            }

            if (neighborCount > 0)
                separationDir = separationDir / neighborCount * separationForce;

            return separationDir;
        }

        #endregion

        #region 상태 전이

        private void ChangeState(MonsterState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;

            // _animBridge null 방어 (풀링 환경에서 Awake 누락 가능)
            if (_animBridge == null)
                _animBridge = GetComponent<CharacterAnimBridge>();

            if (_animBridge != null)
                _animBridge.PlayMonsterState(newState);

            switch (newState)
            {
                case MonsterState.Hit:
                    _hitTimer = hitDuration;
                    break;

                case MonsterState.Die:
                    if (_collider != null)
                        _collider.enabled = false;

                    // 히트 DOPunchScale 잔여 트윈 정리 + 스케일 복원 (찌그러짐 방지)
                    transform.DOKill();
                    transform.localScale = IsBoss
                        ? Vector3.one * (IsChapterBoss ? 2.5f : 1.5f)
                        : Vector3.one;

                    // 원킬/오버킬 판정 (애니메이션은 공통, 텍스트+셰이크만)
                    bool isOneHitKill = _hitCount <= 1 && _maxHpOnSpawn > 0;
                    if (isOneHitKill)
                    {
                        DamageTextManager.Instance?.SpawnOverkill(transform.position);
                        ScreenShakeManager.Instance?.ShakeMedium();
                    }

                    EventBus.Publish(new MonsterDiedEvent
                    {
                        Monster = gameObject,
                        Position = transform.position
                    });

                    _isDying = true;
                    _dieTimer = dieDelay;

                    // Dead 애니메이션 재생 (PlayMonsterState(Die)는 위에서 이미 호출됨)
                    // 애니메이션 시간(_deathFadeDelay) 경과 후 페이드아웃
                    float fadeDuration = Mathf.Max(0.1f, dieDelay - _deathFadeDelay);
                    FadeOutDelayed(_deathFadeDelay, fadeDuration);
                    break;
            }
        }

        #endregion

        #region 플레이어 탐색

        /// <summary>
        /// 스포너에서 플레이어 참조를 캐싱한다. 물리 쿼리 대신 직접 참조 사용.
        /// </summary>
        public void SetPlayer(Transform player, CombatStats stats)
        {
            _playerTransform = player;
            _playerStats = stats;
        }

        private void FindTarget()
        {
            if (_playerStats == null || _playerStats.IsDead)
            {
                _currentTarget = null;
                return;
            }

            float sqrDist = ((Vector2)transform.position - (Vector2)_playerTransform.position).sqrMagnitude;
            _currentTarget = sqrDist <= detectionRange * detectionRange ? _playerStats : null;
        }

        private float GetSqrDistanceToTarget()
        {
            if (_currentTarget == null) return float.MaxValue;
            return ((Vector2)transform.position - (Vector2)_currentTarget.transform.position).sqrMagnitude;
        }

        #endregion

        #region 방향 전환

        private void UpdateFacing()
        {
            if (_currentTarget == null) return;

            float direction = _currentTarget.transform.position.x - transform.position.x;
            if (Mathf.Abs(direction) < 0.01f) return;

            Vector3 scale = transform.localScale;
            float absX = Mathf.Abs(scale.x);
            scale.x = direction > 0 ? -absX : absX;
            transform.localScale = scale;
        }

        #endregion

        #region 외부 호출

        public void OnHit()
        {
            if (_currentState == MonsterState.Die) return;
            if (_stats.IsDead) return;

            _hitCount++;
            ChangeState(MonsterState.Hit);

            Vector2 hitDir = _playerTransform != null
                ? ((Vector2)transform.position - (Vector2)_playerTransform.position).normalized
                : Vector2.right;

            // 피격 플래시 연출
            if (_hitFlash != null)
                _hitFlash.PlayHitFlash(hitDir);

            // 히트 VFX (스프라이트 시트 또는 2D 도형 폴백)
            SkillVfx.SpawnHit(transform.position, new Color(1f, 1f, 1f, 0.8f), 0.6f);

            AudioManager.Instance?.PlaySfx(SfxType.MonsterHit, 0.15f);
        }

        /// <summary>
        /// 지정 시간 동안 스턴 상태로 만든다.
        /// </summary>
        public void Stun(float duration)
        {
            if (_currentState == MonsterState.Die) return;
            if (_stats.IsDead) return;
            _hitTimer = Mathf.Max(_hitTimer, duration);
            ChangeState(MonsterState.Hit);
        }

        // Knockback() 메서드 제거됨 (2026-04-23): idle-control.md 참조
        // 호출부 모두 제거 (CharacterCombat, SkillSystem.DealAreaDamage)

        /// <summary>
        /// 화상 DoT. duration 동안 0.5초마다 totalDamage/틱수 만큼 데미지.
        /// </summary>
        public void ApplyBurn(int totalDamage, float duration)
        {
            if (_currentState == MonsterState.Die || _stats.IsDead) return;

            int tickCount = Mathf.Max(1, Mathf.RoundToInt(duration / 0.5f));
            _burnDamagePerTick = Mathf.Max(1, totalDamage / tickCount);
            _burnTimer = duration;
            _burnTickTimer = 0.5f;

            SkillVfx.SpawnDotTick(transform.position, new Color(1f, 0.5f, 0.1f));
        }

        /// <summary>
        /// 빙결: duration 동안 행동불능 + 파란 시각 효과.
        /// </summary>
        public void ApplyFreeze(float duration)
        {
            if (_currentState == MonsterState.Die || _stats.IsDead) return;

            Stun(duration);
            SkillVfx.SpawnFreezeEffect(transform.position, duration);
            TintSprites(new Color(0.5f, 0.7f, 1f));

            DOTween.Sequence()
                .AppendInterval(duration)
                .AppendCallback(() => TintSprites(Color.white))
                .SetLink(gameObject);
        }

        /// <summary>
        /// 감속: duration 동안 이동속도 rate만큼 감소.
        /// </summary>
        public void ApplySlow(float rate, float duration)
        {
            if (_currentState == MonsterState.Die || _stats.IsDead) return;

            _speedMultiplier = 1f - Mathf.Clamp01(rate);
            _slowTimer = duration;

            SkillVfx.SpawnSlowEffect(transform.position);
            TintSprites(new Color(0.7f, 0.8f, 1f));
        }

        #endregion

        #region CC 업데이트

        private void UpdateBurn()
        {
            if (_burnTimer <= 0f) return;

            _burnTimer -= _deltaTime;
            _burnTickTimer -= _deltaTime;

            if (_burnTickTimer <= 0f && !_stats.IsDead)
            {
                _burnTickTimer = 0.5f;
                _stats.TakeDamage(_burnDamagePerTick);
                SkillVfx.SpawnDotTick(transform.position, new Color(1f, 0.5f, 0.1f), 0.3f);
            }

            if (_burnTimer <= 0f)
            {
                _burnTimer = 0f;
                _burnDamagePerTick = 0;
            }
        }

        private void UpdateSlow()
        {
            if (_slowTimer <= 0f) return;

            _slowTimer -= _deltaTime;
            if (_slowTimer <= 0f)
            {
                _speedMultiplier = 1f;
                _slowTimer = 0f;
                TintSprites(Color.white);
            }
        }

        private void TintSprites(Color tint)
        {
            if (_spriteRenderers == null) return;
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] == null) continue;
                var c = _spriteRenderers[i].color;
                _spriteRenderers[i].color = new Color(tint.r, tint.g, tint.b, c.a);
            }
        }

        #endregion

        #region 페이드아웃

        private void FadeOut(float duration)
        {
            _fadeTween?.Kill();
            // 모든 SpriteRenderer의 알파를 0으로 페이드
            _fadeTween = DOTween.To(() => 1f, SetAllSpriteAlpha, 0f, duration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject);
        }

        private void FadeOutDelayed(float delay, float duration)
        {
            _fadeTween?.Kill();
            _fadeTween = DOTween.To(() => 1f, SetAllSpriteAlpha, 0f, duration)
                .SetDelay(delay)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject);
        }

        private void SetAllSpriteAlpha(float alpha)
        {
            if (_spriteRenderers == null) return;
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] == null) continue;
                var c = _spriteRenderers[i].color;
                c.a = alpha;
                _spriteRenderers[i].color = c;
            }
        }

        private void ResetSpriteAlpha()
        {
            _fadeTween?.Kill();
            _fadeTween = null;
            SetAllSpriteAlpha(1f);
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
            transform.DOKill();
        }

        #endregion

        #region 풀링 (Lean.Pool.IPoolable)

        public void OnSpawn()
        {
            _currentState = MonsterState.Idle;
            _currentTarget = null;
            _isDying = false;
            _hitTimer = 0f;
            _dieTimer = 0f;
            _burnTimer = 0f;
            _burnDamagePerTick = 0;
            _slowTimer = 0f;
            _speedMultiplier = 1f;
            _targetSearchTimer = 0f;
            IsChapterBoss = false;
            _hitCount = 0;
            _maxHpOnSpawn = _stats != null ? _stats.MaxHp : 0;

            if (_collider != null)
                _collider.enabled = true;

            // 컴포넌트 lazy init (풀링 시 Awake 재호출 안 됨)
            if (_animBridge == null) _animBridge = GetComponent<CharacterAnimBridge>();
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlashEffect>();
            if (_deathEffect == null) _deathEffect = GetComponent<DeathEffect>();

            // SPUM 자식이 런타임에 추가됐을 수 있으므로 SR 캐시 갱신 후 alpha 복원
            RefreshSpriteRenderers();
            ResetSpriteAlpha();

            // 스케일 리셋 (풀 재사용 시 안전)
            transform.localScale = IsBoss
                ? Vector3.one * (IsChapterBoss ? 2.5f : 1.5f)
                : Vector3.one;

            if (_animBridge != null)
                _animBridge.PlayMonsterState(MonsterState.Idle);
        }

        public void OnDespawn()
        {
            _currentTarget = null;
            _isDying = false;

            // DOTween 정리 (FadeOut 등 중간값 잔류 방지)
            _fadeTween?.Kill();
            _fadeTween = null;
            transform.DOKill();

            ResetSpriteAlpha();
        }

        private void ReturnToPool()
        {
            LeanPool.Despawn(gameObject);
        }

        #endregion

        #region 이벤트 핸들러

        private void OnDeath()
        {
            if (_currentState != MonsterState.Die)
                ChangeState(MonsterState.Die);
        }

        #endregion

        #region 디버그

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, separationRadius);
        }

        #endregion
    }
}
