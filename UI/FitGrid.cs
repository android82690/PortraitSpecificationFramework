using UnityEngine;
using UnityEngine.UI;

namespace SpecificPortraits.UI
{
    /// <summary>
    /// Sizes a grid from the width it actually gets at runtime instead of fixed numbers:
    /// as many columns as fit a minimum cell width, cells stretched to fill the row,
    /// re-evaluated whenever the measured width changes (resolution, UI scale, resizing).
    /// </summary>
    public class FitGrid : MonoBehaviour
    {
        public GridLayoutGroup Grid;
        /// <summary>Whose width to fill. Usually the scroll viewport; otherwise the grid's parent.</summary>
        public RectTransform WidthSource;
        /// <summary>Smallest acceptable cell width; the column count follows from it. Ignored when FixedColumns is set.</summary>
        public float MinCellWidth = 120f;
        /// <summary>Column count to force, 0 to derive it from MinCellWidth.</summary>
        public int FixedColumns = 0;
        /// <summary>Cell height as a multiple of cell width. Ignored when FixedHeight is set.</summary>
        public float Aspect = 4f / 3f;
        /// <summary>Absolute cell height, 0 to use Aspect.</summary>
        public float FixedHeight = 0f;
        /// <summary>Added below the image, for cards that carry a caption.</summary>
        public float ExtraHeight = 0f;

        float lastWidth = -1f;

        public static FitGrid Attach(GridLayoutGroup grid, RectTransform widthSource)
        {
            var fit = grid.gameObject.AddComponent<FitGrid>();
            fit.Grid = grid;
            fit.WidthSource = widthSource != null ? widthSource : grid.transform.parent as RectTransform;
            return fit;
        }

        void LateUpdate()
        {
            if (Grid == null || WidthSource == null)
                return;
            float width = WidthSource.rect.width;
            if (width <= 0f || Mathf.Approximately(width, lastWidth))
                return;
            lastWidth = width;
            Apply(width);
        }

        void Apply(float width)
        {
            float available = width - Grid.padding.horizontal;
            // the scroll content usually carries its own padding around the grid
            var parentGroup = Grid.transform.parent != null ? Grid.transform.parent.GetComponent<LayoutGroup>() : null;
            if (parentGroup != null)
                available -= parentGroup.padding.horizontal;
            if (available <= 0f)
                return;

            float spacing = Grid.spacing.x;
            int columns = FixedColumns > 0
                ? FixedColumns
                : Mathf.Max(1, Mathf.FloorToInt((available + spacing) / (MinCellWidth + spacing)));
            float cellWidth = Mathf.Floor((available - (columns - 1) * spacing) / columns);
            float cellHeight = (FixedHeight > 0f ? FixedHeight : Mathf.Floor(cellWidth * Aspect)) + ExtraHeight;

            Grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            Grid.constraintCount = columns;
            Grid.cellSize = new Vector2(cellWidth, cellHeight);
        }
    }
}
