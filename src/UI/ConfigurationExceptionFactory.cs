namespace UI;

public static class ConfigurationExceptionFactory
{
    public static InvalidOperationException CreateException(string propertyName) => new($"'{propertyName}' configuration is not set");
}