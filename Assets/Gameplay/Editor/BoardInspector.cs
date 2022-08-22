using Gameplay.Scripts;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Gameplay.Editor
{
[CustomEditor(typeof(Board))]
public class BoardInspector : UnityEditor.Editor
{
    enum DebugLayer { None, Cells, Pieces, Clusters }

    static DebugLayer debugLayer = DebugLayer.None;
    static GUIStyle labelStyle;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Separator();
        debugLayer = (DebugLayer)EditorGUILayout.EnumPopup("Debug", debugLayer);
    }

    // custom GUI methods because the current is broken: https://unity3d.atlassian.net/servicedesk/customer/portal/2/IN-14469
    static Vector2 WorldToGUIPointWithDepth(Vector3 world)
    {
        world = Handles.matrix.MultiplyPoint(world);
        var cam = Camera.current;
        Vector2 screenPoint = cam.WorldToScreenPoint(world);
        screenPoint.y = cam.pixelHeight - screenPoint.y;
        return EditorGUIUtility.PixelsToPoints(screenPoint);
    }

    static readonly GUIContent tempGUIContent = new();

    static void Label(Vector3 pos, string text, GUIStyle style)
    {
        tempGUIContent.text = text;

        var rect = new Rect(WorldToGUIPointWithDepth(pos), style.CalcSize(tempGUIContent));
        // @formatter:off
        rect.x -= rect.width * style.alignment switch
        {
            /* Right  */ TextAnchor.UpperRight  or TextAnchor.MiddleRight  or TextAnchor.LowerRight  => 1f,
            /* Center */ TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => 0.5f,
            _ => 0f
        };
        rect.y -= rect.height * style.alignment switch
        {
            /* Lower  */ TextAnchor.LowerLeft  or TextAnchor.LowerCenter  or TextAnchor.LowerRight  => 1f,
            /* Middle */ TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => 0.5f,
            _ => 0
        };
        // @formatter:on
        style.padding.Add(rect);

        GUI.Label(rect, tempGUIContent, style);
    }

    [DrawGizmo(GizmoType.Selected)]
    static void DrawGizmos(Board board, GizmoType gizmoType)
    {
        // Unity decides the actual look of the style just by the GUIStyle.name, so it is not possible to customize the background etc.
        // TextField has a solid background and minimal mouse interaction (hover, active..)
        labelStyle ??= new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleCenter };

        Handles.BeginGUI();
        switch (debugLayer)
        {
        case DebugLayer.None:
            break;

        case DebugLayer.Cells:
            if (board.level is not null)
            {
                for (var x = 0; x < board.level.size.x; x++)
                for (var y = 0; y < board.level.size.y; y++)
                    Gizmos.DrawSphere(float3(board.Grid2World(int2(x, y)), 0), 0.1f);

                var bl = float3(board.Grid2World(int2(0, 0)) - 0.5f * board.cellSize, 0);
                var tr = float3(board.Grid2World(board.level.size) - 0.5f * board.cellSize, 0);
                for (var x = 0; x <= board.level.size.x; x++) // vertical lines
                    Gizmos.DrawLine(
                        float3(bl.x + x * board.cellSize, bl.y, 0),
                        float3(bl.x + x * board.cellSize, tr.y, 0)
                    );
                for (var y = 0; y <= board.level.size.y; y++) // horizontal lines
                    Gizmos.DrawLine(
                        float3(bl.x, bl.y + y * board.cellSize, 0),
                        float3(tr.x, bl.y + y * board.cellSize, 0)
                    );
            }
            break;

        case DebugLayer.Pieces:
            if (board.Pieces.IsInit)
                for (var i = 0; i < board.Pieces.Length; i++)
                    Label(
                        float3(board.Idx2World(i), 0),
                        board.Pieces[i].ToString(),
                        labelStyle
                    );
            break;

        case DebugLayer.Clusters:
            if (board.clusterSolver.Clusters.IsInit)
            {
                Gizmos.color = Color.black;
                for (var i = 0; i < board.clusterSolver.Clusters.Length; i++)
                {
                    ref readonly var node = ref board.clusterSolver.Nodes[i];
                    if (node.IsRoot)
                    {
                        ref readonly var cluster = ref board.clusterSolver.Clusters[i];
                        Label(
                            float3(board.Idx2World(i), 0),
                            $"({cluster.group.x.ToString()},{cluster.group.y.ToString()}) {cluster.size.ToString()}",
                            labelStyle
                        );
                    }
                    else
                    {
                        Gizmos.DrawLine(
                            float3(board.Grid2World(node.grid), 0),
                            float3(board.Grid2World(node.parentGrid), 0)
                        );
                    }
                }
            }
            break;
        }
        Handles.EndGUI();
    }
}
}