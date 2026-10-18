using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ColorSlide.Board;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using ColorSlide.Interaction;
using ColorSlide.Polish;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorSlide.Tests.EditMode
{
    public sealed class M45FeedbackEditorTests
    {
        private static void Call(object target, string method, params object[] arguments)
        {
            var signature = arguments.Select(argument => argument.GetType()).ToArray();
            var callback = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, signature, null);
            Assert.That(callback, Is.Not.Null, "Missing test callback: " + target.GetType().Name + "." + method);
            callback.Invoke(target, arguments);
        }
        // EditMode has no running input/MonoBehaviour loop: deliver the real producer's event to real subscribers.
        private static void Emit(PointerDragController drag, string name, params object[] arguments) =>
            ((Delegate)typeof(PointerDragController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(drag))?.DynamicInvoke(arguments);
        private static void BindHud(GameplayHud hud, GameplayController gameplay)
        {
            var fields = new SerializedObject(hud); fields.FindProperty("campaign").objectReferenceValue = null;
            fields.FindProperty("gameplay").objectReferenceValue = gameplay; fields.ApplyModifiedPropertiesWithoutUndo();
            hud.GetComponent<TimerUrgency>().Refresh();
        }
        private static void WithGameplay(Action<GameplayController, GameplayHud, Scene> check, bool build = true)
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M45Feedback.ScenePath), Is.Not.Null, "Run Apply M4.5 Feedback Polish first.");
            var active = SceneManager.GetActiveScene();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/_ColorSlide/Tests/EditMode/M45FeedbackOwnedTest.unity");
            var copied = false; var scene = default(Scene); GameplayController gameplay = null;
            TimerUrgency urgency = null; PolishFeedback audio = null;
            try
            {
                copied = AssetDatabase.CopyAsset(M45Feedback.ScenePath, path); Assert.That(copied, Is.True);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                var owner = roots.SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).Single();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
                urgency = hud.GetComponent<TimerUrgency>();
                if (build)
                {
                    var level = owner.Levels[0]; gameplay = owner.Presentation.Build(level.Data, level.CreateSession());
                    gameplay.gameObject.SetActive(true);
                    foreach (var visual in gameplay.GetComponentsInChildren<PolishedBlockVisual>(true))
                    { Call(visual, "OnDisable"); Call(visual, "OnEnable"); }
                    var glow = gameplay.GetComponent<GateGlow>();
                    Call(glow, "Awake"); // Native initialization belongs to the runtime lifecycle, absent from the EditMode loop.
                    Call(glow, "OnDisable"); Call(glow, "OnEnable");
                    audio = roots.SelectMany(root => root.GetComponentsInChildren<PolishFeedback>(true)).Single();
                    var fields = new SerializedObject(audio); fields.FindProperty("source").objectReferenceValue = null;
                    fields.ApplyModifiedPropertiesWithoutUndo(); Call(audio, "Bind", gameplay);
                }
                check(gameplay, hud, scene);
            }
            finally
            {
                try
                {
                    if (audio != null) Call(audio, "OnDisable");
                    if (urgency != null) Call(urgency, "OnDisable");
                    if (gameplay != null)
                    {
                        gameplay.GetComponent<GateGlow>()?.Cancel();
                        foreach (var visual in gameplay.GetComponentsInChildren<PolishedBlockVisual>(true)) Call(visual, "OnDisable");
                        LevelPresentation.Retire(gameplay.gameObject);
                    }
                }
                finally
                {
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                    scene = SceneManager.GetSceneByPath(path);
                    if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                    scene = SceneManager.GetSceneByPath(path);
                    if (copied && (!scene.IsValid() || !scene.isLoaded) && File.Exists(path)) AssetDatabase.DeleteAsset(path);
                }
            }
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
        [Test]
        public void LogicalExitPulsesOnlyItsMatchingGateAndRestoresPropertiesForReuse()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                var glow = gameplay.GetComponent<GateGlow>();
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue");
                var gate = gameplay.GateViews.Single(view => view.CreateDefinition().ColorId == BlockColor.Blue);
                var gatePosition = gate.transform.position; var gateScale = gate.transform.localScale;
                var renderers = gate.GetComponentsInChildren<MeshRenderer>(); var scales = renderers.Select(renderer => renderer.transform.localScale).ToArray();
                var materialColors = renderers.Select(renderer => renderer.sharedMaterial.GetColor("_BaseColor")).ToArray();
                var original = new MaterialPropertyBlock(); original.SetFloat("_FeedbackTest", 0.37f);
                original.SetColor("_BaseColor", new Color(0.2f, 0.5f, 0.7f)); renderers[0].SetPropertyBlock(original);
                gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y);
                Assert.That(glow.ActiveGate, Is.Null);
                gameplay.RequestMove(blue, new Vector2Int(4, 0), GridAxis.X);
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(4, -1), GridAxis.Y).Status, Is.EqualTo(MovementStatus.InvalidGate));
                Assert.That(glow.ActiveGate, Is.Null, "A rejected exit at the actual Amber gate must not glow.");
                gameplay.RequestMove(blue, new Vector2Int(1, 0), GridAxis.X);
                var exit = gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y);
                Assert.That(exit.Status, Is.EqualTo(MovementStatus.Exited)); Assert.That(glow.ActiveGate, Is.SameAs(gate));
                glow.Refresh();
                var untinted = new MaterialPropertyBlock(); renderers[1].GetPropertyBlock(untinted);
                var expectedColor = SoftToyTheme.SelectionSrgb(renderers[1].sharedMaterial);
                Assert.That(untinted.GetColor("_BaseColor").r, Is.EqualTo(expectedColor.r).Within(0.0001f), "No darkening from treating a linear material value as sRGB.");
                gameplay.AdvanceExitPresentation(0.15f); glow.Refresh();
                Assert.That(renderers[0].transform.localScale.y / scales[0].y, Is.InRange(1.17f, 1.19f));
                Assert.That(renderers[0].transform.localScale.x, Is.EqualTo(scales[0].x));
                Assert.That(renderers[0].transform.localScale.z, Is.EqualTo(scales[0].z));
                Assert.That(gate.transform.position, Is.EqualTo(gatePosition)); Assert.That(gate.transform.localScale, Is.EqualTo(gateScale));
                var tinted = new MaterialPropertyBlock(); renderers[0].GetPropertyBlock(tinted);
                Assert.That(tinted.GetColor("_BaseColor").r, Is.GreaterThan(0.2f)); Assert.That(tinted.GetFloat("_FeedbackTest"), Is.EqualTo(0.37f));
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.SessionInactive));
                gameplay.AdvanceExitPresentation(0.16f); glow.Refresh(); Assert.That(glow.ActiveGate, Is.Null);
                for (var i = 0; i < renderers.Length; i++)
                { Assert.That(renderers[i].transform.localScale, Is.EqualTo(scales[i])); Assert.That(renderers[i].sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(materialColors[i])); }
                renderers[0].GetPropertyBlock(tinted); Assert.That(tinted.GetColor("_BaseColor"), Is.EqualTo(original.GetColor("_BaseColor")));
                gameplay.AdvanceExitPresentation(1f);
                // Use the real restart notification after a model restart; the public runtime Restart is Play Mode guarded.
                gameplay.Session.Restart(); Call(gameplay.Board, "AdoptModel", gameplay.Session.Board); blue.gameObject.SetActive(true);
                ((Action)typeof(GameplayController).GetField("Restarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gameplay))?.Invoke();
                Assert.That(gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y).Status, Is.EqualTo(MovementStatus.Exited));
                Assert.That(glow.ActiveGate, Is.SameAs(gate)); gameplay.AdvanceExitPresentation(0.1f); glow.Refresh();
                glow.Cancel(); glow.Cancel(); Assert.That(renderers[0].transform.localScale, Is.EqualTo(scales[0]));
            });
        }
        [Test]
        public void BlockedCueSharesAudioDedupAndCompressesOnlyVisualChildrenThenCancelsOnMovement()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"); var visual = blue.GetComponent<PolishedBlockVisual>();
                gameplay.RequestMove(blue, new Vector2Int(1, 0), GridAxis.Y);
                blue.SnapTo(gameplay.Board.Coordinates.CellToWorld(gameplay.Board.Model.GetBlock("Blue").Anchor));
                var rootPosition = blue.transform.position; var rootScale = blue.transform.localScale; var anchor = gameplay.Board.Model.GetBlock("Blue").Anchor;
                var colliders = blue.GetComponentsInChildren<BoxCollider>(); var colliderSizes = colliders.Select(collider => collider.size).ToArray();
                var shapes = blue.CellRenderers.Select(renderer => renderer.transform).ToArray(); var baseScales = shapes.Select(child => child.localScale).ToArray();
                blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                var blocked = gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y);
                Assert.That(blocked.Status, Is.EqualTo(MovementStatus.BlockedByBoundary)); Assert.That(blocked.AcceptedSteps, Is.Zero);
                Emit(gameplay.Drag, "MovementRequested", blue, blocked); Assert.That(visual.IsBlockedPulse, Is.True);
                visual.AdvanceFeedback(0.09f);
                for (var i = 0; i < shapes.Length; i++)
                { Assert.That(shapes[i].localScale.y, Is.LessThan(baseScales[i].y)); Assert.That(shapes[i].localScale.x, Is.LessThan(baseScales[i].x)); Assert.That(shapes[i].localScale.z, Is.LessThan(baseScales[i].z)); Assert.That(shapes[i].localPosition.y, Is.GreaterThan(0f)); }
                Assert.That(blue.transform.position, Is.EqualTo(rootPosition)); Assert.That(blue.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(gameplay.Board.Model.GetBlock("Blue").Anchor, Is.EqualTo(anchor));
                for (var i = 0; i < colliders.Length; i++) Assert.That(colliders[i].size, Is.EqualTo(colliderSizes[i]));
                for (var i = 0; i < 50; i++) Emit(gameplay.Drag, "MovementRequested", blue, blocked);
                visual.AdvanceFeedback(0.091f); Assert.That(visual.IsBlockedPulse, Is.False);
                Emit(gameplay.Drag, "MovementRequested", blue, blocked); Assert.That(visual.IsBlockedPulse, Is.False, "A held gesture may not rearm after the animation finishes.");
                Emit(gameplay.Drag, "SelectionStarted", blue); Emit(gameplay.Drag, "MovementRequested", blue, blocked);
                Assert.That(visual.IsBlockedPulse, Is.True);
                var partial = gameplay.RequestMove(blue, new Vector2Int(7, 0), GridAxis.X);
                Assert.That(partial.Status, Is.EqualTo(MovementStatus.BlockedByBoundary)); Assert.That(partial.AcceptedSteps, Is.GreaterThan(0));
                Emit(gameplay.Drag, "MovementRequested", blue, partial); Assert.That(visual.IsBlockedPulse, Is.False, "Accepted partial travel also resumes dragging.");
                var moved = gameplay.RequestMove(blue, new Vector2Int(1, 2), GridAxis.Y); Assert.That(moved.Status, Is.EqualTo(MovementStatus.Moved));
                Emit(gameplay.Drag, "MovementRequested", blue, moved); Assert.That(visual.IsBlockedPulse, Is.False);
                for (var i = 0; i < shapes.Length; i++) Assert.That(shapes[i].localScale, Is.EqualTo(baseScales[i]));
                Assert.That(blue.IsSelected, Is.True); blue.SetSelected(false);
                Emit(gameplay.Drag, "SelectionStarted", blue); visual.PlayBlocked(); Assert.That(visual.IsBlockedPulse, Is.False);
            });
        }
        [TestCase(false)] [TestCase(true)]
        public void PartialBlockedRequestDeliversOneVisibleContactRegardlessOfVisualSubscriptionOrder(bool reenableAfterAudio)
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"); var visual = blue.GetComponent<PolishedBlockVisual>();
                if (reenableAfterAudio) { Call(visual, "OnDisable"); Call(visual, "OnEnable"); }
                var renderer = blue.CellRenderers[0]; var scale = renderer.transform.localScale;
                var rootPosition = blue.transform.position; var rootScale = blue.transform.localScale;
                var colliderScales = blue.GetComponentsInChildren<Collider>().Select(collider => collider.transform.localScale).ToArray();
                blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                var before = new MaterialPropertyBlock(); renderer.GetPropertyBlock(before); var selected = before.GetColor("_BaseColor");
                var camera = gameplay.Drag.InteractionCamera; camera.aspect = 9f / 16f; // Only this owned test copy.
                var width = (camera.WorldToViewportPoint(renderer.bounds.max).x - camera.WorldToViewportPoint(renderer.bounds.min).x) * 1080f;
                var blocked = gameplay.RequestMove(blue, new Vector2Int(7, 1), GridAxis.X);
                Assert.That(blocked.Status, Is.EqualTo(MovementStatus.BlockedByBoundary)); Assert.That(blocked.AcceptedSteps, Is.GreaterThan(0));
                var anchor = gameplay.Board.Model.GetBlock("Blue").Anchor;
                Emit(gameplay.Drag, "MovementRequested", blue, blocked); Assert.That(visual.IsBlockedPulse, Is.True);
                visual.AdvanceFeedback(0.09f);
                Assert.That(renderer.transform.localScale.x / scale.x, Is.InRange(0.91f, 0.93f));
                Assert.That(renderer.transform.localScale.z / scale.z, Is.InRange(0.91f, 0.93f));
                var contactWidth = (camera.WorldToViewportPoint(renderer.bounds.max).x - camera.WorldToViewportPoint(renderer.bounds.min).x) * 1080f;
                Assert.That(width - contactWidth, Is.GreaterThan(6f), "Footprint response must be legible at the reference portrait size.");
                var tinted = new MaterialPropertyBlock(); renderer.GetPropertyBlock(tinted);
                Assert.That(tinted.GetColor("_BaseColor").r - selected.r, Is.GreaterThan(0.1f));
                Assert.That(blue.IsSelected, Is.True); Assert.That(blue.transform.position, Is.EqualTo(rootPosition)); Assert.That(blue.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(blue.GetComponentsInChildren<Collider>().Select(collider => collider.transform.localScale), Is.EqualTo(colliderScales));
                Assert.That(gameplay.Board.Model.GetBlock("Blue").Anchor, Is.EqualTo(anchor));
                var held = gameplay.RequestMove(blue, new Vector2Int(7, 1), GridAxis.X); Assert.That(held.AcceptedSteps, Is.Zero);
                for (var frame = 0; frame < 50; frame++) Emit(gameplay.Drag, "MovementRequested", blue, held);
                visual.AdvanceFeedback(0.091f); Assert.That(visual.IsBlockedPulse, Is.False);
                Emit(gameplay.Drag, "MovementRequested", blue, held); Assert.That(visual.IsBlockedPulse, Is.False);
                renderer.GetPropertyBlock(tinted); Assert.That(tinted.GetColor("_BaseColor"), Is.EqualTo(selected));
                Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
            });
        }
        [Test]
        public void ContactFlashRestoresSelectionCustomPropertiesAndRestartPresentation()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"); var visual = blue.GetComponent<PolishedBlockVisual>();
                gameplay.RequestMove(blue, new Vector2Int(1, 0), GridAxis.Y);
                var renderer = blue.CellRenderers[0]; var scale = renderer.transform.localScale;
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties); var resting = properties.GetColor("_BaseColor");
                properties.SetFloat("_FeedbackAudit", 0.42f); renderer.SetPropertyBlock(properties);
                blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                var blocked = gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y);
                Emit(gameplay.Drag, "MovementRequested", blue, blocked); visual.AdvanceFeedback(0.09f);
                renderer.GetPropertyBlock(properties); Assert.That(properties.GetColor("_BaseColor"), Is.Not.EqualTo(resting));
                blue.SetSelected(false); renderer.GetPropertyBlock(properties);
                Assert.That(visual.IsBlockedPulse, Is.False); Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(resting));
                Assert.That(properties.GetFloat("_FeedbackAudit"), Is.EqualTo(0.42f)); Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
                blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                renderer.GetPropertyBlock(properties); var selected = properties.GetColor("_BaseColor");
                Emit(gameplay.Drag, "MovementRequested", blue, blocked); visual.AdvanceFeedback(0.09f);
                var moved = gameplay.RequestMove(blue, new Vector2Int(1, 2), GridAxis.Y);
                Emit(gameplay.Drag, "MovementRequested", blue, moved); renderer.GetPropertyBlock(properties);
                Assert.That(visual.IsBlockedPulse, Is.False); Assert.That(blue.IsSelected, Is.True);
                Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(selected)); Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
                blue.SetSelected(false); blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                Emit(gameplay.Drag, "MovementRequested", blue, gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y)); visual.AdvanceFeedback(0.09f);
                Assert.That(visual.IsBlockedPulse, Is.True);
                gameplay.Session.Restart(); Call(gameplay.Board, "AdoptModel", gameplay.Session.Board);
                ((Action)typeof(GameplayController).GetField("Restarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gameplay))?.Invoke();
                renderer.GetPropertyBlock(properties);
                Assert.That(visual.IsBlockedPulse, Is.False); Assert.That(blue.IsSelected, Is.False);
                Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(resting)); Assert.That(properties.GetFloat("_FeedbackAudit"), Is.EqualTo(0.42f));
                Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
            });
        }
        [Test]
        public void SixWinSparklesHavePortraitSizeContrastVisibleMeshAndTopSiblingOrder()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                BindHud(hud, gameplay); var reveal = hud.GetComponent<TimerUrgency>().Reveal;
                gameplay.RequestMove(gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"), new Vector2Int(1, 8), GridAxis.Y); gameplay.AdvanceExitPresentation(1f);
                gameplay.RequestMove(gameplay.Board.BlockViews.Single(view => view.BlockId == "Amber"), new Vector2Int(4, -1), GridAxis.Y); gameplay.AdvanceExitPresentation(0.47f);
                hud.Refresh(); Assert.That(reveal.ActiveDecorationCount, Is.Zero); Assert.That(reveal.gameObject.activeInHierarchy, Is.False);
                gameplay.AdvanceExitPresentation(0.02f); hud.Refresh(); reveal.BeginPresentation(); reveal.AdvancePresentation(0.30f);
                var stars = reveal.GetComponentsInChildren<SoftSparkle>(); Assert.That(stars.Length, Is.EqualTo(6));
                Assert.That(stars[0].transform.parent.GetSiblingIndex(), Is.EqualTo(reveal.transform.childCount - 1));
                Assert.That(reveal.GetComponentsInParent<Mask>(true), Is.Empty); Assert.That(reveal.GetComponentsInParent<RectMask2D>(true), Is.Empty);
                Assert.That(reveal.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.99f));
                foreach (var star in stars)
                {
                    Assert.That(star.raycastTarget, Is.False); Assert.That(star.canvasRenderer, Is.Not.Null);
                    Assert.That(star.rectTransform.sizeDelta.x * star.rectTransform.localScale.x, Is.InRange(43f, 55f));
                    Assert.That(star.rectTransform.anchorMin.y, Is.GreaterThan(0.60f), "Keep the lower Retry/Next button region clear.");
                    Assert.That(Mathf.Abs(star.rectTransform.anchoredPosition.x), Is.GreaterThan(17f));
                    Assert.That(star.color.a, Is.GreaterThan(0.9f));
                    var visible = Color.Lerp(Color.white, star.color, star.color.a);
                    var luminance = 0.2126f * Mathf.GammaToLinearSpace(visible.r) + 0.7152f * Mathf.GammaToLinearSpace(visible.g) + 0.0722f * Mathf.GammaToLinearSpace(visible.b);
                    Assert.That(1.05f / (luminance + 0.05f), Is.GreaterThan(2.1f));
                    using (var mesh = new VertexHelper())
                    { Call(star, "OnPopulateMesh", mesh); Assert.That(mesh.currentVertCount, Is.EqualTo(9)); Assert.That(mesh.currentIndexCount, Is.EqualTo(24)); }
                }
                reveal.AdvancePresentation(0.31f); Assert.That(reveal.ActiveDecorationCount, Is.Zero);
            });
        }
        [Test]
        public void RepeatedAuthoredLevelReplacementRestoresUrgencyAndRemovesOwnedContactAndGateEffects()
        {
            WithGameplay((unused, hud, scene) =>
            {
                var roots = scene.GetRootGameObjects();
                var campaign = roots.SelectMany(root => root.GetComponentsInChildren<CampaignController>(true)).Single();
                var audio = roots.SelectMany(root => root.GetComponentsInChildren<PolishFeedback>(true)).Single();
                var fields = new SerializedObject(audio); fields.FindProperty("source").objectReferenceValue = null; fields.ApplyModifiedPropertiesWithoutUndo();
                var timer = hud.GetComponent<TimerUrgency>(); var normal = timer.Pill.color;
                GameplayController current = null;
                try
                {
                    for (var iteration = 0; iteration < 8; iteration++)
                    {
                        var level = campaign.Levels[iteration % 4]; current = campaign.Presentation.Build(level.Data, level.CreateSession()); current.gameObject.SetActive(true);
                        foreach (var visual in current.GetComponentsInChildren<PolishedBlockVisual>()) { Call(visual, "OnDisable"); Call(visual, "OnEnable"); }
                        var glow = current.GetComponent<GateGlow>(); Call(glow, "Awake"); Call(glow, "OnDisable"); Call(glow, "OnEnable");
                        Call(audio, "Bind", current); BindHud(hud, current);
                        current.Session.Advance(current.Session.RemainingSeconds - 14d); timer.Refresh(); Assert.That(timer.IsUrgent, Is.True);
                        var block = current.Board.BlockViews[0]; block.SetSelected(true); Emit(current.Drag, "SelectionStarted", block);
                        var blocked = current.RequestMove(block, new Vector2Int(-1, current.Board.Model.GetBlock(block.BlockId).Anchor.y), GridAxis.X);
                        Assert.That(blocked.Status, Is.Not.EqualTo(MovementStatus.Exited)); Emit(current.Drag, "MovementRequested", block, blocked);
                        block.GetComponent<PolishedBlockVisual>().AdvanceFeedback(0.09f); Assert.That(block.GetComponent<PolishedBlockVisual>().IsBlockedPulse, Is.True);
                        Assert.That(current.GetComponents<GateGlow>().Length, Is.EqualTo(1)); Assert.That(hud.GetComponents<TimerUrgency>().Length, Is.EqualTo(1));
                        foreach (var visual in current.GetComponentsInChildren<PolishedBlockVisual>()) Call(visual, "OnDisable");
                        Call(glow, "OnDisable"); Call(current, "OnDisable"); LevelPresentation.Retire(current.gameObject); current = null;
                        timer.Refresh(); Assert.That(timer.IsUrgent, Is.False); Assert.That(timer.Pill.color, Is.EqualTo(normal));
                        Assert.That(campaign.Presentation.GetComponentsInChildren<GateGlow>(true), Is.Empty);
                        Assert.That(hud.GetComponentsInChildren<SoftSparkle>(true), Is.Empty);
                    }
                }
                finally { Call(audio, "OnDisable"); if (current != null) LevelPresentation.Retire(current.gameObject); }
            }, false);
        }
        [Test]
        public void ExistingWinCardWaitsForFinalBurstCelebratesOnceAndRemovesNonRaycastingStars()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                BindHud(hud, gameplay); var timer = hud.GetComponent<TimerUrgency>(); var reveal = timer.Reveal;
                var pillColor = timer.Pill.color;
                gameplay.Session.Advance(gameplay.Session.RemainingSeconds - 14d); timer.Refresh(); Assert.That(timer.IsUrgent, Is.True);
                var panel = (GameObject)new SerializedObject(hud).FindProperty("resultPanel").objectReferenceValue;
                var originalScale = reveal.transform.localScale; var buttons = reveal.GetComponentsInChildren<Button>(true);
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"); var amber = gameplay.Board.BlockViews.Single(view => view.BlockId == "Amber");
                gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y); gameplay.AdvanceExitPresentation(1f);
                gameplay.RequestMove(amber, new Vector2Int(4, -1), GridAxis.Y); gameplay.AdvanceExitPresentation(0.31f);
                timer.Refresh(); Assert.That(timer.IsUrgent, Is.False); Assert.That(timer.Pill.color, Is.EqualTo(pillColor));
                hud.Refresh(); Assert.That(panel.activeSelf, Is.False); Assert.That(reveal.ActiveDecorationCount, Is.Zero);
                gameplay.AdvanceExitPresentation(0.18f); hud.Refresh(); Assert.That(panel.activeSelf, Is.True);
                reveal.BeginPresentation(); Assert.That(reveal.ActiveDecorationCount, Is.EqualTo(6));
                Assert.That(reveal.GetComponentsInChildren<SoftSparkle>().All(star => !star.raycastTarget && star.GetComponent<CanvasRenderer>() != null), Is.True);
                Assert.That(reveal.GetComponentsInChildren<Button>(true), Is.EquivalentTo(buttons));
                reveal.AdvancePresentation(0.3f); Assert.That(reveal.IsCelebrating, Is.True);
                reveal.AdvancePresentation(0.31f); Assert.That(reveal.ActiveDecorationCount, Is.Zero); Assert.That(reveal.transform.localScale, Is.EqualTo(originalScale));
                reveal.BeginPresentation(); Assert.That(reveal.ActiveDecorationCount, Is.Zero, "The same Won session must not create duplicate decoration.");
                reveal.ResetCelebration(); reveal.BeginPresentation(); Assert.That(reveal.ActiveDecorationCount, Is.EqualTo(6));
                Call(timer, "OnDisable"); Assert.That(reveal.ActiveDecorationCount, Is.Zero);
                Call(reveal, "OnDisable"); Assert.That(reveal.ActiveDecorationCount, Is.Zero); Assert.That(reveal.transform.localScale, Is.EqualTo(originalScale));
                Call(timer, "OnEnable"); reveal.BeginPresentation(); Assert.That(reveal.ActiveDecorationCount, Is.Zero, "Cosmetic disable/re-enable cannot replay the same Win.");
                gameplay.Session.Restart(); gameplay.Session.Advance(gameplay.Session.InitialSeconds);
                reveal.ResetCelebration(); reveal.BeginPresentation(); Assert.That(reveal.IsCelebrating, Is.False); Assert.That(reveal.ActiveDecorationCount, Is.Zero);
            });
        }
        [Test]
        public void TimerUsesTheSessionThresholdAndRestoresApprovedAppearanceOnRestartFailAndDisable()
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                BindHud(hud, gameplay); var timer = hud.GetComponent<TimerUrgency>(); var pill = timer.Pill;
                var color = pill.color; var scale = pill.transform.localScale;
                var text = (Text)new SerializedObject(hud).FindProperty("timeText").objectReferenceValue; var textColor = text.color;
                gameplay.Session.Advance(gameplay.Session.RemainingSeconds - 15.01d); timer.Refresh(); Assert.That(timer.IsUrgent, Is.False);
                gameplay.Session.Advance(gameplay.Session.RemainingSeconds - 15d); timer.Refresh(); Assert.That(timer.IsUrgent, Is.True);
                gameplay.Session.Advance(0.5d); timer.Refresh(); Assert.That(pill.color, Is.Not.EqualTo(color));
                Assert.That(text.color, Is.EqualTo(textColor)); Assert.That(pill.transform.localScale, Is.EqualTo(scale));
                gameplay.Session.Restart(); timer.Refresh(); Assert.That(timer.IsUrgent, Is.False); Assert.That(pill.color, Is.EqualTo(color));
                gameplay.Session.Advance(gameplay.Session.RemainingSeconds - 10d); timer.Refresh(); Assert.That(timer.IsUrgent, Is.True);
                Call(timer, "OnDisable"); Assert.That(pill.color, Is.EqualTo(color)); Assert.That(timer.IsUrgent, Is.False);
                Call(timer, "OnEnable"); timer.Refresh(); Assert.That(timer.IsUrgent, Is.True);
                gameplay.Session.Advance(20d); timer.Refresh(); Assert.That(gameplay.Session.State, Is.EqualTo(GameplayState.Failed));
                Assert.That(timer.IsUrgent, Is.False); Assert.That(pill.color, Is.EqualTo(color));
            });
        }
        [TestCase(false)] [TestCase(true)]
        public void ComponentAndLevelInterruptionRestoreGateAndBlockedVisuals(bool wholeLevel)
        {
            WithGameplay((gameplay, hud, scene) =>
            {
                var blue = gameplay.Board.BlockViews.Single(view => view.BlockId == "Blue"); var visual = blue.GetComponent<PolishedBlockVisual>();
                var scales = blue.CellRenderers.Select(renderer => renderer.transform.localScale).ToArray();
                blue.SetSelected(true); Emit(gameplay.Drag, "SelectionStarted", blue);
                Emit(gameplay.Drag, "MovementRequested", blue, gameplay.RequestMove(blue, new Vector2Int(1, -1), GridAxis.Y)); visual.AdvanceFeedback(0.06f);
                if (!wholeLevel) { visual.enabled = false; Call(visual, "OnDisable"); }
                else
                {
                    gameplay.gameObject.SetActive(false); Call(visual, "OnDisable"); gameplay.gameObject.SetActive(true);
                    Call(gameplay.GetComponent<GateGlow>(), "OnDisable"); Call(gameplay.GetComponent<GateGlow>(), "OnEnable");
                }
                for (var i = 0; i < scales.Length; i++) Assert.That(blue.CellRenderers[i].transform.localScale, Is.EqualTo(scales[i]));
                var glow = gameplay.GetComponent<GateGlow>(); var gate = gameplay.GateViews.Single(view => view.CreateDefinition().ColorId == BlockColor.Blue);
                var renderers = gate.GetComponentsInChildren<MeshRenderer>(); var gateScales = renderers.Select(renderer => renderer.transform.localScale).ToArray();
                gameplay.RequestMove(blue, new Vector2Int(1, 8), GridAxis.Y); gameplay.AdvanceExitPresentation(0.15f); glow.Refresh();
                Assert.That(glow.ActiveGate, Is.SameAs(gate));
                if (wholeLevel) { gameplay.gameObject.SetActive(false); Call(gameplay, "OnDisable"); }
                else glow.enabled = false;
                Call(glow, "OnDisable"); Call(glow, "OnDisable"); Assert.That(glow.ActiveGate, Is.Null);
                for (var i = 0; i < gateScales.Length; i++) Assert.That(renderers[i].transform.localScale, Is.EqualTo(gateScales[i]));
            });
        }
        [Test]
        public void RepeatedApplyAndCopySaveReloadPreserveAcceptedAssetsAndRealHudReferences()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }).ToArray();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(M45Feedback.ScenePath), Is.Not.Null, "Run Apply M4.5 Feedback Polish first.");
            var paths = AssetDatabase.GetDependencies(M45Feedback.ScenePath, true).Where(File.Exists)
                .Concat(new[] { M0Foundation.ScenePath, M1InteractionDemo.ScenePath, M2GameplayDemo.ScenePath, M3Campaign.ScenePath, M4Polish.ScenePath, M45Animation.ScenePath })
                .SelectMany(path => File.Exists(path + ".meta") ? new[] { path, path + ".meta" } : new[] { path }).Distinct().ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray();
            var hasUntitledScene = Enumerable.Range(0, SceneManager.sceneCount).Any(index => string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (hasUntitledScene)
                {
                    // The EditMode runner owns an untitled scene. Keep it and verify the real safety policy;
                    // exercise idempotent production configuration/save/reload in our owned copy below.
                    var exception = Assert.Throws<InvalidOperationException>(() => M45Feedback.Apply());
                    Assert.That(exception.Message, Is.EqualTo("Save untitled scenes before feedback setup/open. No assets were changed."));
                }
                else M45Feedback.Apply();
            }
            WithGameplay((gameplay, hud, scene) =>
            {
                var timer = hud.GetComponent<TimerUrgency>(); var tint = timer.Pill.color; var position = timer.Pill.rectTransform.anchoredPosition;
                M45Feedback.ConfigureScene(scene); M45Feedback.ConfigureScene(scene);
                Assert.That(hud.GetComponents<TimerUrgency>().Length, Is.EqualTo(1)); Assert.That(timer.Pill.color, Is.EqualTo(tint));
                Assert.That(timer.Pill.rectTransform.anchoredPosition, Is.EqualTo(position)); Assert.That(M45FeedbackValidation.InspectScene(scene), Is.Empty);
                var path = scene.path; var guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();
                timer = hud.GetComponent<TimerUrgency>();
                Assert.That(M45FeedbackValidation.InspectScene(scene), Is.Empty);
                Assert.That(timer.Pill.color, Is.EqualTo(tint)); Assert.That(timer.Pill.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(hud.GetComponents<TimerUrgency>().Length, Is.EqualTo(1)); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }, false);
            for (var i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(bytes[i]), paths[i]);
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => new { item.path, item.isLoaded, item.isActive }), Is.EqualTo(setup));
        }
    }
}
