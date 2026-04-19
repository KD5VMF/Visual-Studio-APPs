using System.Runtime.InteropServices;

namespace GreatFluidDynamics.Rebuilt;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMessage
    {
        public nint Handle;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public System.Drawing.Point Point;
    }

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    public static bool AppStillIdle => !PeekMessage(out _, nint.Zero, 0, 0, 0);
}
