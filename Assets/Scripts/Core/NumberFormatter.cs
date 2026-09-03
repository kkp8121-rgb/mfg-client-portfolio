namespace MkLike.Core
{
    /// <summary>
    /// 한국식 큰 수 포맷팅 유틸리티 (만/억/조/경).
    /// Core 어셈블리에 위치하여 모든 어셈블리에서 공통 사용 가능.
    /// </summary>
    public static class NumberFormatter
    {
        private const long UNIT_MAN = 10_000L;                    // 만
        private const long UNIT_EUK = 100_000_000L;               // 억
        private const long UNIT_JO = 1_000_000_000_000L;          // 조
        private const long UNIT_GYEONG = 10_000_000_000_000_000L; // 경

        private const double UNIT_MAN_D = 10_000.0;
        private const double UNIT_EUK_D = 100_000_000.0;
        private const double UNIT_JO_D = 1_000_000_000_000.0;
        private const double UNIT_GYEONG_D = 10_000_000_000_000_000.0;

        /// <summary>
        /// long 값을 한국식 큰 수로 포맷한다.
        /// 예: 12345 → "1.2만", 123456789 → "1.2억"
        /// </summary>
        public static string FormatKorean(long value)
        {
            if (value == 0L) return "0";

            bool isNegative = value < 0;
            long abs = isNegative ? -value : value;
            string prefix = isNegative ? "-" : "";

            if (abs < UNIT_MAN)
                return $"{prefix}{abs:#,0}";

            if (abs < UNIT_EUK)
            {
                double v = abs / UNIT_MAN_D;
                return $"{prefix}{v:0.#}만";
            }

            if (abs < UNIT_JO)
            {
                double v = abs / UNIT_EUK_D;
                return $"{prefix}{v:0.#}억";
            }

            if (abs < UNIT_GYEONG)
            {
                double v = abs / UNIT_JO_D;
                return $"{prefix}{v:0.#}조";
            }

            {
                double v = abs / UNIT_GYEONG_D;
                return $"{prefix}{v:0.#}경";
            }
        }

        /// <summary>
        /// BigNumber 값을 한국식 큰 수로 포맷한다. 큰 범위(경 이상)도 지원.
        /// </summary>
        public static string FormatKorean(BigNumber value)
        {
            return value.ToKoreanShort(1);
        }

        /// <summary>
        /// double 값을 한국식 큰 수로 포맷한다.
        /// </summary>
        public static string FormatKorean(double value)
        {
            if (value == 0.0) return "0";

            bool isNegative = value < 0;
            double abs = isNegative ? -value : value;
            string prefix = isNegative ? "-" : "";

            if (abs < UNIT_MAN_D)
                return $"{prefix}{abs:#,0}";

            if (abs < UNIT_EUK_D)
            {
                double v = abs / UNIT_MAN_D;
                return $"{prefix}{v:0.#}만";
            }

            if (abs < UNIT_JO_D)
            {
                double v = abs / UNIT_EUK_D;
                return $"{prefix}{v:0.#}억";
            }

            if (abs < UNIT_GYEONG_D)
            {
                double v = abs / UNIT_JO_D;
                return $"{prefix}{v:0.#}조";
            }

            {
                double v = abs / UNIT_GYEONG_D;
                return $"{prefix}{v:0.#}경";
            }
        }
    }
}
