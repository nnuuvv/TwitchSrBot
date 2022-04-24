# TwitchSrBot
takes spotify playlists and requests the songs 1 by 1 to a twitch sr bot
<br/><br/>
credPath = path to config file 
<br/>config file structure:
```
twitchBotName,oauth:[insertOAuthToken] 
streamerName
spotifyClientId
spotifyClientSecret
```
  
console commands:
```
say [message to send to twitch chat]
swapmode (toggles "ignore" mode)
```

ignore mode:
```
enabled(default): requests when 2< songs requested by itself in the queue
disabled: requests when 2< songs in the queue total
```
