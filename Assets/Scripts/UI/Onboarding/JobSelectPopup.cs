using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// 디아블로 스타일 직업 선택 — Show 시점에 3직업 대표 SPUM을 화면 중앙에 스폰한다.
    /// 클릭하면 해당 캐릭터가 크게 확대되며 MOVE 애니메이션 + 하이라이트, 나머지는 어두워진다.
    /// TitleAmbientController의 주변 캐릭터 70명은 선택 모드 동안 Dim 처리.
    /// </summary>
    public class JobSelectPopup : BasePopup
    {
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _jobNameText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("직업 SPUM 프리팹 (직접 참조 우선)")]
        [SerializeField] private GameObject _warriorPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private GameObject _magePrefab;

        [Header("직업 SPUM 프리팹 경로 (Resources 상대) — 직접 참조 없을 때 fallback")]
        [SerializeField] private string _warriorResourcePath = "Addons/PaladinSet/2_Prefab";
        [SerializeField] private string _archerResourcePath = "Addons/Elf/2_Prefab";
        [SerializeField] private string _mageResourcePath = "Addons/ChosunSet/2_Prefab";

        [Header("배치")]
        [SerializeField] private Vector3 _warriorPos = new(-1.9f, 0.6f, 0f);
        [SerializeField] private Vector3 _archerPos = new(0f, 0.6f, 0f);
        [SerializeField] private Vector3 _magePos = new(1.9f, 0.6f, 0f);
        [SerializeField] private float _spawnScale = 1.8f;
        [Tooltip("자동 크기 정규화 목표 세로 높이(world units). 0이면 비활성.")]
        [SerializeField] private float _normalizedHeight = 1.4f;

        private JobType? _selected;
        private readonly Dictionary<JobType, GameObject> _jobCharacters = new();
        private readonly Dictionary<JobType, Vector3> _originalScales = new();
        private readonly List<GameObject> _spawned = new();
        private TitleAmbientController _ambient;

        private static Type _spumType;
        private static FieldInfo _animField;

        public event Action<JobType> Confirmed;

        protected override void Awake()
        {
            base.Awake();
            StripBackdrop();
            EnsureSpumReflection();
            EnsureComponents();

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            UpdateDescription();
        }

        /// <summary>
        /// BasePopup.ApplyPopupTheme가 입힌 전체 화면 딤드 Image를 투명화하고 raycast 차단도 해제.
        /// 중앙 SPUM 3명의 Physics2D 클릭이 Image에 가로채지지 않도록 한다.
        /// </summary>
        private void StripBackdrop()
        {
            var selfImg = GetComponent<Image>();
            if (selfImg != null)
            {
                selfImg.color = new Color(0f, 0f, 0f, 0f);
                selfImg.sprite = null;
                selfImg.raycastTarget = false;
            }
        }

        private static void EnsureSpumReflection()
        {
            if (_spumType != null) return;
            _spumType = Type.GetType("SPUM_Prefabs, Assembly-CSharp");
            if (_spumType != null) _animField = _spumType.GetField("_anim");
        }

        protected override void OnShow()
        {
            base.OnShow();
            _selected = null;
            if (_confirmButton != null) _confirmButton.interactable = false;

            _ambient = FindFirstObjectByType<TitleAmbientController>();
            _ambient?.SetDimmed(true);

            SpawnSelectionCharacters();
            UpdateDescription();
            RefreshHighlights();
        }

        protected override void OnHide()
        {
            base.OnHide();
            _ambient?.SetDimmed(false);
            DespawnSelectionCharacters();
        }

        private void SpawnSelectionCharacters()
        {
            DespawnSelectionCharacters();
            // 1순위: JobOutfitDatabase의 tier=4 (최종 전직) — Title 화면은 최고 외형 전시
            var db = JobOutfitDatabaseSO.Load();
            var warrior = db?.GetFinalTierPrefab(JobType.Warrior) ?? _warriorPrefab;
            var archer = db?.GetFinalTierPrefab(JobType.Archer) ?? _archerPrefab;
            var mage = db?.GetFinalTierPrefab(JobType.Mage) ?? _magePrefab;
            SpawnJob(JobType.Warrior, warrior, _warriorResourcePath, _warriorPos);
            SpawnJob(JobType.Archer, archer, _archerResourcePath, _archerPos);
            SpawnJob(JobType.Mage, mage, _mageResourcePath, _magePos);
        }

        private void SpawnJob(JobType job, GameObject directPrefab, string resourcePath, Vector3 pos)
        {
            GameObject prefab = directPrefab;
            if (prefab == null)
            {
                var loaded = Resources.LoadAll<GameObject>(resourcePath);
                if (loaded != null && loaded.Length > 0) prefab = loaded[0];
            }
            if (prefab == null)
            {
                Debug.LogWarning($"[JobSelectPopup] {job} 프리팹 로드 실패 (direct=null, resourcePath={resourcePath})");
                return;
            }
            var go = Instantiate(prefab);
            go.name = $"JobSelect_{job}";
            go.transform.position = pos;
            var baseScale = Vector3.one * _spawnScale;
            go.transform.localScale = baseScale;

            if (go.GetComponent<Collider2D>() == null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.4f, 2.4f);
                box.offset = new Vector2(0f, 0.3f);
            }
            var bridge = go.GetComponent<JobCharacterClickBridge>();
            if (bridge == null) bridge = go.AddComponent<JobCharacterClickBridge>();
            bridge.Setup(job, this);

            _jobCharacters[job] = go;
            _originalScales[job] = baseScale;
            _spawned.Add(go);

            // 모든 스프라이트 sorting order 상단으로 올려 주변 70명보다 앞에
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingOrder = 100;
            }

            // SPUM 프리팹마다 스프라이트 크기가 크게 다르므로 wrapper scale을 자동 정규화
            if (_normalizedHeight > 0f)
                NormalizeHeightAsync(go, _normalizedHeight).Forget();
        }

        private async UniTaskVoid NormalizeHeightAsync(GameObject wrapper, float targetHeight)
        {
            // SPUM_Prefabs가 SpriteRenderer 세팅 끝낼 때까지 대기 (최대 6프레임)
            for (int i = 0; i < 6; ++i) await UniTask.Yield();
            if (wrapper == null) return;

            // 무기/방패/투구는 프리팹마다 크기 편차가 크므로 bounds 측정에서 제외.
            // 캐릭터 몸통(Body/Head/Hair 등)만으로 신장을 계산해 정규화한다.
            var renderers = wrapper.GetComponentsInChildren<SpriteRenderer>(true);
            bool hasBounds = false;
            var total = new Bounds();
            for (int i = 0; i < renderers.Length; ++i)
            {
                var r = renderers[i];
                if (r == null || r.sprite == null || !r.gameObject.activeInHierarchy) continue;
                if (IsAccessoryName(r.gameObject.name)) continue;
                if (!hasBounds) { total = r.bounds; hasBounds = true; }
                else total.Encapsulate(r.bounds);
            }
            if (!hasBounds || total.size.y <= 0.01f) return;

            float factor = targetHeight / total.size.y;
            wrapper.transform.localScale *= factor;
            // scale 변경 후 position은 그대로 유지 (world position wrapper 기준)
            if (_jobCharacters.TryGetValue(FindJobByHost(wrapper), out _))
            {
                // baseScale 업데이트 (하이라이트 시 사용)
                var key = FindJobByHost(wrapper);
                if (_originalScales.ContainsKey(key))
                    _originalScales[key] = wrapper.transform.localScale;
            }
        }

        private JobType FindJobByHost(GameObject host)
        {
            foreach (var kv in _jobCharacters)
            {
                if (kv.Value == host) return kv.Key;
            }
            return JobType.Warrior;
        }

        /// <summary>무기/방패/투구 등 부피 편차 큰 부위 식별 (bounds 측정 제외용).</summary>
        private static bool IsAccessoryName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("Weapon")
                || name.Contains("Shield")
                || name.Contains("Helmet")
                || name.Contains("Back")
                || name.Contains("Horse");
        }

        private void DespawnSelectionCharacters()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Destroy(go);
            }
            _spawned.Clear();
            _jobCharacters.Clear();
            _originalScales.Clear();
        }

        private void EnsureComponents()
        {
            // 하단 얇은 정보 바 — 전체 화면을 가리지 않음
            RectTransform bar;
            var barTr = transform.Find("InfoBar");
            if (barTr == null)
            {
                var barGo = new GameObject("InfoBar", typeof(RectTransform));
                barGo.transform.SetParent(transform, false);
                bar = barGo.GetComponent<RectTransform>();
                bar.anchorMin = new Vector2(0, 0);
                bar.anchorMax = new Vector2(1, 0);
                bar.pivot = new Vector2(0.5f, 0);
                bar.sizeDelta = new Vector2(0, 380);
                bar.anchoredPosition = Vector2.zero;
                var img = barGo.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.55f);
            }
            else bar = barTr as RectTransform;

            if (transform.Find("TitleHint") == null)
            {
                var hintGo = new GameObject("TitleHint", typeof(RectTransform));
                hintGo.transform.SetParent(transform, false);
                var hrt = hintGo.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0, 1);
                hrt.anchorMax = new Vector2(1, 1);
                hrt.pivot = new Vector2(0.5f, 1);
                hrt.sizeDelta = new Vector2(0, 80);
                hrt.anchoredPosition = new Vector2(0, -40);
                var t = hintGo.AddComponent<TextMeshProUGUI>();
                t.text = "직업을 선택하세요";
                t.fontSize = 46;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.color = new Color(1f, 0.9f, 0.5f);
                t.outlineWidth = 0.25f;
                t.outlineColor = Color.black;
            }

            if (_jobNameText == null)
            {
                var go = new GameObject("JobName", typeof(RectTransform));
                go.transform.SetParent(bar, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(600, 80);
                rt.anchoredPosition = new Vector2(0, -60);
                _jobNameText = go.AddComponent<TextMeshProUGUI>();
                _jobNameText.fontSize = 56;
                _jobNameText.fontStyle = FontStyles.Bold;
                _jobNameText.alignment = TextAlignmentOptions.Center;
                _jobNameText.color = Color.white;
                _jobNameText.outlineWidth = 0.25f;
                _jobNameText.outlineColor = Color.black;
            }

            if (_descriptionText == null)
            {
                var go = new GameObject("Description", typeof(RectTransform));
                go.transform.SetParent(bar, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1);
                rt.anchorMax = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(900, 120);
                rt.anchoredPosition = new Vector2(0, -160);
                _descriptionText = go.AddComponent<TextMeshProUGUI>();
                _descriptionText.fontSize = 34;
                _descriptionText.alignment = TextAlignmentOptions.Center;
                _descriptionText.color = new Color(0.9f, 0.9f, 0.9f);
            }

            if (_confirmButton == null)
            {
                var btnGo = new GameObject("ConfirmButton", typeof(RectTransform));
                btnGo.transform.SetParent(bar, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(400, 110);
                rt.anchoredPosition = new Vector2(0, 40);
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.25f, 0.6f, 0.35f);
                _confirmButton = btnGo.AddComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                var l = labelGo.AddComponent<TextMeshProUGUI>();
                l.text = "확정";
                l.fontSize = 48;
                l.fontStyle = FontStyles.Bold;
                l.alignment = TextAlignmentOptions.Center;
                l.color = Color.white;
            }
        }

        public void OnCharacterClicked(JobType job)
        {
            _selected = job;
            if (_confirmButton != null) _confirmButton.interactable = true;
            UpdateDescription();
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            foreach (var kv in _jobCharacters)
            {
                var job = kv.Key;
                var go = kv.Value;
                if (go == null) continue;

                bool isSelected = _selected == job;
                var baseScale = _originalScales.TryGetValue(job, out var s) ? s : Vector3.one * _spawnScale;
                go.transform.localScale = isSelected ? baseScale * 1.2f : baseScale;

                foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
                {
                    sr.color = _selected == null ? Color.white
                             : isSelected ? Color.white
                             : new Color(0.5f, 0.5f, 0.55f, 1f);
                }

                SetWalk(go, isSelected);
            }
        }

        private static void SetWalk(GameObject go, bool isWalk)
        {
            if (_spumType == null || _animField == null) return;
            var spum = go.GetComponent(_spumType);
            if (spum == null) return;
            var anim = _animField.GetValue(spum) as Animator;
            if (anim == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                var n = p.name.ToLower();
                if (n.Contains("move") || n.Contains("run"))
                    anim.SetBool(p.name, isWalk);
            }
        }

        private void UpdateDescription()
        {
            if (_jobNameText != null)
            {
                _jobNameText.text = _selected switch
                {
                    JobType.Warrior => "전사",
                    JobType.Archer => "궁수",
                    JobType.Mage => "마법사",
                    _ => "",
                };
            }
            if (_descriptionText != null)
            {
                _descriptionText.text = _selected switch
                {
                    JobType.Warrior => "튼튼한 방어와 강력한 근접 공격. 초심자 추천.",
                    JobType.Archer => "빠른 공격 속도와 치명타 중심. 중거리 딜러.",
                    JobType.Mage => "광역 마법 피해. 스킬 회전에 의존.",
                    _ => "캐릭터를 클릭하여 직업을 선택하세요.",
                };
            }
        }

        private void OnConfirmClicked()
        {
            if (_selected == null) return;
            Confirmed?.Invoke(_selected.Value);
            Hide();
        }

        /// <summary>봇 자동화 진입점 — 직업 선택 + 확정 일괄.</summary>
        public void BotSelectAndConfirm(JobType job)
        {
            OnCharacterClicked(job);
            OnConfirmClicked();
        }

        public override void OnBackButton() { }
    }

    /// <summary>
    /// SPUM 캐릭터 GameObject에 부착되어 클릭(터치) 이벤트를 JobSelectPopup에 전달.
    /// </summary>
    public class JobCharacterClickBridge : MonoBehaviour, IPointerClickHandler
    {
        private JobType _job;
        private JobSelectPopup _popup;
        // 2026-04-23 OnPointerClick + OnMouseDown 둘 다 등록되어 PC Editor에서 2회 fire → dedup
        private float _lastClickAt;
        private const float DOUBLE_CLICK_WINDOW = 0.1f;

        public void Setup(JobType job, JobSelectPopup popup)
        {
            _job = job;
            _popup = popup;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerClick();
        }

        private void OnMouseDown()
        {
            TriggerClick();
        }

        private void TriggerClick()
        {
            if (_popup == null) return;
            if (Time.unscaledTime - _lastClickAt < DOUBLE_CLICK_WINDOW) return;
            _lastClickAt = Time.unscaledTime;
            _popup.OnCharacterClicked(_job);
        }
    }
}
