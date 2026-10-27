using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class AutoGridCellSize : MonoBehaviour
{
    [Header("Grid Settings")]
    private int columns = 6;

    [Tooltip("Width divided by height. Use 1 for square cells.")]
    [SerializeField]
    [Min(0.01f)]
    private float aspectRatio = 1f;

    [Header("Minimum Cell Size")]

    [SerializeField]
    [Min(0f)]
    private float minimumCellWidth = 0f;

    [SerializeField]
    [Min(0f)]
    private float minimumCellHeight = 0f;

    private GridLayoutGroup grid;
    private RectTransform rectTransform;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        UpdateCellSize();
    }

    private void OnValidate()
    {
        columns = Mathf.Max(1, columns);
        aspectRatio = Mathf.Max(0.01f, aspectRatio);

        Initialize();
        UpdateCellSize();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateCellSize();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        UpdateCellSize();
    }

    private void OnTransformChildrenChanged()
    {
        UpdateCellSize();
    }

    private void Initialize()
    {
        if (grid == null)
        {
            grid = GetComponent<GridLayoutGroup>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void UpdateCellSize()
    {
        if (grid == null || rectTransform == null)
        {
            return;
        }

        int childCount = rectTransform.childCount;

        if (childCount == 0)
        {
            return;
        }

        int activeColumns = 3;

        int rows = Mathf.CeilToInt(
            (float)childCount / activeColumns
        );

        grid.constraint =
            GridLayoutGroup.Constraint.FixedColumnCount;

        grid.constraintCount = activeColumns;

        float availableWidth =
            rectTransform.rect.width
            - grid.padding.left
            - grid.padding.right
            - grid.spacing.x * (activeColumns - 1);

        float availableHeight =
            rectTransform.rect.height
            - grid.padding.top
            - grid.padding.bottom
            - grid.spacing.y * (rows - 1);

        availableWidth = Mathf.Max(0f, availableWidth);
        availableHeight = Mathf.Max(0f, availableHeight);

        float maxCellWidth =
            availableWidth / activeColumns;

        float maxCellHeight =
            availableHeight / rows;

        /*
         * Keep the requested aspect ratio:
         *
         * aspectRatio = width / height
         *
         * For square cells, aspectRatio = 1.
         */
        float cellWidth = Mathf.Min(
            maxCellWidth,
            maxCellHeight * aspectRatio
        );

        float cellHeight =
            cellWidth / aspectRatio;

        /*
         * Apply the minimum size.
         *
         * Note: if the minimum size is larger than the
         * available space, the grid may overflow its parent.
         */
        cellWidth = Mathf.Max(
            cellWidth,
            minimumCellWidth
        );

        cellHeight = Mathf.Max(
            cellHeight,
            minimumCellHeight
        );

        /*
         * Reapply the aspect ratio after minimum limits.
         */
        if (cellWidth / cellHeight < aspectRatio)
        {
            cellWidth = cellHeight * aspectRatio;
        }
        else
        {
            cellHeight = cellWidth / aspectRatio;
        }

        grid.cellSize = new Vector2(
            cellWidth,
            cellHeight
        );
    }
}