using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Data;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.UI
{
    /// <summary>
    /// 가챠 결과 자동 비교 시스템.
    /// GachaResultEvent 배치를 수집하여 현재 장비 대비 CP 향상 아이템을 탐지하고
    /// GachaQuickCompare에 추천 목록을 전달한다.
    /// </summary>
    public class GachaCompareSystem : MonoBehaviour
    {
        public static GachaCompareSystem Instance { get; private set; }

        [Header("참조")]
        [SerializeField] private GachaQuickCompare _quickCompare;

        [Header("설정")]
        [SerializeField] private float _batchCollectDelay = 0.3f;
        [SerializeField] private int _maxRecommendations = 3;

        private readonly Queue<GachaResultEvent> _pendingResults = new();
        private bool _isProcessing;

        /// <summary>현재 비교 팝업이 표시 중인지 여부. AcquisitionShortcutPopup에서 참조.</summary>
        public bool IsComparing { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
        }

        private void OnDisable()
        {
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
        }

        private void OnGachaResult(GachaResultEvent evt)
        {
            _pendingResults.Enqueue(evt);

            if (!_isProcessing)
                ProcessBatch().Forget();
        }

        private async UniTaskVoid ProcessBatch()
        {
            _isProcessing = true;
            var token = this.GetCancellationTokenOnDestroy();

            // 짧은 대기: 10연차의 동기 발행을 수집
            await UniTask.Delay(
                (int)(_batchCollectDelay * 1000f),
                ignoreTimeScale: true,
                cancellationToken: token);

            var batch = new List<GachaResultEvent>();
            while (_pendingResults.Count > 0)
                batch.Add(_pendingResults.Dequeue());

            if (batch.Count == 0)
            {
                _isProcessing = false;
                return;
            }

            // 에픽 이상 아이템만 비교 대상
            var upgrades = new List<GachaUpgradeInfo>();
            for (int i = 0; i < batch.Count && upgrades.Count < _maxRecommendations; i++)
            {
                var result = batch[i];
                if (!IsHighGrade(result.Grade)) continue;

                long cpDelta = CalculateRealCpDelta(result);
                if (cpDelta <= 0) continue;

                upgrades.Add(new GachaUpgradeInfo
                {
                    ItemId = result.ItemId,
                    Grade = result.Grade,
                    PoolName = result.PoolName,
                    CpDelta = cpDelta
                });
            }

            // CP 향상이 큰 순서로 정렬
            upgrades.Sort((a, b) => b.CpDelta.CompareTo(a.CpDelta));

            // 최대 _maxRecommendations건
            if (upgrades.Count > _maxRecommendations)
                upgrades.RemoveRange(_maxRecommendations, upgrades.Count - _maxRecommendations);

            if (upgrades.Count > 0 && _quickCompare != null)
            {
                IsComparing = true;

                // GachaEffect 연출 종료 대기: GachaEffect.IsPlaying 체크
                float maxWait = 10f;
                float waited = 0f;
                while (waited < maxWait)
                {
                    var gachaEffect = FindFirstObjectByType<GachaEffect>();
                    if (gachaEffect == null || !gachaEffect.IsPlaying)
                        break;
                    await UniTask.Delay(100, cancellationToken: token, ignoreTimeScale: true);
                    waited += 0.1f;
                }

                // 연출 종료 후 짧은 지연
                await UniTask.Delay(300, cancellationToken: token, ignoreTimeScale: true);

                long currentCp = GetPlayerCp();
                _quickCompare.Show(upgrades, currentCp);
            }

            _isProcessing = false;
        }

        /// <summary>
        /// GachaQuickCompare가 닫힐 때 호출한다.
        /// </summary>
        public void OnCompareFinished()
        {
            IsComparing = false;
        }

        /// <summary>
        /// 가챠 결과 아이템의 실제 CP 차이를 계산한다.
        /// 현재 장착된 동일 슬롯 장비 대비 CP 향상분을 반환한다.
        /// </summary>
        private static long CalculateRealCpDelta(GachaResultEvent result)
        {
            switch (result.PoolName)
            {
                case "Equipment":
                    return CalculateEquipmentCpDelta(result.ItemId, result.Grade);
                case "Weapon":
                    return CalculateWeaponCpDelta(result.ItemId, result.Grade);
                default:
                    return EstimateFallbackCpDelta(result.Grade);
            }
        }

        private static long CalculateEquipmentCpDelta(string equipmentId, string grade)
        {
            if (MkLike.Equipment.EquipmentManager.Instance == null) return EstimateFallbackCpDelta(grade);

            var data = MkLike.Equipment.EquipmentManager.Instance.GetData(equipmentId);
            if (data == null) return EstimateFallbackCpDelta(grade);

            // 새 아이템의 스탯
            int newAtk = data.GetAtk(grade);
            int newHp = data.GetHp(grade);
            int newDef = data.GetDef(grade);
            float newCrit = data.GetCritRate(grade);

            // 현재 해당 슬롯에 장착된 장비 스탯
            int curAtk = 0, curHp = 0, curDef = 0;
            float curCrit = 0f;

            var equipped = MkLike.Equipment.EquipmentManager.Instance.GetEquipped(data.slot);
            if (equipped != null)
            {
                var curData = MkLike.Equipment.EquipmentManager.Instance.GetData(equipped.equipmentId);
                if (curData != null)
                {
                    curAtk = curData.GetAtk(equipped.grade);
                    curHp = curData.GetHp(equipped.grade);
                    curDef = curData.GetDef(equipped.grade);
                    curCrit = curData.GetCritRate(equipped.grade);
                }
            }

            // CP 공식: ATK × 3 + HP × 0.5 + DEF × 2 + CritRate × 500
            long newCp = (long)(newAtk * 3 + newHp * 0.5f + newDef * 2 + newCrit * 500);
            long curCp = (long)(curAtk * 3 + curHp * 0.5f + curDef * 2 + curCrit * 500);

            return newCp - curCp;
        }

        private static long CalculateWeaponCpDelta(string weaponId, string grade)
        {
            if (MkLike.Equipment.WeaponManager.Instance == null) return EstimateFallbackCpDelta(grade);

            var data = MkLike.Equipment.WeaponManager.Instance.GetData(weaponId);
            if (data == null) return EstimateFallbackCpDelta(grade);

            int newAtk = data.GetFinalAtk(grade, 1, 0);
            float newCrit = data.GetCritRate(grade) + data.equipCritRate;
            float newAtkSpd = data.GetAtkSpeed(grade);

            int curAtk = 0;
            float curCrit = 0f, curAtkSpd = 0f;

            var equipped = MkLike.Equipment.WeaponManager.Instance.EquippedWeapon;
            if (equipped != null)
            {
                var curData = MkLike.Equipment.WeaponManager.Instance.GetData(equipped.weaponId);
                if (curData != null)
                {
                    curAtk = curData.GetFinalAtk(equipped.grade, equipped.level, equipped.awakeningStars);
                    curCrit = curData.GetCritRate(equipped.grade) + curData.equipCritRate;
                    curAtkSpd = curData.GetAtkSpeed(equipped.grade);
                }
            }

            long newCp = (long)(newAtk * 3 + newCrit * 500 + newAtkSpd * 100);
            long curCp = (long)(curAtk * 3 + curCrit * 500 + curAtkSpd * 100);

            return newCp - curCp;
        }

        private static long EstimateFallbackCpDelta(string grade)
        {
            return grade switch
            {
                "Mythic" => 500,
                "Legendary" => 300,
                "Unique" => 150,
                "Epic" => 80,
                _ => 0
            };
        }

        private static long GetPlayerCp()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return 0;

            var stats = player.GetComponent<CombatStats>();
            return stats != null ? stats.PowerScore : 0;
        }

        private static bool IsHighGrade(string grade)
        {
            return grade is "Epic" or "Unique" or "Legendary" or "Mythic";
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public struct GachaUpgradeInfo
        {
            public string ItemId;
            public string Grade;
            public string PoolName;
            public long CpDelta;
        }
    }
}
