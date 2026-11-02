using AtomIO.Player;
using UnityEngine;

namespace AtomIO.Controls
{
    /// <summary>
    /// Wires the two on-screen sticks to the player: left moves, right aims and fires on release.
    /// In the editor (legacy input enabled), WASD / arrow keys move for quick testing.
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

        private void OnAim(Vector2 v) => orbit.Aim(v);

        private void OnFire(Vector2 v) => orbit.Release(v);

#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
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
