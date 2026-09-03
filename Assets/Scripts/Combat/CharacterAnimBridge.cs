using System.Reflection;
using UnityEngine;

namespace MkLike.Combat
{
    public enum AttackAnimType { Normal, Bow, Magic }

    /// <summary>
    /// SPUM 캐릭터 애니메이션 브릿지.
    /// 3가지 컨트롤러 자동 감지:
    /// 1. NormalAnimator — RunState float 파라미터 (블렌드 트리)
    /// 2. AnimationNewController — Animator.Play() 직접 전환
    /// 3. SPUMController — Bool 트리거 (1_Move, 2_Attack 등)
    /// </summary>
    public class CharacterAnimBridge : MonoBehaviour
    {
        [Header("공격 애니메이션 타입")]
        [SerializeField] private AttackAnimType attackType = AttackAnimType.Normal;

        private Animator _spumAnimator;
        private string _currentClipKey;
        private bool _initialized;

        // 컨트롤러 모드
        private enum AnimMode { Direct, Param, SpumBool }
        private AnimMode _mode = AnimMode.Direct;

        // NormalAnimator 파라미터 해시
        private static readonly int H_RUN_STATE = Animator.StringToHash("RunState");
        private static readonly int H_ATTACK = Animator.StringToHash("Attack");
        private static readonly int H_DIE = Animator.StringToHash("Die");
        private static readonly int H_ATTACK_STATE = Animator.StringToHash("AttackState");
        private static readonly int H_NORMAL_STATE = Animator.StringToHash("NormalState");
        private static readonly int H_SKILL_STATE = Animator.StringToHash("SkillState");

        // SPUMController Bool 파라미터 해시
        private static readonly int H_MOVE = Animator.StringToHash("1_Move");
        private static readonly int H_SPUM_ATTACK = Animator.StringToHash("2_Attack");
        private static readonly int H_DAMAGED = Animator.StringToHash("3_Damaged");
        private static readonly int H_DEATH = Animator.StringToHash("4_Death");
        private static readonly int H_DEBUFF = Animator.StringToHash("5_Debuff");
        private static readonly int H_IS_DEATH = Animator.StringToHash("isDeath");

        private void Awake() => TryInitialize();
        private void Start() { if (!_initialized) TryInitialize(); }

        public void Reinitialize() { _initialized = false; _spumAnimator = null; TryInitialize(); }
        public void ForceReinitialize() { _initialized = false; _spumAnimator = null; _currentClipKey = null; TryInitialize(); }

        private void TryInitialize()
        {
            // 1. 활성 SPUM_Prefabs._anim에서 Animator 추출 (비활성 = Destroy 예정 제외)
            foreach (var comp in GetComponentsInChildren<Component>(false))
            {
                if (comp == null || comp.GetType().Name != "SPUM_Prefabs") continue;

                var animField = comp.GetType().GetField("_anim", BindingFlags.Public | BindingFlags.Instance);
                if (animField != null)
                    _spumAnimator = animField.GetValue(comp) as Animator;
                break;
            }

            // 2. fallback: 활성 자식에서 Animator 직접 검색
            if (_spumAnimator == null)
                _spumAnimator = GetComponentInChildren<Animator>(false);

            if (_spumAnimator == null) return;
            if (!_spumAnimator.enabled) _spumAnimator.enabled = true;

            // 3. 컨트롤러 타입 감지
            if (HasParam("RunState"))
                _mode = AnimMode.Param;           // NormalAnimator (블렌드 트리)
            else if (HasParam("1_Move"))
                _mode = AnimMode.SpumBool;        // SPUMController (Bool 트리거)
            else
                _mode = AnimMode.Direct;          // AnimationNewController (Play 직접)

            _initialized = true;
        }

        private bool HasParam(string name)
        {
            if (_spumAnimator == null) return false;
            foreach (var p in _spumAnimator.parameters)
                if (p.name == name) return true;
            return false;
        }

        public void SetAttackType(AttackAnimType type) => attackType = type;
        public string CurrentClipKey => _currentClipKey;
        public bool IsInitialized => _initialized;

        // ───── Player 상태 ─────

        public void PlayState(CharacterState state)
        {
            if (!_initialized) TryInitialize();
            if (_spumAnimator == null) return;

            string key = state.ToString();
            bool force = (state == CharacterState.Attack || state == CharacterState.Skill || state == CharacterState.Stun);
            if (!force && _currentClipKey == key) return;
            _currentClipKey = key;

            switch (_mode)
            {
                case AnimMode.Param: PlayParam(state); break;
                case AnimMode.SpumBool: PlaySpumBool(state); break;
                default: PlayDirect(state); break;
            }
        }

        // ───── Monster 상태 ─────

        public void PlayMonsterState(MonsterState state)
        {
            if (!_initialized) TryInitialize();
            if (_spumAnimator == null) return;

            string key = state.ToString();
            bool force = (state == MonsterState.Attack);
            if (!force && _currentClipKey == key) return;
            _currentClipKey = key;

            var cs = state switch
            {
                MonsterState.Chase => CharacterState.Run,
                MonsterState.Attack => CharacterState.Attack,
                MonsterState.Hit => CharacterState.Hit,
                MonsterState.Die => CharacterState.Die,
                _ => CharacterState.Idle
            };

            switch (_mode)
            {
                case AnimMode.Param: PlayParam(cs); break;
                case AnimMode.SpumBool: PlaySpumBool(cs); break;
                default: PlayDirect(cs); break;
            }
        }

        // ───── NormalAnimator: 파라미터 방식 ─────

        private void PlayParam(CharacterState state)
        {
            switch (state)
            {
                case CharacterState.Idle:
                    _spumAnimator.SetFloat(H_RUN_STATE, 0f);
                    _spumAnimator.ResetTrigger(H_ATTACK);
                    break;

                case CharacterState.Run:
                    _spumAnimator.SetFloat(H_RUN_STATE, 0.5f);
                    _spumAnimator.ResetTrigger(H_ATTACK);
                    break;

                case CharacterState.Attack:
                    _spumAnimator.SetFloat(H_RUN_STATE, 0f);
                    _spumAnimator.SetFloat(H_ATTACK_STATE, 0f);
                    _spumAnimator.SetFloat(H_NORMAL_STATE, AttackTypeToFloat());
                    _spumAnimator.SetTrigger(H_ATTACK);
                    break;

                case CharacterState.Skill:
                    _spumAnimator.SetFloat(H_RUN_STATE, 0f);
                    _spumAnimator.SetFloat(H_ATTACK_STATE, 1f);
                    _spumAnimator.SetFloat(H_SKILL_STATE, AttackTypeToFloat());
                    _spumAnimator.SetTrigger(H_ATTACK);
                    break;

                case CharacterState.Die:
                    _spumAnimator.SetFloat(H_RUN_STATE, 0f);
                    _spumAnimator.SetTrigger(H_DIE);
                    break;

                case CharacterState.Hit:
                    _spumAnimator.SetFloat(H_RUN_STATE, 1f);
                    break;

                case CharacterState.Stun:
                    _spumAnimator.SetFloat(H_RUN_STATE, 1f);
                    break;
            }
        }

        private float AttackTypeToFloat() => attackType switch
        {
            AttackAnimType.Normal => 0f,
            AttackAnimType.Bow => 0.5f,
            AttackAnimType.Magic => 1f,
            _ => 0f
        };

        // ───── AnimationNewController: Animator.Play() 직접 전환 ─────

        private void PlayDirect(CharacterState state)
        {
            string stateName = state switch
            {
                CharacterState.Idle => "0_idle",
                CharacterState.Run => "1_Run",
                CharacterState.Attack => GetAttackStateName(),
                CharacterState.Skill => GetSkillStateName(),
                CharacterState.Hit => "3_Debuff_Stun",
                CharacterState.Stun => "3_Debuff_Stun",
                CharacterState.Die => "4_Death",
                _ => "0_idle"
            };

            _spumAnimator.Play(stateName, 0, 0f);
        }

        private string GetAttackStateName() => attackType switch
        {
            AttackAnimType.Normal => "2_Attack_Normal",
            AttackAnimType.Bow => "2_Attack_Bow",
            AttackAnimType.Magic => "2_Attack_Magic",
            _ => "2_Attack_Normal"
        };

        private string GetSkillStateName() => attackType switch
        {
            AttackAnimType.Normal => "5_Skill_Normal",
            AttackAnimType.Bow => "5_Skill_Bow",
            AttackAnimType.Magic => "5_Skill_Magic",
            _ => "5_Skill_Normal"
        };

        // ───── SPUMController: Bool 트리거 방식 ─────
        // 파라미터: 1_Move(Bool), 2_Attack(Bool), 3_Damaged(Bool),
        //          4_Death(Bool), 5_Debuff(Bool), isDeath(Bool)

        private void PlaySpumBool(CharacterState state)
        {
            // 모든 Bool 리셋
            _spumAnimator.SetBool(H_MOVE, false);
            _spumAnimator.SetBool(H_SPUM_ATTACK, false);
            _spumAnimator.SetBool(H_DAMAGED, false);
            _spumAnimator.SetBool(H_DEBUFF, false);

            // Die 상태가 아니면 Death Bool도 리셋 (풀 재사용 시 필수)
            if (state != CharacterState.Die)
            {
                _spumAnimator.SetBool(H_DEATH, false);
                _spumAnimator.SetBool(H_IS_DEATH, false);
            }

            switch (state)
            {
                case CharacterState.Idle:
                    // 모두 false → IDLE 스테이트로 자동 전환
                    break;

                case CharacterState.Run:
                    _spumAnimator.SetBool(H_MOVE, true);
                    break;

                case CharacterState.Attack:
                case CharacterState.Skill:
                    _spumAnimator.SetBool(H_SPUM_ATTACK, true);
                    break;

                case CharacterState.Hit:
                    _spumAnimator.SetBool(H_DAMAGED, true);
                    break;

                case CharacterState.Stun:
                    _spumAnimator.SetBool(H_DEBUFF, true);
                    break;

                case CharacterState.Die:
                    _spumAnimator.SetBool(H_DEATH, true);
                    _spumAnimator.SetBool(H_IS_DEATH, true);
                    break;
            }
        }
    }
}
