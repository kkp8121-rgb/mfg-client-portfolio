using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 부스터 사용 팝업.
    /// 부스터 목록 + 보유 수량 + 사용 버튼 + 활성 부스터 남은 시간.
    /// </summary>
    public class BoosterPopup : BasePopup
    {
        [Header("UI 참조")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _useExpBtn;
        [SerializeField] private Button _useGoldBtn;
        [SerializeField] private Button _useDropBtn;
        [SerializeField] private Button _closeBtn;

        [Header("수량 표시")]
        [SerializeField] private TMP_Text _expStockText;
        [SerializeField] private TMP_Text _goldStockText;
        [SerializeField] private TMP_Text _dropStockText;

        protected override void Awake()
        {
            base.Awake();
            EnsureComponents();
        }

        private void Start()
        {
            if (_useExpBtn != null) _useExpBtn.onClick.AddListener(() => UseBooster("booster_exp"));
            if (_useGoldBtn != null) _useGoldBtn.onClick.AddListener(() => UseBooster("booster_gold"));
            if (_useDropBtn != null) _useDropBtn.onClick.AddListener(() => UseBooster("booster_drop"));
            if (_closeBtn != null) _closeBtn.onClick.AddListener(() => Hide());
        }

        private void OnEnable()
        {
            RefreshStatus();
            EventBus<BoosterUsedEvent>.Subscribe(OnBoosterChanged);
            EventBus<BoosterExpiredEvent>.Subscribe(OnBoosterExpired);
        }

        private void OnDisable()
        {
            EventBus<BoosterUsedEvent>.Unsubscribe(OnBoosterChanged);
            EventBus<BoosterExpiredEvent>.Unsubscribe(OnBoosterExpired);
        }

        private void OnBoosterChanged(BoosterUsedEvent evt) => RefreshStatus();
        private void OnBoosterExpired(BoosterExpiredEvent evt) => RefreshStatus();

        private void RefreshStatus()
        {
            var mgr = BoosterManager.Instance;
            if (mgr == null) return;

            if (_titleText != null)
                _titleText.text = mgr.IsUnlocked ? "부스터" : $"부스터 (Lv.89 해금)";

            // 보유 수량 표시
            RefreshStockText(_expStockText, BoosterType.ExpBoost, mgr);
            RefreshStockText(_goldStockText, BoosterType.GoldBoost, mgr);
            RefreshStockText(_dropStockText, BoosterType.DropRateBoost, mgr);

            // 버튼 활성화 (해금 + 수량 > 0)
            bool unlocked = mgr.IsUnlocked;
            SetButtonInteractable(_useExpBtn, unlocked && mgr.GetStock(BoosterType.ExpBoost) > 0);
            SetButtonInteractable(_useGoldBtn, unlocked && mgr.GetStock(BoosterType.GoldBoost) > 0);
            SetButtonInteractable(_useDropBtn, unlocked && mgr.GetStock(BoosterType.DropRateBoost) > 0);

            // 상태 텍스트
            if (_statusText == null) return;

            if (!unlocked)
            {
                _statusText.text = "레벨 89에서 해금됩니다.";
                return;
            }

            var sb = new System.Text.StringBuilder();

            // 활성 부스터 표시
            var activeList = new System.Collections.Generic.List<(MkLike.Data.BoosterDataSO data, float remaining)>();
            mgr.GetActiveBoosters(activeList);

            if (activeList.Count > 0)
            {
                sb.AppendLine("<color=#FFD700>활성 부스터:</color>");
                foreach (var (data, remaining) in activeList)
                {
                    int min = Mathf.CeilToInt(remaining / 60f);
                    sb.AppendLine($"  {data.displayName} x{data.multiplier} — {min}분 남음");
                }
            }
            else
            {
                sb.AppendLine("활성 부스터 없음");
            }

            sb.AppendLine();
            sb.AppendLine("<color=#A08C6E>사용 시 30분간 배율이 적용됩니다.</color>");

            _statusText.text = sb.ToString();
        }

        private void RefreshStockText(TMP_Text text, BoosterType type, BoosterManager mgr)
        {
            if (text == null) return;
            int stock = mgr.GetStock(type);
            text.text = $"x{stock}";
            text.color = stock > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        private void SetButtonInteractable(Button btn, bool interactable)
        {
            if (btn == null) return;
            btn.interactable = interactable;
        }

        private void UseBooster(string boosterId)
        {
            var mgr = BoosterManager.Instance;
            if (mgr == null || !mgr.IsUnlocked) return;

            if (mgr.UseBooster(boosterId))
            {
                Debug.Log($"[BoosterPopup] {boosterId} 사용 성공");
            }
        }

        private void EnsureComponents()
        {
            if (_titleText == null)
                _titleText = GetComponentInChildren<TMP_Text>();
        }

        private void OnDestroy()
        {
            DG.Tweening.DOTween.Kill(transform);
        }
    }
}
