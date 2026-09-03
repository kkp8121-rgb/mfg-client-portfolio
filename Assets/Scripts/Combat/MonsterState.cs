namespace MkLike.Combat
{
    /// <summary>
    /// 몬스터 행동 상태를 정의하는 열거형.
    /// 상태 머신의 각 노드에 대응한다.
    /// </summary>
    public enum MonsterState
    {
        Idle,
        Chase,
        Attack,
        Hit,
        Die
    }
}
