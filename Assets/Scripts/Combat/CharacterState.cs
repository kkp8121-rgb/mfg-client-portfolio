namespace MkLike.Combat
{
    /// <summary>
    /// 캐릭터 행동 상태를 정의하는 열거형.
    /// 탑다운 전환: Jump 제거.
    /// </summary>
    public enum CharacterState
    {
        Idle,
        Run,
        Attack,
        Skill,
        Hit,
        Stun,
        Die
    }
}
