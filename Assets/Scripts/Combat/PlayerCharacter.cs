using UnityEngine;
using MkLike.Core;
using MkLike.Economy;

namespace MkLike.Combat
{
    /// <summary>
    /// 플레이어 캐릭터의 탑다운 자동 전투 AI.
    /// 8방향 자유 이동, 가장 가까운 몬스터 자동 추적, 자동 공격.
    /// 키네마틱 이동 (transform.position 직접 변경).
    /// </summary>
    [RequireComponent(typeof(CombatStats))]
    [RequireComponent(typeof(CharacterCombat))]
    public class PlayerCharacter : MonoBehaviour
    {
        #region 설정값

        [Header("탐색 설정")]
        [SerializeField] private float detectionRange = 15.0f;
        [SerializeField] private float attackRange = 1.5f;

        [Header("레이어 설정")]
        [SerializeField] private LayerMask monsterLayer;

        [Header("맵 참조")]
        [SerializeField] private ArenaMap arenaMap;

        #endregion

        #region 컴포넌트 참조

        private CombatStats _stats;
        private CharacterCombat _combat;
        private CharacterAnimBridge _animBridge;

        #endregion

        #region 상태

        [Header("디버그")]
        [SerializeField] private CharacterState _currentState = CharacterState.Idle;

        public CharacterState CurrentState => _currentState;
        public CombatStats CurrentTarget => _currentTarget;

        private CombatStats _currentTarget;
        private float _targetSearchTimer;
        private const float TARGET_SEARCH_INTERVAL = 0.1f;
        private static readonly Collider2D[] _targetBuffer = new Collider2D[10];
        private bool _spumInitialized;

        private float _stunTimer;
        private const float DEFAULT_STUN_DURATION = 1.5f;

        private bool _isManualMoving;
        public bool IsManualMoving => _isManualMoving;

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            _combat = GetComponent<CharacterCombat>();
            _animBridge = GetComponent<CharacterAnimBridge>();
        }

        private void Start()
        {
            InitSpumVisual();
        }

        /// <summary>
        /// SpumCharacterManager를 통해 SPUM 비주얼을 초기 적용한다.
        /// JobSystem에 대한 직접 참조 없이 SpumCharacterManager가 알아서 처리.
        /// </summary>
        private void InitSpumVisual()
        {
            if (_spumInitialized) return;
            if (SpumCharacterManager.Instance == null) return;

            SpumCharacterManager.Instance.InitializePlayerVisual(transform);
            _spumInitialized = true;
        }

        private void Update()
        {
            if (_currentState != CharacterState.Die && _stats.IsDead)
            {
                ChangeState(CharacterState.Die);
                return;
            }

            _isManualMoving = JoystickBridge.HasInput
                && _currentState != CharacterState.Die
                && _currentState != CharacterState.Stun;

            // 조이스틱 물리 이동 (상태 머신과 독립)
            if (_isManualMoving)
                ApplyJoystickMovement();

            switch (_currentState)
            {
                case CharacterState.Idle:
                    UpdateIdle();
                    break;
                case CharacterState.Run:
                    UpdateRun();
                    break;
                case CharacterState.Attack:
                    UpdateAttack();
                    break;
                case CharacterState.Skill:
                    ChangeState(CharacterState.Idle);
                    break;
                case CharacterState.Hit:
                    ChangeState(CharacterState.Idle);
                    break;
                case CharacterState.Stun:
                    UpdateStun();
                    break;
                case CharacterState.Die:
                    break;
            }
        }

        #endregion

        #region 상태별 Update

        private void UpdateIdle()
        {
            _targetSearchTimer -= Time.deltaTime;
            if (_targetSearchTimer <= 0f)
            {
                _targetSearchTimer = TARGET_SEARCH_INTERVAL;
                FindTarget();
            }

            if (_currentTarget != null && !_currentTarget.IsDead)
            {
                UpdateFacing();
                float sqrDist = GetSqrDistanceToTarget();
                float sqrAttackRange = attackRange * attackRange;
                if (sqrDist <= sqrAttackRange)
                {
                    ChangeState(CharacterState.Attack);
                }
                else
                {
                    ChangeState(CharacterState.Run);
                }
            }
            else
            {
                if (_isManualMoving)
                    ChangeState(CharacterState.Run); // 조이스틱 이동 중 → Run 전환
                else
                    MoveTowardsCenter();
            }
        }

        private void UpdateRun()
        {
            _targetSearchTimer -= Time.deltaTime;
            if (_targetSearchTimer <= 0f)
            {
                _targetSearchTimer = TARGET_SEARCH_INTERVAL;
                FindTarget();
            }

            if (_currentTarget != null && !_currentTarget.IsDead)
            {
                float sqrDist = GetSqrDistanceToTarget();
                if (sqrDist <= attackRange * attackRange)
                {
                    ChangeState(CharacterState.Attack);
                    return;
                }

                if (!_isManualMoving)
                {
                    MoveTowardsTarget();
                    UpdateFacing();
                }
                // 조이스틱 사용 시 물리 이동은 ApplyJoystickMovement에서 처리
            }
            else
            {
                if (!_isManualMoving)
                    ChangeState(CharacterState.Idle);
                // 조이스틱 이동 중 타겟 없으면 Run 유지
            }
        }

        private void UpdateAttack()
        {
            if (_currentTarget == null || _currentTarget.IsDead)
            {
                _currentTarget = null;
                ChangeState(CharacterState.Idle);
                return;
            }

            UpdateFacing();

            float sqrDist = GetSqrDistanceToTarget();
            float disengageRange = attackRange * 1.5f;
            if (sqrDist > disengageRange * disengageRange)
            {
                ChangeState(CharacterState.Run);
            }
        }


        private void UpdateStun()
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f)
            {
                _stunTimer = 0f;
                // ChangeState 가드를 우회하여 직접 복귀 (스턴 가드가 Idle 전환을 차단하므로)
                _currentState = CharacterState.Idle;
                if (_animBridge == null)
                    _animBridge = GetComponent<CharacterAnimBridge>();
                _animBridge?.PlayState(CharacterState.Idle);
            }
        }

        #endregion

        #region 이동

        /// <summary>
        /// 조이스틱 방향으로 물리 이동. 상태 머신과 독립적으로 동작한다.
        /// </summary>
        private void ApplyJoystickMovement()
        {
            Vector2 dir = JoystickBridge.Direction;
            Vector2 newPos = (Vector2)transform.position + dir * _stats.MoveSpeed * Time.deltaTime;

            if (arenaMap != null)
                newPos = arenaMap.ClampPosition(newPos);

            transform.position = new Vector3(newPos.x, newPos.y, 0f);

            // 이동 방향으로 facing 전환
            if (Mathf.Abs(dir.x) > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = dir.x > 0 ? -1f : 1f;
                transform.localScale = scale;
            }
        }

        private void MoveTowardsTarget()
        {
            if (_currentTarget == null) return;

            Vector2 direction = ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).normalized;
            Vector2 newPos = (Vector2)transform.position + direction * _stats.MoveSpeed * Time.deltaTime;

            if (arenaMap != null)
                newPos = arenaMap.ClampPosition(newPos);

            transform.position = new Vector3(newPos.x, newPos.y, 0f);
        }

        private void MoveTowardsCenter()
        {
            if (arenaMap == null) return;

            Vector2 center = arenaMap.GetArenaCenter();
            float sqrDistToCenter = ((Vector2)transform.position - center).sqrMagnitude;

            if (sqrDistToCenter > 2f * 2f)
            {
                Vector2 direction = (center - (Vector2)transform.position).normalized;
                Vector2 newPos = (Vector2)transform.position + direction * _stats.MoveSpeed * 0.5f * Time.deltaTime;
                newPos = arenaMap.ClampPosition(newPos);
                transform.position = new Vector3(newPos.x, newPos.y, 0f);

                // 방향 전환 (중앙 방향)
                float dirX = center.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.01f)
                {
                    Vector3 scale = transform.localScale;
                    scale.x = dirX > 0 ? -1f : 1f;
                    transform.localScale = scale;
                }

                // 상태는 Idle 유지하되, 이동 중 Run 애니메이션 직접 재생
                if (_animBridge == null)
                    _animBridge = GetComponent<CharacterAnimBridge>();
                _animBridge?.PlayState(CharacterState.Run);
            }
            else
            {
                // 중앙 근처 도착 — Idle 애니메이션 복원
                if (_animBridge == null)
                    _animBridge = GetComponent<CharacterAnimBridge>();
                _animBridge?.PlayState(CharacterState.Idle);
            }
        }

        #endregion

        #region 적 탐색

        private void FindTarget()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position, detectionRange, _targetBuffer, monsterLayer
            );

            if (count == 0)
            {
                _currentTarget = null;
                return;
            }

            Vector2 myPos = transform.position;
            float closestSqrDist = float.MaxValue;
            CombatStats closest = null;

            for (int i = 0; i < count; i++)
            {
                CombatStats targetStats = _targetBuffer[i].GetComponent<CombatStats>();
                if (targetStats == null || targetStats.IsDead) continue;

                float sqrDist = ((Vector2)_targetBuffer[i].transform.position - myPos).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = targetStats;
                }
            }

            _currentTarget = closest;
        }

        private float GetSqrDistanceToTarget()
        {
            if (_currentTarget == null) return float.MaxValue;
            return ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).sqrMagnitude;
        }

        #endregion

        #region 상태 전이

        private void ChangeState(CharacterState newState)
        {
            if (_currentState == newState) return;

            // 스턴 중에는 사망만 허용
            if (_currentState == CharacterState.Stun && newState != CharacterState.Die)
                return;

            _currentState = newState;

            // _animBridge가 null이면 재검색 (런타임 컴포넌트 추가 대응)
            if (_animBridge == null)
                _animBridge = GetComponent<CharacterAnimBridge>();

            if (_animBridge != null)
                _animBridge.PlayState(newState);

            // 상태 전환 시 즉시 타겟 방향으로 전환
            if (newState == CharacterState.Attack || newState == CharacterState.Run)
                UpdateFacing();
        }

        #endregion

        #region 방향 전환

        private void UpdateFacing()
        {
            if (_currentTarget == null || _currentTarget.IsDead) return;

            float directionX = _currentTarget.transform.position.x - transform.position.x;
            if (Mathf.Abs(directionX) < 0.01f) return;

            Vector3 scale = transform.localScale;
            scale.x = directionX > 0 ? -1f : 1f;
            transform.localScale = scale;
        }

        #endregion

        #region 외부 호출

        public void SetArenaMap(ArenaMap map)
        {
            arenaMap = map;
        }

        public void ResetToSpawnPoint()
        {
            if (arenaMap != null)
            {
                Vector2 spawn = arenaMap.GetPlayerSpawnWorld();
                transform.position = new Vector3(spawn.x, spawn.y, 0f);
            }
            else
            {
                transform.position = Vector3.zero;
            }

            _currentTarget = null;
            if (_currentState != CharacterState.Die)
                ChangeState(CharacterState.Idle);
        }


        /// <summary>
        /// Die 상태에서 복귀한다. HP는 호출자가 미리 설정해야 한다.
        /// (StageManager.RetryStage → ReviveWithRatio → Revive 순서)
        /// HP가 아직 0이면 MaxHp로 리셋한다.
        /// </summary>
        public void Revive()
        {
            if (_currentState != CharacterState.Die) return;

            // HP가 아직 0이면 풀 회복 (안전장치)
            if (_stats.IsDead)
                _stats.ResetHp();

            if (_combat != null)
                _combat.CancelRevive();
            ChangeState(CharacterState.Idle);
        }

        /// <summary>
        /// 플레이어를 스턴 상태로 만든다. 스턴 중에는 이동/공격 불가.
        /// </summary>
        public void Stun(float duration = DEFAULT_STUN_DURATION)
        {
            if (_currentState == CharacterState.Die) return;

            _stunTimer = duration;
            _currentState = CharacterState.Stun;

            if (_animBridge == null)
                _animBridge = GetComponent<CharacterAnimBridge>();
            _animBridge?.PlayState(CharacterState.Stun);
        }

        public bool IsStunned => _currentState == CharacterState.Stun;

        public void SetAttackRange(float range)
        {
            attackRange = range;
        }

        #endregion

        #region 디버그

        private void OnDrawGizmos()
        {
            // 항상 표시 (Selected가 아닌 항상)
            // 노란색: 적 탐지 범위
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // 빨간색: 기본 공격 범위 (OverlapCircle 범위)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 캐릭터 facing 방향 화살표
            bool facingRight = transform.localScale.x < 0f;
            Vector3 facingDir = facingRight ? Vector3.right : Vector3.left;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + facingDir * attackRange);

            // 스킬 범위 (SkillSystem이 있으면)
            var skillSystem = GetComponent<SkillSystem>();
            if (skillSystem != null)
            {
                float skillRange = skillSystem.GetActiveRange();
                if (skillRange > 0f)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireSphere(transform.position, skillRange);
                }
            }
        }

        #endregion
    }
}
