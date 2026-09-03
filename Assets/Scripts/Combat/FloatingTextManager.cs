using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 재화 획득 시 플레이어 머리 위에 "+10" 등의 플로팅 텍스트를 표시한다.
    /// 월드 스페이스 TextMeshPro를 풀링하여 성능을 관리한다.
    /// 재화별 고유 색상으로 표시된다.
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _floatHeight = 0.4f;
        [SerializeField] private float _floatDuration = 0.3f;
        [SerializeField] private float _fadeDelay = 0.08f;
        [SerializeField] private int _poolSize = 20;
        [SerializeField] private float _fontSize = 5f;

        [Header("참조")]
        [SerializeField] private Transform _playerTransform;

        private Queue<TMP_Text> _pool;
        private static readonly Dictionary<CurrencyType, Color> CurrencyColors = new()
        {
            { CurrencyType.Gold, new Color(1f, 0.85f, 0.1f) },
            { CurrencyType.Ruby, new Color(1f, 0.3f, 0.4f) },
            { CurrencyType.RuneFragment, new Color(0.3f, 0.85f, 0.4f) },
            { CurrencyType.StarCrystal, new Color(0.4f, 0.7f, 1f) },
            { CurrencyType.PotentialStone, new Color(0.7f, 0.5f, 0.9f) },
            { CurrencyType.HuntPoint, new Color(0.6f, 0.6f, 0.6f) },
            { CurrencyType.WeaponStone, new Color(0.8f, 0.4f, 0.3f) },
            { CurrencyType.BlueDiamond, new Color(0.3f, 0.5f, 1f) },
            { CurrencyType.WeaponTicket, new Color(0.9f, 0.7f, 0.3f) },
            { CurrencyType.SuperPotentialStone, new Color(0.9f, 0.3f, 0.9f) },
            { CurrencyType.ClimbToken, new Color(0.7f, 0.7f, 0.3f) },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitPool();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Subscribe<LootDroppedEvent>(OnLootDropped);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Unsubscribe<LootDroppedEvent>(OnLootDropped);
        }

        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        private void OnGoldGained(GoldGainedEvent evt)
        {
            Spawn(CurrencyType.Gold, evt.Amount);
        }

        private void OnLootDropped(LootDroppedEvent evt)
        {
            Spawn(evt.Type, evt.Amount);
        }

        private void Spawn(CurrencyType type, long amount)
        {
            if (_playerTransform == null) return;
            if (type == CurrencyType.HuntPoint) return;
            if (UIState.IsAnyPanelOpen) return;

            var tmp = GetFromPool();
            if (tmp == null) return;

            Color color = CurrencyColors.TryGetValue(type, out var c) ? c : Color.white;
            string currencyName = GetCurrencyDisplayName(type);
            tmp.text = $"+{NumberFormatter.FormatKorean(amount)} {currencyName}";
            tmp.color = color;

            // 플레이어 머리 위 + 랜덤 방사형 오프셋 (겹침 방지)
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(0.2f, 0.5f);
            float offsetX = Mathf.Cos(angle) * radius;
            float offsetY = Mathf.Sin(angle) * 0.3f;
            Vector3 startPos = _playerTransform.position + new Vector3(offsetX, 0.3f + offsetY, 0f);
            tmp.transform.position = startPos;

            float scale = type == CurrencyType.Gold ? 0.36f : 0.54f;
            tmp.transform.localScale = Vector3.zero;
            tmp.gameObject.SetActive(true);

            // 팝! + 빠르게 위로 떠오르며 페이드아웃
            var seq = DOTween.Sequence().SetLink(tmp.gameObject);
            seq.Append(tmp.transform.DOScale(Vector3.one * scale, 0.1f).SetEase(Ease.OutBack));
            seq.Join(tmp.transform.DOMoveY(startPos.y + _floatHeight, _floatDuration).SetEase(Ease.OutQuad));
            seq.Insert(_fadeDelay, DOTween.To(
                () => tmp.color,
                x => tmp.color = x,
                new Color(color.r, color.g, color.b, 0f),
                _floatDuration - _fadeDelay));
            seq.OnComplete(() => ReturnToPool(tmp));
        }

        /// <summary>
        /// 시스템 메시지를 플레이어 머리 위에 표시한다 (재화 부족 등).
        /// </summary>
        public void ShowSystemMessage(string message)
        {
            if (_playerTransform == null) return;
            if (UIState.IsAnyPanelOpen) return;

            var tmp = GetFromPool();
            if (tmp == null) return;

            Color color = new Color(1f, 0.35f, 0.35f); // 빨간색
            tmp.text = message;
            tmp.color = color;

            Vector3 startPos = _playerTransform.position + new Vector3(0f, 0.6f, 0f);
            tmp.transform.position = startPos;
            tmp.transform.localScale = Vector3.zero;
            tmp.gameObject.SetActive(true);

            float duration = 0.6f;
            var seq = DOTween.Sequence().SetLink(tmp.gameObject);
            seq.Append(tmp.transform.DOScale(Vector3.one * 0.5f, 0.15f).SetEase(Ease.OutBack));
            seq.Join(tmp.transform.DOMoveY(startPos.y + 0.5f, duration).SetEase(Ease.OutQuad));
            seq.Insert(0.2f, DOTween.To(
                () => tmp.color,
                x => tmp.color = x,
                new Color(color.r, color.g, color.b, 0f),
                duration - 0.2f));
            seq.OnComplete(() => ReturnToPool(tmp));
        }

        /// <summary>
        /// CurrencyType을 한국어 표시 이름으로 변환한다. (공용 유틸리티 위임)
        /// </summary>
        private static string GetCurrencyDisplayName(CurrencyType type)
        {
            return DisplayNameUtils.GetCurrencyDisplayName(type);
        }

        // ── 풀링 ──

        private void InitPool()
        {
            _pool = new Queue<TMP_Text>();
            for (int i = 0; i < _poolSize; i++)
                _pool.Enqueue(CreateTextObject());
        }

        private TMP_Text CreateTextObject()
        {
            var obj = new GameObject("FloatingText");
            obj.transform.SetParent(transform);
            var tmp = obj.AddComponent<TextMeshPro>();
            tmp.fontSize = _fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            tmp.sortingOrder = 100;

            obj.SetActive(false);
            return tmp;
        }

        private TMP_Text GetFromPool()
        {
            while (_pool.Count > 0)
            {
                var tmp = _pool.Dequeue();
                if (tmp != null) return tmp;
            }
            return CreateTextObject();
        }

        private void ReturnToPool(TMP_Text tmp)
        {
            if (tmp == null) return;
            DOTween.Kill(tmp.transform);
            tmp.gameObject.SetActive(false);

            // 풀 최대 사이즈 초과 시 Destroy
            if (_pool.Count >= _poolSize * 2)
            {
                Destroy(tmp.gameObject);
                return;
            }
            _pool.Enqueue(tmp);
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
            if (Instance == this)
                Instance = null;
        }
    }
}
