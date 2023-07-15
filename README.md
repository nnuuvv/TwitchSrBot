# TwitchSrBot
takes spotify playlists and requests the songs 1 by 1 to a twitch sr bot
<br/><br/>
'configFile' on line 5 in Program.cs = path to config file | /Data/srBot.txt by default 
<br/>config file structure:
```
twitchBotName,oauth:[insertOAuthToken] 
streamerName
spotifyClientId
spotifyClientSecret
queueLink
prefix
streamerRequestCommand
```
  
console commands:
```
say [message to send to twitch chat]
clearlist
```

<br>

example config file: 
```
[insertTwitchBotName],oauth:[insertOAuthToken] 
[insertStreamerName]
[insertSpotifyClientId]
[insertSpotifyClientSecret]
$
https://streamlabs.com/api/v6/[randomNumberForStreamer]/data/twitch_account (get from streamer)
!sr
```
