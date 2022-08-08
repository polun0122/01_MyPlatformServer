using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientNetworkManager : MonoBehaviour
{
    public static ClientNetworkManager Instance;
    SocketClient client;
    public string IP;
    public int Port;

    private void Awake()
    {
        Instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    public void Init()
    {
        client = new SocketClient();
        client.OnConnect += OnConnect;
        client.OnDisconnect += OnDisconnect;
        client.OnReceiveData += OnReceiveData;
    }

    public void ConnectServer()
    {
        client.ConnectServer(IP, Port);
    }

    public void DisConnectServer()
    {
        client.Close();
    }

    private void OnReceiveData(byte[] data)
    {
    }

    private void OnDisconnect()
    {
        print("OnDisconnect");
    }

    private void OnConnect(bool result)
    {
        print("OnConnect: " + result);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
