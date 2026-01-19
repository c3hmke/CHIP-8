using OpenTK.Graphics.OpenGL4;

namespace CHIP_8.Graphics;

public static class GLProgram
{
    public static int Create(string vertexSrc, string fragmentSrc)
    {
        int vs = Compile(ShaderType.VertexShader, vertexSrc);
        int fs = Compile(ShaderType.FragmentShader, fragmentSrc);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);

        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0)
            throw new Exception(GL.GetProgramInfoLog(program));

        GL.DetachShader(program, vs);
        GL.DetachShader(program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        return program;
    }

    private static int Compile(ShaderType type, string src)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, src);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new Exception(GL.GetShaderInfoLog(shader));

        return shader;
    }
}