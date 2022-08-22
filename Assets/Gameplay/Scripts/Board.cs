using System;
using System.Collections.Generic;
using Gameplay.Scripts.DataStructures;
using UnityEngine;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using int2 = Unity.Mathematics.int2;
using Random = Unity.Mathematics.Random;

namespace Gameplay.Scripts
{
public struct Piece
{
    public enum Type : int
    {
        Empty, Block, ExplosO, ExplosI,
        Triangle, Star
    }

    public Type type;
    public int variant;

    public Piece(Type type, int variant) => (this.type, this.variant) = (type, variant);

    public static bool operator ==(Piece c1, Piece c2) => c1.type == c2.type && c1.variant == c2.variant;
    public static bool operator !=(Piece c1, Piece c2) => !(c1 == c2);
    public bool Equals(Piece other) => type == other.type && variant == other.variant;
    public override bool Equals(object obj) => obj is Piece other && Equals(other);
    public override int GetHashCode() => HashCode.Combine((int)type, variant);

    public override string ToString() => $"{type}:{variant.ToString()}";
}

public readonly struct ClusterSolver
{
    public struct Node
    {
        public int2 grid;
        public int2 parentGrid;

        public Node(int2 grid, int2 parentGrid)
        {
            this.grid = grid;
            this.parentGrid = parentGrid;
        }

        public bool IsRoot => parentGrid.x == -1;

        public override string ToString() =>
            $"({grid.x},{grid.y})>({(IsRoot ? "---" : $"{parentGrid.x},{parentGrid.y}")})";
    }

    readonly int2 size;
    public readonly Array2D<Node> Nodes;
    public readonly Array2D<(int2 group, int size)> Clusters;

    public ClusterSolver(int2 boardSize)
    {
        size = boardSize;
        Nodes = new(size);
        Clusters = new(size);
    }

    int2 GetRoot(Node node)
    {
        while (!node.IsRoot)
            node = Nodes[node.parentGrid.x, node.parentGrid.y];
        return node.grid;
    }

    void AddChild(int2 parent, int2 node)
    {
        if (!node.Equals(parent))
            Nodes[node.x, node.y].parentGrid = parent;
    }

    public void Solve(Array2D<Piece> pieces)
    {
        // clear state
        for (var x = 0; x < size.x; x++)
        for (var y = 0; y < size.y; y++)
        {
            Nodes[x, y] = new(int2(x, y), int2(-1));
        }

        // bottom row
        for (var x = 1; x < size.x; x++)
        {
            if (pieces[x, 0] == pieces[x - 1, 0]) AddChild(int2(x, 0), GetRoot(Nodes[x - 1, 0]));
        }

        // left column
        for (var y = 1; y < size.y; y++)
        {
            if (pieces[0, y] == pieces[0, y - 1]) AddChild(int2(0, y), GetRoot(Nodes[0, y - 1]));
        }

        // the rest
        for (var x = 1; x < size.x; x++)
        for (var y = 1; y < size.y; y++)
        {
            if (pieces[x, y] == pieces[x - 1, y]) AddChild(int2(x, y), GetRoot(Nodes[x - 1, y]));
            if (pieces[x, y] == pieces[x, y - 1]) AddChild(int2(x, y), GetRoot(Nodes[x, y - 1]));
        }

        // Bake the linked nodes into groups
        for (var i = 0; i < Clusters.Length; i++)
            Clusters[i] = (GetRoot(Nodes[i]), 0);

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

    public float cellSize = 1.6f;

    [SerializeField, HideInInspector] Transform _T;
    [SerializeField, HideInInspector] Camera _Cam;
    [SerializeField, HideInInspector] BackBoard backBoard;
    Random random = new(1235);

    [NonSerialized] public Array2D<Piece> Pieces;
    [NonSerialized] public Array2D<Transform> Transforms;
    [NonSerialized] public Array2D<SpriteRenderer> Icons;
    [NonSerialized] public Array2D<SpriteRenderer> Bases;

    // Pooling
    [NonSerialized] Stack<(Transform, SpriteRenderer, SpriteRenderer)> pool;
    (Transform, SpriteRenderer, SpriteRenderer) GetFromPool() => pool.Pop();
    void ReturnToPool(int2 grid) => pool.Push((Transforms[grid], Bases[grid], Icons[grid]));
    void ReturnToPool(int x) => pool.Push((Transforms[x], Bases[x], Icons[x]));

    // Space transforms
    public float2 World2Local(float2 worldPos) => worldPos - float3(_T.position).xy;
    public int2 World2Grid(float2 worldPos) => int2(round(World2Local(worldPos) / cellSize));

    public float2 Local2World(float2 localPos) => float3(_T.position).xy + localPos;
    public float2 Grid2World(int2 gridPos) => Local2World(float2(gridPos) * cellSize);
    public float2 Idx2World(int idx) => Grid2World(Array2DUtility.Idx2Grid(idx, level.size.y));

    void OnValidate() // not Awake because custom inspector use the space transform methods that require _T
    {
        _T = transform;
        _Cam = Camera.main;
        backBoard = GetComponentInChildren<BackBoard>();
    }

    void Start()
    {
        Pieces = new(level.size);
        Transforms = new(level.size);
        Icons = new(level.size);
        Bases = new(level.size);

        pool = new(level.size.x * level.size.y);
        for (var i = 0; i < Transforms.Length; i++)
        {
            var blockTransform = Transforms[i] = Instantiate(blockPrefab, _T).transform;
            Icons[i] = blockTransform.Find("icon").GetComponent<SpriteRenderer>();
            Bases[i] = blockTransform.Find("base").GetComponent<SpriteRenderer>();
            ReturnToPool(i);
        }

        for (var x = 0; x < level.size.x; x++)
        for (var y = 0; y < level.size.y; y++)
        {
            CreateBlock(int2(x, y));
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
            if (MousePrevious.isIn) Transforms[MousePrevious.grid].localScale = float3(1f);
            if (MouseCurrent.isIn) Transforms[MouseCurrent.grid].localScale = float3(1.2f);
        }

        if (Input.GetMouseButtonDown(0) && MouseCurrent.isIn)
            MouseDownGrid = MouseCurrent.grid;

        if (Input.GetMouseButtonUp(0) && MouseCurrent.grid.Equals(MouseDownGrid) &&
            Pieces[MouseCurrent.grid].type != Piece.Type.Empty)
        {
            Debug.Log($"MouseAction on grid: {MouseCurrent.grid} {Pieces[MouseCurrent.grid]}");

            if (Pieces[MouseCurrent.grid] is { type: Piece.Type.Block, variant: var subType })
                backBoard.React(level.blockTypes[subType].blockColor);

            // Remove the cluster
            var cluster = clusterSolver.Clusters[MouseCurrent.grid];
            for (var i = 0; i < clusterSolver.Clusters.Length; i++)
                if (clusterSolver.Clusters[i].group.Equals(cluster.group))
                {
                    Pieces[i].type = Piece.Type.Empty;
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
                    if (Pieces[x, y].type == Piece.Type.Empty)
                    {
                        gap++;
                    }
                    else if (gap > 0)
                    {
                        Transforms[x, y].Translate(0, -1 * gap * cellSize, 0);
                        Transforms[x, y - gap] = Transforms[x, y];
                        Pieces[x, y - gap] = Pieces[x, y];
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
        Pieces[MouseCurrent.grid] = powerLevel switch
        {
            0 => new(Piece.Type.Triangle, default),
            1 => new(Piece.Type.ExplosI, random.NextInt(0, 2)),
            2 => new(Piece.Type.ExplosO, default),
            3 => new(Piece.Type.Star, default),
        };
        var (t, b, i) = (Transforms[grid], Bases[grid], Icons[grid]) = GetFromPool();

        t.gameObject.SetActive(true);
        t.position = float3(Grid2World(MouseCurrent.grid), 0);
        t.localScale = float3(1);

        b.enabled = true;
        b.sprite = powerIcons[powerLevel];
        b.color = Color.white;

        i.enabled = false;
    }

    void CreateBlock(int2 grid)
    {
        var piece = Pieces[grid] = new(Piece.Type.Block, random.NextInt(level.blockTypes.Length));
        var blockType = level.blockTypes[piece.variant];
        var (t, b, i) = (Transforms[grid], Bases[grid], Icons[grid]) = GetFromPool();

        t.gameObject.SetActive(true);
        t.position = float3(Grid2World(int2(grid)), 0);
        t.localScale = float3(1);

        b.enabled = true;
        b.sprite = level.block;
        b.color = blockType.blockColor.color;

        i.enabled = true;
        i.sprite = blockType.icon;
        i.color = blockType.blockColor.Darker;
    }

    void UpdateGroups()
    {
        clusterSolver.Solve(Pieces);

        // Set blocks' power hints
        for (var i = 0; i < Pieces.Length; i++)
            if (Pieces[i] is { type: Piece.Type.Block, variant: var variant })
                Icons[i].sprite = (clusterSolver.Clusters[i].size / 3) switch // TODO(bekorn): rules
                {
                    0 => level.blockTypes[variant].icon,
                    var powerType => powerHints[clamp(powerType - 1, 0, powerHints.Length - 1)], // TODO(bekorn): rules
                };
    }
}
}