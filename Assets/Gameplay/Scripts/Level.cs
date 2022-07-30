using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
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

struct ClusterSolver
{
    class Node
    {
        public int2 grid; // debug only
        public Node parent = null;

        public bool IsRoot => parent == null;

        public Node GetRoot()
        {
            var node = this;
            while (!node.IsRoot)
                node = node.parent;
            return node;
        }

        public void AddChild(Node node)
        {
            if (node != this)
                node.parent = this;
        }

        public override string ToString() => $"({grid.x},{grid.y})>({(parent != null ? $"{grid.x},{grid.y}" : "---")})";
    }

    Node[,] nodes;
    Cell[,] cells;

    public ClusterSolver(int2 boardSize)
    {
        nodes = new Node[boardSize.x, boardSize.y];
        cells = new Cell[boardSize.x, boardSize.y];
    }

    void PrintNodes(int _x, int _y)
    {
        var nodesStr = new StringBuilder($"step({_x}, {_y})\n");
        for (var x = nodes.GetLength(0) - 1; x >= 0; x--)
        {
            for (var y = 0; y < nodes.GetLength(1); y++)
                nodesStr.Append($"{cells[x, y]}{nodes[x, y]} \t");

            nodesStr.Append("\n");
        }

        var str = nodesStr.ToString();
        Debug.Log(str);
    }

    public void Solve(Cell[,] levelCells)
    {
        // clear state
        for (var x = 0; x < nodes.GetLength(0); x++)
        for (var y = 0; y < nodes.GetLength(1); y++)
        {
            nodes[x, y] = new() { parent = null, grid = int2(x, y) };
            cells[x, y] = new() { Type = ItemType.Empty };
        }

        // bottom-left corner
        cells[0, 0] = levelCells[0, 0];
        PrintNodes(0, 0);

        // bottom row
        for (var x = 1; x < cells.GetLength(0); x++)
        {
            cells[x, 0] = levelCells[x, 0];
            if (cells[x, 0] == cells[x - 1, 0]) nodes[x, 0].AddChild(nodes[x - 1, 0].GetRoot());
        }
        PrintNodes(1, 0);
        
        // left column
        for (var y = 1; y < cells.GetLength(1); y++)
        {
            cells[0, y] = levelCells[0, y];
            if (cells[0, y] == cells[0, y - 1]) nodes[0, y].AddChild(nodes[0, y - 1].GetRoot());
        }
        PrintNodes(0, 1);

        // the rest
        for (var x = 1; x < cells.GetLength(0); x++)
        for (var y = 1; y < cells.GetLength(1); y++)
        {
            cells[x, y] = levelCells[x, y];
            if (cells[x, y] == cells[x - 1, y]) nodes[x, y].AddChild(nodes[x - 1, y].GetRoot());
            if (cells[x, y] == cells[x, y - 1]) nodes[x, y].AddChild(nodes[x, y - 1].GetRoot());

            PrintNodes(x, y);
        }

        var clusters = new int[nodes.GetLength(0), nodes.GetLength(1)];
        
    }
}

public class Level : MonoBehaviour
{
    public LevelConfig levelConfig;
    public GameObject blockPrefab;

    public float gridSize = 1.6f;

    Transform t;
    Random random = new(1235);
    [NonSerialized] public Cell[,] cells;
    [NonSerialized] public int[,] groups;

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
        cells = new Cell[levelConfig.size.x, levelConfig.size.y];
        groups = new int[levelConfig.size.x, levelConfig.size.y];

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