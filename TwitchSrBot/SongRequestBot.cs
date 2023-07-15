using System.Net.Http.Headers;
using SimpleJSON;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using TwitchSrBot;

class SongRequestBot
{
    public SongRequestBot(BotConfig botConfig)
    {
        InitTwitchBot();
        m_Config = botConfig;
        
        
        string helpMessage = $"{m_Config.Prefix}help [command] | displays info about the specified command | {m_Config.Prefix}commands <- list of commands";
        m_CommandList = new Dictionary<string, string>
        {
            { "commands", "returns a all commands" },
            { "pr", $"{m_Config.Prefix}pr [playlist link] | request a spotify playlist" },
            { "ql", $"{m_Config.Prefix}ql | returns amount of songs in queue" },
            { "help", helpMessage },
            { "", helpMessage }
        };
        UpdateQueue();

        m_AccessToken = new CurrentAccessToken(m_Config);

        RequestNext();
    }

    private void UpdateQueue()
    {
        m_Queue = File.ReadAllLines(m_QueuePath).ToList();
    }

    public void ResetQueue()
    {
        m_Queue = new List<string>();
        File.WriteAllLines(m_QueuePath, m_Queue);
    }

    private TwitchClient m_Client;
    private readonly BotConfig m_Config;
    private int m_RequestedSongs;
    private const string SpotifyApi = "https://api.spotify.com/v1/playlists/playlist_id/tracks";
    private readonly Dictionary<string, string> m_CommandList;
    private readonly string m_QueuePath = Directory.GetCurrentDirectory() + @"/Data/queue.txt";
    private List<string> m_Queue;

    private readonly CurrentAccessToken m_AccessToken;
    

    private void InitTwitchBot()
    {
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 100000000,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        var credentials =
            new ConnectionCredentials(m_Config.TwitchBotName, m_Config.TwitchOAuth);
        var customClient = new WebSocketClient(clientOptions);
        m_Client = new TwitchClient(customClient);
        m_Client.Initialize(credentials, m_Config.ChannelToBot);

        m_Client.OnLog += ClientOnLog;
        m_Client.OnJoinedChannel += ClientOnJoinedChannel;
        m_Client.OnDisconnected += ClientOnDisconnected;

        Console.WriteLine("Attempting to Connect...");
        m_Client.Connect();
    }
    
    private void ClientOnDisconnected(object? sender, OnDisconnectedEventArgs e)
    {
        Console.WriteLine("Disconnected: " + e);
        Console.WriteLine("Attempting to Reconnect...");
        m_Client.Connect();
    }
    
    private void ClientOnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine(e.BotUsername + " joined channel: " + e.Channel);
    }
    
    //filter for command messages
    private void ClientOnLog(object? sender, OnLogArgs e)
    {
        if (!e.Data.Contains("PRIVMSG")) return;
        var msg = e.Data.Split("PRIVMSG")[1];
        
        HandleMessage(msg);
    }
    
    private void HandleMessage(string msg)
    {
        var crop = " #" + m_Config.ChannelToBot.ToLower() + " :";
        var message = msg.Remove(0, crop.Length);
        
        
        if (!message.StartsWith(m_Config.Prefix)) return;
        var command = message.Split(" ")[0].Remove(0, m_Config.Prefix.Length);
        var commandContent = message.Contains(' ') ? message.Split(" ")[1] : "";

        switch (command)
        {
            case "commands":
                SendMessage($"commands(prefix = {m_Config.Prefix}) {GetCommandList()}");
                break;

            case "pr":
                AddSongsFromLink(commandContent);
                break;

            case "ql":
                var item = m_Queue.FirstOrDefault();
                if (item != null)
                {
                    SendMessage("Songs in queue: " + m_Queue.Count);
                }
                else
                {
                    SendMessage("No songs in queue");
                }

                break;

            case "help":
                if (m_CommandList.TryGetValue(commandContent, out var neededHelp))
                {
                    SendMessage(neededHelp);
                }

                break;
        }
    }
    
    public void SendMessage(string message)
    {
        try
        {
            m_Client.SendMessage(m_Config.ChannelToBot, message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    //adds all songs from playlist to dictionary
    private void AddSongsFromLink(string playlistLink)
    {
        m_RequestedSongs = 0;
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            AuthenticationHeaderValue.Parse(m_AccessToken.GetToken().token_type + " " + m_AccessToken.GetToken().access_token);

        var requestResult = httpClient.GetAsync(SpotifyApi.Replace("playlist_id", GetPlaylistId(playlistLink))).Result.Content.ReadAsStringAsync().Result;

        var json = JSONNode.Parse(requestResult);

        foreach (JSONNode item in json["items"])
        {
            var song = item["track"]["name"].Value;
            var artist = item["track"]["artists"][0]["name"].Value;

            m_Queue.Add($"{song} - {artist}");
            m_RequestedSongs++;
        }

        File.WriteAllLines(m_QueuePath, m_Queue);
        SendMessage($"Added {m_RequestedSongs} songs to request list");
        Console.WriteLine($"Added {m_RequestedSongs} songs to request list");
    }
    
    //puts all commands from commandlist into a |(pipe) seperated string
    private string GetCommandList()
    {   
        return m_CommandList.Aggregate("", (current, pair) => current + $"{pair.Key} | ")[..^6];
    }
    
    //returns playlistID based on url
    private string GetPlaylistId(string playlistUrl)
    {
        return playlistUrl.Split("/").Last().Split("?")[0];
    }

    private void RequestNext()
    {
        var songToReq = m_Queue.FirstOrDefault();
        if (songToReq == null) return;
        if (GetCurrentRequestAmount() >= 2) return;
        SendMessage(m_Config.StreamersRequestCommand + " " + songToReq);

        //possibly implement check for success here
        m_Queue.Remove(songToReq);
        File.WriteAllLines(m_QueuePath, m_Queue);
        Console.WriteLine("Songs left in queue: " + m_Queue.Count);
        
        new Timer(e => RequestNext(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
    }
    
    //returns amount of songs, requested by bot, from songqueue
    int GetCurrentRequestAmount()
    {
        var httpClient = new HttpClient();
        var queueRaw = httpClient.GetStringAsync(m_Config.QueueLink).Result;
    
        return JSONNode.Parse(queueRaw)["songlist"]["list"].Count; 
    }
    
    
}