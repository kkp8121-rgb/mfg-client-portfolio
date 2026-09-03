using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    [CreateAssetMenu(fileName = "TutorialData", menuName = "MkLike/TutorialDataSO")]
    public class TutorialDataSO : ScriptableObject
    {
        public TutorialStepData[] steps;

        public TutorialStepData GetStep(TutorialStep step)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].step == step)
                    return steps[i];
            }
            return null;
        }
    }

    [System.Serializable]
    public class TutorialStepData
    {
        public TutorialStep step;
        [TextArea(2, 4)]
        public string dialogueText;
        public string highlightTarget;
        public bool isPauseCombat;
        public bool isForced;
        public float delayBefore;
        public TutorialReward[] rewards;
        public TutorialStep nextStep;
    }

    [System.Serializable]
    public class TutorialReward
    {
        public CurrencyType currencyType;
        public int amount;
        public string itemId;
    }
}
