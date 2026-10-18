using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    public static class M45Validation
    {
        public static List<string> InspectSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new List<string> { "Leave Play Mode before M4.5 validation." };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(M45Animation.ScenePath) == null) return new List<string> { "Run Apply M4.5 Animation Polish first." };
            var scene = default(Scene);
            try { scene = EditorSceneManager.OpenPreviewScene(M45Animation.ScenePath); return InspectScene(scene); }
            catch (Exception exception) { return new List<string> { "Cannot inspect M4.5: " + exception.Message }; }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static List<string> InspectScene(Scene scene)
        {
            var errors = M4Validation.InspectScene(scene);
            var roots = scene.GetRootGameObjects();
            var owners = roots.SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).ToArray();
            if (owners.Length != 1 || owners[0].Presentation == null) return errors;
            var presentation = owners[0].Presentation;
            if (!presentation.SoftCubeBursts || presentation.Theme == null || AssetDatabase.GetAssetPath(presentation.Theme) != M4Polish.ThemePath)
                errors.Add("M4.5 needs its optional burst enabled and the persistent accepted M4 theme.");
            if (roots.Any(root => root.GetComponentsInChildren<SoftCubeBurst>(true).Length != 0))
                errors.Add("Burst instances are owned by runtime level construction; do not author extra effect components into the scene.");
            try
            {
                foreach (var level in owners[0].Levels)
                    InspectFraming(level.Data, presentation.transform.position, presentation.InteractionCamera, errors);
            }
            catch (Exception exception) { errors.Add("Invalid M4.5 resources/framing: " + exception.Message); }
            return errors;
        }
        // Test the actual piece trajectories at both extremes of each matching gate aperture.
        private static void InspectFraming(LevelData level, Vector3 center, Camera camera, List<string> errors)
        {
            var origin = center + new Vector3(-(level.Width - 1) * level.CellSize / 2f, level.CellSize * 0.04f, -(level.Height - 1) * level.CellSize / 2f);
            foreach (var block in level.Blocks)
            foreach (var gate in level.Gates.Where(gate => gate.Color == block.Color))
            {
                var horizontal = ExitGate.IsHorizontalEdge(gate.Edge);
                var extentX = block.Cells.Max(cell => cell.x); var extentZ = block.Cells.Max(cell => cell.y);
                var perpendicularExtent = horizontal ? extentX : extentZ;
                if (perpendicularExtent >= gate.Span) continue;
                foreach (var along in new[] { gate.Start, gate.Start + gate.Span - 1 - perpendicularExtent })
                {
                    var anchor = gate.Edge == BoardEdge.North ? new Vector2Int(along, level.Height) :
                        gate.Edge == BoardEdge.South ? new Vector2Int(along, -extentZ - 1) :
                        gate.Edge == BoardEdge.East ? new Vector2Int(level.Width, along) : new Vector2Int(-extentX - 1, along);
                    var end = origin + new Vector3(anchor.x, 0f, anchor.y) * level.CellSize;
                    foreach (var piece in SoftBurstMotion.Create(block.Cells, gate.Edge))
                    for (var step = 0; step <= 20; step++)
                    {
                        var u = step / 20f; var point = end + piece.Position(u) * level.CellSize;
                        var diameter = piece.Scale(u) * level.CellSize * 1.733f; // Rotation-independent cube sphere bound.
                        if (!M0Validation.FitsPortraitView(camera, new Bounds(point, Vector3.one * diameter)))
                        { errors.Add("Burst leaves approved portrait framing: " + level.Id + "/" + block.Id + "/" + gate.Edge); return; }
                    }
                }
            }
        }
        [MenuItem("ColorSlide/Setup/Validate M4.5 Animation")]
        private static void ValidateFromMenu()
        {
            var errors = M0Validation.InspectProject(); errors.AddRange(InspectSavedScene());
            if (errors.Count == 0) Debug.Log("ColorSlide M4.5 references and burst framing are valid. Play Mode visual/lifecycle QA remains manual.");
            else Debug.LogError("ColorSlide M4.5 validation:\n- " + string.Join("\n- ", errors));
        }
    }
}
