namespace TwitchSrBot;

internal class TotalCredentials
{
    public TotalCredentials(string twitchBotName, string twitchOAuth, string channelsToBot, string spotifyClientId, string spotifyClientSecret)
    {
        TwitchBotName = twitchBotName;
        TwitchOAuth = twitchOAuth;
        ChannelsToBot = channelsToBot;
        SpotifyClientId = spotifyClientId;
        SpotifyClientSecret = spotifyClientSecret;
    }
    public string TwitchBotName { get; set; }
    public string TwitchOAuth { get; set; }
    public string ChannelsToBot { get; set; }
    public string SpotifyClientId { get; set; }
    public string SpotifyClientSecret { get; set; }
}