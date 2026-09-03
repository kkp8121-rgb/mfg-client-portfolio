using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 스테이지 정적 데이터를 정의하는 ScriptableObject.
    /// 챕터-스테이지 구조: 챕터당 9개 일반 + 1개 보스 스테이지.
    /// 탑다운 전환: platformLayout → arenaSize/spawnInterval/spawnBatchSize.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStage", menuName = "MkLike/Data/Stage")]
    public class StageDataSO : ScriptableObject
    {
        [Header("챕터/스테이지 식별")]
        [Tooltip("챕터 번호 (1~)")]
        public int chapterId = 1;

        [Tooltip("스테이지 인덱스 (1~9: 일반, 10: 보스)")]
        public int stageIndex = 1;

        [Tooltip("표시 이름 (예: 1-3, 2-BOSS)")]
        public string displayName;

        [Tooltip("테마 식별자 (예: forest, cave, volcano)")]
        public string themeId;

        [Header("몬스터 구성")]
        [Tooltip("스테이지 총 몬스터 수 (기본 100)")]
        public int monsterCount = 100;

        [Tooltip("동시 활성 몬스터 수 제한")]
        public int maxAliveMonsters = 40;

        [Tooltip("등장 몬스터 데이터 목록")]
        public MonsterDataSO[] monsterDataList;

        [Header("아레나")]
        [Tooltip("아레나 크기")]
        public Vector2 arenaSize = new Vector2(24f, 18f);

        [Tooltip("스폰 간격 (초)")]
        public float spawnInterval = 3f;

        [Tooltip("한 번에 스폰하는 수")]
        public int spawnBatchSize = 5;

        [Header("미니보스")]
        [Tooltip("미니보스 데이터 (일반 스테이지)")]
        public MonsterDataSO miniBossData;

        [Tooltip("미니보스 타임아웃 (초)")]
        public float miniBossTimeout = 30f;

        [Header("챕터 보스")]
        [Tooltip("챕터 보스 데이터 (BOSS 스테이지만)")]
        public MonsterDataSO chapterBossData;

        [Header("보상")]
        public long clearGold = 100;
        public long clearExp = 50;

        /// <summary>보스 스테이지 여부</summary>
        public bool IsBossStage => stageIndex >= 10;

        /// <summary>"1-3" 또는 "1-BOSS" 형태의 표시명을 자동 생성</summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return IsBossStage ? $"{chapterId}-BOSS" : $"{chapterId}-{stageIndex}";
        }
    }
}
