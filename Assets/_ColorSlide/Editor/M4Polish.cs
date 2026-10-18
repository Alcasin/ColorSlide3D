using System;
using System.IO;
using System.Linq;
using ColorSlide.Gameplay;
using ColorSlide.Polish;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ColorSlide.Editor
{
    public static class M4Polish
    {
        public const string ScenePath = M0Foundation.RootPath + "/Scenes/PolishedCampaign.unity";
        public const string ArtPath = M0Foundation.RootPath + "/Art/M4";
        public const string AudioPath = M0Foundation.RootPath + "/Audio/M4";
        public const string MaterialsPath = M0Foundation.RootPath + "/Materials/M4";
        public const string ThemePath = ArtPath + "/SoftToyTheme.asset";
        public const string ShaderPath = ArtPath + "/SoftContact.shader";
        public static string SoundPath(SoundCue cue) => AudioPath + "/" + cue + ".wav";

        [MenuItem("ColorSlide/Setup/Apply M4 Polish")]
        private static void ApplyFromMenu()
        {
            try { Apply(); Debug.Log("ColorSlide M4: polished assets are ready. Open M4 Polished Campaign and Play in portrait."); }
            catch (Exception exception) { Debug.LogError("ColorSlide M4 setup stopped: " + exception.Message); }
        }
        public static void Apply()
        {
            M0Foundation.RequireEditModeAndSavedScenes();
            ThrowOnErrors(M0Validation.InspectProject());
            RequireCompatible<SceneAsset>(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { RepairExistingScene(); return; }
            ThrowOnErrors(M3Validation.InspectSavedScene());
            RequireCompatible<SoftToyTheme>(ThemePath);
            var theme = AssetDatabase.LoadAssetAtPath<SoftToyTheme>(ThemePath);
            if (theme == null) theme = CreateTheme();
            ThrowOnErrors(M4Validation.InspectTheme(theme));

            var previous = SceneManager.GetActiveScene(); var copied = false; var saved = false;
            try
            {
                copied = AssetDatabase.CopyAsset(M3Campaign.ScenePath, ScenePath);
                if (!copied) throw new IOException("Could not copy Campaign to PolishedCampaign.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                Build(scene, theme);
                ThrowOnErrors(M4Validation.InspectScene(scene));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save PolishedCampaign.");
                saved = true;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                var scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                scene = SceneManager.GetSceneByPath(ScenePath);
                if (copied && !saved && (!scene.IsValid() || !scene.isLoaded)) AssetDatabase.DeleteAsset(ScenePath);
                // Valid newly created resources are preserved for retry. Existing resources are never overwritten.
            }
        }

        private static void RepairExistingScene()
        {
            // Preflight on an isolated copy in memory; unrelated validation failures cannot dirty the user's scene.
            var preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            bool needsRepair;
            try
            {
                needsRepair = M4HudStyling.RepairGraphics(Hud(preview));
                ThrowOnErrors(M4Validation.InspectScene(preview));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            if (!needsRepair) return;

            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                if (!M4HudStyling.RepairGraphics(Hud(scene))) return;
                ThrowOnErrors(M4Validation.InspectScene(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the repaired M4 HUD.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        private static GameplayHud Hud(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GameplayHud>(true)).Single();

        private static SoftToyTheme CreateTheme()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var contactShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (lit == null || contactShader == null) throw new InvalidOperationException("Import the URP Lit and M4 SoftContact shaders first.");
            EnsureFolder(ArtPath); EnsureFolder(MaterialsPath); EnsureFolder(AudioPath);
            var rounded = Saved(ArtPath + "/RoundedBox.asset", () => Mesh("Soft Rounded Box", SoftGeometry.RoundedBox()));
            var contact = Saved(ArtPath + "/ContactShadow.asset", () => Mesh("Soft Contact Shadow", SoftGeometry.ContactShadow()));
            var names = new[] { "Blue", "Amber", "Violet", "Green", "TileA", "TileB", "Obstacle", "Tray" };
            var colors = new[] { "529FE8", "F3AD55", "9A7BDD", "50B993", "E3E9EE", "E1E7EC", "9AA9B8", "D9E3EC" };
            var palette = new Material[8];
            for (var i = 0; i < palette.Length; i++)
            {
                var index = i;
                palette[i] = Saved(MaterialsPath + "/" + names[i] + ".mat", () =>
                {
                    var material = new Material(lit) { name = "M4 " + names[index], enableInstancing = true };
                    // URP Lit _BaseColor has neither HDR nor Gamma flags; SetColor stores the supplied value as-is.
                    var srgb = M4HudStyling.Hex(colors[index]);
                    material.SetColor("_BaseColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb);
                    material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", index < 4 ? 0.28f : 0.18f);
                    material.SetFloat("_Surface", 0f); material.SetFloat("_ZWrite", 1f); material.SetFloat("_ReceiveShadows", 1f);
                    return material;
                });
            }
            var shadow = Saved(MaterialsPath + "/ContactShadow.mat", () =>
            {
                var material = new Material(contactShader) { name = "M4 Soft Contact" };
                var tint = M4HudStyling.Hex("283747");
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) tint = tint.linear;
                tint.a = 0.16f; material.SetColor("_BaseColor", tint); return material;
            });
            var clips = new AudioClip[6];
            for (var i = 0; i < clips.Length; i++)
            {
                var path = SoundPath((SoundCue)(i + 1));
                if (!File.Exists(path)) M4SoundDesign.WriteNewWav(path, (SoundCue)(i + 1));
                M4SoundDesign.ValidateWav(path);
                RequireCompatibleFile<AudioClip>(path);
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[i] == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                if (clips[i] == null) throw new IOException("Import the PCM sound first: " + path);
            }
            return Saved(ThemePath, () =>
            {
                var asset = ScriptableObject.CreateInstance<SoftToyTheme>();
                asset.Configure(rounded, contact, palette.Take(7).ToArray(), palette[7], shadow, clips); return asset;
            });
        }

        private static void Build(Scene scene, SoftToyTheme theme)
        {
            var roots = scene.GetRootGameObjects();
            var campaign = roots.SelectMany(root => root.GetComponentsInChildren<CampaignController>()).Single();
            campaign.Presentation.ConfigureTheme(theme);
            var slab = roots.Single(root => root.name == M0Foundation.BoardName);
            slab.GetComponent<MeshFilter>().sharedMesh = theme.RoundedBox;
            slab.GetComponent<MeshRenderer>().sharedMaterial = theme.Tray;
            var camera = campaign.Presentation.InteractionCamera;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = theme.BackgroundSrgb;
            camera.gameObject.AddComponent<PortraitCameraFit>().Configure(camera.orthographicSize);
            var light = roots.SelectMany(root => root.GetComponentsInChildren<Light>()).First(item => item.type == LightType.Directional);
            light.intensity = 1.0f; light.color = Color.white; light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.25f; light.shadowBias = 0.04f; light.shadowNormalBias = 0.2f;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.70f, 0.73f, 0.77f);
            RenderSettings.reflectionIntensity = 0.35f;
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHud>()).Single();
            var buttons = M4HudStyling.Apply(hud, theme);
            var source = campaign.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f; source.volume = 0.55f; source.priority = 64;
            campaign.gameObject.AddComponent<PolishFeedback>().Configure(campaign, theme, source, buttons);
        }

        public static Mesh Mesh(string name, SoftMeshData data)
        {
            data.Validate();
            var mesh = new Mesh { name = name, vertices = data.Vertices, normals = data.Normals, uv = data.UV, colors = data.Colors, triangles = data.Triangles };
            mesh.RecalculateBounds(); return mesh;
        }
        private static T Saved<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            RequireCompatible<T>(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                if (EditorUtility.IsDirty(existing)) throw new InvalidOperationException("Save M4 asset edits first: " + path);
                return existing;
            }
            var asset = create();
            try
            {
                AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
                var saved = AssetDatabase.LoadAssetAtPath<T>(path);
                if (saved == null || !AssetDatabase.Contains(saved)) throw new IOException("Could not persist " + path);
                return saved;
            }
            catch { if (!AssetDatabase.Contains(asset)) UnityEngine.Object.DestroyImmediate(asset); throw; }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("Conflicting or unimported folder preserved: " + path);
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path)))) throw new IOException("Could not create " + path);
        }
        private static void RequireCompatible<T>(string path) where T : UnityEngine.Object
        {
            if ((File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) &&
                AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("Conflicting, incomplete or unimported asset preserved: " + path);
        }
        // An original WAV may have just been written outside the importer; allow its first supported import.
        private static void RequireCompatibleFile<T>(string path) where T : UnityEngine.Object
        {
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (Directory.Exists(path) || (main != null && !(main is T))) throw new InvalidOperationException("Conflicting sound asset preserved: " + path);
        }
        private static void ThrowOnErrors(System.Collections.Generic.List<string> errors)
        { if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors)); }
        [MenuItem("ColorSlide/Setup/Open M4 Polished Campaign")]
        private static void OpenFromMenu()
        {
            try
            {
                M0Foundation.RequireEditModeAndSavedScenes();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new InvalidOperationException("Run Apply M4 Polish first.");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception) { Debug.LogError("ColorSlide M4 open stopped: " + exception.Message); }
        }
    }
}
