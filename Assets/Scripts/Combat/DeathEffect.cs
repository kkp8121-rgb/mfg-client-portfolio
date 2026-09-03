using System.Collections.Generic;
using DG.Tweening;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 사망 시 축소 + 페이드 + 파편 분산 + 폭발 연출.
    /// 몬스터/캐릭터에 부착하여 사용한다.
    /// </summary>
    public class DeathEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _duration = 0.4f;
        [SerializeField] private int _fragmentCount = 4;
        [SerializeField] private float _fragmentSpread = 1.2f;
        [SerializeField] private float _fragmentDuration = 0.5f;
        [SerializeField] private float _fragmentScale = 0.3f;

        private static readonly Color DarkRed = new Color(0.6f, 0.1f, 0.1f);

        // --- 파편 오브젝트 풀 (static, GC 최소화) ---
        private static readonly Queue<GameObject> _fragmentPool = new Queue<GameObject>();
        private static Transform _fragmentPoolParent;
        private const int POOL_MAX = 32;

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 풀에서 파편 오브젝트를 꺼낸다. 없으면 새로 생성.
        /// </summary>
        private static GameObject GetFragment()
        {
            // 풀 부모 컨테이너 (씬 전환 시 파괴될 수 있으므로 매번 확인)
            if (_fragmentPoolParent == null)
            {
                var container = new GameObject("[DeathFragmentPool]");
                Object.DontDestroyOnLoad(container);
                _fragmentPoolParent = container.transform;
            }

            // 풀에서 유효한 오브젝트 꺼내기
            while (_fragmentPool.Count > 0)
            {
                var candidate = _fragmentPool.Dequeue();
                if (candidate != null)
                {
                    candidate.SetActive(true);
                    return candidate;
                }
            }

            // 풀이 비었으면 새로 생성
            var obj = new GameObject("DeathFragment");
            obj.AddComponent<SpriteRenderer>();
            obj.transform.SetParent(_fragmentPoolParent);
            return obj;
        }

        /// <summary>
        /// 파편 오브젝트를 풀에 반환한다.
        /// </summary>
        private static void ReleaseFragment(GameObject fragObj)
        {
            if (fragObj == null) return;

            // DOTween 정리
            fragObj.transform.DOKill();

            // 상태 초기화
            var sr = fragObj.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = null;

            fragObj.transform.localScale = Vector3.one;
            fragObj.transform.rotation = Quaternion.identity;
            fragObj.SetActive(false);

            // 풀 최대 크기 제한
            if (_fragmentPool.Count < POOL_MAX)
            {
                if (_fragmentPoolParent != null)
                    fragObj.transform.SetParent(_fragmentPoolParent);
                _fragmentPool.Enqueue(fragObj);
            }
            else
            {
                Object.Destroy(fragObj);
            }
        }

        /// <summary>
        /// 사망 연출을 재생한다. Dead 애니메이션은 CharacterAnimBridge에서 처리하므로
        /// 여기서는 파편 + 페이드 + VFX만 담당한다. (DOScale 찌그러지기 연출 제거)
        /// </summary>
        /// <param name="onComplete">연출 완료 후 콜백 (풀 반환 등)</param>
        public void PlayDeath(System.Action onComplete = null)
        {
            if (_renderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            SpawnFragments();

            var seq = DOTween.Sequence();

            // 1. 색상: 짧게 어두운 붉은색으로 변환
            seq.Append(
                ColorTweenHelper.To(_renderer, DarkRed, 0.1f)
            );

            // 2. 페이드: 알파를 0으로 (스케일 변경 없이 자연스럽게 사라짐)
            seq.Append(DOTween.To(
                () => _renderer.color.a,
                a =>
                {
                    var c = _renderer.color;
                    _renderer.color = new Color(c.r, c.g, c.b, a);
                },
                0f, _duration));

            // 3. 폭발 VFX
            seq.OnComplete(() =>
            {
                SkillVfx.SpawnExplosion(transform.position, 0.5f,
                    new Color(1f, 0.2f, 0.2f), new Color(1f, 0.4f, 0.3f), 0.4f);
                onComplete?.Invoke();
            });

            seq.SetLink(gameObject);
        }

        private void SpawnFragments()
        {
            if (_renderer == null || _renderer.sprite == null) return;

            Color baseColor = _renderer.color;
            Vector3 origin = transform.position;
            float angleStep = 360f / _fragmentCount;

            for (int i = 0; i < _fragmentCount; i++)
            {
                var fragObj = GetFragment();
                fragObj.transform.position = origin;
                fragObj.transform.localScale = Vector3.one * _fragmentScale;

                var sr = fragObj.GetComponent<SpriteRenderer>();
                sr.sprite = _renderer.sprite;
                sr.color = baseColor;
                sr.sortingOrder = _renderer.sortingOrder + 1;

                float angle = angleStep * i + Random.Range(-15f, 15f);
                float rad = angle * Mathf.Deg2Rad;
                float dist = _fragmentSpread * Random.Range(0.6f, 1f);
                Vector3 targetPos = origin + new Vector3(Mathf.Cos(rad) * dist, Mathf.Sin(rad) * dist, 0f);

                float rotZ = Random.Range(-180f, 180f);

                var fragSeq = DOTween.Sequence();
                fragSeq.Append(fragObj.transform.DOMove(targetPos, _fragmentDuration).SetEase(Ease.OutQuad));
                fragSeq.Join(fragObj.transform.DOScale(0f, _fragmentDuration).SetEase(Ease.InQuad));
                fragSeq.Join(fragObj.transform.DORotate(new Vector3(0f, 0f, rotZ), _fragmentDuration, RotateMode.FastBeyond360));
                fragSeq.Join(DOTween.To(
                    () => sr.color.a,
                    a => sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, a),
                    0f, _fragmentDuration * 0.8f).SetDelay(_fragmentDuration * 0.2f));
                fragSeq.OnComplete(() => ReleaseFragment(fragObj));
                fragSeq.SetLink(fragObj);
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
