using System.Diagnostics;

namespace Core.Primitives
{
    /// <summary>
    /// Represents a Semantic Version (Major.Minor.Patch).
    /// Implemented as a lightweight readonly struct for performance.
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="major"></param>
    /// <param name="minor"></param>
    /// <param name="patch"></param>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct SemVer(int major, int minor, int patch) : IComparable<SemVer>
    {
        /// <summary>
        /// Major version number
        /// </summary>
        public int Major { get; } = major;

        /// <summary>
        /// Minor version number
        /// </summary>
        public int Minor { get; } = minor;

        /// <summary>
        /// Patch version number
        /// </summary>
        public int Patch { get; } = patch;

        /// <summary>
        /// Parses a version string (e.g., "1.0.5") into a SemVer struct.
        /// Returns 0.0.0 if parsing fails.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static SemVer Parse(string version)
        {
            var parts = version.Split('.');
            return new SemVer(
                parts.Length > 0 ? int.Parse(parts[0]) : 0,
                parts.Length > 1 ? int.Parse(parts[1]) : 0,
                parts.Length > 2 ? int.Parse(parts[2]) : 0
            );
        }

        /// <summary>
        /// Convert to string
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Compare to another SemVer
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public readonly int CompareTo(SemVer other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        // Operators

        /// <summary>
        /// Greater than operator
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;

        /// <summary>
        /// Less than operator
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;

        /// <summary>
        /// Greater than or equal operator
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator >=(SemVer a, SemVer b) => a.CompareTo(b) >= 0;

        /// <summary>
        /// Less than or equal operator
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator <=(SemVer a, SemVer b) => a.CompareTo(b) <= 0;

        /// <summary>
        /// == Override
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(SemVer left, SemVer right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// != Override
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(SemVer left, SemVer right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Equals override
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool Equals(object? obj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// GetHashCode override
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a debugger display to reduce boilerplate and improve the debugging experience.
        /// </summary>
        /// <returns></returns>
        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}