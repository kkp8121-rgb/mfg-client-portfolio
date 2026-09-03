using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Core.Save;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 캐릭터 스탯 탭 컨트롤러.
    /// UIDocument에 연결하여 기존 게임 시스템(LevelSystem, CombatStats)과 바인딩한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CharacterStatTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // 캐싱된 요소
        private VisualElement _portrait;
        private Label _levelText;
        private Label _cpText;
        private Label _gradeText;
        private Label _pointsText;

        // 스탯 이름 목록 (순서 일치)
        private static readonly string[] STAT_KEYS = { "atk", "hp", "def", "crit", "acc", "aspd", "cdmg", "bdmg" };
        private static readonly string[] STAT_NAMES = { "공격력", "최대HP", "방어력", "치명타", "명중", "공격속도", "크리뎀", "보스뎀" };

        // 서브탭 패널 이름 (탭 인덱스 1~에 대응, 인덱스 0은 자기 자신의 content-area)
        // 스킬은 독립 탭으로 분리됨, 동반자(Climber)는 삭제됨
        // 2026-04-20 Char_Relic 제거 — 유물 시스템 완전 제거
        private static readonly string[] SUBTAB_PANEL_NAMES =
        {
            "[UITK] Char_Job"
        };

        // 캐싱된 서브탭 패널 GameObject (인덱스 0=Job)
        private GameObject[] _subtabPanels;

        // 스탯 콘텐츠 영역 (탭 0일 때만 표시)
        private VisualElement _contentArea;

        private Label[] _valueLabels;
        private Label[] _summaryValueLabels; // 하단 수치표 동기화용
        private VisualElement[] _barFills;

        // 게임 시스템 참조
        private LevelSystem _levelSystem;
        private CombatStats _combatStats;

        // 스탯 분배 포인트
        private int[] _allocatedPoints;
        private static readonly int[] ATK_PER_POINT = { 2, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly int[] HP_PER_POINT  = { 0, 10, 0, 0, 0, 0, 0, 0 };
        private static readonly int[] DEF_PER_POINT = { 0, 0, 1, 0, 0, 0, 0, 0 };

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50; // 2026-04-23 침묵 버그 #5 대응: 탭별 고유값 (기준)
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[CharacterStatTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButton(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("root");

            CacheElements();
            CacheSubtabPanels();
            BindButtons();
            BindTabs();

            // 게임 시스템 탐색
            _levelSystem = FindFirstObjectByType<LevelSystem>();
            _combatStats = FindFirstObjectByType<PlayerCharacter>()?.GetComponent<CombatStats>();

            _allocatedPoints = new int[STAT_KEYS.Length];

            // 이벤트 구독
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);

            // 기본 서브탭: 스탯(0)
            SwitchTab(0);
            RefreshAll();

            // 2026-04-23 timing fix: PlayerCharacter/CombatStats가 아직 생성 전이면 1프레임 뒤 재탐색
            if (_combatStats == null)
                RetryCombatStatsLookup().Forget();

            PlayOpenAnimation();
        }

        /// <summary>OnEnable 시점에 PlayerCharacter 미생성이면 1프레임 대기 후 재조회 + 재렌더.</summary>
        private async UniTaskVoid RetryCombatStatsLookup()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            await UniTask.NextFrame(ct);
            if (this == null) return;
            _combatStats = FindFirstObjectByType<PlayerCharacter>()?.GetComponent<CombatStats>();
            if (_combatStats != null)
                RefreshAll();
        }

        /// <summary>서브탭 패널 GameObject를 캐싱 (비활성 오브젝트 포함)</summary>
        private void CacheSubtabPanels()
        {
            _contentArea = _root.Q<VisualElement>(className: "content-area");

            _subtabPanels = new GameObject[SUBTAB_PANEL_NAMES.Length];
            var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < SUBTAB_PANEL_NAMES.Length; i++)
            {
                foreach (var doc in docs)
                {
                    if (doc.gameObject.name == SUBTAB_PANEL_NAMES[i])
                    {
                        _subtabPanels[i] = doc.gameObject;
                        break;
                    }
                }
                if (_subtabPanels[i] == null)
                    Debug.LogWarning($"[CharacterStatTabUI] 서브탭 패널 못 찾음: {SUBTAB_PANEL_NAMES[i]}");
            }
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
        }

        private async void PlayOpenAnimation()
        {
            if (_contentRoot == null) return;
            _contentRoot.RemoveFromClassList("panel--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _contentRoot.AddToClassList("panel--open");
        }

        private void CacheElements()
        {
            _portrait = _root.Q<VisualElement>(className: "info-row__portrait");
            _levelText = _root.Q<Label>("level-text");
            _cpText = _root.Q<Label>("cp-text");
            _gradeText = _root.Q<Label>("grade-text");
            _pointsText = _root.Q<Label>("points-text");

            _valueLabels = new Label[STAT_KEYS.Length];
            _summaryValueLabels = new Label[STAT_KEYS.Length];
            _barFills = new VisualElement[STAT_KEYS.Length];

            for (int i = 0; i < STAT_KEYS.Length; i++)
            {
                _valueLabels[i] = _root.Q<Label>($"val-{STAT_KEYS[i]}");
                _summaryValueLabels[i] = _root.Q<Label>($"summary-val-{STAT_KEYS[i]}");
                _barFills[i] = _root.Q<VisualElement>($"bar-{STAT_KEYS[i]}");
            }
        }

        private void BindButtons()
        {
            for (int i = 0; i < 5; i++) // 일반 스탯만 (0~4)
            {
                int idx = i;
                var btn1 = _root.Q<Button>($"btn-{STAT_KEYS[i]}-1");
                var btn10 = _root.Q<Button>($"btn-{STAT_KEYS[i]}-10");
                var btnMax = _root.Q<Button>($"btn-{STAT_KEYS[i]}-max");

                btn1?.RegisterCallback<ClickEvent>(_ => AllocateStat(idx, 1));
                btn10?.RegisterCallback<ClickEvent>(_ => AllocateStat(idx, 10));
                btnMax?.RegisterCallback<ClickEvent>(_ => AllocateStatMax(idx));
            }

            // 자동분배
            var autoBtn = _root.Q<Button>("auto-btn");
            autoBtn?.RegisterCallback<ClickEvent>(_ => AutoAllocate());

            // 초기화 버튼 제거됨 (2026-04-23): UXML에서 삭제. ResetStats()도 dead code
        }

        // 2026-04-20 "relic" 탭 제거 — 유물 시스템 완전 제거
        private static readonly string[] TAB_NAMES = { "stat", "job" };

        private void BindTabs()
        {
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                int idx = i;
                var tab = _root.Q<VisualElement>($"tab-{TAB_NAMES[i]}");
                tab?.RegisterCallback<ClickEvent>(_ => SwitchTab(idx));
            }
        }

        private void SwitchTab(int index)
        {
            // 탭 활성 상태 시각 전환
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                var tab = _root.Q<VisualElement>($"tab-{TAB_NAMES[i]}");
                if (tab == null) continue;
                if (i == index)
                    tab.AddToClassList("subtab--active");
                else
                    tab.RemoveFromClassList("subtab--active");
            }

            // 인덱스 0 = 스탯(자기 자신의 content-area), 1~6 = 외부 패널
            // content-area: 스탯 탭일 때만 표시
            if (_contentArea != null)
                _contentArea.style.display = (index == 0)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            // 외부 서브탭 패널 활성화/비활성화 (SUBTAB_PANEL_NAMES 인덱스 = tabIndex - 1)
            if (_subtabPanels != null)
            {
                for (int i = 0; i < _subtabPanels.Length; i++)
                {
                    if (_subtabPanels[i] != null)
                        _subtabPanels[i].SetActive(i == index - 1);
                }
            }

            Debug.Log($"[CharacterStatTabUI] 탭 전환: {TAB_NAMES[index]}");
        }

        /// <summary>서브탭에서 스탯 탭으로 돌아온다.</summary>
        public void ReturnToStatTab()
        {
            SwitchTab(0);
        }

        // ── 스탯 분배 ──

        private void AllocateStat(int index, int amount)
        {
            if (_levelSystem == null) return;
            int available = _levelSystem.AvailableStatPoints;
            int toAllocate = Mathf.Min(amount, available);
            if (toAllocate <= 0) return;

            if (!_levelSystem.ConsumeStatPoints(toAllocate)) return;

            _allocatedPoints[index] += toAllocate;

            // 기존 CharacterPanel.ApplyStatBonus와 동일한 방식으로 스탯 적용
            if (_combatStats != null)
            {
                string key = $"StatAlloc_{STAT_KEYS[index]}";
                var mod = new StatModifier(
                    ModifierSource.StatAllocation, key,
                    GetStatType(index),
                    GetStatBonusPerPoint(index) * toAllocate, 0f);
                _combatStats.AddModifier(key, mod);
            }

            RefreshAll();

            // 퀘스트 진행도 추적용 이벤트 발행
            EventBus.Publish(new StatAllocatedEvent
            {
                StatName = STAT_KEYS[index],
                PointsSpent = toAllocate,
                TotalAllocated = _allocatedPoints[index]
            });

            // 2026-04-23 이슈 15 FeedbackBus: 스탯 분배 Toast (포인트 충분히 들었을 때만)
            if (toAllocate >= 5)
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.StatAllocate,
                    $"{STAT_NAMES[index]} +{toAllocate}");
            }
        }

        private void AllocateStatMax(int index)
        {
            if (_levelSystem == null) return;
            AllocateStat(index, _levelSystem.AvailableStatPoints);
        }

        private void AutoAllocate()
        {
            if (_levelSystem == null) return;
            int pts = _levelSystem.AvailableStatPoints;
            if (pts <= 0) return;

            // 공격력 우선 분배
            AllocateStat(0, pts);
        }

        // ResetStats() 제거됨 (2026-04-23): 버튼 UXML + 바인딩 함께 제거. 유저 혼란 유발 + 미구현 TODO

        // ── 갱신 ──

        private void RefreshAll()
        {
            RefreshGrade();
            RefreshInfoRow();
            RefreshPointsRow();
            RefreshStatRows();
        }

        private JobType GetCurrentJob()
        {
            var saveData = SaveManager.Instance?.CurrentData;
            if (saveData == null) return JobType.Warrior;
            return saveData.player.jobId switch
            {
                "archer" => JobType.Archer,
                "mage" => JobType.Mage,
                _ => JobType.Warrior
            };
        }

        private static string GetJobDisplayName(JobType job) => job switch
        {
            JobType.Warrior => "전사",
            JobType.Archer => "궁수",
            JobType.Mage => "마법사",
            _ => "전사"
        };

        private void RefreshInfoRow()
        {
            // 초상화: SPUM 프리뷰 캡처 → 실패 시 정적 초상화 폴백
            JobType currentJob = GetCurrentJob();

            if (_portrait != null)
            {
                // 2026-04-20 SPUM 단일 원천 통일 — CharacterPreviewRenderer 실시간 렌더 우선.
                // Tier 승급 반영 + 런타임 Player와 자동 동일 비주얼.
                Texture2D runtimeTex = null;
                if (CharacterPreviewRenderer.Instance != null)
                    runtimeTex = CharacterPreviewRenderer.Instance.CapturePlayerPreview();

                if (runtimeTex != null)
                {
                    _portrait.style.backgroundImage = new StyleBackground(runtimeTex);
                }
                else
                {
                    // Fallback: 정적 초상화 (Renderer 미준비 시)
                    var sprite = PortraitProvider.GetPortrait(currentJob);
                    if (sprite == null)
                        sprite = Resources.Load<Sprite>("Icons/Portraits/portrait_warrior");
                    if (sprite != null)
                        _portrait.style.backgroundImage = new StyleBackground(sprite);
                }
            }

            // 등급 텍스트에 직업명 표시
            if (_gradeText != null)
                _gradeText.text = GetJobDisplayName(currentJob);

            if (_levelText != null)
            {
                _levelText.text = _levelSystem != null
                    ? $"Lv.{_levelSystem.CurrentLevel}"
                    : "Lv.1";
            }

            if (_cpText != null)
            {
                if (_combatStats != null)
                {
                    long cp = CombatFormula.CalculateCP(_combatStats);
                    _cpText.text = $"전투력 {HudPanel.FormatGold(cp)}";
                }
                else
                {
                    _cpText.text = "전투력 ---";
                }
            }
        }

        private void RefreshPointsRow()
        {
            int avail = _levelSystem != null ? _levelSystem.AvailableStatPoints : 0;
            if (_pointsText != null)
                _pointsText.text = avail.ToString();

            // 이슈 13 가격 표기 (2026-04-23): "최대" 버튼은 현재 보유 포인트 전체를 소비
            // atk/def/hp 3종 공통. +1은 1P, +10은 10P 고정 — 보유 부족 시 AllocateStat에서 Min 클램프
            for (int i = 0; i < 3; i++)
            {
                var key = STAT_KEYS[i];
                var maxLabel = _root.Q<Label>($"cost-{key}-max");
                if (maxLabel != null)
                    maxLabel.text = avail > 0 ? $"{avail}P" : "0P";
            }
        }

        private void RefreshStatRows()
        {
            if (_combatStats == null)
            {
                // 폴백: 기본값 표시
                for (int i = 0; i < STAT_KEYS.Length; i++)
                {
                    if (_valueLabels[i] != null)
                        _valueLabels[i].text = "0";
                    if (_summaryValueLabels[i] != null)
                        _summaryValueLabels[i].text = "0";
                    if (_barFills[i] != null)
                        _barFills[i].style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
                }
                return;
            }

            // 실제 CombatStats 값 바인딩 (공격속도/크리뎀은 float, 보스뎀은 미구현)
            float[] rawValues = {
                _combatStats.Atk,
                _combatStats.MaxHp,
                _combatStats.Def,
                _combatStats.CritRate * 100f,
                0f, // 명중 (미구현)
                _combatStats.AttackSpeed,
                (_combatStats.CritDmg - 1f) * 100f, // 기본 1.5x → 50% 표시
                0f  // 보스뎀 (미구현)
            };

            // 포맷 타입: 0=정수(K/M), 1=퍼센트, 2=소수점
            // atk, hp, def = 정수 / crit, acc, bossdmg = 퍼센트 / aspd = 소수점 / cdmg = 퍼센트
            int[] formatType = { 0, 0, 0, 1, 1, 2, 1, 1 };

            for (int i = 0; i < STAT_KEYS.Length; i++)
            {
                string formatted = formatType[i] switch
                {
                    1 => $"{rawValues[i]:F1}%",
                    2 => $"{rawValues[i]:F2}",
                    _ => HudPanel.FormatGold((long)rawValues[i])
                };

                if (_valueLabels[i] != null)
                    _valueLabels[i].text = formatted;

                // 하단 수치표 동기화
                if (_summaryValueLabels[i] != null)
                    _summaryValueLabels[i].text = formatted;

                if (_barFills[i] != null)
                {
                    float pct = Mathf.Clamp01(_allocatedPoints[i] / 100f) * 100f;
                    _barFills[i].style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
                }
            }

            // 버튼 활성화 상태
            bool canAllocate = _levelSystem != null && _levelSystem.AvailableStatPoints > 0;
            for (int i = 0; i < 5; i++)
            {
                SetButtonEnabled($"btn-{STAT_KEYS[i]}-1", canAllocate);
                SetButtonEnabled($"btn-{STAT_KEYS[i]}-10", canAllocate);
                SetButtonEnabled($"btn-{STAT_KEYS[i]}-max", canAllocate);
            }
        }

        private void SetButtonEnabled(string name, bool enabled)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.SetEnabled(enabled);
        }

        // ── 이벤트 핸들러 ──

        private void OnLevelUp(LevelUpEvent evt) => RefreshAll();
        private void OnCurrencyChanged(CurrencyChangedEvent evt) => RefreshAll();
        private void OnJobChanged(JobChangedEvent evt) => RefreshAll();

        private static StatType GetStatType(int index) => index switch
        {
            0 => StatType.Atk,
            1 => StatType.MaxHp,
            2 => StatType.Def,
            3 => StatType.CritRate,
            4 => StatType.DodgeRate, // 명중 → DodgeRate로 대체 (미구현)
            _ => StatType.Atk
        };

        // ── 메이플 등급 시스템 ──
        // 총 배분 포인트에 따라 등급 상승, 등급별 스탯 배분량 배율 증가
        private static readonly int[] GRADE_THRESHOLDS = { 0, 20, 50, 100, 200 };
        private static readonly string[] GRADE_NAMES = { "1단계", "2단계", "3단계", "4단계", "5단계" };
        private static readonly int[] GRADE_MULTIPLIERS = { 1, 2, 3, 5, 8 };

        private int _currentGrade;

        private int GetMapleGrade()
        {
            int totalAllocated = 0;
            if (_allocatedPoints != null)
                for (int i = 0; i < _allocatedPoints.Length; i++)
                    totalAllocated += _allocatedPoints[i];

            for (int g = GRADE_THRESHOLDS.Length - 1; g >= 0; g--)
            {
                if (totalAllocated >= GRADE_THRESHOLDS[g])
                    return g;
            }
            return 0;
        }

        private void RefreshGrade()
        {
            int newGrade = GetMapleGrade();
            if (newGrade != _currentGrade)
            {
                _currentGrade = newGrade;
                if (_gradeText != null)
                    _gradeText.text = $"메이플 등급 {GRADE_NAMES[_currentGrade]}";
            }
        }

        private int GetStatBonusPerPoint(int index)
        {
            int mult = GRADE_MULTIPLIERS[Mathf.Clamp(_currentGrade, 0, GRADE_MULTIPLIERS.Length - 1)];
            return index switch
            {
                0 => 2 * mult,   // 공격력
                1 => 10 * mult,  // HP
                2 => 1 * mult,   // 방어력
                3 => 1,          // 치명타 (등급 무관)
                4 => 1,          // 명중 (등급 무관)
                _ => 0
            };
        }
    }
}
