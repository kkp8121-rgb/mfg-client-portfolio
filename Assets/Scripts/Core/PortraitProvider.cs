using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Core
{
    /// <summary>
    /// 직업별 초상화 스프라이트를 Resources에서 로드하여 캐싱 제공.
    /// </summary>
    public static class PortraitProvider
    {
        private static readonly Dictionary<JobType, Sprite> _cache = new();

        /// <summary>
        /// 직업에 해당하는 초상화 스프라이트를 반환한다.
        /// Resources/Icons/Portraits/portrait_{job}.png 에서 로드.
        /// </summary>
        public static Sprite GetPortrait(JobType job)
        {
            if (_cache.TryGetValue(job, out var cached))
                return cached;

            string key = job switch
            {
                JobType.Warrior => "warrior",
                JobType.Archer => "archer",
                JobType.Mage => "mage",
                _ => "warrior"
            };

            var sprite = Resources.Load<Sprite>($"Icons/Portraits/portrait_{key}");
            if (sprite == null)
            {
                Debug.LogWarning($"[PortraitProvider] 초상화 없음: portrait_{key}");
                return null;
            }

            _cache[job] = sprite;
            return sprite;
        }

        /// <summary>캐시 초기화 (씬 전환 등)</summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
