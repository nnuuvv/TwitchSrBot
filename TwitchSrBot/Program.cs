using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SimpleJSON;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;


string prefix = "$";
string requestCommand = "!sr";
string spotifyAPI = "https://api.spotify.com/v1/playlists/playlist_id/tracks";
string songQueueLink = "https://streamlabs.com/api/v6/521267c39566929/chatbot/data/twitch_account";


//set up credentials
string credPath = Directory.GetCurrentDirectory() + @"/Data/srBot.txt";
string[] allCredLines = File.ReadAllLines(credPath);
TotalCredentials totalCredentials = new TotalCredentials()
{
    TwitchBotName = allCredLines[0].Trim().ToLower().Split(",")[0],
    TwitchOAuth = allCredLines[0].Trim().ToLower().Split(",")[1],
    ChannelsToBot = allCredLines[1],
    SpotifyClientId = allCredLines[2],
    SpotifyClientSecret = allCredLines[3]
};


TwitchClient client;
Dictionary<string, string> songsAndArtists = new Dictionary<string, string>();
string[] songs = new string[] { };
AccessToken accessToken = new AccessToken();
accessToken.expires_in = 3600;

//token refresh
var startTimeSpan = TimeSpan.Zero;
var periodTimeSpan = TimeSpan.FromSeconds(accessToken.expires_in);
var timer = new System.Threading.Timer((e) =>
{
    accessToken = GetToken().Result;
}, null, startTimeSpan, periodTimeSpan);


InitTwitchBot();

int requestedSongs;

//request new song
var startTimeSpanReq = TimeSpan.Zero;
var periodTimeSpanReq = TimeSpan.FromSeconds(60);
var reqTimer = new System.Threading.Timer((e) =>
{
    string? songToReq = songsAndArtists.FirstOrDefault().Key;
    if (songToReq != null)
    {
        if (GetCurrentRequests() < 2)
        {
            songsAndArtists.TryGetValue(songToReq, out var curArtist);
            ClientSendMessage(requestCommand + " " + songToReq + " - " + curArtist);
            
            //possibly implement check for success here
            songsAndArtists.Remove(songToReq);
            Console.WriteLine("Songs left in queue: " + songsAndArtists.Count);
        }
    }
    
}, null, startTimeSpanReq, periodTimeSpanReq);


//"say xyz" to write message in twitch chat by hand
while (true)
{
    string? cur = Console.ReadLine();
    if (cur != null && cur.StartsWith("say"))
    {
        ClientSendMessage(cur.Remove(0, 3).ToString());
    }
}


//returns amount of songs, requested by bot, from songqueue
int GetCurrentRequests()
{
    int current = 0;
    HttpClient httpClient = new HttpClient();
    string queueRaw = httpClient.GetStringAsync(songQueueLink).Result;
    JSONNode json = JSONNode.Parse(queueRaw);

    foreach (JSONNode song in json["songlist"]["list"])
    {
        if (song["by"] == totalCredentials.TwitchBotName)
        {
            current++;
        }
    }
    return current;
}

//gets spotify api token
async Task<AccessToken> GetToken()
{
    Console.WriteLine("Getting Token");
    string credentials = String.Format("{0}:{1}",totalCredentials.SpotifyClientId, totalCredentials.SpotifyClientSecret);

    using (var client = new HttpClient())
    {
        //Define Headers
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

        //Prepare Request Body
        List<KeyValuePair<string, string>> requestData = new List<KeyValuePair<string, string>>();
        requestData.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));

        FormUrlEncodedContent requestBody = new FormUrlEncodedContent(requestData);

        //Request Token
        var request = await client.PostAsync("https://accounts.spotify.com/api/token", requestBody);
        var response = await request.Content.ReadAsStringAsync();



        return JsonConvert.DeserializeObject<AccessToken>(response);
    }
}



void InitTwitchBot()
{
    var clientOptions = new ClientOptions
    {
        MessagesAllowedInPeriod = 100000000,
        ThrottlingPeriod = TimeSpan.FromSeconds(30)
    };
    
    ConnectionCredentials credentials = new ConnectionCredentials(totalCredentials.TwitchBotName, totalCredentials.TwitchOAuth);
    WebSocketClient customClient = new WebSocketClient(clientOptions);
    client = new TwitchClient(customClient);
    client.Initialize(credentials, totalCredentials.ChannelsToBot);

    client.OnLog += ClientOnLog;
    client.OnJoinedChannel += ClientOnJoinedChannel;
    client.OnDisconnected += ClientOnDisconnected;

    Console.WriteLine("Attempting to Connect...");
    client.Connect();
}

void ClientOnDisconnected(object? sender, OnDisconnectedEventArgs e)
{
    Console.WriteLine("Disconnected: " + e.ToString());
    Console.WriteLine("Attempting to Reconnect...");
    client.Reconnect();
}

void ClientOnLog(object sender, OnLogArgs e)
{
    if (e.Data.Contains("PRIVMSG"))
    {
        string msg = e.Data.Split("PRIVMSG")[1];
        string crop = " #" + totalCredentials.ChannelsToBot.ToLower() +" :";

        ParseMessage(msg.Remove(0, crop.Length));
    }
    
}

void ClientOnJoinedChannel(object sender, OnJoinedChannelArgs e)
{
    Console.WriteLine(e.BotUsername + " joined channel: " + e.Channel);
}


void ClientSendMessage(string message)
{
    try
    {
        client.SendMessage(totalCredentials.ChannelsToBot, message);
    }        
    catch (Exception e)
    {
        Console.WriteLine(e);
    }

}

void ParseMessage(string message)
{
    if (message.StartsWith(prefix))
    {
        string command = message.Split(" ")[0].Remove(0,prefix.Length);
        string commandContent = message.Split(" ")[1];

        switch (command)
        {
            case "pr":
                AddSongsFromLink(commandContent);
                break;
            default:
                break;
        }
    }
}

//adds all songs from playlist to dictionary
void AddSongsFromLink(string playlistLink)
{
    requestedSongs = 0;
    HttpClient httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(accessToken.token_type + " " + accessToken.access_token);
    
    string requestResult = httpClient.GetAsync(spotifyAPI.Replace("playlist_id", GetPlaylistId(playlistLink))).Result
        .Content.ReadAsStringAsync().Result;

    JSONNode json = JSONNode.Parse(requestResult);

    foreach (JSONNode item in json["items"])
    {
        string song = item["track"]["name"].Value;
        string artist = item["track"]["artists"][0]["name"].Value;

        songsAndArtists.Add(key: song, value: artist);
        requestedSongs++;
    }
    
    ClientSendMessage($"Added {requestedSongs} songs to request list");
}

//returns playlistID based on url
string GetPlaylistId(string playlistUrl)
{
    return playlistUrl.Split("/").Last().Split("?")[0];
}


class AccessToken
{
    public string access_token { get; set; }
    public string token_type { get; set; }
    public long expires_in { get; set; }
}


class TotalCredentials
{
    public string TwitchBotName { get; set; }
    public string TwitchOAuth { get; set; }
    public string ChannelsToBot { get; set; }
    public string SpotifyClientId { get; set; }
    public string SpotifyClientSecret { get; set; }
}

