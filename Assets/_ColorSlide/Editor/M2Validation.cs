using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Editor
{
    public static class M2Validation
    {
        [MenuItem("ColorSlide/Setup/Validate M2 Gameplay Demo")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject();
            errors.AddRange(InspectSavedScene());
            if (errors.Count == 0) Debug.Log("ColorSlide M2 saved configuration is valid. Run gameplay and EditMode QA.");
            else Debug.LogError("ColorSlide M2 validation:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return new List<string> { "Leave Play Mode before inspecting the saved M2 scene." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M2GameplayDemo.ScenePath) == null)
                return new List<string> { "Run Apply M2 Gameplay Demo first." };
            var scene = default(Scene);
            try { scene = EditorSceneManager.OpenPreviewScene(M2GameplayDemo.ScenePath); return InspectScene(scene); }
            catch (Exception exception) { return new List<string> { "Cannot inspect M2: " + exception.Message }; }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static List<string> InspectScene(Scene scene)
        {
            var errors = M1Validation.InspectScene(scene);
            var roots = scene.GetRootGameObjects();
            var owners = roots.SelectMany(root => root.GetComponentsInChildren<GameplayController>(true)).ToArray();
            var huds = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).ToArray();
            var events = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if (owners.Length != 1 || huds.Length != 1 || events.Length != 1)
            {
                errors.Add("M2 needs exactly one GameplayController, GameplayHud and EventSystem.");
                return errors;
            }
            var owner = owners[0];
            if (!owner.isActiveAndEnabled || owner.Board == null || owner.Drag == null || owner.Drag.Gameplay != owner ||
                owner.Drag.Board != owner.Board || !huds[0].HasReferences || huds[0].Gameplay != owner || !huds[0].isActiveAndEnabled)
                errors.Add("M2 gameplay/HUD/drag references must be complete, active and consistent.");
            var canvas = huds[0].GetComponent<Canvas>();
            var scaler = huds[0].GetComponent<CanvasScaler>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || scaler == null ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.referenceResolution != new Vector2(1080f, 1920f) ||
                huds[0].GetComponent<GraphicRaycaster>() == null)
                errors.Add("M2 HUD needs a raycastable overlay canvas scaled to 1080 x 1920.");
            var module = events[0].GetComponent<InputSystemUIInputModule>();
            if (!events[0].isActiveAndEnabled || module == null || !module.isActiveAndEnabled || module.actionsAsset == null ||
                events[0].GetComponents<BaseInputModule>().Length != 1 ||
                AssetDatabase.GetAssetPath(module.actionsAsset) != M2GameplayDemo.UiActionsPath)
                errors.Add("EventSystem needs the Input System UI module with persistent UI action references.");
            else
            {
                var references = new Dictionary<string, InputActionReference>
                {
                    { "Point", module.point }, { "Click", module.leftClick }, { "Navigate", module.move },
                    { "Submit", module.submit }, { "Cancel", module.cancel }, { "ScrollWheel", module.scrollWheel },
                    { "RightClick", module.rightClick }, { "MiddleClick", module.middleClick }
                };
                foreach (var pair in references)
                {
                    var reference = pair.Value;
                    if (reference == null || !AssetDatabase.Contains(reference) || !AssetDatabase.IsSubAsset(reference) ||
                        AssetDatabase.GetAssetPath(reference) != M2GameplayDemo.UiActionsPath || reference.asset != module.actionsAsset ||
                        reference.action == null || reference.action != module.actionsAsset.FindAction("UI/" + pair.Key))
                        errors.Add("UI/" + pair.Key + " must reference its persistent imported sub-asset, not a transient generated object.");
                }
                // Tracked-device input is unused by this mouse/touch demo. No generated defaults may survive here either.
                if (module.trackedDevicePosition != null || module.trackedDeviceOrientation != null)
                    errors.Add("M2 does not use tracked-device actions; clear their default references.");
            }

            try
            {
                var session = owner.CreateSession(); // Pure construction; no timer, input enable or scene mutation.
                var board = owner.Board;
                var model = session.Board;
                if (owner.GateViews.Count != roots.SelectMany(root => root.GetComponentsInChildren<ExitGateView>(true)).Count())
                    errors.Add("All scene gates must be owned by the gameplay controller.");
                foreach (var view in owner.GateViews)
                {
                    var gate = view.CreateDefinition();
                    var expected = board.transform.TransformPoint(M2GameplayDemo.BoundaryPosition(model.Width, model.Height,
                        board.CellSize, gate.Edge, gate.Start + (gate.Span - 1) / 2f));
                    if (!view.gameObject.activeInHierarchy || (view.transform.position - expected).sqrMagnitude > 0.0001f ||
                        view.GetComponentsInChildren<Collider>(true).Length != 0)
                        errors.Add("Gate '" + gate.Id + "' must align to its aperture and have no decorative selection colliders.");
                    var renderers = view.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length < 3 || renderers.Any(renderer => !M0Validation.IsUrpMaterial(renderer.sharedMaterial)))
                        errors.Add("Gate '" + gate.Id + "' needs visible URP floor/jamb geometry.");
                }
                var boundary = board.transform.Find("M2 Boundary");
                if (boundary == null) errors.Add("M2 boundary geometry is missing.");
                else foreach (BoardEdge edge in Enum.GetValues(typeof(BoardEdge)))
                {
                    var length = ExitGate.IsHorizontalEdge(edge) ? model.Width : model.Height;
                    for (var index = 0; index < length; index++)
                    {
                        var solid = boundary.Find("Solid " + edge + " " + index);
                        var opening = model.Gates.Any(gate => gate.Edge == edge && gate.Contains(index));
                        if (opening == (solid != null)) errors.Add("Solid boundary/opening mismatch at " + edge + " " + index);
                    }
                }
            }
            catch (Exception exception) { errors.Add("Invalid M2 configuration: " + exception.Message); }
            return errors;
        }
    }
}
