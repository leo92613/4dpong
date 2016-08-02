using UnityEngine;
using System.Collections;

public class NetworkManager : MonoBehaviour {
    const string VERSION = "v0.0.1";
    public string roomName = "4DPong";
	// Use this for initialization
	void Start () {
        PhotonNetwork.ConnectUsingSettings(VERSION);
	}
	
    void OnJoinedLobby()
    {
        RoomOptions roomOptions = new RoomOptions() { IsVisible = true, MaxPlayers = 2 };
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }
	//// Update is called once per frame
	//void Update () {
	
	//}
}
