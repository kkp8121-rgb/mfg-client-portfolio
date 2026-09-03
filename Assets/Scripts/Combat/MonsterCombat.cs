using UnityEngine;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 몬스터의 전투 로직을 담당하는 컴포넌트.
    /// 공격 쿨다운 관리, 데미지 계산 및 적용을 처리한다.
    /// </summary>
    [RequireComponent(typeof(MonsterController))]
    [RequireComponent(typeof(CombatStats))]
    public class MonsterCombat : MonoBehaviour, IUpdatable
    {
        private MonsterController _controller;
        private CombatStats _stats;

        private float _attackCooldown;

        private void Awake()
        {
            _controller = GetComponent<MonsterController>();
            _stats = GetComponent<CombatStats>();
        }

        private void OnEnable()
        {
            UpdateManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            UpdateManager.Instance?.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            // 사망 상태면 처리 안함
            if (_stats.IsDead) return;

            // 공격 쿨다운 감소
            if (_attackCooldown > 0f)
            {
                _attackCooldown -= deltaTime;
            }

            // Attack 상태일 때 공격 실행
            if (_controller.CurrentState == MonsterState.Attack)
            {
                TryAttack();
            }
        }

        /// <summary>
        /// 쿨다운이 끝났으면 타겟에게 공격을 실행한다.
        /// </summary>
        private void TryAttack()
        {
            if (_attackCooldown > 0f) return;

            CombatStats target = _controller.CurrentTarget;
            if (target == null || target.IsDead) return;

            // 공격 쿨다운 설정 (공격속도 기반: 1/attackSpeed 초)
            float attackInterval = 1f / Mathf.Max(0.1f, _stats.AttackSpeed);
            _attackCooldown = attackInterval;

            // 회피 판정 (플레이어 DodgeRate)
            if (target.TryDodge())
            {
                return;
            }

            // 데미지 계산 (몬스터는 치명타 없음, 기본 공격 배율 1.0)
            DamageResult result = DamageCalculator.CalculateWithResult(
                _stats.Atk,
                target.Def,
                1.0f,
                _stats.CritRate
            );

            // 데미지 적용
            target.TakeDamage(result.damage);

            // 공격 애니메이션 재트리거 (매 타격마다)
            var animBridge = GetComponent<CharacterAnimBridge>();
            if (animBridge != null)
                animBridge.PlayMonsterState(MonsterState.Attack);
        }

        /// <summary>
        /// 풀에서 꺼낼 때 쿨다운을 초기화한다.
        /// MonsterController.OnSpawnFromPool에서 간접 호출되거나,
        /// IPoolable 인터페이스로 직접 호출됨.
        /// </summary>
        public void ResetCooldown()
        {
            _attackCooldown = 0f;
        }
    }
}
