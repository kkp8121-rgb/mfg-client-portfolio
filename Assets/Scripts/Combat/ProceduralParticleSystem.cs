using UnityEngine;
using DG.Tweening;

namespace MkLike.Combat
{
    public enum ParticlePreset
    {
        Sparkle,
        Dust,
        Glow,
        Confetti
    }

    /// <summary>
    /// 범용 프로시져럴 파티클 시스템.
    /// 코드 기반으로 4종 프리셋 파티클을 생성한다. Unity ParticleSystem 없이 SpriteRenderer + DOTween으로 구현.
    /// </summary>
    public class ProceduralParticleSystem : MonoBehaviour
    {
        public static ProceduralParticleSystem Instance { get; private set; }

        [SerializeField] private int _maxParticles = 30;

        private int _activeCount;

        private static Sprite _circleSprite;
        private static Sprite _squareSprite;

        private static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 1f, 0.3f),
            new Color(0.3f, 0.5f, 1f),
            new Color(1f, 0.85f, 0.1f),
            new Color(1f, 0.5f, 0f),
            new Color(0.8f, 0.3f, 1f),
            new Color(0f, 0.9f, 0.9f),
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureSprites();
        }

        /// <summary>
        /// 지정 프리셋의 파티클을 생성한다.
        /// </summary>
        public void SpawnParticles(ParticlePreset preset, Vector3 position, Color? color = null, int count = 8)
        {
            if (_activeCount >= _maxParticles) return;

            int spawnCount = Mathf.Min(count, _maxParticles - _activeCount);

            switch (preset)
            {
                case ParticlePreset.Sparkle:
                    SpawnSparkle(position, color ?? new Color(1f, 0.95f, 0.8f), spawnCount);
                    break;
                case ParticlePreset.Dust:
                    SpawnDust(position, color ?? new Color(0.6f, 0.5f, 0.4f), spawnCount);
                    break;
                case ParticlePreset.Glow:
                    SpawnGlow(position, color ?? new Color(1f, 0.85f, 0.1f, 0.5f), spawnCount);
                    break;
                case ParticlePreset.Confetti:
                    SpawnConfetti(position, spawnCount);
                    break;
            }
        }

        /// <summary>Sparkle: 작은 흰/금 점, 방사형 퍼짐 + 페이드</summary>
        private void SpawnSparkle(Vector3 center, Color baseColor, int count)
        {
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i + Random.Range(-15f, 15f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                float dist = Random.Range(0.5f, 1.5f);

                var go = CreateParticle("Sparkle", center, _circleSprite, 200);
                var sr = go.GetComponent<SpriteRenderer>();
                go.transform.localScale = Vector3.one * Random.Range(0.04f, 0.10f);

                Color c = baseColor;
                c.r += Random.Range(-0.05f, 0.05f);
                c.g += Random.Range(-0.05f, 0.05f);
                sr.color = c;

                Vector3 endPos = center + dir * dist;
                float duration = Random.Range(0.3f, 0.6f);

                var seq = DOTween.Sequence();
                seq.Append(go.transform.DOMove(endPos, duration).SetEase(Ease.OutCubic));
                seq.Join(go.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
                seq.Join(DOTween.To(
                    () => sr.color,
                    col => sr.color = col,
                    new Color(c.r, c.g, c.b, 0f),
                    duration * 0.8f
                ).SetDelay(duration * 0.2f));
                seq.OnComplete(() => DestroyParticle(go));
                seq.SetLink(gameObject);
            }
        }

        /// <summary>Dust: 작은 갈/회 점, 느린 낙하 + 좌우 흔들림</summary>
        private void SpawnDust(Vector3 center, Color baseColor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-0.8f, 0.8f),
                    Random.Range(-0.2f, 0.5f),
                    0f
                );

                var go = CreateParticle("Dust", center + offset, _circleSprite, 195);
                var sr = go.GetComponent<SpriteRenderer>();
                go.transform.localScale = Vector3.one * Random.Range(0.02f, 0.06f);

                Color c = baseColor;
                c.r += Random.Range(-0.1f, 0.1f);
                c.g += Random.Range(-0.1f, 0.1f);
                c.b += Random.Range(-0.1f, 0.1f);
                c.a = Random.Range(0.4f, 0.7f);
                sr.color = c;

                float duration = Random.Range(1.0f, 2.0f);
                float fallDist = Random.Range(0.3f, 0.8f);
                float sway = Random.Range(-0.3f, 0.3f);

                Vector3 endPos = go.transform.position + new Vector3(sway, -fallDist, 0f);

                var seq = DOTween.Sequence();
                seq.Append(go.transform.DOMove(endPos, duration).SetEase(Ease.InOutSine));
                seq.Join(DOTween.To(
                    () => sr.color.a,
                    a => { var col = sr.color; col.a = a; sr.color = col; },
                    0f,
                    duration * 0.5f
                ).SetDelay(duration * 0.5f));
                seq.OnComplete(() => DestroyParticle(go));
                seq.SetLink(gameObject);
            }
        }

        /// <summary>Glow: 큰 반투명 원, 확대→축소→페이드</summary>
        private void SpawnGlow(Vector3 center, Color baseColor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.3f, 0.3f),
                    0f
                );

                var go = CreateParticle("Glow", center + offset, _circleSprite, 198);
                var sr = go.GetComponent<SpriteRenderer>();
                go.transform.localScale = Vector3.one * 0.05f;
                sr.color = baseColor;

                float maxScale = Random.Range(0.3f, 0.6f);
                float duration = Random.Range(0.5f, 1.0f);

                var seq = DOTween.Sequence();
                // 확대
                seq.Append(go.transform.DOScale(Vector3.one * maxScale, duration * 0.4f).SetEase(Ease.OutQuad));
                // 축소 + 페이드
                seq.Append(go.transform.DOScale(Vector3.one * maxScale * 0.3f, duration * 0.6f).SetEase(Ease.InQuad));
                seq.Join(DOTween.To(
                    () => sr.color,
                    col => sr.color = col,
                    new Color(baseColor.r, baseColor.g, baseColor.b, 0f),
                    duration * 0.6f
                ));
                seq.OnComplete(() => DestroyParticle(go));
                seq.SetLink(gameObject);
            }
        }

        /// <summary>Confetti: 색상 랜덤 사각형, 위→아래 낙하 + 회전</summary>
        private void SpawnConfetti(Vector3 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnOffset = new Vector3(
                    Random.Range(-1.5f, 1.5f),
                    Random.Range(0.5f, 1.5f),
                    0f
                );

                var go = CreateParticle("Confetti", center + spawnOffset, _squareSprite, 202);
                var sr = go.GetComponent<SpriteRenderer>();

                float size = Random.Range(0.03f, 0.07f);
                go.transform.localScale = new Vector3(size, size * Random.Range(0.5f, 1.5f), 1f);

                Color c = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                sr.color = c;

                float fallDist = Random.Range(1.5f, 3.0f);
                float sway = Random.Range(-0.5f, 0.5f);
                float duration = Random.Range(0.8f, 1.5f);
                float rotSpeed = Random.Range(-360f, 360f);

                Vector3 endPos = go.transform.position + new Vector3(sway, -fallDist, 0f);

                var seq = DOTween.Sequence();
                seq.Append(go.transform.DOMove(endPos, duration).SetEase(Ease.InQuad));
                seq.Join(go.transform.DOScale(Vector3.zero, duration * 0.4f).SetDelay(duration * 0.6f));
                seq.Join(DOTween.To(
                    () => sr.color.a,
                    a => { var col = sr.color; col.a = a; sr.color = col; },
                    0f,
                    duration * 0.4f
                ).SetDelay(duration * 0.6f));

                // 회전
                DOTween.To(
                    () => go.transform.eulerAngles.z,
                    z => go.transform.rotation = Quaternion.Euler(0, 0, z),
                    go.transform.eulerAngles.z + rotSpeed,
                    duration
                ).SetLink(gameObject);

                seq.OnComplete(() => DestroyParticle(go));
                seq.SetLink(gameObject);
            }
        }

        private GameObject CreateParticle(string name, Vector3 position, Sprite sprite, int sortingOrder)
        {
            _activeCount++;
            var go = new GameObject($"Particle_{name}");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            return go;
        }

        private void DestroyParticle(GameObject go)
        {
            _activeCount = Mathf.Max(0, _activeCount - 1);
            if (go != null)
                Destroy(go);
        }

        private static void EnsureSprites()
        {
            if (_circleSprite == null)
            {
                const int size = 8;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                float half = size * 0.5f;
                float radius = half;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - half + 0.5f;
                        float dy = y - half + 0.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        tex.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
                    }
                }

                tex.Apply();
                _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            }

            if (_squareSprite == null)
            {
                const int size = 4;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;

                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        tex.SetPixel(x, y, Color.white);

                tex.Apply();
                _squareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            transform.DOKill();
        }
    }
}
