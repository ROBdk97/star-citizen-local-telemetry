#if !NET
// APIs missing from .NET Standard 2.1, so the same source builds for both targets.
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}

namespace System.Runtime.Versioning
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor |
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute(string platformName) : Attribute
    {
        public string PlatformName { get; } = platformName;
    }
}

namespace StarCitizen.LocalTelemetry
{
    internal static class Polyfills
    {
        extension(ArgumentNullException)
        {
            public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                if (argument is null) throw new ArgumentNullException(paramName);
            }
        }

        extension(ArgumentException)
        {
            public static void ThrowIfNullOrWhiteSpace(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                if (argument is null) throw new ArgumentNullException(paramName);
                if (string.IsNullOrWhiteSpace(argument))
                    throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
            }
        }
    }
}
#endif