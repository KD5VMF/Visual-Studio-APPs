using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HelixSolarShow;

internal sealed class ShaderProgram : IDisposable
{
    public int Handle { get; }

    public ShaderProgram(string vertexSource, string fragmentSource)
    {
        int vertex = CompileShader(ShaderType.VertexShader, vertexSource);
        int fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertex);
        GL.AttachShader(Handle, fragment);
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string info = GL.GetProgramInfoLog(Handle);
            throw new InvalidOperationException($"Program link failed:\n{info}");
        }

        GL.DetachShader(Handle, vertex);
        GL.DetachShader(Handle, fragment);
        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
        {
            string info = GL.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"{type} compilation failed:\n{info}");
        }
        return shader;
    }

    public void Use() => GL.UseProgram(Handle);

    public void SetMatrix4(string name, Matrix4 value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.UniformMatrix4(loc, false, ref value);
    }

    public void SetVector2(string name, Vector2 value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform2(loc, value);
    }

    public void SetVector3(string name, Vector3 value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform3(loc, value);
    }

    public void SetVector4(string name, Vector4 value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform4(loc, value);
    }

    public void SetFloat(string name, float value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform1(loc, value);
    }

    public void SetInt(string name, int value)
    {
        int loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform1(loc, value);
    }

    public void Dispose()
    {
        if (Handle != 0)
            GL.DeleteProgram(Handle);
    }
}
