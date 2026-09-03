using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using MkLike.Combat;

namespace MkLike.UI
{
    /// <summary>
    /// 스테이지 진행 HUD. 웨이브/스테이지 텍스트 + 처치 진행 바 + 보스 경고.
    /// StageChangedEvent, MonsterDiedEvent 구독.
    /// </summary>
    public class WaveProgressHUD : MonoBehaviour
    {
        [Header("웨이브/스테이지 텍스트")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _stageText;

        [Header("진행 바")]
        [SerializeField] private Image _progressFill;

        [Header("보스 경고")]
        [SerializeField] private TMP_Text _bossWarningText;

        [Header("몬스터 카운트")]
        [SerializeField] private TMP_Text _monsterCountText;

        [Header("설정")]
        [SerializeField] private int _monstersPerStage = 100;

        private int _killCount;
        private int _currentChapter = 1;
        private int _currentStageIndex = 1;
        private bool _isBossStage;
        private Tween _bossBlink;

        private void OnEnable()
        {
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
        }

        private void OnDisable()
        {
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
        }

        private void Start()
        {
            if (_bossWarningText != null)
            {
                _bossWarningText.gameObject.SetActive(false);
                _bossWarningText.alpha = 0f;
            }

            RefreshProgress();
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _currentChapter = evt.Chapter;
            _currentStageIndex = evt.StageIndex;
            _killCount = 0;
            _isBossStage = evt.DisplayName.Contains("BOSS");

            // 스테이지 텍스트 갱신
            if (_stageText != null)
                _stageText.text = $"스테이지 {evt.DisplayName}";

            // 웨이브 텍스트 갱신 (챕터 내 스테이지 진행도)
            if (_waveText != null)
            {
                if (_isBossStage)
                    _waveText.text = "BOSS";
                else
                    _waveText.text = $"Wave {_currentStageIndex}/9";
            }

            // 텍스트 펀치 연출
            if (_stageText != null)
            {
                _stageText.transform.DOKill();
                _stageText.transform.localScale = Vector3.one;
                _stageText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_waveText != null)
            {
                _waveText.transform.DOKill();
                _waveText.transform.localScale = Vector3.one;
                _waveText.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 보스 경고
            UpdateBossWarning();
            RefreshProgress();
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _killCount++;
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            float ratio = _monstersPerStage > 0
                ? Mathf.Clamp01((float)_killCount / _monstersPerStage)
                : 0f;

            if (_progressFill != null)
                _progressFill.fillAmount = ratio;

            int remaining = Mathf.Max(0, _monstersPerStage - _killCount);
            if (_monsterCountText != null)
                _monsterCountText.text = $"{_killCount}/{_monstersPerStage}";
        }

        private void UpdateBossWarning()
        {
            if (_bossWarningText == null) return;

            // 보스 스테이지: "BOSS" 깜빡 연출
            if (_isBossStage)
            {
                _bossWarningText.gameObject.SetActive(true);
                _bossWarningText.text = "BOSS";

                _bossBlink?.Kill();
                _bossWarningText.alpha = 0f;
                _bossBlink = DOTween.To(() => _bossWarningText.alpha, x => _bossWarningText.alpha = x, 1f, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
            else
            {
                _bossBlink?.Kill();
                _bossWarningText.alpha = 0f;
                _bossWarningText.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _bossBlink?.Kill();
            if (_stageText != null) _stageText.transform.DOKill();
            if (_waveText != null) _waveText.transform.DOKill();
            if (_bossWarningText != null) DOTween.Kill(_bossWarningText);
        }
    }
}
