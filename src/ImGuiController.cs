using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using static SDL2.SDL;

namespace CHIP_8;

/// <summary>
/// Minimal ImGui controller for SDL2 + OpenGL3.
/// Handles input events from SDL and renders ImGui draw data
/// using OpenTK's GL bindings.
/// </summary>
public sealed class ImGuiController : IDisposable
{
    private int _vertexArray;
    private int _vertexBuffer;
    private int _indexBuffer;
    private int _vertexBufferSize;
    private int _indexBufferSize;

    private int _shader;
    private int _vertexShader;
    private int _fragmentShader;
    private int _fontTexture;

    private int _attribLocationTex;
    private int _attribLocationProjMtx;
    private int _attribLocationVtxPos;
    private int _attribLocationVtxUV;
    private int _attribLocationVtxColor;

    public ImGuiController(int width, int height)
    {
        ImGui.CreateContext();

        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags  |= ImGuiConfigFlags.NavEnableKeyboard;
        io.DisplaySize   = new Vector2(width, height);

        ImGui.StyleColorsDark();
        io.Fonts.AddFontDefault();

        CreateDeviceResources();
    }

    public void Dispose()
    {
        GL.DeleteTexture(_fontTexture);
        GL.DeleteBuffer(_vertexBuffer);
        GL.DeleteBuffer(_indexBuffer);
        GL.DeleteVertexArray(_vertexArray);

        GL.DetachShader(_shader, _vertexShader);
        GL.DetachShader(_shader, _fragmentShader);
        GL.DeleteShader(_vertexShader);
        GL.DeleteShader(_fragmentShader);
        GL.DeleteProgram(_shader);
        ImGui.DestroyContext();
    }

    public void Update(float deltaSeconds, int width, int height)
    {
        var io            = ImGui.GetIO();
        io.DisplaySize    = new Vector2(width, height);
        io.DeltaTime      = deltaSeconds > 0 ? deltaSeconds : 1f / 60f;
        io.DisplayFramebufferScale = Vector2.One;

        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    /// <summary>
    /// Push SDL events into ImGui IO so widgets can react to input.
    /// </summary>
    public void ProcessEvent(SDL_Event e)
    {
        var io = ImGui.GetIO();

        switch (e.type)
        {
            case SDL_EventType.SDL_MOUSEMOTION:
                io.AddMousePosEvent(e.motion.x, e.motion.y);
                break;

            case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                io.AddMouseButtonEvent((int)e.button.button - 1, true);
                break;

            case SDL_EventType.SDL_MOUSEBUTTONUP:
                io.AddMouseButtonEvent((int)e.button.button - 1, false);
                break;

            case SDL_EventType.SDL_MOUSEWHEEL:
                io.AddMouseWheelEvent(e.wheel.x, e.wheel.y);
                break;

            case SDL_EventType.SDL_TEXTINPUT:
                foreach (char c in e.text.text)
                    io.AddInputCharacter(c);
                break;

            case SDL_EventType.SDL_KEYDOWN:
            case SDL_EventType.SDL_KEYUP:
                bool pressed = e.type == SDL_EventType.SDL_KEYDOWN;
                ImGuiKey key = SDLKeyToImGuiKey(e.key.keysym.sym);
                if (key != ImGuiKey.None) io.AddKeyEvent(key, pressed);
                UpdateModifiers(e.key.keysym.mod);
                break;
        }
    }

    private static ImGuiKey SDLKeyToImGuiKey(SDL_Keycode keycode) => keycode switch
    {
        SDL_Keycode.SDLK_TAB        => ImGuiKey.Tab,
        SDL_Keycode.SDLK_LEFT       => ImGuiKey.LeftArrow,
        SDL_Keycode.SDLK_RIGHT      => ImGuiKey.RightArrow,
        SDL_Keycode.SDLK_UP         => ImGuiKey.UpArrow,
        SDL_Keycode.SDLK_DOWN       => ImGuiKey.DownArrow,
        SDL_Keycode.SDLK_PAGEUP     => ImGuiKey.PageUp,
        SDL_Keycode.SDLK_PAGEDOWN   => ImGuiKey.PageDown,
        SDL_Keycode.SDLK_HOME       => ImGuiKey.Home,
        SDL_Keycode.SDLK_END        => ImGuiKey.End,
        SDL_Keycode.SDLK_INSERT     => ImGuiKey.Insert,
        SDL_Keycode.SDLK_DELETE     => ImGuiKey.Delete,
        SDL_Keycode.SDLK_BACKSPACE  => ImGuiKey.Backspace,
        SDL_Keycode.SDLK_SPACE      => ImGuiKey.Space,
        SDL_Keycode.SDLK_RETURN     => ImGuiKey.Enter,
        SDL_Keycode.SDLK_ESCAPE     => ImGuiKey.Escape,
        SDL_Keycode.SDLK_QUOTE      => ImGuiKey.Apostrophe,
        SDL_Keycode.SDLK_COMMA      => ImGuiKey.Comma,
        SDL_Keycode.SDLK_MINUS      => ImGuiKey.Minus,
        SDL_Keycode.SDLK_PERIOD     => ImGuiKey.Period,
        SDL_Keycode.SDLK_SLASH      => ImGuiKey.Slash,
        SDL_Keycode.SDLK_SEMICOLON  => ImGuiKey.Semicolon,
        SDL_Keycode.SDLK_EQUALS     => ImGuiKey.Equal,
        SDL_Keycode.SDLK_LEFTBRACKET => ImGuiKey.LeftBracket,
        SDL_Keycode.SDLK_BACKSLASH  => ImGuiKey.Backslash,
        SDL_Keycode.SDLK_RIGHTBRACKET => ImGuiKey.RightBracket,
        SDL_Keycode.SDLK_KP_0       => ImGuiKey.Keypad0,
        SDL_Keycode.SDLK_KP_1       => ImGuiKey.Keypad1,
        SDL_Keycode.SDLK_KP_2       => ImGuiKey.Keypad2,
        SDL_Keycode.SDLK_KP_3       => ImGuiKey.Keypad3,
        SDL_Keycode.SDLK_KP_4       => ImGuiKey.Keypad4,
        SDL_Keycode.SDLK_KP_5       => ImGuiKey.Keypad5,
        SDL_Keycode.SDLK_KP_6       => ImGuiKey.Keypad6,
        SDL_Keycode.SDLK_KP_7       => ImGuiKey.Keypad7,
        SDL_Keycode.SDLK_KP_8       => ImGuiKey.Keypad8,
        SDL_Keycode.SDLK_KP_9       => ImGuiKey.Keypad9,
        SDL_Keycode.SDLK_KP_PERIOD  => ImGuiKey.KeypadDecimal,
        SDL_Keycode.SDLK_KP_DIVIDE  => ImGuiKey.KeypadDivide,
        SDL_Keycode.SDLK_KP_MULTIPLY => ImGuiKey.KeypadMultiply,
        SDL_Keycode.SDLK_KP_MINUS   => ImGuiKey.KeypadSubtract,
        SDL_Keycode.SDLK_KP_PLUS    => ImGuiKey.KeypadAdd,
        SDL_Keycode.SDLK_KP_ENTER   => ImGuiKey.KeypadEnter,
        SDL_Keycode.SDLK_KP_EQUALS  => ImGuiKey.KeypadEqual,
        _ => MapAlphaNumeric(keycode)
    };

    private static ImGuiKey MapAlphaNumeric(SDL_Keycode keycode)
    {
        if (keycode is >= SDL_Keycode.SDLK_a and <= SDL_Keycode.SDLK_z)
            return ImGuiKey.A + (keycode - SDL_Keycode.SDLK_a);

        if (keycode is >= SDL_Keycode.SDLK_0 and <= SDL_Keycode.SDLK_9)
            return ImGuiKey._0 + (keycode - SDL_Keycode.SDLK_0);

        return ImGuiKey.None;
    }

    private static void UpdateModifiers(SDL_Keymod keymod)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, (keymod & SDL_Keymod.KMOD_CTRL) != 0);
        io.AddKeyEvent(ImGuiKey.ModShift, (keymod & SDL_Keymod.KMOD_SHIFT) != 0);
        io.AddKeyEvent(ImGuiKey.ModAlt, (keymod & SDL_Keymod.KMOD_ALT) != 0);
        io.AddKeyEvent(ImGuiKey.ModSuper, (keymod & SDL_Keymod.KMOD_GUI) != 0);
    }

    private void CreateDeviceResources()
    {
        _vertexBufferSize = 10000;
        _indexBufferSize  = 2000;

        _vertexArray  = GL.GenVertexArray();
        _vertexBuffer = GL.GenBuffer();
        _indexBuffer  = GL.GenBuffer();

        GL.BindVertexArray(_vertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);

        RecreateFontDeviceTexture();
        CreateShaders();
    }

    private void CreateShaders()
    {
        const string vertexSource = """
                                    #version 330 core
                                    uniform mat4 projection_matrix;
                                    layout (location = 0) in vec2 in_position;
                                    layout (location = 1) in vec2 in_texCoord;
                                    layout (location = 2) in vec4 in_color;
                                    out vec2 frag_texCoord;
                                    out vec4 frag_color;
                                    void main()
                                    {
                                        frag_texCoord = in_texCoord;
                                        frag_color = in_color;
                                        gl_Position = projection_matrix * vec4(in_position.xy, 0, 1);
                                    }
                                    """;

        const string fragmentSource = """
                                      #version 330 core
                                      uniform sampler2D in_fontTexture;
                                      in vec2 frag_texCoord;
                                      in vec4 frag_color;
                                      out vec4 output_color;
                                      void main()
                                      {
                                          output_color = frag_color * texture(in_fontTexture, frag_texCoord);
                                      }
                                      """;

        _vertexShader   = GL.CreateShader(ShaderType.VertexShader);
        _fragmentShader = GL.CreateShader(ShaderType.FragmentShader);

        GL.ShaderSource(_vertexShader, vertexSource);
        GL.ShaderSource(_fragmentShader, fragmentSource);
        GL.CompileShader(_vertexShader);
        GL.CompileShader(_fragmentShader);

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, _vertexShader);
        GL.AttachShader(_shader, _fragmentShader);
        GL.LinkProgram(_shader);

        _attribLocationTex     = GL.GetUniformLocation(_shader, "in_fontTexture");
        _attribLocationProjMtx = GL.GetUniformLocation(_shader, "projection_matrix");
        _attribLocationVtxPos  = 0;
        _attribLocationVtxUV   = 1;
        _attribLocationVtxColor = 2;
    }

    private unsafe void RenderDrawData(ImDrawDataPtr drawData)
    {
        int fbWidth  = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        GL.Viewport(0, 0, fbWidth, fbHeight);

        Matrix4x4 projection = new(
            2.0f / drawData.DisplaySize.X, 0.0f, 0.0f, 0.0f,
            0.0f, -2.0f / drawData.DisplaySize.Y, 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            -1.0f, 1.0f, 0.0f, 1.0f);

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.ScissorTest);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.Scissor(0, 0, fbWidth, fbHeight);

        GL.UseProgram(_shader);
        GL.Uniform1(_attribLocationTex, 0);
        GL.UniformMatrix4(_attribLocationProjMtx, false, ref projection);
        GL.BindVertexArray(_vertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);

        GL.EnableVertexAttribArray(_attribLocationVtxPos);
        GL.EnableVertexAttribArray(_attribLocationVtxUV);
        GL.EnableVertexAttribArray(_attribLocationVtxColor);

        const int stride = sizeof(ImDrawVert);
        GL.VertexAttribPointer(_attribLocationVtxPos, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(_attribLocationVtxUV, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.VertexAttribPointer(_attribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[n];

            int vertexSize = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
            if (vertexSize > _vertexBufferSize)
            {
                int newSize = (int)Math.Max(_vertexBufferSize * 1.5f, vertexSize);
                GL.BufferData(BufferTarget.ArrayBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                _vertexBufferSize = newSize;
            }

            int indexSize = cmdList.IdxBuffer.Size * sizeof(ushort);
            if (indexSize > _indexBufferSize)
            {
                int newSize = (int)Math.Max(_indexBufferSize * 1.5f, indexSize);
                GL.BufferData(BufferTarget.ElementArrayBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                _indexBufferSize = newSize;
            }

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexSize, cmdList.VtxBuffer.Data);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, indexSize, cmdList.IdxBuffer.Data);

            int offset = 0;
            for (int cmd = 0; cmd < cmdList.CmdBuffer.Size; cmd++)
            {
                ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmd];

                if (drawCmd.TextureId != IntPtr.Zero)
                    GL.BindTexture(TextureTarget.Texture2D, (int)drawCmd.TextureId);
                else
                    GL.BindTexture(TextureTarget.Texture2D, _fontTexture);

                var clip = drawCmd.ClipRect;
                GL.Scissor((int)clip.X, (int)(fbHeight - clip.W), (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)drawCmd.ElemCount,
                    DrawElementsType.UnsignedShort, (IntPtr)(offset * sizeof(ushort)), (int)drawCmd.VtxOffset);
                offset += (int)drawCmd.ElemCount;
            }
        }

        GL.DisableVertexAttribArray(_attribLocationVtxPos);
        GL.DisableVertexAttribArray(_attribLocationVtxUV);
        GL.DisableVertexAttribArray(_attribLocationVtxColor);
        GL.BindVertexArray(0);
        GL.Disable(EnableCap.ScissorTest);
        GL.UseProgram(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private unsafe void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out _);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)pixels);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }
}
