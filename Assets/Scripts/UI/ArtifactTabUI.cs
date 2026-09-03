using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 아티팩트 탭 컨트롤러.
    /// 4슬롯 장착 + 인벤토리 리스트 + 세트 효과 표시.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ArtifactTabUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _contentRoot;

        private Label _unlockLabel;
        private VisualElement[] _slots = new VisualElement[4];
        private Label _setLabel;
        private VisualElement _itemList;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 50;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[ArtifactTabUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;
            _contentRoot = _root.Q<VisualElement>("artifact-root");

            CacheElements();

            EventBus.Subscribe<ArtifactEquippedEvent>(OnArtifactEquipped);
            EventBus.Subscribe<ArtifactObtainedEvent>(OnArtifactObtained);

            RefreshAll();
            PlayOpenAnimation();
        }

        private void OnDisable()
        {
            _contentRoot?.RemoveFromClassList("panel--open");
            EventBus.Unsubscribe<ArtifactEquippedEvent>(OnArtifactEquipped);
            EventBus.Unsubscribe<ArtifactObtainedEvent>(OnArtifactObtained);
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
            _unlockLabel = _root.Q<Label>("artifact-unlock-label");
            for (int i = 0; i < 4; i++)
                _slots[i] = _root.Q<VisualElement>($"artifact-slot-{i}");
            _setLabel = _root.Q<Label>("artifact-set-label");
            _itemList = _root.Q<VisualElement>("artifact-list");
        }

        private void RefreshAll()
        {
            var mgr = ArtifactManager.Instance;
            if (mgr == null) return;

            if (_unlockLabel != null)
                _unlockLabel.text = mgr.IsUnlocked ? "해금됨" : "스테이지 347 해금";

            RefreshSlots(mgr);
            RefreshInventory(mgr);
        }

        private void RefreshSlots(ArtifactManager mgr)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].Clear();

                string artifactId = i < mgr.EquippedSlots.Count ? mgr.EquippedSlots[i] : "";
                if (!string.IsNullOrEmpty(artifactId))
                {
                    var data = mgr.FindArtifactData(artifactId);
                    _slots[i].AddToClassList("artifact__slot--equipped");

                    if (data != null)
                    {
                        var icon = new VisualElement();
                        icon.AddToClassList("artifact__slot-icon");
                        if (data.icon != null)
                            icon.style.backgroundImage = new StyleBackground(data.icon);
                        _slots[i].Add(icon);

                        var name = new Label(data.displayName);
                        name.AddToClassList("artifact__slot-name");
                        _slots[i].Add(name);
                    }
                }
                else
                {
                    _slots[i].RemoveFromClassList("artifact__slot--equipped");
                    var empty = new Label("빈 슬롯");
                    empty.AddToClassList("artifact__slot-name");
                    _slots[i].Add(empty);
                }
            }
        }

        private void RefreshInventory(ArtifactManager mgr)
        {
            if (_itemList == null) return;
            _itemList.Clear();

            foreach (var inst in mgr.OwnedArtifacts)
            {
                var data = mgr.FindArtifactData(inst.artifactId);
                if (data == null) continue;

                var row = new VisualElement();
                row.AddToClassList("artifact__item");

                var icon = new VisualElement();
                icon.AddToClassList("artifact__item-icon");
                if (data.icon != null)
                    icon.style.backgroundImage = new StyleBackground(data.icon);
                row.Add(icon);

                var info = new VisualElement();
                info.AddToClassList("artifact__item-info");

                var nameLabel = new Label($"{data.displayName} ({data.grade})");
                nameLabel.AddToClassList("artifact__item-name");
                info.Add(nameLabel);

                string statName = GetStatName(data.bonusStat);
                string effect = data.bonusFlat > 0 ? $"{statName} +{data.bonusFlat:F0}" : $"{statName} +{data.bonusPercent}%";
                var effectLabel = new Label(effect);
                effectLabel.AddToClassList("artifact__item-effect");
                info.Add(effectLabel);

                row.Add(info);

                // 장착 버튼 (빈 슬롯이 있을 때)
                var btn = new Button();
                btn.AddToClassList("artifact__item-btn");
                btn.text = "장착";
                string id = inst.artifactId;
                btn.RegisterCallback<ClickEvent>(_ => OnEquipClicked(id));
                row.Add(btn);

                _itemList.Add(row);
            }
        }

        private void OnEquipClicked(string artifactId)
        {
            var mgr = ArtifactManager.Instance;
            if (mgr == null) return;

            // 빈 슬롯 찾기
            for (int i = 0; i < mgr.MaxSlots; i++)
            {
                if (i < mgr.EquippedSlots.Count && string.IsNullOrEmpty(mgr.EquippedSlots[i]))
                {
                    mgr.EquipArtifact(artifactId, i);
                    return;
                }
            }
            // 빈 슬롯 없으면 0번 교체
            mgr.EquipArtifact(artifactId, 0);
        }

        private void OnArtifactEquipped(ArtifactEquippedEvent evt) => RefreshAll();
        private void OnArtifactObtained(ArtifactObtainedEvent evt) => RefreshAll();

        private static string GetStatName(StatType stat)
        {
            return stat switch
            {
                StatType.Atk => "공격력",
                StatType.Def => "방어력",
                StatType.MaxHp => "최대 HP",
                StatType.CritRate => "치명타율",
                StatType.CritDamage => "치명타 데미지",
                StatType.AttackSpeed => "공격 속도",
                _ => stat.ToString()
            };
        }
    }
}
