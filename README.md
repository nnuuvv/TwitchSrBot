# TwitchSrBot
takes spotify playlists and requests the songs 1 by 1 to a twitch sr bot

credPath = path to config file 
config file structure:
  twitchBotName,oauth:[insertOAuthToken]
  streamerName
  spotifyClientId
  spotifyClientSecret
  
  
console commands:
  say [message to send to twitch chat]
  swapmode (toggles "ignore" mode)
  
  
with ignore mode enabled(default) the bot will request when theres 2< songs requested by itself in the queue
with ignore mode disabled the bot will only request when theres 2< songs in the queue total
