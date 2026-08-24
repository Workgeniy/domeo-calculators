using System.Reflection;

namespace Domeo.Calculators.Reference;

/// <summary>Чтение справочников, встроенных в сборку из /data.</summary>
internal static class EmbeddedResources
{
    public static string Read(string name)
    {
        Assembly assembly = typeof(EmbeddedResources).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Встроенный ресурс не найден: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
