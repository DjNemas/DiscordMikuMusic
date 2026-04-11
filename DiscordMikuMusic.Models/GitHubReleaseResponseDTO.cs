using System.Text.Json.Serialization;

namespace DiscordMikuMusic.Models
{
    public record GitHubReleaseResponseDTO(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl
    );
}
