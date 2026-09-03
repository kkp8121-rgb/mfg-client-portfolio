using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    [CreateAssetMenu(fileName = "InscriptionData", menuName = "MkLike/InscriptionDataSO")]
    public class InscriptionDataSO : ScriptableObject
    {
        [SerializeField] private string _inscriptionId;
        [SerializeField] private string _displayName;
        [SerializeField] [TextArea] private string _description;
        [SerializeField] private InscriptionGrade _grade;
        [SerializeField] private InscriptionEffect[] _effects;

        public string InscriptionId => _inscriptionId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public InscriptionGrade Grade => _grade;
        public InscriptionEffect[] Effects => _effects;
    }

    public enum InscriptionGrade
    {
        Normal,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    [System.Serializable]
    public class InscriptionEffect
    {
        [SerializeField] private StatType _statType;
        [SerializeField] private float _value;
        [SerializeField] private bool _isPercentage;

        public StatType StatType => _statType;
        public float Value => _value;
        public bool IsPercentage => _isPercentage;
    }
}
