using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System;

public class SocketClient
{
    Socket client;
    AutoResetEvent connectAutoResetEvent;

    //接收數據緩存區
    List<byte> receiveBuffer = new List<byte>();
    //客戶端異步接收
    SocketAsyncEventArgs receiveSAEA;
    //客戶端異步發送
    Queue<SocketAsyncEventArgs> sendSAEAQueue = new Queue<SocketAsyncEventArgs>();
    //數據發送緩存隊列
    Queue<byte[]> sendBufferQueue = new Queue<byte[]>();

    //連接服務器結果的委託
    public delegate void ConnectDg(bool result);
    public ConnectDg OnConnect;
    //斷開與服務器連接的委託
    public delegate void DisconnectDg();
    public DisconnectDg OnDisconnect;
    //接收服務器委託
    public delegate void ReceiveDataDg(byte[] data);
    public ReceiveDataDg OnReceiveData;

    /// <summary>
    /// 連接服務器
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    public void ConnectServer(string ip, int port)
    {
        if (Connected) return;

        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        client = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        //線程同步
        connectAutoResetEvent = new AutoResetEvent(false);
        SocketAsyncEventArgs e = new SocketAsyncEventArgs();
        e.UserToken = client;
        e.RemoteEndPoint = endPoint;
        e.Completed += Connect_Completed;

        client.ConnectAsync(e);
        //阻塞主線程
        connectAutoResetEvent.WaitOne(2000);
        //調用連接委託
        OnConnect?.Invoke(Connected);

        if (Connected)
        {
            receiveSAEA = new SocketAsyncEventArgs();
            receiveSAEA.RemoteEndPoint = endPoint;
            receiveSAEA.Completed += ReceiveAsync_Completed;
            byte[] buffer = new byte[10240];
            //設置接收緩存區大小
            receiveSAEA.SetBuffer(buffer, 0, buffer.Length);

            StartReceive();
        }
    }

    //開始異步接收服務器發來的消息
    public void StartReceive()
    {
        if (!client.ReceiveAsync(receiveSAEA))
        {
            ProcessReceive(receiveSAEA);
        }
    }

    //接收數據完成之後的回調
    private void ReceiveAsync_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    /// <summary>
    /// 是否與服務器連接
    /// </summary>
    public bool Connected
    {
        get { return client != null && client.Connected; }
    }
    /// <summary>
    /// 連接成功的回調
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Connect_Completed(object sender, SocketAsyncEventArgs e)
    {
        //釋放
        connectAutoResetEvent.Set();
    }

    void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
        {
            byte[] data = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesTransferred);
            //處理接收到的數據
            receiveBuffer.AddRange(data);
            if (!isReading)
            {
                isReading = true;
                ReadData();
            }
            //繼續接收數據
            StartReceive();
        }
        else
        {
            //斷開連線
            Close();
        }
    }

    bool isReading;
    /// <summary>
    /// 讀取緩存區數據
    /// </summary>
    void ReadData()
    {
        //分包、黏包(數據包 = 4個字節int+實際數據包的長度)
        if (receiveBuffer.Count < 4)
        {
            isReading = false;
            return;
        }
        byte[] lengthBytes = receiveBuffer.GetRange(0, 4).ToArray();
        int length = BitConverter.ToInt32(lengthBytes, 0);
        if (receiveBuffer.Count - 4 < length)
        {
            isReading = false;
            return;
        }
        byte[] data = receiveBuffer.GetRange(4, length).ToArray();

        lock (receiveBuffer)
        {
            receiveBuffer.RemoveRange(0, 4 + length);
        }
        //將數據交到應用層去處理
        OnReceiveData.Invoke(data);
        //遞迴讀取數據
        ReadData();
    }

    /// <summary>
    /// 向客戶端發送數據
    /// </summary>
    /// <param name="data"></param>
    public void Send(byte[] data)
    {
        if (client == null) return;
        if (data == null) return;
        sendBufferQueue.Enqueue(data);
        if (!isSending)
        {
            isSending = true;
            HandlerSend();
        }
    }

    bool isSending;
    /// <summary>
    /// 處理數據發送
    /// </summary>
    void HandlerSend()
    {
        try
        {
            lock (sendBufferQueue)
            {
                if (sendBufferQueue.Count == 0)
                {
                    isSending = false;
                    return;
                }
                SocketAsyncEventArgs send = GetSendSAEA();
                if (send == null) return;
                byte[] data = sendBufferQueue.Dequeue();
                send.SetBuffer(data, 0, data.Length);
                if (!client.SendAsync(send))
                {
                    ProccessSend(send);
                }
                HandlerSend();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("send error: " + e.Message);
        }
    }
    void ProccessSend(SocketAsyncEventArgs e)
    {
        //發送成功
        if (e.SocketError == SocketError.Success)
        {
            //回收
            sendSAEAQueue.Enqueue(e);
            if (!isSending)
            {
                isSending = true;
                HandlerSend();
            }
        }
        else
        {
            //斷開連接
            Close();
        }
    }

    //已創建的發送異步對象數量
    int sendCount;

    /// <summary>
    /// 獲取發送異步對象
    /// </summary>
    /// <returns></returns>
    SocketAsyncEventArgs GetSendSAEA()
    {
        if (sendSAEAQueue.Count == 0)
        {
            if (sendCount >= 100) return null;
            SocketAsyncEventArgs send = new SocketAsyncEventArgs();
            send.Completed += Send_Completed;
            send.UserToken = this;
            sendCount++;
            return send;
        }
        else
        {
            return sendSAEAQueue.Dequeue();
        }
    }

    /// <summary>
    /// 異步發送完成的回調
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void Send_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProccessSend(e);
    }

    /// <summary>
    /// 關閉與服務器的連接
    /// </summary>
    public void Close()
    {
        if (!Connected) return;

        sendBufferQueue.Clear();
        receiveBuffer.Clear();
        isReading = false;
        isSending = false;

        try
        {
            client.Shutdown(SocketShutdown.Both);
            client.Close();
            client = null;
        }
        catch (Exception e)
        {
            Console.WriteLine("Client close error: " + e.Message);
        }

        receiveSAEA.Completed -= ReceiveAsync_Completed;
        foreach(var item in sendSAEAQueue)
        {
            item.Completed -= Send_Completed;
        }
        OnDisconnect?.Invoke();
    }
}
