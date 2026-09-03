using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 탑다운 카메라 컨트롤러.
    /// 플레이어를 부드럽게 추적하며, 아레나 경계 내로 카메라를 제한한다.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("추적 설정")]
        [SerializeField] private Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("UI 보정")]
        [Tooltip("하단 UI(스킬 슬롯+EXP바+탭 바)가 차지하는 화면 비율만큼 카메라를 위로 올려 전투 영역을 중앙에 맞춘다.")]
        [SerializeField] private float _uiBottomOffset = 1.1f;

        [Header("하단 UI 뷰포트 제외")]
        [Tooltip("하단 UI가 차지하는 화면 비율. 이 영역은 카메라 렌더링에서 제외되어 전투 오브젝트가 UI 위에 그려지지 않는다.")]
        [SerializeField] private float _uiBottomViewportRatio = 0.18f;

        [Header("아레나 참조")]
        [SerializeField] private ArenaMap arenaMap;

        private Camera _cachedCamera;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetArenaMap(ArenaMap map)
        {
            arenaMap = map;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = FindFirstObjectByType<PlayerCharacter>();
                if (player != null)
                {
                    target = player.transform;
#if UNITY_EDITOR
                    Debug.Log("[CameraController] Player 자동 연결 완료");
#endif
                }
            }

            if (arenaMap == null)
                arenaMap = FindFirstObjectByType<ArenaMap>();

            _cachedCamera = GetComponent<Camera>();

            // 카메라 뷰포트 조정 — 하단 UI 영역을 렌더링에서 제외하여
            // 전투 오브젝트(데미지텍스트, 이펙트 등)가 하단 UI 위에 그려지지 않도록 한다.
            ApplyViewportRect();
        }

        /// <summary>
        /// 카메라 뷰포트를 조정하여 하단 UI 영역을 렌더링 범위에서 제외한다.
        /// ScreenSpace-Overlay Canvas는 뷰포트 밖에서도 정상 렌더링되므로 UI에는 영향 없다.
        /// </summary>
        private void ApplyViewportRect()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return;

            if (_uiBottomViewportRatio > 0f && _uiBottomViewportRatio < 1f)
            {
                cam.rect = new Rect(0f, _uiBottomViewportRatio, 1f, 1f - _uiBottomViewportRatio);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 하단 UI 영역 보정: 카메라를 위로 올려 전투 영역이 UI에 가리지 않도록 함
            Vector3 uiCompensation = new Vector3(0f, _uiBottomOffset, 0f);
            Vector3 desiredPosition = target.position + offset + uiCompensation;

            Vector3 smoothedPosition = Vector3.Lerp(
                transform.position,
                desiredPosition,
                smoothSpeed * Time.deltaTime
            );

            // 아레나 경계 클램핑
            if (arenaMap != null)
            {
                Rect bounds = arenaMap.GetArenaBounds();
                Camera cam = _cachedCamera;
                if (cam != null && cam.orthographic)
                {
                    float camHalfH = cam.orthographicSize;
                    float camHalfW = camHalfH * cam.aspect;

                    // 카메라 뷰가 아레나보다 크면 해당 축을 중앙 고정
                    float xMin = bounds.xMin + camHalfW;
                    float xMax = bounds.xMax - camHalfW;
                    float clampedX = (xMin > xMax)
                        ? bounds.center.x
                        : Mathf.Clamp(smoothedPosition.x, xMin, xMax);

                    float yMin = bounds.yMin + camHalfH;
                    float yMax = bounds.yMax - camHalfH;
                    float clampedY = (yMin > yMax)
                        ? bounds.center.y
                        : Mathf.Clamp(smoothedPosition.y, yMin, yMax);

                    smoothedPosition.x = clampedX;
                    smoothedPosition.y = clampedY;
                }
            }

            transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, offset.z);
        }
    }
}
