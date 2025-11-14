using OpenTK.Graphics.OpenGL4;

namespace CHIP_8;

/// <summary>
/// Render Engine used to draw graphics to the screen. This uses the display buffer as
/// is set up by the CPU to actually draw to the screen. The Render function is called
/// periodically by the ClockHandler to ensure that the cadence is correctly adhered to.
/// </summary>
public sealed class RenderEngine : IDisposable
{
    /// W x H of the display in px & scale to draw
    private readonly int _width, _height, _scale;
    
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
        _width  = width; _height = height; _scale  = scale;

        _decayBuffer = new float[width * height];
        _textureData = new byte[width * height * 4];
        
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
        // SDL_GL_GetDrawableSize(_window, out int fbW, out int fbH);
        GL.Viewport(0, 0, _width * _scale, _height * _scale);
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
        // SDL_GL_SwapWindow(_window);
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
    }
}
