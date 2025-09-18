using UnityEngine;

public class Board : MonoBehaviour
{
    [Header("Grid")]
    public int width = 9;
    public int height = 12;
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero; // bottom-left of the grid in world space

    [Header("Blocks")]
    public Block blockPrefab;
    public Material[] colorMats;

    [Header("Initial Fill")]
    public bool fillAtStart = true;
    public int randomSeed = 0;                    

    [HideInInspector] public Block[,] grid;

    void Awake()
    {
        grid = new Block[width, height];
    }

    void Start()
    {
        if (fillAtStart)
        {
            GenerateStart();
        }
    }

    public void GenerateStart()
    {
        ClearAll();
        if (randomSeed != 0) Random.InitState(randomSeed);

        // Fill from y=0 upwards
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                BlockColor color = (BlockColor)Random.Range(0, Mathf.Min(colorMats.Length, System.Enum.GetValues(typeof(BlockColor)).Length));
                SpawnBlock(x, y, color);
            }
        }
    }

    public void ClearAll()
    {
        if (grid == null)
        {
            grid = new Block[width, height];
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    DestroyImmediate(grid[x, y].gameObject);
                }
                grid[x, y] = null;
            }
        }
    }

    public Vector3 CellToWorld(int x, int y)
    {
        return origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
    }

    public Block SpawnBlock(int x, int y, BlockColor color)
    {
        Block block = Instantiate(blockPrefab, CellToWorld(x, y), Quaternion.identity, transform);
        block.Init(x, y, color, colorMats);
        grid[x, y] = block;
        return block;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;

        // verticals
        for (int x = 0; x <= width; x++)
        {
            Vector3 a = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 b = origin + new Vector3(x * cellSize, height * cellSize, 0);
            Gizmos.DrawLine(a, b);
        }
        // horizontals
        for (int y = 0; y <= height; y++)
        {
            Vector3 a = origin + new Vector3(0, y * cellSize, 0);
            Vector3 b = origin + new Vector3(width * cellSize, y * cellSize, 0);
            Gizmos.DrawLine(a, b);
        }
    }
}
