using System.Collections.Generic;
using System.Drawing;
using FunWrite;

namespace FunWrite
{
    public sealed class Sample
    {
        public int StrokeNumber;
        public long Time;
        public float X, Y;
        public ushort Pressure;
        public Sample(int number, long time, float x, float y, ushort pressure)
        { StrokeNumber = number; Time = time; X = x; Y = y; Pressure = pressure; }
    }

    // Geometry only. No gap joining, endpoint trimming or substroke algorithms.
    public sealed class Stroke
    {
        public List<List<Sample>> StrokeList = new List<List<Sample>>();
        public float Left, Right, Top, Bottom, CenterX, CenterY;
        public int Width, Height;
        public Stroke() { }
        public Stroke(string text) : this(TrajectoryChecker.Parse(text)) { }
        public Stroke(IEnumerable<Sample> samples)
        {
            var parsed = TrajectoryChecker.ContactStroke(samples);
            StrokeList = parsed.StrokeList;
            Left = parsed.Left; Right = parsed.Right; Top = parsed.Top; Bottom = parsed.Bottom;
            CenterX = parsed.CenterX; CenterY = parsed.CenterY; Width = parsed.Width; Height = parsed.Height;
        }
    }

    public static class ZColor
    {
        static readonly Color[] Colors = { Color.Teal, Color.BlueViolet, Color.Tan, Color.LawnGreen,
            Color.Orange, Color.Fuchsia, Color.Turquoise, Color.GreenYellow, Color.DodgerBlue,
            Color.LightSalmon, Color.Khaki, Color.Aquamarine, Color.SteelBlue, Color.MediumOrchid };
        public static Color GetColor(int index) { return Colors[index % Colors.Length]; }
    }
}
