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
    //�����ƾڽw�s��
    List<byte> receiveBuffer = new List<byte>();
    //�Ȥ�ݲ��B����
    SocketAsyncEventArgs receiveSAEA;
    //�Ȥ�ݲ��B�o�e
    Queue<SocketAsyncEventArgs> sendSAEAQueue;
    //�ƾڵo�e�w�s���C
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
    /// ���B�����ƾڧ������^��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ReceiveSAEA_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    /// <summary>
    /// �}�l�����ƾ�
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
            //�B�z�����쪺�ƾ�
            HeartTime = DateTime.Now;
            receiveBuffer.AddRange(data);
            if (!isReading)
            {
                isReading = true;
                ReadData();
            }
            //�~�򱵦��ƾ�
            StartReceive();
        }
        else
        {
            //�_�}�s�u
            server.CloseClient(this, e.SocketError.ToString());
        }
    }

    bool isReading;
    /// <summary>
    /// Ū���w�s�ϼƾ�
    /// </summary>
    void ReadData()
    {
        //���]�B�H�](�ƾڥ] = 4�Ӧr�`int+��ڼƾڥ]������)
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
        //�N�ƾڥ�����μh�h�B�z
        handlerCenter.MessageReceive(this, data);
        //���jŪ���ƾ�
        ReadData();
    }

    /// <summary>
    /// �V�Ȥ�ݵo�e�ƾ�
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
    /// �B�z�ƾڵo�e
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
        //�o�e���\
        if (e.SocketError == SocketError.Success)
        {
            //�^��
            sendSAEAQueue.Enqueue(e);
            if (!isSending)
            {
                isSending = true;
                HandlerSend();
            }
        }
        else
        {
            //�_�}�s��
            server.CloseClient(this, e.SocketError.ToString());
        }
    }

    //�w�Ыت��o�e���B��H�ƶq
    int sendCount;

    /// <summary>
    /// ����o�e���B��H
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
    /// ���B�o�e�������^��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void Send_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProccessSend(e);
    }

    /// <summary>
    /// �����Ȥ��
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
