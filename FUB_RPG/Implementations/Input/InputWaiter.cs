using System;
using Fub.Enums;

namespace Fub.Implementations.Input;

public static class InputWaiter
{
    public static void WaitForAny(InputMode mode)
    {
        if (mode == InputMode.Keyboard)
        {
            Console.ReadKey(true);
            return;
        }
        // For controller: wait for any non-movement action so accidental stick holds don't skip
        while (true)
        {
            var action = InputManager.ReadNextAction(InputMode.Controller);
            if (!IsMovement(action)) return;
        }
    }

    private static bool IsMovement(InputAction a) =>
        a == InputAction.MoveUp || a == InputAction.MoveDown || a == InputAction.MoveLeft || a == InputAction.MoveRight;
}

