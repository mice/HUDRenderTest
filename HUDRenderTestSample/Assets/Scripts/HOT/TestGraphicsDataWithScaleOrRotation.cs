using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;



public class TestGraphicsDataWithScaleOrRotation : MonoBehaviour
{

    public enum ModifierType
    {
        None = 0,
        Scale = 1,
        Rotation = 2,
        Scale_AND_Rotation = 1 | 2,
    }

    public Font font;
    public Camera ui_Camera;


    //九宫格的图片,背景
    public UIImage img_ui_1;
    //正常的图片Simple
    public UIImage img_ui_2;
    //特效
    public UIImage img_ui_3;
    //Text文本.
    public UIText txt_1;

    private const int FontTextureSlot = 0;
    private const int ImageTextureSlot1 = 1;
    private const int ImageTextureSlot2 = 2;
    private const int ImageTextureSlot3 = 3;
    private static readonly int MainTex0 = Shader.PropertyToID("_MainTex0");
    private static readonly int MainTex1 = Shader.PropertyToID("_MainTex1");
    private static readonly int MainTex2 = Shader.PropertyToID("_MainTex2");
    private static readonly int MainTex3 = Shader.PropertyToID("_MainTex3");
    private static readonly VertexAttributeDescriptor[] MultiStreamVertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 1),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
    };
    private static readonly VertexAttributeDescriptor[] SingleStreamVertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
    };

    private Mesh combine_mesh;
    private Material comb_Material;
    private PackedVertex[] vertexBuffer = new PackedVertex[0];
    private int[] indexBuffer = new int[0];
    private bool appliedUseMultiStream;

    public ModifierType Modifier = ModifierType.None;
    public bool UseMultiStream = true;

    [Range(0.25f,5.0f)]
    public float Scale = 1.0f;

    [Range(0f, 360f)]
    public float Rotation_Z = 1.0f;

    private List<Vector3> vertBuff = new List<Vector3>();
    private List<Vector4> uvs = new List<Vector4>();
    private List<Color32> colors = new List<Color32>();
    private List<int> triangles = new List<int>();

    UIMeshData img_meshData_1;
    UIMeshData img_meshData_2;
    UIMeshData img_meshData_3;
    UIMeshData txt_meshData_1;

    [Button("ReCreate")]
    public string _X;

    public void ReCreate()
    {
        img_meshData_1 = new UIMeshData();
        img_ui_1.DoGenerate(img_meshData_1);
        img_meshData_1.UpdateTextureIndex(ImageTextureSlot1);

        img_meshData_2 = new UIMeshData();
        img_ui_2.DoGenerate(img_meshData_2);
        img_meshData_2.UpdateTextureIndex(ImageTextureSlot2);

        img_meshData_3 = new UIMeshData();
        img_ui_3.DoGenerate(img_meshData_3, img_ui_1.transform);
        img_meshData_3.UpdateTextureIndex(ImageTextureSlot3);

        txt_meshData_1 = new UIMeshData();
        txt_meshData_1.TextureIndex = FontTextureSlot;
        txt_1.DoGenerate(txt_meshData_1, img_ui_1.transform);

        if (combine_mesh == null)
        {
            combine_mesh = new Mesh();
            combine_mesh.MarkDynamic();
        }

        var vec = new Vector3(0, 0);

        vertBuff.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        img_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec);
        img_meshData_2.FillToDrawData(vertBuff, uvs, colors, triangles, vec);
        img_meshData_3.FillToDrawData(vertBuff, uvs, colors, triangles, vec);
        txt_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec);


        ApplyMeshData();

        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        BindTextures(comb_Material);
        comb_Material.renderQueue = 3000;
    }

    private void BindTextures(Material material)
    {
        if (material == null)
            return;

        // 为了方便测试,不做重复检查.
        material.SetTexture(MainTex0, font != null && font.material != null ? font.material.mainTexture : null);
        material.SetTexture(MainTex1, GetSpriteTexture(img_ui_1));
        material.SetTexture(MainTex2, GetSpriteTexture(img_ui_2));
        material.SetTexture(MainTex3, GetSpriteTexture(img_ui_3));
    }

    private static Texture GetSpriteTexture(UIImage image)
    {
        return image != null && image.sprite != null ? image.sprite.texture : null;
    }

    private void ApplyMeshData()
    {
        int vertexCount = vertBuff.Count;
        int indexCount = triangles.Count;
        EnsureIndexBufferCapacity(indexCount);

        for (int i = 0; i < indexCount; i++)
        {
            indexBuffer[i] = triangles[i];
        }

        const MeshUpdateFlags flags =
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontRecalculateBounds;

        combine_mesh.Clear(false);
        ApplyVertexBuffer(vertexCount, flags);
        combine_mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        combine_mesh.SetIndexBufferData(indexBuffer, 0, 0, indexCount, flags);
        combine_mesh.subMeshCount = 1;
        combine_mesh.SetSubMesh(
            0,
            new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vertexCount,
            },
            flags);
        combine_mesh.RecalculateBounds();
    }

    private void ApplyVertexBuffer(int vertexCount, MeshUpdateFlags flags)
    {
        if (UseMultiStream)
        {
            combine_mesh.SetVertexBufferParams(vertexCount, MultiStreamVertexLayout);
            combine_mesh.SetVertexBufferData(vertBuff, 0, 0, vertexCount, 0, flags);
            combine_mesh.SetVertexBufferData(uvs, 0, 0, vertexCount, 1, flags);
            combine_mesh.SetVertexBufferData(colors, 0, 0, vertexCount, 2, flags);
        }
        else
        {
            EnsureVertexBufferCapacity(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                vertexBuffer[i] = new PackedVertex
                {
                    Position = vertBuff[i],
                    Color = colors[i],
                    UV0 = uvs[i],
                };
            }

            combine_mesh.SetVertexBufferParams(vertexCount, SingleStreamVertexLayout);
            combine_mesh.SetVertexBufferData(vertexBuffer, 0, 0, vertexCount, 0, flags);
        }

        appliedUseMultiStream = UseMultiStream;
    }

    private void EnsureVertexBufferCapacity(int vertexCount)
    {
        if (vertexBuffer.Length < vertexCount)
        {
            vertexBuffer = new PackedVertex[Mathf.NextPowerOfTwo(Mathf.Max(vertexCount, 1))];
        }
    }

    private void EnsureIndexBufferCapacity(int indexCount)
    {
        if (indexBuffer.Length < indexCount)
        {
            indexBuffer = new int[Mathf.NextPowerOfTwo(Mathf.Max(indexCount, 1))];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PackedVertex
    {
        public Vector3 Position;
        public Color32 Color;
        public Vector4 UV0;
    }



    private void LateUpdate()
    {
        if (combine_mesh != null)
        {
            var matix = txt_1.transform.parent.localToWorldMatrix;
            if (UseMultiStream != appliedUseMultiStream)
            {
                ApplyMeshData();
            }

            if ((int)this.Modifier > 0)
            {
                vertBuff.Clear();
                uvs.Clear();
                colors.Clear();
                triangles.Clear();
                Vector3 scale = Vector3.one;
                Quaternion rot = Quaternion.identity;
                if ((Modifier & ModifierType.Scale) > 0)
                {
                    scale = new Vector3(Scale, Scale, Scale);
                }
                if ((Modifier & ModifierType.Rotation) > 0)
                {
                    rot = Quaternion.Euler(0, 0, Rotation_Z);
                }
                Matrix4x4 mtx = Matrix4x4.TRS(Vector3.zero, rot, scale);

                img_meshData_1.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                img_meshData_2.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                img_meshData_3.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                txt_meshData_1.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);

                ApplyMeshData();

                Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
            }
            else
            {
                Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
            }
        }
    }
}
