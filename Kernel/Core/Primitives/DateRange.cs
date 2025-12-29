namespace Core.Primitives
{
    /// <summary>
    /// New business primitive
    /// </summary>
    public readonly struct DateRange : IEquatable<DateRange>
    {
        /// <summary>
        /// Start datetime
        /// </summary>
        public DateTime Start { get; }

        /// <summary>
        /// End datetime
        /// </summary>
        public DateTime End { get; }

        /// <summary>
        /// Start to end datetime range
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <exception cref="ArgumentException"></exception>
        public DateRange(DateTime start, DateTime end)
        {
            if (end < start) throw new ArgumentException("End date cannot be before Start date.");
            Start = start;
            End = end;
        }

        /// <summary>
        /// Check if a datetime is between start and end datetimes
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public bool Contains(DateTime date) => date >= Start && date <= End;

        /// <summary>
        /// Check if a datetime overlaps start or end datetimes
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Overlaps(DateRange other) => Start < other.End && End > other.Start;

        /// <summary>
        /// Get the duration of start to end datetimes
        /// </summary>
        public TimeSpan Duration => End - Start;

        /// <summary>
        /// Cast to string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd}";

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(DateRange other) => Start == other.Start && End == other.End;

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj) => obj is DateRange other && Equals(other);

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => HashCode.Combine(Start, End);

        /// <summary>
        /// == operator
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(DateRange left, DateRange right) => left.Equals(right);

        /// <summary>
        /// != operator
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(DateRange left, DateRange right) => !left.Equals(right);
    }
}