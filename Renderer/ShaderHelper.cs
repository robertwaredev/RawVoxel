using Godot;
using System.IO;
using System.Diagnostics;

namespace RawVoxel.Rendering;

public static class ShaderHelper
{
    public static class Spatial
    {
        public static Rid Generate(RenderingDevice RD, string vertPath, string fragPath)
        {
            Debug.Assert(File.Exists(vertPath), "Vertex shader file does not exist!");
            Debug.Assert(File.Exists(fragPath), "Fragment shader file does not exist!");

            // Vertex shader
            FileStream vertFile = File.Open(vertPath, FileMode.Open, System.IO.FileAccess.Read);
            string vertSource = new StreamReader(vertFile).ReadToEnd();
            vertFile.Close();

            // Fragment shader
            FileStream fragFile = File.Open(fragPath, FileMode.Open, System.IO.FileAccess.Read);
            string fragSource = new StreamReader(fragFile).ReadToEnd();
            fragFile.Close();

            // Link vertex and fragment
            RDShaderSource shaderSource = new()
            {
                SourceVertex = vertSource,
                SourceFragment = fragSource,
            };

            // Compile SpirV
            RDShaderSpirV shaderSpirV = RD.ShaderCompileSpirVFromSource(shaderSource);
            
            GD.Print(shaderSpirV.CompileErrorVertex);
            GD.Print(shaderSpirV.CompileErrorFragment);
            
            // Create shader from SpirV
            return RD.ShaderCreateFromSpirV(shaderSpirV, "Chunk Shader");
        }
    }
    /* public static class Compute
    {
        public static Rid Generate()
        {
            return new();
        }
    } */
}