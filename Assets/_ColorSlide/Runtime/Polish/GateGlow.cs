using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Polish
{
    [DefaultExecutionOrder(50)]
    public sealed class GateGlow : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private GameplayController owner;
        private MeshRenderer[] visuals;
        private Vector3[] scales;
        private Color[] colors;
        private MaterialPropertyBlock[] originals;
        private MaterialPropertyBlock working;
        public ExitGateView ActiveGate { get; private set; }
        public void Configure(GameplayController gameplay) => owner = gameplay;
        private void Awake() => working = new MaterialPropertyBlock();
        private void OnEnable()
        {
            if (owner == null) return;
            owner.ExitStarted += Begin; owner.Restarted += Cancel;
        }
        private void OnDisable()
        {
            if (owner != null) { owner.ExitStarted -= Begin; owner.Restarted -= Cancel; }
            Cancel();
        }
        private void Begin(ExitPlan plan)
        {
            if (!isActiveAndEnabled || !plan.Block.IsRemoved || plan.Block.ColorId != plan.Gate.ColorId) return;
            Cancel();
            ActiveGate = owner.GateViews.SingleOrDefault(view => view.CreateDefinition().Id == plan.Gate.Id);
            if (ActiveGate == null) return;
            // Accepted gate children are collider-free render geometry; never scale the gate root/aperture.
            visuals = ActiveGate.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.transform != ActiveGate.transform && renderer.GetComponent<Collider>() == null).ToArray();
            scales = visuals.Select(renderer => renderer.transform.localScale).ToArray();
            originals = new MaterialPropertyBlock[visuals.Length]; colors = new Color[visuals.Length];
            for (var i = 0; i < visuals.Length; i++)
            {
                originals[i] = new MaterialPropertyBlock(); visuals[i].GetPropertyBlock(originals[i]);
                colors[i] = originals[i].HasColor(BaseColor) ? originals[i].GetColor(BaseColor) : SoftToyTheme.SelectionSrgb(visuals[i].sharedMaterial);
            }
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (ActiveGate == null) return;
            var age = owner.ExitProgress * SoftBurstMotion.Duration;
            if (!owner.isActiveAndEnabled || !owner.IsPresentingExit || age >= FeedbackMotion.GateSeconds) { Cancel(); return; }
            var strength = FeedbackMotion.GateStrength(age);
            for (var i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null) continue;
                visuals[i].GetPropertyBlock(working);
                working.SetColor(BaseColor, Color.Lerp(colors[i], new Color(1f, 1f, 1f, colors[i].a), strength * 0.45f));
                visuals[i].SetPropertyBlock(working);
                visuals[i].transform.localScale = Vector3.Scale(scales[i], new Vector3(1f, 1f + strength * 0.18f, 1f));
            }
        }
        public void Cancel()
        {
            if (visuals != null)
                for (var i = 0; i < visuals.Length; i++)
                    if (visuals[i] != null) { visuals[i].transform.localScale = scales[i]; visuals[i].SetPropertyBlock(originals[i]); }
            ActiveGate = null; visuals = null; originals = null; colors = null; scales = null;
        }
    }
}
