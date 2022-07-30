using System;
using System.Collections.Generic;
using System.Text;
using Gameplay.Scripts.DataStructures;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
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

readonly struct ClusterSolver
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

        public bool Equals(Node other) => grid.Equals(other.grid) && parentGrid.Equals(other.parentGrid);

        public override string ToString() => $"({grid.x},{grid.y})>({(IsRoot ? "---" : $"{parentGrid.x},{parentGrid.y}")})";
    }

    readonly int2 size;
    readonly Array2D<Node> nodes;
    readonly Array2D<int> groups;

    public ClusterSolver(int2 boardSize)
    {
        size = boardSize;
        nodes = new Array2D<Node>(size);
        groups = new Array2D<int>(size);
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

        var clusters = new int[size.x, size.y];


    }
}

public class Level : MonoBehaviour
{
    public LevelConfig levelConfig;
    public GameObject blockPrefab;

    public float gridSize = 1.6f;

    Transform t;
    Random random = new(1235);
    public Array2D<Cell> cells;

    // Space transforms
    public float2 World2Local(float2 worldPos) => float3(t.position).xy - worldPos;
    public int2 World2Grid(float3 worldPos) => int2(World2Local(worldPos.xy) / gridSize);

    public float2 Local2World(float2 localPos) => float3(t.position).xy + localPos;
    public float2 Grid2World(int2 gridPos) => Local2World(float2(gridPos) * gridSize);

    void OnValidate()
    {
        t = transform;
    }

    void Start()
    {
        cells = new Array2D<Cell>(levelConfig.size);

        float3 pos = t.position;

        for (var x = 0; x < levelConfig.size.x; x++)
        for (var y = 0; y < levelConfig.size.y; y++)
        {
            var cell = cells[x, y] = RandomBlock();

            var blockTransform = Instantiate(
                blockPrefab,
                pos + float3(gridSize * float2(x, y), 0),
                Quaternion.identity,
                t
            ).transform;
            var icon = blockTransform.Find("icon").GetComponent<SpriteRenderer>();
            var block = blockTransform.Find("block").GetComponent<SpriteRenderer>();

            var color = levelConfig.colors[cell.SubType];
            block.color = color;
            icon.color = Color.Lerp(color, Color.black, 0.1f);
            icon.sprite = levelConfig.icons[cell.SubType];
        }

        clusterSolver = new ClusterSolver(levelConfig.size);
        UpdateGroups();
    }

    Cell RandomBlock() => new(ItemType.Block, random.NextInt(levelConfig.colors.Count));

    ClusterSolver clusterSolver;

    void UpdateGroups()
    {
        clusterSolver.Solve(cells);
    }
}
}