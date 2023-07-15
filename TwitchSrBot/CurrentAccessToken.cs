using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using TwitchSrBot;

class CurrentAccessToken
{
    public CurrentAccessToken(BotConfig botConfig)
    {
        m_Config = botConfig;
        UpdateToken();
    }
    private readonly BotConfig m_Config;
    private AccessToken? Token;

    public AccessToken GetToken()
    {
        return Token ?? throw new InvalidOperationException("AccessToken missing");
    }
    
    private async void UpdateToken()
    {
        Console.WriteLine("Getting Token");
        var credentials = $"{m_Config.SpotifyClientId}:{m_Config.SpotifyClientSecret}";

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

        Token = JsonConvert.DeserializeObject<AccessToken>(response);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (Token != null)
        {
            var refreshTimer = new Timer(e => UpdateToken(), null, TimeSpan.Zero, TimeSpan.FromSeconds(Token.expires_in));
        }
        else
        {
            UpdateToken();
        }
    }
}

internal class AccessToken
{
    public string access_token { get; set; }
    public string token_type { get; set; }
    public long expires_in { get; set; }
}