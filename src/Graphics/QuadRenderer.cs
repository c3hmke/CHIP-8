using OpenTK.Graphics.OpenGL4;

namespace CHIP_8.Graphics;

public sealed class QuadRenderer : IDisposable
{
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;
    private readonly int _program;

    private readonly int _uTexture;
    private readonly int _uScale;   // vec2
    private readonly int _uOffset;  // vec2

    // Fullscreen quad in NDC, with UVs.
    // We'll scale+offset in the vertex shader to preserve aspect ratio.
    private static readonly float[] Vertices =
    {
        // pos.x, pos.y,  uv.x, uv.y
        -1f, -1f,         0f, 0f,
         1f, -1f,         1f, 0f,
         1f,  1f,         1f, 1f,
        -1f,  1f,         0f, 1f,
    };

    private static readonly uint[] Indices = { 0, 1, 2, 0, 2, 3 };

    public QuadRenderer()
    {
        _program = CreateProgram(VertexSrc, FragmentSrc);

        _uTexture = GL.GetUniformLocation(_program, "uTexture");
        _uScale   = GL.GetUniformLocation(_program, "uScale");
        _uOffset  = GL.GetUniformLocation(_program, "uOffset");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float), Vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, Indices.Length * sizeof(uint), Indices, BufferUsageHint.StaticDraw);

        int stride = 4 * sizeof(float);

        // location 0: vec2 position
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

        // location 1: vec2 uv
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        GL.BindVertexArray(0);
    }

    public void Draw(int textureId, int windowWidth, int windowHeight, int texW = 64, int texH = 32)
    {
        // Compute scale/offset to preserve aspect ratio and letterbox.
        // We work in NDC: scale in [-1,1] space.
        float winAspect = windowWidth / (float)windowHeight;
        float texAspect = texW / (float)texH;

        float scaleX = 1f, scaleY = 1f;
        if (winAspect > texAspect)
        {
            // Window is wider than content: reduce X (pillarbox)
            scaleX = texAspect / winAspect;
        }
        else
        {
            // Window is taller than content: reduce Y (letterbox)
            scaleY = winAspect / texAspect;
        }

        // offset = 0 means centered
        float offsetX = 0f;
        float offsetY = 0f;

        // Make sure state is known (ImGui changes GL state)
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.ScissorTest);
        GL.Disable(EnableCap.Blend);

        GL.Viewport(0, 0, windowWidth, windowHeight);

        GL.UseProgram(_program);
        GL.Uniform1(_uTexture, 0);
        GL.Uniform2(_uScale, scaleX, scaleY);
        GL.Uniform2(_uOffset, offsetX, offsetY);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, textureId);

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, Indices.Length, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_program);
    }

    private static int CreateProgram(string vs, string fs)
    {
        int v = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(v, vs);
        GL.CompileShader(v);
        CheckShader(v);

        int f = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(f, fs);
        GL.CompileShader(f);
        CheckShader(f);

        int p = GL.CreateProgram();
        GL.AttachShader(p, v);
        GL.AttachShader(p, f);
        GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new Exception("Shader link error: " + GL.GetProgramInfoLog(p));

        GL.DetachShader(p, v);
        GL.DetachShader(p, f);
        GL.DeleteShader(v);
        GL.DeleteShader(f);

        return p;
    }

    private static void CheckShader(int s)
    {
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new Exception("Shader compile error: " + GL.GetShaderInfoLog(s));
    }

    private const string VertexSrc = """
        #version 330 core
        layout (location = 0) in vec2 in_pos;
        layout (location = 1) in vec2 in_uv;

        uniform vec2 uScale;
        uniform vec2 uOffset;

        out vec2 v_uv;

        void main()
        {
            vec2 p = in_pos;
            p = p * uScale + uOffset;
            gl_Position = vec4(p, 0.0, 1.0);
            v_uv = in_uv;
        }
    """;

    private const string FragmentSrc = """
        #version 330 core
        in vec2 v_uv;
        uniform sampler2D uTexture;
        out vec4 out_color;

        void main()
        {
            out_color = texture(uTexture, v_uv);
        }
    """;
}
