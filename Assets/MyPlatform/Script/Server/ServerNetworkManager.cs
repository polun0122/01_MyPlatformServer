using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerNetworkManager : MonoBehaviour
{
    public static ServerNetworkManager Instance;
    SocketServer socketServer;
    HandlerCenter handlerCenter;

    public int Port = 6650;
    public int MaxClient = 20;

    bool isStartServer;

    /// <summary>
    /// 當客戶端斷開連接
    /// </summary>
    /// <param name="token"></param>
    /// <param name="error"></param>
    /// <exception cref="NotImplementedException"></exception>
    internal void OnClientClose(UserToken token, string error)
    {
        print("OnClientClose: " + token.UserName);
    }

    /// <summary>
    /// 當客戶端連接
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="NotImplementedException"></exception>
    internal void OnClientConnect(UserToken token)
    {
        print("OnClientConnect: " + token.UserName);
    }

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        StartServer();
    }

    /// <summary>
    /// 開啟服務器
    /// </summary>
    public void StartServer()
    {
        if (isStartServer) return;
        isStartServer = true;
        handlerCenter = new HandlerCenter(this);
        socketServer = new SocketServer(handlerCenter);

        socketServer.Start(MaxClient, Port);
        print("Server start port: " + Port);
    }

    public void OnDestroy()
    {
        StopServer();
    }

    /// <summary>
    /// 關閉服務器
    /// </summary>
    public void StopServer()
    {
        if (!isStartServer) return;
        isStartServer = false;
        socketServer.Stop();
        print("server stop!");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
