using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class SocketServer
{
    Socket server;
    //�̤j�s�u��
    int maxClient;
    //�Ȥ�ݹ�H��
    Queue<UserToken> userTokenPool;
    //�ثe�s�u���Ȥ�ݦC��
    List<UserToken> userTokenList;
    
    //�ثe�s�u�Ȥ�ݼƶq
    int count;
    //�s���H���q
    Semaphore acceptClientSemaphore;
    //�Ȥ��UserId���ޭ�
    int userIdIndex;
    //�����B�z����
    AbsHandlerCenter handlerCenter;
    public SocketServer(AbsHandlerCenter center)
    {
        handlerCenter = center;
        server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }
    /// <summary>
    /// �ҰʪA�Ⱦ�
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
    /// �}�l���B�����Ȥ�ݳs�u
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
    /// ���B�s�u�������^��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    /// <summary>
    /// �B�z�Ȥ�ݳs��
    /// </summary>
    /// <param name="e"></param>
    void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (count >= maxClient)
        {
            Console.WriteLine("accept client is full, waiting...");
        }
        //�H���q-1
        acceptClientSemaphore.WaitOne();
        //�Ȥ�ݳs����+1
        Interlocked.Add(ref count, 1);

        UserToken token = userTokenPool.Dequeue();
        token.IsUsing = true;
        token.Client = e.AcceptSocket;
        token.ConnectTime = DateTime.Now;
        token.HeartTime = DateTime.Now;
        token.UserId = userIdIndex++;
        token.UserName = "Temp:" + token.Client.RemoteEndPoint;

        userTokenList.Add(token);
        //�q�������B�z���ߦ��Ȥ�ݳs�u
        handlerCenter.ClientConnect(token);
        //�}�l�����Ȥ�ݪ�����
        token.StartReceive();
        //�~�򲧨B�����Ȥ�ݳs�u
        StartAccpet(e);
    }

    /// <summary>
    /// �Ȥ���_�}�s��
    /// </summary>
    /// <param name="token"></param>
    public void CloseClient(UserToken token, string error)
    {
        //�q�������B�z���ߦ��Ȥ���_�}
        handlerCenter.ClientClose(token, error);

        token.Close();
        userTokenList.Remove(token);
        //�H���q+1
        acceptClientSemaphore.Release();
        userTokenPool.Enqueue(token);
        //�Ȥ�ݼƶq-1
        Interlocked.Add(ref count, -1);
    }
}
