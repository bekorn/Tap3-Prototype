using System;
using System.Collections.Generic;
using System.Text;
using Gameplay.Scripts.DataStructures;
using UnityEngine;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using int2 = Unity.Mathematics.int2;
using Random = Unity.Mathematics.Random;

namespace Gameplay.Scripts
{
public enum ItemType : int { Empty, Block, ExplosO, ExplosI, Triangle, Star }

public struct Cell
{
    public ItemType Type;
    public int SubType;

    public Cell(ItemType type, int subType)
    {
        Type = type;
        SubType = subType;
    }

    public static bool operator ==(Cell c1, Cell c2) => c1.Type == c2.Type && c1.SubType == c2.SubType;
    public static bool operator !=(Cell c1, Cell c2) => !(c1 == c2);

    public override string ToString() => $"{Type}:{SubType.ToString()}";
}

public readonly struct ClusterSolver
{
    struct Node
    {
        public int2 grid;
        public int2 parentGrid;

        public Node(int2 grid, int2 parentGrid)
        {
            this.grid = grid;
            this.parentGrid = parentGrid;
        }

        public bool IsRoot => parentGrid.x == -1;

        public override string ToString() => $"({grid.x},{grid.y})>({(IsRoot ? "---" : $"{parentGrid.x},{parentGrid.y}")})";
    }

    readonly int2 size;
    readonly Array2D<Node> nodes;
    public readonly Array2D<(int2 group, int size)> Clusters;

    public ClusterSolver(int2 boardSize)
    {
        size = boardSize;
        nodes = new(size);
        Clusters = new(size);
    }

    void PrintNodes(Array2D<Cell> cells, int _x, int _y)
    {
        var nodesStr = new StringBuilder($"step({_x}, {_y})\n");
        for (var x = size.x - 1; x >= 0; x--)
        {
            for (var y = 0; y < size.y; y++)
                nodesStr.Append($"{cells[x, y]}{nodes[x, y]} \t");

            nodesStr.Append("\n");
        }

        var str = nodesStr.ToString();
        Debug.Log(str);
    }

    int2 GetRoot(Node node)
    {
        while (!node.IsRoot)
            node = nodes[node.parentGrid.x, node.parentGrid.y];
        return node.grid;
    }

    void AddChild(int2 parent, int2 node)
    {
        if (!node.Equals(parent))
            nodes[node.x, node.y].parentGrid = parent;
    }

    public void Solve(Array2D<Cell> cells)
    {
        // clear state
        for (var x = 0; x < size.x; x++)
        for (var y = 0; y < size.y; y++)
        {
            nodes[x, y] = new(int2(x, y), int2(-1));
        }

        // bottom-left corner
        //PrintNodes(cells, 0, 0);

        // bottom row
        for (var x = 1; x < size.x; x++)
        {
            if (cells[x, 0] == cells[x - 1, 0]) AddChild(int2(x, 0), GetRoot(nodes[x - 1, 0]));
        }
        //PrintNodes(cells, 1, 0);

        // left column
        for (var y = 1; y < size.y; y++)
        {
            if (cells[0, y] == cells[0, y - 1]) AddChild(int2(0, y), GetRoot(nodes[0, y - 1]));
        }
        //PrintNodes(cells, 0, 1);

        // the rest
        for (var x = 1; x < size.x; x++)
        for (var y = 1; y < size.y; y++)
        {
            if (cells[x, y] == cells[x - 1, y]) AddChild(int2(x, y), GetRoot(nodes[x - 1, y]));
            if (cells[x, y] == cells[x, y - 1]) AddChild(int2(x, y), GetRoot(nodes[x, y - 1]));

            //PrintNodes(cells, x, y);
        }

        // Bake the linked nodes into groups
        for (var i = 0; i < Clusters.Length; i++)
            Clusters[i] = (GetRoot(nodes[i]), 0);

        // Sum the clusters
        foreach (ref var cluster in Clusters)
            Clusters[cluster.group].size++;

        // Spread the sum
        foreach (ref var node in Clusters)
            node.size = Clusters[node.group].size;
    }
}

public class Board : MonoBehaviour
{
    [SerializeField] public Level level;
    [SerializeField] public GameObject blockPrefab;
    [SerializeField] public Sprite[] powerHints;
    [SerializeField] public Sprite[] powerIcons;

    public float gridSize = 1.6f;

    [HideInInspector][SerializeField] Transform _T;
    [HideInInspector][SerializeField] Camera _Cam;
    [HideInInspector][SerializeField] BackBoard backBoard;
    Random random = new(1235);
    [NonSerialized] public Array2D<Cell> Cells;
    [NonSerialized] public Array2D<Transform> Transforms;
    [NonSerialized] public Array2D<SpriteRenderer> Icons;
    [NonSerialized] public Array2D<SpriteRenderer> Bases;

    // Pooling
    [NonSerialized] Stack<(Transform, SpriteRenderer, SpriteRenderer)> pool;
    (Transform, SpriteRenderer, SpriteRenderer) GetFromPool() => pool.Pop();
    void ReturnToPool(int2 grid) => pool.Push((Transforms[grid], Icons[grid], Bases[grid]));
    void ReturnToPool(int x) => pool.Push((Transforms[x], Icons[x], Bases[x]));

    // Space transforms
    public float2 World2Local(float2 worldPos) => worldPos - float3(_T.position).xy;
    public int2 World2Grid(float2 worldPos) => int2(round(World2Local(worldPos) / gridSize));

    public float2 Local2World(float2 localPos) => float3(_T.position).xy + localPos;
    public float2 Grid2World(int2 gridPos) => Local2World(float2(gridPos) * gridSize);

    void OnValidate() // not Awake because custom inspector use the space transform methods that require _T
    {
        _T = transform;
        _Cam = Camera.main;
        backBoard = GetComponentInChildren<BackBoard>();
    }

    void Start()
    {
        Cells = new(level.size);
        Transforms = new(level.size);
        Icons = new(level.size);
        Bases = new(level.size);

        pool = new(level.size.x * level.size.y);

        float3 pos = _T.position;

        for (var x = 0; x < level.size.x; x++)
        for (var y = 0; y < level.size.y; y++)
        {
            var cell = Cells[x, y] = RandomBlock();

            var blockTransform = Transforms[x, y] = Instantiate(
                blockPrefab,
                pos + float3(gridSize * float2(x, y), 0),
                Quaternion.identity,
                _T
            ).transform;
            var icon = Icons[x, y] = blockTransform.Find("icon").GetComponent<SpriteRenderer>();
            var block = Bases[x, y] = blockTransform.Find("block").GetComponent<SpriteRenderer>();

            var color = level.colors[cell.SubType];
            block.color = color;
            icon.color = Color.Lerp(color, Color.black, 0.1f);
            icon.sprite = level.icons[cell.SubType];
        }

        clusterSolver = new ClusterSolver(level.size);
        UpdateGroups();
    }

    public ClusterSolver clusterSolver;

    // input state
    [NonSerialized] public (int2 grid, bool isIn) MousePrevious;
    [NonSerialized] public (int2 grid, bool isIn) MouseCurrent;
    [NonSerialized] public bool IsMouseChanged;
    [NonSerialized] public int2 MouseDownGrid;

    void Update()
    {
        // Update input state
        MouseCurrent.grid = World2Grid(float3(_Cam.ScreenToWorldPoint(Input.mousePosition)).xy);
        MouseCurrent.isIn = all(0 <= MouseCurrent.grid) && all(MouseCurrent.grid < level.size);
        IsMouseChanged = !MouseCurrent.Equals(MousePrevious);

        if (IsMouseChanged)
        {
            if (MousePrevious.isIn)
                Transforms[MousePrevious.grid].localScale = float3(1f);

            if (MouseCurrent.isIn)
                Transforms[MouseCurrent.grid].localScale = float3(1.2f);
        }

        if (Input.GetMouseButtonDown(0) && MouseCurrent.isIn)
            MouseDownGrid = MouseCurrent.grid;

        if (Input.GetMouseButtonUp(0) && MouseCurrent.grid.Equals(MouseDownGrid) && Cells[MouseCurrent.grid].Type != ItemType.Empty)
        {
            Debug.Log($"MouseAction on grid: {MouseCurrent.grid} {Cells[MouseCurrent.grid]}");
            
            if (Cells[MouseCurrent.grid] is { Type: ItemType.Block, SubType: var subType })
                backBoard.React(level.colors[subType]);

            // Remove the cluster
            var cluster = clusterSolver.Clusters[MouseCurrent.grid];
            for (var i = 0; i < clusterSolver.Clusters.Length; i++)
                if (clusterSolver.Clusters[i].group.Equals(cluster.group))
                {
                    Cells[i].Type = ItemType.Empty;
                    Transforms[i].gameObject.SetActive(false);
                    ReturnToPool(i);
                }

            // Create a power
            var powerLevel = cluster.size / 3 - 1;
            if (powerLevel >= 0)
                CreatePower(MouseCurrent.grid, min(powerLevel, powerIcons.Length - 1)); // TODO(bekorn): rules

            // Update by columns
            for (var x = 0; x < level.size.x; x++)
            {
                // Make them fall
                var gap = 0;
                for (var y = 0; y < level.size.y; y++)
                {
                    if (Cells[x, y].Type == ItemType.Empty)
                    {
                        gap++;
                    }
                    else if (gap > 0)
                    {
                        Transforms[x, y].Translate(0, -1 * gap * gridSize, 0);
                        Transforms[x, y - gap] = Transforms[x, y];
                        Cells[x, y - gap] = Cells[x, y];
                        Icons[x, y - gap] = Icons[x, y];
                        Bases[x, y - gap] = Bases[x, y];
                    }
                }

                // Spawn new blocks
                for (var y = level.size.y - gap; y < level.size.y; y++)
                    CreateBlock(int2(x, y));
            }

            UpdateGroups();
        }

        MousePrevious = MouseCurrent;
    }

    void CreatePower(int2 grid, int powerLevel)
    {
        Cells[MouseCurrent.grid] = powerLevel switch
        {
            0 => new(ItemType.Triangle, default),
            1 => new(ItemType.ExplosI, random.NextInt(0, 2)),
            2 => new(ItemType.ExplosO, default),
            3 => new(ItemType.Star, default),
        };

        var (t, i, b) = (Transforms[grid], Icons[grid], Bases[grid]) = GetFromPool();
        t.gameObject.SetActive(true);
        t.position = float3(Grid2World(MouseCurrent.grid), 0);
        i.sprite = powerIcons[powerLevel];
        b.enabled = false;
    }

    Cell RandomBlock() => new(ItemType.Block, random.NextInt(level.colors.Count));

    void CreateBlock(int2 grid)
    {
        var (t, i, b) = (Transforms[grid], Icons[grid], Bases[grid]) = GetFromPool();
        t.gameObject.SetActive(true);
        t.position = float3(Grid2World(int2(grid)), 0);

        var cell = Cells[grid] = RandomBlock();
        var color = level.colors[cell.SubType];
        b.color = color;
        b.enabled = true;
        i.color = Color.Lerp(color, Color.black, 0.1f); // TODO(bekorn): precompute in level
        i.sprite = level.icons[cell.SubType];
    }

    void UpdateGroups()
    {
        clusterSolver.Solve(Cells);

        // Set blocks' power hints
        for (var i = 0; i < Cells.Length; i++)
            if (Cells[i].Type == ItemType.Block)
                Icons[i].sprite = (clusterSolver.Clusters[i].size / 3) switch // TODO(bekorn): rules
                {
                    0 => level.icons[Cells[i].SubType],
                    var powerType => powerHints[clamp(powerType - 1, 0, powerHints.Length - 1)], // TODO(bekorn): rules
                };
    }
}
}