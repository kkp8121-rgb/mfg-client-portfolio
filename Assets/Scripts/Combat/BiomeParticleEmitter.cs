using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 바이옴별 분위기 파티클 이미터.
    /// 풀잎, 눈송이, 모래, 불씨, 보라 안개 등을
    /// 프로시져럴 스프라이트로 생성하여 카메라 주변에 흩뿌린다.
    /// StageChangedEvent를 구독하여 챕터 변경 시 파티클 종류를 전환한다.
    /// </summary>
    public class BiomeParticleEmitter : MonoBehaviour
    {
        [Header("파티클 설정")]
        [SerializeField] private int _maxParticles = 120;
        [SerializeField] private float _spawnInterval = 0.08f;
        [SerializeField] private float _spawnRadius = 14f;

        private SpriteRenderer[] _particles;
        private Vector2[] _velocities;
        private float[] _lifetimes;
        private float[] _maxLifetimes;
        private float[] _rotSpeeds;
        private int _activeCount;
        private float _spawnTimer;
        private Transform _particleRoot;
        private Camera _cam;
        private int _currentChapter;

        // 바이옴 파티클 설정
        private Color _particleColor;
        private float _particleSize;
        private float _particleSpeed;
        private float _particleLifetime;
        private Sprite _particleSprite;

        // 스프라이트 캐시
        private Sprite _grassSprite;
        private Sprite _snowSprite;
        private Sprite _sandSprite;
        private Sprite _emberSprite;
        private Sprite _manaSprite;

        private void Awake()
        {
            _particleRoot = new GameObject("BiomeParticles").transform;
            _particleRoot.SetParent(transform, false);

            _particles = new SpriteRenderer[_maxParticles];
            _velocities = new Vector2[_maxParticles];
            _lifetimes = new float[_maxParticles];
            _maxLifetimes = new float[_maxParticles];
            _rotSpeeds = new float[_maxParticles];

            CreateSprites();

            for (int i = 0; i < _maxParticles; i++)
            {
                var go = new GameObject($"P{i}");
                go.transform.SetParent(_particleRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 50;
                sr.sprite = _grassSprite;
                sr.color = Color.clear;
                go.SetActive(false);
                _particles[i] = sr;
            }

            _cam = Camera.main;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void Start()
        {
            SetBiome(1);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            if (evt.Chapter != _currentChapter)
                SetBiome(evt.Chapter);
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector2 camPos = _cam.transform.position;

            // 스폰
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && _activeCount < _maxParticles)
            {
                _spawnTimer = _spawnInterval;
                SpawnParticle(camPos);
            }

            // 업데이트
            for (int i = 0; i < _maxParticles; i++)
            {
                if (!_particles[i].gameObject.activeSelf) continue;

                _lifetimes[i] -= Time.deltaTime;
                if (_lifetimes[i] <= 0f)
                {
                    _particles[i].gameObject.SetActive(false);
                    _activeCount--;
                    continue;
                }

                // 이동
                var t = _particles[i].transform;
                Vector3 pos = t.position;
                pos.x += _velocities[i].x * Time.deltaTime;
                pos.y += _velocities[i].y * Time.deltaTime;
                t.position = pos;

                // 회전
                t.Rotate(0f, 0f, _rotSpeeds[i] * Time.deltaTime);

                // 페이드
                float lifeRatio = _lifetimes[i] / _maxLifetimes[i];
                float alpha;
                if (lifeRatio > 0.8f)
                    alpha = (1f - lifeRatio) / 0.2f; // 페이드인
                else if (lifeRatio < 0.3f)
                    alpha = lifeRatio / 0.3f; // 페이드아웃
                else
                    alpha = 1f;

                Color col = _particleColor;
                col.a = alpha;
                _particles[i].color = col;
            }
        }

        private void SpawnParticle(Vector2 camPos)
        {
            for (int i = 0; i < _maxParticles; i++)
            {
                if (_particles[i].gameObject.activeSelf) continue;

                // 카메라 주변 랜덤 위치에서 스폰
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(_spawnRadius * 0.3f, _spawnRadius);
                Vector2 spawnPos = camPos + new Vector2(
                    Mathf.Cos(angle) * dist,
                    Mathf.Sin(angle) * dist
                );

                _particles[i].transform.position = new Vector3(spawnPos.x, spawnPos.y, -0.5f);
                _particles[i].transform.localScale = Vector3.one * _particleSize * Random.Range(0.8f, 1.6f);
                _particles[i].transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _particles[i].sprite = _particleSprite;
                _particles[i].gameObject.SetActive(true);

                // 바람 방향 + 약간의 랜덤
                _velocities[i] = new Vector2(
                    _particleSpeed * Random.Range(0.5f, 1.5f),
                    _particleSpeed * Random.Range(-0.3f, 0.5f)
                );
                _maxLifetimes[i] = _particleLifetime * Random.Range(0.7f, 1.3f);
                _lifetimes[i] = _maxLifetimes[i];
                _rotSpeeds[i] = Random.Range(-90f, 90f);
                _activeCount++;
                return;
            }
        }

        private void SetBiome(int chapter)
        {
            _currentChapter = chapter;
            int biome = ((chapter - 1) % 15);

            // 바이옴별 파티클 설정
            // 0:초원 1:숲 2:해안 3:도시 4:하수도 5:사막 6:설원 7:동굴
            // 8:구름 9:시계탑 10:마을 11:화산 12:늪 13:결정 14:황금사원
            switch (biome)
            {
                case 0: // 초원 — 풀잎+꽃잎
                    SetParams(_grassSprite, C2(0.25f, 0.50f, 0.12f), 0.4f, 1.0f, 3.5f, 0.08f);
                    break;
                case 1: // 숲 — 짙은 풀잎
                    SetParams(_grassSprite, C2(0.15f, 0.40f, 0.08f), 0.35f, 1.2f, 3f, 0.08f);
                    break;
                case 2: // 해안 — 모래 입자
                    SetParams(_sandSprite, C2(0.75f, 0.68f, 0.50f), 0.2f, 0.8f, 4f, 0.1f);
                    break;
                case 3: // 도시 — 먼지
                    SetParams(_sandSprite, C2(0.55f, 0.50f, 0.45f), 0.15f, 0.4f, 5f, 0.15f);
                    break;
                case 4: // 하수도 — 물방울
                    SetParams(_sandSprite, C2(0.30f, 0.45f, 0.30f), 0.18f, 0.3f, 4f, 0.12f);
                    break;
                case 5: // 사막 — 모래바람
                    SetParams(_sandSprite, C2(0.80f, 0.65f, 0.38f), 0.22f, 2.5f, 2.5f, 0.05f);
                    break;
                case 6: // 설원 — 눈송이
                    SetParams(_snowSprite, C2(0.90f, 0.92f, 0.95f), 0.3f, 0.5f, 4.5f, 0.06f);
                    break;
                case 7: // 동굴 — 먼지
                    SetParams(_sandSprite, C2(0.50f, 0.45f, 0.35f), 0.15f, 0.2f, 6f, 0.15f);
                    break;
                case 8: // 구름 — 흰 입자
                    SetParams(_snowSprite, C2(0.85f, 0.88f, 0.95f), 0.45f, 0.5f, 5f, 0.08f);
                    break;
                case 9: // 시계탑 — 금속 파편
                    SetParams(_emberSprite, C2(0.65f, 0.55f, 0.30f), 0.2f, 0.6f, 3f, 0.12f);
                    break;
                case 10: // 마을 — 풀잎
                    SetParams(_grassSprite, C2(0.45f, 0.58f, 0.30f), 0.3f, 0.8f, 3.5f, 0.1f);
                    break;
                case 11: // 화산 — 불씨
                    SetParams(_emberSprite, C2(1f, 0.40f, 0.08f), 0.25f, 1.5f, 2f, 0.05f);
                    break;
                case 12: // 늪 — 독안개
                    SetParams(_manaSprite, C2(0.35f, 0.45f, 0.28f), 0.4f, 0.3f, 5f, 0.08f);
                    break;
                case 13: // 결정동굴 — 마나
                    SetParams(_manaSprite, C2(0.55f, 0.30f, 0.75f), 0.25f, 0.5f, 4f, 0.08f);
                    break;
                case 14: // 황금사원 — 금빛 입자
                    SetParams(_emberSprite, C2(0.85f, 0.70f, 0.25f), 0.22f, 0.7f, 3f, 0.08f);
                    break;
            }
        }

        private void SetParams(Sprite sprite, Color color, float size, float speed, float life, float interval)
        {
            _particleSprite = sprite;
            _particleColor = color;
            _particleSize = size;
            _particleSpeed = speed;
            _particleLifetime = life;
            _spawnInterval = interval;
        }

        private static Color C2(float r, float g, float b) => new(r, g, b);

        #region 프로시져럴 스프라이트 생성

        private void CreateSprites()
        {
            _grassSprite = MakeSprite(8, 8, DrawGrass);
            _snowSprite = MakeSprite(6, 6, DrawSnow);
            _sandSprite = MakeSprite(4, 4, DrawDot);
            _emberSprite = MakeSprite(6, 6, DrawEmber);
            _manaSprite = MakeSprite(6, 6, DrawMana);
        }

        private static Sprite MakeSprite(int w, int h, System.Action<Color32[], int, int> draw)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[w * h];
            draw(pixels, w, h);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 8f);
        }

        // 풀잎 형태 (세로 곡선)
        private static void DrawGrass(Color32[] px, int w, int h)
        {
            Color32 g = new Color32(255, 255, 255, 255);
            // 세로 줄기
            px[3 * w + 3] = g; px[3 * w + 4] = g;
            px[4 * w + 3] = g; px[4 * w + 4] = g;
            px[5 * w + 4] = g; px[5 * w + 5] = g;
            px[6 * w + 4] = g;
            px[7 * w + 5] = g;
            // 잎
            px[2 * w + 2] = g; px[2 * w + 3] = g;
            px[1 * w + 1] = g; px[1 * w + 2] = g;
            px[0 * w + 1] = g;
            // 오른쪽 잎
            px[2 * w + 5] = g; px[2 * w + 6] = g;
            px[1 * w + 6] = g;
        }

        // 눈송이 (십자+대각)
        private static void DrawSnow(Color32[] px, int w, int h)
        {
            Color32 s = new Color32(255, 255, 255, 255);
            px[3 * w + 2] = s; px[3 * w + 3] = s; // 가로
            px[2 * w + 2] = s; px[2 * w + 3] = s; // 가로
            px[0 * w + 3] = s; px[5 * w + 2] = s; // 세로 끝
            px[1 * w + 1] = s; px[4 * w + 4] = s; // 대각
            px[1 * w + 4] = s; px[4 * w + 1] = s; // 대각
        }

        // 작은 점 (먼지/모래)
        private static void DrawDot(Color32[] px, int w, int h)
        {
            Color32 d = new Color32(255, 255, 255, 255);
            px[1 * w + 1] = d; px[1 * w + 2] = d;
            px[2 * w + 1] = d; px[2 * w + 2] = d;
        }

        // 불씨 (밝은 중심 + 흐린 외곽)
        private static void DrawEmber(Color32[] px, int w, int h)
        {
            Color32 bright = new Color32(255, 255, 255, 255);
            Color32 dim = new Color32(255, 255, 255, 128);
            px[2 * w + 2] = bright; px[2 * w + 3] = bright;
            px[3 * w + 2] = bright; px[3 * w + 3] = bright;
            px[1 * w + 2] = dim; px[1 * w + 3] = dim;
            px[4 * w + 2] = dim; px[4 * w + 3] = dim;
            px[2 * w + 1] = dim; px[3 * w + 4] = dim;
        }

        // 마나 입자 (다이아몬드)
        private static void DrawMana(Color32[] px, int w, int h)
        {
            Color32 m = new Color32(255, 255, 255, 255);
            Color32 d = new Color32(255, 255, 255, 160);
            px[0 * w + 3] = d;
            px[1 * w + 2] = m; px[1 * w + 3] = m;
            px[2 * w + 1] = m; px[2 * w + 2] = m; px[2 * w + 3] = m; px[2 * w + 4] = m;
            px[3 * w + 1] = m; px[3 * w + 2] = m; px[3 * w + 3] = m; px[3 * w + 4] = m;
            px[4 * w + 2] = m; px[4 * w + 3] = m;
            px[5 * w + 2] = d;
        }

        #endregion
    }
}
