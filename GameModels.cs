using System;
using System.IO;
using System.Text.Json.Serialization;

namespace GameLauncher;

public class GameDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonPropertyName("executablePath")]
    public string ExecutablePath { get; set; } = string.Empty;

    // Launcher / platform for this game, e.g. "steam" or "epic".
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonIgnore]
    public string PlatformDisplay =>
        Platform?.ToLower() switch
        {
            "steam" => "Steam",
            "epic" => "Epic Games",
            "battlenet" => "Battle.net",
            "origin" => "EA App",
            "ubisoft" => "Ubisoft Connect",
            "riot" => "Riot Games",
            "mojang" => "Mojang",
            _ => Platform ?? ""
        };

    // Optional Steam game ID. When present, the launcher will prefer steam://rungameid over launching the exe directly.
    [JsonPropertyName("steamId")]
    public string? SteamId { get; set; }

    [JsonPropertyName("epicAppName")]
    public string? EpicAppName { get; set; }

    [JsonIgnore]
    public string FullImagePath =>
        string.IsNullOrWhiteSpace(ImagePath)
            ? string.Empty
            : Path.Combine(AppContext.BaseDirectory, ImagePath);
}

public class AccountDefinition
{
    // Launcher / platform this borrow account belongs to, e.g. "steam", "epic".
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class DiscordConfig
{
    [JsonPropertyName("inviteUrl")]
    public string InviteUrl { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Open Discord";
}

public class DiscordChannel
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

