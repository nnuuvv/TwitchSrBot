namespace TwitchSrBot;


internal class BotConfig
{
    /// <summary>
    /// Initializes config class
    /// </summary>
    /// <param name="pathToFile">path to configuration file</param>
    public BotConfig(string pathToFile)
    {
        var lines = File.ReadAllLines(pathToFile);
        
        TwitchBotName = lines[0].Trim().ToLower().Split(",")[0];
        TwitchOAuth = lines[0].Trim().ToLower().Split(",")[1];
        ChannelToBot = lines[1];
        SpotifyClientId = lines[2];
        SpotifyClientSecret = lines[3];
        Prefix = lines[4];  //example: "$"
        QueueLink = lines[5];  //example: "https://streamlabs.com/api/v6/521267c39566929/chatbot/data/twitch_account"
        StreamersRequestCommand = lines[6];  //example: "!sr"
    }
    public string TwitchBotName { get; }
    public string TwitchOAuth { get; }
    public string ChannelToBot { get; }
    public string SpotifyClientId { get; }
    public string SpotifyClientSecret { get; }
    public string QueueLink { get; }
    public string Prefix { get; }
    public string StreamersRequestCommand { get; }
}