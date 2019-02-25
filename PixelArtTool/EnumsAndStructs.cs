using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;

namespace PixelArtTool
{
    public enum ToolMode
    {
        Draw,
        Fill
    }

    public enum BlendMode : byte
    {
        Default = 0,
        Additive = 1
    }

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

    [StructLayout(LayoutKind.Explicit)]
    public struct PixelColor
    {
        // 32 bit BGRA 
        [FieldOffset(0)] public UInt32 ColorBGRA;
        // 8 bit components
        [FieldOffset(0)] public byte Blue;
        [FieldOffset(1)] public byte Green;
        [FieldOffset(2)] public byte Red;
        [FieldOffset(3)] public byte Alpha;
    }

    // helper for converting bool<>enum for xaml linked values
    // https://stackoverflow.com/a/2908885/5452781
    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.Equals(true) == true ? parameter : Binding.DoNothing;
        }
    }
}
