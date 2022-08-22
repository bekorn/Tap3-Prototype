using UnityEngine;
using UnityEngine.UIElements.Experimental;
using static Unity.Mathematics.math;

namespace Gameplay.Scripts
{
public class BackBoard : MonoBehaviour
{
    LineRenderer border;
    MeshRenderer cells;
    Material cellMat, borderMat;

    static readonly int _Thickness = Shader.PropertyToID("_Thickness");
    static readonly int _Constrain = Shader.PropertyToID("_Constrain");
    static readonly int _Color = Shader.PropertyToID("_Color");

    struct State
    {
        public Color color;
        public float thickness, constrain;
    }

    readonly State idle = new()
    {
        color = new(0, 0, 0, 0.5f),
        thickness = 0.4f,
        constrain = 2.8f,
    };

    State peak = new()
    {
        thickness = 0.2f,
        constrain = 0f,
    };

    State current;

    void UpdateMaterials()
    {
        borderMat.SetFloat(_Thickness, current.thickness);
        borderMat.SetFloat(_Constrain, current.constrain);
        borderMat.SetColor(_Color, current.color);
        cellMat.SetColor(_Color, current.color);
    }

    void Start()
    {
        var board = transform.parent.GetComponent<Board>();
        transform.localScale *= board.cellSize;
        transform.Translate(-0.5f * board.cellSize, -0.5f * board.cellSize, 0);

        var level = board.level;

        // configure cells
        {
            cells = GetComponent<MeshRenderer>();
            cellMat = cells.material;
            cellMat.SetTexture("_Texture2D", level.cell.texture);
            var cellUVs = level.cell.uv; // corner order is: tl, tr, bl, br

            var mesh = GetComponent<MeshFilter>().mesh = new Mesh();
            var quadCount = level.size.x * level.size.y;
            var verts = new Vector3[4 * quadCount];
            var uvs = new Vector2[4 * quadCount];
            var indices = new int[6 * quadCount];
            for (var x = 0; x < level.size.x; x++)
            for (var y = 0; y < level.size.y; y++)
            {
                var baseIdx = x * level.size.y + y;

                // corner order is: bl, br, tr, tl
                var baseVert = baseIdx * 4;
                verts[baseVert + 0] = new Vector3(x + 0, y + 0);
                verts[baseVert + 1] = new Vector3(x + 1, y + 0);
                verts[baseVert + 2] = new Vector3(x + 1, y + 1);
                verts[baseVert + 3] = new Vector3(x + 0, y + 1);

                var baseUV = baseIdx * 4;
                uvs[baseUV + 0] = cellUVs[2];
                uvs[baseUV + 1] = cellUVs[3];
                uvs[baseUV + 2] = cellUVs[1];
                uvs[baseUV + 3] = cellUVs[0];

                var baseIndex = baseIdx * 6;
                indices[baseIndex + 0] = baseVert + 0;
                indices[baseIndex + 1] = baseVert + 2;
                indices[baseIndex + 2] = baseVert + 1;
                indices[baseIndex + 3] = baseVert + 0;
                indices[baseIndex + 4] = baseVert + 3;
                indices[baseIndex + 5] = baseVert + 2;
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.Optimize();
            mesh.UploadMeshData(true);
        }

        // configure border
        {
            var width = 0.3f;
            border = GetComponent<LineRenderer>();
            border.widthCurve = AnimationCurve.Constant(0, 1, 2f * width);
            var corners = new Vector3[]
            {
                new(0 - width, 0 - width),
                new(level.size.x + width, 0 - width),
                new(level.size.x + width, level.size.y + width),
                new(0 - width, level.size.y + width)
            };
            border.SetPositions(corners);

            borderMat = border.material;
            borderMat.SetFloat(
                "BorderLength",
                Vector3.Distance(corners[0], corners[1]) + Vector3.Distance(corners[1], corners[2]) +
                Vector3.Distance(corners[2], corners[3]) + Vector3.Distance(corners[3], corners[0]) + 0.001f
            );
        }

        // configure state & materials
        current = idle;
        UpdateMaterials();
    }

    float reactionTime = -100f;
    State reaction;

    public void React(Color color)
    {
        peak.color = color;
        reactionTime = Time.realtimeSinceStartup;
        reaction = current;
    }

    void Update()
    {
        const float total = 4f;
        const float attack = 0.6f / total;

        var t = remap(0, total, 0, 1, Time.realtimeSinceStartup - reactionTime);
        if (t <= 1.01f)
        {
            t = saturate(t);
            if (t < attack)
            {
                // reaction -> peak
                t = Easing.OutBack(t / attack);
                current.thickness = lerp(reaction.thickness, peak.thickness, t);
                current.constrain = lerp(reaction.constrain, peak.constrain, t);
                current.color = Color.Lerp(reaction.color, peak.color, t);
            }
            else
            {
                // peak -> idle
                t = Easing.InQuad(unlerp(attack, 1f, t));
                current.thickness = lerp(peak.thickness, idle.thickness, t);
                current.constrain = lerp(peak.constrain, idle.constrain, t);
                current.color = Color.Lerp(peak.color, idle.color, t);
            }
            UpdateMaterials();
        }
    }
}
}