using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    /// <summary>Read-only diagnostics shared by the menu and EditMode tests.</summary>
    public static class M0Validation
    {
        [MenuItem("ColorSlide/Setup/Validate M0 Foundation")]
        private static void ValidateFromMenu()
        {
            var errors = InspectProject();
            errors.AddRange(InspectGameplayScene());
            if (errors.Count == 0)
                Debug.Log("ColorSlide M0 validation passed for saved assets. Inspect Game View and run EditMode tests.");
            else
                Debug.LogError("ColorSlide M0 validation:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> InspectProject(bool requirePortrait = true)
        {
            var errors = new List<string>();
#if !ENABLE_INPUT_SYSTEM
            errors.Add("Enable Project Settings > Player > Other Settings > Active Input Handling > " +
                       "Input System Package (New), then accept the Unity Editor restart.");
#endif
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
                errors.Add("Graphics Settings must reference the existing URP pipeline asset.");
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                if (pipeline != null && !(pipeline is UniversalRenderPipelineAsset))
                    errors.Add("Quality level '" + QualitySettings.names[i] + "' overrides URP with another pipeline.");
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath);
            if (asset == null)
                errors.Add("Missing or unimported input asset: " + M0Foundation.InputPath);
            else
                errors.AddRange(InspectInput(asset));

            if (requirePortrait)
            {
                if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                    errors.Add("Default orientation must be Portrait. Run Apply M0 Foundation.");
                if (PlayerSettings.defaultScreenWidth != M0Foundation.ReferenceWidth ||
                    PlayerSettings.defaultScreenHeight != M0Foundation.ReferenceHeight)
                    errors.Add("Reference resolution must be 1080 x 1920. Run Apply M0 Foundation.");
            }
            return errors;
        }

        public static List<string> InspectInput(InputActionAsset asset)
        {
            var errors = new List<string>();
            try
            {
                var actions = new PointerActions(asset);
                if (actions.Position.type != InputActionType.Value || actions.Position.expectedControlType != "Vector2")
                    errors.Add("Pointer/Position must be a Vector2 Value action.");
                if (actions.Press.type != InputActionType.Button || actions.Press.expectedControlType != "Button")
                    errors.Add("Pointer/Press must be a Button action, preserving press and release phases.");
                if (!HasUnmodifiedBinding(actions.Position, "<Pointer>/position"))
                    errors.Add("Pointer/Position needs an unmodified <Pointer>/position binding for touch and mouse.");
                if (!HasUnmodifiedBinding(actions.Press, "<Pointer>/press"))
                    errors.Add("Pointer/Press needs an unmodified <Pointer>/press binding for touch and mouse.");
                if (!string.IsNullOrEmpty(actions.Press.interactions) ||
                    !string.IsNullOrEmpty(actions.Press.processors))
                    errors.Add("Pointer/Press must retain default button behavior without interactions or processors.");
            }
            catch (Exception exception)
            {
                errors.Add("Pointer action contract is invalid: " + exception.Message);
            }
            return errors;
        }

        private static bool HasUnmodifiedBinding(InputAction action, string path)
        {
            return action.bindings.Any(binding => binding.effectivePath == path &&
                !binding.isComposite && !binding.isPartOfComposite &&
                string.IsNullOrEmpty(binding.effectiveInteractions) &&
                string.IsNullOrEmpty(binding.effectiveProcessors));
        }

        public static bool IsUrpMaterial(Material material)
        {
            return material != null && material.shader != null &&
                   material.GetTag("RenderPipeline", false, "") == "UniversalPipeline";
        }

        public static List<string> InspectGameplayScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M0Foundation.ScenePath) == null)
                return new List<string> { "Missing Gameplay scene. Run ColorSlide > Setup > Apply M0 Foundation first." };

            var scene = default(Scene);
            try
            {
                // Preview scenes do not replace the user's active scene or save their edits.
                scene = EditorSceneManager.OpenPreviewScene(M0Foundation.ScenePath);
                return InspectScene(scene);
            }
            catch (Exception exception)
            {
                return new List<string> { "Cannot inspect saved Gameplay scene: " + exception.Message };
            }
            finally
            {
                if (scene.IsValid())
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static List<string> InspectScene(Scene scene)
        {
            var errors = new List<string>();
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(includeInactive: true))
                .Select(transform => transform.gameObject).ToArray();
            if (objects.Any(obj => obj.GetComponents<Component>().Any(component => component == null)))
                errors.Add("Gameplay contains a missing script reference.");

            var cameras = objects.SelectMany(obj => obj.GetComponents<Camera>()).ToArray();
            var camera = cameras.Length == 1 ? cameras[0] : null;
            if (camera == null)
                errors.Add("Gameplay must contain exactly one camera.");
            else
            {
                if (!camera.orthographic || camera.orthographicSize <= 0f)
                    errors.Add("Gameplay camera must use a positive orthographic projection.");
                if (!camera.CompareTag("MainCamera") || !camera.enabled || !camera.gameObject.activeInHierarchy)
                    errors.Add("Gameplay camera must be enabled, active, and tagged MainCamera.");
                var downward = Vector3.Dot(camera.transform.forward, Vector3.down);
                if (downward < 0.5f || downward > 0.999f)
                    errors.Add("Gameplay camera must look down onto XZ at a slight angle.");
            }

            if (!objects.SelectMany(obj => obj.GetComponents<Light>()).Any(light =>
                    light.type == LightType.Directional && light.enabled &&
                    light.gameObject.activeInHierarchy && light.intensity > 0f))
                errors.Add("Gameplay needs an active directional light with positive intensity.");

            var boards = objects.Where(obj => obj.name == M0Foundation.BoardName).ToArray();
            if (boards.Length != 1)
            {
                errors.Add("Gameplay must contain exactly one '" + M0Foundation.BoardName + "' object.");
                return errors;
            }

            var board = boards[0];
            var filter = board.GetComponent<MeshFilter>();
            var renderer = board.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null ||
                !renderer.enabled || !board.activeInHierarchy)
                errors.Add("Board placeholder needs an active 3D MeshFilter and MeshRenderer.");
            else
            {
                if (!IsUrpMaterial(renderer.sharedMaterial))
                    errors.Add("Board placeholder needs a saved URP-compatible material.");
                if (renderer.sharedMaterial != null && !AssetDatabase.Contains(renderer.sharedMaterial))
                    errors.Add("Board material must be an asset, rather than a transient material instance.");
                var size = renderer.bounds.size;
                if (Vector3.Dot(board.transform.up, Vector3.up) < 0.99f ||
                    size.y <= 0f || size.x <= size.y || size.z <= size.y)
                    errors.Add("Board must lie in XZ with visible thickness along Y.");
                if (camera != null && camera.orthographic && !FitsPortraitView(camera, renderer.bounds))
                    errors.Add("Board is outside the camera's 9:16 reference view or clipping planes. Adjust the camera in Inspector.");
            }
            return errors;
        }

        public static bool FitsPortraitView(Camera camera, Bounds bounds)
        {
            // Use the reference aspect without changing Camera.aspect or Game View settings.
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * M0Foundation.ReferenceWidth / M0Foundation.ReferenceHeight;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var view = camera.transform.InverseTransformPoint(corner);
                if (Mathf.Abs(view.x) >= halfWidth || Mathf.Abs(view.y) >= halfHeight ||
                    view.z <= camera.nearClipPlane || view.z >= camera.farClipPlane)
                    return false;
            }
            return true;
        }
    }
}
