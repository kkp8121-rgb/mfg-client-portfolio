using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 직업별 고유 메카닉을 처리한다.
    /// - 전사: 콤보 (N타마다 보너스 데미지)
    /// - 궁수: AS 비례 데미지 보너스
    /// - 마법사: 원소 중첩 (동일 대상 연속 타격 시 데미지 누적)
    /// CharacterCombat에서 데미지 계산 시 GetDamageMultiplierBonus()를 호출한다.
    /// </summary>
    public class JobMechanicSystem : MonoBehaviour
    {
        [Header("직업별 메카닉 SO")]
        [SerializeField] private JobMechanicSO _warriorMechanic;
        [SerializeField] private JobMechanicSO _archerMechanic;
        [SerializeField] private JobMechanicSO _mageMechanic;

        private CombatStats _stats;

        // 전사: 콤보 카운터
        private int _comboHitCount;
        private float _lastComboHitTime;

        // 마법사: 원소 중첩 (대상별)
        private readonly Dictionary<int, ElementalStack> _elementalStacks = new();
        private readonly List<int> _expiredStacks = new();

        private struct ElementalStack
        {
            public int stacks;
            public float expireTime;
        }

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
        }

        private void Update()
        {
            // 전사 콤보 리셋
            var mechanic = GetCurrentMechanic();
            if (mechanic != null && mechanic.jobType == JobType.Warrior)
            {
                if (_comboHitCount > 0 && Time.time - _lastComboHitTime > mechanic.comboResetTime)
                    _comboHitCount = 0;
            }

            // 마법사 원소 중첩 만료
            CleanupExpiredStacks();
        }

        /// <summary>
        /// 현재 직업의 메카닉 SO를 반환한다.
        /// SaveData에서 jobId를 읽어 직업을 판별한다 (Growth 순환 참조 방지).
        /// </summary>
        private JobMechanicSO GetCurrentMechanic()
        {
            string jobId = SaveManager.Instance?.CurrentData?.player?.jobId;
            if (string.IsNullOrEmpty(jobId)) return _warriorMechanic;

            return jobId.ToLowerInvariant() switch
            {
                "warrior" => _warriorMechanic,
                "archer" => _archerMechanic,
                "mage" => _mageMechanic,
                _ => _warriorMechanic
            };
        }

        /// <summary>
        /// 공격 적중 시 호출. 직업 메카닉에 따른 추가 데미지 배율을 반환한다.
        /// CharacterCombat에서 데미지 계산 후 곱한다.
        /// </summary>
        /// <param name="targetInstanceId">대상의 인스턴스 ID (마법사 원소 중첩 추적용)</param>
        /// <returns>추가 데미지 배율 (1.0 = 보너스 없음)</returns>
        public float OnHitAndGetMultiplier(int targetInstanceId)
        {
            var mechanic = GetCurrentMechanic();
            if (mechanic == null) return 1f;

            return mechanic.jobType switch
            {
                JobType.Warrior => ProcessWarriorCombo(mechanic),
                JobType.Archer => ProcessArcherAsBonus(mechanic),
                JobType.Mage => ProcessMageStacks(mechanic, targetInstanceId),
                _ => 1f
            };
        }

        /// <summary>
        /// 전사 콤보: N타마다 보너스 데미지.
        /// </summary>
        private float ProcessWarriorCombo(JobMechanicSO mechanic)
        {
            _comboHitCount++;
            _lastComboHitTime = Time.time;

            if (_comboHitCount % mechanic.comboInterval == 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[JobMechanicSystem] 전사 콤보 발동! {_comboHitCount}히트 (+{mechanic.comboBonusRate * 100}%)");
#endif
                return 1f + mechanic.comboBonusRate;
            }

            return 1f;
        }

        /// <summary>
        /// 궁수: 공격속도 비례 보너스. AS가 기준치를 초과하면 초과분 비례로 데미지 증가.
        /// </summary>
        private float ProcessArcherAsBonus(JobMechanicSO mechanic)
        {
            if (_stats == null) return 1f;

            float excessAs = _stats.AttackSpeed - mechanic.baseAttackSpeed;
            if (excessAs <= 0f) return 1f;

            float bonus = Mathf.Min(excessAs * mechanic.asBonusRatePerUnit, mechanic.asMaxBonusRate);
            return 1f + bonus;
        }

        /// <summary>
        /// 마법사: 원소 중첩. 같은 대상에 연속 타격 시 스택 누적.
        /// </summary>
        private float ProcessMageStacks(JobMechanicSO mechanic, int targetInstanceId)
        {
            float multiplier = 1f;

            if (_elementalStacks.TryGetValue(targetInstanceId, out var stack))
            {
                // 기존 스택에서 보너스 계산
                multiplier = 1f + stack.stacks * mechanic.stackBonusRate;

                // 스택 증가
                stack.stacks = Mathf.Min(stack.stacks + 1, mechanic.maxStacks);
                stack.expireTime = Time.time + mechanic.stackDuration;
                _elementalStacks[targetInstanceId] = stack;
            }
            else
            {
                // 첫 타격: 스택 1 시작 (보너스는 다음 타격부터)
                _elementalStacks[targetInstanceId] = new ElementalStack
                {
                    stacks = 1,
                    expireTime = Time.time + mechanic.stackDuration
                };
            }

            return multiplier;
        }

        /// <summary>
        /// 만료된 원소 중첩을 정리한다.
        /// </summary>
        private void CleanupExpiredStacks()
        {
            if (_elementalStacks.Count == 0) return;

            _expiredStacks.Clear();
            float now = Time.time;

            foreach (var kvp in _elementalStacks)
            {
                if (kvp.Value.expireTime < now)
                    _expiredStacks.Add(kvp.Key);
            }

            for (int i = 0; i < _expiredStacks.Count; i++)
            {
                _elementalStacks.Remove(_expiredStacks[i]);
            }
        }

        /// <summary>
        /// 현재 전사 콤보 카운트를 반환한다 (UI 표시용).
        /// </summary>
        public int GetWarriorComboCount() => _comboHitCount;

        /// <summary>
        /// 대상의 현재 원소 중첩 수를 반환한다 (UI 표시용).
        /// </summary>
        public int GetMageStacks(int targetInstanceId)
        {
            return _elementalStacks.TryGetValue(targetInstanceId, out var stack) ? stack.stacks : 0;
        }
    }
}
