using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    public static class M1Validation
    {
        [MenuItem("ColorSlide/Setup/Validate M1 Interaction Demo")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject();
            errors.AddRange(InspectSavedScene());
            if (errors.Count == 0)
                Debug.Log("ColorSlide M1: saved scene configuration is valid. Play Mode interaction and touch still need QA.");
            else
                Debug.LogError("ColorSlide M1 validation:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new List<string> { "Leave Play Mode before inspecting the saved M1 scene." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M1InteractionDemo.ScenePath) == null)
                return new List<string> { "Run Apply M1 Interaction Demo to create InteractionDemo.unity." };
            var scene = default(Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(M1InteractionDemo.ScenePath);
                return InspectScene(scene);
            }
            catch (Exception exception)
            {
                return new List<string> { "Cannot inspect M1 scene: " + exception.Message };
            }
            finally
            {
                if (scene.IsValid())
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static List<string> InspectScene(Scene scene)
        {
            var errors = M0Validation.InspectScene(scene);
            var roots = scene.GetRootGameObjects();
            var boards = roots.SelectMany(root => root.GetComponentsInChildren<InteractionBoard>(true)).ToArray();
            var controllers = roots.SelectMany(root => root.GetComponentsInChildren<PointerDragController>(true)).ToArray();
            if (boards.Length != 1 || controllers.Length != 1)
            {
                errors.Add("M1 needs exactly one InteractionBoard and one PointerDragController.");
                return errors;
            }
            var board = boards[0];
            var controller = controllers[0];
            if (!board.isActiveAndEnabled || !controller.isActiveAndEnabled || controller.Board != board ||
                controller.InteractionCamera == null || controller.InteractionCamera.gameObject.scene != scene ||
                controller.InputActions != AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath))
                errors.Add("M1 board/controller/camera/input references are missing, disabled or inconsistent.");
            try
            {
                var model = board.CreateModel();
                if (model.Blocks.Count() < 3)
                    errors.Add("The interaction demo requires at least three movable blocks.");
                foreach (var view in board.BlockViews)
                {
                    if ((view.transform.position - board.Coordinates.CellToWorld(view.InitialAnchor)).sqrMagnitude > 0.0001f)
                        errors.Add("Block '" + view.BlockId + "' is not at its authored anchor.");
                    var colliders = view.GetComponentsInChildren<BoxCollider>(true);
                    if (colliders.Length != view.Footprint.Count || view.CellRenderers == null ||
                        view.CellRenderers.Count != view.Footprint.Count)
                        errors.Add("Block '" + view.BlockId + "' needs one cell renderer and collider per footprint offset.");
                    for (var i = 0; i < view.Footprint.Count; i++)
                    {
                        var expected = board.Coordinates.CellToWorld(view.InitialAnchor + view.Footprint[i]);
                        var matching = colliders.Where(collider =>
                        {
                            var center = collider.transform.TransformPoint(collider.center);
                            return Mathf.Abs(center.x - expected.x) < 0.001f && Mathf.Abs(center.z - expected.z) < 0.001f;
                        }).ToArray();
                        if (matching.Length != 1 || !matching[0].enabled || !matching[0].gameObject.activeInHierarchy ||
                            matching[0].isTrigger || Quaternion.Angle(matching[0].transform.rotation, Quaternion.identity) > 0.01f ||
                            Vector3.Scale(matching[0].size, matching[0].transform.lossyScale).x > board.CellSize ||
                            Vector3.Scale(matching[0].size, matching[0].transform.lossyScale).z > board.CellSize)
                            errors.Add("Block '" + view.BlockId + "' has invalid compound cell colliders.");
                    }
                    if (view.CellRenderers == null || view.CellRenderers.Any(renderer => renderer == null ||
                        !renderer.enabled || !M0Validation.IsUrpMaterial(renderer.sharedMaterial)))
                        errors.Add("Block '" + view.BlockId + "' has missing or incompatible visuals.");
                }
                var camera = controller.InteractionCamera;
                if (camera != null)
                {
                    var renderers = board.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var renderer in renderers)
                        if (!M0Validation.FitsPortraitView(camera, renderer.bounds))
                        {
                            errors.Add("M1 geometry is outside the existing camera's 9:16 view. Review framing without resetting M0.");
                            break;
                        }
                }
            }
            catch (Exception exception)
            {
                errors.Add("Invalid M1 board configuration: " + exception.Message);
            }
            return errors;
        }
    }
}
