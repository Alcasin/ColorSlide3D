using System.Linq;
using System;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ColorSlide.Editor
{
    /// <summary>Automatic Game View QA keys; this entire assembly is excluded from Android/player builds.</summary>
    [InitializeOnLoad]
    internal static class CampaignPlayModeShortcuts
    {
        static CampaignPlayModeShortcuts()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            if (EditorApplication.isPlaying) Subscribe(); // Also handles script reload during Play.
        }
        private static void Subscribe()
        {
            InputSystem.onAfterUpdate -= AfterInputUpdate;
            InputSystem.onAfterUpdate += AfterInputUpdate;
        }
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Also reset transient QA state when domain/scene reload are disabled.
                foreach (var fitter in UnityEngine.Object.FindObjectsByType<PortraitCameraFit>(FindObjectsSortMode.None)) fitter.ResetEditorZoom();
                Subscribe();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) InputSystem.onAfterUpdate -= AfterInputUpdate;
        }
        private static void AfterInputUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            var window = EditorWindow.focusedWindow;
            if (window == null || window.GetType().Name != "GameView") return;
            var keyboard = Keyboard.current;
            if (keyboard == null || keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed ||
                keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return;
            var index = keyboard.shiftKey.isPressed ? -1 : keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame ? 4 :
                keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame ? 5 :
                keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame ? 6 :
                keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame ? 7 : -1;
            var direction = ZoomDirection(keyboard);
            if (index < 0 && direction == 0) return;
            var owners = UnityEngine.Object.FindObjectsByType<CampaignController>(FindObjectsSortMode.None).Where(owner =>
                owner.isActiveAndEnabled && owner.gameObject.scene.path == M45Feedback.ScenePath &&
                owner.Presentation != null && owner.Presentation.GameplayFeedback && owner.Campaign != null).ToArray();
            if (owners.Length != 1) return;
            if (index >= 0) owners[0].StartEditorLevel(index);
            if (direction == 0) return;
            var fitter = owners[0].Presentation.InteractionCamera.GetComponent<PortraitCameraFit>();
            if (fitter == null) return;
            fitter.SetEditorZoom((float)Math.Round(fitter.ZoomMultiplier + direction * 0.01f, 2));
            var screen = new Vector2(Screen.width, Screen.height); var camera = owners[0].Presentation.InteractionCamera;
            var frame = fitter.CalculateFrame(screen, Screen.safeArea, camera.aspect);
            var usable = SafeAreaPanel.Normalize(Screen.safeArea, screen).width;
            Debug.Log($"ColorSlide zoom {fitter.ZoomMultiplier:F2}x (effective {fitter.EffectiveZoom:F2}x), " +
                $"tray {fitter.TrayViewportUtilization(frame, camera.aspect):P1} of viewport / {fitter.TrayViewportUtilization(frame, camera.aspect) / usable:P1} of safe width" +
                (fitter.ZoomPending ? "; queued until the current drag/exit ends." : "."), fitter);
        }
        private static int ZoomDirection(Keyboard keyboard)
        {
            if (keyboard == null || keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return 0;
            var plus = keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame;
            var minus = keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;
            return plus == minus ? 0 : plus ? 1 : -1;
        }
    }
}
