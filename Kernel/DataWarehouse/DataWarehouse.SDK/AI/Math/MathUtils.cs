using System;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.SDK.AI.Math
{
    /// <summary>
    /// Production-ready mathematical utility functions for AI and analytics operations.
    /// Provides common mathematical operations with optimized implementations.
    /// Thread-safe and performance-optimized for high-throughput scenarios.
    /// </summary>
    public static class MathUtils
    {
        // ==================== BASIC ARITHMETIC ====================

        /// <summary>
        /// Returns the minimum of two values.
        /// </summary>
        public static T Min<T>(T a, T b) where T : IComparable<T>
        {
            return a.CompareTo(b) <= 0 ? a : b;
        }

        /// <summary>
        /// Returns the minimum value in a collection.
        /// </summary>
        public static T Min<T>(params T[] values) where T : IComparable<T>
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array cannot be null or empty");

            T min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i].CompareTo(min) < 0)
                    min = values[i];
            }
            return min;
        }

        /// <summary>
        /// Returns the minimum value in an enumerable.
        /// </summary>
        public static T Min<T>(IEnumerable<T> values) where T : IComparable<T>
        {
            ArgumentNullException.ThrowIfNull(values);

            using var enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Sequence contains no elements");

            T min = enumerator.Current;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.CompareTo(min) < 0)
                    min = enumerator.Current;
            }
            return min;
        }

        /// <summary>
        /// Returns the maximum of two values.
        /// </summary>
        public static T Max<T>(T a, T b) where T : IComparable<T>
        {
            return a.CompareTo(b) >= 0 ? a : b;
        }

        /// <summary>
        /// Returns the maximum value in a collection.
        /// </summary>
        public static T Max<T>(params T[] values) where T : IComparable<T>
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array cannot be null or empty");

            T max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i].CompareTo(max) > 0)
                    max = values[i];
            }
            return max;
        }

        /// <summary>
        /// Returns the maximum value in an enumerable.
        /// </summary>
        public static T Max<T>(IEnumerable<T> values) where T : IComparable<T>
        {
            ArgumentNullException.ThrowIfNull(values);

            using var enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Sequence contains no elements");

            T max = enumerator.Current;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.CompareTo(max) > 0)
                    max = enumerator.Current;
            }
            return max;
        }

        /// <summary>
        /// Returns the absolute value of a number.
        /// </summary>
        public static int Abs(int value) => MathUtils.Abs(value);

        /// <summary>
        /// Returns the absolute value of a long.
        /// </summary>
        public static long Abs(long value) => MathUtils.Abs(value);

        /// <summary>
        /// Returns the absolute value of a float.
        /// </summary>
        public static float Abs(float value) => MathUtils.Abs(value);

        /// <summary>
        /// Returns the absolute value of a double.
        /// </summary>
        public static double Abs(double value) => MathUtils.Abs(value);

        /// <summary>
        /// Returns the absolute value of a decimal.
        /// </summary>
        public static decimal Abs(decimal value) => MathUtils.Abs(value);

        // ==================== POWER & ROOT ====================

        /// <summary>
        /// Returns a specified number raised to the specified power.
        /// </summary>
        public static double Pow(double x, double y) => MathUtils.Pow(x, y);

        /// <summary>
        /// Returns the square root of a specified number.
        /// </summary>
        public static double Sqrt(double value)
        {
            if (value < 0)
                throw new ArgumentException("Cannot compute square root of negative number");
            return MathUtils.Sqrt(value);
        }

        /// <summary>
        /// Returns the cube root of a specified number.
        /// </summary>
        public static double Cbrt(double value) => MathUtils.Pow(value, 1.0 / 3.0);

        /// <summary>
        /// Returns the square of a number (x²).
        /// </summary>
        public static double Square(double value) => value * value;

        /// <summary>
        /// Returns the cube of a number (x³).
        /// </summary>
        public static double Cube(double value) => value * value * value;

        // ==================== ROUNDING ====================

        /// <summary>
        /// Rounds a value to the nearest integer.
        /// </summary>
        public static double Round(double value) => System.Math.Round(value);

        /// <summary>
        /// Rounds a value to a specified number of decimal places.
        /// </summary>
        public static double Round(double value, int decimals) => System.Math.Round(value, decimals);

        /// <summary>
        /// Returns the largest integer less than or equal to the specified number.
        /// </summary>
        public static double Floor(double value) => MathUtils.Floor(value);

        /// <summary>
        /// Returns the smallest integer greater than or equal to the specified number.
        /// </summary>
        public static double Ceiling(double value) => MathUtils.Ceiling(value);

        /// <summary>
        /// Returns the integer part of a number.
        /// </summary>
        public static double Truncate(double value) => System.Math.Truncate(value);

        // ==================== TRIGONOMETRIC ====================

        /// <summary>
        /// Returns the sine of the specified angle (in radians).
        /// </summary>
        public static double Sin(double angle) => System.Math.Sin(angle);

        /// <summary>
        /// Returns the cosine of the specified angle (in radians).
        /// </summary>
        public static double Cos(double angle) => System.Math.Cos(angle);

        /// <summary>
        /// Returns the tangent of the specified angle (in radians).
        /// </summary>
        public static double Tan(double angle) => System.Math.Tan(angle);

        /// <summary>
        /// Returns the angle whose sine is the specified number (in radians).
        /// </summary>
        public static double Asin(double value) => System.Math.Asin(value);

        /// <summary>
        /// Returns the angle whose cosine is the specified number (in radians).
        /// </summary>
        public static double Acos(double value) => System.Math.Acos(value);

        /// <summary>
        /// Returns the angle whose tangent is the specified number (in radians).
        /// </summary>
        public static double Atan(double value) => System.Math.Atan(value);

        /// <summary>
        /// Returns the angle whose tangent is the quotient of two specified numbers (in radians).
        /// </summary>
        public static double Atan2(double y, double x) => System.Math.Atan2(y, x);

        // ==================== LOGARITHMIC & EXPONENTIAL ====================

        /// <summary>
        /// Returns e raised to the specified power.
        /// </summary>
        public static double Exp(double power) => System.Math.Exp(power);

        /// <summary>
        /// Returns the natural (base e) logarithm of a specified number.
        /// </summary>
        public static double Log(double value) => MathUtils.Log(value);

        /// <summary>
        /// Returns the logarithm of a specified number in a specified base.
        /// </summary>
        public static double Log(double value, double baseValue) => MathUtils.Log(value, baseValue);

        /// <summary>
        /// Returns the base 10 logarithm of a specified number.
        /// </summary>
        public static double Log10(double value) => MathUtils.Log10(value);

        /// <summary>
        /// Returns the base 2 logarithm of a specified number.
        /// </summary>
        public static double Log2(double value) => MathUtils.Log2(value);

        // ==================== STATISTICAL FUNCTIONS ====================

        /// <summary>
        /// Calculates the mean (average) of a collection of values.
        /// </summary>
        public static double Mean(IEnumerable<double> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            double sum = 0;
            int count = 0;

            foreach (var value in values)
            {
                sum += value;
                count++;
            }

            if (count == 0)
                throw new InvalidOperationException("Sequence contains no elements");

            return sum / count;
        }

        /// <summary>
        /// Calculates the median of a collection of values.
        /// </summary>
        public static double Median(IEnumerable<double> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            var sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0)
                throw new InvalidOperationException("Sequence contains no elements");

            int n = sorted.Length;
            if (n % 2 == 1)
                return sorted[n / 2];
            else
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        /// <summary>
        /// Calculates the variance of a collection of values.
        /// </summary>
        public static double Variance(IEnumerable<double> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            var valueList = values.ToList();
            if (valueList.Count == 0)
                throw new InvalidOperationException("Sequence contains no elements");

            double mean = Mean(valueList);
            double sumSquaredDiffs = valueList.Sum(x => Square(x - mean));

            return sumSquaredDiffs / valueList.Count;
        }

        /// <summary>
        /// Calculates the standard deviation of a collection of values.
        /// </summary>
        public static double StandardDeviation(IEnumerable<double> values)
        {
            return Sqrt(Variance(values));
        }

        // ==================== UTILITY FUNCTIONS ====================

        /// <summary>
        /// Clamps a value between a minimum and maximum value.
        /// </summary>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        /// <summary>
        /// Linearly interpolates between two values.
        /// </summary>
        /// <param name="a">Start value</param>
        /// <param name="b">End value</param>
        /// <param name="t">Interpolation factor (0.0 to 1.0)</param>
        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Clamp(t, 0.0, 1.0);
        }

        /// <summary>
        /// Checks if a value is within a specified range (inclusive).
        /// </summary>
        public static bool InRange<T>(T value, T min, T max) where T : IComparable<T>
        {
            return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
        }

        /// <summary>
        /// Returns the sign of a number (-1, 0, or 1).
        /// </summary>
        public static int Sign(double value)
        {
            if (value > 0) return 1;
            if (value < 0) return -1;
            return 0;
        }

        /// <summary>
        /// Checks if two floating-point numbers are approximately equal.
        /// </summary>
        public static bool ApproximatelyEqual(double a, double b, double epsilon = 1e-10)
        {
            return Abs(a - b) < epsilon;
        }

        // ==================== CONSTANTS ====================

        /// <summary>
        /// Represents the ratio of the circumference of a circle to its diameter (π).
        /// </summary>
        public const double PI = System.Math.PI;

        /// <summary>
        /// Represents the natural logarithmic base (e).
        /// </summary>
        public const double E = System.Math.E;

        /// <summary>
        /// Represents the golden ratio (φ).
        /// </summary>
        public const double GoldenRatio = 1.618033988749895;

        /// <summary>
        /// Represents tau (2π).
        /// </summary>
        public const double Tau = 2 * PI;
    }
}
