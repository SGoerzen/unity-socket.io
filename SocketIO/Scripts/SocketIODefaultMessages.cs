using UnityEngine;
using SocketIO;

public class SocketIODefaultMessages : MonoBehaviour
{
    public SocketIOComponent socket;
    // Start is called before the first frame update
    void Start()
    {
        if (!socket)
        {
            var go = GameObject.Find("SocketIO");
            socket = go.GetComponent<SocketIOComponent>();
        }

        Debug.Log("Connecting to '" + socket.url + "'.");

        socket.On("open", OnOpen);
        socket.On("close", OnClose);
        socket.On("error", OnError);

        var json = new JSONObject();
        json.SetField("test", 1.5f);
        print(json.GetFloat("test"));
    }

    void OnOpen(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Open received: " + e.name + " " + e.data);
    }

    void OnClose(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Close received: " + e.name + " " + e.data);
    }

    void OnError(SocketIOEvent e)
    {
        Debug.LogError("[SocketIO] Error received: " + e.name + " " + e.data);
    }
}
