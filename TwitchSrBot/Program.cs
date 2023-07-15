using SimpleJSON;
using TwitchLib.Client;
using TwitchSrBot;

var configFile = Directory.GetCurrentDirectory() + @"/Data/srBot.txt";
var config = new BotConfig(configFile);

var requestBot = new SongRequestBot(config);


//"say xyz" to write message in twitch chat by hand
while (true)
{
    var cur = Console.ReadLine();
    if (cur != null && cur.StartsWith("say"))
    {
        requestBot.SendMessage(cur.Remove(0, 3));
    }
    else if (cur != null && cur.ToLower().StartsWith("clearlist"))
    {
        requestBot.ResetQueue();
        Console.WriteLine("Cleared list");
    }
}






