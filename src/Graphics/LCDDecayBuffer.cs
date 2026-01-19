using OpenTK.Graphics.OpenGL4;

namespace CHIP_8.Graphics;

/// <summary>
/// Rear buffer for double buffered rendering emulating LCD screen
/// Old LCDs which were used on interpreters had some pixel "decay"
/// This can be emulated with the following:
/// - Temporal smoothing : pixels turn on / off slowly instead of instant
/// - Directional smear  : this creates the shadow effect on pixels
///
/// Pipeline:
/// CHIP-8 framebuffer (64x32)  ──► upload as texture A
/// Previous LCD texture        ──► texture B
/// 
/// Shader:     mix(prev, current, rise/fall) + horizontal smear
/// 
/// Render → output texture C
/// Swap B ↔ C
/// Draw C to screen
/// </summary>
public sealed class LCDDecayBuffer : IDisposable
{
    public int Width { get; }
    public int Height { get; }

    public int[] Textures = new int[2];
    public int[] FBOs     = new int[2];

    private int _index;

    public int ReadTexture  => Textures[_index];
    public int WriteFbo     => FBOs[1 - _index];
    public int WriteTexture => Textures[1 - _index];

    public void Swap() => _index = 1 - _index;

    public LCDDecayBuffer(int w, int h)
    {
        Width = w;
        Height = h;

        for (int i = 0; i < 2; i++)
        {
            Textures[i] = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Textures[i]);

            GL.TexImage2D(
                TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba8,
                w, h, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            FBOs[i] = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBOs[i]);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                Textures[i],
                0);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Dispose()
    {
        foreach (var t in Textures) GL.DeleteTexture(t);
        foreach (var f in FBOs)     GL.DeleteFramebuffer(f);
    }
}