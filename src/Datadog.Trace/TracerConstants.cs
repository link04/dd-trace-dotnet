namespace Datadog.Trace
{
    internal static class TracerConstants
    {
        public const string Language = "dotnet";

        /// <summary>
        /// 2^63-1
        /// </summary>
        public const ulong MaxTraceId = 9_223_372_036_854_775_807;

        public static readonly string AssemblyVersion = "1.20.1.0";

        public static readonly int Major = 1;

        public static readonly int Minor = 20;

        public static readonly int Patch = 1;
    }
}
