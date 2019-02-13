using System.Runtime.InteropServices;
using System.Windows;

namespace PixelArtTool
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CustomPoint
    {
        public int X;
        public int Y;
        public static implicit operator Point(CustomPoint point)
        {
            return new Point(point.X, point.Y);
        }
    }
} // namespace
