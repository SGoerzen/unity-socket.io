# Overview
  This plugin allows you to integrate your Unity game with Socket.IO back-end
  It implements the protocol described at socket.io-protocol github repo.
  ( https://github.com/automattic/socket.io-protocol )
  
  While connected, Socket.IO run on it's own thread to avoid blocking the main
  thread. Events are queued and dispatched on the next frame they are received.

# Fork Notes
Thanks a lot to Fabio Panettieri for creating this project. Since his repository is deprecated, I have forked this project from a newer repository https://github.com/vedi/unity-socket.io (Thanks to vedi (Fedor Shubin) on this point). 

I have stabilized some event handling, added some more methods (e.g. `SetField(name, array)`) and modernized coding style. 

Because of the old state of vedi's repository, the aim is to create here an open source version with many contributors and new updates.

A Unity Asset Store release is also possible. The current official version is here: https://assetstore.unity.com/packages/tools/network/socket-io-for-unity-21721

# Quick Start

  In order to start using Socket.IO in your project you need to:
  1. Drag the SocketIO prefab from SocketIO/Prefab/ to your scene.
  2. Configure the url where your Socket.IO server is listening.
  3. Toggle the autoconnect flag if you want it to be always running.
  4. That's it! You can now start using Socket.IO in your game.


# How to use

## Obtaining the Socket.IO component reference    
    GameObject go = GameObject.Find("SocketIO");
    SocketIOComponent socket = go.GetComponent<SocketIOComponent>();

Bear in mind that using GameObject.Find might be pretty expensive, you might want to store that reference in a variable for later use.
	
## Receiving events    
Using the socket reference you can receive custom events
    
    public void Start(){
    	socket.On("boop", TestBoop);
    }
    
    public void TestBoop(SocketIOEvent e){
		Debug.Log(string.Format("[name: {0}, data: {1}]", e.name, e.data));
	}
	
Also, you can also use lambda expresions as callbacks
	
	socket.On("boop", (SocketIOEvent e) => {
		Debug.Log(string.Format("[name: {0}, data: {1}]", e.name, e.data));
	});
  
  
## Sending events
Besides listening to Socket.IO events or your custom events, you can use send information to Socket.IO server using the Emit method.
    
### Sending plain messages 
    socket.Emit("user:login");
    
### Sending additional data
    Dictionary<string, string> data = new Dictionary<string, string>();
    data["email"] = "some@email.com";
    data["pass"] = Encrypt("1234");
    socket.Emit("user:login", new JSONObject(data));

### Use callbacks       
Sometimes, you might want to get a callback when the client confirmed the message reception. To do this, simply pass a function as the last parameter of .Emit()

       socket.Emit("user:login", OnLogin);
       socket.Emit("user:login", new JSONObject(data), OnLogin);

### Obtaining current socket id (socket.sid)
  
    public void Start(){
    	socket.On("open", OnSocketOpen);
    }
    
    public void OnSocketOpen(SocketIOEvent ev){
    	Debug.Log("updated socket id " + socket.sid);
    }


### Namespace Support
Not implemented yet!
    
### Binary Events
Not implemented yet!

# Examples
  This package also includes a minimalist test that you might want to use
  to verify that you have setup your environment properly.

  1. Ensure you have installed NodeJs (https://nodejs.org/)
  2. Navigate to the server directory `cd PATH/TO/PROJECT/Assets/SocketIO/Server`
  3. Run following command `npm install`
  4. Start `beep.sh` (linux) or `beep.bat` to start the test server or run command `npm run start` 
  5. Open the test scene `SocketIO/Scenes/SocketIOTest`
  6. Run the scene. Some debug message will be printed to Unity console.
  7. Open `SocketIO/Scripts/Test/TestSocketIO.cs` to check what's going on. 

# Thanks to

Fabio Panettieri and Fedor Shubin for Socket IO version and Matt Schoen for JSONObject classes.