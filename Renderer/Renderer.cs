using System.Diagnostics;
using Godot;

namespace RawVoxel.Rendering;

public class Renderer
{
    public RenderingDevice RenderingDevice = RenderingServer.GetRenderingDevice();
    
    public void SetupRenderingPipeline()
    {
        // Chunk shader
        RDShaderFile shaderFile = GD.Load("") as RDShaderFile;
        RDShaderSpirV shaderSpirV = shaderFile.GetSpirV();
        Rid shader = RenderingDevice.ShaderCreateFromSpirV(shaderSpirV, "RawVoxel Shader");

        // Framebuffer format
        long framebufferFormat = RenderingDevice.FramebufferFormatCreate
        ([
            new RDAttachmentFormat()
            {
                Format = RenderingDevice.DataFormat.R8G8B8A8Srgb,
                Samples = RenderingDevice.TextureSamples.Samples64
            }
        ]);

        // Vertex format
        long vertexFormat = RenderingDevice.VertexFormatCreate
        ([
            new RDVertexAttribute() // Vertex
            {
                Format = RenderingDevice.DataFormat.R8G8B8A8Srgb,
                Frequency = RenderingDevice.VertexFrequency.Instance,
                Location = 0,
                Offset = 0,
                Stride = 0
            }
        ]);

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
                new RDPipelineColorBlendStateAttachment()
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
        Rid renderingPipeline = RenderingDevice.RenderPipelineCreate
        (
            shader: shader,
            framebufferFormat: framebufferFormat,
            vertexFormat: vertexFormat,
            primitive: RenderingDevice.RenderPrimitive.Triangles,
            rasterizationState: rasterizationState,
            multisampleState: multisampleState,
            stencilState: depthStencilState,
            colorBlendState: colorBlendState
            // dynamicStateFlags:
            // forRenderPass:
            // specializationConstants: 
        );

        Debug.Assert(RenderingDevice.RenderPipelineIsValid(renderingPipeline), "Invalid render pipeline!");

        // Framebuffer
        //Rid framebuffer = RenderingDevice.FramebufferCreate();
        //Debug.Assert(RenderingDevice.FramebufferIsValid(framebuffer), "Invalid framebuffer!");
    }
}