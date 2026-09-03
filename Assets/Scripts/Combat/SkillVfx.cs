using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 스킬 시각 효과. 스프라이트 시트 VFX 우선, 없으면 런타임 2D 도형 폴백.
    /// 오브젝트 풀링으로 GC 스파이크 최소화.
    /// </summary>
    public static class SkillVfx
    {
        private static Sprite _circleSprite;
        private static Sprite _ringSprite;

        private const int SPRITE_RES = 64;
        private const int VFX_ORDER = 10;

        // 스킬 타입별 최대 수명 (루핑 VFX 강제 회수용)
        private const float ACTIVE_VFX_LIFETIME = 1.5f;
        private const float AWAKENING_VFX_LIFETIME = 3f;
        private const float BUFF_VFX_LIFETIME = 2f;

        #region VFX 오브젝트 풀

        private static readonly Dictionary<string, Queue<GameObject>> _vfxPool = new();
        // 2026-04-23 AoE 피크 대응: 40몹 광역 히트 = key별 ~40 동시. Destroy() 스파이크 방지.
        private const int MAX_POOL_PER_KEY = 50;

        private static GameObject GetFromPool(string key)
        {
            if (_vfxPool.TryGetValue(key, out var queue))
            {
                // 파괴된 오브젝트를 스킵하면서 유효한 것을 찾는다
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj != null)
                    {
                        obj.SetActive(true);
                        return obj;
                    }
                    // 파괴된 오브젝트 — 스킵
                }
            }
            return null;
        }

        private static void ReturnToPool(string key, GameObject obj)
        {
            if (obj == null) return;

            if (!_vfxPool.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>();
                _vfxPool[key] = queue;
            }

            if (queue.Count >= MAX_POOL_PER_KEY)
            {
                Object.Destroy(obj);
                return;
            }

            obj.SetActive(false);
            queue.Enqueue(obj);
        }

        /// <summary>풀 전체 정리. 씬 전환 시 호출.</summary>
        public static void ClearPool()
        {
            foreach (var kvp in _vfxPool)
            {
                while (kvp.Value.Count > 0)
                {
                    var obj = kvp.Value.Dequeue();
                    if (obj != null) Object.Destroy(obj);
                }
            }
            _vfxPool.Clear();
        }

        #endregion

        #region 스프라이트 생성 (런타임 텍스처)

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            var tex = new Texture2D(SPRITE_RES, SPRITE_RES, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = SPRITE_RES / 2f;
            float radius = center - 1f;

            for (int y = 0; y < SPRITE_RES; y++)
            {
                for (int x = 0; x < SPRITE_RES; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _circleSprite = Sprite.Create(tex,
                new Rect(0, 0, SPRITE_RES, SPRITE_RES),
                Vector2.one * 0.5f, SPRITE_RES);
            return _circleSprite;
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            var tex = new Texture2D(SPRITE_RES, SPRITE_RES, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = SPRITE_RES / 2f;
            float outer = center - 1f;
            float inner = outer * 0.82f;

            for (int y = 0; y < SPRITE_RES; y++)
            {
                for (int x = 0; x < SPRITE_RES; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerA = Mathf.Clamp01((outer - dist) / 1.5f);
                    float innerA = Mathf.Clamp01((dist - inner) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, outerA * innerA));
                }
            }

            tex.Apply();
            _ringSprite = Sprite.Create(tex,
                new Rect(0, 0, SPRITE_RES, SPRITE_RES),
                Vector2.one * 0.5f, SPRITE_RES);
            return _ringSprite;
        }

        #endregion

        #region 2D 도형 이펙트

        /// <summary>원형 범위 이펙트 (확장 후 페이드)</summary>
        public static void SpawnCircle(Vector3 center, float radius, Color color, float duration = 0.4f)
        {
            if (UIState.IsAnyPanelOpen) return;
            var go = CreateVfxObj("circle", "Vfx_Circle", center);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = WithAlpha(color, 0.4f);

            float scale = radius * 2f;
            go.transform.localScale = Vector3.one * (scale * 0.3f);

            DOTween.Sequence()
                .Append(go.transform.DOScale(scale, duration * 0.4f).SetEase(Ease.OutBack))
                .AppendInterval(duration * 0.3f)
                .Append(FadeRenderer(sr, 0f, duration * 0.3f))
                .OnComplete(() => ReturnToPool("circle", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("circle", go); });
        }

        /// <summary>링 이펙트 (확장하며 페이드)</summary>
        public static void SpawnRing(Vector3 center, float radius, Color color, float duration = 0.5f)
        {
            if (UIState.IsAnyPanelOpen) return;
            var go = CreateVfxObj("ring", "Vfx_Ring", center);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetRingSprite();
            sr.color = WithAlpha(color, 0.7f);

            float scale = radius * 2f;
            go.transform.localScale = Vector3.one * (scale * 0.2f);

            DOTween.Sequence()
                .Append(go.transform.DOScale(scale, duration * 0.7f).SetEase(Ease.OutQuad))
                .Join(FadeRenderer(sr, 0f, duration).SetEase(Ease.InQuad))
                .OnComplete(() => ReturnToPool("ring", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("ring", go); });
        }

        /// <summary>슬래시/돌진 이펙트 (방향성 타원)</summary>
        public static void SpawnSlash(Vector3 position, Vector2 direction, float length, float width,
            Color color, float duration = 0.3f)
        {
            if (UIState.IsAnyPanelOpen) return;
            Vector3 center = position + (Vector3)(direction.normalized * length * 0.4f);
            var go = CreateVfxObj("slash", "Vfx_Slash", center);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = WithAlpha(color, 0.6f);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = new Vector3(length * 0.3f, width * 0.3f, 1f);

            DOTween.Sequence()
                .Append(go.transform.DOScale(new Vector3(length, width, 1f), duration * 0.35f)
                    .SetEase(Ease.OutQuad))
                .Append(FadeRenderer(sr, 0f, duration * 0.65f))
                .OnComplete(() => ReturnToPool("slash", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("slash", go); });
        }

        /// <summary>히트 임팩트 (작은 원 플래시). 레지스트리에 기본 히트 VFX가 있으면 우선 사용.</summary>
        public static void SpawnHit(Vector3 position, Color color, float size = 0.8f)
        {
            if (UIState.IsAnyPanelOpen) return;
            // 레지스트리 기본 히트 VFX 우선
            if (SkillVfxRegistry.Instance != null && SkillVfxRegistry.Instance.GetDefaultHitVfx(JobType.Warrior) != null)
            {
                SpriteSheetVfx.Spawn(SkillVfxRegistry.Instance.GetDefaultHitVfx(JobType.Warrior),
                    position, Mathf.Max(size, 1.5f));
                return;
            }

            // 2D 도형 폴백
            var go = CreateVfxObj("hit", "Vfx_Hit", position);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = WithAlpha(color, 0.8f);
            sr.sortingOrder = VFX_ORDER + 1;

            go.transform.localScale = Vector3.one * (size * 0.2f);

            DOTween.Sequence()
                .Append(go.transform.DOScale(size, 0.1f).SetEase(Ease.OutQuad))
                .Append(FadeRenderer(sr, 0f, 0.2f))
                .OnComplete(() => ReturnToPool("hit", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("hit", go); });
        }

        /// <summary>폭발 이펙트 (원 + 링 복합)</summary>
        public static void SpawnExplosion(Vector3 center, float radius, Color coreColor, Color ringColor,
            float duration = 0.6f)
        {
            SpawnCircle(center, radius * 0.7f, coreColor, duration);
            SpawnRing(center, radius, ringColor, duration * 1.2f);
        }

        /// <summary>연속 임팩트 (유성우, 다연장 등)</summary>
        public static void SpawnMultiImpact(Vector3 center, float radius, Color color,
            int count, float totalDuration)
        {
            var anchor = CreateVfxObj("multi", "Vfx_MultiAnchor", center);

            var seq = DOTween.Sequence().SetLink(anchor);
            float interval = totalDuration / Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(0f, radius * 0.7f);
                Vector3 pos = center + new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0f);
                Vector3 cp = pos;
                float sz = radius * Random.Range(0.2f, 0.35f);

                seq.AppendCallback(() =>
                {
                    SpawnCircle(cp, sz, color, 0.3f);
                    SpawnRing(cp, sz * 1.4f, color * 0.7f, 0.35f);
                });
                if (i < count - 1) seq.AppendInterval(interval);
            }

            seq.OnComplete(() => ReturnToPool("multi", anchor));
            seq.OnKill(() => { if (anchor != null && anchor.activeSelf) ReturnToPool("multi", anchor); });
        }

        /// <summary>DoT 틱 이펙트</summary>
        public static void SpawnDotTick(Vector3 position, Color color, float size = 0.4f)
        {
            var go = CreateVfxObj("dot", "Vfx_Dot", position + new Vector3(
                Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), 0f));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = WithAlpha(color, 0.6f);
            sr.sortingOrder = VFX_ORDER + 1;

            go.transform.localScale = Vector3.one * size;

            DOTween.Sequence()
                .Append(go.transform.DOScale(size * 1.5f, 0.15f))
                .Join(go.transform.DOMoveY(go.transform.position.y + 0.6f, 0.3f))
                .Append(FadeRenderer(sr, 0f, 0.15f))
                .OnComplete(() => ReturnToPool("dot", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("dot", go); });
        }

        /// <summary>빙결 이펙트</summary>
        public static void SpawnFreezeEffect(Vector3 position, float duration)
        {
            Color ice = new Color(0.3f, 0.7f, 1f);
            SpawnRing(position, 0.8f, ice, 0.4f);

            var go = CreateVfxObj("freeze", "Vfx_Freeze", position);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = WithAlpha(ice, 0.3f);
            sr.sortingOrder = VFX_ORDER - 1;

            go.transform.localScale = Vector3.one * 1.2f;

            DOTween.Sequence()
                .Append(FadeRenderer(sr, 0.15f, duration * 0.8f).SetEase(Ease.InQuad))
                .Append(FadeRenderer(sr, 0f, duration * 0.2f))
                .OnComplete(() => ReturnToPool("freeze", go))
                .OnKill(() => { if (go != null && go.activeSelf) ReturnToPool("freeze", go); });
        }

        /// <summary>감속 이펙트</summary>
        public static void SpawnSlowEffect(Vector3 position)
        {
            Color slow = new Color(0.4f, 0.6f, 1f);
            SpawnHit(position + Vector3.down * 0.3f, slow, 0.6f);
        }

        #endregion

        #region 유틸 (스프라이트 시트 VFX)

        /// <summary>두 위치 사이의 Z축 각도 (도).</summary>
        private static float DirectionAngle(Vector3 from, Vector3 to)
        {
            Vector2 dir = ((Vector2)to - (Vector2)from).normalized;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        #endregion

        #region 액티브 스킬 VFX (CharacterCombat에서 호출)

        /// <summary>
        /// 액티브 기본공격 VFX. 스프라이트 시트 우선, 없으면 2D 도형 폴백.
        /// facingScaleX: 플레이어 transform.localScale.x (-1=오른쪽, 1=왼쪽)
        /// </summary>
        public static void SpawnActiveAttackVfx(Vector3 playerPos, CombatStats target,
            SkillDataSO skill, float areaRange, float facingScaleX = -1f)
        {
            if (skill == null) return;

            // 스프라이트 시트 VFX가 있으면 우선 사용
            if (skill.vfxSheet != null)
            {
                SpawnActiveSheetVfx(playerPos, target, skill, areaRange, facingScaleX);
                return;
            }

            // 레지스트리에서 직업별 기본 VFX 조회
            if (SkillVfxRegistry.Instance != null)
            {
                var jobType = InferJobType(skill);
                var registryVfx = SkillVfxRegistry.Instance.GetDefaultAttackVfx(jobType);
                if (registryVfx != null)
                {
                    float rotAngle = 0f;
                    if (target != null && !target.IsDead)
                    {
                        Vector2 dir = ((Vector2)target.transform.position - (Vector2)playerPos).normalized;
                        rotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    }
                    else
                    {
                        bool facingRight = facingScaleX < 0f;
                        rotAngle = facingRight ? 0f : 180f;
                    }

                    float worldSize = areaRange > 0f ? Mathf.Max(areaRange * 2f, 3f) : 3f;
                    SpriteSheetVfx.Spawn(registryVfx, playerPos, worldSize,
                        maxLifetime: ACTIVE_VFX_LIFETIME, rotation: rotAngle);

                    // 히트 이펙트
                    var hitVfx = SkillVfxRegistry.Instance.GetDefaultHitVfx(jobType);
                    if (hitVfx != null && target != null && !target.IsDead)
                        SpriteSheetVfx.Spawn(hitVfx, target.transform.position, 1.5f);

                    return;
                }
            }

            // 폴백: 히트 VFX만 스폰 (링/서클 사용 안 함)
            if (target != null && !target.IsDead)
            {
                var jobType = InferJobType(skill);
                var hitVfx = SkillVfxRegistry.Instance != null
                    ? SkillVfxRegistry.Instance.GetDefaultHitVfx(jobType) : null;
                if (hitVfx != null)
                    SpriteSheetVfx.Spawn(hitVfx, target.transform.position, 1.5f);
            }
        }

        /// <summary>
        /// 액티브 스킬 스프라이트 시트 VFX 스폰.
        ///
        /// 방향: 타겟 방향으로 회전 (flipX 대신 rotation 사용)
        /// 크기: 실제 판정 범위(areaRange * 2)와 정확히 일치
        /// 위치: 플레이어 중심 (판정 원점과 동일)
        /// </summary>
        private static void SpawnActiveSheetVfx(Vector3 playerPos, CombatStats target,
            SkillDataSO skill, float areaRange, float facingScaleX)
        {
            Color color = GetSkillColor(skill);

            // 타겟 방향으로 VFX 회전 (flipX 대신 rotation 사용)
            float rotAngle = 0f;
            if (target != null && !target.IsDead)
            {
                Vector2 dir = ((Vector2)target.transform.position - (Vector2)playerPos).normalized;
                rotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
            else
            {
                // 타겟 없으면 facing 방향
                bool facingRight = facingScaleX < 0f;
                rotAngle = facingRight ? 0f : 180f;
            }

            Vector3 vfxPos = playerPos;

            if (areaRange > 0f)
            {
                // 범위 공격: VFX 크기 = 실제 판정 범위와 일치 (areaRange * 2 = 지름)
                float worldSize = Mathf.Max(areaRange * 2f, 3f);
                var vfx = SpriteSheetVfx.Spawn(skill.vfxSheet, vfxPos, worldSize,
                    maxLifetime: ACTIVE_VFX_LIFETIME, rotation: rotAngle);

                if (vfx == null)
                    Debug.LogWarning("[SkillVfx] 범위 VFX 스폰 실패 — vfxSheet 프레임 확인 필요");
            }
            else
            {
                // 단일 타겟: 타겟 방향으로 슬래시
                var vfx = SpriteSheetVfx.Spawn(skill.vfxSheet, vfxPos, 3f,
                    maxLifetime: ACTIVE_VFX_LIFETIME, rotation: rotAngle);

                if (target != null && !target.IsDead)
                    SpawnHit(target.transform.position, color, 0.5f);

                if (vfx == null)
                    Debug.LogWarning("[SkillVfx] 단일타겟 VFX 스폰 실패 — vfxSheet 프레임 확인 필요");
            }
        }

        #endregion

        #region 각성기 VFX (SkillSystem에서 호출)

        /// <summary>각성기 VFX. 스프라이트 시트 우선, 없으면 2D 도형 폴백.</summary>
        /// <param name="facingScaleX">플레이어 localScale.x (-1=오른쪽, 1=왼쪽). VFX 방향 결정.</param>
        /// <param name="target">현재 타겟. 있으면 타겟 방향으로 회전.</param>
        public static void SpawnAwakeningVfx(Vector3 casterPos, SkillDataSO skill,
            float facingScaleX = -1f, CombatStats target = null)
        {
            if (skill == null) return;

            // 스프라이트 시트 VFX — 범위 지름에 맞춤
            if (skill.vfxSheet != null)
            {
                float range = skill.range > 0f ? skill.range : 3f;
                float worldSize = range * 2f;
                Color color = GetSkillColor(skill);

                // 타겟 방향으로 VFX 회전
                float rotAngle = 0f;
                if (target != null && !target.IsDead)
                {
                    Vector2 dir = ((Vector2)target.transform.position - (Vector2)casterPos).normalized;
                    rotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
                else
                {
                    bool facingRight = facingScaleX < 0f;
                    rotAngle = facingRight ? 0f : 180f;
                }

                SpriteSheetVfx.Spawn(skill.vfxSheet, casterPos, worldSize,
                    maxLifetime: AWAKENING_VFX_LIFETIME, rotation: rotAngle);
                return;
            }

            // 폴백: 레지스트리 공격 VFX 사용 (링/서클 사용 안 함)
            var jobType = InferJobType(skill);
            float fallbackRange = skill.range > 0f ? skill.range : 3f;

            if (SkillVfxRegistry.Instance != null)
            {
                var atkVfx = SkillVfxRegistry.Instance.GetDefaultAttackVfx(jobType);
                if (atkVfx != null)
                {
                    float rotAngle2 = 0f;
                    if (target != null && !target.IsDead)
                    {
                        Vector2 dir = ((Vector2)target.transform.position - (Vector2)casterPos).normalized;
                        rotAngle2 = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    }
                    SpriteSheetVfx.Spawn(atkVfx, casterPos, fallbackRange * 2f,
                        maxLifetime: AWAKENING_VFX_LIFETIME, rotation: rotAngle2);
                }
            }
        }

        #endregion

        #region 버프 VFX

        /// <summary>버프 활성화 VFX + 지속 오라. 스프라이트 시트 우선, 없으면 2D 도형 버스트.</summary>
        public static void SpawnBuffVfx(Vector3 casterPos, SkillDataSO skill, Transform parent = null)
        {
            if (skill == null) return;

            Color color = GetSkillColor(skill);

            // 버스트 이펙트 (1회성)
            if (skill.vfxSheet != null)
            {
                SpriteSheetVfx.Spawn(skill.vfxSheet, casterPos, 3f,
                    parent: parent, maxLifetime: BUFF_VFX_LIFETIME);
            }
            else
            {
                // 폴백: 레지스트리 히트 VFX 사용
                var jobType = InferJobType(skill);
                var hitVfx = SkillVfxRegistry.Instance != null
                    ? SkillVfxRegistry.Instance.GetDefaultHitVfx(jobType) : null;
                if (hitVfx != null)
                    SpriteSheetVfx.Spawn(hitVfx, casterPos, 2f, parent: parent, maxLifetime: BUFF_VFX_LIFETIME);
            }

            // 지속 오라 (버프 시전 시 캐릭터 주변 펄스 링)
            if (parent != null && skill.duration > 0f)
                SpawnBuffAura(parent, color, skill.duration);
        }

        /// <summary>
        /// 버프 활성 동안 캐릭터 주변에 오라 SpriteRenderer를 표시한다.
        /// 알파 펄스 (0.15→0.4→0.15 반복) 후 버프 종료 시 페이드아웃.
        /// </summary>
        private static void SpawnBuffAura(Transform parent, Color color, float duration)
        {
            var go = new GameObject("Vfx_BuffAura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetRingSprite();
            sr.color = WithAlpha(color, 0.15f);
            sr.sortingOrder = VFX_ORDER - 1;

            go.transform.localScale = Vector3.one * 2f;

            // 알파 펄스 루프
            float pulseTime = Mathf.Min(duration - 0.3f, duration * 0.85f);
            int loopCount = Mathf.Max(1, Mathf.RoundToInt(pulseTime / 0.8f));

            var seq = DOTween.Sequence();

            // 펄스 루프: 0.15 → 0.4 → 0.15
            seq.Append(
                DOTween.To(
                    () => sr.color.a,
                    a => sr.color = WithAlpha(color, a),
                    0.4f, 0.4f
                ).SetEase(Ease.InOutSine)
            );
            seq.Append(
                DOTween.To(
                    () => sr.color.a,
                    a => sr.color = WithAlpha(color, a),
                    0.15f, 0.4f
                ).SetEase(Ease.InOutSine)
            );
            seq.SetLoops(loopCount, LoopType.Restart);

            // 페이드 아웃
            var fadeSeq = DOTween.Sequence();
            fadeSeq.AppendInterval(pulseTime);
            fadeSeq.Append(
                DOTween.To(
                    () => sr.color.a,
                    a => sr.color = WithAlpha(color, a),
                    0f, 0.3f
                ).SetEase(Ease.InQuad)
            );
            fadeSeq.OnComplete(() =>
            {
                if (go != null) Object.Destroy(go);
            });
            fadeSeq.SetLink(go);

            seq.SetLink(go);
        }

        #endregion

        #region 투사체 트레일 VFX

        /// <summary>
        /// 원거리 공격의 투사체 트레일 이펙트.
        /// 시작점에서 타겟까지 작은 잔상 파티클을 순차 스폰한다.
        /// </summary>
        public static void SpawnProjectileTrail(Vector3 from, Vector3 to, Color color,
            int particleCount = 4, float totalDuration = 0.25f)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.1f) return;

            var anchor = new GameObject("Vfx_Trail");
            anchor.transform.position = from;

            var seq = DOTween.Sequence().SetLink(anchor);
            float interval = totalDuration / Mathf.Max(1, particleCount);

            for (int i = 0; i < particleCount; i++)
            {
                float t = (float)(i + 1) / (particleCount + 1);
                Vector3 pos = Vector3.Lerp(from, to, t);
                float size = 0.2f + (1f - t) * 0.15f; // 선두가 약간 더 큼

                seq.InsertCallback(i * interval, () =>
                {
                    SpawnDotTick(pos, color, size);
                });
            }

            // 착탄 히트
            seq.InsertCallback(totalDuration, () =>
            {
                SpawnHit(to, color, 0.5f);
            });

            seq.OnComplete(() => Object.Destroy(anchor));
        }

        /// <summary>
        /// 액티브 공격 시 원거리 직업이면 투사체 트레일을 추가로 스폰한다.
        /// CharacterCombat에서 단일타겟 히트 후 호출.
        /// </summary>
        public static void SpawnRangedTrailIfNeeded(Vector3 playerPos, CombatStats target,
            SkillDataSO skill)
        {
            if (skill == null || target == null || target.IsDead) return;

            var jobType = InferJobType(skill);
            // 전사 계열은 근접이므로 트레일 생략
            if (jobType == JobType.Warrior) return;

            Color color = GetSkillColor(skill);
            SpawnProjectileTrail(playerPos, target.transform.position, color);
        }

        #endregion

        #region 색상 유틸

        /// <summary>스킬 데이터에서 적절한 VFX 색상을 결정한다.</summary>
        public static Color GetSkillColor(SkillDataSO skill)
        {
            if (skill == null) return Color.white;

            switch (skill.specialEffect)
            {
                case SkillEffect.Burn: return new Color(1f, 0.5f, 0.1f);
                case SkillEffect.Freeze: return new Color(0.3f, 0.7f, 1f);
                case SkillEffect.Stun: return new Color(1f, 0.9f, 0.2f);
                case SkillEffect.Knockback: return new Color(1f, 0.4f, 0.2f);
                case SkillEffect.Lifesteal: return new Color(0.8f, 0.15f, 0.25f);
                case SkillEffect.Slow: return new Color(0.4f, 0.6f, 1f);
                case SkillEffect.Penetrate: return new Color(0.2f, 1f, 0.4f);
                case SkillEffect.MultiShot: return new Color(0.3f, 0.9f, 0.6f);
            }

            if (!string.IsNullOrEmpty(skill.id))
            {
                string id = skill.id;
                if (id.StartsWith("warrior") || id.StartsWith("knight") ||
                    id.StartsWith("warlord") || id.StartsWith("titan") ||
                    id.StartsWith("dragonslayer"))
                    return new Color(1f, 0.3f, 0.2f);

                if (id.StartsWith("archer") || id.StartsWith("scout") ||
                    id.StartsWith("windwalker") || id.StartsWith("hawkeye") ||
                    id.StartsWith("stormbringer"))
                    return new Color(0.2f, 0.9f, 0.4f);

                if (id.StartsWith("mage") || id.StartsWith("sorcerer") ||
                    id.StartsWith("sage") || id.StartsWith("runemaster") ||
                    id.StartsWith("archmage"))
                    return new Color(0.5f, 0.3f, 1f);
            }

            return Color.white;
        }

        #endregion

        #region 내부 유틸

        /// <summary>풀에서 가져오거나 새로 생성. transform/SpriteRenderer 상태를 초기화.</summary>
        private static GameObject CreateVfxObj(string poolKey, string name, Vector3 position)
        {
            var go = GetFromPool(poolKey);
            if (go == null)
            {
                go = new GameObject(name);
                go.AddComponent<SpriteRenderer>();
            }

            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = VFX_ORDER;
            sr.color = Color.white;
            sr.sprite = null;

            return go;
        }

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        private static Tween FadeRenderer(SpriteRenderer sr, float targetAlpha, float duration)
        {
            return DOTween.To(
                () => sr.color,
                c => sr.color = c,
                new Color(sr.color.r, sr.color.g, sr.color.b, targetAlpha),
                duration);
        }

        /// <summary>스킬 ID에서 직업 타입을 추론한다.</summary>
        private static JobType InferJobType(SkillDataSO skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.id))
                return JobType.Warrior;

            string id = skill.id;
            if (id.StartsWith("archer") || id.StartsWith("scout") ||
                id.StartsWith("windwalker") || id.StartsWith("hawkeye") ||
                id.StartsWith("stormbringer"))
                return JobType.Archer;

            if (id.StartsWith("mage") || id.StartsWith("sorcerer") ||
                id.StartsWith("sage") || id.StartsWith("runemaster") ||
                id.StartsWith("archmage"))
                return JobType.Mage;

            // dragonslayer는 전사 계열
            return JobType.Warrior;
        }

        #endregion
    }
}
