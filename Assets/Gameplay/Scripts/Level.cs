using System;
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
public enum ItemType : int { Empty, Block, ExplosO, ExplosI }

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
        return;
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
        PrintNodes(cells, 0, 0);

        // bottom row
        for (var x = 1; x < size.x; x++)
        {
            if (cells[x, 0] == cells[x - 1, 0]) AddChild(int2(x, 0), GetRoot(nodes[x - 1, 0]));
        }
        PrintNodes(cells, 1, 0);

        // left column
        for (var y = 1; y < size.y; y++)
        {
            if (cells[0, y] == cells[0, y - 1]) AddChild(int2(0, y), GetRoot(nodes[0, y - 1]));
        }
        PrintNodes(cells, 0, 1);

        // the rest
        for (var x = 1; x < size.x; x++)
        for (var y = 1; y < size.y; y++)
        {
            if (cells[x, y] == cells[x - 1, y]) AddChild(int2(x, y), GetRoot(nodes[x - 1, y]));
            if (cells[x, y] == cells[x, y - 1]) AddChild(int2(x, y), GetRoot(nodes[x, y - 1]));

            PrintNodes(cells, x, y);
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

public class Level : MonoBehaviour
{
    [SerializeField] public LevelConfig levelConfig;
    [SerializeField] public GameObject blockPrefab;
    [SerializeField] public Sprite[] powerUpIcons;

    public float gridSize = 1.6f;

    [SerializeField] Camera _Cam;
    Transform _T;
    Random random = new(1235);
    [NonSerialized] public Array2D<Cell> Cells;
    [NonSerialized] public Array2D<Transform> Transforms;
    [NonSerialized] public Array2D<SpriteRenderer> Icons;

    // Space transforms
    public float2 World2Local(float2 worldPos) => worldPos - float3(_T.position).xy;
    public int2 World2Grid(float2 worldPos) => int2(round(World2Local(worldPos) / gridSize));

    public float2 Local2World(float2 localPos) => float3(_T.position).xy + localPos;
    public float2 Grid2World(int2 gridPos) => Local2World(float2(gridPos) * gridSize);

    void OnValidate()
    {
        _T = transform;
        _Cam = Camera.main;
    }

    void Start()
    {
        Cells = new (levelConfig.size);
        Transforms = new (levelConfig.size);
        Icons = new (levelConfig.size);

        float3 pos = _T.position;

        for (var x = 0; x < levelConfig.size.x; x++)
        for (var y = 0; y < levelConfig.size.y; y++)
        {
            var cell = Cells[x, y] = RandomBlock();

            var blockTransform = Transforms[x, y] = Instantiate(
                blockPrefab,
                pos + float3(gridSize * float2(x, y), 0),
                Quaternion.identity,
                _T
            ).transform;
            var icon = Icons[x, y] = blockTransform.Find("icon").GetComponent<SpriteRenderer>();
            var block = blockTransform.Find("block").GetComponent<SpriteRenderer>();

            var color = levelConfig.colors[cell.SubType];
            block.color = color;
            icon.color = Color.Lerp(color, Color.black, 0.1f);
            icon.sprite = levelConfig.icons[cell.SubType];
        }

        clusterSolver = new ClusterSolver(levelConfig.size);
        UpdateGroups();
    }

    // TODO(bekorn): this will grow into random level generation
    Cell RandomBlock() => new(ItemType.Block, random.NextInt(levelConfig.colors.Count));

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
        MouseCurrent.isIn = all(0 <= MouseCurrent.grid) && all(MouseCurrent.grid < levelConfig.size);
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

            var clusterGrid = clusterSolver.Clusters[MouseCurrent.grid].group;
            for (var i = 0; i < clusterSolver.Clusters.Length; i++)
                if (clusterSolver.Clusters[i].group.Equals(clusterGrid))
                {
                    Transforms[i].gameObject.SetActive(false);
                    Cells[i].Type = ItemType.Empty;
                }
            
            UpdateGroups();
        }

        MousePrevious = MouseCurrent;
    }

    void UpdateGroups()
    {
        clusterSolver.Solve(Cells);
        
        for (var i = 0; i < Icons.Length; i++)
        {
            var type = clusterSolver.Clusters[i].size / 3;
            Icons[i].sprite = type switch
            {
                0 => levelConfig.icons[Cells[i].SubType],
                _ => powerUpIcons[type - 1],
            };
        }
    }
}
}