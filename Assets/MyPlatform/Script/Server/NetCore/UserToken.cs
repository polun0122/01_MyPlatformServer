using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;

public class UserToken
{
    public Socket Client;
    public DateTime ConnectTime;
    public DateTime HeartTime;
    public int UserId;
    public string UserName;
    public int UserType;

    public bool IsUsing;
    SocketServer server;
    AbsHandlerCenter handlerCenter;
    //接收數據緩存區
    List<byte> receiveBuffer = new List<byte>();
    //客戶端異步接收
    SocketAsyncEventArgs receiveSAEA;
    //客戶端異步發送
    Queue<SocketAsyncEventArgs> sendSAEAQueue;
    //數據發送緩存隊列
    Queue<byte[]> sendBufferQueue = new Queue<byte[]>();

    public UserToken(SocketServer server, AbsHandlerCenter center)
    {
        this.server = server;
        this.handlerCenter = center;
        receiveSAEA = new SocketAsyncEventArgs();
        receiveSAEA.Completed += ReceiveSAEA_Completed;
        receiveSAEA.SetBuffer(new byte[10240], 0, 10240);
        receiveSAEA.UserToken = this;
        sendSAEAQueue = new Queue<SocketAsyncEventArgs>();
    }

    /// <summary>
    /// 異步接收數據完成的回調
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ReceiveSAEA_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    /// <summary>
    /// 開始接收數據
    /// </summary>
    public void StartReceive()
    {
        if (Client == null) return;
        if (!Client.ReceiveAsync(receiveSAEA))
        {
            ProcessReceive(receiveSAEA);
        }
    }

    void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError==SocketError.Success && e.BytesTransferred > 0)
        {
            byte[] data = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesTransferred);
            //處理接收到的數據
            HeartTime = DateTime.Now;
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
            server.CloseClient(this, e.SocketError.ToString());
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

        lock(receiveBuffer)
        {
            receiveBuffer.RemoveRange(0, 4 + length);
        }
        //將數據交到應用層去處理
        handlerCenter.MessageReceive(this, data);
        //遞迴讀取數據
        ReadData();
    }

    /// <summary>
    /// 向客戶端發送數據
    /// </summary>
    /// <param name="data"></param>
    public void Send(byte[] data)
    {
        if (Client == null) return;
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
                if (!Client.SendAsync(send))
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
            server.CloseClient(this, e.SocketError.ToString());
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
    /// 關閉客戶端
    /// </summary>
    public void Close()
    {
        IsUsing = false;
        sendBufferQueue.Clear();
        receiveBuffer.Clear();
        isReading = false;
        isSending = false;

        try
        {
            Client.Shutdown(SocketShutdown.Both);
            Client.Close();
            Client = null;
        }
        catch (Exception e)
        {
            Console.WriteLine("UserToken close error: " + e.Message);
        }
    }
}
