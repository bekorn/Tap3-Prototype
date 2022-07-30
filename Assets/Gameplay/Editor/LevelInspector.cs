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
                    for (var x = 0; x < level.levelConfig.size.x; x++)
                    for (var y = 0; y < level.levelConfig.size.y; y++)
                        Gizmos.DrawSphere(float3(level.Grid2World(int2(x, y)), 0), 0.1f);
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
                break;
        }
    }
}
}