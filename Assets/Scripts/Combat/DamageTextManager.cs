using UnityEngine;
using TMPro;
using Lean.Pool;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 데미지 텍스트 매니저. LeanPool 기반 데미지 텍스트 생성.
    /// 다단히트 시 순차적으로 위로 스태킹.
    /// </summary>
    public class DamageTextManager : MonoBehaviour
    {
        public static DamageTextManager Instance { get; private set; }

        private const float HIT_STACK_OFFSET = 0.35f;
        private const float MONSTER_HEAD_OFFSET = 1.0f;
        // 2026-04-23 AoE 스킬 피크 대응: 40몬스터 × 3히트 = 120 동시 요청 가능
        private const int POOL_PRELOAD = 40;
        private const int POOL_CAPACITY = 150;

        [Header("프리팹")]
        [SerializeField] private GameObject _damageTextPrefab;

        private LeanGameObjectPool _pool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_damageTextPrefab == null)
            {
                _damageTextPrefab = CreateDamageTextPrefab();
            }

            SetupPool();
        }

        private void SetupPool()
        {
            var poolGo = new GameObject("Pool_DamageText");
            poolGo.transform.SetParent(transform);
            _pool = poolGo.AddComponent<LeanGameObjectPool>();
            _pool.Prefab = _damageTextPrefab;
            _pool.Preload = POOL_PRELOAD;
            _pool.Capacity = POOL_CAPACITY;
            _pool.Recycle = true;
            _pool.Notification = LeanGameObjectPool.NotificationType.IPoolable;
            _pool.Strategy = LeanGameObjectPool.StrategyType.DeactivateViaHierarchy;
            _pool.PreloadAll();
        }

        /// <summary>평균 데미지 추적 (등급화 기준)</summary>
        private float _averageDamage = 100f;
        private int _damageCount;

        /// <summary>
        /// 데미지 텍스트를 생성한다.
        /// 등급화: 일반 / 강타(평균1.5배+) / 크리티컬 / 오버킬
        /// </summary>
        public void Spawn(Vector3 position, int damage, bool isCritical, int hitIndex = 0,
            bool isBoss = false, bool isPenetrating = false)
        {
            if (UIState.IsAnyPanelOpen) return;
            if (_pool == null)
            {
                Debug.LogWarning("[DamageTextManager] Pool이 초기화되지 않았습니다.");
                return;
            }

            // 평균 데미지 추적 (EMA)
            _damageCount++;
            _averageDamage = _damageCount <= 1 ? damage : _averageDamage * 0.95f + damage * 0.05f;

            // 등급 판정 (우선순위: 보스 > 크리티컬 > 관통 > 강타 > 일반)
            DamageTextType type = DamageTextType.Normal;
            if (isBoss)
                type = DamageTextType.BossHit;
            else if (isCritical)
                type = DamageTextType.Critical;
            else if (isPenetrating)
                type = DamageTextType.Penetrating;
            else if (damage >= _averageDamage * 1.5f)
                type = DamageTextType.HeavyHit;

            Vector3 spawnPos = position + Vector3.up * MONSTER_HEAD_OFFSET;
            GameObject obj = LeanPool.Spawn(_damageTextPrefab, spawnPos, Quaternion.identity);

            if (obj == null)
            {
                Debug.LogWarning("[DamageTextManager] Spawn 실패");
                return;
            }

            var dt = obj.GetComponent<DamageText>();
            if (dt != null)
            {
                float yOffset = hitIndex * HIT_STACK_OFFSET;
                dt.Setup(NumberFormatter.FormatKorean(damage), type, 0, yOffset);
            }
        }

        /// <summary>
        /// 오버킬 데미지 텍스트를 생성한다.
        /// 몬스터 HP의 2배 이상 데미지로 원킬 시 호출.
        /// </summary>
        public void SpawnOverkill(Vector3 position)
        {
            SpawnSpecial(position, "처치!", DamageTextType.Overkill);
        }

        /// <summary>
        /// 힐 텍스트 (초록, 위로 올라감).
        /// </summary>
        public void ShowHealText(Vector3 position, int amount)
        {
            SpawnSpecial(position, $"+{NumberFormatter.FormatKorean(amount)}", DamageTextType.Heal);
        }

        /// <summary>
        /// 버프 텍스트 (파란, "ATK UP!" 등).
        /// </summary>
        public void ShowBuffText(Vector3 position, string buffName)
        {
            SpawnSpecial(position, buffName, DamageTextType.Buff);
        }

        /// <summary>
        /// 회피 텍스트 (회색, 작은 크기, 빠르게).
        /// </summary>
        public void ShowMissText(Vector3 position)
        {
            SpawnSpecial(position, "MISS", DamageTextType.Miss);
        }

        /// <summary>
        /// 방어 텍스트 (노란, "BLOCKED").
        /// </summary>
        public void ShowBlockedText(Vector3 position)
        {
            SpawnSpecial(position, "BLOCKED", DamageTextType.Blocked);
        }

        /// <summary>
        /// 콤보 텍스트. 이벤트 기반으로 UI HUD에 표시한다.
        /// </summary>
        public void ShowComboText(Vector3 position, int comboCount)
        {
            EventBus<ComboEvent>.Publish(new ComboEvent
            {
                ComboCount = comboCount,
                Position = position
            });
        }

        private void SpawnSpecial(Vector3 position, string text, DamageTextType type, int comboCount = 0)
        {
            if (UIState.IsAnyPanelOpen) return;
            if (_pool == null) return;

            Vector3 spawnPos = position + Vector3.up * MONSTER_HEAD_OFFSET;
            GameObject obj = LeanPool.Spawn(_damageTextPrefab, spawnPos, Quaternion.identity);
            if (obj == null) return;

            var dt = obj.GetComponent<DamageText>();
            if (dt != null)
            {
                dt.Setup(text, type, comboCount, 0f);
            }
        }

        private GameObject CreateDamageTextPrefab()
        {
            var prefabObj = new GameObject("DamageText_Prefab");
            prefabObj.SetActive(false);

            var tmp = prefabObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 3.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.sortingOrder = 200;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

            var font = TMP_Settings.defaultFontAsset;
            if (font != null)
                tmp.font = font;

            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);

            prefabObj.AddComponent<DamageText>();

            prefabObj.transform.SetParent(transform);
            return prefabObj;
        }
    }
}
