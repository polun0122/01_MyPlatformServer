using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class SocketServer
{
    Socket server;
    //最大連線數
    int maxClient;
    //客戶端對象池
    Queue<UserToken> userTokenPool;
    //目前連線的客戶端列表
    List<UserToken> userTokenList;
    
    //目前連線客戶端數量
    int count;
    //連接信號量
    Semaphore acceptClientSemaphore;
    //客戶端UserId索引值
    int userIdIndex;
    //消息處理中心
    AbsHandlerCenter handlerCenter;
    public SocketServer(AbsHandlerCenter center)
    {
        handlerCenter = center;
        server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }
    /// <summary>
    /// 啟動服務器
    /// </summary>
    /// <param name="max"></param>
    /// <param name="port"></param>
    public void Start(int max, int port)
    {
        maxClient = max;
        userTokenPool = new Queue<UserToken>(maxClient);
        userTokenList = new List<UserToken>();
        acceptClientSemaphore = new Semaphore(maxClient, maxClient);
        for (int i = 0; i < maxClient; i++)
        {
            UserToken token = new UserToken(this, handlerCenter);
            userTokenPool.Enqueue(token);
        }
        server.Bind(new IPEndPoint(IPAddress.Any, port));
        server.Listen(2);
        StartAccpet(null);
    }

    public void Stop()
    {
        try
        {
            for (int i = 0; i < userTokenList.Count; i++)
            {
                userTokenList[i].Close();
            }
            userTokenList.Clear();
            userTokenPool.Clear();
            server.Close();
            server = null;
        }
        catch (Exception e)
        {
            Console.WriteLine("Stop server error: " + e.Message);
        }
    }

    /// <summary>
    /// 開始異步接收客戶端連線
    /// </summary>
    /// <param name="e"></param>
    void StartAccpet(SocketAsyncEventArgs e)
    {
        if (e == null)
        {
            e = new SocketAsyncEventArgs();
            e.Completed += AcceptCompleted;
        }
        else
        {
            e.AcceptSocket = null;
        }
        if (!server.AcceptAsync(e))
        {
            ProcessAccept(e);
        }
    }

    /// <summary>
    /// 異步連線完成的回調
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    /// <summary>
    /// 處理客戶端連接
    /// </summary>
    /// <param name="e"></param>
    void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (count >= maxClient)
        {
            Console.WriteLine("accept client is full, waiting...");
        }
        //信號量-1
        acceptClientSemaphore.WaitOne();
        //客戶端連接數+1
        Interlocked.Add(ref count, 1);

        UserToken token = userTokenPool.Dequeue();
        token.IsUsing = true;
        token.Client = e.AcceptSocket;
        token.ConnectTime = DateTime.Now;
        token.HeartTime = DateTime.Now;
        token.UserId = userIdIndex++;
        token.UserName = "Temp:" + token.Client.RemoteEndPoint;

        userTokenList.Add(token);
        //通知消息處理中心有客戶端連線
        handlerCenter.ClientConnect(token);
        //開始接收客戶端的消息
        token.StartReceive();
        //繼續異步接收客戶端連線
        StartAccpet(e);
    }

    /// <summary>
    /// 客戶端斷開連接
    /// </summary>
    /// <param name="token"></param>
    public void CloseClient(UserToken token, string error)
    {
        //通知消息處理中心有客戶端斷開
        handlerCenter.ClientClose(token, error);

        token.Close();
        userTokenList.Remove(token);
        //信號量+1
        acceptClientSemaphore.Release();
        userTokenPool.Enqueue(token);
        //客戶端數量-1
        Interlocked.Add(ref count, -1);
    }
}
