using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FluidSim
{
    public static class Extensions
    {
        #region [can be removed if using newer framework]
        static readonly WeakReference s_random = new WeakReference(null);
        public static Random Rand
        {
            get
            {
                var r = (Random)s_random.Target;
                if (r == null) { s_random.Target = r = new Random(); }
                return r;
            }
        }
        #endregion

        public const double Epsilon = 0.000000000001;

        /// <summary>
        /// Generate a random color
        /// </summary>
        /// <returns><see cref="SolidColorBrush"/></returns>
        public static SolidColorBrush GenerateRandomBrush()
        {
            byte r = (byte)Rand.Next(0, 256);
            byte g = (byte)Rand.Next(0, 256);
            byte b = (byte)Rand.Next(0, 256);
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// Generate a random color
        /// </summary>
        /// <returns><see cref="System.Windows.Media.Color"/></returns>
        public static System.Windows.Media.Color GenerateRandomColor()
        {
            byte r = (byte)Rand.Next(0, 256);
            byte g = (byte)Rand.Next(0, 256);
            byte b = (byte)Rand.Next(0, 256);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// Fetch all <see cref="System.Windows.Media.Brushes"/>.
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Brush> GetAllMediaBrushes()
        {
            List<Brush> brushes = new List<Brush>();
            Type brushesType = typeof(Brushes);

            //TypeAttributes ta = typeof(Brushes).Attributes;
            //Debug.WriteLine($"[INFO] TypeAttributes: {ta}");

            // Iterate through the static properties of the Brushes class type.
            foreach (PropertyInfo pi in brushesType.GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                // Check if the property type is Brush/SolidColorBrush
                if (pi.PropertyType == typeof(Brush) || pi.PropertyType == typeof(SolidColorBrush))
                {
                    if (pi.Name.Contains("Transparent"))
                        continue;

                    //Debug.WriteLine($"[INFO] Adding brush '{pi.Name}'");

                    // Get the brush value from the static property
                    brushes.Add((Brush)pi.GetValue(null, null));
                }
            }
            return brushes;
        }

        /// <summary>
        /// 'BitmapCacheBrush','DrawingBrush','GradientBrush','ImageBrush',
        /// 'LinearGradientBrush','RadialGradientBrush','SolidColorBrush',
        /// 'TileBrush','VisualBrush','ImplicitInputBrush'
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Type> GetAllDerivedBrushClasses()
        {
            List<Type> derivedBrushes = new List<Type>();
            // Get the assembly containing the Brush class
            Assembly assembly = typeof(Brush).Assembly;
            try
            {   // Iterate through all types in the assembly
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if the type is a subclass of Brush
                    if (type.IsSubclassOf(typeof(Brush)))
                    {
                        //Debug.WriteLine($"[INFO] Adding type '{type.Name}'");
                        derivedBrushes.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] GetAllDerivedBrushClasses: {ex.Message}");
            }
            return derivedBrushes;
        }

        /// <summary>
        /// Fetch all derived types from a super class.
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Type> GetDerivedSubClasses<T>(T objectClass) where T : class
        {
            List<Type> derivedClasses = new List<Type>();
            // Get the assembly containing the base class
            Assembly assembly = typeof(T).Assembly;
            try
            {   // Iterate through all types in the assembly
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if the type is a subclass of T
                    if (type.IsSubclassOf(typeof(T)))
                    {
                        //Debug.WriteLine($"[INFO] Adding subclass type '{type.Name}'");
                        derivedClasses.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] GetDerivedClasses: {ex.Message}");
            }
            return derivedClasses;
        }

        /// <summary>
        /// Returns the Euclidian distance between two <see cref="System.Windows.Media.Color"/>s.
        /// </summary>
        /// <param name="color1">1st <see cref="System.Windows.Media.Color"/></param>
        /// <param name="color2">2nd <see cref="System.Windows.Media.Color"/></param>
        public static double ColorDistance(System.Windows.Media.Color color1, System.Windows.Media.Color color2)
        {
            return Math.Sqrt(Math.Pow(color1.R - color2.R, 2) + Math.Pow(color1.G - color2.G, 2) + Math.Pow(color1.B - color2.B, 2));
        }

        /// <summary>
        /// Finds the contrast ratio.
        /// This is helpful for determining if one control's foreground and another control's background will be hard to distinguish.
        /// https://www.w3.org/WAI/GL/wiki/Contrast_ratio
        /// (L1 + 0.05) / (L2 + 0.05), where
        /// L1 is the relative luminance of the lighter of the colors, and
        /// L2 is the relative luminance of the darker of the colors.
        /// </summary>
        /// <param name="first"><see cref="System.Windows.Media.Color"/></param>
        /// <param name="second"><see cref="System.Windows.Media.Color"/></param>
        /// <returns>ratio between relative luminance</returns>
        public static double CalculateContrastRatio(System.Windows.Media.Color first, System.Windows.Media.Color second)
        {
            double relLuminanceOne = GetRelativeLuminance(first);
            double relLuminanceTwo = GetRelativeLuminance(second);
            return (Math.Max(relLuminanceOne, relLuminanceTwo) + 0.05) / (Math.Min(relLuminanceOne, relLuminanceTwo) + 0.05);
        }

        /// <summary>
        /// Gets the relative luminance.
        /// https://www.w3.org/WAI/GL/wiki/Relative_luminance
        /// For the sRGB colorspace, the relative luminance of a color is defined as L = 0.2126 * R + 0.7152 * G + 0.0722 * B
        /// </summary>
        /// <param name="c"><see cref="System.Windows.Media.Color"/></param>
        /// <remarks>This is mainly used by <see cref="Extensions.CalculateContrastRatio(Color, Color)"/></remarks>
        public static double GetRelativeLuminance(System.Windows.Media.Color c)
        {
            double rSRGB = c.R / 255.0;
            double gSRGB = c.G / 255.0;
            double bSRGB = c.B / 255.0;

            // WebContentAccessibilityGuideline 2.x definition was 0.03928 (incorrect)
            // WebContentAccessibilityGuideline 3.x definition is 0.04045 (correct)
            double r = rSRGB <= 0.04045 ? rSRGB / 12.92 : Math.Pow(((rSRGB + 0.055) / 1.055), 2.4);
            double g = gSRGB <= 0.04045 ? gSRGB / 12.92 : Math.Pow(((gSRGB + 0.055) / 1.055), 2.4);
            double b = bSRGB <= 0.04045 ? bSRGB / 12.92 : Math.Pow(((bSRGB + 0.055) / 1.055), 2.4);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// Calculates the linear interpolated Color based on the given Color values.
        /// </summary>
        /// <param name="colorFrom">Source Color.</param>
        /// <param name="colorTo">Target Color.</param>
        /// <param name="amount">Weight given to the target color.</param>
        /// <returns>Linear Interpolated Color.</returns>
        public static System.Windows.Media.Color Lerp(this System.Windows.Media.Color colorFrom, System.Windows.Media.Color colorTo, float amount)
        {
            // Convert colorFrom components to lerp-able floats
            float sa = colorFrom.A, sr = colorFrom.R, sg = colorFrom.G, sb = colorFrom.B;

            // Convert colorTo components to lerp-able floats
            float ea = colorTo.A, er = colorTo.R, eg = colorTo.G, eb = colorTo.B;

            // lerp the colors to get the difference
            byte a = (byte)Math.Max(0, Math.Min(255, sa.Lerp(ea, amount))),
                 r = (byte)Math.Max(0, Math.Min(255, sr.Lerp(er, amount))),
                 g = (byte)Math.Max(0, Math.Min(255, sg.Lerp(eg, amount))),
                 b = (byte)Math.Max(0, Math.Min(255, sb.Lerp(eb, amount)));

            // return the new color
            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// Darkens the color by the given percentage using lerp.
        /// </summary>
        /// <param name="color">Source color.</param>
        /// <param name="amount">Percentage to darken. Value should be between 0 and 1.</param>
        /// <returns>Color</returns>
        public static System.Windows.Media.Color DarkerBy(this System.Windows.Media.Color color, float amount)
        {
            return color.Lerp(Colors.Black, amount);
        }

        /// <summary>
        /// Lightens the color by the given percentage using lerp.
        /// </summary>
        /// <param name="color">Source color.</param>
        /// <param name="amount">Percentage to lighten. Value should be between 0 and 1.</param>
        /// <returns>Color</returns>
        public static System.Windows.Media.Color LighterBy(this System.Windows.Media.Color color, float amount)
        {
            return color.Lerp(Colors.White, amount);
        }

        /// <summary>
        /// Negative <paramref name="luminance"/> values darken the given <paramref name="color"/>.
        /// </summary>
        public static Color ColorWithLuminance(Color color, double luminance = -0.25)
        {
            var result = color;
            var partWithLuminance = Clamp(result.R + result.R * luminance, 0, 255);
            var roundValue = (int)Math.Round(partWithLuminance);
            result.R = (byte)roundValue;

            partWithLuminance = Clamp(result.G + result.G * luminance, 0, 255);
            roundValue = (int)Math.Round(partWithLuminance);
            result.G = (byte)roundValue;

            partWithLuminance = Clamp(result.B + result.B * luminance, 0, 255);
            roundValue = (int)Math.Round(partWithLuminance);
            result.B = (byte)roundValue;
            return result;
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to a hexadecimal string representation.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The hexadecimal string representation of the color.</returns>
        public static string ToHex(this Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Creates a <see cref="Color"/> from a XAML color string.
        /// Any format used in XAML should work.
        /// </summary>
        /// <param name="colorString">The XAML color string.</param>
        /// <returns>The created <see cref="Color"/>.</returns>
        public static Color ToColor(this string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
            {
                ThrowArgumentException();
            }

            if (colorString[0] == '#')
            {
                switch (colorString.Length)
                {
                    case 9:
                        {
                            var cuint = Convert.ToUInt32(colorString.Substring(1), 16);
                            var a = (byte)(cuint >> 24);
                            var r = (byte)((cuint >> 16) & 0xff);
                            var g = (byte)((cuint >> 8) & 0xff);
                            var b = (byte)(cuint & 0xff);

                            return Color.FromArgb(a, r, g, b);
                        }

                    case 7:
                        {
                            var cuint = Convert.ToUInt32(colorString.Substring(1), 16);
                            var r = (byte)((cuint >> 16) & 0xff);
                            var g = (byte)((cuint >> 8) & 0xff);
                            var b = (byte)(cuint & 0xff);

                            return Color.FromArgb(255, r, g, b);
                        }

                    case 5:
                        {
                            var cuint = Convert.ToUInt16(colorString.Substring(1), 16);
                            var a = (byte)(cuint >> 12);
                            var r = (byte)((cuint >> 8) & 0xf);
                            var g = (byte)((cuint >> 4) & 0xf);
                            var b = (byte)(cuint & 0xf);
                            a = (byte)(a << 4 | a);
                            r = (byte)(r << 4 | r);
                            g = (byte)(g << 4 | g);
                            b = (byte)(b << 4 | b);

                            return Color.FromArgb(a, r, g, b);
                        }

                    case 4:
                        {
                            var cuint = Convert.ToUInt16(colorString.Substring(1), 16);
                            var r = (byte)((cuint >> 8) & 0xf);
                            var g = (byte)((cuint >> 4) & 0xf);
                            var b = (byte)(cuint & 0xf);
                            r = (byte)(r << 4 | r);
                            g = (byte)(g << 4 | g);
                            b = (byte)(b << 4 | b);

                            return Color.FromArgb(255, r, g, b);
                        }

                    default: return ThrowFormatException();
                }
            }

            if (colorString.Length > 3 && colorString[0] == 's' && colorString[1] == 'c' && colorString[2] == '#')
            {
                var values = colorString.Split(',');

                if (values.Length == 4)
                {
                    var scA = double.Parse(values[0].Substring(3), CultureInfo.InvariantCulture);
                    var scR = double.Parse(values[1], CultureInfo.InvariantCulture);
                    var scG = double.Parse(values[2], CultureInfo.InvariantCulture);
                    var scB = double.Parse(values[3], CultureInfo.InvariantCulture);

                    return Color.FromArgb((byte)(scA * 255), (byte)(scR * 255), (byte)(scG * 255), (byte)(scB * 255));
                }

                if (values.Length == 3)
                {
                    var scR = double.Parse(values[0].Substring(3), CultureInfo.InvariantCulture);
                    var scG = double.Parse(values[1], CultureInfo.InvariantCulture);
                    var scB = double.Parse(values[2], CultureInfo.InvariantCulture);

                    return Color.FromArgb(255, (byte)(scR * 255), (byte)(scG * 255), (byte)(scB * 255));
                }

                return ThrowFormatException();
            }

            var prop = typeof(Colors).GetTypeInfo().GetDeclaredProperty(colorString);

            if (prop != null)
            {
                return (Color)prop.GetValue(null);
            }

            return ThrowFormatException();

            static void ThrowArgumentException() => throw new ArgumentException("The parameter \"colorString\" must not be null or empty.");
            static Color ThrowFormatException() => throw new FormatException("The parameter \"colorString\" is not a recognized Color format.");
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to an <see cref="HslColor"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert.</param>
        /// <returns>The converted <see cref="HslColor"/>.</returns>
        public static HslColor ToHsl(this Color color)
        {
            const double toDouble = 1.0 / 255;
            var r = toDouble * color.R;
            var g = toDouble * color.G;
            var b = toDouble * color.B;
            var max = Math.Max(Math.Max(r, g), b);
            var min = Math.Min(Math.Min(r, g), b);
            var chroma = max - min;
            double h1;

            if (chroma == 0)
            {
                h1 = 0;
            }
            else if (max == r)
            {
                // The % operator doesn't do proper modulo on negative
                // numbers, so we'll add 6 before using it
                h1 = (((g - b) / chroma) + 6) % 6;
            }
            else if (max == g)
            {
                h1 = 2 + ((b - r) / chroma);
            }
            else
            {
                h1 = 4 + ((r - g) / chroma);
            }

            double lightness = 0.5 * (max + min);
            double saturation = chroma == 0 ? 0 : chroma / (1 - Math.Abs((2 * lightness) - 1));
            HslColor ret;
            ret.H = 60 * h1;
            ret.S = saturation;
            ret.L = lightness;
            ret.A = toDouble * color.A;
            return ret;
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to an <see cref="HsvColor"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert.</param>
        /// <returns>The converted <see cref="HsvColor"/>.</returns>
        public static HsvColor ToHsv(this Color color)
        {
            const double toDouble = 1.0 / 255;
            var r = toDouble * color.R;
            var g = toDouble * color.G;
            var b = toDouble * color.B;
            var max = Math.Max(Math.Max(r, g), b);
            var min = Math.Min(Math.Min(r, g), b);
            var chroma = max - min;
            double h1;

            if (chroma == 0)
            {
                h1 = 0;
            }
            else if (max == r)
            {
                // The % operator doesn't do proper modulo on negative
                // numbers, so we'll add 6 before using it
                h1 = (((g - b) / chroma) + 6) % 6;
            }
            else if (max == g)
            {
                h1 = 2 + ((b - r) / chroma);
            }
            else
            {
                h1 = 4 + ((r - g) / chroma);
            }

            double saturation = chroma == 0 ? 0 : chroma / max;
            HsvColor ret;
            ret.H = 60 * h1;
            ret.S = saturation;
            ret.V = max;
            ret.A = toDouble * color.A;
            return ret;
        }

        /// <summary>
        /// Creates a <see cref="Color"/> from the specified hue, saturation, lightness, and alpha values.
        /// </summary>
        /// <param name="hue">0..360 range hue</param>
        /// <param name="saturation">0..1 range saturation</param>
        /// <param name="lightness">0..1 range lightness</param>
        /// <param name="alpha">0..1 alpha</param>
        /// <returns>The created <see cref="Color"/>.</returns>
        public static Color FromHsl(double hue, double saturation, double lightness, double alpha = 1.0)
        {
            if (hue < 0 || hue > 360)
            {
                throw new ArgumentOutOfRangeException(nameof(hue));
            }

            double chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
            double h1 = hue / 60;
            double x = chroma * (1 - Math.Abs((h1 % 2) - 1));
            double m = lightness - (0.5 * chroma);
            double r1, g1, b1;

            if (h1 < 1)
            {
                r1 = chroma;
                g1 = x;
                b1 = 0;
            }
            else if (h1 < 2)
            {
                r1 = x;
                g1 = chroma;
                b1 = 0;
            }
            else if (h1 < 3)
            {
                r1 = 0;
                g1 = chroma;
                b1 = x;
            }
            else if (h1 < 4)
            {
                r1 = 0;
                g1 = x;
                b1 = chroma;
            }
            else if (h1 < 5)
            {
                r1 = x;
                g1 = 0;
                b1 = chroma;
            }
            else
            {
                r1 = chroma;
                g1 = 0;
                b1 = x;
            }

            byte r = (byte)(255 * (r1 + m));
            byte g = (byte)(255 * (g1 + m));
            byte b = (byte)(255 * (b1 + m));
            byte a = (byte)(255 * alpha);

            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// Creates a <see cref="Color"/> from the specified hue, saturation, value, and alpha values.
        /// </summary>
        /// <param name="hue">0..360 range hue</param>
        /// <param name="saturation">0..1 range saturation</param>
        /// <param name="value">0..1 range value</param>
        /// <param name="alpha">0..1 alpha</param>
        /// <returns>The created <see cref="Color"/>.</returns>
        public static Color FromHsv(double hue, double saturation, double value, double alpha = 1.0)
        {
            if (hue < 0 || hue > 360)
            {
                throw new ArgumentOutOfRangeException(nameof(hue));
            }

            double chroma = value * saturation;
            double h1 = hue / 60;
            double x = chroma * (1 - Math.Abs((h1 % 2) - 1));
            double m = value - chroma;
            double r1, g1, b1;

            if (h1 < 1)
            {
                r1 = chroma;
                g1 = x;
                b1 = 0;
            }
            else if (h1 < 2)
            {
                r1 = x;
                g1 = chroma;
                b1 = 0;
            }
            else if (h1 < 3)
            {
                r1 = 0;
                g1 = chroma;
                b1 = x;
            }
            else if (h1 < 4)
            {
                r1 = 0;
                g1 = x;
                b1 = chroma;
            }
            else if (h1 < 5)
            {
                r1 = x;
                g1 = 0;
                b1 = chroma;
            }
            else
            {
                r1 = chroma;
                g1 = 0;
                b1 = x;
            }

            byte r = (byte)(255 * (r1 + m));
            byte g = (byte)(255 * (g1 + m));
            byte b = (byte)(255 * (b1 + m));
            byte a = (byte)(255 * alpha);

            return Color.FromArgb(a, r, g, b);
        }

        public static bool IsZeroOrLess(this double value) => value < Epsilon;
        public static bool IsZeroOrLess(this float value) => value < (float)Epsilon;
        public static bool IsZero(this double value) => Math.Abs(value) < Epsilon;
        public static bool IsZero(this float value) => Math.Abs(value) < (float)Epsilon;
        public static bool IsInvalid(this double value)
        {
            if (value == double.NaN || value == double.NegativeInfinity || value == double.PositiveInfinity)
                return true;

            return false;
        }
        public static bool IsInvalidOrZero(this double value)
        {
            if (value == double.NaN || value == double.NegativeInfinity || value == double.PositiveInfinity || value <= 0)
                return true;

            return false;
        }
        public static bool IsOne(this double value)
        {
            return Math.Abs(value) >= 1d - Epsilon && Math.Abs(value) <= 1d + Epsilon;
        }

        public static bool AreClose(this double left, double right)
        {
            if (left == right)
                return true;

            double a = (Math.Abs(left) + Math.Abs(right) + 10.0d) * Epsilon;
            double b = left - right;
            return (-a < b) && (a > b);
        }

        public static bool AreClose(this float left, float right)
        {
            if (left == right)
                return true;

            float a = (Math.Abs(left) + Math.Abs(right) + 10.0f) * (float)Epsilon;
            float b = left - right;
            return (-a < b) && (a > b);
        }

        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 64)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateRight(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft(this ulong value, int shiftBits)
        {
            int s = shiftBits & 63; // normalize to [0, 63]
            if (s == 0) { return value; }
            return (value << s) | (value >> (64 - s));
        }


        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 64)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateRight(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateRight(this ulong value, int shiftBits)
        {
            int s = shiftBits & 63; // normalize to [0, 63]
            if (s == 0) { return value; }
            return (value >> s) | (value << (64 - s));
        }

        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 32)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateLeft(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(this uint value, int shiftBits)
        {
            int s = shiftBits & 31; // normalize to [0,31]
            if (s == 0) { return value; }
            return ((value << s) | (value >> (32 - s)));
        }

        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 32)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateRight(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateRight(this uint value, int shiftBits)
        {
            int s = shiftBits & 31; // normalize to [0,31]
            if (s == 0) { return value; }
            return ((value >> s) | (value << (32 - s)));
        }

        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 16)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateLeft(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort RotateLeft(this ushort value, int shiftBits)
        {
            int s = shiftBits & 15; // normalize to [0,15]
            if (s == 0) { return value; }
            return (ushort)((value << s) | (value >> (16 - s)));
        }

        /// <summary>
        /// Mask the shift to avoid odd cases (e.g., negative values or counts ≥ 16)
        /// </summary>
        /// <remarks>
        /// In modern .NET, use System.Numerics.BitOperations.RotateRight(value, shiftBits)
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort RotateRight(this ushort value, int shiftBits)
        {
            int s = shiftBits & 15; // normalize to [0,15]
            if (s == 0) { return value; }
            return (ushort)((value >> s) | (value << (16 - s)));
        }


        /// <summary>
        /// Returns the biggest value from an <see cref="IComparable"/> set.
        /// Similar to Math.Max(), but this allows more than two input values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <returns>biggest comparable value</returns>
        /// <example>
        /// int biggest = Max(2, 41, 28, -11);
        /// </example>
        public static T Max<T>(params T[] values) where T : IComparable
        {
            T result = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (result.CompareTo(values[i]) < 0)
                    result = values[i];
            }
            return result;
        }

        /// <summary>
        /// Returns the smallest value from an <see cref="IComparable"/> set.
        /// Similar to Math.Min(), but this allows more than two input values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <returns>smallest comparable value</returns>
        /// <example>
        /// int smallest = Min(2, 41, 28, -11);
        /// </example>
        public static T Min<T>(params T[] values) where T : IComparable
        {
            T result = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (result.CompareTo(values[i]) > 0)
                    result = values[i];
            }
            return result;
        }

        /// <summary>
        /// Evaluate the median value of a list from an <see cref="IComparable{T}"/> set.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <example>
        /// List{int} list = new List{int}(new int[] { 2, 4, 9, 22, 3, 5, 9, 11, 0 });
        /// Console.WriteLine("Median: {0}", list.MedianValue());
        /// </example>
        public static T MedianValue<T>(this List<T> list) where T : IComparable<T>
        {
            return MedianValue<T>(list, -1);
        }

        /// <summary>
        /// Evaluate the median value of a list from an <see cref="IComparable{T}"/> set.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="position"></param>
        /// <example>
        /// List{int} list = new List{int}(new int[] { 2, 4, 9, 22, 3, 5, 9, 11, 0 });
        /// Console.WriteLine("Median: {0}", list.MedianValue(5));
        /// </example>
        public static T MedianValue<T>(this List<T> list, int position) where T : IComparable<T>
        {
            if (position < 0)
                position = list.Count / 2;

            T guess = list[0];

            if (list.Count == 1)
                return guess;

            List<T> lowList = new List<T>();
            List<T> highList = new List<T>();

            for (int i = 1; i < list.Count; i++)
            {
                T value = list[i];
                if (guess.CompareTo(value) <= 0) // Value is higher than or equal to the current guess.
                    highList.Add(value);
                else // Value is lower than the current guess.
                    lowList.Add(value);
            }

            if (lowList.Count > position) // Median value must be in the lower-than list.
                return MedianValue(lowList, position);
            else if (lowList.Count < position) // Median value must be in the higher-than list.
                return MedianValue(highList, position - lowList.Count - 1);
            else // Guess is correct.
                return guess;
        }

        /// <summary>
        /// Clamping function for any value of type <see cref="IComparable{T}"/>.
        /// </summary>
        /// <param name="val">initial value</param>
        /// <param name="min">lowest range</param>
        /// <param name="max">highest range</param>
        /// <returns>clamped value</returns>
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            return val.CompareTo(min) < 0 ? min : (val.CompareTo(max) > 0 ? max : val);
        }

        /// <summary>
        /// Scale a range of numbers. [baseMin to baseMax] will become [limitMin to limitMax]
        /// </summary>
        public static double Scale(this double valueIn, double baseMin, double baseMax, double limitMin, double limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
        public static float Scale(this float valueIn, float baseMin, float baseMax, float limitMin, float limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
        public static int Scale(this int valueIn, int baseMin, int baseMax, int limitMin, int limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;

        /// <summary>
        /// LERP a range of numbers.
        /// </summary>
        public static double Lerp(this double start, double end, double amount = 0.5D) => start + (end - start) * amount;
        public static float Lerp(this float start, float end, float amount = 0.5F) => start + (end - start) * amount;

        public static float LogLerp(this float start, float end, float percent, float logBase = 1.2F) => start + (end - start) * (float)Math.Log(percent, logBase);
        public static double LogLerp(this double start, double end, double percent, double logBase = 1.2F) => start + (end - start) * Math.Log(percent, logBase);

        public static int MapValue(this int val, int inMin, int inMax, int outMin, int outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
        public static float MapValue(this float val, float inMin, float inMax, float outMin, float outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
        public static double MapValue(this double val, double inMin, double inMax, double outMin, double outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;


        /// <summary>
        /// Display a readable sentence as to when the time will happen.
        /// e.g. "in one second" or "in 2 days"
        /// </summary>
        /// <param name="value"><see cref="TimeSpan"/>the future time to compare from now</param>
        /// <returns>human friendly format</returns>
        public static string ToReadableTime(this TimeSpan value, bool reportMilliseconds = false)
        {
            double delta = value.TotalSeconds;
            if (delta < 1 && !reportMilliseconds) { return "less than one second"; }
            if (delta < 1 && reportMilliseconds) { return $"{value.TotalMilliseconds:N1} milliseconds"; }
            if (delta < 60) { return value.Seconds == 1 ? "one second" : value.Seconds + " seconds"; }
            if (delta < 120) { return "a minute"; }                  // 2 * 60
            if (delta < 3000) { return value.Minutes + " minutes"; } // 50 * 60
            if (delta < 5400) { return "an hour"; }                  // 90 * 60
            if (delta < 86400) { return value.Hours + " hours"; }    // 24 * 60 * 60
            if (delta < 172800) { return "one day"; }                // 48 * 60 * 60
            if (delta < 2592000) { return value.Days + " days"; }    // 30 * 24 * 60 * 60
            if (delta < 31104000)                                    // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)value.Days / 30));
                return months <= 1 ? "one month" : months + " months";
            }
            int years = Convert.ToInt32(Math.Floor((double)value.Days / 365));
            return years <= 1 ? "one year" : years + " years";
        }

        public static void SaveWriteableBitmap(this WriteableBitmap wb, string filePath)
        {
            var encoder = new PngBitmapEncoder(); // Or JpegBitmapEncoder, BmpBitmapEncoder, etc.
            encoder.Frames.Add(BitmapFrame.Create(wb)); // Just one frame, the bitmap itself.
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }
    }

    /// <summary>
    /// Defines a color in Hue/Saturation/Value (HSV) space.
    /// </summary>
    public struct HsvColor
    {
        /// <summary>
        /// The Hue in 0..360 range.
        /// </summary>
        public double H;

        /// <summary>
        /// The Saturation in 0..1 range.
        /// </summary>
        public double S;

        /// <summary>
        /// The Value in 0..1 range.
        /// </summary>
        public double V;

        /// <summary>
        /// The Alpha/opacity in 0..1 range.
        /// </summary>
        public double A;
    }

    /// <summary>
    /// Defines a color in Hue/Saturation/Lightness (HSL) space.
    /// </summary>
    public struct HslColor
    {
        /// <summary>
        /// The Hue in 0..360 range.
        /// </summary>
        public double H;

        /// <summary>
        /// The Saturation in 0..1 range.
        /// </summary>
        public double S;

        /// <summary>
        /// The Lightness in 0..1 range.
        /// </summary>
        public double L;

        /// <summary>
        /// The Alpha/opacity in 0..1 range.
        /// </summary>
        public double A;
    }
}
