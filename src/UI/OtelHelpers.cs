using System.Diagnostics;
using System.Reflection;
using UI.Infrastructure.Collections;

namespace UI;

public static class OtelHelpers
{
    private static readonly AssemblyName AssemblyName = typeof(OtelHelpers).Assembly.GetName();

    public static readonly ActivitySource ActivitySource = new(AssemblyName.Name ?? throw new InvalidOperationException($"'{nameof(AssemblyName)}' can not be null."),
                                                               AssemblyName.Version?.ToString() ?? throw new InvalidOperationException($"'{nameof(AssemblyName.Version)}' can not be null."));

    public static class ActivityNames
    {
        public const string ColorCollectionInitializationMessage = $"{nameof(ColorCollection)}_Initialization";
        public const string ColorSearchVectorsMessage = "Color_Search.";

        public const string ImageCollectionInitializationMessage =  $"{nameof(ImageCollection)}_Initialization";
    }
}