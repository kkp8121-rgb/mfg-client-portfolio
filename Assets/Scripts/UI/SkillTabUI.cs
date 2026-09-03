using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;
using MkLike.Economy;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 스킬 탭 컨트롤러.
    /// UIDocument에 연결하여 SkillSystem의 학습된 스킬을 표시하고 강화 기능을 제공한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SkillTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        // 캐싱된 요소
        // 2026-04-23 Phase B 단계 3.5: 슬롯 4칸은 요약(이름+레벨만). 강화 버튼은 하단 리스트.
        private const int SLOT_COUNT = 4;
        private VisualElement[] _slotElements = new VisualElement[SLOT_COUNT];
        private Label[] _slotNameLabels = new Label[SLOT_COUNT];
        private Label[] _slotLevelLabels = new Label[SLOT_COUNT];
        private Label _emptyMsg;
        private VisualElement _skillList;
        private ScrollView _skillScroll;

        // 게임 시스템 참조
        private SkillSystem _skillSystem;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 52; // 탭별 고유값 (2026-04-23)
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[SkillTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            PanelCloseHelper.BindCloseButtonAsSubtab(_root, gameObject);
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("skill-root");

            CacheElements();
            FindSkillSystem();

            EventBus.Subscribe<SkillLearnedEvent>(OnSkillLearned);
            EventBus.Subscribe<SkillLevelUpEvent>(OnSkillLevelUp);
            RefreshAll();

            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            // Subscribe는 InitializeAsync에서 수행되므로 구독 안 된 상태에서 Unsubscribe 호출 시 무해
            EventBus.Unsubscribe<SkillLearnedEvent>(OnSkillLearned);
            EventBus.Unsubscribe<SkillLevelUpEvent>(OnSkillLevelUp);
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
            // 2026-04-23 Phase B 단계 3.5: slot-0..3 요약 카드 (이름+레벨만)
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slotElements[i] = _root.Q<VisualElement>($"slot-{i}");
                _slotNameLabels[i] = _root.Q<Label>($"slot-{i}-name");
                _slotLevelLabels[i] = _root.Q<Label>($"slot-{i}-level");
            }

            _emptyMsg = _root.Q<Label>("skill-empty-msg");
            _skillList = _root.Q<VisualElement>("skill-list");
            _skillScroll = _root.Q<ScrollView>("skill-scroll");
        }

        private void FindSkillSystem()
        {
            if (_skillSystem != null) return;

            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
                _skillSystem = player.GetComponent<SkillSystem>();

            if (_skillSystem == null)
                _skillSystem = FindFirstObjectByType<SkillSystem>();
        }

        // ── 전체 갱신 ──

        private void RefreshAll()
        {
            FindSkillSystem();
            RefreshSlots();
            RefreshSkillList();
        }

        // ── 슬롯 갱신 (2026-04-23 Phase B 단계 3: 슬롯 인덱스 기반) ──

        private void RefreshSlots()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
                UpdateSlotByIndex(i);
        }

        private void UpdateSlotByIndex(int slot)
        {
            var slotEl = _slotElements[slot];
            if (slotEl == null) return;

            SkillDataSO skill = _skillSystem != null ? _skillSystem.GetSkillAtSlot(slot) : null;
            var iconElement = slotEl.Q<VisualElement>(className: "skill__slot-icon");
            var nameLabel = _slotNameLabels[slot];
            var levelLabel = _slotLevelLabels[slot];

            if (skill != null)
            {
                slotEl.AddToClassList("skill__slot--assigned");
                int level = _skillSystem.GetSlotLevel(slot);
                if (nameLabel != null) nameLabel.text = skill.displayName;
                if (levelLabel != null) levelLabel.text = $"Lv.{level}";
                if (iconElement != null && skill.icon != null)
                    iconElement.style.backgroundImage = new StyleBackground(skill.icon);
            }
            else
            {
                slotEl.RemoveFromClassList("skill__slot--assigned");
                if (nameLabel != null) nameLabel.text = "미습득";
                if (levelLabel != null) levelLabel.text = "Lv.0";
            }
        }

        // ── 스킬 리스트 갱신 ──

        private void RefreshSkillList()
        {
            if (_skillList == null) return;

            _skillList.Clear();

            if (_skillSystem == null)
            {
                SetEmptyMsgVisible(true);
                return;
            }

            var learned = _skillSystem.LearnedSkills;
            if (learned == null || learned.Count == 0)
            {
                SetEmptyMsgVisible(true);
                return;
            }

            SetEmptyMsgVisible(false);

            for (int i = 0; i < learned.Count; i++)
            {
                var skill = learned[i];
                if (skill == null) continue;

                var row = CreateSkillRow(skill, i % 2 == 1);
                _skillList.Add(row);
            }
        }

        private void SetEmptyMsgVisible(bool isVisible)
        {
            if (_emptyMsg != null)
                _emptyMsg.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement CreateSkillRow(SkillDataSO skill, bool isAlt)
        {
            var row = new VisualElement();
            row.AddToClassList("skill__row");
            if (isAlt) row.AddToClassList("skill__row--alt");

            // 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("skill__row-icon");
            icon.AddToClassList(GetIconTypeClass(skill.skillType));
            if (skill.icon != null)
                icon.style.backgroundImage = new StyleBackground(skill.icon);
            row.Add(icon);

            // 정보 영역
            var info = new VisualElement();
            info.AddToClassList("skill__row-info");

            var nameLabel = new Label(skill.displayName);
            nameLabel.AddToClassList("skill__row-name");
            info.Add(nameLabel);

            var typeLabel = new Label(GetSkillTypeDisplay(skill.skillType));
            typeLabel.AddToClassList("skill__row-type");
            typeLabel.AddToClassList(GetTypeTextClass(skill.skillType));
            info.Add(typeLabel);

            // 설명 (있는 경우)
            if (!string.IsNullOrEmpty(skill.description))
            {
                var descLabel = new Label(skill.description);
                descLabel.AddToClassList("skill__row-desc");
                info.Add(descLabel);
            }

            row.Add(info);

            // 레벨 — 2026-04-23 Phase B 단계 2: 슬롯 API 경유 (slot 매핑되면 GetSlotLevel, 아니면 레거시 fallback)
            int slot = _skillSystem.GetSlotForSkill(skill);
            int level = slot >= 0 ? _skillSystem.GetSlotLevel(slot) : _skillSystem.GetSkillLevel(skill.id);
            var levelLabel = new Label($"Lv.{level}");
            levelLabel.AddToClassList("skill__row-level");
            row.Add(levelLabel);

            // 비용
            long cost = CombatFormula.SkillLevelUpCost(level);
            var costLabel = new Label($"{HudPanel.FormatGold(cost)}G");
            costLabel.AddToClassList("skill__row-cost");
            row.Add(costLabel);

            // 강화 버튼
            var btn = new Button();
            btn.AddToClassList("skill__row-btn");
            var btnLabel = new Label("강화");
            btn.Add(btnLabel);

            string skillId = skill.id;
            btn.RegisterCallback<ClickEvent>(_ => OnSkillLevelUpClick(skillId));
            row.Add(btn);

            return row;
        }

        // ── 강화 클릭 ──

        private void OnSkillLevelUpClick(string skillId)
        {
            if (_skillSystem == null) return;

            // 2026-04-23 Phase B 단계 2: 슬롯 기반 레벨업 우선 시도 (실패 시 레거시 skillId 방식 fallback)
            int currentLevel = _skillSystem.GetSkillLevel(skillId);
            long cost = CombatFormula.SkillLevelUpCost(currentLevel);

            if (CurrencyManager.Instance == null) return;

            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, cost))
            {
                // CurrencyShortageEvent가 자동 발행됨
                return;
            }

            // 슬롯 역매핑: 성공하면 LevelUpSlot, 실패하면 레거시 LevelUpSkill
            bool success = false;
            SkillDataSO skill = null;
            for (int i = 0; i < _skillSystem.LearnedSkills.Count; i++)
            {
                if (_skillSystem.LearnedSkills[i] != null && _skillSystem.LearnedSkills[i].id == skillId)
                {
                    skill = _skillSystem.LearnedSkills[i];
                    break;
                }
            }
            int slot = skill != null ? _skillSystem.GetSlotForSkill(skill) : -1;
            if (slot >= 0)
                success = _skillSystem.LevelUpSlot(slot);
            else
                success = _skillSystem.LevelUpSkill(skillId);

            if (!success)
            {
                // 실패 시 골드 환불
                CurrencyManager.Instance.Add(CurrencyType.Gold, cost);
                return;
            }

            // 2026-04-23 이슈 15 FeedbackBus: 스킬 레벨업 Toast
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.EquipEnhance,
                $"{skill?.displayName ?? skillId} Lv.{currentLevel + 1} 달성!");

            // 성공 — 리스트 새로고침
            RefreshSkillList();
        }

        // ── 이벤트 핸들러 ──

        private void OnSkillLearned(SkillLearnedEvent evt)
        {
            RefreshAll();
        }

        private void OnSkillLevelUp(SkillLevelUpEvent evt)
        {
            RefreshSkillList();
        }

        // ── 유틸리티 ──

        private static string GetSkillTypeDisplay(SkillType type)
        {
            return type switch
            {
                SkillType.Active => "기본공격",
                SkillType.Passive => "패시브",
                SkillType.Buff => "버프",
                SkillType.Awakening => "각성기",
                _ => type.ToString()
            };
        }

        private static string GetIconTypeClass(SkillType type)
        {
            return type switch
            {
                SkillType.Active => "skill__row-icon--active",
                SkillType.Passive => "skill__row-icon--passive",
                SkillType.Buff => "skill__row-icon--buff",
                SkillType.Awakening => "skill__row-icon--awakening",
                _ => ""
            };
        }

        private static string GetTypeTextClass(SkillType type)
        {
            return type switch
            {
                SkillType.Active => "skill__row-type--active",
                SkillType.Passive => "skill__row-type--passive",
                SkillType.Buff => "skill__row-type--buff",
                SkillType.Awakening => "skill__row-type--awakening",
                _ => ""
            };
        }

    }
}
