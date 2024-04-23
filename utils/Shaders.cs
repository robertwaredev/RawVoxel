using Godot;
using Godot.Collections;

namespace RawUtils
{
    public static class Shaders
    {
        public static Rid CreateComputeShader(RenderingDevice renderingDevice, string rdShaderFile)
        {
            // Load GLSL shader
            RDShaderFile shaderFile = GD.Load<RDShaderFile>(rdShaderFile);
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            Rid shader = renderingDevice.ShaderCreateFromSpirV(shaderBytecode);
            
            return shader;
        }
        public static RDUniform CreateStorageBufferUniform(RenderingDevice renderingDevice, uint byteArrayLength, byte[] byteArray, int binding)
        {
            // Create a uniform to hold the buffer.
            RDUniform uniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = binding
            };

            // Add storage buffer to the uniform.
            uniform.AddId(renderingDevice.StorageBufferCreate(byteArrayLength, byteArray));

            return uniform;
        }
        public static void SetupComputePipeline(RenderingDevice renderingDevice, Rid shader, Rid uniformSet, uint xGroups, uint yGroups, uint zGroups)
        {
            Rid pipeline = renderingDevice.ComputePipelineCreate(shader);
            long computeList = renderingDevice.ComputeListBegin();
            
            renderingDevice.ComputeListBindComputePipeline(computeList, pipeline);
            renderingDevice.ComputeListBindUniformSet(computeList, uniformSet, 0);
            renderingDevice.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
            renderingDevice.ComputeListEnd();
        }
    }
}