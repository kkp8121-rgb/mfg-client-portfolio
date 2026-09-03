using System;
using BreakInfinity;

namespace MkLike.Core
{
    /// <summary>
    /// 키우기 게임용 대용량 수치 래퍼. BreakInfinity.BigDouble 위에 한국식 표시 포맷을 얹는다.
    /// long/int와 암시적 변환 + 사칙연산 지원. Save 직렬화는 ToString()/Parse() 사용.
    /// </summary>
    [Serializable]
    public struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
    {
        // ── 한국 단위 상수 (10^4 단위 블록) ──
        private static readonly string[] KOREAN_UNITS = { "", "만", "억", "조", "경", "해", "자", "양", "구", "간", "정", "재", "극" };

        private BigDouble _value;

        public BigDouble Raw => _value;

        public BigNumber(BigDouble value) { _value = value; }
        public BigNumber(double value) { _value = new BigDouble(value); }
        public BigNumber(long value) { _value = new BigDouble(value); }
        public BigNumber(double mantissa, long exponent) { _value = new BigDouble(mantissa, exponent); }

        public static BigNumber Zero => new BigNumber(BigDouble.Zero);
        public static BigNumber One => new BigNumber(BigDouble.One);

        public double Mantissa => _value.Mantissa;
        public long Exponent => _value.Exponent;

        public bool IsZero => _value == BigDouble.Zero;
        public bool IsNegative => _value < BigDouble.Zero;

        // ── 암시적 변환 ──
        public static implicit operator BigNumber(long value) => new BigNumber(value);
        public static implicit operator BigNumber(int value) => new BigNumber(value);
        public static implicit operator BigNumber(double value) => new BigNumber(value);
        public static implicit operator BigNumber(float value) => new BigNumber(value);
        public static implicit operator BigNumber(BigDouble value) => new BigNumber(value);
        public static implicit operator BigDouble(BigNumber value) => value._value;

        // ── 사칙연산 ──
        public static BigNumber operator +(BigNumber a, BigNumber b) => new BigNumber(a._value + b._value);
        public static BigNumber operator -(BigNumber a, BigNumber b) => new BigNumber(a._value - b._value);
        public static BigNumber operator *(BigNumber a, BigNumber b) => new BigNumber(a._value * b._value);
        public static BigNumber operator /(BigNumber a, BigNumber b) => new BigNumber(a._value / b._value);
        public static BigNumber operator -(BigNumber v) => new BigNumber(-v._value);

        public static bool operator <(BigNumber a, BigNumber b) => a._value < b._value;
        public static bool operator >(BigNumber a, BigNumber b) => a._value > b._value;
        public static bool operator <=(BigNumber a, BigNumber b) => a._value <= b._value;
        public static bool operator >=(BigNumber a, BigNumber b) => a._value >= b._value;
        public static bool operator ==(BigNumber a, BigNumber b) => a._value == b._value;
        public static bool operator !=(BigNumber a, BigNumber b) => a._value != b._value;

        // ── 안전 변환 (오버플로우 시 상한/하한 반환) ──
        public long ToLongClamped()
        {
            double d = _value.ToDouble();
            if (d >= long.MaxValue) return long.MaxValue;
            if (d <= long.MinValue) return long.MinValue;
            return (long)d;
        }

        public int ToIntClamped()
        {
            double d = _value.ToDouble();
            if (d >= int.MaxValue) return int.MaxValue;
            if (d <= int.MinValue) return int.MinValue;
            return (int)d;
        }

        public double ToDouble() => _value.ToDouble();
        public float ToFloatClamped()
        {
            double d = _value.ToDouble();
            if (d >= float.MaxValue) return float.MaxValue;
            if (d <= float.MinValue) return float.MinValue;
            return (float)d;
        }

        public int CompareTo(BigNumber other) => _value.CompareTo(other._value);
        public bool Equals(BigNumber other) => _value.Equals(other._value);
        public override bool Equals(object obj) => obj is BigNumber bn && Equals(bn);
        public override int GetHashCode() => _value.GetHashCode();

        // ── 직렬화용 (e-표기) ──
        public override string ToString() => _value.ToString("R");
        public string ToSerializedString() => _value.ToString("R");
        public static BigNumber Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return Zero;
            return new BigNumber(BigDouble.Parse(s));
        }

        public static bool TryParse(string s, out BigNumber result)
        {
            try { result = Parse(s); return true; }
            catch { result = Zero; return false; }
        }

        /// <summary>
        /// 한국식 큰 수 포맷 (만/억/조/경/해/자/...)
        /// 예: 12345 → "1.23만", 1.5e18 → "150경"
        /// </summary>
        public string ToKoreanShort(int decimals = 2)
        {
            if (BigDouble.IsNaN(_value)) return "NaN";
            if (_value == BigDouble.Zero) return "0";

            bool isNeg = _value < BigDouble.Zero;
            BigDouble abs = isNeg ? -_value : _value;
            string prefix = isNeg ? "-" : "";

            // 만 미만
            if (abs < new BigDouble(10000))
            {
                double d = abs.ToDouble();
                return prefix + d.ToString("#,0");
            }

            // 만 단위 블록 인덱스 계산 (10^4 단위)
            // exponent 기준으로 단위 인덱스 = floor(log10(abs) / 4)
            double log10 = BigDouble.Log10(abs);
            int unitIdx = (int)Math.Floor(log10 / 4.0);

            if (unitIdx >= KOREAN_UNITS.Length)
            {
                // 사전 단위 초과 시 E 표기로 폴백
                return prefix + _value.ToString("E" + decimals);
            }

            // abs / 10^(4*unitIdx)
            BigDouble divisor = BigDouble.Pow10((long)(unitIdx * 4));
            BigDouble scaled = abs / divisor;
            double scaledD = scaled.ToDouble();

            string unit = KOREAN_UNITS[unitIdx];
            string numStr = scaledD.ToString("0." + new string('#', decimals));
            return prefix + numStr + unit;
        }

        /// <summary>정수 포맷 (쉼표 구분, 작은 수 전용). 큰 수에는 ToKoreanShort 권장.</summary>
        public string ToCommaString()
        {
            if (_value.Exponent > 18) return ToKoreanShort();
            double d = _value.ToDouble();
            return d.ToString("#,0");
        }
    }
}
