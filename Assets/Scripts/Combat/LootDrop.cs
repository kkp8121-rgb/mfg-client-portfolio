using DG.Tweening;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 개별 전리품 드롭 오브젝트.
    /// 풀링되어 재사용되며, DOTween으로 포물선 팝 + 자석 흡수 연출을 한다.
    /// Phase 1: 사망 위치에서 랜덤 방향으로 포물선 팝 (0.3초)
    /// Phase 2: 체공 대기 (등급별 0.15~0.8초)
    /// Phase 3: 플레이어로 가속 흡수
    /// </summary>
    public class LootDrop : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private Transform _magnetTarget;
        private float _waitTimer;
        private float _magnetSpeed;
        private float _baseScale;
        private bool _isMagneting;
        private bool _isActive;
        private System.Action<LootDrop> _onComplete;
        private int _grade;

        // 스프라이트 애니메이션
        private Sprite[] _frames;
        private int _frameIndex;
        private float _frameTimer;
        private const float FrameInterval = 0.1f;

        // 등급별 색상
        private static readonly Color RareColor = new Color(0.4f, 0.6f, 1f, 1f);
        private static readonly Color EpicColor = new Color(0.75f, 0.4f, 1f, 1f);
        private static readonly Color LegendaryColor = new Color(1f, 0.85f, 0.2f, 1f);

        private Tweener _popTween;
        private Tweener _gradeTween;

        /// <summary>
        /// 기존 시그니처 유지 (grade=0 기본값).
        /// </summary>
        public void Initialize(
            Sprite[] frames,
            Vector3 startPos,
            float waitTime,
            float scale,
            Transform magnetTarget,
            System.Action<LootDrop> onComplete)
        {
            Initialize(frames, startPos, waitTime, scale, magnetTarget, onComplete, 0);
        }

        /// <summary>
        /// 등급 정보를 포함한 초기화.
        /// grade: 0=일반, 1=희귀, 2=에픽, 3=전설
        /// </summary>
        public void Initialize(
            Sprite[] frames,
            Vector3 startPos,
            float waitTime,
            float scale,
            Transform magnetTarget,
            System.Action<LootDrop> onComplete,
            int grade)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            _frames = frames;
            _frameIndex = 0;
            _frameTimer = 0f;
            _magnetTarget = magnetTarget;
            _waitTimer = waitTime;
            _magnetSpeed = 4f;
            _baseScale = scale;
            _isMagneting = false;
            _isActive = true;
            _onComplete = onComplete;
            _grade = grade;

            transform.position = startPos;
            transform.localScale = Vector3.one * scale;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.white;
                if (frames != null && frames.Length > 0)
                {
                    _spriteRenderer.sprite = frames[0];
                    _spriteRenderer.enabled = true;
                }
            }

            // Phase 1: 랜덤 방향 포물선 팝 + 착지 바운스
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float popDist = Random.Range(0.3f, 0.8f);
            Vector3 landPos = startPos + new Vector3(randomDir.x * popDist, randomDir.y * popDist + 0.4f, 0f);

            _popTween = transform.DOMove(landPos, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject).OnComplete(() =>
            {
                // 착지 바운스: 스케일 펌프 + 약간 떨림
                transform.DOPunchScale(Vector3.one * _baseScale * 0.2f, 0.2f, 4, 0.3f)
                    .SetLink(gameObject);

                // 착지 시 살짝 아래로 떨어지는 미세 연출
                var bouncePos = transform.position + Vector3.down * 0.05f;
                transform.DOMove(bouncePos, 0.1f).SetEase(Ease.OutBounce)
                    .SetLink(gameObject);

                ApplyGradeVfx();
            });
        }

        private void ApplyGradeVfx()
        {
            if (_spriteRenderer == null) return;

            _gradeTween?.Kill();

            switch (_grade)
            {
                case 1: // 희귀: 파란 틴트 펄스
                    _gradeTween = ColorTweenHelper.To(_spriteRenderer, RareColor, 0.4f)
                        .SetLoops(6, LoopType.Yoyo)
                        .SetLink(gameObject);
                    break;

                case 2: // 에픽: 보라 틴트 + 펀치 스케일
                    _spriteRenderer.color = EpicColor;
                    transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 5, 0.5f)
                        .SetLink(gameObject);
                    break;

                case 3: // 전설: 황금 틴트 + 스케일 연출 + 자석 속도 감소
                    _spriteRenderer.color = LegendaryColor;
                    transform.DOScale(transform.localScale * 1.5f, 0.25f)
                        .SetLoops(2, LoopType.Yoyo)
                        .SetLink(gameObject);
                    _magnetSpeed *= 0.5f;
                    break;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // 스프라이트 애니메이션
            if (_frames != null && _frames.Length > 1)
            {
                _frameTimer += Time.deltaTime;
                if (_frameTimer >= FrameInterval)
                {
                    _frameTimer -= FrameInterval;
                    _frameIndex = (_frameIndex + 1) % _frames.Length;
                    _spriteRenderer.sprite = _frames[_frameIndex];
                }
            }

            if (_isMagneting)
            {
                // Phase 3: 가속 흡수 + 회전
                if (_magnetTarget == null)
                {
                    Complete();
                    return;
                }

                _magnetSpeed += Time.deltaTime * 25f; // 강화된 가속
                Vector3 dir = (_magnetTarget.position - transform.position);
                float dist = dir.magnitude;

                if (dist < 0.15f)
                {
                    Complete();
                    return;
                }

                transform.position += dir.normalized * _magnetSpeed * Time.deltaTime;

                // 흡수될수록 작아지는 연출 + 회전
                float t = Mathf.Clamp01(dist / 1.5f);
                transform.localScale = Vector3.one * _baseScale * Mathf.Lerp(0.3f, 1f, t);

                // 빨려들며 회전 (시각적 속도감)
                transform.Rotate(0f, 0f, _magnetSpeed * 15f * Time.deltaTime);

                // 가까워질수록 알파 감소 (부드러운 흡수)
                if (_spriteRenderer != null && t < 0.3f)
                {
                    var c = _spriteRenderer.color;
                    c.a = Mathf.Lerp(0.2f, 1f, t / 0.3f);
                    _spriteRenderer.color = c;
                }
            }
            else
            {
                // Phase 2: 대기
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    _isMagneting = true;
                }
            }
        }

        private void Complete()
        {
            _isActive = false;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
                _spriteRenderer.color = Color.white;
            }

            _popTween?.Kill();
            _popTween = null;
            _gradeTween?.Kill();
            _gradeTween = null;

            _onComplete?.Invoke(this);
        }

        public void Deactivate()
        {
            _isActive = false;
            _popTween?.Kill();
            _popTween = null;
            _gradeTween?.Kill();
            _gradeTween = null;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
                _spriteRenderer.color = Color.white;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        private void OnDisable()
        {
            _popTween?.Kill();
            _popTween = null;
            _gradeTween?.Kill();
            _gradeTween = null;
        }
    }
}
