using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 레벨업 시 화면 전체 플래시 + "LEVEL UP!" 대문자 + 스킬 예고 + SFX 연출.
    /// 플레이어 캐릭터에 부착한다.
    /// </summary>
    public class LevelUpEffect : MonoBehaviour
    {
        [SerializeField] private float _ringRadius = 1.5f;
        [SerializeField] private float _punchScale = 0.3f;
        [SerializeField] private float _punchDuration = 0.3f;

        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color FlashColor = new Color(1f, 1f, 0.9f, 0.6f);

        /// <summary>전직 레벨 테이블 (마일스톤 표시용)</summary>
        private static readonly int[] AdvancementLevels = { 40, 80, 120, 160 };
        private static readonly string[] AdvancementNames = { "1차 전직", "2차 전직", "3차 전직", "4차 전직" };

        private TextMeshPro _levelUpTmp;
        private TextMeshPro _subTmp;
        private TextMeshPro _statGainTmp;
        private SpriteRenderer _flashRenderer;

        // 스파크용 텍스처/스프라이트 캐시 (매 레벨업마다 생성 방지)
        private static Texture2D _cachedSparkTex;
        private static Sprite _cachedSparkSprite;
        private Sequence _mainSequence;

        private void OnEnable()
        {
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
        }

        private void OnDisable()
        {
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
        }

        private void OnLevelUp(LevelUpEvent e)
        {
            PlayLevelUpAsync(e);
        }

        private void PlayLevelUpAsync(LevelUpEvent e)
        {
            // 팝업/패널이 열려있으면 월드 스페이스 연출 억제 (SFX만 재생)
            bool isUIOpen = UIState.IsAnyPanelOpen;

            // SFX
            AudioManager.Instance?.PlaySfx(SfxType.LevelUp);

            // 팝업이 열려있으면 시각 연출 전부 스킵
            if (isUIOpen) return;

            // 1. 황금 링 VFX
            SkillVfx.SpawnRing(transform.position, _ringRadius, GoldColor, 0.5f);

            // 2. 스케일 펌프
            transform.DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0f)
                .SetLink(gameObject);

            // 3. 스파크 파티클 (범용 파티클 시스템 우선, fallback으로 로컬)
            if (ProceduralParticleSystem.Instance != null)
                ProceduralParticleSystem.Instance.SpawnParticles(ParticlePreset.Sparkle, transform.position, GoldColor, 8);
            else
                SpawnSparkParticles(transform.position, 8);

            // 4. 화면 전체 백색 플래시
            PlayScreenFlash();

            // 5. "LEVEL UP!" 텍스트 (TMPro, 월드 스페이스)
            EnsureTmpObjects();

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 centerWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.55f, 10f));
            centerWorld.z = 0f;
            _levelUpTmp.transform.position = centerWorld;
            _levelUpTmp.text = "레벨 업!";
            _levelUpTmp.color = GoldColor;
            _levelUpTmp.alpha = 1f;
            _levelUpTmp.transform.localScale = Vector3.zero;
            _levelUpTmp.gameObject.SetActive(true);

            // 서브 텍스트: "Lv.XX → Lv.YY"
            Vector3 subPos = centerWorld + Vector3.down * 0.6f;
            _subTmp.transform.position = subPos;
            _subTmp.text = $"Lv.{e.PreviousLevel} → Lv.{e.CurrentLevel}";
            _subTmp.color = Color.white;
            _subTmp.alpha = 0f;
            _subTmp.transform.localScale = Vector3.one * 0.6f;
            _subTmp.gameObject.SetActive(true);

            // 스탯 증가 텍스트
            bool hasStatInfo = e.HpGain > 0 || e.AtkGain > 0 || e.DefGain > 0;
            if (hasStatInfo)
            {
                Vector3 statPos = centerWorld + Vector3.down * 1.1f;
                _statGainTmp.transform.position = statPos;
                var sb = new System.Text.StringBuilder();
                if (e.AtkGain > 0) sb.Append($"<color=#FF9966>ATK +{e.AtkGain}</color>  ");
                if (e.HpGain > 0) sb.Append($"<color=#66FF66>HP +{e.HpGain}</color>  ");
                if (e.DefGain > 0) sb.Append($"<color=#6699FF>DEF +{e.DefGain}</color>  ");
                if (e.StatPoints > 0) sb.Append($"<color=#FFD700>SP +{e.StatPoints}</color>");
                _statGainTmp.text = sb.ToString().TrimEnd();
                _statGainTmp.color = Color.white;
                _statGainTmp.alpha = 0f;
                _statGainTmp.transform.localScale = Vector3.one * 0.6f;
                _statGainTmp.gameObject.SetActive(true);
            }

            // 마일스톤 체크 (전직 레벨 도달 시 추가 텍스트)
            string milestone = CheckMilestone(e.CurrentLevel);

            // 이전 시퀀스가 남아있으면 텍스트를 숨기고 정리
            if (_mainSequence != null && _mainSequence.IsActive())
            {
                _mainSequence.Kill();
                _levelUpTmp.gameObject.SetActive(false);
                _subTmp.gameObject.SetActive(false);
                _statGainTmp.gameObject.SetActive(false);
            }
            _mainSequence = DOTween.Sequence();

            // "LEVEL UP!" 팝인
            _mainSequence.Append(
                _levelUpTmp.transform.DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutBack));
            _mainSequence.Append(
                _levelUpTmp.transform.DOScale(Vector3.one, 0.1f).SetEase(Ease.InQuad));

            // 서브 텍스트 페이드인
            _mainSequence.Insert(0.15f,
                DOTween.To(() => _subTmp.alpha, a => _subTmp.alpha = a, 1f, 0.2f));

            // 스탯 증가 텍스트 페이드인
            if (hasStatInfo)
            {
                _mainSequence.Insert(0.3f,
                    DOTween.To(() => _statGainTmp.alpha, a => _statGainTmp.alpha = a, 1f, 0.25f));
            }

            // 마일스톤이 있으면 서브 텍스트 교체
            if (!string.IsNullOrEmpty(milestone))
            {
                _mainSequence.InsertCallback(1.0f, () =>
                {
                    _subTmp.text = milestone;
                    _subTmp.color = new Color(1f, 0.6f, 0.1f);
                });
                _mainSequence.Insert(1.0f,
                    _subTmp.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 3, 0.3f));
            }

            // 유지
            _mainSequence.AppendInterval(1.0f);

            // 페이드아웃 + 스케일 축소
            _mainSequence.Append(
                DOTween.To(() => _levelUpTmp.alpha, a => _levelUpTmp.alpha = a, 0f, 0.4f));
            _mainSequence.Join(
                DOTween.To(() => _subTmp.alpha, a => _subTmp.alpha = a, 0f, 0.4f));
            if (hasStatInfo)
            {
                _mainSequence.Join(
                    DOTween.To(() => _statGainTmp.alpha, a => _statGainTmp.alpha = a, 0f, 0.4f));
            }
            _mainSequence.Join(
                _levelUpTmp.transform.DOScale(Vector3.one * 0.5f, 0.4f).SetEase(Ease.InQuad));

            _mainSequence.OnKill(() =>
            {
                if (_levelUpTmp != null) _levelUpTmp.gameObject.SetActive(false);
                if (_subTmp != null) _subTmp.gameObject.SetActive(false);
                if (_statGainTmp != null) _statGainTmp.gameObject.SetActive(false);
            });
            _mainSequence.OnComplete(() =>
            {
                if (_levelUpTmp != null)
                {
                    _levelUpTmp.alpha = 0f;
                    _levelUpTmp.gameObject.SetActive(false);
                }
                if (_subTmp != null)
                {
                    _subTmp.alpha = 0f;
                    _subTmp.gameObject.SetActive(false);
                }
                if (_statGainTmp != null)
                {
                    _statGainTmp.alpha = 0f;
                    _statGainTmp.gameObject.SetActive(false);
                }
            });
            _mainSequence.SetLink(gameObject);
        }

        private void PlayScreenFlash()
        {
            if (_flashRenderer == null)
            {
                var flashObj = new GameObject("LevelUpFlash");
                flashObj.transform.SetParent(transform);
                _flashRenderer = flashObj.AddComponent<SpriteRenderer>();

                // 단색 스프라이트 (static 캐시 공유)
                if (_cachedSparkTex == null)
                {
                    _cachedSparkTex = new Texture2D(1, 1);
                    _cachedSparkTex.SetPixel(0, 0, Color.white);
                    _cachedSparkTex.Apply();
                    _cachedSparkSprite = Sprite.Create(_cachedSparkTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                _flashRenderer.sprite = _cachedSparkSprite;
                _flashRenderer.sortingOrder = 190;
            }

            var cam = Camera.main;
            if (cam == null) return;

            // 화면 전체를 덮는 크기
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;
            _flashRenderer.transform.position = cam.transform.position + Vector3.forward;
            _flashRenderer.transform.localScale = new Vector3(camWidth * 2f, camHeight * 2f, 1f);

            _flashRenderer.color = FlashColor;
            _flashRenderer.gameObject.SetActive(true);

            // 0.3초 페이드아웃 (float 보간 — WebGL AOT safe)
            ColorTweenHelper.To(_flashRenderer, new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f), 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _flashRenderer.gameObject.SetActive(false))
                .SetLink(_flashRenderer.gameObject);
        }

        private string CheckMilestone(int level)
        {
            for (int i = 0; i < AdvancementLevels.Length; i++)
            {
                if (level == AdvancementLevels[i])
                    return $"<color=#FFD700>{AdvancementNames[i]} 가능!</color>";
            }
            return null;
        }

        private void EnsureTmpObjects()
        {
            if (_levelUpTmp == null)
            {
                var obj = new GameObject("LevelUpTMP");
                obj.transform.SetParent(transform);
                _levelUpTmp = obj.AddComponent<TextMeshPro>();
                _levelUpTmp.fontSize = 6f;
                _levelUpTmp.fontStyle = FontStyles.Bold;
                _levelUpTmp.alignment = TextAlignmentOptions.Center;
                _levelUpTmp.textWrappingMode = TextWrappingModes.NoWrap;
                _levelUpTmp.sortingOrder = 210;
                _levelUpTmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
                _levelUpTmp.outlineWidth = 0.3f;
                _levelUpTmp.outlineColor = new Color32(80, 40, 0, 200);

                var font = TMP_Settings.defaultFontAsset;
                if (font != null) _levelUpTmp.font = font;

                obj.SetActive(false);
            }

            if (_subTmp == null)
            {
                var obj = new GameObject("LevelUpSubTMP");
                obj.transform.SetParent(transform);
                _subTmp = obj.AddComponent<TextMeshPro>();
                _subTmp.fontSize = 3.5f;
                _subTmp.fontStyle = FontStyles.Bold;
                _subTmp.alignment = TextAlignmentOptions.Center;
                _subTmp.textWrappingMode = TextWrappingModes.NoWrap;
                _subTmp.sortingOrder = 210;
                _subTmp.rectTransform.sizeDelta = new Vector2(10f, 1.5f);
                _subTmp.outlineWidth = 0.2f;
                _subTmp.outlineColor = new Color32(0, 0, 0, 180);

                var font = TMP_Settings.defaultFontAsset;
                if (font != null) _subTmp.font = font;

                obj.SetActive(false);
            }

            if (_statGainTmp == null)
            {
                var obj = new GameObject("LevelUpStatGainTMP");
                obj.transform.SetParent(transform);
                _statGainTmp = obj.AddComponent<TextMeshPro>();
                _statGainTmp.fontSize = 2.8f;
                _statGainTmp.fontStyle = FontStyles.Bold;
                _statGainTmp.alignment = TextAlignmentOptions.Center;
                _statGainTmp.textWrappingMode = TextWrappingModes.NoWrap;
                _statGainTmp.sortingOrder = 210;
                _statGainTmp.richText = true;
                _statGainTmp.rectTransform.sizeDelta = new Vector2(12f, 1.5f);
                _statGainTmp.outlineWidth = 0.2f;
                _statGainTmp.outlineColor = new Color32(0, 0, 0, 180);

                var font = TMP_Settings.defaultFontAsset;
                if (font != null) _statGainTmp.font = font;

                obj.SetActive(false);
            }
        }

        /// <summary>
        /// 원형으로 퍼지는 스파크 파티클을 생성한다.
        /// 각 스파크는 SpriteRenderer로 작은 황금 점이며, 바깥으로 이동 + 스케일 축소 + 페이드아웃된다.
        /// </summary>
        private void SpawnSparkParticles(Vector3 center, int count)
        {
            // 1x1 white pixel 텍스처 (static 캐시)
            if (_cachedSparkTex == null)
            {
                _cachedSparkTex = new Texture2D(1, 1);
                _cachedSparkTex.SetPixel(0, 0, Color.white);
                _cachedSparkTex.Apply();
                _cachedSparkSprite = Sprite.Create(_cachedSparkTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
            }
            var sparkSprite = _cachedSparkSprite;

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i + Random.Range(-10f, 10f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                float dist = _ringRadius * Random.Range(0.8f, 1.4f);

                var spark = new GameObject($"Spark_{i}");
                spark.transform.position = center;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var sr = spark.AddComponent<SpriteRenderer>();
                sr.sprite = sparkSprite;
                sr.color = new Color(
                    Random.Range(0.9f, 1f),
                    Random.Range(0.7f, 0.9f),
                    Random.Range(0.1f, 0.3f),
                    1f
                );
                sr.sortingOrder = 200;

                Vector3 endPos = center + dir * dist;

                // 바깥으로 이동 + 스케일 축소 + 페이드아웃
                var seq = DOTween.Sequence();
                seq.Append(spark.transform.DOMove(endPos, 0.5f).SetEase(Ease.OutCubic));
                seq.Join(spark.transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InQuad));
                seq.Join(ColorTweenHelper.To(sr, new Color(sr.color.r, sr.color.g, sr.color.b, 0f), 0.4f)
                    .SetDelay(0.1f));
                seq.OnComplete(() => Destroy(spark));
                seq.SetLink(gameObject);
            }
        }

        private void OnDestroy()
        {
            _mainSequence?.Kill();
            transform.DOKill();
            if (_levelUpTmp != null) _levelUpTmp.transform.DOKill();
            if (_subTmp != null) _subTmp.transform.DOKill();
            if (_statGainTmp != null) _statGainTmp.transform.DOKill();
        }
    }
}
