using System;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.SDK.AI.Math
{
    /// <summary>
    /// Performs statistical analysis on execution metrics and system behavior.
    /// Detects anomalies, identifies trends, and discovers correlations.
    ///
    /// Used by AI Runtime and Proactive Agents to:
    /// - Detect performance degradation
    /// - Identify unusual patterns (security threats, bugs)
    /// - Predict future trends
    /// - Discover relationships between metrics
    /// - Trigger proactive optimizations
    ///
    /// Analysis techniques:
    /// - Anomaly detection (Z-score, IQR)
    /// - Trend analysis (linear regression)
    /// - Correlation discovery (Pearson correlation)
    /// - Moving averages (smoothing)
    /// </summary>
    public class StatisticalAnalyzer
    {
        /// <summary>
        /// Detects anomalies in a time series using Z-score method.
        /// Values with |Z-score| > threshold are considered anomalies.
        ///
        /// Use cases:
        /// - Detect sudden CPU spikes
        /// - Identify unusually slow operations
        /// - Flag suspicious access patterns
        ///
        /// Z-score = (value - mean) / stddev
        /// Typical threshold: 3.0 (99.7% of normal data within ±3σ)
        /// </summary>
        /// <param name="values">Time series data points.</param>
        /// <param name="threshold">Z-score threshold (default 3.0).</param>
        /// <returns>List of anomalies with indices and Z-scores.</returns>
        public List<Anomaly> DetectAnomalies(List<double> values, double threshold = 3.0)
        {
            if (values == null || values.Count < 3)
                return new List<Anomaly>(); // Need sufficient data

            var mean = CalculateMean(values);
            var stddev = CalculateStandardDeviation(values, mean);

            if (stddev == 0)
                return new List<Anomaly>(); // No variation = no anomalies

            var anomalies = new List<Anomaly>();

            for (int i = 0; i < values.Count; i++)
            {
                var zScore = Math.Abs((values[i] - mean) / stddev);
                if (zScore > threshold)
                {
                    anomalies.Add(new Anomaly
                    {
                        Index = i,
                        Value = values[i],
                        ZScore = zScore,
                        ExpectedValue = mean,
                        Deviation = values[i] - mean
                    });
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Detects anomalies using Interquartile Range (IQR) method.
        /// More robust to outliers than Z-score.
        ///
        /// Method:
        /// 1. Calculate Q1 (25th percentile) and Q3 (75th percentile)
        /// 2. IQR = Q3 - Q1
        /// 3. Lower bound = Q1 - 1.5 × IQR
        /// 4. Upper bound = Q3 + 1.5 × IQR
        /// 5. Values outside bounds are anomalies
        /// </summary>
        /// <param name="values">Time series data points.</param>
        /// <returns>List of anomalies with indices.</returns>
        public List<Anomaly> DetectAnomaliesIQR(List<double> values)
        {
            if (values == null || values.Count < 4)
                return new List<Anomaly>();

            var sorted = values.OrderBy(v => v).ToList();
            var q1 = CalculatePercentile(sorted, 25);
            var q3 = CalculatePercentile(sorted, 75);
            var iqr = q3 - q1;

            var lowerBound = q1 - (1.5 * iqr);
            var upperBound = q3 + (1.5 * iqr);

            var anomalies = new List<Anomaly>();

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] < lowerBound || values[i] > upperBound)
                {
                    anomalies.Add(new Anomaly
                    {
                        Index = i,
                        Value = values[i],
                        ExpectedValue = (lowerBound + upperBound) / 2,
                        Deviation = values[i] < lowerBound
                            ? values[i] - lowerBound
                            : values[i] - upperBound
                    });
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Performs linear regression to identify trends.
        /// Returns slope (trend direction) and R² (fit quality).
        ///
        /// Use cases:
        /// - Detect increasing latency over time
        /// - Identify memory leak patterns
        /// - Forecast future values
        ///
        /// Interpretation:
        /// - Slope > 0: Upward trend
        /// - Slope < 0: Downward trend
        /// - R² close to 1: Strong linear relationship
        /// - R² close to 0: No linear relationship
        /// </summary>
        /// <param name="values">Time series data points.</param>
        /// <returns>Trend analysis result.</returns>
        public TrendAnalysis AnalyzeTrend(List<double> values)
        {
            if (values == null || values.Count < 2)
            {
                return new TrendAnalysis { HasTrend = false };
            }

            // Prepare x values (time indices)
            var x = Enumerable.Range(0, values.Count).Select(i => (double)i).ToList();
            var y = values;

            // Calculate means
            var xMean = CalculateMean(x);
            var yMean = CalculateMean(y);

            // Calculate slope and intercept
            double numerator = 0;
            double denominator = 0;

            for (int i = 0; i < x.Count; i++)
            {
                numerator += (x[i] - xMean) * (y[i] - yMean);
                denominator += (x[i] - xMean) * (x[i] - xMean);
            }

            if (denominator == 0)
            {
                return new TrendAnalysis { HasTrend = false };
            }

            var slope = numerator / denominator;
            var intercept = yMean - (slope * xMean);

            // Calculate R² (coefficient of determination)
            var ssTotal = y.Sum(val => Math.Pow(val - yMean, 2));
            var ssResidual = 0.0;

            for (int i = 0; i < x.Count; i++)
            {
                var predicted = intercept + (slope * x[i]);
                ssResidual += Math.Pow(y[i] - predicted, 2);
            }

            var rSquared = ssTotal > 0 ? 1 - (ssResidual / ssTotal) : 0;

            // Forecast next value
            var nextX = values.Count;
            var forecast = intercept + (slope * nextX);

            return new TrendAnalysis
            {
                HasTrend = true,
                Slope = slope,
                Intercept = intercept,
                RSquared = rSquared,
                TrendDirection = slope > 0.01 ? TrendDirection.Increasing
                    : slope < -0.01 ? TrendDirection.Decreasing
                    : TrendDirection.Stable,
                ForecastNextValue = forecast
            };
        }

        /// <summary>
        /// Calculates Pearson correlation coefficient between two variables.
        /// Measures linear relationship strength.
        ///
        /// Use cases:
        /// - Discover relationship between latency and data size
        /// - Find correlation between CPU usage and throughput
        /// - Identify dependent metrics
        ///
        /// Interpretation:
        /// - r = 1: Perfect positive correlation
        /// - r = -1: Perfect negative correlation
        /// - r = 0: No linear correlation
        /// - |r| > 0.7: Strong correlation
        /// - |r| < 0.3: Weak correlation
        /// </summary>
        /// <param name="x">First variable data points.</param>
        /// <param name="y">Second variable data points.</param>
        /// <returns>Correlation analysis result.</returns>
        public CorrelationAnalysis CalculateCorrelation(List<double> x, List<double> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count < 2)
            {
                return new CorrelationAnalysis { IsValid = false };
            }

            var xMean = CalculateMean(x);
            var yMean = CalculateMean(y);

            double numerator = 0;
            double xDenominator = 0;
            double yDenominator = 0;

            for (int i = 0; i < x.Count; i++)
            {
                var xDiff = x[i] - xMean;
                var yDiff = y[i] - yMean;

                numerator += xDiff * yDiff;
                xDenominator += xDiff * xDiff;
                yDenominator += yDiff * yDiff;
            }

            var denominator = Math.Sqrt(xDenominator * yDenominator);

            if (denominator == 0)
            {
                return new CorrelationAnalysis { IsValid = false };
            }

            var correlation = numerator / denominator;

            return new CorrelationAnalysis
            {
                IsValid = true,
                Coefficient = correlation,
                Strength = Math.Abs(correlation) > 0.7 ? CorrelationStrength.Strong
                    : Math.Abs(correlation) > 0.4 ? CorrelationStrength.Moderate
                    : CorrelationStrength.Weak,
                Direction = correlation > 0 ? CorrelationDirection.Positive : CorrelationDirection.Negative
            };
        }

        /// <summary>
        /// Calculates simple moving average for smoothing time series.
        /// Reduces noise and highlights trends.
        ///
        /// Use cases:
        /// - Smooth noisy performance metrics
        /// - Identify underlying trends
        /// - Remove short-term fluctuations
        /// </summary>
        /// <param name="values">Time series data points.</param>
        /// <param name="windowSize">Number of points in moving average (default 5).</param>
        /// <returns>Smoothed time series.</returns>
        public List<double> CalculateMovingAverage(List<double> values, int windowSize = 5)
        {
            if (values == null || values.Count < windowSize)
                return values ?? new List<double>();

            var smoothed = new List<double>();

            for (int i = 0; i < values.Count; i++)
            {
                var start = Math.Max(0, i - windowSize + 1);
                var window = values.Skip(start).Take(i - start + 1).ToList();
                smoothed.Add(CalculateMean(window));
            }

            return smoothed;
        }

        /// <summary>
        /// Calculates exponential moving average (EMA).
        /// Gives more weight to recent values.
        /// </summary>
        /// <param name="values">Time series data points.</param>
        /// <param name="alpha">Smoothing factor (0-1, default 0.3).</param>
        /// <returns>Exponentially smoothed time series.</returns>
        public List<double> CalculateExponentialMovingAverage(List<double> values, double alpha = 0.3)
        {
            if (values == null || values.Count == 0)
                return new List<double>();

            if (alpha < 0 || alpha > 1)
                throw new ArgumentException("Alpha must be between 0 and 1");

            var ema = new List<double> { values[0] };

            for (int i = 1; i < values.Count; i++)
            {
                var newEma = (alpha * values[i]) + ((1 - alpha) * ema[i - 1]);
                ema.Add(newEma);
            }

            return ema;
        }

        // =========================================================================
        // HELPER METHODS
        // =========================================================================

        private double CalculateMean(List<double> values)
        {
            return values.Count > 0 ? values.Average() : 0;
        }

        private double CalculateStandardDeviation(List<double> values, double mean)
        {
            if (values.Count < 2)
                return 0;

            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1);
            return Math.Sqrt(variance);
        }

        private double CalculatePercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
                return 0;

            var index = (percentile / 100.0) * (sortedValues.Count - 1);
            var lowerIndex = (int)Math.Floor(index);
            var upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            var fraction = index - lowerIndex;
            return sortedValues[lowerIndex] + (fraction * (sortedValues[upperIndex] - sortedValues[lowerIndex]));
        }
    }

    /// <summary>
    /// Represents a detected anomaly.
    /// </summary>
    public class Anomaly
    {
        public int Index { get; set; }
        public double Value { get; set; }
        public double ZScore { get; set; }
        public double ExpectedValue { get; set; }
        public double Deviation { get; set; }
    }

    /// <summary>
    /// Result of trend analysis.
    /// </summary>
    public class TrendAnalysis
    {
        public bool HasTrend { get; set; }
        public double Slope { get; set; }
        public double Intercept { get; set; }
        public double RSquared { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public double ForecastNextValue { get; set; }
    }

    /// <summary>
    /// Trend direction.
    /// </summary>
    public enum TrendDirection
    {
        Increasing,
        Decreasing,
        Stable
    }

    /// <summary>
    /// Result of correlation analysis.
    /// </summary>
    public class CorrelationAnalysis
    {
        public bool IsValid { get; set; }
        public double Coefficient { get; set; }
        public CorrelationStrength Strength { get; set; }
        public CorrelationDirection Direction { get; set; }
    }

    /// <summary>
    /// Correlation strength.
    /// </summary>
    public enum CorrelationStrength
    {
        Weak,
        Moderate,
        Strong
    }

    /// <summary>
    /// Correlation direction.
    /// </summary>
    public enum CorrelationDirection
    {
        Positive,
        Negative
    }
}
