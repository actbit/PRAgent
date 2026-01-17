using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PRAgent.Configuration;

public static class YamlConfigurationProvider
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T? Deserialize<T>(string yaml) where T : class
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return default;
        }

        try
        {
            return Deserializer.Deserialize<T>(yaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML: {ex.Message}", ex);
        }
    }

    public static bool IsValidYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return false;
        }

        try
        {
            Deserializer.Deserialize<object>(yaml);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
