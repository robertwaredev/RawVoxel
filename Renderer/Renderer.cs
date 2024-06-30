using Godot;
using System.IO;
using System.Diagnostics;
using System;

namespace RawVoxel.Rendering;

[Tool]
public partial class Renderer : Node
{
    #region Variables

    private Rid spatialShader;
    
    private Rid pipeline;
    private long vertexFormat;
    
    private Rid framebuffer;
    private long framebufferFormat;
    private RDTextureFormat colorTextureFormat;
    private RDTextureFormat depthTextureFormat;
    private RDTextureView textureView;
    private Rid colorTexture;
    private Rid depthTexture;

    private Rid indexBuffer;
    private Rid indexArray;
    private Rid storageBuffer;

    long drawList;

    private RenderingDevice RD = RenderingServer.GetRenderingDevice();

    #endregion Variables

    public override void _Ready()
    {
        string vertPath = "addons/RawVoxel/Renderer/vert.glsl";
        string fragPath = "addons/RawVoxel/Renderer/frag.glsl";

        spatialShader = ShaderHelper.Spatial.Generate(RD, vertPath, fragPath);

        SetupFormats();
        SetupPipeline();
        SetupFramebuffer();
        SetupDrawList();
    }
    
    public void SetupFormats()
    {
        // Vertex attributes
        vertexFormat = RD.VertexFormatCreate
        ([
            new RDVertexAttribute() // Vertex
            {
                Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
                Frequency = RenderingDevice.VertexFrequency.Instance,
                Location = 0,
                Offset = 0,
                Stride = 0
            }
        ]);

        // Framebuffer texture formats
        colorTextureFormat = new()
        {
            Width = 1920,
            Height = 1080,
            Depth = 1,
            Mipmaps = 1,
            ArrayLayers = 1,
            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
            Samples = RenderingDevice.TextureSamples.Samples1,
            TextureType = RenderingDevice.TextureType.Type2D,
            UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
        };

        depthTextureFormat = new()
        {
            Width = 1920,
            Height = 1080,
            Depth = 1,
            Mipmaps = 1,
            ArrayLayers = 1,
            Format = RenderingDevice.DataFormat.D16Unorm,
            Samples = RenderingDevice.TextureSamples.Samples1,
            TextureType = RenderingDevice.TextureType.Type2D,
            UsageBits = RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit
        };
        
        // Framebuffer texture view
        textureView = new()
        {
            FormatOverride = RenderingDevice.DataFormat.R8G8B8A8Unorm,
            SwizzleR = RenderingDevice.TextureSwizzle.R,
            SwizzleG = RenderingDevice.TextureSwizzle.G,
            SwizzleB = RenderingDevice.TextureSwizzle.B,
            SwizzleA = RenderingDevice.TextureSwizzle.A
        };
        
        // Framebuffer formats
        framebufferFormat = RD.FramebufferFormatCreate
        ([
            new RDAttachmentFormat() // Color
            {
                Format = colorTextureFormat.Format,
                Samples = colorTextureFormat.Samples,
                UsageFlags = (uint)colorTextureFormat.UsageBits
            },
            new RDAttachmentFormat() // Depth
            {
                Format = depthTextureFormat.Format,
                Samples = depthTextureFormat.Samples,
                UsageFlags = (uint)depthTextureFormat.UsageBits
            }
        ]);
    }
    public void SetupPipeline()
    {
        // Rasterization state
        RDPipelineRasterizationState rasterizationState = new()
        {
            CullMode = RenderingDevice.PolygonCullMode.Disabled,
            DepthBiasClamp = 0.0f,
            DepthBiasConstantFactor = 0.0f,
            DepthBiasEnabled = true,
            DepthBiasSlopeFactor = 0.0f,
            DiscardPrimitives = false,
            EnableDepthClamp = false,
            FrontFace = RenderingDevice.PolygonFrontFace.Clockwise,
            LineWidth = 1.0f,
            PatchControlPoints = 1,
            Wireframe = false
        };

        // Multisample antialiasing state
        RDPipelineMultisampleState multisampleState = new()
        {
            EnableAlphaToCoverage = false,
            EnableAlphaToOne = false,
            EnableSampleShading = false,
            MinSampleShading = 0.0f,
            SampleCount = RenderingDevice.TextureSamples.Samples16,
            SampleMasks =
            [
                // TODO - Figure out what this is.
            ]
        };

        // Depth stencil state
        RDPipelineDepthStencilState depthStencilState = new()
        {
            EnableDepthTest = false,
            EnableDepthWrite = false,
            DepthCompareOperator = RenderingDevice.CompareOperator.Always,
            EnableDepthRange = false,
            DepthRangeMin = 0.0f,
            DepthRangeMax = 0.0f,
            EnableStencil = false,
            FrontOpFail = RenderingDevice.StencilOperation.Zero,
            FrontOpPass = RenderingDevice.StencilOperation.Zero,
            FrontOpDepthFail = RenderingDevice.StencilOperation.Zero,
            FrontOpCompare = RenderingDevice.CompareOperator.Always,
            FrontOpCompareMask = 0,
            FrontOpWriteMask = 0,
            FrontOpReference = 0,
            BackOpFail = RenderingDevice.StencilOperation.Zero,
            BackOpPass = RenderingDevice.StencilOperation.Zero,
            BackOpDepthFail = RenderingDevice.StencilOperation.Zero,
            BackOpCompare = RenderingDevice.CompareOperator.Always,
            BackOpCompareMask = 0,
            BackOpWriteMask = 0,
            BackOpReference = 0,
        };

        // Color blend state
        RDPipelineColorBlendState colorBlendState = new()
        {
            BlendConstant = Colors.Black,
            EnableLogicOp = false,
            LogicOp = RenderingDevice.LogicOperation.Clear,
            Attachments =
            [
                new RDPipelineColorBlendStateAttachment() // I have no idea what these attachments do.
                {
                    AlphaBlendOp = RenderingDevice.BlendOperation.Add,
                    ColorBlendOp = RenderingDevice.BlendOperation.Add,
                    DstAlphaBlendFactor = RenderingDevice.BlendFactor.Zero,
                    DstColorBlendFactor = RenderingDevice.BlendFactor.Zero,
                    EnableBlend = false,
                    SrcAlphaBlendFactor = RenderingDevice.BlendFactor.Zero,
                    SrcColorBlendFactor = RenderingDevice.BlendFactor.Zero,
                    WriteR = true,
                    WriteG = true,
                    WriteB = true,
                    WriteA = true
                }
            ]
        };

        // Render pipeline
        pipeline = RD.RenderPipelineCreate
        (
            shader: spatialShader,
            framebufferFormat: framebufferFormat,
            vertexFormat: vertexFormat,
            primitive: RenderingDevice.RenderPrimitive.Triangles,
            rasterizationState: rasterizationState,
            multisampleState: multisampleState,
            stencilState: depthStencilState,
            colorBlendState: colorBlendState
            // THESE ARE OPTIONAL, ALSO I DON'T KNOW WHAT THEY DO.
            // dynamicStateFlags:
            // forRenderPass:
            // specializationConstants: 
        );
        
        Debug.Assert(RD.RenderPipelineIsValid(pipeline), "Invalid render pipeline!");
    }
    public void SetupFramebuffer()
    {
        // Framebuffer textures
        colorTexture = RD.TextureCreate(colorTextureFormat, textureView);
        Debug.Assert(RD.TextureIsValid(colorTexture), "Invalid framebuffer color texture!");

        depthTexture = RD.TextureCreate(depthTextureFormat, textureView);
        Debug.Assert(RD.TextureIsValid(depthTexture), "Invalid framebuffer depth texture!");

        // Framebuffer
        framebuffer = RD.FramebufferCreate([colorTexture, depthTexture], framebufferFormat);
        Debug.Assert(RD.FramebufferIsValid(framebuffer), "Invalid framebuffer!");
    }
    public void SetupDrawList()
    {
        // Create draw list
        drawList = RD.DrawListBegin
        (
            framebuffer: framebuffer,
            initialColorAction: RenderingDevice.InitialAction.Clear,
            finalColorAction: RenderingDevice.FinalAction.Read,
            initialDepthAction: RenderingDevice.InitialAction.Clear,
            finalDepthAction: RenderingDevice.FinalAction.Discard,
            clearColorValues: null, // TODO - This probably needs to be filled in.
            clearDepth: 1.0f,
            clearStencil: 0,
            region: null
        );

        // Bind draw list to the pipeline
        RD.DrawListBindRenderPipeline(drawList, pipeline);
    }
    
    public void BufferSurface(byte[] indices, byte[] data)
    {
        Debug.Assert(indices.Length != 0, "Tried to buffer a surface with no indices!");
        Debug.Assert(data.Length != 0, "Tried to buffer a surface with no data!");

        // Index buffer
        indexBuffer = RD.IndexBufferCreate((uint)indices.Length, RenderingDevice.IndexBufferFormat.Uint32, indices, false);
        indexArray = RD.IndexArrayCreate(indexBuffer, 0, (uint)indices.Length);
        RD.DrawListBindIndexArray(drawList, indexArray);

        // Data buffer
        storageBuffer = RD.StorageBufferCreate((uint)data.Length * sizeof(short), data, RenderingDevice.StorageBufferUsage.Indirect);
    }

    public void Draw()
    {
        RD.DrawListDraw
        (
            drawList: drawList,
            useIndices: true,
            instances: 1,
            proceduralVertexCount: 0
        );
        
        // Stop drawing stuff
        RD.DrawListEnd();
    }
}