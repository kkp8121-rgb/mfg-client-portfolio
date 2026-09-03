---
read_count: 3
last_read: "2026-04-07"
status: reference
---

# 사운드 & 비주얼 연출 가이드

> mkLike 오디오/비주얼 연출 기획서
> 모든 트윈은 DOTween, 비동기는 UniTask 사용

---

## 1. BGM 설계

### 1-1. 파일 구조

```
Assets/Audio/BGM/
  bgm_lobby.ogg            -- 로비/메뉴
  bgm_chapter_forest.ogg   -- 챕터 1~3 (숲)
  bgm_chapter_cave.ogg     -- 챕터 4~6 (동굴)
  bgm_chapter_volcano.ogg  -- 챕터 7~9 (화산)
  bgm_chapter_sky.ogg       -- 챕터 10~12 (하늘)
  bgm_chapter_abyss.ogg    -- 챕터 13+ (심연)
  bgm_boss.ogg             -- 보스전
  bgm_dungeon.ogg          -- 성장 던전
  bgm_gacha.ogg            -- 가챠 연출
  bgm_worldboss.ogg        -- 월드보스/레이드
```

### 1-2. 사양

| 트랙 | 길이 | BPM | 분위기 | 루프 |
|------|------|-----|--------|------|
| bgm_lobby | 60~90초 | 90~100 | 편안한 판타지, 하프+피아노 | 무한 루프 |
| bgm_chapter_forest | 90~120초 | 120~130 | 경쾌한 액션, 현악+퍼커션 | 무한 루프 |
| bgm_chapter_cave | 90~120초 | 110~120 | 어두운 긴장감, 저음현+에코 | 무한 루프 |
| bgm_chapter_volcano | 90~120초 | 130~140 | 격렬한 전투, 오케스트라+브라스 | 무한 루프 |
| bgm_chapter_sky | 90~120초 | 115~125 | 신비로운 고양감, 합창+벨 | 무한 루프 |
| bgm_chapter_abyss | 90~120초 | 140~150 | 긴박+웅장, 풀오케스트라 | 무한 루프 |
| bgm_boss | 60~90초 | 150~160 | 극한 긴장, 스트링 트레몰로+타이코 | 무한 루프 |
| bgm_dungeon | 60~90초 | 135~145 | 긴박한 제한시간, 일렉+퍼커션 | 무한 루프 |
| bgm_gacha | 30~40초 | 100~110 | 기대감 고조, 점진적 빌드업 | 1회 재생 |
| bgm_worldboss | 90~120초 | 155~165 | 대규모 전투, 에픽 오케스트라 | 무한 루프 |

### 1-3. 전환 규칙

| 상황 | 전환 방식 | 타이밍 |
|------|-----------|--------|
| 스테이지 진입 | 크로스페이드 | 1.5초 |
| 보스 스테이지 진입 | 페이드아웃 0.5초 → 드럼롤 1초 → bgm_boss 페이드인 0.5초 | 총 2초 |
| 보스 처치 → 다음 챕터 | bgm_boss 페이드아웃 1초 → 승리 징글 2초 → 다음 BGM 페이드인 1초 | 총 4초 |
| 던전 입장 | 컷 전환 (즉시 교체, 페이드인 0.3초) | 0.3초 |
| 가챠 팝업 오픈 | 전투 BGM 볼륨 30%로 덕 → bgm_gacha 재생 | 0.5초 |
| 가챠 종료 | bgm_gacha 페이드아웃 0.5초 → 전투 BGM 복귀 | 0.5초 |

### 1-4. AudioManager BGM 전환 DOTween 시퀀스

```csharp
// 크로스페이드 전환
public async UniTaskVoid CrossfadeBgm(AudioClip newClip, float duration = 1.5f)
{
    var seq = DOTween.Sequence();
    seq.Append(DOTween.To(() => _bgmSource.volume, v => _bgmSource.volume = v, 0f, duration * 0.5f));
    seq.AppendCallback(() => { _bgmSource.clip = newClip; _bgmSource.Play(); });
    seq.Append(DOTween.To(() => _bgmSource.volume, v => _bgmSource.volume = v, _bgmVolume, duration * 0.5f));
    seq.SetLink(gameObject);
    await seq.ToUniTask(cancellationToken: destroyCancellationToken);
}

// 보스 등장 전환
public async UniTaskVoid BossBgmTransition(AudioClip bossClip)
{
    var seq = DOTween.Sequence();
    seq.Append(DOTween.To(() => _bgmSource.volume, v => _bgmSource.volume = v, 0f, 0.5f));
    seq.AppendInterval(1.0f); // 드럼롤/무음 긴장
    seq.AppendCallback(() => { _bgmSource.clip = bossClip; _bgmSource.Play(); });
    seq.Append(DOTween.To(() => _bgmSource.volume, v => _bgmSource.volume = v, _bgmVolume, 0.5f));
    seq.SetLink(gameObject);
    await seq.ToUniTask(cancellationToken: destroyCancellationToken);
}
```

---

## 2. SFX 설계

### 2-1. 파일 구조

```
Assets/Audio/SFX/
  Attack/
    sfx_sword_swing_01.ogg ~ 03    -- 전사 기본공격 (3종 랜덤)
    sfx_bow_release_01.ogg ~ 03    -- 궁수 기본공격
    sfx_magic_cast_01.ogg ~ 03     -- 마법사 기본공격
    sfx_sword_hit_01.ogg ~ 03      -- 근접 피격
    sfx_arrow_hit_01.ogg ~ 03      -- 원거리 피격
    sfx_magic_hit_01.ogg ~ 03      -- 마법 피격
    sfx_crit_hit.ogg               -- 치명타 (공통)
  Skill/
    sfx_skill_slash.ogg            -- 참격 계열 (강타/선풍참/멸살)
    sfx_skill_charge.ogg           -- 돌진 계열
    sfx_skill_warcry.ogg           -- 함성/고양 버프
    sfx_skill_bow_multi.ogg        -- 다발/연사
    sfx_skill_bow_storm.ogg        -- 폭풍/태풍
    sfx_skill_fire.ogg             -- 화염 계열
    sfx_skill_ice.ogg              -- 빙결 계열
    sfx_skill_lightning.ogg        -- 번개 계열
    sfx_skill_arcane.ogg           -- 비전/차원 계열
    sfx_skill_meteor.ogg           -- 유성우/대지 분쇄
    sfx_skill_buff_activate.ogg    -- 버프 활성화 (공통)
    sfx_skill_awakening.ogg        -- 각성기 발동 (공통 레이어)
  Monster/
    sfx_monster_hit_01.ogg ~ 03    -- 몬스터 피격
    sfx_monster_die_01.ogg ~ 03    -- 몬스터 사망
    sfx_boss_roar.ogg              -- 보스 등장 포효
    sfx_boss_die.ogg               -- 보스 사망
  System/
    sfx_levelup.ogg                -- 레벨업
    sfx_job_advance.ogg            -- 전직
    sfx_enhance_success.ogg        -- 강화 성공
    sfx_enhance_fail.ogg           -- 강화 실패
    sfx_enhance_destroy.ogg        -- 장비 파괴
    sfx_starforce_up.ogg           -- 성급 상승
    sfx_potential_change.ogg       -- 잠재능력 변경
    sfx_gold_pickup.ogg            -- 골드 획득
    sfx_exp_pickup.ogg             -- 경험치 획득
    sfx_item_drop.ogg              -- 아이템 드롭
    sfx_equip.ogg                  -- 장비 장착
    sfx_unequip.ogg                -- 장비 해제
  Gacha/
    sfx_gacha_spin.ogg             -- 가챠 회전/빌드업 (2~3초)
    sfx_gacha_stop.ogg             -- 회전 정지
    sfx_gacha_reveal_normal.ogg    -- Normal 등급 공개
    sfx_gacha_reveal_rare.ogg      -- Rare 등급 공개
    sfx_gacha_reveal_epic.ogg      -- Epic 등급 공개
    sfx_gacha_reveal_unique.ogg    -- Unique 등급 공개
    sfx_gacha_reveal_legendary.ogg -- Legendary 등급 공개
    sfx_gacha_reveal_mythic.ogg    -- Mythic 등급 공개
    sfx_gacha_multi_result.ogg     -- 10연차 결과 일괄 공개
  UI/
    sfx_ui_tap.ogg                 -- 일반 버튼 터치
    sfx_ui_back.ogg                -- 뒤로가기/닫기
    sfx_ui_tab_switch.ogg          -- 탭 전환
    sfx_ui_popup_open.ogg          -- 팝업 열기
    sfx_ui_popup_close.ogg         -- 팝업 닫기
    sfx_ui_confirm.ogg             -- 확인 버튼
    sfx_ui_cancel.ogg              -- 취소 버튼
    sfx_ui_error.ogg               -- 불가능 알림
    sfx_ui_reward.ogg              -- 보상 수령
```

### 2-2. SFX 사양

| 카테고리 | 포맷 | 길이 | 채널 | 동시 재생 제한 |
|----------|------|------|------|---------------|
| 공격/피격 | OGG Vorbis | 0.1~0.5초 | Mono | 8 |
| 스킬 발동 | OGG Vorbis | 0.3~1.5초 | Mono | 4 |
| 몬스터 | OGG Vorbis | 0.2~0.8초 | Mono | 6 |
| 시스템 | OGG Vorbis | 0.5~2.0초 | Stereo | 2 |
| 가챠 | OGG Vorbis | 0.5~3.0초 | Stereo | 1 |
| UI | OGG Vorbis | 0.05~0.3초 | Mono | 3 |

### 2-3. 직업별 공격 SFX 차별화

| 직업 | 기본공격 SFX | 피격 SFX | 특징 |
|------|-------------|---------|------|
| 전사 | sfx_sword_swing (묵직한 금속 휘두름) | sfx_sword_hit (육중한 타격) | 낮은 피치, 강한 임팩트, 0.3초 |
| 궁수 | sfx_bow_release (시위 당김 → 발사) | sfx_arrow_hit (날카로운 관통) | 높은 피치, 빠른 연속, 0.15초 |
| 마법사 | sfx_magic_cast (에너지 충전 → 방출) | sfx_magic_hit (에너지 폭발) | 리버브 강, 꼬리 긴 0.4초 |

### 2-4. 스킬 SFX 매핑

| 스킬 카테고리 | SFX 파일 | 피치 변조 | 볼륨 |
|--------------|---------|-----------|------|
| 전사 Active (강타/돌진/선풍참/멸살) | sfx_skill_slash | 0.9~1.1 랜덤 | 1.0 |
| 전사 Buff (전투 함성/전의 고양/피의 서약/타이탄의 힘) | sfx_skill_warcry | 고정 1.0 | 0.8 |
| 전사 Awakening (분노의 일격/심판의 검/대지 분쇄/천지개벽) | sfx_skill_meteor + sfx_skill_awakening 레이어 | Tier에 비례 (T0=0.9, T3=1.2) | 1.0 |
| 궁수 Active (정조준/관통 사격/바람 꿰뚫기/멸절의 화살) | sfx_skill_bow_multi | 0.9~1.1 | 1.0 |
| 궁수 Buff (연사/바람의 가호/질풍/사냥의 시간) | sfx_skill_buff_activate | 고정 1.0 | 0.8 |
| 궁수 Awakening (화살비/폭풍 사격/태풍의 눈/천벌의 비) | sfx_skill_bow_storm + sfx_skill_awakening | Tier 비례 | 1.0 |
| 마법사 Active (마력탄/화염구/비전 화살/룬 폭발) | sfx_skill_fire 또는 sfx_skill_arcane | 0.9~1.1 | 1.0 |
| 마법사 Buff (마력 충전/연쇄 번개/차원 균열/절대 영역) | sfx_skill_lightning + sfx_skill_buff_activate | 고정 1.0 | 0.8 |
| 마법사 Awakening (화염 폭발/빙결 폭풍/유성우/종말의 룬) | sfx_skill_meteor + sfx_skill_ice/fire + sfx_skill_awakening | Tier 비례 | 1.0 |

---

## 3. 비주얼 연출

### 3-1. 가챠 연출 플로우

#### 1회 뽑기 플로우 (총 3.5초)

```
[0.0초] 화면 암전 (0.3초 페이드)
[0.3초] 빛 구체 등장 (중앙, 작은 크기에서 확대)
[0.5초] 구체 회전 + 빌드업 SFX 시작
[1.5초] 구체 최대 크기, 빛 입자 흡수
[2.0초] 구체 폭발 → 등급별 배경색 변화 + 등급 SFX
[2.3초] 결과 카드 등장 (아래에서 위로 슬라이드 + 스케일 바운스)
[2.8초] 등급 텍스트 + 아이템명 페이드인
[3.5초] 확인 버튼 활성화
```

#### 10연차 플로우 (총 8초)

```
[0.0초] 화면 암전 (0.3초)
[0.3초] 10개 빛 구체가 원형 배치로 등장
[1.0초] 구체들 회전 가속 + 빌드업
[2.5초] 첫 번째 구체 정지 → 폭발 → 카드 공개
[2.8초] 두 번째 ... (0.3초 간격)
       ...연쇄 공개 (일반 등급은 빠르게, 높은 등급은 슬로우)
[5.5초] 마지막(10번째) 공개 (10연차 보장 Rare+ → 강조 연출)
[6.0초] 전체 결과 그리드 표시
[7.0초] 최고 등급 아이템 하이라이트 바운스
[8.0초] 확인 버튼 활성화
```

#### DOTween 시퀀스 (1회 뽑기)

```csharp
public async UniTask PlaySingleGachaSequence(GachaResult result, CancellationToken ct)
{
    // 등급별 색상
    Color gradeColor = GetGradeColor(result.grade);

    var seq = DOTween.Sequence();

    // [0.0s] 암전
    seq.Append(DOTween.To(() => _overlay.color, c => _overlay.color = c,
        new Color(0, 0, 0, 0.85f), 0.3f));

    // [0.3s] 빛 구체 등장 + 확대
    seq.AppendCallback(() => {
        _orbGlow.gameObject.SetActive(true);
        _orbGlow.transform.localScale = Vector3.one * 0.1f;
        AudioManager.Instance.PlaySfx("sfx_gacha_spin");
    });
    seq.Append(_orbGlow.transform.DOScale(1.5f, 1.7f).SetEase(Ease.InQuad));
    seq.Join(_orbGlow.transform.DORotate(new Vector3(0, 0, 720), 1.7f, RotateMode.FastBeyond360)
        .SetEase(Ease.InQuad));

    // [2.0s] 폭발
    seq.AppendCallback(() => {
        _orbGlow.gameObject.SetActive(false);
        _burstFlash.color = gradeColor;
        _burstFlash.gameObject.SetActive(true);
        AudioManager.Instance.PlaySfx("sfx_gacha_stop");
        AudioManager.Instance.PlaySfx(GetGradeRevealSfx(result.grade));
        SetBackgroundGradeColor(gradeColor);
    });
    seq.Append(_burstFlash.transform.DOScale(3f, 0.15f).SetEase(Ease.OutQuad));
    seq.Append(DOTween.To(() => _burstFlash.color, c => _burstFlash.color = c,
        new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0f), 0.15f));

    // [2.3s] 결과 카드
    _resultCard.transform.localScale = Vector3.one * 0.3f;
    _resultCard.anchoredPosition = new Vector2(0, -200);
    seq.Append(_resultCard.DOAnchorPosY(0, 0.3f).SetEase(Ease.OutBack));
    seq.Join(_resultCard.DOScale(1f, 0.3f).SetEase(Ease.OutBack));

    // [2.8s] 등급 텍스트
    seq.Append(DOTween.To(() => _gradeText.alpha, a => _gradeText.alpha = a, 1f, 0.2f));

    // [3.5s] 확인 버튼
    seq.AppendInterval(0.5f);
    seq.AppendCallback(() => _confirmButton.gameObject.SetActive(true));

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
    await seq.ToUniTask(cancellationToken: ct);
}
```

### 3-2. 강화 연출

#### 성공 연출 (0.8초)

```csharp
public void PlayEnhanceSuccess(RectTransform itemSlot, int newStarCount)
{
    var seq = DOTween.Sequence();

    // 흔들림 + 플래시
    seq.Append(itemSlot.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8));
    seq.Join(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        new Color(1, 0.9f, 0.3f, 0.6f), 0.1f));
    seq.Append(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        Color.clear, 0.15f));

    // 별 애니메이션 (하나씩 점등)
    seq.AppendCallback(() => {
        AudioManager.Instance.PlaySfx("sfx_enhance_success");
        LightUpStar(newStarCount);
    });
    seq.Append(_starIcons[newStarCount - 1].DOScale(1.5f, 0.15f).SetEase(Ease.OutBack));
    seq.Append(_starIcons[newStarCount - 1].DOScale(1f, 0.1f));

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
}
```

#### 실패 연출 (0.6초)

```csharp
public void PlayEnhanceFail(RectTransform itemSlot, bool isDowngrade)
{
    var seq = DOTween.Sequence();

    // 흔들림 (좌우 떨림)
    seq.Append(itemSlot.DOShakePosition(0.3f, 10f, 20, 90, false, true, ShakeRandomnessMode.Harmonic));
    seq.Join(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        new Color(0.8f, 0.2f, 0.2f, 0.4f), 0.1f));
    seq.Append(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        Color.clear, 0.2f));

    seq.AppendCallback(() => {
        AudioManager.Instance.PlaySfx(isDowngrade ? "sfx_enhance_fail" : "sfx_enhance_fail");
    });

    // 하락 시 별 소멸 연출
    if (isDowngrade)
    {
        seq.AppendCallback(() => _statusText.text = "하락!");
        seq.Append(_statusText.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5));
    }

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
}
```

#### 파괴 연출 (1.2초)

```csharp
public void PlayEnhanceDestroy(RectTransform itemSlot)
{
    var seq = DOTween.Sequence();

    AudioManager.Instance.PlaySfx("sfx_enhance_destroy");

    // 아이템 슬롯 진동 → 균열 → 산산조각
    seq.Append(itemSlot.DOShakePosition(0.4f, 15f, 30, 90, false, true, ShakeRandomnessMode.Harmonic));
    seq.Join(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        new Color(1f, 0f, 0f, 0.7f), 0.2f));
    seq.AppendCallback(() => _crackOverlay.gameObject.SetActive(true));
    seq.Append(_crackOverlay.DOFade(1f, 0.15f)); // Image.DOFade는 가능
    seq.AppendInterval(0.2f);
    seq.Append(itemSlot.DOScale(0f, 0.2f).SetEase(Ease.InBack));
    seq.Join(DOTween.To(() => _flashOverlay.color, c => _flashOverlay.color = c,
        Color.clear, 0.3f));

    seq.AppendCallback(() => {
        _statusText.text = "파괴!";
        _statusText.color = Color.red;
    });
    seq.Append(_statusText.transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 5));

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
}
```

### 3-3. 레벨업 연출 (1.0초)

```csharp
public void PlayLevelUpEffect(Transform playerTransform)
{
    AudioManager.Instance.PlaySfx("sfx_levelup");

    // 전투 화면 VFX (SkillVfx 활용)
    SkillVfx.SpawnRing(playerTransform.position, 2f, new Color(1f, 0.85f, 0.2f), 0.6f);
    SkillVfx.SpawnCircle(playerTransform.position, 1.5f, new Color(1f, 0.95f, 0.5f, 0.3f), 0.5f);

    // HUD 텍스트 연출 (기존 ShowSkillLearnedNotification 패턴 활용)
    var seq = DOTween.Sequence();

    _levelUpBanner.gameObject.SetActive(true);
    _levelUpBanner.transform.localScale = Vector3.one * 0.3f;

    seq.Append(DOTween.To(() => _levelUpBanner.transform.localScale,
        s => _levelUpBanner.transform.localScale = s, Vector3.one * 1.2f, 0.15f).SetEase(Ease.OutBack));
    seq.Append(DOTween.To(() => _levelUpBanner.transform.localScale,
        s => _levelUpBanner.transform.localScale = s, Vector3.one, 0.1f));
    seq.AppendInterval(0.5f);
    seq.Append(DOTween.To(() => _levelUpCg.alpha, a => _levelUpCg.alpha = a, 0f, 0.3f));
    seq.AppendCallback(() => _levelUpBanner.gameObject.SetActive(false));

    seq.SetLink(gameObject);
}
```

### 3-4. 전직 연출 (2.5초)

```csharp
public async UniTask PlayJobAdvanceEffect(Transform playerTransform, string newJobName, CancellationToken ct)
{
    AudioManager.Instance.PlaySfx("sfx_job_advance");

    var seq = DOTween.Sequence();

    // [0.0s] 화면 암전
    seq.Append(DOTween.To(() => _overlay.color, c => _overlay.color = c,
        new Color(0, 0, 0, 0.7f), 0.3f));

    // [0.3s] 캐릭터 중앙 빛기둥
    seq.AppendCallback(() => {
        _lightPillar.gameObject.SetActive(true);
        _lightPillar.transform.localScale = new Vector3(0.5f, 0f, 1f);
    });
    seq.Append(_lightPillar.transform.DOScaleY(5f, 0.5f).SetEase(Ease.OutQuad));
    seq.Join(_lightPillar.transform.DOScaleX(1.5f, 0.5f).SetEase(Ease.OutQuad));

    // [0.8s] 빛 폭발
    seq.AppendCallback(() => {
        SkillVfx.SpawnExplosion(playerTransform.position, 4f,
            new Color(1f, 0.9f, 0.4f), new Color(1f, 0.7f, 0.2f), 0.8f);
    });

    // [1.0s] 전직 텍스트
    seq.AppendInterval(0.2f);
    _jobAdvanceText.text = newJobName;
    seq.Append(DOTween.To(() => _jobAdvanceText.alpha, a => _jobAdvanceText.alpha = a, 1f, 0.3f));
    seq.Join(DOTween.To(() => _jobAdvanceText.transform.localScale,
        s => _jobAdvanceText.transform.localScale = s, Vector3.one, 0.3f).SetEase(Ease.OutBack));

    // [1.5s] 유지
    seq.AppendInterval(0.5f);

    // [2.0s] 페이드아웃
    seq.Append(DOTween.To(() => _overlay.color, c => _overlay.color = c, Color.clear, 0.3f));
    seq.Join(DOTween.To(() => _jobAdvanceText.alpha, a => _jobAdvanceText.alpha = a, 0f, 0.3f));
    seq.AppendCallback(() => _lightPillar.gameObject.SetActive(false));

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
    await seq.ToUniTask(cancellationToken: ct);
}
```

### 3-5. 보스 등장 연출 (3.0초)

```csharp
public async UniTask PlayBossEntrance(string bossName, CancellationToken ct)
{
    var seq = DOTween.Sequence();

    // [0.0s] 경고 텍스트
    _warningText.text = "WARNING";
    _warningText.alpha = 0f;
    seq.Append(DOTween.To(() => _warningText.alpha, a => _warningText.alpha = a, 1f, 0.2f));
    seq.AppendInterval(0.3f);
    seq.Append(DOTween.To(() => _warningText.alpha, a => _warningText.alpha = a, 0f, 0.2f));
    seq.AppendInterval(0.1f);
    seq.Append(DOTween.To(() => _warningText.alpha, a => _warningText.alpha = a, 1f, 0.2f));
    seq.AppendInterval(0.3f);
    seq.Append(DOTween.To(() => _warningText.alpha, a => _warningText.alpha = a, 0f, 0.2f));

    // [1.3s] 화면 흔들림 + 보스 포효
    seq.AppendCallback(() => {
        AudioManager.Instance.PlaySfx("sfx_boss_roar");
        Camera.main.transform.DOShakePosition(0.5f, 0.15f, 15, 90, false, true, ShakeRandomnessMode.Harmonic)
            .SetUpdate(true);
    });

    // [1.8s] 보스 이름 표시 (어둡게 깔린 배너)
    seq.AppendInterval(0.5f);
    seq.AppendCallback(() => {
        _bossNameBanner.gameObject.SetActive(true);
        _bossNameText.text = bossName;
    });
    seq.Append(DOTween.To(() => _bossNameCg.alpha, a => _bossNameCg.alpha = a, 1f, 0.3f));
    seq.Join(_bossNameBanner.DOAnchorPosX(0, 0.3f).From(new Vector2(-500, 0)).SetEase(Ease.OutQuad));

    // [2.5s] 배너 유지 후 페이드
    seq.AppendInterval(0.3f);
    seq.Append(DOTween.To(() => _bossNameCg.alpha, a => _bossNameCg.alpha = a, 0f, 0.3f));
    seq.AppendCallback(() => _bossNameBanner.gameObject.SetActive(false));

    seq.SetLink(gameObject);
    seq.SetUpdate(true);
    await seq.ToUniTask(cancellationToken: ct);
}
```

### 3-6. 등급별 테두리/파티클 효과

| 등급 | 테두리 색상 | 테두리 효과 | 배경 글로우 | 파티클 |
|------|------------|------------|------------|--------|
| Normal | #A0A0A0 (회색) | 없음 | 없음 | 없음 |
| Rare | #4488FF (파랑) | 정적 테두리 | 없음 | 없음 |
| Epic | #AA44FF (보라) | 미세한 펄스 (DOScale 0.98~1.02, 1초 루프) | 약한 보라 글로우 | 없음 |
| Unique | #FFD700 (금색) | 빛나는 펄스 (DOScale 0.96~1.04, 0.8초) | 금색 글로우 | 작은 빛 입자 상승 |
| Legendary | #FF6B00 (주황) | 강한 펄스 + 번쩍임 | 주황 글로우 + 광선 | 화염 입자 상승 |
| Mythic | #FF1744 (붉은색) | 강한 펄스 + 레인보우 시프트 | 붉은 오라 | 대형 빛 입자 + 링 이펙트 |

#### 등급별 테두리 펄스 DOTween

```csharp
public void SetGradeBorder(ItemGrade grade, RectTransform border)
{
    border.DOKill();
    border.localScale = Vector3.one;

    switch (grade)
    {
        case ItemGrade.Epic:
            border.DOScale(1.02f, 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(border.gameObject);
            break;
        case ItemGrade.Unique:
            border.DOScale(1.04f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(border.gameObject);
            break;
        case ItemGrade.Legendary:
            border.DOScale(1.05f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(border.gameObject);
            break;
        case ItemGrade.Mythic:
            border.DOScale(1.06f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(border.gameObject);
            break;
    }
}
```

#### 등급 색상 유틸

```csharp
public static Color GetGradeColor(ItemGrade grade)
{
    return grade switch
    {
        ItemGrade.Normal    => new Color(0.63f, 0.63f, 0.63f),  // #A0A0A0
        ItemGrade.Rare      => new Color(0.27f, 0.53f, 1f),     // #4488FF
        ItemGrade.Epic      => new Color(0.67f, 0.27f, 1f),     // #AA44FF
        ItemGrade.Unique    => new Color(1f, 0.84f, 0f),        // #FFD700
        ItemGrade.Legendary => new Color(1f, 0.42f, 0f),        // #FF6B00
        ItemGrade.Mythic    => new Color(1f, 0.09f, 0.27f),     // #FF1744
        _ => Color.white
    };
}

public static string GetGradeRevealSfx(ItemGrade grade)
{
    return grade switch
    {
        ItemGrade.Normal    => "sfx_gacha_reveal_normal",
        ItemGrade.Rare      => "sfx_gacha_reveal_rare",
        ItemGrade.Epic      => "sfx_gacha_reveal_epic",
        ItemGrade.Unique    => "sfx_gacha_reveal_unique",
        ItemGrade.Legendary => "sfx_gacha_reveal_legendary",
        ItemGrade.Mythic    => "sfx_gacha_reveal_mythic",
        _ => "sfx_gacha_reveal_normal"
    };
}
```

---

## 4. 화면 효과

### 4-1. 치명타 히트스톱

```csharp
/// <summary>치명타 발생 시 0.05초 히트스톱 (Time.timeScale 조작)</summary>
public async UniTaskVoid PlayCritHitStop()
{
    Time.timeScale = 0.05f;
    await UniTask.Delay(50, ignoreTimeScale: true, cancellationToken: destroyCancellationToken);
    Time.timeScale = 1f;
}
```

- **발동 조건**: 치명타 데미지 발생 시 (DamageCalculator에서 isCritical=true)
- **지속 시간**: 0.05초 (3프레임 @60fps)
- **주의**: 연속 크리티컬 시 히트스톱 쿨다운 0.3초 적용 (남용 방지)
- **SFX**: sfx_crit_hit 동시 재생

```csharp
private float _lastHitStopTime;
private const float HIT_STOP_COOLDOWN = 0.3f;

public void OnCriticalHit(Vector3 hitPosition)
{
    if (Time.unscaledTime - _lastHitStopTime < HIT_STOP_COOLDOWN) return;
    _lastHitStopTime = Time.unscaledTime;

    AudioManager.Instance.PlaySfx("sfx_crit_hit");
    PlayCritHitStop().Forget();

    // 히트 이펙트 강화
    SkillVfx.SpawnHit(hitPosition, new Color(1f, 0.55f, 0.1f), 1.2f);
}
```

### 4-2. 보스 사망 슬로우모션

```csharp
/// <summary>보스 사망 시 1초 슬로우모션 연출</summary>
public async UniTask PlayBossDeathSlowMotion(Vector3 bossPosition, CancellationToken ct)
{
    AudioManager.Instance.PlaySfx("sfx_boss_die");

    // 슬로우모션 시작
    Time.timeScale = 0.2f;

    // 카메라 줌인 (보스 위치로)
    var cam = Camera.main;
    float originalSize = cam.orthographicSize;
    DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
        originalSize * 0.7f, 0.5f).SetUpdate(true);

    // 화면 플래시
    var flashSeq = DOTween.Sequence();
    flashSeq.Append(DOTween.To(() => _whiteFlash.color, c => _whiteFlash.color = c,
        new Color(1, 1, 1, 0.8f), 0.1f).SetUpdate(true));
    flashSeq.Append(DOTween.To(() => _whiteFlash.color, c => _whiteFlash.color = c,
        Color.clear, 0.3f).SetUpdate(true));
    flashSeq.SetUpdate(true);

    await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: ct);

    // 복귀
    Time.timeScale = 1f;
    DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
        originalSize, 0.5f);

    // 보스 사망 VFX
    SkillVfx.SpawnExplosion(bossPosition, 5f,
        new Color(1f, 0.8f, 0.2f), new Color(1f, 0.5f, 0.1f), 1.0f);
}
```

### 4-3. 대량 처치 화면 흔들림

```csharp
/// <summary>
/// 짧은 시간 내 다수 처치 시 화면 흔들림.
/// MonsterDiedEvent 수신, 1초 내 5마리 이상 처치 시 발동.
/// </summary>
private int _recentKillCount;
private float _killWindowTimer;
private const float KILL_WINDOW = 1.0f;
private const int SHAKE_THRESHOLD = 5;
private const float SHAKE_COOLDOWN = 0.5f;
private float _lastShakeTime;

private void OnMonsterDied(MonsterDiedEvent evt)
{
    _recentKillCount++;

    if (_recentKillCount >= SHAKE_THRESHOLD &&
        Time.unscaledTime - _lastShakeTime > SHAKE_COOLDOWN)
    {
        _lastShakeTime = Time.unscaledTime;
        float intensity = Mathf.Clamp(_recentKillCount * 0.02f, 0.05f, 0.2f);
        Camera.main.transform.DOShakePosition(0.2f, intensity, 12, 90, false, true,
            ShakeRandomnessMode.Harmonic);
    }
}

private void Update()
{
    _killWindowTimer += Time.unscaledDeltaTime;
    if (_killWindowTimer >= KILL_WINDOW)
    {
        _recentKillCount = 0;
        _killWindowTimer = 0f;
    }
}
```

### 4-4. 가챠 등급별 배경색 변화

| 등급 | 배경 연출 | 전환 시간 |
|------|----------|----------|
| Normal | 변화 없음 (약간 밝아짐) | 0.1초 |
| Rare | 파란색 그라디언트 배경 | 0.15초 |
| Epic | 보라색 그라디언트 + 파티클 | 0.2초 |
| Unique | 금색 그라디언트 + 광선 + 파티클 | 0.25초 |
| Legendary | 주황색 배경 + 화염 + 카메라 줌 | 0.3초 |
| Mythic | 붉은 배경 + 레인보우 시프트 + 풀스크린 플래시 + 카메라 줌 | 0.4초 |

```csharp
public void SetBackgroundGradeColor(Color gradeColor)
{
    // 기본 배경
    DOTween.To(() => _gachaBg.color, c => _gachaBg.color = c,
        gradeColor * 0.3f, 0.2f).SetUpdate(true);

    // Unique 이상: 광선 추가
    if (gradeColor == GetGradeColor(ItemGrade.Unique) ||
        gradeColor == GetGradeColor(ItemGrade.Legendary) ||
        gradeColor == GetGradeColor(ItemGrade.Mythic))
    {
        _lightRays.gameObject.SetActive(true);
        _lightRays.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0f);
        DOTween.To(() => _lightRays.color, c => _lightRays.color = c,
            new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.4f), 0.3f).SetUpdate(true);
        _lightRays.transform.DORotate(new Vector3(0, 0, 360), 8f, RotateMode.FastBeyond360)
            .SetLoops(-1).SetEase(Ease.Linear).SetUpdate(true).SetLink(_lightRays.gameObject);
    }

    // Legendary 이상: 카메라 줌 (UI 스케일)
    if (gradeColor == GetGradeColor(ItemGrade.Legendary) ||
        gradeColor == GetGradeColor(ItemGrade.Mythic))
    {
        _gachaContainer.DOScale(1.05f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // Mythic: 풀스크린 백색 플래시
    if (gradeColor == GetGradeColor(ItemGrade.Mythic))
    {
        var flashSeq = DOTween.Sequence();
        flashSeq.Append(DOTween.To(() => _whiteFlash.color, c => _whiteFlash.color = c,
            new Color(1, 1, 1, 0.9f), 0.05f));
        flashSeq.Append(DOTween.To(() => _whiteFlash.color, c => _whiteFlash.color = c,
            Color.clear, 0.35f));
        flashSeq.SetUpdate(true).SetLink(gameObject);
    }
}
```

---

## 5. AudioManager 아키텍처

### 5-1. 클래스 구조

```
Assets/Scripts/Core/AudioManager.cs
```

```csharp
// AudioManager: 싱글톤, Core 어셈블리
// BGM 1채널 + SFX 풀 8채널 + UI 2채널
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("오디오 소스")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource[] _sfxSources;  // 8개
    [SerializeField] private AudioSource[] _uiSources;   // 2개

    [Header("볼륨")]
    [SerializeField] private float _bgmVolume = 0.5f;
    [SerializeField] private float _sfxVolume = 0.8f;
    [SerializeField] private float _uiVolume = 0.6f;

    // 오디오 클립 캐시 (Resources 또는 Addressables)
    private Dictionary<string, AudioClip> _clipCache = new();

    public void PlaySfx(string clipName, float pitch = 1f);
    public void PlayUiSfx(string clipName);
    public async UniTaskVoid CrossfadeBgm(AudioClip newClip, float duration = 1.5f);
    public void SetBgmVolume(float volume);
    public void SetSfxVolume(float volume);
    public void SetUiVolume(float volume);
    public void StopBgm(float fadeOut = 0.5f);
}
```

### 5-2. 오디오 설정 연동

```csharp
// SettingsManager에서 AudioManager 볼륨 연동
public void SetMasterVolume(float value)  => AudioListener.volume = value;
public void SetBgmVolume(float value)     => AudioManager.Instance.SetBgmVolume(value);
public void SetSfxVolume(float value)     => AudioManager.Instance.SetSfxVolume(value);
```

---

## 6. 구현 우선순위

| 순서 | 항목 | 이유 |
|------|------|------|
| 1 | AudioManager 싱글톤 + SFX 재생 | 모든 사운드의 기반 |
| 2 | UI 터치/버튼 SFX | 즉시 체감 가능, 가장 빈번 |
| 3 | 공격/피격 SFX (직업별) | 전투 타격감의 핵심 |
| 4 | 치명타 히트스톱 + 화면 흔들림 | 전투 쾌감 극대화 |
| 5 | 레벨업/전직 연출 + SFX | 성장 피드백 |
| 6 | BGM 시스템 (크로스페이드, 보스 전환) | 분위기 조성 |
| 7 | 강화 성공/실패/파괴 연출 | 강화 긴장감 |
| 8 | 보스 등장/사망 연출 | 보스전 임팩트 |
| 9 | 가챠 연출 (풀 시퀀스) | 가장 복잡, 마지막 |
| 10 | 등급별 테두리/글로우 | UI 폴리싱 |

---

## 변경 이력

| 날짜 | 변경 내용 |
|------|-----------|
| 2026-03-15 | 초안 작성. BGM/SFX/비주얼/화면효과 전체 설계 |
