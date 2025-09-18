using System.Collections;
using System.Collections.Generic;
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

    private bool[] columnBusy;

    void Awake()
    {
        grid = new Block[width, height];
        columnBusy = new bool[width];
    }

    void Start()
    {
        if (fillAtStart)
        {
            GenerateStart();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DestroyAt(0, 0);
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

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
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
    #region Move Operations
    private class MoveOp
    {
        public Block block;
        public int toY;
        public Vector3 fromPos;
        public Vector3 toPos;
    }

    private void EnsureColumnLockAllocated()
    {
        if (columnBusy == null || columnBusy.Length != width)
        {
            columnBusy = new bool[width];
        }
    }

    public void DestroyAt(int x, int y, float moveDuration = 0.12f)
    {
        StartCoroutine(DestroyAndCollapseColumn(x, y, moveDuration));
    }

    public void DestroyBlock(Block b, float moveDuration = 0.12f)
    {
        if (b == null) return;
        StartCoroutine(DestroyAndCollapseColumn(b.X, b.Y, moveDuration));
    }

    private IEnumerator DestroyAndCollapseColumn(int x, int y, float duration)
    {
        if (!InBounds(x, y))
        {
            yield break;
        }

        EnsureColumnLockAllocated();

        if (columnBusy[x])
        {
            yield break;
        }

        columnBusy[x] = true;

        Block victim = grid[x, y];
        if (victim != null)
        {
            grid[x, y] = null;
            Destroy(victim.gameObject);
            yield return null;
        }

        List<(Block block, int toY, Vector3 fromPos, Vector3 toPos)> moves =
            new List<(Block, int, Vector3, Vector3)>();

        int nextFillRowIndex = y;
        for (int sourceRowIndex = y + 1; sourceRowIndex < height; sourceRowIndex++)
        {
            Block b = grid[x, sourceRowIndex];
            if (b == null)
            {
                continue;
            }

            if (sourceRowIndex != nextFillRowIndex)
            {
                Vector3 from = b.transform.position;
                Vector3 to = CellToWorld(x, nextFillRowIndex);
                moves.Add((b, nextFillRowIndex, from, to));
            }

            nextFillRowIndex++;
        }

        for (int sourceRowIndex = y + 1; sourceRowIndex < height; sourceRowIndex++)
        {
            grid[x, sourceRowIndex] = null;
        }

        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            grid[x, m.toY] = m.block;
        }

        float t = 0f;
        while (t < duration && moves.Count > 0)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            a = a * a * (3f - 2f * a);

            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                m.block.transform.position = Vector3.Lerp(m.fromPos, m.toPos, a);
            }

            yield return null;
        }

        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            m.block.transform.position = m.toPos;
            m.block.SetCoords(x, m.toY);
        }

        columnBusy[x] = false;
        yield break;
    }
    #endregion

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
