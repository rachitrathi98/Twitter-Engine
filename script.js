let formRegistration = document.getElementById("registration");

formRegistration.addEventListener("submit", (event) => {
  event.preventDefault();
  sendData("register", formRegistration);
});

let formLogin = document.getElementById("login");

formLogin.addEventListener("submit", (event) => {
  event.preventDefault();
  sendData("login", formLogin);
});

let formSearchUser = document.getElementById("searchUser");

formSearchUser.addEventListener("submit", (event) => {
  event.preventDefault();
  sendData("searchUser", formSearchUser);
});

let formHashtagTweets = document.getElementById("hashtagTweet");

formHashtagTweets.addEventListener("submit", (event) => {
  event.preventDefault();
  sendData("hashtagTweet", formHashtagTweets);
});

let formFollow = document.getElementById("follow");

formFollow.addEventListener("submit", (event) => {
  event.preventDefault();
  sendDataN("follow", formFollow, formLogin);
});

let tweetField = document.getElementById("tweet");

tweetField.addEventListener("submit", (event) => {
  event.preventDefault();
  sendDataN("tweet", tweetField, formLogin);
});

let retweetField = document.getElementById("retweet");

retweetField.addEventListener("submit", (event) => {
  event.preventDefault();
  sendData("retweet", retweetField);
});

function handleWallFeed() {
  sendDataN("ShowWallfeed", formFollow, formLogin);
}
function handleLogout() {
  writeToScreen("button pressed");
  sendData("Logout", formLogin);
}

var wsUri = "ws://localhost:8080/websocket";
var output;

function init() {
  output = document.getElementById("output");
  testWebSocket();
}

function testWebSocket() {
  websocket = new WebSocket(wsUri);
  websocket.onopen = function (evt) {
    onOpen(evt);
  };
  websocket.onmessage = function (evt) {
    onMessage(evt);
  };
}

function onOpen(evt) {
  writeToScreen("User Connected");
}

function onMessage(evt) {
  writeToScreen(
    '<span style="color: green;">Response: ' + evt.data + "</span>"
  );
}

function sendData(type, details) {
  var operation = type;
  var fname = "";
  var lname = "";
  var username = "";
  var password = "";
  var usernameSearch = "";
  var hashtagTweet = "";
  var retweet = "";
  var tweetField = "";
  var usernameFollow = "";

  if (operation == "register" || operation == "login") {
    username = details.elements["username"].value;
    password = details.elements["password"].value;
  }

  if (operation == "register") {
    fname = details.elements["fname"].value;
    lname = details.elements["lname"].value;
  }

  if (operation == "searchUser") {
    usernameSearch = details.elements["usernameSearch"].value;
  }

  if (operation == "hashtagTweet") {
    hashtagTweet = details.elements["hashtagTweet"].value;
  }

  if (operation == "retweet") {
    retweet = details.elements["retweet"].value;
  }

  var jsonAPI = {
    Operation: operation,
    Content: {
      fname: fname,
      lname: lname,
      username: username,
      password: password,
      usernameSearch: usernameSearch,
      hashtagTweet: hashtagTweet,
      retweet: retweet,
      tweetField: tweetField,
      usernameFollow: usernameFollow,
    },
  };

  websocket.send(JSON.stringify(jsonAPI));
}

function sendDataN(type, det, details) {
  var operation = type;
  var fname = "";
  var lname = "";
  var username = "";
  var password = "";
  var usernameSearch = "";
  var hashtagTweet = "";
  var retweet = "";
  var tweetField = "";
  var usernameFollow = "";

  if (operation == "follow") {
    usernameFollow = det.elements["username"].value;
    username = details.elements["username"].value;
  }

  if (operation == "tweet") {
    tweetField = det.elements["tweetField"].value;
    username = details.elements["username"].value;
  }

  if (operation == "ShowWallfeed") {
    username = det.elements["username"].value;
  }

  var jsonAPI = {
    Operation: operation,
    Content: {
      fname: fname,
      lname: lname,
      username: username,
      password: password,
      usernameSearch: usernameSearch,
      hashtagTweet: hashtagTweet,
      retweet: retweet,
      tweetField: tweetField,
      usernameFollow: usernameFollow,
    },
  };
  websocket.send(JSON.stringify(jsonAPI));
}
function writeToScreen(message) {
  var pre = document.createElement("p");
  pre.style.wordWrap = "break-word";
  pre.innerHTML = message;
  output.appendChild(pre);
}

window.addEventListener("load", init, false);
