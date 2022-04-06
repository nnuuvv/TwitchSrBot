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


//configurations
string prefix = "$";
string requestCommand = "!sr";
string spotifyAPI = "https://api.spotify.com/v1/playlists/playlist_id/tracks";
string songQueueLink = "https://streamlabs.com/api/v6/521267c39566929/chatbot/data/twitch_account";


//message returned by help command
string helpMessage =
    $"{prefix}help [command] | displays info about the specified command | {prefix}commands <- list of commands";


//commands and their help responses
Dictionary<string, string> commandList = new Dictionary<string, string>()
{
    {"commands", "returns a all commands"},
    {"pr", $"{prefix}pr [playlist link] | request a spotify playlist"},
    {"ql", $"{prefix}ql | returns amount of songs in queue"},
    {"help", helpMessage},
    {"", helpMessage}
};


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


string queuePath = Directory.GetCurrentDirectory() + @"/Data/queue.txt";
List<string> queue = File.ReadAllLines(queuePath).ToList();

TwitchClient client;
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
    string? songToReq = queue.FirstOrDefault();
    if (songToReq != null)
    {
        if (GetCurrentRequests() < 2)
        {
            ClientSendMessage(requestCommand + " " + songToReq);
            
            //possibly implement check for success here
            queue.Remove(songToReq);
            File.WriteAllLines(queuePath, queue);
            Console.WriteLine("Songs left in queue: " + queue.Count);
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
    else if(cur != null && cur.ToLower().StartsWith("clearlist"))
    {
        queue = new List<string>();
        File.WriteAllLines(queuePath, queue);
        Console.WriteLine("Cleared list");
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
    client.Connect();
}


//filter for command messages
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

string GetCommandList()
{
    string returnString = "";
    bool isFirst = true;
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
    return returnString.Remove(returnString.Length-2, 2);
}


//handles commands
void ParseMessage(string message)
{
    if (message.StartsWith(prefix))
    {
        string commandContent;
        var command = message.Split(" ")[0].Remove(0,prefix.Length);
        if (message.Contains(" "))
        {
            commandContent = message.Split(" ")[1];
        }
        else
        {
            commandContent = "";
        }
        
        switch (command)
        {
            case "commands":
                ClientSendMessage($"commands(prefix = {prefix}) {GetCommandList()}");
                break;
            
            case "pr":
                AddSongsFromLink(commandContent);
                break;
            
            case "ql":
                string? item = queue.FirstOrDefault();
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
                    if (neededHelp != null)
                    {
                        ClientSendMessage(neededHelp);
                    }
                }
                
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