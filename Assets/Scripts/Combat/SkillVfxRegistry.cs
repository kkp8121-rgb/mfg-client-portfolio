using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Combat
{
    /// <summary>
    /// 직업별/스킬별 기본 VFX SO 매핑 레지스트리.
    /// SkillVfx에서 스킬에 vfxSheet가 없을 때 직업별 기본 VFX를 조회한다.
    /// </summary>
    public class SkillVfxRegistry : MonoBehaviour
    {
        public static SkillVfxRegistry Instance { get; private set; }

        [Header("직업별 기본 공격 VFX")]
        [SerializeField] private SpriteSheetVfxSO _warriorAttackVfx;
        [SerializeField] private SpriteSheetVfxSO _archerAttackVfx;
        [SerializeField] private SpriteSheetVfxSO _mageAttackVfx;

        [Header("직업별 기본 히트 VFX")]
        [SerializeField] private SpriteSheetVfxSO _warriorHitVfx;
        [SerializeField] private SpriteSheetVfxSO _archerHitVfx;
        [SerializeField] private SpriteSheetVfxSO _mageHitVfx;

        [Header("스킬별 VFX 매핑 (선택)")]
        [SerializeField] private VfxMapping[] _skillVfxMappings;

        private Dictionary<string, SpriteSheetVfxSO> _skillVfxCache;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildCache();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void BuildCache()
        {
            _skillVfxCache = new Dictionary<string, SpriteSheetVfxSO>();
            if (_skillVfxMappings == null) return;

            for (int i = 0; i < _skillVfxMappings.Length; i++)
            {
                var mapping = _skillVfxMappings[i];
                if (!string.IsNullOrEmpty(mapping.skillId) && mapping.vfxSO != null)
                    _skillVfxCache[mapping.skillId] = mapping.vfxSO;
            }
        }

        /// <summary>스킬 ID로 전용 VFX SO를 조회한다. 없으면 null.</summary>
        public SpriteSheetVfxSO GetVfxForSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            if (_skillVfxCache == null) BuildCache();
            _skillVfxCache.TryGetValue(skillId, out var vfx);
            return vfx;
        }

        /// <summary>직업별 기본 공격 VFX를 반환한다.</summary>
        public SpriteSheetVfxSO GetDefaultAttackVfx(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Warrior: return _warriorAttackVfx;
                case JobType.Archer: return _archerAttackVfx;
                case JobType.Mage: return _mageAttackVfx;
                default: return _warriorAttackVfx;
            }
        }

        /// <summary>직업별 기본 히트 VFX를 반환한다.</summary>
        public SpriteSheetVfxSO GetDefaultHitVfx(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Warrior: return _warriorHitVfx;
                case JobType.Archer: return _archerHitVfx;
                case JobType.Mage: return _mageHitVfx;
                default: return _warriorHitVfx;
            }
        }
    }

    [Serializable]
    public struct VfxMapping
    {
        [Tooltip("스킬 ID (SkillDataSO.id)")]
        public string skillId;
        [Tooltip("해당 스킬에 사용할 VFX SO")]
        public SpriteSheetVfxSO vfxSO;
    }
}
