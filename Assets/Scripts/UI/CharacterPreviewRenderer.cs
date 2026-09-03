using UnityEngine;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 캐릭터 초상화용 전용 SPUM 클론 오브젝트 렌더러.
    /// 플레이어와 동일한 SPUM 캐릭터를 숨겨진 위치에 배치하고,
    /// 전용 카메라로 RenderTexture에 캡처하여 UI에 제공한다.
    /// 항상 Idle 포즈, 정면 방향으로 표시.
    /// </summary>
    public class CharacterPreviewRenderer : MonoBehaviour
    {
        public static CharacterPreviewRenderer Instance { get; private set; }

        [Header("렌더 설정")]
        [SerializeField] private int _textureSize = 256;
        [SerializeField] private float _cameraSize = 0.8f;
        [SerializeField] private Vector3 _cloneOffset = new(0f, 0.3f, 0f);

        [Header("클론 배치")]
        [SerializeField] private Vector3 _hiddenPosition = new(100f, 100f, 0f);

        private Camera _previewCamera;
        private RenderTexture _renderTexture;
        private Texture2D _capturedTexture;
        private bool _isInitialized;

        // 전용 SPUM 클론
        private GameObject _spumClone;
        private Transform _cloneRoot;
        private JobType _cloneJob = (JobType)(-1);
        private int _cloneTier = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Cleanup();
        }

        private void OnJobChanged(JobChangedEvent evt)
        {
            // 직업/전직 변경 시 클론 갱신 예약
            _cloneJob = (JobType)(-1);
            _cloneTier = -1;
        }

        #region 초기화

        private void Initialize()
        {
            if (_isInitialized) return;

            // 클론 루트 생성 (숨겨진 위치)
            _cloneRoot = new GameObject("PortraitCloneRoot").transform;
            _cloneRoot.SetParent(transform);
            _cloneRoot.position = _hiddenPosition;

            // RenderTexture 생성
            _renderTexture = new RenderTexture(_textureSize, _textureSize, 16, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            // 전용 카메라 생성
            var camObj = new GameObject("PortraitCamera");
            camObj.transform.SetParent(_cloneRoot);
            camObj.transform.localPosition = new Vector3(_cloneOffset.x, _cloneOffset.y, -10f);
            _previewCamera = camObj.AddComponent<Camera>();
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = _cameraSize;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = Color.clear;
            _previewCamera.depth = -100;
            _previewCamera.enabled = false; // 수동 렌더링만

            _capturedTexture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);

            _isInitialized = true;
            Debug.Log("[CharacterPreviewRenderer] 초기화 완료 (SPUM 클론 방식)");
        }

        #endregion

        #region SPUM 클론 관리

        /// <summary>
        /// 현재 직업/티어에 맞는 SPUM 클론을 생성 또는 갱신한다.
        /// </summary>
        private void EnsureClone()
        {
            var spumManager = SpumCharacterManager.Instance;
            if (spumManager == null) return;

            // 현재 직업/티어 확인
            JobType currentJob = JobType.Warrior;
            int currentTier = 0;

            var saveData = MkLike.Core.Save.SaveManager.Instance?.CurrentData;
            if (saveData != null)
            {
                currentJob = saveData.player.jobId switch
                {
                    "archer" => JobType.Archer,
                    "mage" => JobType.Mage,
                    _ => JobType.Warrior
                };
                currentTier = saveData.player.jobTier;
            }

            // 이미 동일한 클론이 있으면 스킵
            if (_spumClone != null && currentJob == _cloneJob && currentTier == _cloneTier)
                return;

            // 기존 클론 제거
            if (_spumClone != null)
            {
                Destroy(_spumClone);
                _spumClone = null;
            }

            // SpumCharacterManager를 통해 새 클론 생성
            // 임시 루트 생성 → ApplyJobVisual 호출 → SPUM 자식 추출
            var tempRoot = new GameObject("TempCloneRoot");
            tempRoot.transform.SetParent(_cloneRoot);
            tempRoot.transform.localPosition = Vector3.zero;
            tempRoot.transform.localScale = Vector3.one;

            // 2026-04-20 SPUM 단일 원천 통일 — JobOutfitDatabaseSO에서 직접 프리팹 조회.
            // 기존 CurrentSpumInstance 복제 방식은 Player 없을 때 깨지므로 fallback으로 밀어냄.
            GameObject sourcePrefab = null;
            var outfitDb = MkLike.Data.JobOutfitDatabaseSO.Load();
            if (outfitDb != null)
                sourcePrefab = outfitDb.GetPrefab(currentJob, currentTier);

            if (sourcePrefab != null)
            {
                _spumClone = Instantiate(sourcePrefab, _cloneRoot);
                _spumClone.name = "PortraitSPUM";
                _spumClone.transform.localPosition = Vector3.zero;
                _spumClone.transform.localScale = Vector3.one;
                _spumClone.transform.localRotation = Quaternion.identity;
            }
            else if (spumManager.CurrentSpumInstance != null)
            {
                // Fallback: 런타임 Player 인스턴스 복제 (DB 로드 실패 시)
                _spumClone = Instantiate(spumManager.CurrentSpumInstance, _cloneRoot);
                _spumClone.name = "PortraitSPUM";
                _spumClone.transform.localPosition = Vector3.zero;
                _spumClone.transform.localScale = Vector3.one;
                _spumClone.transform.localRotation = Quaternion.identity;
            }

            if (_spumClone != null)
            {

                // Idle 애니메이션 재생 (SPUM은 별도 어셈블리 — 리플렉션 사용)
                try
                {
                    var stateType = System.Type.GetType("PlayerState, Assembly-CSharp");
                    var spumType = System.Type.GetType("SPUM_Prefabs, Assembly-CSharp");
                    if (spumType != null && stateType != null)
                    {
                        var spumComp = _spumClone.GetComponentInChildren(spumType);
                        if (spumComp != null)
                        {
                            var playAnim = spumType.GetMethod("PlayAnimation");
                            var idleValue = System.Enum.Parse(stateType, "IDLE");
                            if (playAnim != null)
                                playAnim.Invoke(spumComp, new object[] { idleValue, 0 });
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CharacterPreviewRenderer] SPUM Idle 재생 실패: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[CharacterPreviewRenderer] SPUM 인스턴스 없음 — 클론 생성 실패");
            }

            Destroy(tempRoot);

            _cloneJob = currentJob;
            _cloneTier = currentTier;
        }

        #endregion

        #region 캡처 API

        /// <summary>
        /// Player SPUM 클론의 현재 모습을 캡처하여 Texture2D로 반환.
        /// 패널 열 때 호출한다.
        /// </summary>
        public Texture2D CapturePlayerPreview()
        {
            if (!_isInitialized) Initialize();

            EnsureClone();

            if (_spumClone == null)
            {
                // fallback: 플레이어 직접 촬영
                var direct = CapturePlayerDirect();
                return IsTextureValid(direct) ? direct : null;
            }

            // 클론 위치의 카메라 렌더링
            _previewCamera.cullingMask = -1;
            _previewCamera.Render();

            // RenderTexture → Texture2D
            var prevActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            _capturedTexture.ReadPixels(new Rect(0, 0, _textureSize, _textureSize), 0, 0);
            _capturedTexture.Apply();
            RenderTexture.active = prevActive;

            return IsTextureValid(_capturedTexture) ? _capturedTexture : null;
        }

        /// <summary>텍스처가 투명/검정이 아닌 유효한 이미지인지 확인</summary>
        private static bool IsTextureValid(Texture2D tex)
        {
            if (tex == null) return false;
            // 중앙 픽셀 샘플링 — 완전 투명이면 유효하지 않음
            var pixel = tex.GetPixel(tex.width / 2, tex.height / 2);
            return pixel.a > 0.01f;
        }

        /// <summary>
        /// 직접 플레이어 캐릭터를 촬영하는 fallback.
        /// </summary>
        private Texture2D CapturePlayerDirect()
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[CharacterPreviewRenderer] PlayerCharacter 없음 — 캡처 실패");
                return null;
            }

            // SPUM 인스턴스 찾기
            GameObject spumInstance = null;
            var spumManager = SpumCharacterManager.Instance;
            if (spumManager != null && spumManager.CurrentSpumInstance != null)
            {
                spumInstance = spumManager.CurrentSpumInstance;
            }
            else
            {
                for (int i = 0; i < player.transform.childCount; i++)
                {
                    var child = player.transform.GetChild(i);
                    if (child.name.StartsWith("SPUM_"))
                    {
                        spumInstance = child.gameObject;
                        break;
                    }
                }
            }

            if (spumInstance == null)
            {
                Debug.LogWarning("[CharacterPreviewRenderer] SPUM 없음 — 캡처 실패");
                return null;
            }

            // SPUM bounds 기반 카메라 배치
            var renderers = spumInstance.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return null;

            Bounds bounds = default;
            bool hasInit = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled || renderers[i].sprite == null) continue;
                if (!hasInit) { bounds = renderers[i].bounds; hasInit = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }

            if (!hasInit) return null;

            _previewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y + 0.3f, bounds.center.z - 10f);
            _previewCamera.orthographicSize = _cameraSize;
            _previewCamera.cullingMask = -1;
            _previewCamera.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            _capturedTexture.ReadPixels(new Rect(0, 0, _textureSize, _textureSize), 0, 0);
            _capturedTexture.Apply();
            RenderTexture.active = prevActive;

            return _capturedTexture;
        }

        #endregion

        #region 정리

        private void Cleanup()
        {
            if (_spumClone != null)
            {
                Destroy(_spumClone);
                _spumClone = null;
            }
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
            if (_capturedTexture != null)
            {
                Destroy(_capturedTexture);
                _capturedTexture = null;
            }
            if (_previewCamera != null)
            {
                Destroy(_previewCamera.gameObject);
                _previewCamera = null;
            }
            _isInitialized = false;
        }

        #endregion
    }
}
