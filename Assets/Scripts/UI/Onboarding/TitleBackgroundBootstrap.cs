using MkLike.Core;
using MkLike.Utils;
using UnityEngine;

namespace MkLike.UI.Onboarding
{
    /// <summary>
    /// Title 씬에서 인게임 1챕터 1스테이지와 동일한 배경을 연출하기 위한 부트스트랩.
    /// 기존 하늘/바닥 배경 오브젝트를 비활성화하고, ZoneVisualManager 등 구독자에게
    /// StageChangedEvent(Chapter=1, StageIndex=0)를 발행해 배경을 동기화한다.
    /// </summary>
    public class TitleBackgroundBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject[] _hideOnStart;
        [SerializeField] private int _chapter = 1;
        [SerializeField] private int _stageIndex = 0;
        [SerializeField] private string _displayName = "1-1";

        private void Awake()
        {
            if (_hideOnStart != null)
            {
                for (int i = 0; i < _hideOnStart.Length; ++i)
                {
                    if (_hideOnStart[i] != null) _hideOnStart[i].SetActive(false);
                }
            }
        }

        private void Start()
        {
            EventBus.Publish(new StageChangedEvent
            {
                Chapter = _chapter,
                StageIndex = _stageIndex,
                DisplayName = _displayName,
            });
        }
    }
}
