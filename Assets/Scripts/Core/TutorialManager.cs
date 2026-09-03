using UnityEngine;
using MkLike.Utils;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace MkLike.Core
{
    /// <summary>
    /// 튜토리얼 진행 관리 매니저.
    /// 게임 이벤트를 구독하여 조건 충족 시 튜토리얼 단계를 진행하고,
    /// 대사/하이라이트/보상 지급을 처리한다.
    /// SaveManager를 통해 진행 상태를 영속화한다.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("튜토리얼 단계 설정")]
        [SerializeField] private TutorialStepConfig[] _stepConfigs;

        [Header("튜토리얼 UI")]
        [SerializeField] private MonoBehaviour _dialogueUIObject;
        [SerializeField] private MonoBehaviour _highlightMaskObject;

        private ITutorialDialogue _dialogueUI;
        private ITutorialHighlight _highlightMask;

        private TutorialSaveData _saveData;
        private TutorialStep _currentStep;
        private bool _isTutorialActive;
        private bool _isStepInProgress;
        private int _killCount;
        private CancellationTokenSource _cts;
        private Dictionary<TutorialStep, TutorialStepConfig> _configMap;

        public bool IsTutorialActive => _isTutorialActive;
        public TutorialStep CurrentStep => _currentStep;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildConfigMap();
            CacheUIReferences();
        }

        private void Start()
        {
            LoadTutorialState();

            if (_currentStep < TutorialStep.Completed && _currentStep < TutorialStep.TabIntroduction)
            {
                _isTutorialActive = true;
            }

            // 신규 유저: 튜토리얼 첫 단계 자동 시작
            if (_currentStep == TutorialStep.None)
            {
                AdvanceToStep(TutorialStep.Intro);
            }
        }

        private void OnEnable()
        {
            _cts = new CancellationTokenSource();
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
            // 2026-04-23 QA 감사 P1: Start에서 OnEnable로 이동 — OnDisable Unsubscribe와 쌍 유지 (Scene 재활성 시 먹통 방지)
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SaveTutorialState();

        private void OnLoadCompleted(LoadCompletedEvent evt) => LoadTutorialState();

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BuildConfigMap()
        {
            _configMap = new Dictionary<TutorialStep, TutorialStepConfig>();
            if (_stepConfigs == null) return;

            for (int i = 0; i < _stepConfigs.Length; i++)
            {
                var config = _stepConfigs[i];
                if (!_configMap.ContainsKey(config.step))
                {
                    _configMap[config.step] = config;
                }
            }
        }

        private void CacheUIReferences()
        {
            if (_dialogueUIObject != null)
                _dialogueUI = _dialogueUIObject as ITutorialDialogue;
            if (_highlightMaskObject != null)
                _highlightMask = _highlightMaskObject as ITutorialHighlight;

            if (_dialogueUIObject != null && _dialogueUI == null)
                Debug.Log("[TutorialManager] _dialogueUIObject가 ITutorialDialogue를 구현하지 않음");
            if (_highlightMaskObject != null && _highlightMask == null)
                Debug.Log("[TutorialManager] _highlightMaskObject가 ITutorialHighlight를 구현하지 않음");
        }

        private TutorialStepConfig GetStepConfig(TutorialStep step)
        {
            if (_configMap != null && _configMap.TryGetValue(step, out var config))
            {
                return config;
            }
            return null;
        }

        // ── 세이브/로드 ──

        private void LoadTutorialState()
        {
            var saveManager = Save.SaveManager.Instance;
            if (saveManager != null && saveManager.CurrentData != null)
            {
                _saveData = saveManager.CurrentData.tutorial ?? new TutorialSaveData();
                _currentStep = _saveData.completedStep;
            }
            else
            {
                _saveData = new TutorialSaveData();
                _currentStep = TutorialStep.None;
            }
        }

        private void SaveTutorialState()
        {
            _saveData.completedStep = _currentStep;
            var saveManager = Save.SaveManager.Instance;
            if (saveManager != null && saveManager.CurrentData != null)
            {
                saveManager.CurrentData.tutorial = _saveData;
            }
        }

        // ── 이벤트 구독 ──

        // 가이드 퀘스트 chainIndex → TutorialStep 매핑 (탭 해금 트리거)
        // 해당 chainIndex의 퀘스트가 완료되면 TutorialStep 진행 → 다음 퀘스트에 필요한 탭 해금
        // 14종 순환 기반 350개 퀘스트 체인 (guide-quest-design.md 참조)
        private static readonly Dictionary<int, TutorialStep> _guideToStepMap = new()
        {
            { 3,  TutorialStep.FirstGacha },       // Q3 사냥연습 완료 → 소환 탭 해금 (Q4 첫 소환 가능)
            { 4,  TutorialStep.FirstEquipment },   // Q4 첫 소환 완료 → 장비 탭 해금 (Q5 장비 장착 가능)
            { 7,  TutorialStep.SkillUnlock },      // Q7 몬스터 30마리 완료 → 스킬 탭 해금 (Q8 스킬 강화 가능)
            { 12, TutorialStep.WeaponSummon },     // Q12 챕터1 클리어 완료 → 무기 탭 해금 (Q13 무기 소환 가능)
            { 96, TutorialStep.CompanionIntro },   // Q96 동료 출격 해금 오버라이드
        };

        private void SubscribeEvents()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<SkillLearnedEvent>.Subscribe(OnSkillLearned);
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<GachaResultEvent>.Subscribe(OnGachaCompleted);
            EventBus<GuideQuestCompletedEvent>.Subscribe(OnGuideQuestCompleted);
        }

        private void UnsubscribeEvents()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<SkillLearnedEvent>.Unsubscribe(OnSkillLearned);
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaCompleted);
            EventBus<GuideQuestCompletedEvent>.Unsubscribe(OnGuideQuestCompleted);
        }

        // ── 이벤트 핸들러 ──

        private void OnMonsterDied(MonsterDiedEvent e)
        {
            _killCount++;

            if (_currentStep == TutorialStep.FirstBattle && _killCount >= 10)
            {
                AdvanceToStep(TutorialStep.FirstKill);
            }
        }

        private void OnLevelUp(LevelUpEvent e)
        {
            // 기본 튜토리얼만 자동 진행 (탭 해금은 가이드 퀘스트 브릿지가 관리)
            if (_currentStep < TutorialStep.FirstLevelUp && e.CurrentLevel >= 2)
            {
                AdvanceToStep(TutorialStep.FirstLevelUp);
            }
        }

        private void OnEquipmentChanged(EquipmentChangedEvent e)
        {
            if (_currentStep == TutorialStep.FirstEquipment && e.IsEquipped)
            {
                CompleteCurrentStep();
            }
        }

        private void OnStageChanged(StageChangedEvent e)
        {
            // 기본 튜토리얼만 자동 진행 (탭 해금은 가이드 퀘스트 브릿지가 관리)
            if (e.Chapter == 1 && e.StageIndex >= 2 && _currentStep < TutorialStep.FirstMiniBoss)
            {
                AdvanceToStep(TutorialStep.FirstMiniBoss);
            }
        }

        private void OnSkillLearned(SkillLearnedEvent e)
        {
            if (_currentStep == TutorialStep.SkillUnlock)
            {
                CompleteCurrentStep();
            }
        }

        private void OnDungeonCompleted(DungeonCompletedEvent e)
        {
            if (_currentStep == TutorialStep.DungeonUnlock)
            {
                _saveData.hasCompletedDungeonIntro = true;
                CompleteCurrentStep();
            }
        }

        private void OnGachaCompleted(GachaResultEvent e)
        {
            if (_currentStep == TutorialStep.FirstGacha)
            {
                _saveData.hasCompletedFirstGacha = true;
                CompleteCurrentStep();
            }
        }

        private void OnGuideQuestCompleted(GuideQuestCompletedEvent e)
        {
            if (!_guideToStepMap.TryGetValue(e.ChainIndex, out TutorialStep targetStep))
                return;

            // IsTabUnlocked()은 >= 비교이므로, 현재 step이 높으면 하위 탭도 자동 해금
            if (targetStep > _currentStep)
            {
                AdvanceToStep(targetStep);
                Debug.Log($"[TutorialManager] 가이드 #{e.ChainIndex} 완료 → {targetStep} 진행");
            }
        }

        // ── 튜토리얼 진행 ──

        /// <summary>
        /// 지정된 튜토리얼 단계로 진행한다.
        /// 이미 진행 중이거나 이미 완료된 단계이면 무시한다.
        /// </summary>
        public void AdvanceToStep(TutorialStep step)
        {
            if (_isStepInProgress || step <= _currentStep) return;

            _currentStep = step;
            _isStepInProgress = true;

            EventBus<TutorialStepStartedEvent>.Publish(new TutorialStepStartedEvent { Step = step });

            var stepConfig = GetStepConfig(step);
            if (stepConfig != null)
            {
                ExecuteStepAsync(stepConfig, _cts.Token).Forget();
            }
            else
            {
                CompleteCurrentStep();
            }
        }

        private async UniTaskVoid ExecuteStepAsync(TutorialStepConfig config, CancellationToken token)
        {
            if (config.delayBefore > 0f)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(config.delayBefore),
                    cancellationToken: token);
            }

            // 전투 일시 정지
            if (config.isPauseCombat)
            {
                EventBus<TutorialPauseCombatEvent>.Publish(new TutorialPauseCombatEvent());
                Time.timeScale = 0f;
            }

            // 보상 지급 (이벤트를 통해 Economy 레이어에 위임)
            if (config.rewards != null)
            {
                for (int i = 0; i < config.rewards.Length; i++)
                {
                    var reward = config.rewards[i];
                    if (reward.amount > 0)
                    {
                        EventBus<TutorialRewardRequestEvent>.Publish(
                            new TutorialRewardRequestEvent
                            {
                                CurrencyType = reward.currencyType,
                                Amount = reward.amount
                            });
                    }
                }
            }

            // 하이라이트 마스크 표시
            bool isHighlightShown = false;
            if (!string.IsNullOrEmpty(config.highlightTarget) && _highlightMask != null)
            {
                var targetGo = GameObject.Find(config.highlightTarget);
                if (targetGo != null)
                {
                    var rt = targetGo.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        await _highlightMask.ShowHighlight(rt, config.arrowDirection, token);
                        isHighlightShown = true;
                    }
                }
                else
                {
                    Debug.Log($"[TutorialManager] 하이라이트 대상 '{config.highlightTarget}'을 찾을 수 없음");
                }
            }

            // 대사 표시
            if (!string.IsNullOrEmpty(config.dialogueText))
            {
                if (_dialogueUI != null)
                {
                    await _dialogueUI.ShowDialogue(config.dialogueText, "가이드", "", true, token);
                    await _dialogueUI.HideDialogue(token);
                }
                else
                {
                    Debug.Log($"[TutorialManager] {config.dialogueText}");
                    await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);
                }
            }

            // 하이라이트 해제
            if (isHighlightShown && _highlightMask != null)
            {
                await _highlightMask.HideHighlight(token);
            }

            // 전투 재개
            if (config.isPauseCombat)
            {
                Time.timeScale = 1f;
                EventBus<TutorialResumeCombatEvent>.Publish(new TutorialResumeCombatEvent());
            }

            // 강제 가이드가 아니면 자동 완료
            if (!config.isForced)
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// 현재 튜토리얼 단계를 완료 처리하고 다음 단계로 진행한다.
        /// 강제 가이드 단계에서 조건 충족 시 외부에서 호출한다.
        /// </summary>
        public void CompleteCurrentStep()
        {
            _isStepInProgress = false;

            EventBus<TutorialStepCompletedEvent>.Publish(
                new TutorialStepCompletedEvent { Step = _currentStep });

            SaveTutorialState();

            var stepConfig = GetStepConfig(_currentStep);
            if (stepConfig != null && stepConfig.nextStep != TutorialStep.None)
            {
                AdvanceToStep(stepConfig.nextStep);
            }
            else if (_currentStep >= TutorialStep.PotentialIntro)
            {
                _currentStep = TutorialStep.Completed;
                _isTutorialActive = false;
                SaveTutorialState();
                Debug.Log("[TutorialManager] 모든 튜토리얼 완료!");
            }
        }

        // ── 마일스톤 보상 ──

        /// <summary>
        /// 마일스톤 보상을 청구한다. 이미 수령한 경우 false를 반환한다.
        /// 보상 지급은 TutorialRewardRequestEvent로 Economy 레이어에 위임한다.
        /// </summary>
        public bool TryClaimMilestone(string milestoneId, CurrencyType type, int amount)
        {
            if (_saveData.claimedMilestones.Contains(milestoneId))
                return false;

            _saveData.claimedMilestones.Add(milestoneId);
            EventBus<TutorialRewardRequestEvent>.Publish(
                new TutorialRewardRequestEvent
                {
                    CurrencyType = type,
                    Amount = amount
                });
            SaveTutorialState();
            return true;
        }

        /// <summary>
        /// 마일스톤 보상을 이미 수령했는지 확인한다.
        /// </summary>
        public bool IsMilestoneClaimed(string milestoneId)
        {
            return _saveData.claimedMilestones.Contains(milestoneId);
        }

        // ── 직업 선택 ──

        /// <summary>
        /// 직업 선택 완료 시 호출한다.
        /// </summary>
        public void OnJobSelected()
        {
            _saveData.hasSelectedJob = true;
            if (_currentStep == TutorialStep.JobSelection)
            {
                CompleteCurrentStep();
            }
        }
    }

    // ── 튜토리얼 단계 설정 (인스펙터용) ──

    /// <summary>
    /// 튜토리얼 단계별 설정 데이터.
    /// TutorialManager의 SerializeField 배열로 인스펙터에서 설정한다.
    /// </summary>
    [System.Serializable]
    public class TutorialStepConfig
    {
        public TutorialStep step;

        [TextArea(2, 4)]
        public string dialogueText;

        public string highlightTarget;
        public ArrowDirection arrowDirection;
        public bool isPauseCombat;
        public bool isForced;
        public float delayBefore;
        public TutorialStepReward[] rewards;
        public TutorialStep nextStep;
    }

    /// <summary>
    /// 튜토리얼 보상 데이터.
    /// </summary>
    [System.Serializable]
    public class TutorialStepReward
    {
        public CurrencyType currencyType;
        public int amount;
    }

    /// <summary>
    /// 튜토리얼 보상 요청 이벤트.
    /// Economy 레이어(CurrencyManager)가 구독하여 실제 재화를 지급한다.
    /// Core에서 Economy를 직접 참조하지 않기 위한 이벤트 기반 설계.
    /// </summary>
    public struct TutorialRewardRequestEvent : IEvent
    {
        public CurrencyType CurrencyType;
        public int Amount;
    }

    /// <summary>
    /// 튜토리얼 대사 UI 인터페이스.
    /// UI 레이어(TutorialDialogueUI)가 구현한다.
    /// Core→UI 순환 참조를 방지하기 위한 추상화.
    /// </summary>
    public interface ITutorialDialogue
    {
        UniTask ShowDialogue(string text, string npcName = null, string npcGrade = null,
            bool isSkippable = false, CancellationToken token = default);
        UniTask HideDialogue(CancellationToken token = default);
    }

    /// <summary>
    /// 튜토리얼 하이라이트 마스크 인터페이스.
    /// UI 레이어(TutorialHighlightMask)가 구현한다.
    /// Core→UI 순환 참조를 방지하기 위한 추상화.
    /// </summary>
    public interface ITutorialHighlight
    {
        UniTask ShowHighlight(RectTransform target, ArrowDirection direction = ArrowDirection.Down,
            CancellationToken token = default);
        UniTask HideHighlight(CancellationToken token = default);
    }
}
