using System;
using UnityEngine.InputSystem;

namespace ColorSlide.Input
{
    /// <summary>
    /// Typed access to the authored pointer action map. Does not enable actions, own the
    /// asset, subscribe to events, or process gameplay. The caller controls its lifetime.
    /// </summary>
    public sealed class PointerActions
    {
        public InputActionMap Map { get; }
        public InputAction Position { get; }
        public InputAction Press { get; }

        public PointerActions(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            Map = asset.FindActionMap("Pointer", throwIfNotFound: true);
            Position = Map.FindAction("Position", throwIfNotFound: true);
            Press = Map.FindAction("Press", throwIfNotFound: true);
        }
    }
}
