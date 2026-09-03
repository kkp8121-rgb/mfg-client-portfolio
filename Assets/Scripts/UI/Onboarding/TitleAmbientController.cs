using System.Collections.Generic;
using MkLike.Combat;
using UnityEngine;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// Title 씬 배경의 SPUM 캐릭터 30명 관리.
    /// 3가지 행동: Idle(아이들 고정), Wander(랜덤 이동), Duel(짝과 마주보며 공격 반복).
    /// Awake 시점에 기존 자식 모두 삭제 후 Resources.LoadAll 로 SPUM 프리팹을 로드하여 스폰한다.
    /// </summary>
    public class TitleAmbientController : MonoBehaviour
    {
        private enum Behavior { Idle, Wander, Duel }

        [Header("Spawn")]
        [SerializeField] private int _totalCount = 70;
        [SerializeField] private int _idleCount = 30;
        [SerializeField] private int _wanderCount = 24;
        [SerializeField] private int _duelPairs = 8; // 8쌍 = 16명 (쌍당 2슬롯 사용)
        [SerializeField] private Vector2 _areaMin = new(-4.3f, -3.8f);
        [SerializeField] private Vector2 _areaMax = new(4.3f, 3.5f);
        // 팝업 3버튼 780x680 → 월드 약 4.55x3.97, 중앙 (0,-0.47).
        [SerializeField] private Vector2 _popupExcludeMin = new(-2.3f, -2.3f);
        [SerializeField] private Vector2 _popupExcludeMax = new(2.3f, 1.5f);
        [SerializeField] private int _gridCols = 10;
        [SerializeField] private int _gridRows = 10;
        [SerializeField] private Vector2 _jitter = new(0.1f, 0.08f);
        [SerializeField] private Vector2 _scaleRange = new(0.6f, 0.8f);

        [Header("Behavior")]
        [SerializeField] private float _wanderSpeed = 0.4f;
        [SerializeField] private float _wanderPauseMin = 1.2f;
        [SerializeField] private float _wanderPauseMax = 3.0f;
        [SerializeField] private float _wanderMaxRadius = 0.55f;
        [SerializeField] private float _duelAttackInterval = 1.3f;
        [SerializeField] private float _duelSpacing = 0.42f;

        [Header("SPUM")]
        [SerializeField]
        private string[] _resourcePaths = new[]
        {
            "Addons/ChosunSet/2_Prefab",
            "Addons/Elf/2_Prefab",
            "Addons/ModernPackVer1/2_Prefab",
            "Addons/PaladinSet/2_Prefab",
            "Addons/RetroHeroes/2_Prefab",
            "Addons/MS_Orc/2_Prefab",
            "Addons/Undead/2_Prefab",
        };

        private readonly List<GameObject> _prefabPool = new();
        private readonly List<Agent> _agents = new();
        private readonly List<Vector3> _freeSlots = new();

        private static readonly Color DimColor = new(0.35f, 0.35f, 0.4f, 1f);
        private bool _isDimmed;

        /// <summary>
        /// 주위 SPUM 캐릭터 전체를 어둡게/밝게 토글 (JobSelect 등 모달 진입 시).
        /// </summary>
        public void SetDimmed(bool dim)
        {
            if (_isDimmed == dim) return;
            _isDimmed = dim;
            var color = dim ? DimColor : Color.white;
            for (int i = 0; i < _agents.Count; ++i)
            {
                var host = _agents[i].Host;
                if (host == null) continue;
                foreach (var sr in host.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.color = color;
                }
            }
        }

        public IReadOnlyList<GameObject> PrefabPool => _prefabPool;

        private class Agent
        {
            public Transform Host;
            public CharacterAnimBridge Bridge;
            public Behavior Behavior;

            // Wander
            public Vector3 TargetPos;
            public float WaitTimer;
            public bool IsMoving;

            // Duel
            public Agent Partner;
            public bool FacingRight;
            public float AttackTimer;
            public Vector3 AnchorPos;
        }

        private void Awake()
        {
            ClearChildren();
            LoadPrefabs();
            if (_prefabPool.Count == 0)
            {
                Debug.LogWarning("[TitleAmbient] SPUM 프리팹 로드 실패 — Resources/Addons 경로 확인");
                return;
            }
            BuildSlotGrid();
            SpawnAll();
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void LoadPrefabs()
        {
            _prefabPool.Clear();
            foreach (var path in _resourcePaths)
            {
                var loaded = Resources.LoadAll<GameObject>(path);
                if (loaded == null) continue;
                for (int i = 0; i < loaded.Length; ++i)
                {
                    if (loaded[i] != null) _prefabPool.Add(loaded[i]);
                }
            }
        }

        private void BuildSlotGrid()
        {
            _freeSlots.Clear();
            float cellW = (_areaMax.x - _areaMin.x) / _gridCols;
            float cellH = (_areaMax.y - _areaMin.y) / _gridRows;
            for (int y = 0; y < _gridRows; ++y)
            {
                for (int x = 0; x < _gridCols; ++x)
                {
                    float cx = _areaMin.x + (x + 0.5f) * cellW;
                    float cy = _areaMin.y + (y + 0.5f) * cellH;
                    var p = new Vector3(cx, cy, 0f);
                    if (IsInsidePopupExclude(p)) continue;
                    _freeSlots.Add(p);
                }
            }
            // Fisher–Yates shuffle
            for (int i = _freeSlots.Count - 1; i > 0; --i)
            {
                int j = Random.Range(0, i + 1);
                (_freeSlots[i], _freeSlots[j]) = (_freeSlots[j], _freeSlots[i]);
            }
        }

        private Vector3? TakeSlot()
        {
            if (_freeSlots.Count == 0) return null;
            var p = _freeSlots[_freeSlots.Count - 1];
            _freeSlots.RemoveAt(_freeSlots.Count - 1);
            p.x += Random.Range(-_jitter.x, _jitter.x);
            p.y += Random.Range(-_jitter.y, _jitter.y);
            return p;
        }

        private void SpawnAll()
        {
            int idleN = Mathf.Max(0, _idleCount);
            int wanderN = Mathf.Max(0, _wanderCount);
            int duelPairs = Mathf.Max(0, _duelPairs);
            int totalWanted = idleN + wanderN + duelPairs * 2;

            if (totalWanted != _totalCount)
            {
                wanderN = Mathf.Max(0, _totalCount - idleN - duelPairs * 2);
            }

            for (int i = 0; i < idleN; ++i) SpawnAgent(Behavior.Idle, TakeSlot());
            for (int i = 0; i < wanderN; ++i) SpawnAgent(Behavior.Wander, TakeSlot());
            for (int i = 0; i < duelPairs; ++i) SpawnDuelPair();
        }

        private Agent SpawnAgent(Behavior behavior, Vector3? forcePos)
        {
            var prefab = _prefabPool[Random.Range(0, _prefabPool.Count)];
            var go = Instantiate(prefab, transform);
            go.name = $"Ambient_{behavior}_{_agents.Count}";

            var pos = forcePos ?? (TakeSlot() ?? new Vector3(
                Random.Range(_areaMin.x, _areaMax.x),
                Random.Range(_areaMin.y, _areaMax.y), 0f));
            go.transform.localPosition = pos;
            float s = Random.Range(_scaleRange.x, _scaleRange.y);
            go.transform.localScale = new Vector3(s, s, 1f);

            var bridge = go.GetComponent<CharacterAnimBridge>();
            if (bridge == null) bridge = go.AddComponent<CharacterAnimBridge>();

            var agent = new Agent
            {
                Host = go.transform,
                Bridge = bridge,
                Behavior = behavior,
                AnchorPos = pos,
                WaitTimer = Random.Range(_wanderPauseMin, _wanderPauseMax),
                IsMoving = false,
                AttackTimer = Random.Range(0f, _duelAttackInterval),
            };
            _agents.Add(agent);
            return agent;
        }

        private void SpawnDuelPair()
        {
            // 듀얼 쌍은 인접한 두 슬롯에 배치 (세로 영역 넓히기 위해 x 우선)
            var slotA = TakeSlot();
            var slotB = TakeSlot();
            if (slotA == null || slotB == null) return;

            var leftSlot = slotA.Value.x <= slotB.Value.x ? slotA.Value : slotB.Value;
            var rightSlot = slotA.Value.x <= slotB.Value.x ? slotB.Value : slotA.Value;

            // 두 슬롯의 중심을 서로 duelSpacing만큼 떨어뜨림
            var mid = (leftSlot + rightSlot) * 0.5f;
            var leftPos = new Vector3(mid.x - _duelSpacing, mid.y, 0f);
            var rightPos = new Vector3(mid.x + _duelSpacing, mid.y, 0f);

            var a = SpawnAgent(Behavior.Duel, leftPos);
            var b = SpawnAgent(Behavior.Duel, rightPos);
            a.Partner = b; b.Partner = a;
            a.FacingRight = true;
            b.FacingRight = false;
            ApplyFacing(a);
            ApplyFacing(b);
            a.AttackTimer = 0f;
            b.AttackTimer = _duelAttackInterval * 0.5f;
        }

        private bool IsInsidePopupExclude(Vector3 p)
        {
            return p.x > _popupExcludeMin.x && p.x < _popupExcludeMax.x
                && p.y > _popupExcludeMin.y && p.y < _popupExcludeMax.y;
        }

        private void Start()
        {
            for (int i = 0; i < _agents.Count; ++i)
            {
                var a = _agents[i];
                switch (a.Behavior)
                {
                    case Behavior.Idle:
                        a.Bridge?.PlayState(CharacterState.Idle);
                        break;
                    case Behavior.Wander:
                        a.Bridge?.PlayState(CharacterState.Idle);
                        PickWanderTarget(a);
                        break;
                    case Behavior.Duel:
                        a.Bridge?.PlayState(CharacterState.Idle);
                        break;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _agents.Count; ++i)
            {
                var a = _agents[i];
                switch (a.Behavior)
                {
                    case Behavior.Wander: TickWander(a, dt); break;
                    case Behavior.Duel: TickDuel(a, dt); break;
                }
            }
        }

        private void PickWanderTarget(Agent a)
        {
            // 원래 배치된 Anchor 반경 _wanderMaxRadius 내에서만 목적지 선택 → 분포 유지
            for (int attempt = 0; attempt < 6; ++attempt)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(_wanderMaxRadius * 0.3f, _wanderMaxRadius);
                var candidate = a.AnchorPos + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.6f, 0f);
                candidate.x = Mathf.Clamp(candidate.x, _areaMin.x, _areaMax.x);
                candidate.y = Mathf.Clamp(candidate.y, _areaMin.y, _areaMax.y);
                if (IsInsidePopupExclude(candidate)) continue;
                a.TargetPos = candidate;
                a.IsMoving = true;
                a.Bridge?.PlayState(CharacterState.Run);
                float dx = a.TargetPos.x - a.Host.localPosition.x;
                SetFacingByDirection(a, dx >= 0f);
                return;
            }
            // 실패 시 Anchor로 복귀
            a.TargetPos = a.AnchorPos;
            a.IsMoving = true;
            a.Bridge?.PlayState(CharacterState.Run);
        }

        private void TickWander(Agent a, float dt)
        {
            if (!a.IsMoving)
            {
                a.WaitTimer -= dt;
                if (a.WaitTimer <= 0f)
                {
                    PickWanderTarget(a);
                }
                return;
            }

            var cur = a.Host.localPosition;
            var delta = (a.TargetPos - cur);
            float dist = delta.magnitude;
            if (dist < 0.05f)
            {
                a.IsMoving = false;
                a.WaitTimer = Random.Range(_wanderPauseMin, _wanderPauseMax);
                a.Bridge?.PlayState(CharacterState.Idle);
                return;
            }

            var step = delta.normalized * _wanderSpeed * dt;
            if (step.magnitude > dist) step = delta;
            a.Host.localPosition = cur + step;
        }

        private void TickDuel(Agent a, float dt)
        {
            a.AttackTimer -= dt;
            if (a.AttackTimer <= 0f)
            {
                a.Bridge?.PlayState(CharacterState.Attack);
                a.AttackTimer = _duelAttackInterval + Random.Range(-0.15f, 0.15f);
            }
        }

        private void SetFacingByDirection(Agent a, bool facingRight)
        {
            if (a.FacingRight == facingRight) return;
            a.FacingRight = facingRight;
            ApplyFacing(a);
        }

        private void ApplyFacing(Agent a)
        {
            var s = a.Host.localScale;
            float abs = Mathf.Abs(s.x);
            s.x = a.FacingRight ? abs : -abs;
            a.Host.localScale = s;
        }
    }
}
