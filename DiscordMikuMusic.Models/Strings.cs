namespace DiscordMikuMusic.Models
{
    /// <summary>
    /// [EN] Central store for shared string constants used across the application,
    /// such as date/time format strings.<br/>
    /// [DE] Zentrale Sammlung gemeinsam genutzter Zeichenkettenkonstanten in der gesamten Anwendung,
    /// z.B. Datums- und Uhrzeitformate.
    /// </summary>
    public static class Strings
    {
        /// <summary>
        /// [EN] German date-time format including UTC offset, used for human-readable log output
        /// (e.g. <c>01.04.2025 10:30:00 +00:00</c>).<br/>
        /// [DE] Deutsches Datums-/Uhrzeitformat mit UTC-Offset, für lesbare Log-Ausgaben
        /// (z.B. <c>01.04.2025 10:30:00 +00:00</c>).
        /// </summary>
        public const string GermanDateTimeFormat = "dd.MM.yyyy HH:mm:ss zzz";

        /// <summary>
        /// [EN] Timestamp format used for MikuLogger output entries
        /// (e.g. <c>2025-04-01 10:30:00.000</c>).<br/>
        /// [DE] Zeitstempelformat für MikuLogger-Ausgabezeilen
        /// (z.B. <c>2025-04-01 10:30:00.000</c>).
        /// </summary>
        public const string LogDateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// [EN] Date format used for log sub-folder names
        /// (e.g. <c>2025-04-01</c>).<br/>
        /// [DE] Datumsformat für die Namen der Log-Unterordner
        /// (z.B. <c>2025-04-01</c>).
        /// </summary>
        public const string LogDateFolderFormat = "yyyy-MM-dd";
    }
}
