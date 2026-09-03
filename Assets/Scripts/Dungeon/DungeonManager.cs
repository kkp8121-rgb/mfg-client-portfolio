using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;
using MkLike.Economy;

namespace MkLike.Dungeon
{
    /// <summary>
    /// 던전 입장/완료/열쇠 관리 매니저.
    /// 하루 6회 충전, 최대 20개 누적.
    /// 클리어 기록을 관리하여 소탕(즉시 완료) 기능을 지원한다.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        private const int MAX_KEYS = 30;
        private const int DAILY_RECHARGE = 12;

        [SerializeField] private int _currentKeys = MAX_KEYS;

        /// <summary>클리어 완료 던전 ID 집합 (소탕 해금용)</summary>
        private readonly HashSet<string> _clearedDungeons = new();

        /// <summary>현재 보유 열쇠 수</summary>
        public int CurrentKeys => _currentKeys;

        /// <summary>최대 열쇠 수</summary>
        public int MaxKeys => MAX_KEYS;

        /// <summary>일일 충전량</summary>
        public int DailyRecharge => DAILY_RECHARGE;

        /// <summary>현재 진행 중인 던전 (null이면 던전 밖)</summary>
        private DungeonDataSO _activeDungeon;

        /// <summary>현재 진행 중인 던전 (읽기 전용)</summary>
        public DungeonDataSO ActiveDungeon => _activeDungeon;

        /// <summary>던전 진행 중 여부</summary>
        public bool IsInDungeon => _activeDungeon != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // ReturneeKeyGrantEvent: 2026-04-20 ReturneeGuide 시스템 완전 제거
            EventBus.Subscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DungeonBattleEndedEvent>(OnDungeonBattleEnded);
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            SyncToSaveData();
            SyncClearedToSaveData();
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var data = SaveManager.Instance?.CurrentData?.dungeon;
            Initialize(data);
        }

        /// <summary>
        /// SaveData에서 열쇠 수를 로드하여 초기화한다.
        /// </summary>
        public void Initialize(DungeonSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[DungeonManager] DungeonSaveData가 null — 기본값으로 초기화");
                _currentKeys = DAILY_RECHARGE;
                _clearedDungeons.Clear();
                return;
            }

            _currentKeys = Mathf.Clamp(data.currentKeys, 0, MAX_KEYS);

            // 클리어 기록 로드
            _clearedDungeons.Clear();
            if (data.clearedDungeonIds != null)
            {
                for (int i = 0; i < data.clearedDungeonIds.Count; i++)
                {
                    _clearedDungeons.Add(data.clearedDungeonIds[i]);
                }
            }

            Debug.Log($"[DungeonManager] 초기화 완료 — 열쇠: {_currentKeys}, 클리어: {_clearedDungeons.Count}개");
        }

        /// <summary>
        /// 해당 던전을 클리어한 적이 있는지 확인한다 (소탕 해금 조건).
        /// </summary>
        public bool HasCleared(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId)) return false;
            return _clearedDungeons.Contains(dungeonId);
        }

        /// <summary>
        /// 던전 클리어 기록을 저장한다.
        /// </summary>
        private void RecordClear(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId)) return;
            if (_clearedDungeons.Add(dungeonId))
            {
                SyncClearedToSaveData();
                Debug.Log($"[DungeonManager] 클리어 기록 — {dungeonId} (총 {_clearedDungeons.Count}개)");
            }
        }

        /// <summary>
        /// 던전 전투 종료 시 score >= 1.0이면 클리어 기록을 저장한다.
        /// 2026-04-23 QA 감사: >= 1f는 부동소수점 오차로 0.9999... 실패 가능. 0.9999 이상으로 완화.
        /// _killCount / _targetKills 계산식 상 int 나눗셈이면 0.xxx → 정확히 1.0, 하지만 float 곱셈/치환에서 오차 유발.
        /// </summary>
        private void OnDungeonBattleEnded(DungeonBattleEndedEvent evt)
        {
            if (evt.Score >= 0.9999f && !string.IsNullOrEmpty(evt.DungeonId))
            {
                RecordClear(evt.DungeonId);
            }
        }

        /// <summary>
        /// 던전 입장 가능 여부를 확인한다.
        /// </summary>
        public bool CanEnter(DungeonDataSO dungeon)
        {
            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonManager] CanEnter — dungeon이 null");
                return false;
            }

            if (_currentKeys < dungeon.requiredKeys)
            {
                return false;
            }

            // 최소 층수 조건 확인
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                int currentFloor = SaveManager.Instance.CurrentData.progress.maxFloor;
                if (currentFloor < dungeon.requiredFloor)
                {
                    return false;
                }
            }

            // CP 게이팅: requiredCp > 0이면 플레이어 CP 확인
            if (dungeon.requiredCp > 0)
            {
                long playerCp = GetPlayerCp();
                if (playerCp < dungeon.requiredCp)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 입장 불가 시 구체적 사유 문자열을 반환한다. 입장 가능하면 빈 문자열.
        /// </summary>
        public string GetDenyReason(DungeonDataSO dungeon)
        {
            if (dungeon == null) return "던전 데이터 없음";

            if (_currentKeys < dungeon.requiredKeys)
                return $"열쇠 부족! ({_currentKeys}/{dungeon.requiredKeys})";

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                int currentFloor = SaveManager.Instance.CurrentData.progress.maxFloor;
                if (currentFloor < dungeon.requiredFloor)
                    return $"최소 {dungeon.requiredFloor}층 도달 필요! (현재: {currentFloor}층)";
            }

            if (dungeon.requiredCp > 0)
            {
                long playerCp = GetPlayerCp();
                if (playerCp < dungeon.requiredCp)
                    return $"전투력 부족! ({playerCp:N0}/{dungeon.requiredCp:N0})";
            }

            return "";
        }

        /// <summary>
        /// 던전에 입장한다. 열쇠를 소비하고 던전을 시작한다.
        /// </summary>
        /// <returns>입장 성공 여부</returns>
        public bool EnterDungeon(DungeonDataSO dungeon)
        {
            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonManager] EnterDungeon — dungeon이 null");
                return false;
            }

            if (IsInDungeon)
            {
                Debug.LogWarning("[DungeonManager] EnterDungeon — 이미 던전 진행 중");
                return false;
            }

            if (!CanEnter(dungeon))
            {
                Debug.LogWarning($"[DungeonManager] EnterDungeon — 입장 불가 (열쇠: {_currentKeys}/{dungeon.requiredKeys})");
                return false;
            }

            _currentKeys -= dungeon.requiredKeys;
            _activeDungeon = dungeon;
            SyncToSaveData();

            // 던전 BGM 전환
            AudioManager.Instance?.PlayBgm(BgmType.Dungeon);

            // 던전 유형별 배경 전환
            ZoneVisualManager.Instance?.EnterDungeon((int)dungeon.dungeonType);

            // 던전 입장 이벤트 발행 (연출용)
            EventBus.Publish(new DungeonEnteredEvent
            {
                DungeonId = dungeon.id,
                DungeonName = dungeon.displayName,
                DungeonType = dungeon.dungeonType.ToString()
            });

            Debug.Log($"[DungeonManager] 던전 입장 — {dungeon.displayName} (열쇠 잔여: {_currentKeys})");
            return true;
        }

        /// <summary>
        /// 던전을 완료하고 보상을 지급한다.
        /// score는 0~1 사이의 달성도 (1.0 = 완벽 클리어).
        /// </summary>
        public void CompleteDungeon(DungeonDataSO dungeon, float score)
        {
            if (dungeon == null)
            {
                Debug.LogWarning("[DungeonManager] CompleteDungeon — dungeon이 null");
                return;
            }

            float clampedScore = Mathf.Clamp01(score);

            // 주 보상 지급
            int mainReward = Mathf.RoundToInt(dungeon.baseRewardAmount * clampedScore);
            if (mainReward > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(dungeon.mainRewardType, mainReward);
            }

            // 부 보상 지급
            int bonusReward = Mathf.RoundToInt(dungeon.bonusRewardAmount * clampedScore);
            if (bonusReward > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(dungeon.bonusRewardType, bonusReward);
            }

            // 이벤트 발행
            EventBus.Publish(new DungeonCompletedEvent
            {
                DungeonId = dungeon.id,
                DungeonType = dungeon.dungeonType.ToString(),
                RewardAmount = mainReward
            });

            Debug.Log($"[DungeonManager] 던전 완료 — {dungeon.displayName}, 점수: {clampedScore:F2}, 주보상: {mainReward} {dungeon.mainRewardType}");

            _activeDungeon = null;

            // 이전 BGM 복귀 (로비)
            AudioManager.Instance?.PlayBgm(BgmType.Lobby);

            // 원래 배경 복귀
            ZoneVisualManager.Instance?.ExitDungeon();
        }

        /// <summary>
        /// 일일 열쇠 충전 (로그인 시 호출).
        /// </summary>
        public void RechargeKeys()
        {
            int previous = _currentKeys;
            _currentKeys = Mathf.Min(_currentKeys + DAILY_RECHARGE, MAX_KEYS);
            SyncToSaveData();

            Debug.Log($"[DungeonManager] 열쇠 충전 — {previous} → {_currentKeys}");
        }

        /// <summary>
        /// 열쇠를 직접 추가한다 (보상/이벤트 등).
        /// </summary>
        public void AddKeys(int amount)
        {
            if (amount <= 0) return;

            int previous = _currentKeys;
            _currentKeys = Mathf.Min(_currentKeys + amount, MAX_KEYS);
            SyncToSaveData();

            Debug.Log($"[DungeonManager] 열쇠 추가 — {previous} → {_currentKeys} (+{amount})");
        }

        /// <summary>
        /// 현재 열쇠 수를 SaveData에 동기화한다.
        /// </summary>
        private void SyncToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            SaveManager.Instance.CurrentData.dungeon.currentKeys = _currentKeys;
        }

        /// <summary>
        /// 클리어 기록을 SaveData에 동기화한다.
        /// </summary>
        private void SyncClearedToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            var dungeonData = SaveManager.Instance.CurrentData.dungeon;
            if (dungeonData.clearedDungeonIds == null)
                dungeonData.clearedDungeonIds = new List<string>();
            else
                dungeonData.clearedDungeonIds.Clear();

            foreach (string id in _clearedDungeons)
            {
                dungeonData.clearedDungeonIds.Add(id);
            }
        }

        /// <summary>
        /// 플레이어의 현재 전투력(CP)을 조회한다.
        /// </summary>
        private static long GetPlayerCp()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return 0;

            var stats = player.GetComponent<CombatStats>();
            return stats != null ? stats.PowerScore : 0;
        }

        /// <summary>
        /// 던전의 요구 CP를 반환한다. UI 표시용.
        /// </summary>
        public long GetRequiredCp(DungeonDataSO dungeon)
        {
            return dungeon != null ? dungeon.requiredCp : 0;
        }

        // OnReturneeKeyGrant: 2026-04-20 ReturneeGuide 시스템 완전 제거

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
