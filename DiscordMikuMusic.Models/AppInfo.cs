namespace DiscordMikuMusic.Models
{
    /// <summary>
    /// [EN] Holds application-level metadata read from <c>settings.json</c> at startup.
    /// Registered as a singleton so it can be injected into any service regardless of lifetime.<br/>
    /// [DE] Enthält anwendungsweite Metadaten, die beim Start aus <c>settings.json</c> gelesen werden.
    /// Als Singleton registriert, damit es in jeden Service unabhängig von dessen Lifetime injiziert werden kann.
    /// </summary>
    /// <param name="Name">
    /// [EN] The application name used e.g. in HTTP User-Agent headers.<br/>
    /// [DE] Der Anwendungsname, z.B. für HTTP-User-Agent-Header.
    /// </param>
    /// <param name="Version">
    /// [EN] The application version used e.g. in HTTP User-Agent headers and the Discord bot status.<br/>
    /// [DE] Die Anwendungsversion, z.B. für HTTP-User-Agent-Header und den Discord-Bot-Status.
    /// </param>
    public record AppInfo(string Name, string Version);
}
