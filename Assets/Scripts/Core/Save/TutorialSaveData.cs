using System.Collections.Generic;

namespace MkLike.Core
{
    [System.Serializable]
    public class TutorialSaveData
    {
        public TutorialStep completedStep = TutorialStep.None;
        public bool hasSelectedJob;
        public bool hasCompletedFirstGacha;
        public bool hasCompletedDungeonIntro;
        public List<string> claimedMilestones = new List<string>();
    }
}
