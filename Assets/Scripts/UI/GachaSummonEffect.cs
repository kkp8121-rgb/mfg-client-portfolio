using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MkLike.UI
{
    /// <summary>
    /// 메키 스타일 가챠 소환 연출. UI Toolkit 풀스크린 오버레이.
    /// 검은 배경 페이드 → 소환 이펙트 프레임 애니메이션 → 결과 텍스트 → 터치 대기.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GachaSummonEffect : MonoBehaviour
    {
        // ── 설정 ──
        [Header("프레임 애니메이션")]
        [SerializeField] private int _fps = 20;
        [SerializeField] private int _loopCount = 2;
        [SerializeField] private int _effectSize = 320;

        [Header("타이밍 (ms)")]
        [SerializeField] private int _fadeInMs = 300;
        [SerializeField] private int _fadeOutMs = 300;
        [SerializeField] private int _resultPopupMs = 500;

        // ── 등급별 tint 색상 ──
        private static readonly Dictionary<string, Color> GradeTintMap = new()
        {
            { "Normal",    Color.white },
            { "Rare",      new Color(0.3f, 0.6f, 1.0f) },
            { "Epic",      new Color(0.7f, 0.3f, 0.9f) },
            { "Unique",    new Color(1.0f, 0.85f, 0.1f) },
            { "Legendary", new Color(1.0f, 0.5f, 0.1f) },
            { "Mythic",    new Color(1.0f, 0.2f, 0.3f) },
        };

        private static readonly Dictionary<string, string> GradeDisplayName = new()
        {
            { "Normal", "일반" },
            { "Rare", "희귀" },
            { "Epic", "에픽" },
            { "Unique", "유니크" },
            { "Legendary", "전설" },
            { "Mythic", "신화" },
        };

        // ── 프레임 데이터 (Resources에서 로드) ──
        private Texture2D[] _startFrames;
        private Texture2D[] _loopFrames;
        private Texture2D[] _endFrames;
        private bool _isFramesLoaded;

        // ── UI 요소 ──
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _background;
        private VisualElement _effectImage;
        private Label _gradeLabel;
        private Label _itemLabel;
        private Label _touchHint;
        private VisualElement _resultContainer;

        // ── 상태 ──
        private bool _isPlaying;
        private bool _isTouchWaiting;

        /// <summary>연출 재생 중 여부.</summary>
        public bool IsPlaying => _isPlaying;

        // ── 싱글톤 접근 (선택적) ──
        private static GachaSummonEffect _instance;
        public static GachaSummonEffect Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 200;

            LoadFrames();
            BuildUI();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ────────────────────────────────────────
        // 프레임 로드
        // ────────────────────────────────────────

        private void LoadFrames()
        {
            _startFrames = LoadAndSort("VFX/GachaSummon/Start");
            _loopFrames = LoadAndSort("VFX/GachaSummon/Loop");
            _endFrames = LoadAndSort("VFX/GachaSummon/End");

            _isFramesLoaded = _startFrames.Length > 0 || _loopFrames.Length > 0 || _endFrames.Length > 0;

            if (!_isFramesLoaded)
            {
                Debug.LogWarning("[GachaSummonEffect] VFX 프레임을 로드하지 못했습니다. Resources/VFX/GachaSummon/ 경로를 확인하세요.");
            }
            else
            {
                Debug.Log($"[GachaSummonEffect] 프레임 로드 완료 — Start:{_startFrames.Length}, Loop:{_loopFrames.Length}, End:{_endFrames.Length}");
            }
        }

        private static Texture2D[] LoadAndSort(string path)
        {
            var textures = Resources.LoadAll<Texture2D>(path);
            if (textures == null || textures.Length == 0)
            {
                // Sprite로 로드 시도
                var sprites = Resources.LoadAll<Sprite>(path);
                if (sprites != null && sprites.Length > 0)
                {
                    Array.Sort(sprites, (a, b) => NaturalCompare(a.name, b.name));
                    var result = new Texture2D[sprites.Length];
                    for (int i = 0; i < sprites.Length; i++)
                        result[i] = sprites[i].texture;
                    return result;
                }
                return Array.Empty<Texture2D>();
            }

            Array.Sort(textures, (a, b) => NaturalCompare(a.name, b.name));
            return textures;
        }

        /// <summary>
        /// 자연 정렬: frame1, frame2, ..., frame10 순서로 정렬.
        /// </summary>
        private static int NaturalCompare(string a, string b)
        {
            int ia = 0, ib = 0;
            while (ia < a.Length && ib < b.Length)
            {
                if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                {
                    // 숫자 부분 추출
                    int numStartA = ia, numStartB = ib;
                    while (ia < a.Length && char.IsDigit(a[ia])) ia++;
                    while (ib < b.Length && char.IsDigit(b[ib])) ib++;
                    int numA = int.Parse(a.Substring(numStartA, ia - numStartA));
                    int numB = int.Parse(b.Substring(numStartB, ib - numStartB));
                    if (numA != numB) return numA.CompareTo(numB);
                }
                else
                {
                    if (a[ia] != b[ib]) return a[ia].CompareTo(b[ib]);
                    ia++;
                    ib++;
                }
            }
            return a.Length.CompareTo(b.Length);
        }

        // ────────────────────────────────────────
        // UI 빌드 (C# 동적 생성)
        // ────────────────────────────────────────

        private void BuildUI()
        {
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[GachaSummonEffect] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;

            // 검은 배경 (풀스크린)
            _background = new VisualElement();
            _background.name = "gacha-summon-bg";
            _background.pickingMode = PickingMode.Position;
            _background.style.position = Position.Absolute;
            _background.style.left = 0;
            _background.style.top = 0;
            _background.style.right = 0;
            _background.style.bottom = 0;
            _background.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _background.style.alignItems = Align.Center;
            _background.style.justifyContent = Justify.Center;
            _background.RegisterCallback<ClickEvent>(OnBackgroundClicked);
            _root.Add(_background);

            // 이펙트 이미지 (중앙)
            _effectImage = new VisualElement();
            _effectImage.name = "gacha-summon-effect";
            _effectImage.pickingMode = PickingMode.Ignore;
            _effectImage.style.width = _effectSize;
            _effectImage.style.height = _effectSize;
            _effectImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _effectImage.style.opacity = 0f;
            _background.Add(_effectImage);

            // 결과 컨테이너
            _resultContainer = new VisualElement();
            _resultContainer.name = "gacha-summon-result";
            _resultContainer.pickingMode = PickingMode.Ignore;
            _resultContainer.style.position = Position.Absolute;
            _resultContainer.style.left = 0;
            _resultContainer.style.right = 0;
            _resultContainer.style.bottom = new Length(35, LengthUnit.Percent);
            _resultContainer.style.alignItems = Align.Center;
            _resultContainer.style.opacity = 0f;
            _background.Add(_resultContainer);

            // 등급 라벨
            _gradeLabel = new Label();
            _gradeLabel.name = "gacha-summon-grade";
            _gradeLabel.pickingMode = PickingMode.Ignore;
            _gradeLabel.style.fontSize = 40;
            _gradeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _gradeLabel.style.color = Color.white;
            _gradeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _gradeLabel.style.marginBottom = 8;
            _gradeLabel.style.textShadow = new TextShadow
            {
                offset = new Vector2(2, 2),
                blurRadius = 4,
                color = new Color(0, 0, 0, 0.8f)
            };
            _resultContainer.Add(_gradeLabel);

            // 아이템 이름 라벨
            _itemLabel = new Label();
            _itemLabel.name = "gacha-summon-item";
            _itemLabel.pickingMode = PickingMode.Ignore;
            _itemLabel.style.fontSize = 28;
            _itemLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            _itemLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _itemLabel.style.textShadow = new TextShadow
            {
                offset = new Vector2(1, 1),
                blurRadius = 3,
                color = new Color(0, 0, 0, 0.7f)
            };
            _resultContainer.Add(_itemLabel);

            // 터치 힌트
            _touchHint = new Label("터치하여 닫기");
            _touchHint.name = "gacha-summon-hint";
            _touchHint.pickingMode = PickingMode.Ignore;
            _touchHint.style.position = Position.Absolute;
            _touchHint.style.bottom = new Length(10, LengthUnit.Percent);
            _touchHint.style.left = 0;
            _touchHint.style.right = 0;
            _touchHint.style.fontSize = 20;
            _touchHint.style.color = new Color(0.6f, 0.6f, 0.6f, 0f);
            _touchHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _background.Add(_touchHint);
        }

        // ────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────

        /// <summary>
        /// 소환 연출을 재생한다. 연출 완료(터치 닫기) 후 반환.
        /// </summary>
        /// <param name="gradeName">등급 영문 (Normal, Rare, Epic, Unique, Legendary, Mythic)</param>
        /// <param name="itemName">아이템 표시명</param>
        public async UniTask PlaySummonAsync(string gradeName, string itemName,
            CancellationToken ct = default)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[GachaSummonEffect] 이미 연출 재생 중");
                return;
            }

            if (!_isFramesLoaded)
            {
                Debug.LogWarning("[GachaSummonEffect] 프레임 미로드, 연출 스킵");
                return;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct, this.GetCancellationTokenOnDestroy());
            var token = linkedCts.Token;

            _isPlaying = true;
            _isTouchWaiting = false;

            Color tint = GradeTintMap.TryGetValue(gradeName, out var c) ? c : Color.white;
            string gradeDisplay = GradeDisplayName.TryGetValue(gradeName, out var gd) ? gd : gradeName;

            try
            {
                SetVisible(true);
                ResetUI();

                // Phase 1: 검은 배경 페이드인
                await FadeBackgroundAsync(0f, 1f, _fadeInMs, token);

                // Phase 2: Start 프레임 재생
                _effectImage.style.opacity = 1f;
                _effectImage.style.unityBackgroundImageTintColor = tint;
                await PlayFramesAsync(_startFrames, 1, token);

                // Phase 3: Loop 프레임 반복
                await PlayFramesAsync(_loopFrames, _loopCount, token);

                // Phase 4: End 프레임 재생
                await PlayFramesAsync(_endFrames, 1, token);

                // 이펙트 페이드아웃
                await FadeElementAsync(_effectImage, 1f, 0f, 200, token);

                // Phase 5: 결과 텍스트 팝업
                _gradeLabel.text = gradeDisplay;
                _gradeLabel.style.color = tint;
                _itemLabel.text = itemName;

                await FadeElementAsync(_resultContainer, 0f, 1f, _resultPopupMs, token);

                // 결과 텍스트 스케일 팝 효과
                _resultContainer.style.scale = new Scale(new Vector3(1.2f, 1.2f, 1f));
                await UniTask.Delay(50, cancellationToken: token);
                _resultContainer.style.scale = new Scale(Vector3.one);

                // Phase 6: 터치 대기
                _touchHint.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                _isTouchWaiting = true;

                // 터치 대기 루프
                await UniTask.WaitUntil(() => !_isTouchWaiting, cancellationToken: token);

                // 닫기 페이드아웃
                await FadeBackgroundAsync(1f, 0f, _fadeOutMs, token);
            }
            catch (OperationCanceledException)
            {
                // 취소됨 — 즉시 정리
                Debug.Log("[GachaSummonEffect] 연출 취소됨");
            }
            finally
            {
                SetVisible(false);
                _isPlaying = false;
                _isTouchWaiting = false;
                linkedCts.Dispose();
            }
        }

        /// <summary>
        /// 10연차용: 최고 등급 기준 연출 1회 → 닫기 없이 즉시 반환.
        /// 결과 팝업은 호출측에서 별도 표시.
        /// </summary>
        public async UniTask PlaySummonBriefAsync(string gradeName,
            CancellationToken ct = default)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[GachaSummonEffect] 이미 연출 재생 중");
                return;
            }

            if (!_isFramesLoaded)
            {
                Debug.LogWarning("[GachaSummonEffect] 프레임 미로드, 연출 스킵");
                return;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct, this.GetCancellationTokenOnDestroy());
            var token = linkedCts.Token;

            _isPlaying = true;

            Color tint = GradeTintMap.TryGetValue(gradeName, out var c) ? c : Color.white;

            try
            {
                SetVisible(true);
                ResetUI();

                // 배경 페이드인
                await FadeBackgroundAsync(0f, 1f, _fadeInMs, token);

                // 이펙트 재생 (Start → Loop 1회 → End)
                _effectImage.style.opacity = 1f;
                _effectImage.style.unityBackgroundImageTintColor = tint;
                await PlayFramesAsync(_startFrames, 1, token);
                await PlayFramesAsync(_loopFrames, 1, token);
                await PlayFramesAsync(_endFrames, 1, token);

                // 이펙트 페이드아웃
                await FadeElementAsync(_effectImage, 1f, 0f, 200, token);

                // 배경 페이드아웃
                await FadeBackgroundAsync(1f, 0f, _fadeOutMs, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GachaSummonEffect] 10연차 연출 취소됨");
            }
            finally
            {
                SetVisible(false);
                _isPlaying = false;
                linkedCts.Dispose();
            }
        }

        // ────────────────────────────────────────
        // 내부: 프레임 애니메이션
        // ────────────────────────────────────────

        private async UniTask PlayFramesAsync(Texture2D[] frames, int repeatCount,
            CancellationToken ct)
        {
            if (frames == null || frames.Length == 0) return;

            int frameDelayMs = 1000 / Mathf.Max(_fps, 1);

            for (int r = 0; r < repeatCount; r++)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (frames[i] != null)
                        _effectImage.style.backgroundImage = new StyleBackground(frames[i]);

                    await UniTask.Delay(frameDelayMs, ignoreTimeScale: true, cancellationToken: ct);
                }
            }
        }

        // ────────────────────────────────────────
        // 내부: 페이드 유틸
        // ────────────────────────────────────────

        private async UniTask FadeBackgroundAsync(float from, float to, int durationMs,
            CancellationToken ct)
        {
            int steps = Mathf.Max(durationMs / 16, 1); // ~60fps 기준
            float stepTime = (float)durationMs / steps;

            for (int i = 0; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                float t = (float)i / steps;
                float alpha = Mathf.Lerp(from, to, t);
                _background.style.backgroundColor = new Color(0f, 0f, 0f, alpha);

                await UniTask.Delay((int)stepTime, ignoreTimeScale: true, cancellationToken: ct);
            }
        }

        private async UniTask FadeElementAsync(VisualElement element, float from, float to,
            int durationMs, CancellationToken ct)
        {
            int steps = Mathf.Max(durationMs / 16, 1);
            float stepTime = (float)durationMs / steps;

            for (int i = 0; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                float t = (float)i / steps;
                element.style.opacity = Mathf.Lerp(from, to, t);

                await UniTask.Delay((int)stepTime, ignoreTimeScale: true, cancellationToken: ct);
            }
        }

        // ────────────────────────────────────────
        // 내부: UI 제어
        // ────────────────────────────────────────

        private void ResetUI()
        {
            _background.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _effectImage.style.opacity = 0f;
            _effectImage.style.backgroundImage = StyleKeyword.None;
            _resultContainer.style.opacity = 0f;
            _resultContainer.style.scale = new Scale(Vector3.one);
            _gradeLabel.text = "";
            _itemLabel.text = "";
            _touchHint.style.color = new Color(0.6f, 0.6f, 0.6f, 0f);
        }

        private void SetVisible(bool isVisible)
        {
            if (_root == null) return;

            _root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (isVisible)
                _root.pickingMode = PickingMode.Position;
            else
                _root.pickingMode = PickingMode.Ignore;
        }

        private void OnBackgroundClicked(ClickEvent evt)
        {
            if (_isTouchWaiting)
            {
                _isTouchWaiting = false;
            }
        }
    }
}
