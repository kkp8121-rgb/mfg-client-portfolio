using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Utils;
using MkLike.Core;
using MkLike.Core.Net;
using MkLike.Core.Save;

namespace MkLike.Economy
{
    /// <summary>
    /// 재화 관리 매니저.
    /// BigNumber 기반 (오버플로우 방지 + 한국식 단위 표시).
    /// SaveManager.CurrentData.currency와 동기화하여 저장 데이터 일관성을 유지한다.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        /// <summary>재화별 상한 (null = 무제한). BigNumber로 상한 지정.</summary>
        private static readonly Dictionary<CurrencyType, BigNumber> Caps = new()
        {
            { CurrencyType.WeaponTicket, new BigNumber(999L) }
        };

        /// <summary>내부 재화 저장소 (BigNumber).</summary>
        private readonly Dictionary<CurrencyType, BigNumber> _currencies = new();

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
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
        }

        private void Start()
        {
            // SaveManager.Awake가 앞서 실행됐다면 CurrentData가 이미 준비된 상태
            TryLoadFromSave();
            EnsureTestCurrency();
            EventBus<ServerConnectedEvent>.Subscribe(OnServerConnected);
        }

        private void OnDisable()
        {
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
            EventBus<ServerConnectedEvent>.Unsubscribe(OnServerConnected);
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            TryLoadFromSave();
        }

        private void TryLoadFromSave()
        {
            var data = SaveManager.Instance?.CurrentData?.currency;
            if (data == null) return;
            Initialize(data);
        }

        private void OnServerConnected(ServerConnectedEvent evt)
        {
            SyncBalanceFromServerAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 최초 설치/세이브 삭제 후에만 테스트 재화 충전.
        /// 세이브가 존재하거나 이미 재화가 있으면 작동하지 않는다 (기존 유저 재화 초기화 방지).
        /// </summary>
        private void EnsureTestCurrency()
        {
            // 기존 유저 보호: 세이브 파일에 재화가 있다면 절대 충전 금지
            var sm = SaveManager.Instance;
            if (sm != null && sm.CurrentData != null)
            {
                var cur = sm.CurrentData.currency;
                if (cur != null && (ReadBigField(cur.gold_big, cur.gold) > BigNumber.Zero ||
                                    ReadBigField(cur.ruby_big, cur.ruby) > BigNumber.Zero))
                    return;
            }

            bool needsCharge = GetAmount(CurrencyType.Gold) <= BigNumber.Zero && GetAmount(CurrencyType.Ruby) <= BigNumber.Zero;
            if (!needsCharge) return;

            Add(CurrencyType.Gold, 10000);
            Add(CurrencyType.Ruby, 10000);
            Add(CurrencyType.BlueDiamond, 10000);
            Add(CurrencyType.WeaponTicket, 100);
            Add(CurrencyType.RuneFragment, 10000);
            Add(CurrencyType.StarCrystal, 10000);
            Add(CurrencyType.PotentialStone, 10000);
            Add(CurrencyType.SuperPotentialStone, 10000);
            Add(CurrencyType.ClimbToken, 10000);
            Add(CurrencyType.HuntPoint, 10000);
            Add(CurrencyType.WeaponStone, 10000);
            Debug.Log("[CurrencyManager] 신규 유저 테스트 재화 강제 충전 완료");
        }

        /// <summary>
        /// 세이브 필드에서 BigNumber 읽기. `*_big` 우선, 없으면 long fallback.
        /// </summary>
        private static BigNumber ReadBigField(string bigStr, long legacyLong)
        {
            if (!string.IsNullOrEmpty(bigStr))
            {
                if (BigNumber.TryParse(bigStr, out var bn)) return bn;
            }
            return new BigNumber(legacyLong);
        }

        /// <summary>
        /// SaveData에서 초기값을 로드하여 딕셔너리를 세팅한다.
        /// </summary>
        public void Initialize(CurrencyData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[CurrencyManager] CurrencyData가 null — 기본값으로 초기화");
                data = new CurrencyData();
            }

            // 모든 재화 타입을 0으로 초기화
            foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            {
                _currencies[type] = BigNumber.Zero;
            }

            // 세이브 데이터에서 로드 (BigNumber 우선, long fallback)
            _currencies[CurrencyType.Gold] = ReadBigField(data.gold_big, data.gold);
            _currencies[CurrencyType.Ruby] = ReadBigField(data.ruby_big, data.ruby);
            _currencies[CurrencyType.BlueDiamond] = ReadBigField(data.blueDiamond_big, data.blueDiamond);
            _currencies[CurrencyType.WeaponTicket] = ReadBigField(data.weaponTicket_big, data.weaponTicket);
            _currencies[CurrencyType.RuneFragment] = ReadBigField(data.runeFragment_big, data.runeFragment);
            _currencies[CurrencyType.StarCrystal] = ReadBigField(data.starCrystal_big, data.starCrystal);
            _currencies[CurrencyType.PotentialStone] = ReadBigField(data.potentialStone_big, data.potentialStone);
            _currencies[CurrencyType.SuperPotentialStone] = ReadBigField(data.superPotentialStone_big, data.superPotentialStone);
            _currencies[CurrencyType.ClimbToken] = ReadBigField(data.climbToken_big, data.climbToken);
            _currencies[CurrencyType.HuntPoint] = ReadBigField(data.huntPoint_big, data.huntPoint);
            _currencies[CurrencyType.WeaponStone] = ReadBigField(data.weaponStone_big, data.weaponStone);

            Debug.Log($"[CurrencyManager] 초기화 완료 — 골드:{_currencies[CurrencyType.Gold].ToKoreanShort()}, 루비:{_currencies[CurrencyType.Ruby].ToKoreanShort()}");
        }

        /// <summary>
        /// 현재 보유량을 반환한다.
        /// </summary>
        public BigNumber GetAmount(CurrencyType type)
        {
            return _currencies.TryGetValue(type, out var amount) ? amount : BigNumber.Zero;
        }

        /// <summary>
        /// long으로 보유량을 반환한다 (오버플로우 시 long.MaxValue 클램프).
        /// 네트워크 DTO나 UI 호환을 위해 long이 필요할 때만 사용.
        /// </summary>
        public long GetAmountLong(CurrencyType type)
        {
            return GetAmount(type).ToLongClamped();
        }

        /// <summary>
        /// 재화를 추가한다.
        /// </summary>
        public bool Add(CurrencyType type, BigNumber amount)
        {
            if (amount <= BigNumber.Zero)
            {
                Debug.LogWarning($"[CurrencyManager] 추가 실패 — 유효하지 않은 양: {amount.ToKoreanShort()}");
                return false;
            }

            BigNumber previous = GetAmount(type);
            BigNumber next = previous + amount;

            // 상한 적용
            if (Caps.TryGetValue(type, out var cap) && next > cap)
            {
                next = cap;
            }

            _currencies[type] = next;
            SyncToSaveData();

            EventBus.Publish(new CurrencyChangedEvent
            {
                Type = type,
                PreviousAmount = previous,
                CurrentAmount = next
            });

            return true;
        }

        /// <summary>
        /// 재화를 소비한다.
        /// </summary>
        public bool Spend(CurrencyType type, BigNumber amount)
        {
            if (amount <= BigNumber.Zero)
            {
                Debug.LogWarning($"[CurrencyManager] 소비 실패 — 유효하지 않은 양: {amount.ToKoreanShort()}");
                return false;
            }

            BigNumber previous = GetAmount(type);
            if (previous < amount)
            {
                Debug.LogWarning($"[CurrencyManager] 소비 실패 — {type} 보유량 부족 (보유: {previous.ToKoreanShort()}, 필요: {amount.ToKoreanShort()})");
                // UX-19: 재화 부족 이벤트 발행 (UI가 구독하여 팝업 표시)
                EventBus.Publish(new CurrencyShortageEvent
                {
                    Type = type,
                    Required = amount,
                    Current = previous
                });
                return false;
            }

            BigNumber next = previous - amount;
            if (next < BigNumber.Zero) next = BigNumber.Zero;

            _currencies[type] = next;
            SyncToSaveData();

            EventBus.Publish(new CurrencyChangedEvent
            {
                Type = type,
                PreviousAmount = previous,
                CurrentAmount = next
            });

            return true;
        }

        /// <summary>
        /// 복수 재화를 한 번에 소비한다 (강화 비용 등).
        /// 모든 재화가 충분할 때만 소비한다 (원자적).
        /// </summary>
        public bool SpendMultiple(params (CurrencyType type, BigNumber amount)[] costs)
        {
            // 먼저 모든 재화가 충분한지 확인
            foreach (var (type, amount) in costs)
            {
                if (!HasEnough(type, amount))
                {
                    Debug.LogWarning($"[CurrencyManager] 복수 소비 실패 — {type} 부족 (보유: {GetAmount(type).ToKoreanShort()}, 필요: {amount.ToKoreanShort()})");
                    return false;
                }
            }

            // 모두 충분하면 일괄 소비
            foreach (var (type, amount) in costs)
            {
                Spend(type, amount);
            }

            return true;
        }

        /// <summary>
        /// 지정한 재화가 충분한지 확인한다.
        /// </summary>
        public bool HasEnough(CurrencyType type, BigNumber amount)
        {
            return GetAmount(type) >= amount;
        }

        /// <summary>
        /// 현재 딕셔너리 값을 SaveManager.CurrentData.currency에 동기화한다.
        /// BigNumber는 `*_big` 필드에, long 클램프 값은 레거시 필드에 같이 기록한다 (구버전 호환).
        /// </summary>
        public void SyncToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            CurrencyData c = SaveManager.Instance.CurrentData.currency;
            var gold = GetAmount(CurrencyType.Gold); c.gold = gold.ToLongClamped(); c.gold_big = gold.ToSerializedString();
            var ruby = GetAmount(CurrencyType.Ruby); c.ruby = ruby.ToLongClamped(); c.ruby_big = ruby.ToSerializedString();
            var blueDiamond = GetAmount(CurrencyType.BlueDiamond); c.blueDiamond = blueDiamond.ToLongClamped(); c.blueDiamond_big = blueDiamond.ToSerializedString();
            var weaponTicket = GetAmount(CurrencyType.WeaponTicket); c.weaponTicket = weaponTicket.ToLongClamped(); c.weaponTicket_big = weaponTicket.ToSerializedString();
            var runeFragment = GetAmount(CurrencyType.RuneFragment); c.runeFragment = runeFragment.ToLongClamped(); c.runeFragment_big = runeFragment.ToSerializedString();
            var starCrystal = GetAmount(CurrencyType.StarCrystal); c.starCrystal = starCrystal.ToLongClamped(); c.starCrystal_big = starCrystal.ToSerializedString();
            var potentialStone = GetAmount(CurrencyType.PotentialStone); c.potentialStone = potentialStone.ToLongClamped(); c.potentialStone_big = potentialStone.ToSerializedString();
            var superPotentialStone = GetAmount(CurrencyType.SuperPotentialStone); c.superPotentialStone = superPotentialStone.ToLongClamped(); c.superPotentialStone_big = superPotentialStone.ToSerializedString();
            var climbToken = GetAmount(CurrencyType.ClimbToken); c.climbToken = climbToken.ToLongClamped(); c.climbToken_big = climbToken.ToSerializedString();
            var huntPoint = GetAmount(CurrencyType.HuntPoint); c.huntPoint = huntPoint.ToLongClamped(); c.huntPoint_big = huntPoint.ToSerializedString();
            var weaponStone = GetAmount(CurrencyType.WeaponStone); c.weaponStone = weaponStone.ToLongClamped(); c.weaponStone_big = weaponStone.ToSerializedString();
        }

        /// <summary>
        /// SaveManager.CurrentData.currency에서 값을 읽어 딕셔너리를 갱신한다.
        /// </summary>
        public void SyncFromSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            {
                return;
            }

            CurrencyData c = SaveManager.Instance.CurrentData.currency;
            _currencies[CurrencyType.Gold] = ReadBigField(c.gold_big, c.gold);
            _currencies[CurrencyType.Ruby] = ReadBigField(c.ruby_big, c.ruby);
            _currencies[CurrencyType.BlueDiamond] = ReadBigField(c.blueDiamond_big, c.blueDiamond);
            _currencies[CurrencyType.WeaponTicket] = ReadBigField(c.weaponTicket_big, c.weaponTicket);
            _currencies[CurrencyType.RuneFragment] = ReadBigField(c.runeFragment_big, c.runeFragment);
            _currencies[CurrencyType.StarCrystal] = ReadBigField(c.starCrystal_big, c.starCrystal);
            _currencies[CurrencyType.PotentialStone] = ReadBigField(c.potentialStone_big, c.potentialStone);
            _currencies[CurrencyType.SuperPotentialStone] = ReadBigField(c.superPotentialStone_big, c.superPotentialStone);
            _currencies[CurrencyType.ClimbToken] = ReadBigField(c.climbToken_big, c.climbToken);
            _currencies[CurrencyType.HuntPoint] = ReadBigField(c.huntPoint_big, c.huntPoint);
            _currencies[CurrencyType.WeaponStone] = ReadBigField(c.weaponStone_big, c.weaponStone);
        }

        // ── 서버/로컬 자동 분기 (async) ──

        /// <summary>서버 모드 여부.</summary>
        public bool IsServerMode => ApiClient.HasAuth;

        /// <summary>재화 소비 (서버/로컬 자동 분기).</summary>
        /// <remarks>서버 API는 long 기반이므로 amount는 long으로 받는다.</remarks>
        public async UniTask<bool> SpendAsync(CurrencyType type, long amount, string reason = "spend", CancellationToken ct = default)
        {
            if (IsServerMode)
                return await SpendServerAsync(type, amount, reason, null, ct);
            return Spend(type, amount);
        }

        /// <summary>재화 획득 (서버/로컬 자동 분기).</summary>
        public async UniTask<bool> AddAsync(CurrencyType type, long amount, string reason = "earn", CancellationToken ct = default)
        {
            if (IsServerMode)
                return await EarnServerAsync(type, amount, reason, null, ct);
            return Add(type, amount);
        }

        // ── 서버 연동 (내부) ──

        /// <summary>
        /// 서버에서 확정된 재화 잔액으로 로컬 값을 덮어쓴다.
        /// 가챠 등 서버 API 응답의 currencyRemaining 반영용. 서버 DTO는 long 기반.
        /// </summary>
        public void SyncAmountFromServer(CurrencyType type, long serverAmount)
        {
            BigNumber previous = GetAmount(type);
            BigNumber next = new BigNumber(serverAmount);
            _currencies[type] = next;
            SyncToSaveData();

            if (previous != next)
            {
                EventBus.Publish(new CurrencyChangedEvent
                {
                    Type = type,
                    PreviousAmount = previous,
                    CurrentAmount = next
                });
            }
        }

        /// <summary>
        /// 서버에서 전체 재화 잔액을 가져와 동기화한다.
        /// </summary>
        public async UniTask SyncBalanceFromServerAsync(CancellationToken ct)
        {
            if (!ApiClient.HasAuth) return;

            var response = await ApiClient.GetAsync<CurrencyBalanceResponse>("currency/balance", ct);
            if (!response.success || response.data == null || response.data.currencies == null) return;

            foreach (var entry in response.data.currencies)
            {
                if (Enum.TryParse<CurrencyType>(entry.type, out var type))
                {
                    SyncAmountFromServer(type, entry.amount);
                }
            }

            Debug.Log("[CurrencyManager] 서버 재화 잔액 동기화 완료");
        }

        /// <summary>
        /// 서버에 재화 소비를 요청한다 (서버 검증).
        /// 성공 시 로컬 잔액을 서버 잔액으로 동기화한다.
        /// </summary>
        public async UniTask<bool> SpendServerAsync(CurrencyType type, long amount, string reason, string referenceId = null, CancellationToken ct = default)
        {
            var request = new CurrencySpendRequest
            {
                currencyType = type.ToString(),
                amount = amount,
                reason = reason,
                referenceId = referenceId ?? ""
            };

            var response = await ApiClient.PostAsync<CurrencyTransactionResponse>("currency/spend", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[CurrencyManager] 서버 소비 실패: {response.error}");
                return false;
            }

            SyncAmountFromServer(type, response.data.balanceAfter);
            return true;
        }

        /// <summary>
        /// 서버에 재화 지급을 요청한다.
        /// </summary>
        public async UniTask<bool> EarnServerAsync(CurrencyType type, long amount, string reason, string referenceId = null, CancellationToken ct = default)
        {
            var request = new CurrencyEarnRequest
            {
                currencyType = type.ToString(),
                amount = amount,
                reason = reason,
                referenceId = referenceId ?? ""
            };

            var response = await ApiClient.PostAsync<CurrencyTransactionResponse>("currency/earn", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[CurrencyManager] 서버 지급 실패: {response.error}");
                return false;
            }

            SyncAmountFromServer(type, response.data.balanceAfter);
            return true;
        }

        private void OnDestroy()
        {
            EventBus<ServerConnectedEvent>.Unsubscribe(OnServerConnected);
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
