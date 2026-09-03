using UnityEngine;
using DG.Tweening;

namespace MkLike.Combat
{
    /// <summary>
    /// 도트/픽셀 느낌의 사각형 타일 맵을 프로시져럴 텍스처로 생성.
    /// 단일 Texture2D에 타일 패턴을 그려 SpriteRenderer 하나로 렌더링한다.
    /// 챕터별/던전별 팔레트 전환을 지원한다.
    /// </summary>
    public class PixelMapGenerator : MonoBehaviour
    {
        public static PixelMapGenerator Instance { get; private set; }

        #region 설정

        [Header("맵 크기 (월드 유닛)")]
        [SerializeField] private float _mapWidth = 50f;
        [SerializeField] private float _mapHeight = 65f;

        [Header("타일 설정")]
        [Tooltip("한 타일의 월드 크기 (작을수록 촘촘)")]
        [SerializeField] private float _tileWorldSize = 0.08f;

        [Header("전환 설정")]
        [SerializeField] private float _transitionDuration = 1.2f;

        #endregion

        #region 내부 상태

        private SpriteRenderer _mapRenderer;
        private Texture2D _currentTexture;
        private Texture2D _targetTexture;
        private Texture2D _displayTexture;
        private int _texWidth;
        private int _texHeight;
        private float _transitionProgress;
        private bool _isTransitioning;
        private int _currentPaletteId = -1;

        // 타일별 패턴 시드 (텍스처 좌표 기반, 생성 시 고정)
        private float[] _patternSeeds;

        #endregion

        #region 팔레트 정의

        [System.Serializable]
        public struct MapPalette
        {
            public Color ground;
            public Color groundAlt;
            public Color accent1;
            public Color accent2;
            public Color border;
            public Color dark;
        }

        // ground: 메인 바닥, groundAlt: 바닥 변형, accent1: 짙은 패치, accent2: 포인트 장식
        // border: 가장자리, dark: 카메라 배경

        /// <summary>15개 챕터 바이옴 팔레트 + 던전/보스/아레나</summary>
        private static readonly MapPalette[] CHAPTER_PALETTES =
        {
            // Ch1 초원 (Meadow) — 밝은 초록, 꽃
            new() { ground = C(0.48f, 0.72f, 0.35f), groundAlt = C(0.44f, 0.68f, 0.32f),
                    accent1 = C(0.55f, 0.78f, 0.40f), accent2 = C(0.70f, 0.55f, 0.35f),
                    border = C(0.35f, 0.55f, 0.25f), dark = C(0.28f, 0.45f, 0.20f) },
            // Ch2 숲 (Forest) — 짙은 초록, 흙
            new() { ground = C(0.35f, 0.58f, 0.25f), groundAlt = C(0.32f, 0.52f, 0.22f),
                    accent1 = C(0.25f, 0.42f, 0.18f), accent2 = C(0.48f, 0.40f, 0.28f),
                    border = C(0.20f, 0.35f, 0.15f), dark = C(0.15f, 0.28f, 0.10f) },
            // Ch3 해안 (Beach) — 모래+수풀
            new() { ground = C(0.72f, 0.65f, 0.45f), groundAlt = C(0.68f, 0.60f, 0.42f),
                    accent1 = C(0.45f, 0.58f, 0.35f), accent2 = C(0.55f, 0.68f, 0.78f),
                    border = C(0.58f, 0.52f, 0.38f), dark = C(0.48f, 0.42f, 0.30f) },
            // Ch4 도시 (City) — 돌바닥+벽돌
            new() { ground = C(0.55f, 0.52f, 0.48f), groundAlt = C(0.50f, 0.48f, 0.44f),
                    accent1 = C(0.42f, 0.38f, 0.35f), accent2 = C(0.60f, 0.45f, 0.35f),
                    border = C(0.35f, 0.32f, 0.28f), dark = C(0.25f, 0.22f, 0.20f) },
            // Ch5 하수도 (Sewer) — 어두운 초록+오염
            new() { ground = C(0.28f, 0.35f, 0.25f), groundAlt = C(0.25f, 0.30f, 0.22f),
                    accent1 = C(0.20f, 0.28f, 0.18f), accent2 = C(0.35f, 0.42f, 0.30f),
                    border = C(0.15f, 0.20f, 0.12f), dark = C(0.10f, 0.14f, 0.08f) },
            // Ch6 사막 (Desert) — 황토+바위
            new() { ground = C(0.78f, 0.65f, 0.40f), groundAlt = C(0.72f, 0.60f, 0.38f),
                    accent1 = C(0.62f, 0.50f, 0.30f), accent2 = C(0.55f, 0.48f, 0.38f),
                    border = C(0.60f, 0.48f, 0.28f), dark = C(0.50f, 0.40f, 0.22f) },
            // Ch7 설원 (Snowfield) — 흰색+노출된 땅
            new() { ground = C(0.82f, 0.85f, 0.88f), groundAlt = C(0.76f, 0.80f, 0.84f),
                    accent1 = C(0.52f, 0.48f, 0.40f), accent2 = C(0.65f, 0.68f, 0.72f),
                    border = C(0.65f, 0.70f, 0.75f), dark = C(0.50f, 0.55f, 0.60f) },
            // Ch8 동굴 (Cave) — 돌바닥+광물
            new() { ground = C(0.42f, 0.38f, 0.32f), groundAlt = C(0.38f, 0.34f, 0.28f),
                    accent1 = C(0.30f, 0.27f, 0.22f), accent2 = C(0.50f, 0.48f, 0.42f),
                    border = C(0.22f, 0.20f, 0.16f), dark = C(0.15f, 0.13f, 0.10f) },
            // Ch9 구름 (Cloud) — 하늘+구름
            new() { ground = C(0.60f, 0.72f, 0.85f), groundAlt = C(0.55f, 0.68f, 0.82f),
                    accent1 = C(0.80f, 0.85f, 0.92f), accent2 = C(0.45f, 0.58f, 0.75f),
                    border = C(0.42f, 0.55f, 0.70f), dark = C(0.32f, 0.45f, 0.60f) },
            // Ch10 시계탑 (Clocktower) — 기계+금속
            new() { ground = C(0.45f, 0.40f, 0.35f), groundAlt = C(0.42f, 0.37f, 0.32f),
                    accent1 = C(0.55f, 0.50f, 0.40f), accent2 = C(0.65f, 0.55f, 0.30f),
                    border = C(0.30f, 0.28f, 0.24f), dark = C(0.22f, 0.20f, 0.18f) },
            // Ch11 마을 (Village) — 흙+풀 혼합
            new() { ground = C(0.50f, 0.55f, 0.35f), groundAlt = C(0.46f, 0.50f, 0.32f),
                    accent1 = C(0.55f, 0.45f, 0.30f), accent2 = C(0.38f, 0.48f, 0.28f),
                    border = C(0.38f, 0.40f, 0.25f), dark = C(0.28f, 0.30f, 0.18f) },
            // Ch12 화산 (Volcano) — 암석+용암
            new() { ground = C(0.28f, 0.20f, 0.18f), groundAlt = C(0.24f, 0.17f, 0.15f),
                    accent1 = C(0.16f, 0.10f, 0.08f), accent2 = C(0.75f, 0.30f, 0.05f),
                    border = C(0.12f, 0.08f, 0.06f), dark = C(0.08f, 0.05f, 0.04f) },
            // Ch13 늪 (Swamp) — 어두운 초록+보라
            new() { ground = C(0.25f, 0.35f, 0.22f), groundAlt = C(0.22f, 0.30f, 0.20f),
                    accent1 = C(0.30f, 0.25f, 0.35f), accent2 = C(0.18f, 0.28f, 0.18f),
                    border = C(0.15f, 0.22f, 0.14f), dark = C(0.10f, 0.15f, 0.10f) },
            // Ch14 결정동굴 (Crystal) — 보라+결정 빛
            new() { ground = C(0.28f, 0.20f, 0.38f), groundAlt = C(0.25f, 0.18f, 0.34f),
                    accent1 = C(0.18f, 0.12f, 0.28f), accent2 = C(0.55f, 0.35f, 0.70f),
                    border = C(0.15f, 0.10f, 0.22f), dark = C(0.10f, 0.06f, 0.16f) },
            // Ch15 황금사원 (Golden Temple) — 금+붉은색
            new() { ground = C(0.62f, 0.50f, 0.28f), groundAlt = C(0.58f, 0.46f, 0.25f),
                    accent1 = C(0.72f, 0.58f, 0.22f), accent2 = C(0.55f, 0.22f, 0.15f),
                    border = C(0.48f, 0.38f, 0.18f), dark = C(0.38f, 0.28f, 0.12f) },
        };

        // Color 단축 생성
        private static Color C(float r, float g, float b) => new(r, g, b);

        // 던전 유형별 팔레트 (5종)
        private static readonly MapPalette[] DUNGEON_PALETTES =
        {
            // Weapon — 어두운 철제 바닥 + 무기 광택
            new() { ground = C(0.25f, 0.23f, 0.28f), groundAlt = C(0.22f, 0.20f, 0.24f),
                    accent1 = C(0.35f, 0.32f, 0.38f), accent2 = C(0.50f, 0.45f, 0.55f),
                    border = C(0.14f, 0.12f, 0.16f), dark = C(0.08f, 0.07f, 0.10f) },
            // Experience — 고대 수련장 (황토+이끼)
            new() { ground = C(0.30f, 0.28f, 0.20f), groundAlt = C(0.26f, 0.24f, 0.18f),
                    accent1 = C(0.22f, 0.30f, 0.18f), accent2 = C(0.40f, 0.35f, 0.20f),
                    border = C(0.18f, 0.16f, 0.12f), dark = C(0.10f, 0.09f, 0.06f) },
            // Equipment — 사냥터 (어두운 초록)
            new() { ground = C(0.18f, 0.28f, 0.16f), groundAlt = C(0.15f, 0.24f, 0.14f),
                    accent1 = C(0.12f, 0.20f, 0.10f), accent2 = C(0.28f, 0.22f, 0.15f),
                    border = C(0.08f, 0.16f, 0.08f), dark = C(0.05f, 0.10f, 0.05f) },
            // Climber — 시련의 탑 (회색 돌+푸른 빛)
            new() { ground = C(0.32f, 0.33f, 0.38f), groundAlt = C(0.28f, 0.29f, 0.34f),
                    accent1 = C(0.22f, 0.25f, 0.32f), accent2 = C(0.35f, 0.42f, 0.55f),
                    border = C(0.18f, 0.19f, 0.24f), dark = C(0.10f, 0.11f, 0.15f) },
            // Enhancement — 마법 연구소 (보라+횃불)
            new() { ground = C(0.26f, 0.20f, 0.32f), groundAlt = C(0.22f, 0.17f, 0.28f),
                    accent1 = C(0.18f, 0.12f, 0.25f), accent2 = C(0.50f, 0.35f, 0.18f),
                    border = C(0.14f, 0.10f, 0.20f), dark = C(0.08f, 0.06f, 0.12f) },
        };

        // 기본 던전 팔레트 (fallback)
        private static readonly MapPalette PALETTE_DUNGEON = DUNGEON_PALETTES[0];

        // 보스 — 붉은 대지 + 균열 + 화염
        private static readonly MapPalette PALETTE_BOSS = new()
        {
            ground    = new Color(0.32f, 0.14f, 0.10f),
            groundAlt = new Color(0.28f, 0.12f, 0.08f),
            accent1   = new Color(0.18f, 0.06f, 0.05f),  // 검은 균열
            accent2   = new Color(0.70f, 0.25f, 0.05f),  // 화염 빛
            border    = new Color(0.14f, 0.06f, 0.04f),
            dark      = new Color(0.08f, 0.03f, 0.02f),
        };

        // 아레나 — 모래 바닥 + 자갈 + 금빛
        private static readonly MapPalette PALETTE_ARENA = new()
        {
            ground    = new Color(0.58f, 0.50f, 0.35f),
            groundAlt = new Color(0.54f, 0.46f, 0.32f),
            accent1   = new Color(0.42f, 0.36f, 0.25f),  // 다진 흙
            accent2   = new Color(0.65f, 0.58f, 0.38f),  // 밝은 모래
            border    = new Color(0.35f, 0.30f, 0.20f),
            dark      = new Color(0.25f, 0.22f, 0.15f),
        };

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateMapRenderer();
            GenerateSeeds();
        }

        private void Start()
        {
            ApplyChapterImmediate(1);
        }

        private void Update()
        {
            if (!_isTransitioning) return;

            _transitionProgress += Time.deltaTime / _transitionDuration;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                _isTransitioning = false;
            }

            float t = Mathf.SmoothStep(0f, 1f, _transitionProgress);
            BlendTextures(t);

            if (!_isTransitioning)
            {
                // 전환 완료 — current를 target으로 교체
                CopyTexture(_targetTexture, _currentTexture);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 초기화

        private void CreateMapRenderer()
        {
            // 텍스처 해상도: 타일 수 = 맵크기 / 타일월드크기
            _texWidth = Mathf.CeilToInt(_mapWidth / _tileWorldSize);
            _texHeight = Mathf.CeilToInt(_mapHeight / _tileWorldSize);
            _texWidthStatic = _texWidth;
            _texHeightStatic = _texHeight;

            // 텍스처 생성
            _currentTexture = CreateTexture(_texWidth, _texHeight);
            _targetTexture = CreateTexture(_texWidth, _texHeight);
            _displayTexture = CreateTexture(_texWidth, _texHeight);

            // SpriteRenderer로 표시
            var go = new GameObject("PixelMap");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);

            _mapRenderer = go.AddComponent<SpriteRenderer>();
            _mapRenderer.sortingOrder = -90;

            // Sprite 생성: pixelsPerUnit = texWidth / mapWidth
            float ppu = _texWidth / _mapWidth;
            var sprite = Sprite.Create(
                _displayTexture,
                new Rect(0, 0, _texWidth, _texHeight),
                Vector2.one * 0.5f,
                ppu
            );
            _mapRenderer.sprite = sprite;

            Debug.Log($"[PixelMapGenerator] 텍스처 맵 생성: {_texWidth}x{_texHeight} ({_texWidth * _texHeight}타일), " +
                      $"월드 크기: {_mapWidth}x{_mapHeight}, 타일크기: {_tileWorldSize}");
        }

        private void GenerateSeeds()
        {
            int count = _texWidth * _texHeight;
            _patternSeeds = new float[count];
            // 결정론적 시드로 항상 같은 패턴
            var rng = new System.Random(42);
            for (int i = 0; i < count; i++)
                _patternSeeds[i] = (float)rng.NextDouble();
        }

        private static Texture2D CreateTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // 도트 느낌 핵심
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        #endregion

        #region 맵 전환 API

        public void TransitionToChapter(int chapter, float duration = -1f)
        {
            TransitionToChapter(chapter, 1, duration);
        }

        /// <summary>챕터+스테이지별 전환 (스테이지 진행에 따라 미세 어둡게)</summary>
        public void TransitionToChapter(int chapter, int stageIndex, float duration = -1f)
        {
            int paletteId = chapter * 100 + stageIndex;
            if (paletteId == _currentPaletteId) return;
            _currentPaletteId = paletteId;

            var palette = GetChapterPalette(chapter);

            // 스테이지 진행에 따라 미세 변형 (1~9: 점점 살짝 어둡게)
            if (stageIndex > 1)
            {
                float stageShift = (stageIndex - 1) * 0.012f;
                palette.ground = Color.Lerp(palette.ground, palette.border, stageShift);
                palette.groundAlt = Color.Lerp(palette.groundAlt, palette.border, stageShift);
            }

            PaintTexture(_targetTexture, palette);
            StartTransition(duration > 0 ? duration : _transitionDuration);
            SyncCameraBackground(palette.dark);
        }

        public void TransitionToDungeon(float duration = -1f)
        {
            TransitionToDungeon(0, duration);
        }

        /// <summary>던전 유형별 팔레트로 전환 (0=Weapon, 1=Exp, 2=Equip, 3=Climber, 4=Enhancement)</summary>
        public void TransitionToDungeon(int dungeonTypeIndex, float duration = -1f)
        {
            _currentPaletteId = 100 + dungeonTypeIndex;
            int idx = Mathf.Clamp(dungeonTypeIndex, 0, DUNGEON_PALETTES.Length - 1);
            var palette = DUNGEON_PALETTES[idx];
            PaintTexture(_targetTexture, palette);
            StartTransition(duration > 0 ? duration : _transitionDuration * 0.5f);
            SyncCameraBackground(palette.dark);
        }

        public void TransitionToBoss(float duration = -1f)
        {
            _currentPaletteId = 200;
            PaintTexture(_targetTexture, PALETTE_BOSS);
            StartTransition(duration > 0 ? duration : _transitionDuration * 0.5f);
            SyncCameraBackground(PALETTE_BOSS.dark);
        }

        public void TransitionToArena(float duration = -1f)
        {
            _currentPaletteId = 300;
            PaintTexture(_targetTexture, PALETTE_ARENA);
            StartTransition(duration > 0 ? duration : _transitionDuration * 0.5f);
            SyncCameraBackground(PALETTE_ARENA.dark);
        }

        public void ApplyChapterImmediate(int chapter)
        {
            _currentPaletteId = chapter;
            var palette = GetChapterPalette(chapter);
            PaintTexture(_currentTexture, palette);
            CopyTexture(_currentTexture, _displayTexture);
            _displayTexture.Apply();
            _isTransitioning = false;
            SyncCameraBackground(palette.dark);
        }

        #endregion

        #region 텍스처 페인팅

        private void PaintTexture(Texture2D tex, MapPalette palette)
        {
            var pixels = tex.GetPixels32();
            int borderWidth = Mathf.Max(3, _texWidth / 30);

            for (int y = 0; y < _texHeight; y++)
            {
                for (int x = 0; x < _texWidth; x++)
                {
                    int idx = y * _texWidth + x;
                    float seed = _patternSeeds[idx];

                    // 가장자리 판정
                    bool isOuterEdge = x < 2 || x >= _texWidth - 2 || y < 2 || y >= _texHeight - 2;
                    bool isBorder = x < borderWidth || x >= _texWidth - borderWidth ||
                                    y < borderWidth || y >= _texHeight - borderWidth;

                    Color c;
                    if (isOuterEdge)
                    {
                        c = palette.dark;
                    }
                    else if (isBorder)
                    {
                        // 테두리: 4x4 브릭 패턴
                        bool brickLine = (y % 4 == 0) || (x % 4 == 0 && (y / 4) % 2 == 0) ||
                                         ((x + 2) % 4 == 0 && (y / 4) % 2 == 1);
                        c = brickLine ? palette.dark : palette.border;
                    }
                    else
                    {
                        c = PaintTilePattern(x, y, seed, palette);
                    }

                    pixels[idx] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
        }

        /// <summary>
        /// 평야 스타일 바닥 지형 패턴.
        /// 입체 오브젝트 없이 색감만으로 지형 표현.
        /// - 베이스: 깔끔한 바닥 (대각선 해칭 질감)
        /// - 패치: Perlin 노이즈로 유기적 짙은 풀/흙/돌 패치
        /// - 포인트: 드문 꽃/돌/균열 장식
        /// </summary>
        private static Color PaintTilePattern(int x, int y, float seed, MapPalette palette)
        {
            // === 1. 베이스: 저주파 Perlin으로 부드러운 ground↔groundAlt ===
            float baseNoise = Mathf.PerlinNoise(x * 0.008f + 10f, y * 0.008f + 10f);
            Color c = Color.Lerp(palette.ground, palette.groundAlt, baseNoise);

            // === 2. 짙은 풀/패치 — 유기적 군집 ===
            float patchNoise = Mathf.PerlinNoise(x * 0.02f + 30f, y * 0.02f + 30f);
            if (patchNoise > 0.55f)
            {
                float blend = Mathf.InverseLerp(0.55f, 0.78f, patchNoise);
                c = Color.Lerp(c, palette.accent1, blend * 0.6f);
            }

            // === 3. 흙/돌 패치 ===
            float dirtNoise = Mathf.PerlinNoise(x * 0.025f + 200f, y * 0.025f + 200f);
            if (dirtNoise > 0.62f)
            {
                float blend = Mathf.InverseLerp(0.62f, 0.82f, dirtNoise);
                c = Color.Lerp(c, palette.accent2, blend * 0.5f);
            }

            // === 4. 부드러운 질감 ===
            float detailNoise = Mathf.PerlinNoise(x * 0.045f + 500f, y * 0.045f + 500f);
            c = Color.Lerp(c, palette.groundAlt, (detailNoise - 0.5f) * 0.1f);

            // === 5. 다양한 형태/크기의 장식 클러스터 ===
            c = ApplyDecoCluster(x, y, c, palette);

            // === 6. 가장자리 ===
            int edgeDist = EdgeDistance(x, y);
            if (edgeDist < 20)
            {
                float edgeBlend = 1f - (edgeDist / 20f);
                c = Color.Lerp(c, palette.border, edgeBlend * 0.5f);
            }

            // === 7. 미세 변형 ===
            float v = (seed - 0.5f) * 0.006f;
            c.r = Mathf.Clamp01(c.r + v);
            c.g = Mathf.Clamp01(c.g + v);
            c.b = Mathf.Clamp01(c.b + v);

            return c;
        }

        /// <summary>
        /// 멀티픽셀 장식 클러스터.
        /// 셀(16x16) 내에서 해시로 형태/크기/색을 결정.
        /// 풀 덩어리, 돌무더기, 꽃 패치, 흙 자국 등 7종.
        /// </summary>
        private static Color ApplyDecoCluster(int x, int y, Color baseColor, MapPalette palette)
        {
            const int CELL = 16;
            int cellX = x / CELL;
            int cellY = y / CELL;
            int localX = x % CELL;
            int localY = y % CELL;

            // 셀별 해시 → 장식 유형/위치/크기 결정
            int h = (cellX * 73 + cellY * 137 + (cellX ^ cellY) * 53) & 0xFFF;
            int decoType = h % 30; // 0~29
            int cx = (h >> 3) % 8 + 4;  // 중심X (4~11)
            int cy = (h >> 6) % 8 + 4;  // 중심Y (4~11)
            int dx = localX - cx;
            int dy = localY - cy;

            switch (decoType)
            {
                case 0: case 1: case 2:
                    // 풀 덩어리 (불규칙 5~7px 블롭)
                    if (IsInBlob(dx, dy, h, 3))
                        return Color.Lerp(baseColor, palette.accent1, 0.55f);
                    break;

                case 3: case 4:
                    // 큰 풀 패치 (넓은 영역 8~10px)
                    if (IsInBlob(dx, dy, h, 5))
                        return Color.Lerp(baseColor, palette.accent1, 0.40f);
                    break;

                case 5: case 6:
                    // 돌무더기 (컴팩트 3~4px)
                    if (dx * dx + dy * dy <= 4)
                        return Color.Lerp(baseColor, palette.accent2, 0.65f);
                    break;

                case 7:
                    // 큰 바위 (6px 원)
                    if (dx * dx + dy * dy <= 9)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float shade = dist / 3f;
                        return Color.Lerp(palette.accent2, palette.border, shade * 0.3f);
                    }
                    break;

                case 8: case 9:
                    // 꽃/장식 3점 패턴
                    if ((dx == 0 && dy == 0) || (dx == 2 && dy == -1) || (dx == -1 && dy == 2))
                    {
                        Color flower = Color.Lerp(palette.accent1, palette.ground, 0.3f);
                        flower.r += 0.08f; // 약간 따뜻하게
                        return flower;
                    }
                    break;

                case 10:
                    // 풀 줄기 (세로 3px 라인)
                    if (dx == 0 && dy >= -1 && dy <= 1)
                        return Color.Lerp(baseColor, palette.accent1, 0.5f);
                    // 옆에 짧은 줄기
                    if (dx == 2 && dy >= 0 && dy <= 1)
                        return Color.Lerp(baseColor, palette.accent1, 0.4f);
                    break;

                case 11:
                    // 흙 자국 (가로 길쭉 5x2)
                    if (Mathf.Abs(dx) <= 2 && Mathf.Abs(dy) <= 1)
                        return Color.Lerp(baseColor, palette.accent2, 0.45f);
                    break;

                case 12:
                    // 풀+돌 혼합 클러스터
                    if (dx * dx + dy * dy <= 6)
                    {
                        if ((dx + dy) % 2 == 0)
                            return Color.Lerp(baseColor, palette.accent1, 0.45f);
                        else
                            return Color.Lerp(baseColor, palette.accent2, 0.35f);
                    }
                    break;

                // 13~29: 빈 셀 (약 57%) — 장식 없이 깨끗한 바닥
            }

            return baseColor;
        }

        /// <summary>불규칙 블롭 형태 판정 (해시로 변형)</summary>
        private static bool IsInBlob(int dx, int dy, int hash, int radius)
        {
            // 기본 원 + 해시로 각 방향 오프셋
            float rx = radius + ((hash >> 8) % 3 - 1) * 0.5f;
            float ry = radius + ((hash >> 10) % 3 - 1) * 0.5f;
            float dist = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
            return dist <= 1f;
        }

        /// <summary>맵 가장자리까지의 거리 (px)</summary>
        private static int EdgeDistance(int x, int y)
        {
            int dLeft = x;
            int dRight = _texWidthStatic - 1 - x;
            int dBottom = y;
            int dTop = _texHeightStatic - 1 - y;
            return Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBottom, dTop));
        }

        private static int _texWidthStatic;
        private static int _texHeightStatic;

        private void BlendTextures(float t)
        {
            var current = _currentTexture.GetPixels32();
            var target = _targetTexture.GetPixels32();
            var display = _displayTexture.GetPixels32();

            for (int i = 0; i < display.Length; i++)
            {
                display[i].r = (byte)Mathf.Lerp(current[i].r, target[i].r, t);
                display[i].g = (byte)Mathf.Lerp(current[i].g, target[i].g, t);
                display[i].b = (byte)Mathf.Lerp(current[i].b, target[i].b, t);
                display[i].a = 255;
            }

            _displayTexture.SetPixels32(display);
            _displayTexture.Apply();
        }

        private static void CopyTexture(Texture2D src, Texture2D dst)
        {
            var pixels = src.GetPixels32();
            dst.SetPixels32(pixels);
            dst.Apply();
        }

        private void StartTransition(float duration)
        {
            _transitionDuration = duration;
            _transitionProgress = 0f;
            _isTransitioning = true;
        }

        #endregion

        #region 유틸

        private static void SyncCameraBackground(Color darkColor)
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = darkColor;
        }

        private static MapPalette GetChapterPalette(int chapter)
        {
            // 15챕터 순환 (16챕터부터는 1~15 반복 + 점진적 어둡게)
            int idx = ((chapter - 1) % CHAPTER_PALETTES.Length);
            var palette = CHAPTER_PALETTES[idx];

            // 2주기 이상이면 점진적 어둡게
            int cycle = (chapter - 1) / CHAPTER_PALETTES.Length;
            if (cycle > 0)
            {
                float darken = Mathf.Clamp01(cycle * 0.08f);
                palette.ground = Color.Lerp(palette.ground, Color.black, darken);
                palette.groundAlt = Color.Lerp(palette.groundAlt, Color.black, darken);
                palette.accent1 = Color.Lerp(palette.accent1, Color.black, darken * 0.6f);
                palette.accent2 = Color.Lerp(palette.accent2, Color.black, darken * 0.4f);
                palette.border = Color.Lerp(palette.border, Color.black, darken);
                palette.dark = Color.Lerp(palette.dark, Color.black, darken);
            }

            return palette;
        }

        #endregion
    }
}
