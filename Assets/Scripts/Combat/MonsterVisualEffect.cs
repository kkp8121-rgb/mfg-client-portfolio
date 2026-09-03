using DG.Tweening;
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 몬스터 스폰/사망 비주얼 연출.
    /// MonsterDiedEvent 구독으로 사망 연출 자동 호출. 스폰은 외부에서 PlaySpawn() 호출.
    /// SO 기반 SpriteSheetVfx 우선, 없으면 기존 SkillVfx 2D 도형 폴백.
    /// </summary>
    public class MonsterVisualEffect : MonoBehaviour
    {
        [Header("보스 설정")]
        [SerializeField] private float _bossScale = 2.0f;
        [SerializeField] private float _chapterBossScale = 2.5f;
        [SerializeField] private Color _bossAuraColor = new Color(1f, 0.3f, 0.1f, 0.4f);
        [SerializeField] private float _bossAuraRadius = 0.8f;

        private void OnEnable()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
        }

        /// <summary>
        /// 몬스터 스폰 연출. MonsterSpawner에서 스폰 직후 호출한다.
        /// </summary>
        public void PlaySpawn(Transform monster, bool isElite)
        {
            if (monster == null) return;

            monster.localScale = Vector3.zero;
            monster.DOScale(1f, 0.2f)
                .SetEase(Ease.OutBack)
                .SetLink(monster.gameObject);

            if (isElite)
            {
                var registry = SkillVfxRegistry.Instance;
                var eliteVfx = registry != null ? registry.GetVfxForSkill("elite_spawn") : null;
                if (eliteVfx != null)
                    SpriteSheetVfx.Spawn(eliteVfx, monster.position, 1.2f);
                else
                    SkillVfx.SpawnCircle(monster.position, 0.6f, Color.red, 0.4f);
            }
        }

        /// <summary>
        /// 보스 몬스터 스폰 연출. 일반 대비 2~3배 스케일 + 빛나는 오라.
        /// </summary>
        public void PlayBossSpawn(Transform monster, bool isChapterBoss)
        {
            if (monster == null) return;

            float targetScale = isChapterBoss ? _chapterBossScale : _bossScale;

            // 0에서 타겟 스케일로 팝
            monster.localScale = Vector3.zero;
            var seq = DOTween.Sequence();
            seq.Append(monster.DOScale(targetScale * 1.2f, 0.3f).SetEase(Ease.OutBack));
            seq.Append(monster.DOScale(targetScale, 0.15f).SetEase(Ease.InOutQuad));
            seq.SetLink(monster.gameObject);

            // 오라 이펙트: 반복 펄스 글로우
            CreateBossAura(monster, targetScale);

            // 스폰 VFX
            var registry = SkillVfxRegistry.Instance;
            var bossVfx = registry != null ? registry.GetVfxForSkill("boss_spawn") : null;
            if (bossVfx != null)
                SpriteSheetVfx.Spawn(bossVfx, monster.position, targetScale);
            else
                SkillVfx.SpawnCircle(monster.position, _bossAuraRadius * targetScale, _bossAuraColor, 0.5f);

            // 화면 흔들림
            ScreenShakeManager.Instance?.ShakeHeavy();
        }

        /// <summary>
        /// 보스 주변에 펄스하는 글로우 오라 SpriteRenderer를 생성한다.
        /// </summary>
        private void CreateBossAura(Transform boss, float bossScale)
        {
            // 기존 BossAura 제거 (풀 리스폰 시 중복 방지)
            for (int i = boss.childCount - 1; i >= 0; i--)
            {
                var child = boss.GetChild(i);
                if (child.name == "BossAura")
                {
                    child.DOKill();
                    Object.Destroy(child.gameObject);
                }
            }

            var auraObj = new GameObject("BossAura");
            auraObj.transform.SetParent(boss, false);
            auraObj.transform.localPosition = Vector3.zero;
            auraObj.transform.localScale = Vector3.one * 1.5f;

            var sr = auraObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = _bossAuraColor;
            sr.sortingOrder = -1;

            // 펄스 애니메이션 (0.8 ↔ 1.2 스케일, 반복)
            auraObj.transform.DOScale(1.8f, 0.8f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(auraObj);

            // 알파 펄스
            DOTween.To(() => sr.color.a, a =>
                {
                    var c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, a);
                }, _bossAuraColor.a * 0.3f, 0.8f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(auraObj);
        }

        /// <summary>
        /// 간단한 원형 스프라이트를 코드로 생성한다.
        /// </summary>
        private static Sprite _cachedCircleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = alpha * alpha; // 가장자리 소프트
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            return _cachedCircleSprite;
        }

        /// <summary>
        /// 몬스터 사망 연출. Dead 애니메이션은 MonsterController에서 처리하므로
        /// 여기서는 히트 VFX만 재생한다. (DOScale 찌그러지기 연출 제거)
        /// </summary>
        public void PlayDeath(Transform monster, Vector3 pos)
        {
            if (monster == null) return;

            var registry = SkillVfxRegistry.Instance;
            var hitVfx = registry != null ? registry.GetDefaultHitVfx(JobType.Warrior) : null;
            if (hitVfx != null)
                SpriteSheetVfx.Spawn(hitVfx, pos, 1.5f);
            else
                SkillVfx.SpawnHit(pos, Color.white, 0.3f);
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (evt.Monster == null) return;
            PlayDeath(evt.Monster.transform, evt.Position);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
