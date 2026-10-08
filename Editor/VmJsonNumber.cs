using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace VMUnityAutomation.Editor
{
    /// <summary>Converts a JSON numeric lexeme with exact binary64 rounding.</summary>
    public static class VmJsonNumber
    {
        private const int RetainedDigits = 769;

        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) throw InvalidNumber();
            if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0 && text.IndexOf('E') < 0)
            {
                int start = text[0] == '-' ? 1 : 0;
                if (start == text.Length || text[start] == '0' && text.Length - start != 1) throw InvalidNumber();
                for (int index = start; index < text.Length; index++)
                    if (!IsDigit(text[index])) throw InvalidNumber();
                if (text != "-0" && long.TryParse(text, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out long integer)) return integer;
            }
            return ParseDouble(text);
        }

        public static string FormatDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw NonFiniteNumber();
            if (value == 0 && BitConverter.DoubleToInt64Bits(value) < 0) return "-0.0";
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static double ParseDouble(string text)
        {
            if (string.IsNullOrEmpty(text)) throw InvalidNumber();
            int index = 0;
            bool negative = text[0] == '-';
            if (negative) index++;
            if (index == text.Length || !IsDigit(text[index])) throw InvalidNumber();
            var prefix = new StringBuilder(Math.Min(RetainedDigits, text.Length));
            int significantDigits = 0;
            bool nonzeroTail = false;
            int integerStart = index;
            ReadDigits(text, ref index, prefix, ref significantDigits, ref nonzeroTail);
            if (text[integerStart] == '0' && index - integerStart != 1) throw InvalidNumber();
            int fractionDigits = 0;
            if (index < text.Length && text[index] == '.')
            {
                int fractionStart = ++index;
                ReadDigits(text, ref index, prefix, ref significantDigits, ref nonzeroTail);
                fractionDigits = index - fractionStart;
                if (fractionDigits == 0) throw InvalidNumber();
            }
            int exponentStart = -1;
            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                exponentStart = ++index;
                if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                int digitsStart = index;
                while (index < text.Length && IsDigit(text[index])) index++;
                if (index == digitsStart) throw InvalidNumber();
            }
            if (index != text.Length) throw InvalidNumber();
            long sign = negative ? long.MinValue : 0;
            if (significantDigits == 0) return BitConverter.Int64BitsToDouble(sign);
            long exponent = 0;
            if (exponentStart >= 0)
            {
                string exponentText = text.Substring(exponentStart);
                if (!long.TryParse(exponentText, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out exponent))
                {
                    if (exponentText[0] == '-') return BitConverter.Int64BitsToDouble(sign);
                    throw NonFiniteNumber();
                }
            }
            // These classifications precede exponent arithmetic; neither can affect
            // a finite nonzero binary64 value, even with the full source mantissa.
            if (exponent > (long)text.Length + 309) throw NonFiniteNumber();
            if (exponent < -(long)text.Length - 324) return BitConverter.Int64BitsToDouble(sign);
            exponent += significantDigits - prefix.Length - (long)fractionDigits;
            long decimalOrder = prefix.Length + exponent - 1;
            if (decimalOrder > 308) throw NonFiniteNumber();
            if (decimalOrder < -324) return BitConverter.Int64BitsToDouble(sign);
            BigInteger numerator = BigInteger.Parse(prefix.ToString(), CultureInfo.InvariantCulture);
            BigInteger denominator = BigInteger.One;
            if (exponent >= 0) numerator *= BigInteger.Pow(10, (int)exponent);
            else denominator = BigInteger.Pow(10, (int)-exponent);
            int binaryExponent = BitLength(numerator) - BitLength(denominator);
            if (binaryExponent >= 0 ? numerator < denominator << binaryExponent
                : numerator << -binaryExponent < denominator) binaryExponent--;
            int spacingExponent = Math.Max(binaryExponent - 52, -1074);
            if (spacingExponent < 0) numerator <<= -spacingExponent;
            else denominator <<= spacingExponent;
            BigInteger remainder;
            BigInteger significand = BigInteger.DivRem(numerator, denominator, out remainder);
            int midpoint = (remainder << 1).CompareTo(denominator);
            if (midpoint > 0 || midpoint == 0 && (nonzeroTail || !significand.IsEven)) significand++;
            if (significand >= BigInteger.One << 53)
            {
                significand >>= 1;
                spacingExponent++;
            }
            long bits;
            if (significand < BigInteger.One << 52) bits = (long)significand;
            else
            {
                int biasedExponent = spacingExponent + 52 + 1023;
                if (biasedExponent >= 2047) throw NonFiniteNumber();
                bits = ((long)biasedExponent << 52) | ((long)significand - (1L << 52));
            }
            return BitConverter.Int64BitsToDouble(sign | bits);
        }

        private static void ReadDigits(string text, ref int index, StringBuilder prefix,
            ref int significantDigits, ref bool nonzeroTail)
        {
            while (index < text.Length && IsDigit(text[index]))
            {
                char digit = text[index++];
                if (significantDigits == 0 && digit == '0') continue;
                significantDigits++;
                if (prefix.Length < RetainedDigits) prefix.Append(digit);
                else if (digit != '0') nonzeroTail = true;
            }
        }

        private static int BitLength(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            int last = bytes.Length - 1;
            if (bytes[last] == 0) last--;
            int result = last * 8;
            for (int highest = bytes[last]; highest != 0; highest >>= 1) result++;
            return result;
        }

        private static bool IsDigit(char value) => value >= '0' && value <= '9';
        private static FormatException InvalidNumber() => new FormatException("Invalid JSON number lexeme.");
        private static FormatException NonFiniteNumber() => new FormatException("JSON number exceeds finite binary64 range.");
    }
}
