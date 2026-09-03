using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// asmdef 경계를 넘어 조이스틱 입력을 전달하는 정적 브릿지.
    /// JoystickInputProvider(Assembly-CSharp)가 값을 쓰고,
    /// PlayerCharacter(MkLike.Combat)가 값을 읽는다.
    /// </summary>
    public static class JoystickBridge
    {
        public static Vector2 Direction;
        public static bool HasInput => Direction.sqrMagnitude > 0.01f;
    }
}
