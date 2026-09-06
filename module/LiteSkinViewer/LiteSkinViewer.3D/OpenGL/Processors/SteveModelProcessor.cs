using System.Numerics;
using System.Runtime.InteropServices;
using LiteSkinViewer3D.Shared;
using LiteSkinViewer3D.Shared.Enums;
using LiteSkinViewer3D.Shared.Models;
using SkiaSharp;

namespace LiteSkinViewer3D.OpenGL.Processors;

internal sealed class SteveModelProcessor : IDisposable
{
    private readonly OpenGLApi gl;
    private readonly int shaderProgram;

    public SteveModelProcessor(OpenGLApi gl, int shaderProgram)
    {
        this.gl = gl;
        this.shaderProgram = shaderProgram;
    }

    public SteveModelBindings BaseVAO { get; } = new();
    public SteveModelBindings OverlayVAO { get; } = new();
    public SteveModelBindings VoxelVAO { get; } = new();

    /// <summary>动态模型 VAO 列表</summary>
    public List<MeshBinding> DynamicVAOs { get; } = [];

    /// <summary>动态模型各部分的索引数量</summary>
    public List<int> DynamicIndexCounts { get; } = [];

    /// <summary>每个身体部件的 Voxel 索引数量（按 Head,Body,LeftArm,RightArm,LeftLeg,RightLeg 顺序）</summary>
    public int[] VoxelIndexCounts { get; } = new int[6];

    public int DrawIndexCount { get; private set; }

    public void Dispose()
    {
        DeleteVAO(BaseVAO);
        DeleteVAO(OverlayVAO);
        DeleteVAO(VoxelVAO);
    }

    public void Initialize(SkinType type)
    {
        InitVAO(BaseVAO);
        InitVAO(OverlayVAO);
        InitVAO(VoxelVAO);
        Load(type);
    }

    public void LoadDynamic(DynamicModel model)
    {
        foreach (var binding in DynamicVAOs)
        {
            DeleteVAOItem(binding);
        }
        DynamicVAOs.Clear();
        DynamicIndexCounts.Clear();

        foreach (var part in model.Parts)
        {
            var mb = new MeshBinding();
            InitVAOItem(mb);
            PutDynamicVAO(mb, part);
            DynamicVAOs.Add(mb);
            DynamicIndexCounts.Add(part.Indices.Length);
        }
    }

    public void ClearDynamic()
    {
        foreach (var binding in DynamicVAOs)
        {
            DeleteVAOItem(binding);
        }
        DynamicVAOs.Clear();
        DynamicIndexCounts.Clear();
    }

    private void InitVAOItem(MeshBinding item)
    {
        item.VertexBufferObject = gl.GenBuffer();
        item.IndexBufferObject = gl.GenBuffer();
        item.VertexArrayObject = gl.GenVertexArray();
    }

    private void InitVAO(SteveModelBindings smb)
    {
        InitVAOItem(smb.Head);
        InitVAOItem(smb.Body);
        InitVAOItem(smb.LeftArm);
        InitVAOItem(smb.RightArm);
        InitVAOItem(smb.LeftLeg);
        InitVAOItem(smb.RightLeg);
        InitVAOItem(smb.Cape);
    }

    public void Load(SkinType type)
    {
        var baseModel = SteveModelFactory.CreateBaseModel(type);
        var baseTexture = SteveTextureBuilder.GetSteveTexture(type);
        var overlayModel = SteveModelFactory.CreateOverlayModel(type);
        var overlayTexture = SteveTextureBuilder.GetSteveTextureTop(type);

        DrawIndexCount = baseModel.Head.Indices.Length;

        PutVAO(BaseVAO.Head, baseModel.Head, baseTexture.Head);
        PutVAO(BaseVAO.Body, baseModel.Body, baseTexture.Body);
        PutVAO(BaseVAO.LeftArm, baseModel.LeftArm, baseTexture.LeftArm);
        PutVAO(BaseVAO.RightArm, baseModel.RightArm, baseTexture.RightArm);
        PutVAO(BaseVAO.LeftLeg, baseModel.LeftLeg, baseTexture.LeftLeg);
        PutVAO(BaseVAO.RightLeg, baseModel.RightLeg, baseTexture.RightLeg);
        if (baseModel.Cape != null && baseTexture.Cape != null)
            PutVAO(BaseVAO.Cape, baseModel.Cape, baseTexture.Cape);

        PutVAO(OverlayVAO.Head, overlayModel.Head, overlayTexture.Head);
        if (type != SkinType.Legacy)
        {
            PutVAO(OverlayVAO.Body, overlayModel.Body, overlayTexture.Body);
            PutVAO(OverlayVAO.LeftArm, overlayModel.LeftArm, overlayTexture.LeftArm);
            PutVAO(OverlayVAO.RightArm, overlayModel.RightArm, overlayTexture.RightArm);
            PutVAO(OverlayVAO.LeftLeg, overlayModel.LeftLeg, overlayTexture.LeftLeg);
            PutVAO(OverlayVAO.RightLeg, overlayModel.RightLeg, overlayTexture.RightLeg);
        }
    }

    public void LoadVoxel(SKBitmap skin, SkinType type)
    {
        var data = VoxelOverlayGenerator.Generate(skin, type);

        var parts = new (VoxelOverlayPart part, MeshBinding binding)[]
        {
            (data.Head, VoxelVAO.Head),
            (data.Body, VoxelVAO.Body),
            (data.LeftArm, VoxelVAO.LeftArm),
            (data.RightArm, VoxelVAO.RightArm),
            (data.LeftLeg, VoxelVAO.LeftLeg),
            (data.RightLeg, VoxelVAO.RightLeg)
        };

        for (var pi = 0; pi < parts.Length; pi++)
        {
            var (part, binding) = parts[pi];
            VoxelIndexCounts[pi] = part.Indices.Length;
            PutColoredVAO(binding, part);
        }
    }

    private unsafe void PutColoredVAO(MeshBinding mb, VoxelOverlayPart part)
    {
        gl.UseProgram(shaderProgram);
        gl.BindVertexArray(mb.VertexArrayObject);

        var posLoc = gl.GetAttribLocation(shaderProgram, "a_position");
        var colorLoc = gl.GetAttribLocation(shaderProgram, "a_color");
        var normLoc = gl.GetAttribLocation(shaderProgram, "a_normal");

        var vertexCount = part.Vertices.Length / 3;
        var vertices = new ColoredVertexDataGL[vertexCount];

        for (var i = 0; i < vertexCount; i++)
        {
            int vi = i * 3, ci = i * 3, ni = i * 3;
            vertices[i] = new ColoredVertexDataGL
            {
                Position = new Vector3(part.Vertices[vi], part.Vertices[vi + 1], part.Vertices[vi + 2]),
                Color = new Vector3(part.Colors[ci], part.Colors[ci + 1], part.Colors[ci + 2]),
                Normal = new Vector3(part.Normals[ni], part.Normals[ni + 1], part.Normals[ni + 2])
            };
        }

        gl.BindBuffer(gl.GL_ARRAY_BUFFER, mb.VertexBufferObject);
        fixed (void* ptr = vertices)
        {
            gl.BufferData(gl.GL_ARRAY_BUFFER, vertexCount * Marshal.SizeOf<ColoredVertexDataGL>(), new IntPtr(ptr),
                gl.GL_STATIC_DRAW);
        }

        gl.BindBuffer(gl.GL_ELEMENT_ARRAY_BUFFER, mb.IndexBufferObject);
        fixed (void* iptr = part.Indices)
        {
            gl.BufferData(gl.GL_ELEMENT_ARRAY_BUFFER, part.Indices.Length * sizeof(ushort), new IntPtr(iptr),
                gl.GL_STATIC_DRAW);
        }

        gl.VertexAttribPointer(posLoc, 3, gl.GL_FLOAT, false, 9 * sizeof(float), 0);
        gl.VertexAttribPointer(colorLoc, 3, gl.GL_FLOAT, false, 9 * sizeof(float), 3 * sizeof(float));
        gl.VertexAttribPointer(normLoc, 3, gl.GL_FLOAT, false, 9 * sizeof(float), 6 * sizeof(float));

        gl.EnableVertexAttribArray(posLoc);
        gl.EnableVertexAttribArray(colorLoc);
        gl.EnableVertexAttribArray(normLoc);

        gl.BindVertexArray(0);
    }

    private unsafe void PutVAO(MeshBinding mb, CubeItemModel model, float[] uv)
    {
        gl.UseProgram(shaderProgram);
        gl.BindVertexArray(mb.VertexArrayObject);

        var posLoc = gl.GetAttribLocation(shaderProgram, "a_position");
        var texLoc = gl.GetAttribLocation(shaderProgram, "a_texCoord");
        var normLoc = gl.GetAttribLocation(shaderProgram, "a_normal");

        var vertexCount = model.Vertices.Length / 3;
        var vertices = new VertexDataGL[vertexCount];

        for (var i = 0; i < vertexCount; i++)
        {
            int vi = i * 3, ui = i * 2;
            vertices[i] = new VertexDataGL
            {
                Position = new Vector3(model.Vertices[vi], model.Vertices[vi + 1], model.Vertices[vi + 2]),
                UV = new Vector2(uv[ui], uv[ui + 1]),
                Normal = new Vector3(Cube.Vertices[vi], Cube.Vertices[vi + 1], Cube.Vertices[vi + 2])
            };
        }

        gl.BindBuffer(gl.GL_ARRAY_BUFFER, mb.VertexBufferObject);
        fixed (void* ptr = vertices)
        {
            gl.BufferData(gl.GL_ARRAY_BUFFER, vertexCount * Marshal.SizeOf<VertexDataGL>(), new IntPtr(ptr),
                gl.GL_STATIC_DRAW);
        }

        gl.BindBuffer(gl.GL_ELEMENT_ARRAY_BUFFER, mb.IndexBufferObject);
        fixed (void* iptr = model.Indices)
        {
            gl.BufferData(gl.GL_ELEMENT_ARRAY_BUFFER, model.Indices.Length * sizeof(ushort), new IntPtr(iptr),
                gl.GL_STATIC_DRAW);
        }

        gl.VertexAttribPointer(posLoc, 3, gl.GL_FLOAT, false, 8 * sizeof(float), 0);
        gl.VertexAttribPointer(texLoc, 2, gl.GL_FLOAT, false, 8 * sizeof(float), 3 * sizeof(float));
        gl.VertexAttribPointer(normLoc, 3, gl.GL_FLOAT, false, 8 * sizeof(float), 5 * sizeof(float));

        gl.EnableVertexAttribArray(posLoc);
        gl.EnableVertexAttribArray(texLoc);
        gl.EnableVertexAttribArray(normLoc);

        gl.BindVertexArray(0);
    }

    private unsafe void PutDynamicVAO(MeshBinding mb, DynamicMeshData mesh)
    {
        gl.UseProgram(shaderProgram);
        gl.BindVertexArray(mb.VertexArrayObject);

        var posLoc = gl.GetAttribLocation(shaderProgram, "a_position");
        var texLoc = gl.GetAttribLocation(shaderProgram, "a_texCoord");
        var normLoc = gl.GetAttribLocation(shaderProgram, "a_normal");

        var vertexCount = mesh.Vertices.Length / 3;
        var vertices = new VertexDataGL[vertexCount];
        var hasNormals = mesh.Normals.Length >= mesh.Vertices.Length;

        for (var i = 0; i < vertexCount; i++)
        {
            int vi = i * 3, ui = i * 2;
            vertices[i] = new VertexDataGL
            {
                Position = new Vector3(mesh.Vertices[vi], mesh.Vertices[vi + 1], mesh.Vertices[vi + 2]),
                UV = new Vector2(mesh.Uvs[ui], mesh.Uvs[ui + 1]),
                Normal = hasNormals
                    ? new Vector3(mesh.Normals[vi], mesh.Normals[vi + 1], mesh.Normals[vi + 2])
                    : new Vector3(0, 1, 0)
            };
        }

        gl.BindBuffer(gl.GL_ARRAY_BUFFER, mb.VertexBufferObject);
        fixed (void* ptr = vertices)
        {
            gl.BufferData(gl.GL_ARRAY_BUFFER, vertexCount * Marshal.SizeOf<VertexDataGL>(), new IntPtr(ptr),
                gl.GL_STATIC_DRAW);
        }

        gl.BindBuffer(gl.GL_ELEMENT_ARRAY_BUFFER, mb.IndexBufferObject);
        fixed (void* iptr = mesh.Indices)
        {
            gl.BufferData(gl.GL_ELEMENT_ARRAY_BUFFER, mesh.Indices.Length * sizeof(uint), new IntPtr(iptr),
                gl.GL_STATIC_DRAW);
        }

        gl.VertexAttribPointer(posLoc, 3, gl.GL_FLOAT, false, 8 * sizeof(float), 0);
        gl.VertexAttribPointer(texLoc, 2, gl.GL_FLOAT, false, 8 * sizeof(float), 3 * sizeof(float));
        gl.VertexAttribPointer(normLoc, 3, gl.GL_FLOAT, false, 8 * sizeof(float), 5 * sizeof(float));

        gl.EnableVertexAttribArray(posLoc);
        gl.EnableVertexAttribArray(texLoc);
        gl.EnableVertexAttribArray(normLoc);

        gl.BindVertexArray(0);
    }

    private void DeleteVAO(SteveModelBindings smb)
    {
        DeleteVAOItem(smb.Head);
        DeleteVAOItem(smb.Body);
        DeleteVAOItem(smb.LeftArm);
        DeleteVAOItem(smb.RightArm);
        DeleteVAOItem(smb.LeftLeg);
        DeleteVAOItem(smb.RightLeg);
        DeleteVAOItem(smb.Cape);
    }

    private void DeleteVAOItem(MeshBinding mb)
    {
        if (mb.VertexBufferObject != 0) gl.DeleteBuffer(mb.VertexBufferObject);
        if (mb.IndexBufferObject != 0) gl.DeleteBuffer(mb.IndexBufferObject);
        if (mb.VertexArrayObject != 0) gl.DeleteVertexArray(mb.VertexArrayObject);
    }
}