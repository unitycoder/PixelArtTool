﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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

        public PixelColor(byte r, byte g, byte b, byte a)
        {
            ColorBGRA = (UInt32)(b + (g << 8) + (r << 16) + (a << 24));
            Red = r;
            Green = g;
            Blue = b;
            Alpha = a;
        }

        public PixelColor(SolidColorBrush b)
        {
            ColorBGRA = (UInt32)(b.Color.B + (b.Color.G << 8) + (b.Color.R << 16) + (b.Color.A << 24));
            Red = b.Color.R;
            Green = b.Color.G;
            Blue = b.Color.B;
            Alpha = b.Color.A;
        }

        public PixelColor(Color b)
        {
            ColorBGRA = (UInt32)(b.B + (b.G << 8) + (b.R << 16) + (b.A << 24));
            Red = b.R;
            Green = b.G;
            Blue = b.B;
            Alpha = b.A;
        }

        public SolidColorBrush AsSolidColorBrush()
        {
            return new SolidColorBrush(Color.FromArgb(Alpha, Red, Green, Blue));
        }

        public PixelColor Inverted(byte alphaOverride = 255)
        {
            var pc = new PixelColor();
            pc.ColorBGRA = ColorBGRA ^ 0xffffffff;
            pc.Alpha = alphaOverride;
            return pc;
        }

        public static bool operator ==(PixelColor c1, PixelColor c2)
        {
            return c1.ColorBGRA == c2.ColorBGRA;
        }

        public static bool operator !=(PixelColor c1, PixelColor c2)
        {
            return c1.ColorBGRA != c2.ColorBGRA;
        }

        public static PixelColor White { get { return new PixelColor(255, 255, 255, 255); } }
        public static PixelColor Black { get { return new PixelColor(0, 0, 0, 255); } }
        public static PixelColor Transparent { get { return new PixelColor(0, 0, 0, 0); } }

    } // PixelColor

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
