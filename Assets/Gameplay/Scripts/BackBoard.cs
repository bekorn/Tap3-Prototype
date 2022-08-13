using UnityEngine;

namespace Gameplay.Scripts
{
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class BackBoard : MonoBehaviour
{
    void Start()
    {
        var board = transform.parent.GetComponent<Board>();
        transform.localScale *= board.gridSize;
        transform.Translate(-0.5f * board.gridSize, -0.5f * board.gridSize, 0);

        var level = board.level;

        // create and set cell mesh
        {
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
                uvs[baseUV + 0] = new Vector2(0, 0);
                uvs[baseUV + 1] = new Vector2(1, 0);
                uvs[baseUV + 2] = new Vector2(1, 1);
                uvs[baseUV + 3] = new Vector2(0, 1);

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

        // configure line renderer for border
        {
            var width = 0.3f;
            var border = GetComponent<LineRenderer>();
            border.widthCurve = AnimationCurve.Constant(0, 1, 2f * width);
            var corners = new Vector3[]
            {
                new(0 - width, 0 - width),
                new(level.size.x + width, 0 - width),
                new(level.size.x + width, level.size.y + width),
                new(0 - width, level.size.y + width)
            };
            border.SetPositions(corners);

            var borderLength = Vector3.Distance(corners[0], corners[1])
                               + Vector3.Distance(corners[1], corners[2])
                               + Vector3.Distance(corners[2], corners[3])
                               + Vector3.Distance(corners[3], corners[0])
                               + 0.001f;
            border.sharedMaterial.SetFloat("BorderLength", borderLength);
        }
    }
}
}