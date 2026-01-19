using OpenTK.Graphics.OpenGL4;

namespace CHIP_8.Graphics;

public class CHIP8Texture : IDisposable
{
    public readonly int TextureId;

    public CHIP8Texture()
    {
        TextureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, TextureId);
        
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            64, 32,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            IntPtr.Zero);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }
    
    public unsafe void Upload(ReadOnlySpan<uint> framebuffer)
    {
        fixed (uint* ptr = framebuffer)
        {
            GL.BindTexture(TextureTarget.Texture2D, TextureId);
            GL.TexSubImage2D(
                TextureTarget.Texture2D,
                0,
                0, 0,
                64, 32,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                (IntPtr)ptr);
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        GL.DeleteTexture(TextureId);
    }
}