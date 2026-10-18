using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ColorSlide.Polish
{
    public enum SoundCue { None, Select, Blocked, Exit, UiClick, Win, Fail }

    public sealed class SoftToyTheme : ScriptableObject
    {
        [SerializeField] private Mesh roundedBox;
        [SerializeField] private Mesh contactShadow;
        [SerializeField] private Material[] palette;
        [SerializeField] private Material tray;
        [SerializeField] private Material shadow;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private Color backgroundSrgb = new Color(243f / 255f, 245f / 255f, 247f / 255f, 1f);
        [SerializeField] private Color inkSrgb = new Color(40f / 255f, 55f / 255f, 71f / 255f, 1f);
        public Mesh RoundedBox => roundedBox;
        public Mesh ContactShadow => contactShadow;
        public IReadOnlyList<Material> Palette => palette;
        public Material Tray => tray;
        public Material Shadow => shadow;
        public Color BackgroundSrgb => backgroundSrgb;
        public Color InkSrgb => inkSrgb;
        public AudioClip Clip(SoundCue cue) => cue == SoundCue.None ? null : clips[(int)cue - 1];
        public void Configure(Mesh box, Mesh contact, Material[] colors, Material trayMaterial, Material shadowMaterial, AudioClip[] sounds)
        { roundedBox = box; contactShadow = contact; palette = (Material[])colors.Clone(); tray = trayMaterial; shadow = shadowMaterial; clips = (AudioClip[])sounds.Clone(); Validate(); }
        public void Validate()
        {
            if (roundedBox == null || contactShadow == null || tray == null || shadow == null || palette == null || palette.Length != 7 ||
                palette.Any(material => material == null) || clips == null || clips.Length != 6 || clips.Any(clip => clip == null))
                throw new InvalidOperationException("M4 theme needs its two saved meshes, seven-color palette, tray/contact materials and six sound clips.");
        }
        // URP Lit's untagged _BaseColor is stored linear; MaterialPropertyBlock.SetColor expects sRGB.
        public static Color SelectionSrgb(Material material)
        {
            var value = material.GetColor("_BaseColor");
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? value.gamma : value;
        }
    }
}
