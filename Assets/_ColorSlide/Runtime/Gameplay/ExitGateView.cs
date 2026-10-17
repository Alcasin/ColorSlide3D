using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Gameplay
{
    /// <summary>The serialized gate definition is the source for both logic and its authored opening.</summary>
    public sealed class ExitGateView : MonoBehaviour
    {
        [SerializeField] private string gateId;
        [SerializeField] private BlockColor colorId;
        [SerializeField] private BoardEdge edge;
        [SerializeField] private int start;
        [SerializeField] private int span;
        public ExitGate CreateDefinition() => new ExitGate(gateId, colorId, edge, start, span);
        public void Configure(ExitGate gate)
        {
            gateId = gate.Id;
            colorId = gate.ColorId;
            edge = gate.Edge;
            start = gate.Start;
            span = gate.Span;
        }
    }
}
