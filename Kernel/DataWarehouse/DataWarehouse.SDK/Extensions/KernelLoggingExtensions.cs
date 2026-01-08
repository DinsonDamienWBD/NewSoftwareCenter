using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.Extensions
{
    /// <summary>
    /// God Tier Extensions: Enables Structured Logging syntax for the simple IKernelContext.
    /// Fixes CS1501 errors by automatically formatting arguments.
    /// </summary>
    public static class KernelLoggingExtensions
    {
        public static void LogInfo(this IKernelContext context, string format, params object[] args)
        {
            if (args.Length > 0)
                context.LogInfo(string.Format(format, args));
            else
                context.LogInfo(format);
        }

        public static void LogWarning(this IKernelContext context, string format, params object[] args)
        {
            if (args.Length > 0)
                context.LogWarning(string.Format(format, args));
            else
                context.LogWarning(format);
        }

        public static void LogDebug(this IKernelContext context, string format, params object[] args)
        {
            if (args.Length > 0)
                context.LogDebug(string.Format(format, args));
            else
                context.LogDebug(format);
        }

        public static void LogError(this IKernelContext context, Exception ex, string message)
        {
            // Adapter for (Exception, Message) signature preference
            context.LogError(message, ex);
        }

        public static void LogError(this IKernelContext context, Exception ex, string format, params object[] args)
        {
            // Adapter for (Exception, Format, Args) signature
            string message = (args.Length > 0) ? string.Format(format, args) : format;
            context.LogError(message, ex);
        }
    }
}