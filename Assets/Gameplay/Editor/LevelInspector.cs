using System;
using Gameplay.Scripts;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Gameplay.Editor
{
[CustomEditor(typeof(Level))]
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
    static void DrawGizmos(Level level, GizmoType gizmoType)
    {
        // TODO(bekorn): stylize the handles to improve readability
        
        switch (debugLayer)
        {
            case DebugLayer.None:
                break;
            case DebugLayer.Grid:
                if (level.levelConfig != null)
                {
                    for (var x = 0; x < level.levelConfig.size.x; x++)
                    for (var y = 0; y < level.levelConfig.size.y; y++)
                        Gizmos.DrawSphere(float3(level.Grid2World(int2(x, y)), 0), 0.1f);

                    var bl = float3(level.Grid2World(int2(0, 0)) - 0.5f * level.gridSize, 0);
                    var tr = float3(level.Grid2World(level.levelConfig.size) - 0.5f * level.gridSize, 0);
                    for (var x = 0; x <= level.levelConfig.size.x; x++) // vertical lines
                        Gizmos.DrawLine(float3(bl.x + x * level.gridSize, bl.y, 0), float3(bl.x + x * level.gridSize, tr.y, 0));
                    for (var y = 0; y <= level.levelConfig.size.y; y++) // horizontal lines
                        Gizmos.DrawLine(float3(bl.x, bl.y + y * level.gridSize, 0), float3(tr.x, bl.y + y * level.gridSize, 0));
                }
                break;

            case DebugLayer.Cells:
                if (level.Cells.array != null)
                    for (var x = 0; x < level.levelConfig.size.x; x++)
                    for (var y = 0; y < level.levelConfig.size.y; y++)
                        Handles.Label(
                            float3(level.Grid2World(int2(x, y)), 0),
                            $"{level.Cells[x, y].Type}|{level.Cells[x, y].SubType}]"
                        );
                break;
            case DebugLayer.Groups:
                if (level.clusterSolver.Groups.array != null)
                {
                    for (var x = 0; x < level.levelConfig.size.x; x++)
                    for (var y = 0; y < level.levelConfig.size.y; y++)
                        Handles.Label(
                            float3(level.Grid2World(int2(x, y)), 0), 
                            level.clusterSolver.Groups[int2(x, y)].ToString()
                        );
                }
                break;
        }
    }
}
}