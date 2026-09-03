using UnityEngine;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 프로시져럴 바닥 패턴 + 존별 장식물 + 배경 파티클 + 패럴랙스 + 아레나 테두리.
    /// ZoneVisualManager와 연동하여 챕터/존 변경 시 비주얼을 갱신한다.
    /// </summary>
    public class ProceduralBackground : MonoBehaviour
    {
        public static ProceduralBackground Instance { get; private set; }

        private const int MAX_DECORATIONS = 40;
        private const int MAX_PARTICLES = 25;
        private const int PATTERN_TEX_SIZE = 64;
        private const int GROUND_TEX_SIZE = 128;
        private const float PARTICLE_SPAWN_INTERVAL = 0.4f;

        [Header("패럴랙스")]
        [SerializeField] private float _foregroundFactor = 0.8f;
        [SerializeField] private float _backgroundFactor = 0.3f;

        [Header("아레나 테두리")]
        [SerializeField] private Color _borderColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private float _borderThickness = 0.15f;

        private Transform _decorationRoot;
        private Transform _particleRoot;
        private Transform _borderRoot;
        private Transform _patternRoot;
        private Transform _groundRoot;

        private SpriteRenderer[] _decorations;
        private int _activeDecoCount;

        private SpriteRenderer[] _particles;
        private int _activeParticleCount;
        private float _particleTimer;

        private SpriteRenderer _patternRenderer;
        private SpriteRenderer _groundRenderer;
        private SpriteRenderer[] _borders; // 상하좌우 4개

        private Camera _mainCam;
        private Vector3 _prevCamPos;
        private Transform _foregroundLayer; // patternRoot
        private Transform _backgroundLayer; // decorationRoot

        private string _currentZone = "";
        private Rect _arenaBounds;
        private bool _hasBounds;

        // 프로시져럴 텍스처 캐시
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _spriteCache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _decorationRoot = new GameObject("Deco_Root").transform;
            _decorationRoot.SetParent(transform, false);

            _particleRoot = new GameObject("Particle_Root").transform;
            _particleRoot.SetParent(transform, false);

            _borderRoot = new GameObject("Border_Root").transform;
            _borderRoot.SetParent(transform, false);

            _patternRoot = new GameObject("Pattern_Root").transform;
            _patternRoot.SetParent(transform, false);

            _groundRoot = new GameObject("Ground_Root").transform;
            _groundRoot.SetParent(transform, false);

            _foregroundLayer = _patternRoot;
            _backgroundLayer = _decorationRoot;

            _decorations = new SpriteRenderer[MAX_DECORATIONS];
            _particles = new SpriteRenderer[MAX_PARTICLES];
            _borders = new SpriteRenderer[4];

            _mainCam = Camera.main;
            if (_mainCam != null) _prevCamPos = _mainCam.transform.position;

            InitBorders();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<ArenaMatchEvent>(OnArenaMatchEnd);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<ArenaMatchEvent>(OnArenaMatchEnd);
        }

        private void Update()
        {
            UpdateParallax();
            UpdateParticleSpawn();
        }

        #region 이벤트

        private void OnStageChanged(StageChangedEvent evt)
        {
            string zone = GetChapterZoneName(evt.Chapter);
            if (zone == _currentZone) return;
            _currentZone = zone;

            RefreshArenaBounds();
            if (!_hasBounds) return;

            RebuildGround(zone);
            RebuildPattern(zone);
            RebuildDecorations(zone);
            UpdateBorderVisibility(true);
        }

        private void OnArenaMatchEnd(ArenaMatchEvent evt)
        {
            // 아레나 종료 후에도 유지
        }

        #endregion

        #region 공개 API

        /// <summary>
        /// 외부에서 존 변경 시 호출. ZoneVisualManager 등에서 사용.
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (zoneName == _currentZone) return;
            _currentZone = zoneName;

            RefreshArenaBounds();
            if (!_hasBounds) return;

            RebuildGround(zoneName);
            RebuildPattern(zoneName);
            RebuildDecorations(zoneName);
        }

        /// <summary>
        /// 장식물 스폰 API.
        /// </summary>
        public void SpawnDecorations(string zone, Bounds bounds)
        {
            _arenaBounds = new Rect(
                bounds.min.x, bounds.min.y,
                bounds.size.x, bounds.size.y);
            _hasBounds = true;
            RebuildDecorations(zone);
        }

        #endregion

        #region 바닥 그라운드 텍스처 (노이즈 기반)

        private void RebuildGround(string zone)
        {
            if (_groundRenderer == null)
            {
                var go = new GameObject("GroundTexture");
                go.transform.SetParent(_groundRoot, false);
                go.transform.localPosition = new Vector3(0f, 0f, 9.5f);
                _groundRenderer = go.AddComponent<SpriteRenderer>();
                _groundRenderer.sortingOrder = -100;
            }

            _groundRenderer.sprite = GetOrCreateGroundSprite(zone);
            _groundRenderer.color = Color.white;

            float scaleX = _arenaBounds.width / (GROUND_TEX_SIZE * 0.01f) * 0.55f;
            float scaleY = _arenaBounds.height / (GROUND_TEX_SIZE * 0.01f) * 0.55f;
            _groundRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            _groundRenderer.transform.position = new Vector3(_arenaBounds.center.x, _arenaBounds.center.y, 9.5f);
        }

        private static Sprite GetOrCreateGroundSprite(string zone)
        {
            string key = $"ground_{zone}";
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;

            var tex = new Texture2D(GROUND_TEX_SIZE, GROUND_TEX_SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            var pixels = new Color[GROUND_TEX_SIZE * GROUND_TEX_SIZE];

            // 존별 기본 색상 가져오기
            GetGroundColors(zone, out Color baseColor, out Color detailColor, out Color accentColor);

            // 간단한 해시 기반 노이즈로 자연스러운 바닥 생성
            int seed = zone.GetHashCode();
            var rng = new System.Random(seed);

            for (int y = 0; y < GROUND_TEX_SIZE; y++)
            {
                for (int x = 0; x < GROUND_TEX_SIZE; x++)
                {
                    // 여러 스케일의 노이즈 합성
                    float n1 = PseudoNoise(x * 0.05f, y * 0.05f, seed);
                    float n2 = PseudoNoise(x * 0.12f, y * 0.12f, seed + 100) * 0.5f;
                    float n3 = PseudoNoise(x * 0.25f, y * 0.25f, seed + 200) * 0.25f;
                    float noise = Mathf.Clamp01((n1 + n2 + n3) / 1.75f);

                    // 기본 색상에 노이즈로 변화
                    Color c = Color.Lerp(baseColor, detailColor, noise);

                    // 간헐적 액센트 포인트
                    float accent = PseudoNoise(x * 0.08f + 50f, y * 0.08f + 50f, seed + 300);
                    if (accent > 0.75f)
                    {
                        c = Color.Lerp(c, accentColor, (accent - 0.75f) * 2f);
                    }

                    pixels[y * GROUND_TEX_SIZE + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, GROUND_TEX_SIZE, GROUND_TEX_SIZE),
                Vector2.one * 0.5f, 100f);
            _spriteCache[key] = sprite;
            return sprite;
        }

        private static void GetGroundColors(string zone, out Color baseColor, out Color detailColor, out Color accentColor)
        {
            switch (zone)
            {
                case "숲":
                    baseColor = new Color(0.12f, 0.22f, 0.08f);    // 어두운 풀색
                    detailColor = new Color(0.18f, 0.32f, 0.12f);  // 밝은 풀색
                    accentColor = new Color(0.25f, 0.38f, 0.15f);  // 밝은 이끼
                    break;
                case "동굴":
                    baseColor = new Color(0.14f, 0.11f, 0.09f);    // 어두운 돌바닥
                    detailColor = new Color(0.22f, 0.18f, 0.14f);  // 밝은 돌
                    accentColor = new Color(0.18f, 0.15f, 0.20f);  // 자색 광물
                    break;
                case "화산":
                    baseColor = new Color(0.18f, 0.08f, 0.04f);    // 어두운 화산암
                    detailColor = new Color(0.28f, 0.12f, 0.06f);  // 밝은 용암석
                    accentColor = new Color(0.55f, 0.20f, 0.05f);  // 용암 틈새 빛
                    break;
                case "하늘":
                    baseColor = new Color(0.14f, 0.22f, 0.35f);    // 어두운 하늘돌
                    detailColor = new Color(0.20f, 0.30f, 0.45f);  // 밝은 하늘빛 돌
                    accentColor = new Color(0.30f, 0.40f, 0.60f);  // 빛나는 결정
                    break;
                case "심연":
                    baseColor = new Color(0.10f, 0.05f, 0.15f);    // 어두운 심연
                    detailColor = new Color(0.18f, 0.08f, 0.25f);  // 보라빛 바닥
                    accentColor = new Color(0.30f, 0.12f, 0.40f);  // 마력 빛
                    break;
                default:
                    baseColor = new Color(0.12f, 0.14f, 0.10f);
                    detailColor = new Color(0.18f, 0.20f, 0.16f);
                    accentColor = new Color(0.22f, 0.24f, 0.20f);
                    break;
            }
        }

        /// <summary>
        /// 간단한 해시 기반 의사 노이즈. Perlin 없이 부드러운 변화 생성.
        /// </summary>
        private static float PseudoNoise(float x, float y, int seed)
        {
            // 격자점 보간
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

            // Smoothstep
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float v00 = HashFloat(ix, iy, seed);
            float v10 = HashFloat(ix + 1, iy, seed);
            float v01 = HashFloat(ix, iy + 1, seed);
            float v11 = HashFloat(ix + 1, iy + 1, seed);

            float a = Mathf.Lerp(v00, v10, fx);
            float b = Mathf.Lerp(v01, v11, fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float HashFloat(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        #endregion

        #region 바닥 오버레이 패턴

        private void RebuildPattern(string zone)
        {
            if (_patternRenderer == null)
            {
                var go = new GameObject("FloorPattern");
                go.transform.SetParent(_patternRoot, false);
                go.transform.localPosition = new Vector3(0f, 0f, 9f);
                _patternRenderer = go.AddComponent<SpriteRenderer>();
                _patternRenderer.sortingOrder = -99;
            }

            _patternRenderer.sprite = GetOrCreatePatternSprite(zone);
            _patternRenderer.color = GetPatternTint(zone);

            // 아레나 크기에 맞춰 스케일
            float scaleX = _arenaBounds.width / (PATTERN_TEX_SIZE * 0.01f) * 0.5f;
            float scaleY = _arenaBounds.height / (PATTERN_TEX_SIZE * 0.01f) * 0.5f;
            _patternRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            _patternRenderer.transform.position = new Vector3(_arenaBounds.center.x, _arenaBounds.center.y, 9f);
        }

        private static Sprite GetOrCreatePatternSprite(string zone)
        {
            string key = $"pattern_{zone}";
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;

            var tex = new Texture2D(PATTERN_TEX_SIZE, PATTERN_TEX_SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            Color bg = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[PATTERN_TEX_SIZE * PATTERN_TEX_SIZE];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            switch (zone)
            {
                case "숲":
                    // 풀밭 텍스처 — 불규칙 풀잎 패턴
                    GenerateGrassPattern(pixels);
                    break;

                case "동굴":
                    // 돌바닥 균열 패턴
                    GenerateCrackPattern(pixels, 42);
                    break;

                case "화산":
                    // 용암 균열 (빛나는 선)
                    GenerateLavaPattern(pixels);
                    break;

                case "하늘":
                    // 구름/안개 패턴
                    GenerateCloudPattern(pixels);
                    break;

                case "심연":
                    // 나선형 마력 패턴
                    GenerateAbyssPattern(pixels);
                    break;

                default:
                    // 기본 격자
                    for (int y = 0; y < PATTERN_TEX_SIZE; y++)
                        for (int x = 0; x < PATTERN_TEX_SIZE; x++)
                            if (x % 8 == 0 || y % 8 == 0)
                                pixels[y * PATTERN_TEX_SIZE + x] = new Color(1f, 1f, 1f, 0.08f);
                    break;
            }

            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, PATTERN_TEX_SIZE, PATTERN_TEX_SIZE),
                Vector2.one * 0.5f, 100f);
            _spriteCache[key] = sprite;
            return sprite;
        }

        private static void GenerateGrassPattern(Color[] pixels)
        {
            var rng = new System.Random(101);
            Color grassDark = new Color(0.2f, 0.45f, 0.12f, 0.15f);
            Color grassLight = new Color(0.35f, 0.6f, 0.2f, 0.12f);

            // 풀잎 라인 (세로 줄기)
            for (int i = 0; i < 80; i++)
            {
                int bx = rng.Next(PATTERN_TEX_SIZE);
                int by = rng.Next(PATTERN_TEX_SIZE);
                int height = 2 + rng.Next(5);
                Color c = rng.NextDouble() > 0.5 ? grassDark : grassLight;

                for (int h = 0; h < height; h++)
                {
                    int py = (by + h) % PATTERN_TEX_SIZE;
                    int px = bx + (h > 2 ? (rng.Next(3) - 1) : 0);
                    px = (px + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;
                    pixels[py * PATTERN_TEX_SIZE + px] = c;
                }
            }

            // 작은 꽃/잎 점
            for (int i = 0; i < 30; i++)
            {
                int px = rng.Next(PATTERN_TEX_SIZE);
                int py = rng.Next(PATTERN_TEX_SIZE);
                pixels[py * PATTERN_TEX_SIZE + px] = new Color(0.5f, 0.7f, 0.3f, 0.18f);
            }
        }

        private static void GenerateCrackPattern(Color[] pixels, int seed)
        {
            var rng = new System.Random(seed);
            Color crackColor = new Color(0.3f, 0.25f, 0.2f, 0.18f);

            // 균열 선 여러 개
            for (int line = 0; line < 8; line++)
            {
                float cx = rng.Next(PATTERN_TEX_SIZE);
                float cy = rng.Next(PATTERN_TEX_SIZE);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                int length = 10 + rng.Next(20);

                for (int s = 0; s < length; s++)
                {
                    int px = ((int)cx + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;
                    int py = ((int)cy + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;
                    pixels[py * PATTERN_TEX_SIZE + px] = crackColor;

                    // 분기
                    if (rng.NextDouble() > 0.7f)
                        angle += (float)(rng.NextDouble() - 0.5) * 1.2f;

                    cx += Mathf.Cos(angle);
                    cy += Mathf.Sin(angle);
                }
            }

            // 돌 경계 점
            for (int i = 0; i < 40; i++)
            {
                int px = rng.Next(PATTERN_TEX_SIZE);
                int py = rng.Next(PATTERN_TEX_SIZE);
                pixels[py * PATTERN_TEX_SIZE + px] = new Color(0.4f, 0.35f, 0.3f, 0.12f);
            }
        }

        private static void GenerateLavaPattern(Color[] pixels)
        {
            Color lavaGlow = new Color(1f, 0.35f, 0.08f, 0.2f);
            Color lavaDim = new Color(0.7f, 0.15f, 0.02f, 0.12f);

            // 대각선 + 불규칙 용암 균열
            var rng = new System.Random(77);
            for (int line = 0; line < 6; line++)
            {
                float cx = rng.Next(PATTERN_TEX_SIZE);
                float cy = rng.Next(PATTERN_TEX_SIZE);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                int length = 15 + rng.Next(25);

                for (int s = 0; s < length; s++)
                {
                    int px = ((int)cx + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;
                    int py = ((int)cy + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;

                    // 중심선은 밝게
                    pixels[py * PATTERN_TEX_SIZE + px] = lavaGlow;
                    // 양옆 글로우
                    int px2 = (px + 1) % PATTERN_TEX_SIZE;
                    int px3 = (px - 1 + PATTERN_TEX_SIZE) % PATTERN_TEX_SIZE;
                    pixels[py * PATTERN_TEX_SIZE + px2] = lavaDim;
                    pixels[py * PATTERN_TEX_SIZE + px3] = lavaDim;

                    if (rng.NextDouble() > 0.6f)
                        angle += (float)(rng.NextDouble() - 0.5) * 0.8f;

                    cx += Mathf.Cos(angle);
                    cy += Mathf.Sin(angle);
                }
            }
        }

        private static void GenerateCloudPattern(Color[] pixels)
        {
            // 부드러운 원형 클러스터
            var rng = new System.Random(55);
            for (int c = 0; c < 12; c++)
            {
                float cx = rng.Next(PATTERN_TEX_SIZE);
                float cy = rng.Next(PATTERN_TEX_SIZE);
                float radius = 3f + (float)rng.NextDouble() * 6f;
                float alpha = 0.06f + (float)rng.NextDouble() * 0.08f;

                for (int y = 0; y < PATTERN_TEX_SIZE; y++)
                {
                    for (int x = 0; x < PATTERN_TEX_SIZE; x++)
                    {
                        float dx = Mathf.Min(Mathf.Abs(x - cx), PATTERN_TEX_SIZE - Mathf.Abs(x - cx));
                        float dy = Mathf.Min(Mathf.Abs(y - cy), PATTERN_TEX_SIZE - Mathf.Abs(y - cy));
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist < radius)
                        {
                            float falloff = 1f - (dist / radius);
                            falloff *= falloff; // 부드러운 감쇠
                            Color existing = pixels[y * PATTERN_TEX_SIZE + x];
                            Color add = new Color(0.8f, 0.85f, 1f, alpha * falloff);
                            pixels[y * PATTERN_TEX_SIZE + x] = BlendAdditive(existing, add);
                        }
                    }
                }
            }
        }

        private static void GenerateAbyssPattern(Color[] pixels)
        {
            float center = PATTERN_TEX_SIZE * 0.5f;

            for (int y = 0; y < PATTERN_TEX_SIZE; y++)
            {
                for (int x = 0; x < PATTERN_TEX_SIZE; x++)
                {
                    float cx = x - center;
                    float cy = y - center;
                    float angle = Mathf.Atan2(cy, cx);
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    // 이중 나선
                    float spiral1 = Mathf.Sin(angle * 3f + dist * 0.3f);
                    float spiral2 = Mathf.Sin(angle * 5f - dist * 0.2f + 1.5f);
                    float combined = (spiral1 + spiral2) * 0.5f;

                    if (combined > 0.5f)
                    {
                        float intensity = (combined - 0.5f) * 2f;
                        pixels[y * PATTERN_TEX_SIZE + x] = new Color(
                            0.4f, 0.15f, 0.6f,
                            0.12f * intensity);
                    }

                    // 가장자리에서 빛나는 점
                    if (dist > center * 0.6f && dist < center * 0.9f)
                    {
                        float ring = Mathf.Sin(angle * 8f + dist * 0.5f);
                        if (ring > 0.85f)
                        {
                            pixels[y * PATTERN_TEX_SIZE + x] = new Color(
                                0.6f, 0.3f, 0.8f,
                                0.15f * (ring - 0.85f) * 6.67f);
                        }
                    }
                }
            }
        }

        private static Color BlendAdditive(Color a, Color b)
        {
            return new Color(
                Mathf.Min(a.r + b.r * b.a, 1f),
                Mathf.Min(a.g + b.g * b.a, 1f),
                Mathf.Min(a.b + b.b * b.a, 1f),
                Mathf.Min(a.a + b.a, 1f));
        }

        private static Color GetPatternTint(string zone)
        {
            return zone switch
            {
                "숲" => new Color(0.4f, 0.7f, 0.3f, 0.4f),
                "동굴" => new Color(0.6f, 0.5f, 0.4f, 0.35f),
                "화산" => new Color(0.9f, 0.4f, 0.15f, 0.35f),
                "하늘" => new Color(0.7f, 0.8f, 1f, 0.3f),
                "심연" => new Color(0.6f, 0.3f, 0.8f, 0.35f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.2f)
            };
        }

        #endregion

        #region 장식물

        private void RebuildDecorations(string zone)
        {
            // 기존 장식 비활성화
            for (int i = 0; i < _activeDecoCount; i++)
            {
                if (_decorations[i] != null)
                    _decorations[i].gameObject.SetActive(false);
            }
            _activeDecoCount = 0;

            int count = GetDecorationCount(zone);
            var rng = new System.Random(_currentZone.GetHashCode());

            for (int i = 0; i < count && i < MAX_DECORATIONS; i++)
            {
                if (_decorations[i] == null)
                {
                    var go = new GameObject($"Deco_{i}");
                    go.transform.SetParent(_decorationRoot, false);
                    _decorations[i] = go.AddComponent<SpriteRenderer>();
                    _decorations[i].sortingOrder = -98;
                }

                var sr = _decorations[i];
                // 다양한 장식 타입 선택
                int decoType = rng.Next(3);
                sr.sprite = GetOrCreateDecoSprite(zone, decoType);
                sr.color = GetDecoColor(zone, rng);

                float x = _arenaBounds.xMin + (float)rng.NextDouble() * _arenaBounds.width;
                float y = _arenaBounds.yMin + (float)rng.NextDouble() * _arenaBounds.height;
                sr.transform.position = new Vector3(x, y, 8f);

                float scale = 0.3f + (float)rng.NextDouble() * 0.6f;
                sr.transform.localScale = Vector3.one * scale;

                // 랜덤 회전으로 자연스러움
                sr.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);

                sr.gameObject.SetActive(true);
                _activeDecoCount++;
            }
        }

        private static int GetDecorationCount(string zone)
        {
            return zone switch
            {
                "숲" => 30,
                "동굴" => 20,
                "화산" => 15,
                "하늘" => 12,
                "심연" => 22,
                _ => 12
            };
        }

        private static Sprite GetOrCreateDecoSprite(string zone, int variant)
        {
            string key = $"deco_{zone}_{variant}";
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;

            int size = 16; // 8 → 16으로 해상도 증가
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];

            switch (zone)
            {
                case "숲":
                    GenerateForestDeco(pixels, size, variant);
                    break;
                case "동굴":
                    GenerateCaveDeco(pixels, size, variant);
                    break;
                case "화산":
                    GenerateVolcanoDeco(pixels, size, variant);
                    break;
                case "하늘":
                    GenerateSkyDeco(pixels, size, variant);
                    break;
                case "심연":
                    GenerateAbyssDeco(pixels, size, variant);
                    break;
                default:
                    GenerateDefaultDeco(pixels, size, variant);
                    break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 16f);
            _spriteCache[key] = sprite;
            return sprite;
        }

        private static void GenerateForestDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            switch (variant)
            {
                case 0: // 풀 뭉치
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy * 0.6f);
                            if (dist < center * 0.8f)
                            {
                                float a = 1f - dist / (center * 0.8f);
                                pixels[y * size + x] = new Color(1f, 1f, 1f, a * 0.8f);
                            }
                        }
                    break;
                case 1: // 작은 나무 그림자 (삼각형)
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float normY = (float)y / size;
                            float halfWidth = (1f - normY) * center * 0.7f;
                            if (Mathf.Abs(x - center) < halfWidth)
                                pixels[y * size + x] = new Color(1f, 1f, 1f, 0.6f * normY);
                        }
                    break;
                default: // 둥근 이끼
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            if (dx * dx + dy * dy < center * center * 0.5f)
                                pixels[y * size + x] = new Color(1f, 1f, 1f, 0.5f);
                        }
                    break;
            }
        }

        private static void GenerateCaveDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            switch (variant)
            {
                case 0: // 둥근 바위
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x - center) / center;
                            float dy = (y - center * 0.8f) / (center * 0.6f);
                            float dist = dx * dx + dy * dy;
                            if (dist < 1f)
                            {
                                float shade = 1f - dist * 0.5f;
                                pixels[y * size + x] = new Color(shade, shade, shade, 0.7f);
                            }
                        }
                    break;
                case 1: // 각진 바위 조각
                    for (int y = 2; y < size - 2; y++)
                        for (int x = 2; x < size - 2; x++)
                        {
                            int cx = Mathf.Abs(x - (int)center);
                            int cy = Mathf.Abs(y - (int)center);
                            if (cx + cy < (int)(center * 0.9f))
                                pixels[y * size + x] = Color.white * 0.6f;
                        }
                    break;
                default: // 작은 돌멩이들
                    var rng = new System.Random(variant * 13);
                    for (int s = 0; s < 3; s++)
                    {
                        int sx = 2 + rng.Next(size - 6);
                        int sy = 2 + rng.Next(size - 6);
                        int r = 1 + rng.Next(2);
                        for (int dy = -r; dy <= r; dy++)
                            for (int dx = -r; dx <= r; dx++)
                                if (dx * dx + dy * dy <= r * r)
                                {
                                    int px = sx + dx, py = sy + dy;
                                    if (px >= 0 && px < size && py >= 0 && py < size)
                                        pixels[py * size + px] = new Color(1f, 1f, 1f, 0.5f);
                                }
                    }
                    break;
            }
        }

        private static void GenerateVolcanoDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            switch (variant)
            {
                case 0: // 용암 웅덩이 (원형 글로우)
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist < center * 0.7f)
                            {
                                float glow = 1f - dist / (center * 0.7f);
                                pixels[y * size + x] = new Color(1f, 0.7f * glow, 0.2f * glow, glow * 0.8f);
                            }
                        }
                    break;
                case 1: // 화산 암석
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            int cx = Mathf.Abs((int)dx), cy = Mathf.Abs((int)dy);
                            if (cx + cy < (int)(center * 0.8f))
                                pixels[y * size + x] = new Color(0.6f, 0.3f, 0.2f, 0.6f);
                        }
                    break;
                default: // 재/먼지 점
                    var rng = new System.Random(variant * 7);
                    for (int i = 0; i < 8; i++)
                    {
                        int px = rng.Next(size);
                        int py = rng.Next(size);
                        pixels[py * size + px] = new Color(0.4f, 0.2f, 0.1f, 0.5f);
                        if (px + 1 < size) pixels[py * size + px + 1] = new Color(0.3f, 0.15f, 0.08f, 0.3f);
                    }
                    break;
            }
        }

        private static void GenerateSkyDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            switch (variant)
            {
                case 0: // 구름 조각
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x - center) / (center * 1.2f);
                            float dy = (y - center) / (center * 0.6f);
                            float dist = dx * dx + dy * dy;
                            if (dist < 1f)
                            {
                                float a = (1f - dist);
                                a = a * a;
                                pixels[y * size + x] = new Color(1f, 1f, 1f, a * 0.5f);
                            }
                        }
                    break;
                case 1: // 빛나는 별
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Abs(x - center);
                            float dy = Mathf.Abs(y - center);
                            bool onCross = (dx < 1.5f || dy < 1.5f) && (dx + dy < center);
                            if (onCross)
                            {
                                float dist = dx + dy;
                                float a = 1f - dist / center;
                                pixels[y * size + x] = new Color(1f, 1f, 1f, a * 0.7f);
                            }
                        }
                    break;
                default: // 부드러운 원형 빛
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist < center)
                            {
                                float a = (1f - dist / center);
                                pixels[y * size + x] = new Color(0.9f, 0.95f, 1f, a * a * 0.3f);
                            }
                        }
                    break;
            }
        }

        private static void GenerateAbyssDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            switch (variant)
            {
                case 0: // 마력 결정
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Abs(x - center);
                            float dy = Mathf.Abs(y - center);
                            // 다이아몬드 형태
                            if (dx / center + dy / center < 0.7f)
                            {
                                float dist = (dx + dy) / center;
                                float a = 1f - dist / 0.7f;
                                pixels[y * size + x] = new Color(0.8f, 0.5f, 1f, a * 0.7f);
                            }
                        }
                    break;
                case 1: // 안개 웅덩이
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x - center, dy = y - center;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist < center * 0.9f)
                            {
                                float a = (1f - dist / (center * 0.9f));
                                a = a * a;
                                pixels[y * size + x] = new Color(0.5f, 0.2f, 0.7f, a * 0.4f);
                            }
                        }
                    break;
                default: // 흩어진 마력 점
                    var rng = new System.Random(variant * 31);
                    for (int i = 0; i < 6; i++)
                    {
                        int px = rng.Next(size);
                        int py = rng.Next(size);
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int fx = px + dx, fy = py + dy;
                                if (fx >= 0 && fx < size && fy >= 0 && fy < size)
                                {
                                    float a = (dx == 0 && dy == 0) ? 0.6f : 0.25f;
                                    pixels[fy * size + fx] = new Color(0.6f, 0.3f, 0.9f, a);
                                }
                            }
                    }
                    break;
            }
        }

        private static void GenerateDefaultDeco(Color[] pixels, int size, int variant)
        {
            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center, dy = y - center;
                    if (dx * dx + dy * dy < center * center * 0.5f)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0.4f);
                }
        }

        private static Color GetDecoColor(string zone, System.Random rng)
        {
            float v = 0.8f + (float)rng.NextDouble() * 0.2f;
            return zone switch
            {
                "숲" => new Color(0.18f * v, 0.50f * v, 0.12f * v, 0.35f),
                "동굴" => new Color(0.45f * v, 0.38f * v, 0.28f * v, 0.30f),
                "화산" => new Color(0.90f * v, 0.30f * v, 0.08f * v, 0.40f),
                "하늘" => new Color(0.85f * v, 0.90f * v, 1.0f * v, 0.18f),
                "심연" => new Color(0.40f * v, 0.18f * v, 0.60f * v, 0.30f),
                _ => new Color(0.35f, 0.35f, 0.35f, 0.20f)
            };
        }

        #endregion

        #region 배경 파티클 (스프라이트 기반)

        private void UpdateParticleSpawn()
        {
            if (!_hasBounds || string.IsNullOrEmpty(_currentZone)) return;

            _particleTimer += Time.deltaTime;
            if (_particleTimer < PARTICLE_SPAWN_INTERVAL) return;
            _particleTimer = 0f;

            if (_activeParticleCount >= MAX_PARTICLES) return;

            // 비활성 슬롯 찾기
            int slot = -1;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (_particles[i] == null || !_particles[i].gameObject.activeSelf)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return;

            SpawnParticle(slot);
        }

        private void SpawnParticle(int slot)
        {
            if (_particles[slot] == null)
            {
                var go = new GameObject($"BgParticle_{slot}");
                go.transform.SetParent(_particleRoot, false);
                _particles[slot] = go.AddComponent<SpriteRenderer>();
                _particles[slot].sortingOrder = -97;
            }

            var sr = _particles[slot];
            sr.sprite = GetOrCreateDecoSprite(_currentZone, 0);
            GetParticleSettings(_currentZone, out Color color, out float size, out Vector3 moveDir, out float lifetime);

            float rx = _arenaBounds.xMin + Random.value * _arenaBounds.width;
            float ry = _arenaBounds.yMin + Random.value * _arenaBounds.height;
            sr.transform.position = new Vector3(rx, ry, 7f);
            sr.transform.localScale = Vector3.one * size * 0.5f;
            sr.color = color;
            sr.gameObject.SetActive(true);
            _activeParticleCount++;

            Vector3 endPos = sr.transform.position + moveDir * lifetime;

            // DOTween 시퀀스: 이동 + 스케일 + 페이드
            var seq = DOTween.Sequence()
                .SetLink(sr.gameObject);

            seq.Append(sr.transform.DOMove(endPos, lifetime).SetEase(Ease.Linear));
            seq.Join(sr.transform.DOScale(Vector3.one * size, lifetime * 0.3f)
                .From(Vector3.one * size * 0.2f)
                .SetEase(Ease.OutQuad));

            // 마지막 30%에서 페이드아웃
            float fadeStart = lifetime * 0.7f;
            seq.Insert(fadeStart,
                ColorTweenHelper.To(sr, new Color(color.r, color.g, color.b, 0f),
                    lifetime * 0.3f)
                .SetLink(sr.gameObject));

            seq.OnComplete(() =>
            {
                sr.gameObject.SetActive(false);
                _activeParticleCount = Mathf.Max(0, _activeParticleCount - 1);
            });
        }

        private static void GetParticleSettings(string zone, out Color color, out float size, out Vector3 moveDir, out float lifetime)
        {
            switch (zone)
            {
                case "숲":
                    color = new Color(0.3f, 0.7f, 0.2f, 0.30f);
                    size = 0.35f;
                    moveDir = new Vector3(-0.3f, -1f, 0f); // 나뭇잎 떨어짐
                    lifetime = 3f;
                    break;
                case "동굴":
                    color = new Color(0.6f, 0.5f, 0.3f, 0.20f);
                    size = 0.18f;
                    moveDir = new Vector3(0.1f, 0.3f, 0f); // 먼지 부유
                    lifetime = 4f;
                    break;
                case "화산":
                    color = new Color(1f, 0.4f, 0.1f, 0.50f);
                    size = 0.25f;
                    moveDir = new Vector3(0.2f, 1.5f, 0f); // 불씨 상승
                    lifetime = 2f;
                    break;
                case "하늘":
                    color = new Color(0.85f, 0.9f, 1f, 0.15f);
                    size = 0.9f;
                    moveDir = new Vector3(1f, 0.1f, 0f); // 구름 이동
                    lifetime = 5f;
                    break;
                case "심연":
                    color = new Color(0.4f, 0.2f, 0.6f, 0.25f);
                    size = 0.7f;
                    moveDir = new Vector3(0.2f, -0.2f, 0f); // 안개 표류
                    lifetime = 4f;
                    break;
                default:
                    color = new Color(0.5f, 0.5f, 0.5f, 0.15f);
                    size = 0.3f;
                    moveDir = new Vector3(0f, -0.5f, 0f);
                    lifetime = 3f;
                    break;
            }
        }

        #endregion

        #region 패럴랙스

        private void UpdateParallax()
        {
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null) return;
                _prevCamPos = _mainCam.transform.position;
                return;
            }

            Vector3 camPos = _mainCam.transform.position;
            Vector3 delta = camPos - _prevCamPos;
            _prevCamPos = camPos;

            if (delta.sqrMagnitude < 0.0001f) return;

            // 전경 레이어 (바닥 패턴): 카메라에 가깝게 따라감
            if (_foregroundLayer != null)
                _foregroundLayer.position += new Vector3(delta.x * _foregroundFactor, delta.y * _foregroundFactor, 0f);

            // 배경 레이어 (장식물): 느리게 따라감
            if (_backgroundLayer != null)
                _backgroundLayer.position += new Vector3(delta.x * _backgroundFactor, delta.y * _backgroundFactor, 0f);
        }

        #endregion

        #region 아레나 테두리

        private void InitBorders()
        {
            var borderSprite = GetOrCreateBorderSprite();
            string[] names = { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" };

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(_borderRoot, false);
                _borders[i] = go.AddComponent<SpriteRenderer>();
                _borders[i].sprite = borderSprite;
                _borders[i].color = _borderColor;
                _borders[i].sortingOrder = -95;
                go.SetActive(false);
            }
        }

        private void UpdateBorderVisibility(bool show)
        {
            if (!_hasBounds || _borders[0] == null) return;

            float hw = _arenaBounds.width * 0.5f;
            float hh = _arenaBounds.height * 0.5f;
            float cx = _arenaBounds.center.x;
            float cy = _arenaBounds.center.y;

            // Top
            _borders[0].transform.position = new Vector3(cx, cy + hh, 5f);
            _borders[0].transform.localScale = new Vector3(_arenaBounds.width, _borderThickness, 1f);

            // Bottom
            _borders[1].transform.position = new Vector3(cx, cy - hh, 5f);
            _borders[1].transform.localScale = new Vector3(_arenaBounds.width, _borderThickness, 1f);

            // Left
            _borders[2].transform.position = new Vector3(cx - hw, cy, 5f);
            _borders[2].transform.localScale = new Vector3(_borderThickness, _arenaBounds.height, 1f);

            // Right
            _borders[3].transform.position = new Vector3(cx + hw, cy, 5f);
            _borders[3].transform.localScale = new Vector3(_borderThickness, _arenaBounds.height, 1f);

            for (int i = 0; i < 4; i++)
                _borders[i].gameObject.SetActive(show);
        }

        private static Sprite GetOrCreateBorderSprite()
        {
            const string key = "border";
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;

            int size = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 4f);
            _spriteCache[key] = sprite;
            return sprite;
        }

        #endregion

        #region 유틸리티

        private void RefreshArenaBounds()
        {
            var arenaMap = FindFirstObjectByType<ArenaMap>();
            if (arenaMap != null)
            {
                var rect = arenaMap.GetArenaBounds();
                _arenaBounds = rect;
                _hasBounds = true;
            }
            else
            {
                // 기본값 사용
                _arenaBounds = new Rect(-12f, -9f, 24f, 18f);
                _hasBounds = true;
            }
        }

        private static string GetChapterZoneName(int chapter)
        {
            int zone = ((chapter - 1) % 5) + 1;
            return zone switch
            {
                1 => "숲",
                2 => "동굴",
                3 => "화산",
                4 => "하늘",
                5 => "심연",
                _ => "일반"
            };
        }

        #endregion

        private void OnDestroy()
        {
            DOTween.Kill(_patternRoot);
            DOTween.Kill(_particleRoot);

            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (_particles[i] != null)
                    DOTween.Kill(_particles[i].gameObject);
            }

            if (Instance == this) Instance = null;
        }
    }
}
