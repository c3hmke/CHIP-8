using OpenTK.Graphics.OpenGL4;

namespace CHIP_8.Graphics;

public sealed class LCDDecayPass : IDisposable
{
    private readonly int _program;
    private readonly int _uCurrent;
    private readonly int _uPrevious;

    public LCDDecayPass()
    {
        _program   = GLProgram.Create(VertexSrc, FragmentSrc);

        _uCurrent  = GL.GetUniformLocation(_program, "uCurrent");
        _uPrevious = GL.GetUniformLocation(_program, "uPrevious");
    }

    public void Execute(
        int inputTexture,      // raw CHIP-8 texture
        int previousTexture,   // last LCD frame
        int targetFBO          // where to render
    )
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFBO);
        GL.Viewport(0, 0, 64, 32);

        GL.UseProgram(_program);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, inputTexture);
        GL.Uniform1(_uCurrent, 0);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, previousTexture);
        GL.Uniform1(_uPrevious, 1);

        // Draw fullscreen quad (no scaling, no aspect logic)
        FullscreenQuad.Draw();
    }

    public void Dispose()
    {
        GL.DeleteProgram(_program);
    }

    // ---------- SHADERS ----------

    private const string VertexSrc = """
        #version 330 core
        layout (location = 0) in vec2 in_pos;
        layout (location = 1) in vec2 in_uv;
        out vec2 v_uv;

        void main()
        {
            v_uv = in_uv;
            gl_Position = vec4(in_pos, 0.0, 1.0);
        }
    """;

    private const string FragmentSrc = """
        #version 330 core
        in vec2 v_uv;
        out vec4 out_color;
        
        uniform sampler2D uCurrent;
        uniform sampler2D uPrevious;
        
        const float RiseRate = 0.25;
        const float FallRate = 0.25;
        
        void main()
        {
            float curr = texture(uCurrent, v_uv).r;
            float prev = texture(uPrevious, v_uv).r;
        
            float value = (curr > 0.5)
                ? mix(prev, 1.0, RiseRate)
                : mix(prev, 0.0, FallRate);
        
            // Horizontal smear (no wraparound, and no smear on left edge)
            vec2 texel = vec2(1.0 / 64.0, 0.0);
            float left = prev;
            if (v_uv.x > texel.x)
                left = texture(uPrevious, v_uv - texel).r;
        
            // Ghosting settings, smears only on fade
            float smearStrength = (curr > 0.5) ? 0.0 : 0.015;
            value = value * (1.0 - smearStrength) + left * smearStrength;    
                    
            // Snap near-black to black (prevents "never fully off")
            if (value < 0.01) value = 0.0;
        
            out_color = vec4(vec3(value), 1.0);
        }
    """;
}
