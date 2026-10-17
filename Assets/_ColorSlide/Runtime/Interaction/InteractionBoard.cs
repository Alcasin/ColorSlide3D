using System;
using System.Collections.Generic;
using ColorSlide.Board;
using UnityEngine;

namespace ColorSlide.Interaction
{
    /// <summary>Owns one board model and explicit scene views; the controller initializes it.</summary>
    public sealed class InteractionBoard : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 6;
        [SerializeField, Min(1)] private int height = 8;
        [SerializeField, Min(0.01f)] private float cellSize = 0.9f;
        [SerializeField] private Vector2Int[] blockedCells = Array.Empty<Vector2Int>();
        [SerializeField] private GridBlockView[] blockViews = Array.Empty<GridBlockView>();

        public GridBoard Model { get; private set; }
        public BoardCoordinates Coordinates => new BoardCoordinates(transform.position, cellSize);
        public IReadOnlyList<GridBlockView> BlockViews => blockViews;
        public IReadOnlyList<Vector2Int> BlockedCells => blockedCells;
        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        public void Configure(int columns, int rows, float size, Vector2Int[] obstacles, GridBlockView[] views)
        {
            if (Model != null)
                throw new InvalidOperationException("Do not reconfigure a running board.");
            width = columns;
            height = rows;
            cellSize = size;
            blockedCells = (Vector2Int[])obstacles.Clone();
            blockViews = (GridBlockView[])views.Clone();
        }

        // Used by startup and read-only Editor validation. Never changes scene objects.
        public GridBoard CreateModel()
        {
            if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
                (transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("The grid root must be unrotated with unit world scale (world XZ).");
            _ = Coordinates;
            if (blockViews == null || blockViews.Length == 0)
                throw new InvalidOperationException("The board needs explicit block view references.");
            var model = new GridBoard(width, height, blockedCells);
            foreach (var view in blockViews)
            {
                if (view == null || !view.transform.IsChildOf(transform))
                    throw new InvalidOperationException("Every block view must belong to this board hierarchy.");
                model.AddBlock(view.BlockId, view.InitialAnchor, view.Footprint, view.ColorId);
            }
            return model;
        }

        public void Initialize()
        {
            if (Model != null)
                return;
            Model = CreateModel();
            foreach (var view in blockViews)
            {
                view.SnapTo(Coordinates.CellToWorld(Model.GetBlock(view.BlockId).Anchor));
                view.SetSelected(false);
            }
        }

        public bool Owns(GridBlockView view) => Array.IndexOf(blockViews, view) >= 0;

        // M2 retries replace the authoritative model without rebuilding or duplicating scene objects.
        internal void AdoptModel(GridBoard model)
        {
            if (model == null || model.Width != width || model.Height != height ||
                model.ActiveBlockCount != blockViews.Length || Array.Exists(blockViews, view => !model.IsActive(view.BlockId)))
                throw new InvalidOperationException("The replacement model must match the authored board and block identities.");
            Model = model;
            foreach (var view in blockViews)
            {
                view.SnapTo(Coordinates.CellToWorld(model.GetBlock(view.BlockId).Anchor));
                view.SetSelected(false);
            }
        }
    }
}
