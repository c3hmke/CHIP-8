using OpenTK;
using OpenTK.Graphics.OpenGL4;
using static SDL2.SDL;

namespace CHIP_8;

/// <summary>
/// Custom binding context for OpenGL <-> SDL.
/// OpenTK 4.x requires explicit binding initialization, this class is used to tell
/// it about the SDL OpenGL context which is created when the renderer is created.
/// </summary>
public class SDL_GL_BindingsContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL_GL_GetProcAddress(procName);
    }
}

/// <summary>
/// Render Engine used to draw graphics to the screen. This uses the display buffer as
/// is set up by the CPU to actually draw to the screen. The Render function is called
/// periodically by the ClockHandler to ensure that the cadence is correctly adhered to.
/// </summary>
public sealed class RenderEngine : IDisposable
{
    /// W x H of the display in px
    private readonly int _width;
    private readonly int _height;
    
    /// The decay buffer is used to emulate phosphor decay which was present on the
    /// original hardware used to run these emulators. A lot of the programs flicker
    /// every time the screen is drawn, however the screens would hide that effect.
    /// Modern displays respond too quickly and the flicker is very distracting.
    /// DecayRate can be used to modify how long the decay trails are, however the
    /// screen size also affects how noticeable the flicker is through the decay,
    /// so a correct number is guesswork for the most part.
    private readonly float[] _decayBuffer;
    private const    float   DecayRate = 0.9f;

    /// Used to actually draw the display
    private readonly byte[] _textureData; // RGBA8

    /// Class properties
    private readonly nint _window;
    private readonly nint _glContext;
    
    private readonly int _textureId;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;
    private readonly int _shaderProgram;

    /// <summary>
    /// Build the RenderEngine, this will also create an SDL Window and initialize all
    /// the GraphicsLibrary related configuration to be able to draw correctly to the display.
    /// </summary>
    public RenderEngine(int width, int height, int scale)
    {
        _width  = width;
        _height = height;

        _decayBuffer = new float[width * height];
        _textureData = new byte[width * height * 4];

        /// Create an SDL Window using OpenGL
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
        SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, 8);

        _window = SDL_CreateWindow(
            "CHIP-8", 
            SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
            _width * scale, _height * scale,
            SDL_WindowFlags.SDL_WINDOW_OPENGL);

        if (_window == 0)
            throw new Exception($"SDL_CreateWindow FAIL: {SDL_GetError()}");

        _glContext = SDL_GL_CreateContext(_window);
        
        if (_glContext == 0)
            throw new Exception($"SDL_GL_CreateContext FAIL: {SDL_GetError()}");
        
        /// Set up the Window
        SDL_GL_MakeCurrent(_window, _glContext);    // Active window
        SDL_GL_SetSwapInterval(1);                  // VSync

        GL.LoadBindings(new SDL_GL_BindingsContext());
        GL.Viewport(0, 0, _width * scale, _height * scale);
        GL.ClearColor(1f, 0f, 0f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        
        SDL_GL_SwapWindow(_window);
        
        const int stride = 4 * sizeof(float);

        uint[]  indices  = [0, 1, 2, 2, 3, 0];
        float[] vertices =
        [
            // pos     // tex
            -1f, -1f,  0f, 1f,  // bottom-left  -> tex (0,1)
             1f, -1f,  1f, 1f,  // bottom-right -> tex (1,1)
             1f,  1f,  1f, 0f,  // top-right    -> tex (1,0)
            -1f,  1f,  0f, 0f   // top-left     -> tex (0,0)
        ];

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        GL.BindVertexArray(0);

        /// Textures
        _textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _textureId);

        GL.TexImage2D(
            target:         TextureTarget.Texture2D,
            level:          0,
            internalformat: PixelInternalFormat.Rgba,
            width:          _width,
            height:         _height,
            border:         0,
            format:         PixelFormat.Rgba,
            type:           PixelType.UnsignedByte,
            pixels:         0);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        /// Shaders
        const string vertexSrc = """

                                 #version 330 core
                                 layout (location = 0) in vec2 inPos;
                                 layout (location = 1) in vec2 inTex;

                                 out vec2 TexCoord;

                                 void main()
                                 {
                                     TexCoord = inTex;
                                     gl_Position = vec4(inPos, 0.0, 1.0);
                                 }

                                 """;

        const string fragmentSrc = """

                                   #version 330 core
                                   in vec2 TexCoord;
                                   out vec4 FragColor;

                                   uniform sampler2D uTex;

                                   void main()
                                   {
                                       FragColor = texture(uTex, TexCoord);
                                   }

                                   """;

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSrc);
        GL.CompileShader(vertexShader);
        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vsStatus);
        
        if (vsStatus == 0)
            throw new Exception($"Vertex shader compile FAIL: {GL.GetShaderInfoLog(vertexShader)}");

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSrc);
        GL.CompileShader(fragmentShader);
        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fsStatus);
        
        if (fsStatus == 0)
            throw new Exception($"Fragment shader compile FAIL: {GL.GetShaderInfoLog(fragmentShader)}");

        int shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);
        GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
        
        if (linkStatus == 0)
            throw new Exception($"Shader program link FAIL: {GL.GetProgramInfoLog(shaderProgram)}");

        GL.DetachShader(shaderProgram, vertexShader);
        GL.DetachShader(shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        
        _shaderProgram = shaderProgram;
    }
    
    /// <summary>
    /// Draw the passed display buffer to the screen.
    /// Interlaces the display buffer with a phosphor decay layer to emulate the effect.
    /// </summary>
    public void Render(uint[] display)
    {
        /// Update decay buffer based on display state.
        for (var i = 0; i < _width * _height; i++)
        {
            bool pixelOn = display[i] == 0xFFFFFFFF;    // is the pixel lit?

            if (pixelOn) _decayBuffer[i] = 1.0f;        // instant full brightness
            else         _decayBuffer[i] *= DecayRate;  // decay old light

            // Convert brightness [0,1] to grayscale RGBA
            var brightness = (byte)(_decayBuffer[i] * 255.0f);
            
            _textureData[i * 4 + 0] = brightness;       // R
            _textureData[i * 4 + 1] = brightness;       // G
            _textureData[i * 4 + 2] = brightness;       // B
            _textureData[i * 4 + 3] = 255;              // A
        }

        /// Upload to OpenGL texture
        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        unsafe
        {
            fixed (byte* ptr = _textureData)
            {
                GL.TexSubImage2D(
                    target:  TextureTarget.Texture2D,
                    level:   0,
                    xoffset: 0,
                    yoffset: 0,
                    width:   _width,
                    height:  _height,
                    format:  PixelFormat.Rgba,
                    type:    PixelType.UnsignedByte,
                    pixels:  (IntPtr)ptr);
            }
        }

        /// Draw quad
        SDL_GL_GetDrawableSize(_window, out int fbW, out int fbH);
        GL.Viewport(0, 0, fbW, fbH);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.UseProgram(_shaderProgram);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uTex"), 0);

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.UseProgram(0);

        /// Present
        SDL_GL_SwapWindow(_window);
    }

    /// <summary>
    /// Method used to clean up this object correctly. (Used by GC)
    /// </summary>
    public void Dispose()
    {
        if (_textureId != 0)     GL.DeleteTexture(_textureId);
        if (_vbo != 0)           GL.DeleteBuffer(_vbo);
        if (_ebo != 0)           GL.DeleteBuffer(_ebo);
        if (_vao != 0)           GL.DeleteVertexArray(_vao);
        if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);

        if (_glContext != 0) SDL_GL_DeleteContext(_glContext);
        if (_window != 0)    SDL_DestroyWindow(_window);
    }
}
