using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using Lean.Pool;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 보스 패턴 사이클을 제어한다.
    /// Normal(10초) → Warning(2초) → StrongAttack(1초) → Stunned(3초) → 반복.
    ///
    /// 보스 HP바 + 경고 텍스트 + 무력화 텍스트 + 제한시간 타이머를
    /// 코드로 동적 생성하여 ScreenSpaceOverlay Canvas 위에 표시한다.
    ///
    /// MonsterController.IsBoss / IsChapterBoss인 몬스터에 자동 부착된다.
    /// </summary>
    public class BossPatternController : MonoBehaviour
    {
        #region 상수

        private const float NORMAL_DURATION = 10f;
        private const float WARNING_DURATION = 2f;
        private const float STRONG_ATTACK_DURATION = 1f;
        private const float STUNNED_DURATION = 3f;
        private const float DEFAULT_TIME_LIMIT = 60f;
        private const float STUN_DAMAGE_MULTIPLIER = 2f;
        private const int STRONG_ATTACK_DAMAGE_RATIO = 3;
        private const int SORTING_ORDER = 320;

        #endregion

        #region 색상

        private static readonly Color COLOR_HP_BG = new(0.15f, 0.15f, 0.2f, 0.9f);
        private static readonly Color COLOR_HP_YELLOW = new(1f, 0.84f, 0f, 1f);
        private static readonly Color COLOR_HP_RED = new(0.85f, 0.15f, 0.1f, 1f);
        private static readonly Color COLOR_WARNING = new(1f, 0.2f, 0.15f, 1f);
        private static readonly Color COLOR_STUN = new(0.2f, 0.85f, 1f, 1f);
        private static readonly Color COLOR_TIMER_NORMAL = Color.white;
        private static readonly Color COLOR_TIMER_URGENT = new(1f, 0.3f, 0.2f, 1f);
        private static readonly Color COLOR_OVERLAY_FLASH = new(1f, 0f, 0f, 0.15f);

        #endregion

        #region 페이즈 열거형

        public enum BossPhase
        {
            Normal,
            Warning,
            StrongAttack,
            Stunned
        }

        /// <summary>보스 공격 패턴 (탑뷰 회피용)</summary>
        public enum BossAttackPattern
        {
            CircularShockwave,  // 원형 충격파 — 보스 중심 범위 밖으로 이동
            LinearProjectile,   // 직선 투사체 — 플레이어 방향으로 발사, 좌우 회피
            CrossLaser          // 십자 레이저 — 4방향 직선, 대각선 회피
        }

        #endregion

        #region 상태

        private BossPhase _currentPhase = BossPhase.Normal;
        private BossAttackPattern _currentPattern;
        private float _phaseTimer;
        private float _remainingTime;
        private float _timeLimit;
        private bool _isActive;
        private bool _playerDodged;
        private bool _isEnding;

        // 경고 범위 시각 효과
        private const float CIRCULAR_RADIUS = 3.5f;
        private const float CROSS_WIDTH = 1.2f;
        private const float CROSS_LENGTH = 15f;
        private const float PROJECTILE_SPEED = 8f;
        private const int PROJECTILE_COUNT = 3;
        private const float PROJECTILE_SPREAD = 15f; // 각도
        private GameObject _warningIndicator;
        private Vector2 _attackDirection; // 직선 투사체용

        /// <summary>현재 보스 페이즈</summary>
        public BossPhase CurrentPhase => _currentPhase;
        /// <summary>보스 패턴 전투 진행 중 여부</summary>
        public bool IsActive => _isActive;
        /// <summary>남은 시간</summary>
        public float RemainingTime => _remainingTime;

        #endregion

        #region 참조

        private CombatStats _bossStats;
        private MonsterController _bossController;
        private PlayerCharacter _player;
        private CombatStats _playerStats;

        // 데미지 배율 수정자 키
        private const string STUN_MODIFIER_KEY = "boss_stun_dmg";

        #endregion

        #region UI 요소

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        // HP바
        private RectTransform _hpBarPanel;
        private Image _hpBarBg;
        private RectTransform _hpBarYellowRect;
        private Image _hpBarYellowImage;
        private RectTransform _hpBarRedRect;
        private Image _hpBarRedImage;
        private TMP_Text _bossNameText;
        private TMP_Text _hpPercentText;

        // 타이머
        private TMP_Text _timerText;

        // 경고/무력화 중앙 텍스트
        private RectTransform _centerTextPanel;
        private TMP_Text _centerText;
        private Image _centerTextBg;

        // 빨간 플래시 오버레이
        private Image _flashOverlay;

        // HP바 레이아웃
        private const float HP_BAR_WIDTH = 500f;
        private const float HP_BAR_HEIGHT = 16f;
        private const float HP_BAR_RED_HEIGHT = 8f;
        private const float HP_BAR_TOP_OFFSET = 110f;

        private Sequence _centerTextSequence;
        private Sequence _flashSequence;
        private Tween _hpBarTween;

        #endregion

        #region 초기화

        /// <summary>
        /// 보스 패턴 전투를 시작한다.
        /// MonsterController에서 보스 스폰 시 호출.
        /// </summary>
        public void Initialize(MonsterController bossController, float timeLimit = DEFAULT_TIME_LIMIT)
        {
            if (_isActive) return;

            _bossController = bossController;
            _bossStats = bossController.GetComponent<CombatStats>();
            _timeLimit = timeLimit;
            _remainingTime = timeLimit;

            // 플레이어 참조
            _player = FindFirstObjectByType<PlayerCharacter>();
            if (_player != null)
                _playerStats = _player.GetComponent<CombatStats>();

            BuildUI();
            SubscribeEvents();

            _isActive = true;
            _isEnding = false;
            _currentPhase = BossPhase.Normal;
            _phaseTimer = NORMAL_DURATION;

            SetCanvasVisible(true);
            UpdateHpBar();

            // 보스 이름 표시
            string bossName = bossController.IsChapterBoss
                ? $"챕터 보스"
                : "미니보스";
            if (_bossNameText != null)
                _bossNameText.text = bossName;

            Debug.Log($"[BossPatternController] 보스 패턴 시작: {bossName}, 제한 시간: {timeLimit}초");
        }

        private void SubscribeEvents()
        {
            if (_bossStats != null)
            {
                _bossStats.OnHpChanged += OnBossHpChanged;
                _bossStats.OnDeath += OnBossDeath;
            }
            UIState.OnPanelStateChanged += OnPanelStateChanged;
            MkLike.Utils.EventBus<FarmingModeEvent>.Subscribe(OnFarmingMode);
        }

        private void UnsubscribeEvents()
        {
            UIState.OnPanelStateChanged -= OnPanelStateChanged;
            MkLike.Utils.EventBus<FarmingModeEvent>.Unsubscribe(OnFarmingMode);
            if (_bossStats != null)
            {
                _bossStats.OnHpChanged -= OnBossHpChanged;
                _bossStats.OnDeath -= OnBossDeath;
            }
        }

        /// <summary>파밍 모드 진입 시 보스 UI 즉시 정리 (보스 실패 후퇴 대응)</summary>
        private void OnFarmingMode(FarmingModeEvent e)
        {
            if (e.IsActive && _isActive && !_isEnding)
            {
                _isEnding = true;
                Cleanup();
            }
        }

        #endregion

        #region Unity 생명주기

        private void Update()
        {
            if (!_isActive || _isEnding) return;

            // 전체 제한 시간 감소
            _remainingTime -= Time.deltaTime;
            UpdateTimerText();

            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                OnTimeOver();
                return;
            }

            // 보스 사망 체크
            if (_bossStats != null && _bossStats.IsDead)
            {
                OnBossClear();
                return;
            }

            // 페이즈 타이머
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
            {
                AdvancePhase();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            CleanupTweens();
            DestroyWarningIndicator();

            // 무력화 수정자가 남아있으면 제거
            if (_bossStats != null && _bossStats.HasModifier(STUN_MODIFIER_KEY))
                _bossStats.RemoveModifier(STUN_MODIFIER_KEY);
        }

        private void CleanupTweens()
        {
            _centerTextSequence?.Kill();
            _flashSequence?.Kill();
            _hpBarTween?.Kill();
            transform.DOKill();
        }

        #endregion

        #region 페이즈 전환

        private void AdvancePhase()
        {
            switch (_currentPhase)
            {
                case BossPhase.Normal:
                    EnterWarning();
                    break;
                case BossPhase.Warning:
                    EnterStrongAttack();
                    break;
                case BossPhase.StrongAttack:
                    if (_playerDodged)
                        EnterStunned();
                    else
                        EnterNormal(); // 회피 실패 → 그냥 Normal로 복귀
                    break;
                case BossPhase.Stunned:
                    ExitStunned();
                    EnterNormal();
                    break;
            }
        }

        private void EnterNormal()
        {
            _currentPhase = BossPhase.Normal;
            _phaseTimer = NORMAL_DURATION;
            _playerDodged = false;

            HideCenterText();

            Debug.Log("[BossPatternController] 페이즈: Normal");
        }

        private void EnterWarning()
        {
            _currentPhase = BossPhase.Warning;
            _phaseTimer = WARNING_DURATION;

            // 3패턴 중 랜덤 선택
            _currentPattern = (BossAttackPattern)Random.Range(0, 3);

            // 패턴별 경고 텍스트
            string warningText = _currentPattern switch
            {
                BossAttackPattern.CircularShockwave => "충격파! 범위 밖으로!",
                BossAttackPattern.LinearProjectile => "투사체! 옆으로 피하세요!",
                BossAttackPattern.CrossLaser => "십자 공격! 대각선으로!",
                _ => "강공격 경고!"
            };
            ShowCenterText(warningText, COLOR_WARNING);

            // 빨간 플래시
            PlayFlashOverlay();

            // 경고 범위 시각 표시
            ShowWarningIndicator();

            // 경고음
            AudioManager.Instance?.PlaySfx(SfxType.BossWarning);

            // 화면 흔들림
            ScreenShakeManager.Instance?.ShakeLight();

            Debug.Log($"[BossPatternController] 페이즈: Warning — 패턴: {_currentPattern}");
        }

        private void EnterStrongAttack()
        {
            _currentPhase = BossPhase.StrongAttack;
            _phaseTimer = STRONG_ATTACK_DURATION;

            // 경고 표시 제거
            DestroyWarningIndicator();

            // 패턴별 회피 판정
            _playerDodged = CheckDodge();

            if (_playerDodged)
            {
                ShowCenterText("회피 성공!", new Color(0.3f, 1f, 0.5f, 1f));
                DamageTextManager.Instance?.ShowMissText(
                    _player != null ? _player.transform.position : transform.position);
                AudioManager.Instance?.PlaySfx(SfxType.UiConfirm);
                Debug.Log($"[BossPatternController] {_currentPattern} 회피 성공!");
            }
            else
            {
                // 회피 실패 → 대 데미지
                if (_playerStats != null && _bossStats != null && !_playerStats.IsDead)
                {
                    int strongDamage = _bossStats.Atk * STRONG_ATTACK_DAMAGE_RATIO;
                    _playerStats.TakeDamage(strongDamage);
                    DamageTextManager.Instance?.Spawn(
                        _player != null ? _player.transform.position : Vector3.zero,
                        strongDamage, false, 0, true);
                    ScreenShakeManager.Instance?.ShakeHeavy();
                }

                // 플레이어 스턴 적용
                if (_player != null && !_playerStats.IsDead)
                    _player.Stun();

                ShowCenterText("강공격 피격!", COLOR_WARNING);
                Debug.Log($"[BossPatternController] {_currentPattern} 피격! 플레이어 스턴");
            }

            // 직선 투사체 패턴: 실제 투사체 발사
            if (_currentPattern == BossAttackPattern.LinearProjectile)
                SpawnProjectiles();

            PlayFlashOverlay();
        }

        /// <summary>패턴별 회피 판정 (탑뷰 범위 기반)</summary>
        private bool CheckDodge()
        {
            if (_player == null || _bossController == null) return false;

            Vector2 bossPos = _bossController.transform.position;
            Vector2 playerPos = _player.transform.position;

            return _currentPattern switch
            {
                // 원형: 보스 중심 반경 밖이면 회피
                BossAttackPattern.CircularShockwave =>
                    Vector2.Distance(bossPos, playerPos) > CIRCULAR_RADIUS,

                // 직선: 투사체 경로(보스→플레이어 방향)에서 수직 거리로 판정
                // 투사체는 Warning 시점의 방향으로 발사되므로, 그 후 이동하면 회피
                BossAttackPattern.LinearProjectile =>
                    !IsInLinearPath(bossPos, _attackDirection, playerPos, 1.5f),

                // 십자: 4방향 직선 범위(X축/Y축) 밖이면 회피
                BossAttackPattern.CrossLaser =>
                    !IsInCrossPattern(bossPos, playerPos, CROSS_WIDTH),

                _ => false
            };
        }

        private static bool IsInLinearPath(Vector2 origin, Vector2 direction, Vector2 point, float width)
        {
            if (direction == Vector2.zero) return false;
            Vector2 toPoint = point - origin;
            float dot = Vector2.Dot(toPoint, direction);
            if (dot < 0) return false; // 뒤쪽은 안전
            Vector2 projected = origin + direction * dot;
            return Vector2.Distance(projected, point) < width;
        }

        private static bool IsInCrossPattern(Vector2 center, Vector2 point, float width)
        {
            float dx = Mathf.Abs(point.x - center.x);
            float dy = Mathf.Abs(point.y - center.y);
            return dx < width || dy < width; // X축 또는 Y축 근처
        }

        /// <summary>직선 투사체 3발 발사</summary>
        private void SpawnProjectiles()
        {
            if (_bossController == null) return;

            Vector2 bossPos = _bossController.transform.position;
            float baseAngle = Mathf.Atan2(_attackDirection.y, _attackDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < PROJECTILE_COUNT; i++)
            {
                float angle = baseAngle + (i - 1) * PROJECTILE_SPREAD;
                Vector2 dir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

                var go = new GameObject("BossProjectile");
                go.transform.position = (Vector3)bossPos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = COLOR_WARNING;
                sr.sortingOrder = 100;

                // 간단한 원형 스프라이트 (없으면 기본)
                var projectile = go.AddComponent<BossProjectile>();
                projectile.Initialize(dir, PROJECTILE_SPEED,
                    _bossStats != null ? _bossStats.Atk * STRONG_ATTACK_DAMAGE_RATIO : 100,
                    _player?.transform, _playerStats);
            }
        }

        private void EnterStunned()
        {
            _currentPhase = BossPhase.Stunned;
            _phaseTimer = STUNNED_DURATION;

            // 무력화 텍스트
            ShowCenterText("보스가 무력화! 지금 공격하세요!", COLOR_STUN);

            // 보스 스턴 (움직임 정지)
            if (_bossController != null)
                _bossController.Stun(STUNNED_DURATION);

            // 보스에게 받는 데미지 2배 수정자 적용
            // (플레이어 FinalDamage 증가 대신 보스의 Def를 낮추는 방식보다,
            //  직관적으로 보스 MaxHp 감소 수정자 대신 데미지 배율을 활용)
            // → 플레이어 FinalDamage에 임시 수정자 추가
            if (_playerStats != null)
            {
                _playerStats.AddModifier(STUN_MODIFIER_KEY,
                    new StatModifier(
                        ModifierSource.Buff, "boss_stun",
                        StatType.FinalDamage, STUN_DAMAGE_MULTIPLIER - 1f, 0f));
            }

            AudioManager.Instance?.PlaySfx(SfxType.BossRoar);
            ScreenShakeManager.Instance?.ShakeMedium();

            Debug.Log("[BossPatternController] 페이즈: Stunned — 무력화! 데미지 2배");
        }

        private void ExitStunned()
        {
            // 데미지 배율 수정자 제거
            if (_playerStats != null && _playerStats.HasModifier(STUN_MODIFIER_KEY))
                _playerStats.RemoveModifier(STUN_MODIFIER_KEY);
        }

        #endregion

        #region 전투 종료

        private void OnBossClear()
        {
            if (_isEnding) return;
            _isEnding = true;

            ExitStunned();
            CleanupTweens();
            DestroyWarningIndicator();  // 경고 원 잔상 방지 (보스 사망 시점에 표시 중일 수 있음)

            Debug.Log("[BossPatternController] 보스 클리어!");

            // CLEAR 결과 팝업
            var popup = FindFirstObjectByType<BossResultPopup>();
            if (popup == null)
            {
                var popupGo = new GameObject("BossResultPopup");
                popup = popupGo.AddComponent<BossResultPopup>();
            }

            popup.ShowClear(_bossNameText != null ? _bossNameText.text : "보스");

            // 챕터 BGM 복귀는 StageManager가 처리

            EndCleanupAsync().Forget();
        }

        private void OnTimeOver()
        {
            if (_isEnding) return;
            _isEnding = true;

            ExitStunned();
            HideCenterText();
            CleanupTweens();
            DestroyWarningIndicator();  // 경고 원 잔상 방지 (TIME OVER 시점에 표시 중일 수 있음)

            Debug.Log("[BossPatternController] TIME OVER — 실패!");

            // FAILED 결과 팝업
            var popup = FindFirstObjectByType<BossResultPopup>();
            if (popup == null)
            {
                var popupGo = new GameObject("BossResultPopup");
                popup = popupGo.AddComponent<BossResultPopup>();
            }

            popup.ShowFailed(
                _bossNameText != null ? _bossNameText.text : "보스",
                OnRetryClicked,
                OnExitClicked
            );

            // 보스 Despawn (죽이지 않음 — 파밍 모드용)
            if (_bossController != null && _bossController.gameObject.activeInHierarchy)
                LeanPool.Despawn(_bossController.gameObject);

            Cleanup();
        }

        private void OnBossDeath()
        {
            // 사망 이벤트 → 클리어 처리
            if (_isActive && !_isEnding)
                OnBossClear();
        }

        private void OnPanelStateChanged(bool isOpen)
        {
            // 탭 패널이 열리면 보스 HUD 숨기기
            if (_canvas != null) _canvas.enabled = !isOpen;
        }

        private void OnBossHpChanged(int current, int max)
        {
            UpdateHpBar();
        }

        private void OnRetryClicked()
        {
            // StageManager에게 재도전 요청
            var stageManager = FindFirstObjectByType<StageManager>();
            if (stageManager != null)
                stageManager.RetryStage();

            Cleanup();
        }

        private void OnExitClicked()
        {
            // 보스 실패 → 죽이지 않고 Despawn + 파밍 모드 진입
            if (_bossController != null && _bossController.gameObject.activeInHierarchy)
                Lean.Pool.LeanPool.Despawn(_bossController.gameObject);

            // StageManager 파밍 모드 진입
            var stageManager = FindFirstObjectByType<StageManager>();
            stageManager?.EnterFarmingMode();

            Cleanup();
        }

        private async UniTaskVoid EndCleanupAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                // 4초 대기 후 UI 정리 (BossResultPopup이 자체 타이머 관리)
                await UniTask.Delay(4000, cancellationToken: ct);
                Cleanup();
            }
            catch (System.OperationCanceledException)
            {
                // 오브젝트 파괴 — 무시
            }
        }

        private bool _isCleanedUp;

        private void Cleanup()
        {
            if (_isCleanedUp) return;
            _isCleanedUp = true;

            _isActive = false;
            UnsubscribeEvents();
            ExitStunned();
            CleanupTweens();
            SetCanvasVisible(false);

            // 자체 게임오브젝트 제거 (별도 GO에 생성됨)
            if (gameObject != null)
                Destroy(gameObject);
        }

        #endregion

        #region HP바 갱신

        private void UpdateHpBar()
        {
            if (_bossStats == null) return;

            float ratio = _bossStats.MaxHp > 0
                ? Mathf.Clamp01((float)_bossStats.CurrentHp / _bossStats.MaxHp)
                : 0f;

            // 노란 바: 전체 HP 비율
            if (_hpBarYellowRect != null)
            {
                var size = _hpBarYellowRect.sizeDelta;
                size.x = HP_BAR_WIDTH * ratio;
                _hpBarYellowRect.sizeDelta = size;
            }

            // 빨간 바: 노란 바 뒤에서 살짝 지연 감소 (시각적 효과)
            if (_hpBarRedRect != null)
            {
                _hpBarTween?.Kill();
                float targetWidth = HP_BAR_WIDTH * ratio;
                _hpBarTween = DOTween.To(
                    () => _hpBarRedRect.sizeDelta,
                    v => _hpBarRedRect.sizeDelta = v,
                    new Vector2(targetWidth, HP_BAR_RED_HEIGHT),
                    0.5f
                ).SetEase(Ease.OutQuad).SetLink(gameObject);
            }

            // HP 퍼센트 텍스트
            if (_hpPercentText != null)
            {
                int percent = Mathf.RoundToInt(ratio * 100f);
                _hpPercentText.text = $"{percent}%";
            }
        }

        private void UpdateTimerText()
        {
            if (_timerText == null) return;

            int seconds = Mathf.CeilToInt(_remainingTime);
            int min = seconds / 60;
            int sec = seconds % 60;
            _timerText.text = $"{min:D1}:{sec:D2}";

            // 10초 이하 긴급 색상
            _timerText.color = _remainingTime <= 10f ? COLOR_TIMER_URGENT : COLOR_TIMER_NORMAL;
        }

        #endregion

        #region 경고 범위 표시

        private void ShowWarningIndicator()
        {
            DestroyWarningIndicator();

            if (_bossController == null) return;
            Vector3 bossPos = _bossController.transform.position;

            switch (_currentPattern)
            {
                case BossAttackPattern.CircularShockwave:
                    _warningIndicator = CreateCircleIndicator(bossPos, CIRCULAR_RADIUS);
                    break;

                case BossAttackPattern.LinearProjectile:
                    // Warning 시점의 플레이어 방향 기록 (이후 이동하면 회피 가능)
                    if (_player != null)
                        _attackDirection = ((Vector2)_player.transform.position - (Vector2)bossPos).normalized;
                    else
                        _attackDirection = Vector2.right;
                    _warningIndicator = CreateLineIndicator(bossPos, _attackDirection, 12f);
                    break;

                case BossAttackPattern.CrossLaser:
                    _warningIndicator = CreateCrossIndicator(bossPos, CROSS_WIDTH, CROSS_LENGTH);
                    break;
            }
        }

        private void DestroyWarningIndicator()
        {
            if (_warningIndicator != null)
            {
                Destroy(_warningIndicator);
                _warningIndicator = null;
            }
        }

        private GameObject CreateCircleIndicator(Vector3 center, float radius)
        {
            var go = new GameObject("WarningCircle");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0.1f, 0.1f, 0.3f);
            sr.sortingOrder = 90;
            go.transform.localScale = Vector3.zero;

            // 경고 시간 동안 확대 애니메이션
            float targetScale = radius * 2f;
            go.transform.DOScale(new Vector3(targetScale, targetScale, 1f), WARNING_DURATION)
                .SetEase(Ease.OutQuad)
                .SetLink(go);

            return go;
        }

        private GameObject CreateLineIndicator(Vector3 origin, Vector2 direction, float length)
        {
            var go = new GameObject("WarningLine");
            go.transform.position = origin;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRectSprite();
            sr.color = new Color(1f, 0.1f, 0.1f, 0.3f);
            sr.sortingOrder = 90;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(length, 1.5f);

            // 깜빡임 애니메이션
            DOTween.To(() => sr.color.a, a => {
                var c = sr.color; c.a = a; sr.color = c;
            }, 0.6f, 0.3f).SetLoops(-1, LoopType.Yoyo).SetLink(go);

            return go;
        }

        private GameObject CreateCrossIndicator(Vector3 center, float width, float length)
        {
            var go = new GameObject("WarningCross");
            go.transform.position = center;

            // 가로 막대
            var hBar = new GameObject("HBar");
            hBar.transform.SetParent(go.transform, false);
            var srH = hBar.AddComponent<SpriteRenderer>();
            srH.sprite = CreateRectSprite();
            srH.color = new Color(1f, 0.1f, 0.1f, 0.3f);
            srH.sortingOrder = 90;
            srH.drawMode = SpriteDrawMode.Tiled;
            srH.size = new Vector2(length, width);

            // 세로 막대
            var vBar = new GameObject("VBar");
            vBar.transform.SetParent(go.transform, false);
            var srV = vBar.AddComponent<SpriteRenderer>();
            srV.sprite = CreateRectSprite();
            srV.color = new Color(1f, 0.1f, 0.1f, 0.3f);
            srV.sortingOrder = 90;
            srV.drawMode = SpriteDrawMode.Tiled;
            srV.size = new Vector2(width, length);

            // 깜빡임
            DOTween.To(() => srH.color.a, a => {
                var c = new Color(1f, 0.1f, 0.1f, a);
                srH.color = c; srV.color = c;
            }, 0.6f, 0.3f).SetLoops(-1, LoopType.Yoyo).SetLink(go);

            return go;
        }

        // 1x1 흰 원형 스프라이트 (프로시저럴)
        private static Sprite _cachedCircleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    tex.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
                }
            tex.Apply();
            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _cachedCircleSprite;
        }

        // 1x1 흰 사각형 스프라이트 (프로시저럴)
        private static Sprite _cachedRectSprite;
        private static Sprite CreateRectSprite()
        {
            if (_cachedRectSprite != null) return _cachedRectSprite;
            int size = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            _cachedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _cachedRectSprite;
        }

        #endregion

        #region 중앙 텍스트 연출

        private void ShowCenterText(string message, Color color)
        {
            if (_centerTextPanel == null || _centerText == null) return;

            _centerTextSequence?.Kill();

            _centerTextPanel.gameObject.SetActive(true);
            _centerText.text = message;
            _centerText.color = color;

            // 배경색 (반투명 어두운)
            if (_centerTextBg != null)
            {
                var bgColor = new Color(0f, 0f, 0f, 0.6f);
                _centerTextBg.color = bgColor;
            }

            // 등장 애니메이션: 스케일 0 → 1.1 → 1
            _centerTextPanel.localScale = Vector3.zero;
            _centerTextSequence = DOTween.Sequence()
                .Append(
                    DOTween.To(
                        () => _centerTextPanel.localScale,
                        v => _centerTextPanel.localScale = v,
                        new Vector3(1.1f, 1.1f, 1f),
                        0.2f
                    ).SetEase(Ease.OutBack)
                )
                .Append(
                    DOTween.To(
                        () => _centerTextPanel.localScale,
                        v => _centerTextPanel.localScale = v,
                        Vector3.one,
                        0.1f
                    ).SetEase(Ease.InOutSine)
                )
                .SetLink(gameObject);
        }

        private void HideCenterText()
        {
            if (_centerTextPanel == null) return;

            _centerTextSequence?.Kill();

            // 페이드아웃
            _centerTextSequence = DOTween.Sequence()
                .Append(
                    DOTween.To(
                        () => _centerTextPanel.localScale,
                        v => _centerTextPanel.localScale = v,
                        Vector3.zero,
                        0.15f
                    ).SetEase(Ease.InBack)
                )
                .OnComplete(() =>
                {
                    if (_centerTextPanel != null)
                        _centerTextPanel.gameObject.SetActive(false);
                })
                .SetLink(gameObject);
        }

        private void PlayFlashOverlay()
        {
            if (_flashOverlay == null) return;

            _flashSequence?.Kill();

            _flashOverlay.color = COLOR_OVERLAY_FLASH;
            _flashSequence = DOTween.Sequence()
                .Append(
                    DOTween.To(
                        () => _flashOverlay.color,
                        c => _flashOverlay.color = c,
                        new Color(1f, 0f, 0f, 0f),
                        0.3f
                    )
                )
                .SetLink(gameObject);
        }

        #endregion

        #region UI 동적 생성

        private void BuildUI()
        {
            if (_canvas != null) return; // 이미 생성됨

            // Canvas
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORTING_ORDER;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // ── 빨간 플래시 오버레이 (전체 화면) ──
            var flashGo = CreateChild("FlashOverlay", transform);
            _flashOverlay = flashGo.AddComponent<Image>();
            _flashOverlay.color = new Color(1f, 0f, 0f, 0f);
            _flashOverlay.raycastTarget = false;
            StretchFull(flashGo.GetComponent<RectTransform>());

            // ── HP바 패널 (상단 중앙) ──
            var hpPanelGo = CreateChild("BossHpPanel", transform);
            _hpBarPanel = hpPanelGo.GetComponent<RectTransform>();
            _hpBarPanel.anchorMin = new Vector2(0.5f, 1f);
            _hpBarPanel.anchorMax = new Vector2(0.5f, 1f);
            _hpBarPanel.pivot = new Vector2(0.5f, 1f);
            _hpBarPanel.anchoredPosition = new Vector2(0f, -HP_BAR_TOP_OFFSET);
            _hpBarPanel.sizeDelta = new Vector2(HP_BAR_WIDTH + 40f, 70f);

            // HP바 패널 배경
            var panelBg = hpPanelGo.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.8f);
            panelBg.raycastTarget = false;

            // ── 보스 이름 ──
            _bossNameText = CreateTextElement("BossName", _hpBarPanel, 20, Color.white);
            var nameRect = _bossNameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(0f, 24f);
            _bossNameText.fontStyle = FontStyles.Bold;

            // ── HP바 배경 ──
            var hpBgGo = CreateChild("HpBarBg", _hpBarPanel);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 1f);
            hpBgRect.anchorMax = new Vector2(0.5f, 1f);
            hpBgRect.pivot = new Vector2(0.5f, 1f);
            hpBgRect.anchoredPosition = new Vector2(0f, -28f);
            hpBgRect.sizeDelta = new Vector2(HP_BAR_WIDTH, HP_BAR_HEIGHT + 4f);

            _hpBarBg = hpBgGo.AddComponent<Image>();
            _hpBarBg.color = COLOR_HP_BG;
            _hpBarBg.raycastTarget = false;

            // ── HP바 빨간색 (지연 감소용, 노란 바 뒤) ──
            var hpRedGo = CreateChild("HpBarRed", hpBgRect);
            _hpBarRedRect = hpRedGo.GetComponent<RectTransform>();
            _hpBarRedRect.anchorMin = new Vector2(0f, 0f);
            _hpBarRedRect.anchorMax = new Vector2(0f, 1f);
            _hpBarRedRect.pivot = new Vector2(0f, 0.5f);
            _hpBarRedRect.anchoredPosition = Vector2.zero;
            _hpBarRedRect.sizeDelta = new Vector2(HP_BAR_WIDTH, 0f);

            _hpBarRedImage = hpRedGo.AddComponent<Image>();
            _hpBarRedImage.color = COLOR_HP_RED;
            _hpBarRedImage.raycastTarget = false;

            // ── HP바 노란색 (실제 HP) ──
            var hpYellowGo = CreateChild("HpBarYellow", hpBgRect);
            _hpBarYellowRect = hpYellowGo.GetComponent<RectTransform>();
            _hpBarYellowRect.anchorMin = new Vector2(0f, 0f);
            _hpBarYellowRect.anchorMax = new Vector2(0f, 1f);
            _hpBarYellowRect.pivot = new Vector2(0f, 0.5f);
            _hpBarYellowRect.anchoredPosition = Vector2.zero;
            _hpBarYellowRect.sizeDelta = new Vector2(HP_BAR_WIDTH, 0f);

            _hpBarYellowImage = hpYellowGo.AddComponent<Image>();
            _hpBarYellowImage.color = COLOR_HP_YELLOW;
            _hpBarYellowImage.raycastTarget = false;

            // ── HP 퍼센트 텍스트 (바 위에 중앙) ──
            _hpPercentText = CreateTextElement("HpPercent", hpBgRect, 14, Color.white);
            var hpTextRect = _hpPercentText.GetComponent<RectTransform>();
            StretchFull(hpTextRect);
            _hpPercentText.alignment = TextAlignmentOptions.Center;
            _hpPercentText.fontStyle = FontStyles.Bold;

            // ── 타이머 텍스트 (HP바 아래) ──
            _timerText = CreateTextElement("Timer", _hpBarPanel, 18, Color.white);
            var timerRect = _timerText.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0f, 1f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -52f);
            timerRect.sizeDelta = new Vector2(0f, 22f);
            _timerText.alignment = TextAlignmentOptions.Center;

            // ── 중앙 경고/무력화 텍스트 패널 ──
            var centerGo = CreateChild("CenterTextPanel", transform);
            _centerTextPanel = centerGo.GetComponent<RectTransform>();
            _centerTextPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _centerTextPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _centerTextPanel.pivot = new Vector2(0.5f, 0.5f);
            _centerTextPanel.anchoredPosition = new Vector2(0f, 100f);
            _centerTextPanel.sizeDelta = new Vector2(700f, 60f);

            _centerTextBg = centerGo.AddComponent<Image>();
            _centerTextBg.color = new Color(0f, 0f, 0f, 0.6f);
            _centerTextBg.raycastTarget = false;

            _centerText = CreateTextElement("CenterText", _centerTextPanel, 28, Color.white);
            var centerTextRect = _centerText.GetComponent<RectTransform>();
            StretchFull(centerTextRect);
            _centerText.alignment = TextAlignmentOptions.Center;
            _centerText.fontStyle = FontStyles.Bold;

            _centerTextPanel.gameObject.SetActive(false);
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = false; // 게임 플레이를 방해하지 않음
            }
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        #endregion

        #region UI 유틸

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text CreateTextElement(string name, Transform parent, int fontSize, Color color)
        {
            var go = CreateChild(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
