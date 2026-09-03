using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 존/챕터/던전/아레나별 배경 비주얼 관리.
    /// 챕터별 그라데이션 배경, 던전/아레나/보스전 배경 전환, 파티클 분위기 연출.
    /// StageChangedEvent, TowerFloorReachedEvent, ArenaMatchEvent 구독.
    /// </summary>
    public class ZoneVisualManager : MonoBehaviour
    {
        public static ZoneVisualManager Instance { get; private set; }

        #region 챕터별 배경 색상

        [Header("챕터별 배경 (하단→상단 그라데이션)")]
        [SerializeField] private Color _ch1Bottom = new Color(0.08f, 0.18f, 0.08f);  // 숲 — 어두운 녹색
        [SerializeField] private Color _ch1Top = new Color(0.15f, 0.30f, 0.12f);     // 숲 — 밝은 녹색
        [SerializeField] private Color _ch2Bottom = new Color(0.12f, 0.08f, 0.05f);  // 동굴 — 어두운 갈색
        [SerializeField] private Color _ch2Top = new Color(0.22f, 0.15f, 0.08f);     // 동굴 — 밝은 갈색
        [SerializeField] private Color _ch3Bottom = new Color(0.25f, 0.05f, 0.02f);  // 화산 — 어두운 붉은색
        [SerializeField] private Color _ch3Top = new Color(0.40f, 0.15f, 0.05f);     // 화산 — 밝은 붉은색
        [SerializeField] private Color _ch4Bottom = new Color(0.10f, 0.18f, 0.30f);  // 하늘 — 어두운 하늘색
        [SerializeField] private Color _ch4Top = new Color(0.20f, 0.35f, 0.55f);     // 하늘 — 밝은 하늘색
        [SerializeField] private Color _ch5Bottom = new Color(0.15f, 0.05f, 0.22f);  // 심연 — 어두운 보라
        [SerializeField] private Color _ch5Top = new Color(0.25f, 0.10f, 0.35f);     // 심연 — 밝은 보라

        #endregion

        #region 특수 모드 배경

        [Header("특수 모드 배경")]
        [SerializeField] private Color _dungeonBottom = new Color(0.05f, 0.05f, 0.08f);  // 던전 — 매우 어두운 톤
        [SerializeField] private Color _dungeonTop = new Color(0.10f, 0.08f, 0.15f);
        [SerializeField] private Color _arenaBottom = new Color(0.08f, 0.05f, 0.12f);    // 아레나 — 보라빛
        [SerializeField] private Color _arenaTop = new Color(0.18f, 0.10f, 0.25f);
        [SerializeField] private Color _bossBottom = new Color(0.20f, 0.02f, 0.02f);     // 보스전 — 붉은 톤
        [SerializeField] private Color _bossTop = new Color(0.35f, 0.08f, 0.05f);

        #endregion

        #region 타워 존 배경 (기존 호환)

        [Header("타워 존 배경")]
        [SerializeField] private Color _normalZoneColor = new Color(0.15f, 0.18f, 0.25f);
        [SerializeField] private Color _eliteZoneColor = new Color(0.1f, 0.2f, 0.15f);
        [SerializeField] private Color _abyssZoneColor = new Color(0.2f, 0.1f, 0.25f);
        [SerializeField] private Color _infiniteZoneColor = new Color(0.25f, 0.1f, 0.1f);

        #endregion

        [Header("전환 설정")]
        [SerializeField] private float _transitionDuration = 1.5f;
        [SerializeField] private float _fastTransitionDuration = 0.5f;

        [Header("분위기 파티클")]
        [SerializeField] private ParticleSystem _ambientParticles;

        [Header("그라데이션 배경")]
        [SerializeField] private SpriteRenderer _bgTop;
        [SerializeField] private SpriteRenderer _bgBottom;

        private string _currentZone = "";
        private int _currentChapter;
        private Tween _bgCamTween;
        private Tween _bgTopTween;
        private Tween _bgBottomTween;
        private bool _isSpecialMode; // 던전/아레나 진입 시 true

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureGradientBackground();
        }

        private void OnEnable()
        {
            EventBus<TowerFloorReachedEvent>.Subscribe(OnFloorReached);
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<ArenaMatchEvent>(OnArenaMatchEnd);
        }

        private void OnDisable()
        {
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnFloorReached);
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<ArenaMatchEvent>(OnArenaMatchEnd);
        }

        #region 이벤트 핸들러

        private void OnStageChanged(StageChangedEvent evt)
        {
            if (_isSpecialMode) return;

            _currentChapter = evt.Chapter;

            // 보스 스테이지이면 보스 배경
            if (evt.DisplayName != null && evt.DisplayName.Contains("BOSS"))
            {
                TransitionGradient(_bossBottom, _bossTop, _transitionDuration);
                UpdateAmbientParticles("보스");
                // 픽셀맵 보스 전환
                PixelMapGenerator.Instance?.TransitionToBoss(_transitionDuration);
                return;
            }

            // 챕터별 배경 전환
            GetChapterColors(evt.Chapter, out Color bottom, out Color top);
            TransitionGradient(bottom, top, _transitionDuration);
            string zoneName = GetChapterZoneName(evt.Chapter);
            UpdateAmbientParticles(zoneName);

            // 픽셀맵 챕터+스테이지 전환
            PixelMapGenerator.Instance?.TransitionToChapter(evt.Chapter, evt.StageIndex, _transitionDuration);

            // 프로시져럴 배경 동기화
            if (ProceduralBackground.Instance != null)
                ProceduralBackground.Instance.SetZone(zoneName);
        }

        private void OnFloorReached(TowerFloorReachedEvent e)
        {
            if (e.ZoneName == _currentZone) return;
            _currentZone = e.ZoneName;

            Color targetColor = _currentZone switch
            {
                "엘리트" => _eliteZoneColor,
                "심연" => _abyssZoneColor,
                "무한" => _infiniteZoneColor,
                _ => _normalZoneColor
            };

            // 타워 존은 Camera.backgroundColor 사용 (기존 호환)
            TransitionCameraColor(targetColor, _transitionDuration);
            UpdateAmbientParticles(_currentZone);
        }

        /// <summary>
        /// 아레나 매치 결과 이벤트 — 아레나 종료 후 원래 배경으로 복귀.
        /// 아레나 진입은 EnterArena()를 직접 호출해야 한다.
        /// </summary>
        private void OnArenaMatchEnd(ArenaMatchEvent evt)
        {
            if (!_isSpecialMode) return;

            _isSpecialMode = false;
            GetChapterColors(_currentChapter, out Color bottom, out Color top);
            TransitionGradient(bottom, top, _fastTransitionDuration);
            UpdateAmbientParticles(GetChapterZoneName(_currentChapter));
        }

        #endregion

        #region 외부 호출 API

        /// <summary>
        /// 던전 진입 시 외부에서 호출. 어두운 톤 배경 전환.
        /// </summary>
        public void EnterDungeon(int dungeonTypeIndex = 0)
        {
            _isSpecialMode = true;
            TransitionGradient(_dungeonBottom, _dungeonTop, _fastTransitionDuration);
            UpdateAmbientParticles("던전");
            // 픽셀맵 던전 유형별 전환
            PixelMapGenerator.Instance?.TransitionToDungeon(dungeonTypeIndex, _fastTransitionDuration);
        }

        /// <summary>
        /// 던전 퇴장 시 외부에서 호출. 원래 챕터 배경 복귀.
        /// </summary>
        public void ExitDungeon()
        {
            _isSpecialMode = false;
            GetChapterColors(_currentChapter, out Color bottom, out Color top);
            TransitionGradient(bottom, top, _fastTransitionDuration);
            UpdateAmbientParticles(GetChapterZoneName(_currentChapter));
            // 픽셀맵 챕터 복귀
            PixelMapGenerator.Instance?.TransitionToChapter(_currentChapter, _fastTransitionDuration);
        }

        /// <summary>
        /// 아레나 진입 시 외부에서 호출.
        /// </summary>
        public void EnterArena()
        {
            _isSpecialMode = true;
            TransitionGradient(_arenaBottom, _arenaTop, _fastTransitionDuration);
            UpdateAmbientParticles("아레나");
            PixelMapGenerator.Instance?.TransitionToArena(_fastTransitionDuration);
        }

        /// <summary>
        /// 아레나 퇴장 시 외부에서 호출.
        /// </summary>
        public void ExitArena()
        {
            _isSpecialMode = false;
            GetChapterColors(_currentChapter, out Color bottom, out Color top);
            TransitionGradient(bottom, top, _fastTransitionDuration);
            UpdateAmbientParticles(GetChapterZoneName(_currentChapter));
            PixelMapGenerator.Instance?.TransitionToChapter(_currentChapter, _fastTransitionDuration);
        }

        /// <summary>
        /// 보스전 배경으로 전환.
        /// </summary>
        public void EnterBossFight()
        {
            TransitionGradient(_bossBottom, _bossTop, _fastTransitionDuration);
            UpdateAmbientParticles("보스");
        }

        #endregion

        #region 그라데이션 배경 시스템

        /// <summary>
        /// SpriteRenderer 2개(상단/하단)로 그라데이션 배경을 구성한다.
        /// 없으면 자동 생성. 각 스프라이트는 그라데이션 텍스처 사용.
        /// </summary>
        private void EnsureGradientBackground()
        {
            // PixelMapGenerator가 있으면 그라디언트 배경 비활성화 (타일맵이 배경 담당)
            bool hasPixelMap = PixelMapGenerator.Instance != null ||
                               GetComponent<PixelMapGenerator>() != null;

            if (hasPixelMap)
            {
                // 기존 그라디언트 배경이 있으면 비활성화
                if (_bgBottom != null) _bgBottom.gameObject.SetActive(false);
                if (_bgTop != null) _bgTop.gameObject.SetActive(false);

                // 카메라 배경을 어두운 색으로 (타일 밖이 보일 경우 대비)
                var cam = Camera.main;
                if (cam != null)
                    cam.backgroundColor = new Color(0.05f, 0.08f, 0.04f);
                return;
            }

            if (_bgBottom != null && _bgTop != null) return;

            if (_bgBottom == null)
            {
                var go = new GameObject("BG_Bottom");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -5f, 10f);
                go.transform.localScale = new Vector3(30f, 12f, 1f);
                _bgBottom = go.AddComponent<SpriteRenderer>();
                _bgBottom.sprite = CreateGradientSprite(_ch1Bottom, Color.Lerp(_ch1Bottom, _ch1Top, 0.5f));
                _bgBottom.sortingOrder = -100;
                _bgBottom.color = Color.white;
            }

            if (_bgTop == null)
            {
                var go = new GameObject("BG_Top");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 5f, 10f);
                go.transform.localScale = new Vector3(30f, 12f, 1f);
                _bgTop = go.AddComponent<SpriteRenderer>();
                _bgTop.sprite = CreateGradientSprite(Color.Lerp(_ch1Bottom, _ch1Top, 0.5f), _ch1Top);
                _bgTop.sortingOrder = -100;
                _bgTop.color = Color.white;
            }

            // 카메라 배경도 어둡게
            var cam2 = Camera.main;
            if (cam2 != null)
                cam2.backgroundColor = _ch1Bottom;
        }

        private void TransitionGradient(Color bottom, Color top, float duration)
        {
            _bgBottomTween?.Kill();
            _bgTopTween?.Kill();
            _bgCamTween?.Kill();

            Color midColor = Color.Lerp(bottom, top, 0.5f);

            if (_bgBottom != null)
            {
                // 새 그라데이션 텍스처 생성 후 적용
                _bgBottom.sprite = CreateGradientSprite(bottom, midColor);
                _bgBottom.color = Color.white;
            }

            if (_bgTop != null)
            {
                _bgTop.sprite = CreateGradientSprite(midColor, top);
                _bgTop.color = Color.white;
            }

            // Camera.backgroundColor도 하단 색상에 맞춤 (배경 누출 방지)
            var cam = Camera.main;
            if (cam != null)
            {
                _bgCamTween = DOTween.To(() => cam.backgroundColor, c => cam.backgroundColor = c, bottom, duration)
                    .SetEase(Ease.InOutSine)
                    .SetLink(gameObject);
            }
        }

        private void TransitionCameraColor(Color color, float duration)
        {
            _bgCamTween?.Kill();
            var cam = Camera.main;
            if (cam == null) return;

            _bgCamTween = DOTween.To(() => cam.backgroundColor, c => cam.backgroundColor = c, color, duration)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);
        }

        #endregion

        #region 챕터 색상 매핑

        private void GetChapterColors(int chapter, out Color bottom, out Color top)
        {
            // ChapterMonsterConfig SO에서 배경색 로드 (우선)
            var configs = Resources.LoadAll<Data.ChapterMonsterConfigSO>("Data/Chapters");
            if (configs != null)
            {
                // 정확히 일치하는 챕터 찾기
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i].ChapterNumber == chapter)
                    {
                        bottom = configs[i].BgBottom;
                        top = configs[i].BgTop;
                        ApplyLateChapterDarken(chapter, ref bottom, ref top);
                        return;
                    }
                }

                // 순환: 8챕터 이후 → 모듈러 매핑
                if (configs.Length > 0)
                {
                    int wrapped = ((chapter - 1) % configs.Length) + 1;
                    for (int i = 0; i < configs.Length; i++)
                    {
                        if (configs[i].ChapterNumber == wrapped)
                        {
                            bottom = configs[i].BgBottom;
                            top = configs[i].BgTop;
                            ApplyLateChapterDarken(chapter, ref bottom, ref top);
                            return;
                        }
                    }
                }
            }

            // fallback: 기존 5챕터 순환
            int zone = ((chapter - 1) % 5) + 1;
            switch (zone)
            {
                case 1:  bottom = _ch1Bottom; top = _ch1Top; break;
                case 2:  bottom = _ch2Bottom; top = _ch2Top; break;
                case 3:  bottom = _ch3Bottom; top = _ch3Top; break;
                case 4:  bottom = _ch4Bottom; top = _ch4Top; break;
                case 5:  bottom = _ch5Bottom; top = _ch5Top; break;
                default: bottom = _ch1Bottom; top = _ch1Top; break;
            }
            ApplyLateChapterDarken(chapter, ref bottom, ref top);
        }

        private static void ApplyLateChapterDarken(int chapter, ref Color bottom, ref Color top)
        {
            if (chapter > 8)
            {
                float darken = Mathf.Clamp01((chapter - 8) * 0.04f);
                bottom = Color.Lerp(bottom, Color.black, darken);
                top = Color.Lerp(top, Color.black, darken * 0.5f);
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

        #region 파티클 분위기

        private void UpdateAmbientParticles(string zone)
        {
            if (_ambientParticles == null) return;

            var main = _ambientParticles.main;
            var emission = _ambientParticles.emission;

            switch (zone)
            {
                case "숲":
                    // 나뭇잎: 느리게 떨어지는 녹색 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                    main.startColor = new Color(0.3f, 0.7f, 0.2f, 0.3f);
                    main.startLifetime = 3f;
                    emission.rateOverTime = 5f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "동굴":
                    // 먼지: 느린 작은 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                    main.startColor = new Color(0.6f, 0.5f, 0.3f, 0.2f);
                    main.startLifetime = 5f;
                    emission.rateOverTime = 3f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "화산":
                    // 불씨: 빠른 작은 붉은 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                    main.startColor = new Color(1f, 0.4f, 0.1f, 0.6f);
                    main.startLifetime = 1.5f;
                    emission.rateOverTime = 20f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "하늘":
                    // 구름 조각: 흰색 큰 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
                    main.startColor = new Color(0.8f, 0.85f, 1f, 0.15f);
                    main.startLifetime = 4f;
                    emission.rateOverTime = 4f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "심연":
                    // 안개: 느린 큰 보라 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
                    main.startColor = new Color(0.4f, 0.2f, 0.6f, 0.3f);
                    main.startLifetime = 4f;
                    emission.rateOverTime = 8f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "던전":
                    // 어두운 안개
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
                    main.startColor = new Color(0.2f, 0.15f, 0.3f, 0.2f);
                    main.startLifetime = 5f;
                    emission.rateOverTime = 6f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "아레나":
                    // 투기장 먼지
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                    main.startColor = new Color(0.6f, 0.4f, 0.8f, 0.3f);
                    main.startLifetime = 2f;
                    emission.rateOverTime = 12f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "보스":
                    // 불길한 붉은 입자
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                    main.startColor = new Color(1f, 0.2f, 0.05f, 0.5f);
                    main.startLifetime = 2f;
                    emission.rateOverTime = 15f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                case "무한":
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                    main.startColor = new Color(1f, 0.4f, 0.1f, 0.6f);
                    main.startLifetime = 1.5f;
                    emission.rateOverTime = 20f;
                    if (!_ambientParticles.isPlaying) _ambientParticles.Play();
                    break;

                default:
                    _ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
            }
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// 하단→상단 세로 그라데이션 스프라이트 생성. 캐시 방식 — 동일 색상이면 재사용.
        /// </summary>
        private static readonly Dictionary<long, Sprite> _gradientCache = new();

        private static Sprite CreateGradientSprite(Color bottomColor, Color topColor)
        {
            // 색상을 int key로 변환하여 캐시
            long key = ((long)bottomColor.GetHashCode() << 32) | (uint)topColor.GetHashCode();
            if (_gradientCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int width = 4;
            const int height = 32;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                Color c = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * 0.5f, 1f);
            _gradientCache[key] = sprite;
            return sprite;
        }

        #endregion

        private void OnDestroy()
        {
            _bgCamTween?.Kill();
            _bgTopTween?.Kill();
            _bgBottomTween?.Kill();
            if (Instance == this) Instance = null;
        }
    }
}
