using UnityEngine;
using System.Collections.Generic;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Economy
{
    public enum IAPProductType
    {
        BattlePassPremium,
        BattlePassPremiumPlus,
        MonthlyBasic,
        MonthlyPremium,
        MonthlyVIP,
        StarterPack,
        LimitedBundle,
        RubyPack,
        BlueDiamondPack
    }

    /// <summary>
    /// IAP 매니저.
    /// Unity IAP 연동 전 placeholder 구현. 상품 정의, 구매 흐름, 이벤트 발행을 관리한다.
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        public static IAPManager Instance { get; private set; }

        [System.Serializable]
        public class IAPProduct
        {
            public string productId;
            public string displayName;
            public string priceDisplay; // "₩5,500"
            public float priceUsd;
            public IAPProductType type;
        }

        [SerializeField] private IAPProduct[] _products;

        private readonly Dictionary<string, IAPProduct> _productMap = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_products == null) return;
            for (int i = 0; i < _products.Length; i++)
                _productMap[_products[i].productId] = _products[i];
            Debug.Log($"[IAPManager] {_products.Length}개 상품 등록");
        }

        public IAPProduct GetProduct(string productId)
        {
            return _productMap.TryGetValue(productId, out var p) ? p : null;
        }

        public IAPProduct[] GetAllProducts() => _products;

        public void Purchase(string productId)
        {
            if (!_productMap.TryGetValue(productId, out var product))
            {
                Debug.LogWarning($"[IAPManager] 상품 없음: {productId}");
                return;
            }

            // TODO: Unity IAP 연동 시 실제 결제 호출
            Debug.Log($"[IAPManager] 구매 시도: {product.displayName} ({product.priceDisplay})");

            // Placeholder: 즉시 성공 처리
            OnPurchaseSuccess(product);
        }

        private void OnPurchaseSuccess(IAPProduct product)
        {
            Debug.Log($"[IAPManager] 구매 성공: {product.displayName}");
            EventBus<IAPPurchaseEvent>.Publish(new IAPPurchaseEvent
            {
                ProductId = product.productId,
                ProductType = product.type.ToString()
            });
        }

        public void RestorePurchases()
        {
            // TODO: iOS 구매 복원, Android는 자동 복원
            Debug.Log("[IAPManager] 구매 복원 요청 (미구현)");
        }
    }
}
