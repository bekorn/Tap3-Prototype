using System;
using Gameplay.Scripts;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Gameplay.Editor
{
[CustomEditor(typeof(Board))]
public class LevelInspector : UnityEditor.Editor
{
    enum DebugLayer { None, Grid, Cells, Groups }

    static DebugLayer debugLayer = DebugLayer.None;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Separator();
        debugLayer = (DebugLayer)EditorGUILayout.EnumPopup("Debug", debugLayer);
    }

    [DrawGizmo(GizmoType.Selected)]
    static void DrawGizmos(Board board, GizmoType gizmoType)
    {
        // TODO(bekorn): stylize the handles to improve readability
        
        switch (debugLayer)
        {
            case DebugLayer.None:
                break;
            case DebugLayer.Grid:
                if (board.level != null)
                {
                    for (var x = 0; x < board.level.size.x; x++)
                    for (var y = 0; y < board.level.size.y; y++)
                        Gizmos.DrawSphere(float3(board.Grid2World(int2(x, y)), 0), 0.1f);

                    var bl = float3(board.Grid2World(int2(0, 0)) - 0.5f * board.gridSize, 0);
                    var tr = float3(board.Grid2World(board.level.size) - 0.5f * board.gridSize, 0);
                    for (var x = 0; x <= board.level.size.x; x++) // vertical lines
                        Gizmos.DrawLine(float3(bl.x + x * board.gridSize, bl.y, 0), float3(bl.x + x * board.gridSize, tr.y, 0));
                    for (var y = 0; y <= board.level.size.y; y++) // horizontal lines
                        Gizmos.DrawLine(float3(bl.x, bl.y + y * board.gridSize, 0), float3(tr.x, bl.y + y * board.gridSize, 0));
                }
                break;

            case DebugLayer.Cells:
                if (board.Cells.IsInit)
                    for (var x = 0; x < board.level.size.x; x++)
                    for (var y = 0; y < board.level.size.y; y++)
                        Handles.Label(
                            float3(board.Grid2World(int2(x, y)), 0),
                            $"{board.Cells[x, y].Type}|{board.Cells[x, y].SubType}]"
                        );
                break;
            case DebugLayer.Groups:
                if (board.clusterSolver.Clusters.IsInit)
                {
                    for (var x = 0; x < board.level.size.x; x++)
                    for (var y = 0; y < board.level.size.y; y++)
                        Handles.Label(
                            float3(board.Grid2World(int2(x, y)), 0), 
                            board.clusterSolver.Clusters[int2(x, y)].ToString()
                        );
                }
                break;
        }
    }
}
}