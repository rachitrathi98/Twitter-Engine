open Suave
open Suave.Operators
open Suave.Filters
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open System
open Akka.FSharp
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System.Text
open Newtonsoft.Json

let system = System.create "TwitterWebSocket" (Configuration.defaultConfig())

type Command=
    | UserSubs of Set<int>*int
    | TweetUpdate of int*string
    | Wallfeed of int*List<string>
    | UserRegistration of int*string*WebSocket
    | Updates of string
    | UserSubsg of Set<int>*int
    | OfflineUser of int
    | TweetHdlr of int*string
    | LoginHdlr of int*string*WebSocket
    | Feeds of int
    | LogoutHdlr of int
    | SubUpdate of int*int
    | Retweet of int*int
    | UserSearch of int*int
    | HashTagQry of int*string
    | Tweet of int*string
    | Login of int

type Content = {
    fname : String
    lname : String
    username : String
    password : String
    usernameSearch : String
    hashtagTweet : String
    retweet: String
    tweetField: String
    usernameFollow: String
}

type Message = {
    Operation: String
    Content: Content
}
let serverPath = "akka://TwitterWebSocket/user/server"

let actorRef = select serverPath system

let join (p:Map<'a,'b>) (q:Map<'a,'b>) = Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])

let mutable db :Map<int,bool>= Map.empty
let mutable numPass :Map<int,string>= Map.empty 
let mutable numWbskt :Map<int,WebSocket>= Map.empty
let mutable tweetInfo :Map<int,string>= Map.empty
let mutable userflg :Map<int,Set<int>>= Map.empty
let mutable userSubs :Map<int,Set<int>>= Map.empty
let mutable tweets :Map<int,List<string>>= Map.empty
let mutable hashTags :Map<string,List<string>>= Map.empty
let mutable offlineUsers=0
let mutable tweetID=100

let webSkt (webSocket : WebSocket, ans:String) =
  printfn "%s" ans

  let msgByt = ans |>System.Text.Encoding.ASCII.GetBytes|>ByteSegment
  webSocket.send Text msgByt true

let ServerEngine numUsers (mailbox: Actor<_>) =
    printfn "Twitter ServerEngine Running"

    let manageLogin(numUser:int,password:string,webSocket:WebSocket) =
      let mutable ans=""

      if numPass.ContainsKey(numUser) then
      
        if numPass.[numUser]=password then
          db <- db.Add(int numUser,true)
          ans <- sprintf "User %i Logged In: Tweets of users it follows are= " numUser

          if userflg.ContainsKey(numUser) then

            for sub in userflg.[numUser] do
            
              ans<-sprintf "%s" (ans+System.String.Concat(tweets.[sub]))

        else
          ans <- sprintf "Password is Incorrect" 

      else
        ans <- sprintf "Username is not registered" 

      Async.RunSynchronously(webSkt(webSocket,ans))

   
    let manageRetweet(numUser:int,retweetID:int)=
      let mutable ans=""

      if(tweetInfo.ContainsKey(retweetID)) then
            let tR=tweetInfo.[retweetID]
            let updatedRetw = sprintf "[TweetId = %i,  Retweeted -- %s ]" tweetID tR
            tweetID <- tweetID+1
            tweetInfo <- tweetInfo.Add(tweetID,updatedRetw )
            ans <- sprintf "Tweet Sent " 
            actorRef<? Tweet(numUser,updatedRetw )
            Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

      else
            ans <- sprintf "TweetID not found"    
            Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

    let manageFeeds (numUser:int)=
      let mutable ans=""

      if(tweets.ContainsKey(numUser)) then
            let x= tweets.[numUser]
            let st=System.String.Concat(x)
            ans<-sprintf "My Tweets are:  %s" st

      else
            ans<-sprintf "No Tweets Available"

      Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))
      
    let manageTweets(numUser:int,tweet:string)=
      let mutable ans=""
      let updatedTweet=sprintf " [Tweet Id = %i, %s] | " tweetID tweet
            
      tweetInfo <- tweetInfo.Add(tweetID,updatedTweet)
      tweetID <- tweetID+1
      ans <- sprintf "Tweet sent"
      actorRef <? Tweet(numUser,updatedTweet)

      System.Threading.Thread.Sleep(100)

      Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

      if userSubs.ContainsKey(numUser) then

        for sub in userSubs.[numUser] do

          if db.[sub]=true then 
            ans <- sprintf "User %i tweeted %s" numUser updatedTweet
            Async.RunSynchronously(webSkt(numWbskt.[sub],ans))
            ()

    let manageReqUsername(numUser:int,searchUserByNum:int)=
        let mutable ans = ""

        if(tweets.ContainsKey(searchUserByNum)) then
            let temp=String.Concat(tweets.[searchUserByNum])
            ans <- sprintf "Tweets for %i are %s" searchUserByNum  temp

        else
            ans <- sprintf "No tweets Available"

        Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))
    
    let changeUserSubscribed (username:int,subs:Set<int>) =

        if userflg.ContainsKey(username) then
          let mutable ch = userflg.[username]
          ch <- ch.Add(subs.MaximumElement)
          userflg <- userflg.Add(username,ch)

        else
          userflg <- userflg.Add(username,subs)

    let userSubsUpdate  (userSubscribing:Set<int>,userToSub:int) =

        if (userSubs.ContainsKey(userSubscribing.MaximumElement)) then
          let mutable ch = userSubs.[userSubscribing.MaximumElement]
          ch <- ch.Add(userToSub)
          userSubs <- userSubs.Add(userSubscribing.MaximumElement,ch)

        else
          userSubs<-userSubs.Add(userSubscribing.MaximumElement, Set.empty.Add(userToSub))
        printfn "%A" userSubs

    let modifyTweetDB(username:int,tweet:string) =
        
        if not (tweets.ContainsKey(username)) then
            let ch = [tweet]
            tweets <- tweets.Add(username,ch)

        else
            let mutable ch=tweets.[username]
            ch <- [tweet] |> List.append ch
            tweets <- tweets.Add(username,ch)
        
        printfn "%A" tweets
        
        if tweet.IndexOf "#" <> -1 then
            let mutable i = tweet.IndexOf "#"
            let starting = i

            while i < tweet.Length && tweet.[i]<> ' ' do
                i <- i + 1

            let hashTag=tweet.[starting..i-1]

            if not (hashTags.ContainsKey(hashTag)) then
                hashTags <- hashTags.Add(hashTag,[tweet])

            else
                let mutable ch = hashTags.[hashTag]
                ch<-[tweet] |> List.append ch
                hashTags <- hashTags.Add(hashTag,ch)

        if tweet.IndexOf "@" <> -1 then
            let mutable i = tweet.IndexOf "@"
            let starting = i

            while i<tweet.Length && tweet.[i]<> ' ' do
                i <- i + 1

            let menuser = int tweet.[starting + 1..i-1]

            if db.ContainsKey(menuser) then 

                if not(tweets.ContainsKey(menuser)) then
                    let ch=[tweet]
                    tweets<-tweets.Add(menuser,ch)

                else
                    let mutable ch=tweets.[menuser]
                    ch<-[tweet] |> List.append ch
                    tweets<-tweets.Add(menuser,ch)
        
    let manageSubs(numUser:int,userToSub:int)=
      let mutable ans=""

      if(numWbskt.ContainsKey(userToSub)) then
        let mutable set :Set<int>=Set.empty.Add(userToSub)
        actorRef<!UserSubsg (set, numUser)
        actorRef<!UserSubs (set, numUser)
        ans<-sprintf "Followed Successfully"

      else
        ans<-sprintf "The User to follow does not exists"

      Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

    let hashTagQuery(numUser:int,searchHashtag:string) =
      let mutable ans = ""

      if(hashTags.ContainsKey(searchHashtag)) then
            let temp=String.Concat(hashTags.[searchHashtag])
            ans<-sprintf "Tweets having %s are: %s" searchHashtag temp

      else
            ans<-sprintf "No tweets found"

      Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

    let ManageRegistration(numUser:int,password:string,webSocket:WebSocket)=
      let mutable ans =""

      if(numPass.ContainsKey(numUser)) then
          ans <- "User already registered"

      else
          numPass<-numPass.Add(numUser,password)
          numWbskt<-numWbskt.Add(numUser,webSocket)
          ans <-  "User Signed Up Succesfully"

      Async.RunSynchronously(webSkt(numWbskt.[numUser],ans))

    let rec loop() =
            actor {
                
                let! msg = mailbox.Receive()
                let sender = mailbox.Sender()

                match msg with 

                | UserSubsg (subsSet,username) ->
                        let m = changeUserSubscribed(username, subsSet)
                        () 

                | Retweet (username,retweetID)->
                    manageRetweet(username,retweetID)
                
                | LogoutHdlr(numUser) ->
                  db<-db.Add(int numUser,false)
                  Async.RunSynchronously(webSkt(numWbskt.[numUser],"User Logged out"))

                | UserSearch (numUser,searchUserByNum) ->
                    manageReqUsername(numUser, searchUserByNum)
                           
                | TweetHdlr (numUser,tweet)->
                    manageTweets(numUser,tweet)
                  
                |Tweet (username,tweet) ->
                    modifyTweetDB(username,tweet)
                    
                | HashTagQry (username,hashtagString)->
                    hashTagQuery (username, hashtagString)

                | UserSubs (userSubscribing,userToSub) ->
                    userSubsUpdate(userSubscribing,userToSub)
              
                | OfflineUser username ->
                    db <- db.Add(username,false)
                    offlineUsers <- offlineUsers+1
                    printfn "%A" db
                    if(offlineUsers>=numUsers/2) then
                        sender <! Updates "Half Users Disconnected"

                | Login username->
                    db <- db.Add(username,true)
            
                    
                | UserRegistration (user, password, webSocket)->
                     ManageRegistration(user,password, webSocket)
                     return! loop()

                | SubUpdate (numUser,userToSub)->
                  manageSubs(numUser,userToSub)

                | Feeds (username) ->
                  manageFeeds(username)

                | LoginHdlr (numUser,password, webSocket) ->
                  manageLogin(numUser,password, webSocket)
              
                return! loop()
            }
    loop()

let serverRef = ServerEngine 1000 |> spawn system "server"

let ws (webSocket : WebSocket) (context: HttpContext) =
  
  let mutable numUser = -1
  
  socket {

    let mutable loop = true

    while loop do
      
      let mutable operation = ""
      let! msg = webSocket.read()
      printfn "%A" webSocket.send

      match msg with
      
      | (Text, data, true) ->

        let dataSt = Encoding.UTF8.GetString data 
        let info  = JsonConvert.DeserializeObject<Message> dataSt
        operation <- info.Operation
        printfn "Request Type : %s" operation
        printfn "Data Body: %s" dataSt
        let content = info.Content

        match operation with 
        | "register" ->                            
            numUser <- content.username |> int
            ()
            serverRef<! UserRegistration (numUser, content.password, webSocket)

         | "login" ->                            
            numUser <- content.username |> int
            ()
            serverRef<! LoginHdlr (numUser, content.password, webSocket)

        | "searchUser" ->                            
            let userNameToSearch = content.usernameSearch |> int
            serverRef<! UserSearch (numUser, userNameToSearch)

        | "hashtagTweet" ->                            
            serverRef<! HashTagQry (numUser, content.hashtagTweet)

        | "follow" ->  
            let userToSub = content.usernameFollow |> int                          
            serverRef<! SubUpdate (numUser, userToSub)

         | "tweet" ->  
            serverRef<! TweetHdlr (numUser, content.tweetField)

        | "retweet" ->  
            let retweetID = content.retweet|>int
            serverRef<! Retweet (numUser, retweetID)

        | "ShowWallfeed" ->  
            serverRef<! Feeds numUser

        | "Logout" ->  
            serverRef<! LogoutHdlr numUser

        | _ -> ()
      | _ -> ()
    }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Not Found" ]

[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0