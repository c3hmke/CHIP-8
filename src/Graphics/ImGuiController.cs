using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using SDL2;

namespace CHIP_8.Graphics;

public class ImGuiController : IDisposable
{
    private int _vertexArray;
    private int _vertexBuffer;
    private int _indexBuffer;
    private int _shader;
    private int _fontTexture;
    private int _uProjMatrixLocation;
    private int _uTextureLocation;

    private int _vertexBufferSize = 10000;
    private int _indexBufferSize  = 2000;
    
    
    public ImGuiController(int windowWidth, int windowHeight)
    {
        // ImGui context + style
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        ImGui.StyleColorsDark();

        io.DisplaySize = new Vector2(windowWidth, windowHeight);

        CreateDeviceObjects();
    }
    
    // -------- SDL EVENT HANDLING --------
    public void ProcessEvent(ref SDL.SDL_Event e)
    {
        var io = ImGui.GetIO();

        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_KEYDOWN:
            case SDL.SDL_EventType.SDL_KEYUP:
            {
                bool down   = e.type == SDL.SDL_EventType.SDL_KEYDOWN;
                var  keysym = e.key.keysym;
                ImGuiKey imGuiKey = SdlScancodeToImGuiKey(keysym.scancode);

                if (imGuiKey != ImGuiKey.None)
                {
                    io.AddKeyEvent(imGuiKey, down);
                    io.SetKeyEventNativeData(
                        imGuiKey,
                        (int) keysym.sym,
                        (int) keysym.scancode);
                }

                // Modifiers
                SDL.SDL_Keymod mods = SDL.SDL_GetModState();
                io.AddKeyEvent(ImGuiKey.ModCtrl,  (mods & SDL.SDL_Keymod.KMOD_CTRL) != 0);
                io.AddKeyEvent(ImGuiKey.ModShift, (mods & SDL.SDL_Keymod.KMOD_SHIFT) != 0);
                io.AddKeyEvent(ImGuiKey.ModAlt,   (mods & SDL.SDL_Keymod.KMOD_ALT) != 0);
                io.AddKeyEvent(ImGuiKey.ModSuper, (mods & SDL.SDL_Keymod.KMOD_GUI) != 0);

                break;
            }

            case SDL.SDL_EventType.SDL_TEXTINPUT:
            {
                unsafe
                {
                    fixed (byte* text = e.text.text)
                    {
                        string s = Marshal.PtrToStringUTF8((IntPtr)text);
                        
                        if (!string.IsNullOrEmpty(s))
                            io.AddInputCharactersUTF8(s);
                    }
                }
                break;
            }

            case SDL.SDL_EventType.SDL_MOUSEMOTION:
            {
                io.AddMousePosEvent(e.motion.x, e.motion.y);
                break;
            }

            case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
            case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
            {
                bool down = e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN;
                int b = e.button.button;
                
                int buttonIndex = b switch
                {
                    1 => 0, // SDL_BUTTON_LEFT
                    3 => 1, // SDL_BUTTON_RIGHT
                    2 => 2, // SDL_BUTTON_MIDDLE
                    _ => -1
                };
                
                if (buttonIndex != -1)
                    io.AddMouseButtonEvent(buttonIndex, down);
                
                break;
            }

            case SDL.SDL_EventType.SDL_MOUSEWHEEL:
            {
                // SDL: y>0 = up, y<0 = down
                io.AddMouseWheelEvent(e.wheel.x, e.wheel.y);
                break;
            }
        }
    }

    private ImGuiKey SdlScancodeToImGuiKey(SDL.SDL_Scancode sc)
    {
        return sc switch
        {
            SDL.SDL_Scancode.SDL_SCANCODE_TAB => ImGuiKey.Tab,
            SDL.SDL_Scancode.SDL_SCANCODE_LEFT => ImGuiKey.LeftArrow,
            SDL.SDL_Scancode.SDL_SCANCODE_RIGHT => ImGuiKey.RightArrow,
            SDL.SDL_Scancode.SDL_SCANCODE_UP => ImGuiKey.UpArrow,
            SDL.SDL_Scancode.SDL_SCANCODE_DOWN => ImGuiKey.DownArrow,
            SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP => ImGuiKey.PageUp,
            SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN => ImGuiKey.PageDown,
            SDL.SDL_Scancode.SDL_SCANCODE_HOME => ImGuiKey.Home,
            SDL.SDL_Scancode.SDL_SCANCODE_END => ImGuiKey.End,
            SDL.SDL_Scancode.SDL_SCANCODE_INSERT => ImGuiKey.Insert,
            SDL.SDL_Scancode.SDL_SCANCODE_DELETE => ImGuiKey.Delete,
            SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE => ImGuiKey.Backspace,
            SDL.SDL_Scancode.SDL_SCANCODE_SPACE => ImGuiKey.Space,
            SDL.SDL_Scancode.SDL_SCANCODE_RETURN => ImGuiKey.Enter,
            SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE => ImGuiKey.Escape,
            SDL.SDL_Scancode.SDL_SCANCODE_A => ImGuiKey.A,
            SDL.SDL_Scancode.SDL_SCANCODE_C => ImGuiKey.C,
            SDL.SDL_Scancode.SDL_SCANCODE_V => ImGuiKey.V,
            SDL.SDL_Scancode.SDL_SCANCODE_X => ImGuiKey.X,
            SDL.SDL_Scancode.SDL_SCANCODE_Y => ImGuiKey.Y,
            SDL.SDL_Scancode.SDL_SCANCODE_Z => ImGuiKey.Z,
            _ => ImGuiKey.None
        };
    }

    // -------- PER-FRAME UPDATE --------

    public void UpdateFrame(float deltaSeconds, int width, int height)
    {
        var io = ImGui.GetIO();
        
        io.DeltaTime   = deltaSeconds <= 0 ? 1f / 60f : deltaSeconds;
        io.DisplaySize = new Vector2(width, height);

        ImGui.NewFrame();
    }

    // You call this AFTER building your UI for the frame
    public void Render()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    // -------- GL RESOURCE SETUP --------

    private void CreateDeviceObjects()
    {
        _shader = CreateShaderProgram();
        GL.UseProgram(_shader);

        _uProjMatrixLocation = GL.GetUniformLocation(_shader, "uProjection");
        _uTextureLocation = GL.GetUniformLocation(_shader, "uTexture");

        _vertexArray = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArray);

        _vertexBuffer = GL.GenBuffer();
        _indexBuffer = GL.GenBuffer();

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        int stride = Marshal.SizeOf<ImDrawVert>();

        // position (loc 0)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

        // uv (loc 1)
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);

        // color (loc 2)
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        GL.BindVertexArray(0);

        CreateFontTexture();
    }

    private int CreateShaderProgram()
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        const string vertexShaderSrc = """

                          #version 330 core
                          layout (location = 0) in vec2 in_position;
                          layout (location = 1) in vec2 in_texCoord;
                          layout (location = 2) in vec4 in_color;

                          uniform mat4 uProjection;

                          out vec2 frag_uv;
                          out vec4 frag_color;

                          void main()
                          {
                              frag_uv = in_texCoord;
                              frag_color = in_color;
                              gl_Position = uProjection * vec4(in_position, 0.0, 1.0);
                          }
                          """;
        GL.ShaderSource(vertexShader, vertexShaderSrc);
        GL.CompileShader(vertexShader);
        CheckShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        const string fragmentShaderSrc = """

                          #version 330 core
                          in vec2 frag_uv;
                          in vec4 frag_color;

                          uniform sampler2D uTexture;

                          out vec4 out_color;

                          void main()
                          {
                              out_color = frag_color * texture(uTexture, frag_uv);
                          }
                          """;

        GL.ShaderSource(fragmentShader, fragmentShaderSrc);
        GL.CompileShader(fragmentShader);
        CheckShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        
        if (status == 0)
            throw new Exception("Program link error: " + GL.GetProgramInfoLog(program));

        GL.DetachShader(program, vertexShader);
        GL.DetachShader(program, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private void CheckShader(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
            throw new Exception("Shader compile error: " + GL.GetShaderInfoLog(shader));
    }

    private unsafe void CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();

        io.Fonts.GetTexDataAsRGBA32(
            out byte* pixels,
            out int width,
            out int height,
            out int _);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            width, height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            (IntPtr)pixels);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }

    // -------- RENDERING DRAW DATA --------

    private unsafe void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;

        var io = ImGui.GetIO();
        int fbWidth = (int)(io.DisplaySize.X * io.DisplayFramebufferScale.X);
        int fbHeight = (int)(io.DisplaySize.Y * io.DisplayFramebufferScale.Y);
        
        if (fbWidth <= 0 || fbHeight <= 0) return;

        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.ScissorTest);
        GL.ActiveTexture(TextureUnit.Texture0);

        GL.Viewport(0, 0, fbWidth, fbHeight);

        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        float[] orthoProjection =
        {
            2.0f/(R-L),  0.0f,        0.0f, 0.0f,
            0.0f,        2.0f/(T-B),  0.0f, 0.0f,
            0.0f,        0.0f,       -1.0f, 0.0f,
            (R+L)/(L-R), (T+B)/(B-T), 0.0f, 1.0f
        };

        GL.UseProgram(_shader);
        GL.Uniform1(_uTextureLocation, 0);
        GL.UniformMatrix4(_uProjMatrixLocation, 1, false, orthoProjection);

        GL.BindVertexArray(_vertexArray);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];

            int vtxSize = cmdList.VtxBuffer.Size * Marshal.SizeOf<ImDrawVert>();
            if (vtxSize > _vertexBufferSize)
            {
                while (vtxSize > _vertexBufferSize)
                    _vertexBufferSize *= 2;

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
                GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }

            int idxSize = cmdList.IdxBuffer.Size * sizeof(ushort);
            if (idxSize > _indexBufferSize)
            {
                while (idxSize > _indexBufferSize)
                    _indexBufferSize *= 2;

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
                GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vtxSize, (IntPtr)cmdList.VtxBuffer.Data);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, idxSize, (IntPtr)cmdList.IdxBuffer.Data);

            int idxOffset = 0;
            for (int cmd_i = 0; cmd_i < cmdList.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmd_i];

                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);

                var clip = pcmd.ClipRect;
                GL.Scissor(
                    (int)clip.X,
                    (int)(fbHeight - clip.W),
                    (int)(clip.Z - clip.X),
                    (int)(clip.W - clip.Y));

                GL.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    (int)pcmd.ElemCount,
                    DrawElementsType.UnsignedShort,
                    (IntPtr)(idxOffset * sizeof(ushort)),
                    0);

                idxOffset += (int)pcmd.ElemCount;
            }
        }

        GL.Disable(EnableCap.ScissorTest);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        
        GL.DeleteBuffer(_vertexBuffer);
        GL.DeleteBuffer(_indexBuffer);
        GL.DeleteVertexArray(_vertexArray);
        GL.DeleteTexture(_fontTexture);
        GL.DeleteProgram(_shader);
    }
}