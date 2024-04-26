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
    }
}