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

    //�����ƾڽw�s��
    List<byte> receiveBuffer = new List<byte>();
    //�Ȥ�ݲ��B����
    SocketAsyncEventArgs receiveSAEA;
    //�Ȥ�ݲ��B�o�e
    Queue<SocketAsyncEventArgs> sendSAEAQueue = new Queue<SocketAsyncEventArgs>();
    //�ƾڵo�e�w�s���C
    Queue<byte[]> sendBufferQueue = new Queue<byte[]>();

    //�s���A�Ⱦ����G���e�U
    public delegate void ConnectDg(bool result);
    public ConnectDg OnConnect;
    //�_�}�P�A�Ⱦ��s�����e�U
    public delegate void DisconnectDg();
    public DisconnectDg OnDisconnect;
    //�����A�Ⱦ��e�U
    public delegate void ReceiveDataDg(byte[] data);
    public ReceiveDataDg OnReceiveData;

    /// <summary>
    /// �s���A�Ⱦ�
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    public void ConnectServer(string ip, int port)
    {
        if (Connected) return;

        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        client = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        //�u�{�P�B
        connectAutoResetEvent = new AutoResetEvent(false);
        SocketAsyncEventArgs e = new SocketAsyncEventArgs();
        e.UserToken = client;
        e.RemoteEndPoint = endPoint;
        e.Completed += Connect_Completed;

        client.ConnectAsync(e);
        //����D�u�{
        connectAutoResetEvent.WaitOne(2000);
        //�եγs���e�U
        OnConnect?.Invoke(Connected);

        if (Connected)
        {
            receiveSAEA = new SocketAsyncEventArgs();
            receiveSAEA.RemoteEndPoint = endPoint;
            receiveSAEA.Completed += ReceiveAsync_Completed;
            byte[] buffer = new byte[10240];
            //�]�m�����w�s�Ϥj�p
            receiveSAEA.SetBuffer(buffer, 0, buffer.Length);

            StartReceive();
        }
    }

    //�}�l���B�����A�Ⱦ��o�Ӫ�����
    public void StartReceive()
    {
        if (!client.ReceiveAsync(receiveSAEA))
        {
            ProcessReceive(receiveSAEA);
        }
    }

    //�����ƾڧ������᪺�^��
    private void ReceiveAsync_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    /// <summary>
    /// �O�_�P�A�Ⱦ��s��
    /// </summary>
    public bool Connected
    {
        get { return client != null && client.Connected; }
    }
    /// <summary>
    /// �s�����\���^��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Connect_Completed(object sender, SocketAsyncEventArgs e)
    {
        //����
        connectAutoResetEvent.Set();
    }

    void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
        {
            byte[] data = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesTransferred);
            //�B�z�����쪺�ƾ�
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
            Close();
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

        lock (receiveBuffer)
        {
            receiveBuffer.RemoveRange(0, 4 + length);
        }
        //�N�ƾڥ�����μh�h�B�z
        OnReceiveData.Invoke(data);
        //���jŪ���ƾ�
        ReadData();
    }

    /// <summary>
    /// �V�Ȥ�ݵo�e�ƾ�
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
            Close();
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
    /// �����P�A�Ⱦ����s��
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
