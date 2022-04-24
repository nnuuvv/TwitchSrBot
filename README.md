# TwitchSrBot
<br/>takes spotify playlists and requests the songs 1 by 1 to a twitch sr bot
<br/>
<br/>credPath = path to config file 
<br/>config file structure:
<br/>  twitchBotName,oauth:[insertOAuthToken] 
<br/>  streamerName
<br/>  spotifyClientId
<br/>  spotifyClientSecret
<br/><br/>  
  
<br/>console commands:
<br/>  say [message to send to twitch chat]
<br/>  swapmode (toggles "ignore" mode)
<br/><br/>  
  
<br/>with ignore mode enabled(default) the bot will request when theres 2< songs requested by itself in the queue
<br/>with ignore mode disabled the bot will only request when theres 2< songs in the queue total
