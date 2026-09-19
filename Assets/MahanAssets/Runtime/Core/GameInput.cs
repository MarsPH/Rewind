using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TimeEcho
{
    public struct InputFrame
    {
        public Vector2 Move;
        public Vector2 PointerScreen;
        public bool JumpPressed;
        public bool PrimaryPressed;
        public bool PrimaryHeld;
        public bool PrimaryReleased;
        public bool SecondaryPressed;
        public bool SecondaryHeld;
        public bool SecondaryReleased;
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class GameInput : MonoBehaviour
    {
        public InputFrame Current { get; private set; }

        private void Update()
        {
            Current = ReadFrame();
        }

        private void OnDisable()
        {
            Current = default;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Current = default;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Current = default;
            }
        }

        private static InputFrame ReadFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            float horizontal = 0f;
            float vertical = 0f;
            bool jumpPressed = false;

            if (keyboard != null)
            {
                horizontal = ReadAxis(
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
                vertical = ReadAxis(
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
                jumpPressed = keyboard.spaceKey.wasPressedThisFrame ||
                              keyboard.wKey.wasPressedThisFrame ||
                              keyboard.upArrowKey.wasPressedThisFrame;
            }

            return new InputFrame
            {
                Move = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f),
                PointerScreen = mouse != null ? mouse.position.ReadValue() : Vector2.zero,
                JumpPressed = jumpPressed,
                PrimaryPressed = mouse != null && mouse.leftButton.wasPressedThisFrame,
                PrimaryHeld = mouse != null && mouse.leftButton.isPressed,
                PrimaryReleased = mouse != null && mouse.leftButton.wasReleasedThisFrame,
                SecondaryPressed = mouse != null && mouse.rightButton.wasPressedThisFrame,
                SecondaryHeld = mouse != null && mouse.rightButton.isPressed,
                SecondaryReleased = mouse != null && mouse.rightButton.wasReleasedThisFrame
            };
#else
            float horizontal = ReadAxis(
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));
            float vertical = ReadAxis(
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow));

            return new InputFrame
            {
                Move = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f),
                PointerScreen = Input.mousePosition,
                JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow),
                PrimaryPressed = Input.GetMouseButtonDown(0),
                PrimaryHeld = Input.GetMouseButton(0),
                PrimaryReleased = Input.GetMouseButtonUp(0),
                SecondaryPressed = Input.GetMouseButtonDown(1),
                SecondaryHeld = Input.GetMouseButton(1),
                SecondaryReleased = Input.GetMouseButtonUp(1)
            };
#endif
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            if (negative == positive)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
