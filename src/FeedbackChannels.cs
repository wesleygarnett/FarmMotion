// GPL-2.0-only. Shared channel selection and carrier-independent rumble levels.
using System;
namespace FarmMotion {
    public enum FeedbackSolo { All, Bumps, Body, Texture, Road, Original }
    public struct RumbleSignal {
        public double Low, High;
        public RumbleSignal(double low,double high) { Low=Clamp(low); High=Clamp(high); }
        static double Clamp(double value) { return double.IsNaN(value)||double.IsInfinity(value) ? 0 : Math.Max(0,Math.Min(1,value)); }
    }
    internal static class FeedbackChannels {
        public static bool Includes(FeedbackSolo solo,FeedbackSolo channel) { return solo==FeedbackSolo.All || solo==channel; }
    }
}
