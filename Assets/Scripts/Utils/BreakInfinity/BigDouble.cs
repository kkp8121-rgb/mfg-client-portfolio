using System;
using System.Globalization;
using Random = System.Random;

// Source: https://github.com/Razenpok/BreakInfinity.cs (MIT License)
// Adapted for mkLike — single-file drop-in. Extensions moved to BigDoubleExtensions.cs.

#if UNITY_2017_1_OR_NEWER
using UnityEngine;
#endif

namespace BreakInfinity
{
#if UNITY_2017_1_OR_NEWER
    [Serializable]
#endif
    public struct BigDouble : IFormattable, IComparable, IComparable<BigDouble>, IEquatable<BigDouble>
    {
        public const double Tolerance = 1e-18;

        private const int MaxSignificantDigits = 17;
        private const long ExpLimit = long.MaxValue;
        private const long DoubleExpMax = 308;
        private const long DoubleExpMin = -324;

#if UNITY_2017_1_OR_NEWER
        [SerializeField]
        private double mantissa;
        [SerializeField]
        private long exponent;
#else
        private double mantissa;
        private long exponent;
#endif

        private BigDouble(double mantissa, long exponent, PrivateConstructorArg _)
        {
            this.mantissa = mantissa;
            this.exponent = exponent;
        }

        public BigDouble(double mantissa, long exponent)
        {
            this = Normalize(mantissa, exponent);
        }

        public BigDouble(BigDouble other)
        {
            mantissa = other.mantissa;
            exponent = other.exponent;
        }

        public BigDouble(double value)
        {
            if (double.IsNaN(value)) { this = NaN; }
            else if (double.IsPositiveInfinity(value)) { this = PositiveInfinity; }
            else if (double.IsNegativeInfinity(value)) { this = NegativeInfinity; }
            else if (IsZero(value)) { this = Zero; }
            else { this = Normalize(value, 0); }
        }

        public static BigDouble Normalize(double mantissa, long exponent)
        {
            if (mantissa >= 1 && mantissa < 10 || !IsFinite(mantissa))
                return FromMantissaExponentNoNormalize(mantissa, exponent);
            if (IsZero(mantissa))
                return Zero;

            var tempExponent = (long)Math.Floor(Math.Log10(Math.Abs(mantissa)));
            if (tempExponent == DoubleExpMin)
                mantissa = mantissa * 10 / 1e-323;
            else
                mantissa = mantissa / PowersOf10.Lookup(tempExponent);

            return FromMantissaExponentNoNormalize(mantissa, exponent + tempExponent);
        }

        public double Mantissa => mantissa;
        public long Exponent => exponent;

        public static BigDouble FromMantissaExponentNoNormalize(double mantissa, long exponent)
            => new BigDouble(mantissa, exponent, new PrivateConstructorArg());

        public static BigDouble Zero = FromMantissaExponentNoNormalize(0, 0);
        public static BigDouble One = FromMantissaExponentNoNormalize(1, 0);
        public static BigDouble NaN = FromMantissaExponentNoNormalize(double.NaN, long.MinValue);
        public static BigDouble PositiveInfinity = FromMantissaExponentNoNormalize(double.PositiveInfinity, 0);
        public static BigDouble NegativeInfinity = FromMantissaExponentNoNormalize(double.NegativeInfinity, 0);

        public static bool IsNaN(BigDouble value) => double.IsNaN(value.Mantissa);
        public static bool IsPositiveInfinity(BigDouble value) => double.IsPositiveInfinity(value.Mantissa);
        public static bool IsNegativeInfinity(BigDouble value) => double.IsNegativeInfinity(value.Mantissa);
        public static bool IsInfinity(BigDouble value) => double.IsInfinity(value.Mantissa);

        public static BigDouble Parse(string value)
        {
            if (value.IndexOf('e') != -1)
            {
                var parts = value.Split('e');
                var m = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var e = long.Parse(parts[1], CultureInfo.InvariantCulture);
                return Normalize(m, e);
            }
            if (value == "NaN") return NaN;
            var result = new BigDouble(double.Parse(value, CultureInfo.InvariantCulture));
            if (IsNaN(result)) throw new Exception("Invalid argument: " + value);
            return result;
        }

        public double ToDouble()
        {
            if (IsNaN(this)) return double.NaN;
            if (Exponent > DoubleExpMax) return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (Exponent < DoubleExpMin) return 0.0;
            if (Exponent == DoubleExpMin) return Mantissa > 0 ? 5e-324 : -5e-324;

            var result = Mantissa * PowersOf10.Lookup(Exponent);
            if (!IsFinite(result) || Exponent < 0) return result;

            var resultrounded = Math.Round(result);
            if (Math.Abs(resultrounded - result) < 1e-10) return resultrounded;
            return result;
        }

        public override string ToString() => BigNumberFormatter.FormatBigDouble(this, null, null);
        public string ToString(string format) => BigNumberFormatter.FormatBigDouble(this, format, null);
        public string ToString(string format, IFormatProvider formatProvider)
            => BigNumberFormatter.FormatBigDouble(this, format, formatProvider);

        public static BigDouble Abs(BigDouble value)
            => FromMantissaExponentNoNormalize(Math.Abs(value.Mantissa), value.Exponent);
        public static BigDouble Negate(BigDouble value)
            => FromMantissaExponentNoNormalize(-value.Mantissa, value.Exponent);
        public static int Sign(BigDouble value) => Math.Sign(value.Mantissa);

        public static BigDouble Round(BigDouble value)
        {
            if (IsNaN(value)) return value;
            if (value.Exponent < -1) return Zero;
            if (value.Exponent < MaxSignificantDigits) return new BigDouble(Math.Round(value.ToDouble()));
            return value;
        }

        public static BigDouble Floor(BigDouble value)
        {
            if (IsNaN(value)) return value;
            if (value.Exponent < -1) return Math.Sign(value.Mantissa) >= 0 ? Zero : -One;
            if (value.Exponent < MaxSignificantDigits) return new BigDouble(Math.Floor(value.ToDouble()));
            return value;
        }

        public static BigDouble Ceiling(BigDouble value)
        {
            if (IsNaN(value)) return value;
            if (value.Exponent < -1) return Math.Sign(value.Mantissa) > 0 ? One : Zero;
            if (value.Exponent < MaxSignificantDigits) return new BigDouble(Math.Ceiling(value.ToDouble()));
            return value;
        }

        public static BigDouble Truncate(BigDouble value)
        {
            if (IsNaN(value)) return value;
            if (value.Exponent < 0) return Zero;
            if (value.Exponent < MaxSignificantDigits) return new BigDouble(Math.Truncate(value.ToDouble()));
            return value;
        }

        public static BigDouble Add(BigDouble left, BigDouble right)
        {
            if (IsZero(left.Mantissa)) return right;
            if (IsZero(right.Mantissa)) return left;
            if (IsNaN(left) || IsNaN(right) || IsInfinity(left) || IsInfinity(right))
                return left.Mantissa + right.Mantissa;

            BigDouble bigger, smaller;
            if (left.Exponent >= right.Exponent) { bigger = left; smaller = right; }
            else { bigger = right; smaller = left; }

            if (bigger.Exponent - smaller.Exponent > MaxSignificantDigits) return bigger;

            return Normalize(
                Math.Round(1e14 * bigger.Mantissa + 1e14 * smaller.Mantissa *
                           PowersOf10.Lookup(smaller.Exponent - bigger.Exponent)),
                bigger.Exponent - 14);
        }

        public static BigDouble Subtract(BigDouble left, BigDouble right) => left + -right;
        public static BigDouble Multiply(BigDouble left, BigDouble right)
            => Normalize(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent);
        public static BigDouble Divide(BigDouble left, BigDouble right) => left * Reciprocate(right);
        public static BigDouble Reciprocate(BigDouble value)
            => Normalize(1.0 / value.Mantissa, -value.Exponent);

        public static implicit operator BigDouble(double value) => new BigDouble(value);
        public static implicit operator BigDouble(int value) => new BigDouble(value);
        public static implicit operator BigDouble(long value) => new BigDouble(value);
        public static implicit operator BigDouble(float value) => new BigDouble(value);

        public static BigDouble operator -(BigDouble value) => Negate(value);
        public static BigDouble operator +(BigDouble left, BigDouble right) => Add(left, right);
        public static BigDouble operator -(BigDouble left, BigDouble right) => Subtract(left, right);
        public static BigDouble operator *(BigDouble left, BigDouble right) => Multiply(left, right);
        public static BigDouble operator /(BigDouble left, BigDouble right) => Divide(left, right);
        public static BigDouble operator ++(BigDouble value) => Add(value, 1);
        public static BigDouble operator --(BigDouble value) => Subtract(value, 1);

        public int CompareTo(object other)
        {
            if (other == null) return 1;
            if (other is BigDouble bd) return CompareTo(bd);
            throw new ArgumentException("The parameter must be a BigDouble.");
        }

        public int CompareTo(BigDouble other)
        {
            if (IsZero(Mantissa) || IsZero(other.Mantissa) || IsNaN(this) || IsNaN(other)
                || IsInfinity(this) || IsInfinity(other))
                return Mantissa.CompareTo(other.Mantissa);
            if (Mantissa > 0 && other.Mantissa < 0) return 1;
            if (Mantissa < 0 && other.Mantissa > 0) return -1;

            var exponentComparison = Exponent.CompareTo(other.Exponent);
            return exponentComparison != 0
                ? (Mantissa > 0 ? exponentComparison : -exponentComparison)
                : Mantissa.CompareTo(other.Mantissa);
        }

        public override bool Equals(object other) => other is BigDouble bd && Equals(bd);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent.GetHashCode();
            }
        }

        public bool Equals(BigDouble other)
            => !IsNaN(this) && !IsNaN(other) && (AreSameInfinity(this, other)
                || Exponent == other.Exponent && AreEqual(Mantissa, other.Mantissa));

        public bool Equals(BigDouble other, double tolerance)
            => !IsNaN(this) && !IsNaN(other) && (AreSameInfinity(this, other)
                || Abs(this - other) <= Max(Abs(this), Abs(other)) * tolerance);

        private static bool AreSameInfinity(BigDouble first, BigDouble second)
            => IsPositiveInfinity(first) && IsPositiveInfinity(second)
                || IsNegativeInfinity(first) && IsNegativeInfinity(second);

        public static bool operator ==(BigDouble left, BigDouble right) => left.Equals(right);
        public static bool operator !=(BigDouble left, BigDouble right) => !(left == right);

        public static bool operator <(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b)) return false;
            if (IsZero(a.Mantissa)) return b.Mantissa > 0;
            if (IsZero(b.Mantissa)) return a.Mantissa < 0;
            if (a.Exponent == b.Exponent) return a.Mantissa < b.Mantissa;
            if (a.Mantissa > 0) return b.Mantissa > 0 && a.Exponent < b.Exponent;
            return b.Mantissa > 0 || a.Exponent > b.Exponent;
        }

        public static bool operator <=(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b)) return false;
            return !(a > b);
        }

        public static bool operator >(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b)) return false;
            if (IsZero(a.Mantissa)) return b.Mantissa < 0;
            if (IsZero(b.Mantissa)) return a.Mantissa > 0;
            if (a.Exponent == b.Exponent) return a.Mantissa > b.Mantissa;
            if (a.Mantissa > 0) return b.Mantissa < 0 || a.Exponent > b.Exponent;
            return b.Mantissa < 0 && a.Exponent < b.Exponent;
        }

        public static bool operator >=(BigDouble a, BigDouble b)
        {
            if (IsNaN(a) || IsNaN(b)) return false;
            return !(a < b);
        }

        public static BigDouble Max(BigDouble left, BigDouble right)
        {
            if (IsNaN(left) || IsNaN(right)) return NaN;
            return left > right ? left : right;
        }

        public static BigDouble Min(BigDouble left, BigDouble right)
        {
            if (IsNaN(left) || IsNaN(right)) return NaN;
            return left > right ? right : left;
        }

        public static double AbsLog10(BigDouble value)
            => value.Exponent + Math.Log10(Math.Abs(value.Mantissa));
        public static double Log10(BigDouble value)
            => value.Exponent + Math.Log10(value.Mantissa);

        public static double Log(BigDouble value, BigDouble @base) => Log(value, @base.ToDouble());
        public static double Log(BigDouble value, double @base)
        {
            if (IsZero(@base)) return double.NaN;
            return 2.30258509299404568402 / Math.Log(@base) * Log10(value);
        }

        public static double Log2(BigDouble value) => 3.32192809488736234787 * Log10(value);
        public static double Ln(BigDouble value) => 2.30258509299404568402 * Log10(value);

        public static BigDouble Pow10(double power)
            => IsInteger(power) ? Pow10((long)power)
                                : Normalize(Math.Pow(10, power % 1), (long)Math.Truncate(power));

        public static BigDouble Pow10(long power) => FromMantissaExponentNoNormalize(1, power);

        public static BigDouble Pow(BigDouble value, BigDouble power) => Pow(value, power.ToDouble());

        public static BigDouble Pow(BigDouble value, long power)
        {
            if (Is10(value)) return Pow10(power);
            var m = Math.Pow(value.Mantissa, power);
            if (double.IsInfinity(m)) return Pow(Pow(value, 2), (double)power / 2);
            return Normalize(m, value.Exponent * power);
        }

        public static BigDouble Pow(BigDouble value, double power)
        {
            var powerIsInteger = IsInteger(power);
            if (value < 0 && !powerIsInteger) return NaN;
            return Is10(value) && powerIsInteger ? Pow10(power) : PowInternal(value, power);
        }

        private static bool Is10(BigDouble value)
            => value.Exponent == 1 && value.Mantissa - 1 < double.Epsilon;

        private static BigDouble PowInternal(BigDouble value, double other)
        {
            var temp = value.Exponent * other;
            double newMantissa;
            if (IsInteger(temp) && IsFinite(temp) && Math.Abs(temp) < ExpLimit)
            {
                newMantissa = Math.Pow(value.Mantissa, other);
                if (IsFinite(newMantissa)) return Normalize(newMantissa, (long)temp);
            }

            var newexponent = Math.Truncate(temp);
            var residue = temp - newexponent;
            newMantissa = Math.Pow(10, other * Math.Log10(value.Mantissa) + residue);
            if (IsFinite(newMantissa)) return Normalize(newMantissa, (long)newexponent);

            var result = Pow10(other * AbsLog10(value));
            if (Sign(value) == -1 && AreEqual(other % 2, 1)) return -result;
            return result;
        }

        public static BigDouble Exp(BigDouble value) => Pow(2.71828182845904523536, value);

        public static BigDouble Sqrt(BigDouble value)
        {
            if (value.Mantissa < 0) return new BigDouble(double.NaN);
            if (value.Exponent % 2 != 0)
                return Normalize(Math.Sqrt(value.Mantissa) * 3.16227766016838, (long)Math.Floor(value.Exponent / 2.0));
            return Normalize(Math.Sqrt(value.Mantissa), (long)Math.Floor(value.Exponent / 2.0));
        }

        private static bool IsZero(double value) => Math.Abs(value) < double.Epsilon;
        private static bool AreEqual(double first, double second) => Math.Abs(first - second) < Tolerance;
        private static bool IsInteger(double value) => IsZero(Math.Abs(value % 1));
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static class BigNumberFormatter
        {
            public static string FormatBigDouble(BigDouble value, string format, IFormatProvider formatProvider)
            {
                if (IsNaN(value)) return "NaN";
                if (value.Exponent >= ExpLimit)
                    return value.Mantissa > 0 ? "Infinity" : "-Infinity";

                int formatDigits;
                var formatSpecifier = ParseFormatSpecifier(format, out formatDigits);
                switch (formatSpecifier)
                {
                    case 'R':
                    case 'G': return FormatGeneral(value, formatDigits);
                    case 'E': return FormatExponential(value, formatDigits);
                    case 'F': return FormatFixed(value, formatDigits);
                }
                throw new FormatException($"Unknown string format '{formatSpecifier}'");
            }

            private static char ParseFormatSpecifier(string format, out int digits)
            {
                const char customFormat = (char)0;
                digits = -1;
                if (string.IsNullOrEmpty(format)) return 'R';

                var i = 0;
                var ch = format[i];
                if ((ch < 'A' || ch > 'Z') && (ch < 'a' || ch > 'z')) return customFormat;

                i++;
                var n = -1;
                if (i < format.Length && format[i] >= '0' && format[i] <= '9')
                {
                    n = format[i++] - '0';
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                    {
                        n = n * 10 + (format[i++] - '0');
                        if (n >= 10) break;
                    }
                }
                if (i < format.Length && format[i] != '\0') return customFormat;
                digits = n;
                return ch;
            }

            private static string FormatGeneral(BigDouble value, int places)
            {
                if (value.Exponent <= -ExpLimit || IsZero(value.Mantissa)) return "0";
                var format = places > 0 ? $"G{places}" : "G";
                if (value.Exponent < 21 && value.Exponent > -7)
                    return value.ToDouble().ToString(format, CultureInfo.InvariantCulture);
                return value.Mantissa.ToString(format, CultureInfo.InvariantCulture)
                       + "E" + (value.Exponent >= 0 ? "+" : "")
                       + value.Exponent.ToString(CultureInfo.InvariantCulture);
            }

            private static string ToFixed(double value, int places)
                => value.ToString($"F{places}", CultureInfo.InvariantCulture);

            private static string FormatExponential(BigDouble value, int places)
            {
                if (value.Exponent <= -ExpLimit || IsZero(value.Mantissa))
                    return "0" + (places > 0 ? ".".PadRight(places + 1, '0') : "") + "E+0";

                var len = (places >= 0 ? places : MaxSignificantDigits) + 1;
                var numDigits = (int)Math.Ceiling(Math.Log10(Math.Abs(value.Mantissa)));
                var rounded = Math.Round(value.Mantissa * Math.Pow(10, len - numDigits)) * Math.Pow(10, numDigits - len);

                var mantissa = ToFixed(rounded, Math.Max(len - numDigits, 0));
                if (mantissa != "0" && places < 0) mantissa = mantissa.TrimEnd('0', '.');
                return mantissa + "E" + (value.Exponent >= 0 ? "+" : "") + value.Exponent;
            }

            private static string FormatFixed(BigDouble value, int places)
            {
                if (places < 0) places = MaxSignificantDigits;
                if (value.Exponent <= -ExpLimit || IsZero(value.Mantissa))
                    return "0" + (places > 0 ? ".".PadRight(places + 1, '0') : "");

                if (value.Exponent >= MaxSignificantDigits)
                {
                    return value.Mantissa
                        .ToString(CultureInfo.InvariantCulture)
                        .Replace(".", "")
                        .PadRight((int)value.Exponent + 1, '0')
                        + (places > 0 ? ".".PadRight(places + 1, '0') : "");
                }
                return ToFixed(value.ToDouble(), places);
            }
        }

        private static class PowersOf10
        {
            private static double[] Powers { get; } = new double[DoubleExpMax - DoubleExpMin];
            private const long IndexOf0 = -DoubleExpMin - 1;

            static PowersOf10()
            {
                var index = 0;
                for (var i = 0; i < Powers.Length; i++)
                    Powers[index++] = double.Parse("1e" + (i - IndexOf0), CultureInfo.InvariantCulture);
            }

            public static double Lookup(long power) => Powers[IndexOf0 + power];
        }

        private struct PrivateConstructorArg { }
    }
}
