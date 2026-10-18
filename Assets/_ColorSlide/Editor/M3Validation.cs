using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
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
    public static class M3Validation
    {
        [MenuItem("ColorSlide/Setup/Validate M3 Campaign")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject(); errors.AddRange(InspectSavedScene());
            if (errors.Count == 0) Debug.Log("ColorSlide M3 saved assets are valid. Run campaign gameplay and EditMode QA.");
            else Debug.LogError("ColorSlide M3 validation:\n- " + string.Join("\n- ", errors));
        }
        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new List<string> { "Leave Play Mode before inspecting M3." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M3Campaign.ScenePath) == null) return new List<string> { "Run Apply M3 Campaign first." };
            var scene = default(Scene);
            try { scene = EditorSceneManager.OpenPreviewScene(M3Campaign.ScenePath); return InspectScene(scene); }
            catch (Exception exception) { return new List<string> { "Cannot inspect M3: " + exception.Message }; }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static List<string> InspectScene(Scene scene)
        {
            var errors = M0Validation.InspectScene(scene);
            var roots = scene.GetRootGameObjects();
            var owners = roots.SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).ToArray();
            var huds = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).ToArray();
            var views = roots.SelectMany(root => root.GetComponentsInChildren<LevelPresentation>(true)).ToArray();
            var events = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if (owners.Length != 1 || huds.Length != 1 || views.Length != 1 || events.Length != 1)
            { errors.Add("Campaign needs one campaign owner, presentation, HUD and EventSystem."); return errors; }
            var owner = owners[0]; var view = views[0]; var hud = huds[0];
            if (!owner.isActiveAndEnabled || !view.isActiveAndEnabled || owner.Presentation != view || hud.Campaign != owner ||
                !hud.HasReferences || !hud.HasCampaignReferences || !hud.isActiveAndEnabled ||
                roots.Any(root => root.GetComponentsInChildren<InteractionBoard>(true).Length != 0 || root.GetComponentsInChildren<GameplayController>(true).Length != 0))
                errors.Add("Campaign/HUD references must be active and consistent; level objects are constructed only at runtime.");
            var canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).ToArray();
            var scaler = hud.GetComponent<CanvasScaler>();
            if (canvases.Length != 1 || hud.GetComponent<Canvas>() == null || hud.GetComponent<Canvas>().renderMode != RenderMode.ScreenSpaceOverlay ||
                hud.GetComponent<GraphicRaycaster>() == null || scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.referenceResolution != new Vector2(1080f, 1920f))
                errors.Add("Campaign must retain the single portrait M2 overlay canvas and UI raycaster.");
            try
            {
                var campaign = owner.CreateCampaign();
                if (campaign.CurrentIndex != 0 || campaign.CurrentSession.State != GameplayState.Playing)
                    errors.Add("Campaign must start at Level 1.");
                foreach (var level in owner.Levels)
                {
                    if (!AssetDatabase.Contains(level) || EditorUtility.IsDirty(level)) errors.Add("Save persistent level asset: " + level.name);
                    if (!M0Validation.FitsPortraitView(view.InteractionCamera, LevelBounds(level.Data, view.transform.position)))
                        errors.Add("Level '" + level.Data.Id + "' does not fit the accepted camera, including full exit paths.");
                }
                if (view.Materials.Any(material => !M0Validation.IsUrpMaterial(material)) ||
                    view.PointerActions != AssetDatabase.LoadAssetAtPath<InputActionAsset>(M0Foundation.InputPath) ||
                    view.InteractionCamera.gameObject.scene != scene)
                    errors.Add("Campaign must use its scene camera, accepted pointer actions and URP palette.");
            }
            catch (Exception exception) { errors.Add("Invalid campaign data/resources: " + exception.Message); }
            var module = events[0].GetComponent<InputSystemUIInputModule>();
            if (!events[0].isActiveAndEnabled || module == null || !module.isActiveAndEnabled || events[0].GetComponents<BaseInputModule>().Length != 1 ||
                module.actionsAsset == null || AssetDatabase.GetAssetPath(module.actionsAsset) != M2GameplayDemo.UiActionsPath)
                errors.Add("Campaign requires one active Input System UI module using the accepted UI asset.");
            else
            {
                var names = new[] { "Point", "Click", "Navigate", "Submit", "Cancel", "ScrollWheel", "RightClick", "MiddleClick" };
                var references = new[] { module.point, module.leftClick, module.move, module.submit, module.cancel, module.scrollWheel, module.rightClick, module.middleClick };
                for (var i = 0; i < names.Length; i++)
                    if (references[i] == null || !AssetDatabase.Contains(references[i]) || !AssetDatabase.IsSubAsset(references[i]) ||
                        references[i].asset != module.actionsAsset || references[i].action != module.actionsAsset.FindAction("UI/" + names[i]))
                        errors.Add("UI/" + names[i] + " must retain its imported persistent action reference.");
            }
            return errors;
        }

        // Conservative envelope in XZ includes full departure, not just initial occupied cells.
        public static Bounds LevelBounds(LevelData level, Vector3 center)
        {
            var model = level.CreateSession().Board;
            var minX = -0.6f; var maxX = model.Width - 0.4f;
            var minZ = -0.6f; var maxZ = model.Height - 0.4f;
            foreach (var gate in model.Gates)
            foreach (var block in model.Blocks.Where(block => block.ColorId == gate.ColorId))
            {
                var x = block.Cells.Max(cell => cell.x); var y = block.Cells.Max(cell => cell.y);
                switch (gate.Edge)
                {
                    case BoardEdge.West: minX = Math.Min(minX, -x - 1.5f); break;
                    case BoardEdge.East: maxX = Math.Max(maxX, model.Width + x + 0.5f); break;
                    case BoardEdge.South: minZ = Math.Min(minZ, -y - 1.5f); break;
                    case BoardEdge.North: maxZ = Math.Max(maxZ, model.Height + y + 0.5f); break;
                }
            }
            var origin = center + new Vector3(-(model.Width - 1) * level.CellSize / 2f, 0f, -(model.Height - 1) * level.CellSize / 2f);
            var min = origin + new Vector3(minX * level.CellSize, 0f, minZ * level.CellSize);
            var max = origin + new Vector3(maxX * level.CellSize, 0.8f * level.CellSize, maxZ * level.CellSize);
            var result = new Bounds(); result.SetMinMax(min, max); return result;
        }
    }
}
