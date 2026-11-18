using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Controls
{
    /// <summary>
    /// Wires the two on-screen sticks to the player: left moves, right aims and fires on release.
    /// In the editor, WASD / arrow keys also move for quick testing (new Input System or legacy).
    /// The sticks themselves work with the mouse in the editor.
    /// </summary>
    public class PlayerInputBridge : MonoBehaviour
    {
        [SerializeField] private TouchStick moveStick;
        [SerializeField] private TouchStick aimStick;
        [SerializeField] private PlayerAtom player;
        [SerializeField] private ElectronOrbit orbit;

        private void OnEnable()
        {
            if (moveStick != null)
            {
                moveStick.Moved += OnMove;
            }

            if (aimStick != null)
            {
                aimStick.Moved += OnAim;
                aimStick.Released += OnFire;
            }
        }

        private void OnDisable()
        {
            if (moveStick != null)
            {
                moveStick.Moved -= OnMove;
            }

            if (aimStick != null)
            {
                aimStick.Moved -= OnAim;
                aimStick.Released -= OnFire;
            }
        }

        private void OnMove(Vector2 v) => player.SetMoveInput(v);

        private void OnAim(Vector2 v)
        {
            orbit.Aim(v);
            player.SetAiming(v.sqrMagnitude > 0.01f && orbit.CanRelease); // slow down while lining up a throw
        }

        private void OnFire(Vector2 v)
        {
            player.SetAiming(false);
            if (orbit.Release(v))
            {
                player.ApplyThrowRecoil(v); // the atom is kicked backwards
            }
        }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || (moveStick != null && moveStick.Held))
            {
                return;
            }

            var keys = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keys.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keys.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keys.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keys.y += 1f;
            player.SetMoveInput(keys);
        }
#elif UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (moveStick != null && moveStick.Held)
            {
                return;
            }

            var keys = new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
            player.SetMoveInput(keys);
        }
#endif
    }
}
