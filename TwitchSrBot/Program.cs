using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using SimpleJSON;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using TwitchSrBot;


//configurations
const string prefix = "$";
const string requestCommand = "!sr";
const string spotifyApi = "https://api.spotify.com/v1/playlists/playlist_id/tracks";
const string songQueueLink = "https://streamlabs.com/api/v6/521267c39566929/chatbot/data/twitch_account";


//message returned by help command
const string helpMessage =
    $"{prefix}help [command] | displays info about the specified command | {prefix}commands <- list of commands";


//commands and their help responses
var commandList = new Dictionary<string, string>
{
    { "commands", "returns a all commands" },
    { "pr", $"{prefix}pr [playlist link] | request a spotify playlist" },
    { "ql", $"{prefix}ql | returns amount of songs in queue" },
    { "help", helpMessage },
    { "", helpMessage }
};


//set up credentials
var credPath = Directory.GetCurrentDirectory() + @"/Data/srBot.txt";
var allCredLines = File.ReadAllLines(credPath);
var totalCredentials = new TotalCredentials(
    allCredLines[0].Trim().ToLower().Split(",")[0],
    allCredLines[0].Trim().ToLower().Split(",")[1],
    allCredLines[1],
    allCredLines[2],
    allCredLines[3]);


var queuePath = Directory.GetCurrentDirectory() + @"/Data/queue.txt";
var queue = File.ReadAllLines(queuePath).ToList();
bool ignoreEveryone = true;

TwitchClient client;
var accessToken = new AccessToken();
accessToken.expires_in = 3600;

//token refresh
var startTimeSpan = TimeSpan.Zero;
var periodTimeSpan = TimeSpan.FromSeconds(accessToken.expires_in);
var timer = new Timer(e => { accessToken = GetToken().Result; }, null, startTimeSpan, periodTimeSpan);


InitTwitchBot();

int requestedSongs;

//request new song
var startTimeSpanReq = TimeSpan.Zero;
var periodTimeSpanReq = TimeSpan.FromSeconds(60);
var reqTimer = new Timer(e =>
{
    var songToReq = queue.FirstOrDefault();
    if (songToReq == null) return;
    if (GetCurrentRequests() >= 2) return;
    ClientSendMessage(requestCommand + " " + songToReq);

    //possibly implement check for success here
    queue.Remove(songToReq);
    File.WriteAllLines(queuePath, queue);
    Console.WriteLine("Songs left in queue: " + queue.Count);
}, null, startTimeSpanReq, periodTimeSpanReq);

//"say xyz" to write message in twitch chat by hand
while (true)
{
    var cur = Console.ReadLine();
    if (cur != null && cur.StartsWith("say"))
    {
        ClientSendMessage(cur.Remove(0, 3));
    }
    else if (cur != null && cur.ToLower().StartsWith("clearlist"))
    {
        queue = new List<string>();
        File.WriteAllLines(queuePath, queue);
        Console.WriteLine("Cleared list");
    }
    else if(cur != null && cur.ToLower().StartsWith("swapmode"))
    {
        ignoreEveryone = !ignoreEveryone;
        Console.WriteLine("Ignore Everyone: " + ignoreEveryone.ToString());
    }
}


//returns amount of songs, requested by bot, from songqueue
int GetCurrentRequests()
{
    var current = 0;
    var httpClient = new HttpClient();
    var queueRaw = httpClient.GetStringAsync(songQueueLink).Result;
    var json = JSONNode.Parse(queueRaw);
    foreach (JSONNode song in json["songlist"]["list"])
    {
        if (ignoreEveryone)
        {
            if (song["by"] == totalCredentials.TwitchBotName)
            {
                current++;
            }
        }
        else
        {
            current++;
        }
    }

    return current;
}

//gets spotify api token
async Task<AccessToken?> GetToken()
{
    Console.WriteLine("Getting Token");
    var credentials =
        $"{totalCredentials.SpotifyClientId}:{totalCredentials.SpotifyClientSecret}";

    using var httpClient = new HttpClient();
    //Define Headers
    httpClient.DefaultRequestHeaders.Accept.Clear();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

    //Prepare Request Body
    var requestData = new List<KeyValuePair<string, string>>
        { new KeyValuePair<string, string>("grant_type", "client_credentials") };

    var requestBody = new FormUrlEncodedContent(requestData);

    //Request Token
    var request = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestBody);
    var response = await request.Content.ReadAsStringAsync();


    return JsonConvert.DeserializeObject<AccessToken>(response);
}


void InitTwitchBot()
{
    var clientOptions = new ClientOptions
    {
        MessagesAllowedInPeriod = 100000000,
        ThrottlingPeriod = TimeSpan.FromSeconds(30)
    };

    var credentials =
        new ConnectionCredentials(totalCredentials.TwitchBotName, totalCredentials.TwitchOAuth);
    var customClient = new WebSocketClient(clientOptions);
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
    Console.WriteLine("Disconnected: " + e);
    Console.WriteLine("Attempting to Reconnect...");
    client.Connect();
}


//filter for command messages
void ClientOnLog(object? sender, OnLogArgs e)
{
    if (!e.Data.Contains("PRIVMSG")) return;
    var msg = e.Data.Split("PRIVMSG")[1];
    var crop = " #" + totalCredentials.ChannelsToBot.ToLower() + " :";
    ParseMessage(msg.Remove(0, crop.Length));
}

void ClientOnJoinedChannel(object? sender, OnJoinedChannelArgs e)
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

string GetCommandList()
{
    var returnString = "";
    var isFirst = true;
    foreach (var pair in commandList)
    {
        if (!isFirst)
        {
            returnString += $" | {pair.Key}";
        }
        else
        {
            isFirst = false;
        }
    }

    return returnString.Remove(returnString.Length - 2, 2);
}


//handles commands
void ParseMessage(string message)
{
    if (!message.StartsWith(prefix)) return;
    var command = message.Split(" ")[0].Remove(0, prefix.Length);
    var commandContent = message.Contains(' ') ? message.Split(" ")[1] : "";

    switch (command)
    {
        case "commands":
            ClientSendMessage($"commands(prefix = {prefix}) {GetCommandList()}");
            break;

        case "pr":
            AddSongsFromLink(commandContent);
            break;

        case "ql":
            var item = queue.FirstOrDefault();
            if (item != null)
            {
                ClientSendMessage("Songs in queue: " + queue.Count);
            }
            else
            {
                ClientSendMessage("No songs in queue");
            }

            break;

        case "help":
            if (commandList.TryGetValue(commandContent, out var neededHelp))
            {
                ClientSendMessage(neededHelp);
            }

            break;
    }
}

//adds all songs from playlist to dictionary
void AddSongsFromLink(string playlistLink)
{
    requestedSongs = 0;
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization =
        AuthenticationHeaderValue.Parse(accessToken.token_type + " " + accessToken.access_token);

    var requestResult = httpClient.GetAsync(spotifyApi.Replace("playlist_id", GetPlaylistId(playlistLink))).Result
        .Content.ReadAsStringAsync().Result;

    var json = JSONNode.Parse(requestResult);

    foreach (JSONNode item in json["items"])
    {
        var song = item["track"]["name"].Value;
        var artist = item["track"]["artists"][0]["name"].Value;

        queue.Add($"{song} - {artist}");
        requestedSongs++;
    }

    File.WriteAllLines(queuePath, queue);
    ClientSendMessage($"Added {requestedSongs} songs to request list");
    Console.WriteLine($"Added {requestedSongs} songs to request list");
}

//returns playlistID based on url
string GetPlaylistId(string playlistUrl)
{
    return playlistUrl.Split("/").Last().Split("?")[0];
}


internal class AccessToken
{
    public string access_token { get; set; }
    public string token_type { get; set; }
    public long expires_in { get; set; }
}