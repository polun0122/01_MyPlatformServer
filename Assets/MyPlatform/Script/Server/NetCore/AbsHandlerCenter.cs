using System.Collections;
using System.Collections.Generic;

public abstract class AbsHandlerCenter
{
    /// <summary>
    /// �Ȥ�ݳs��
    /// </summary>
    /// <param name="token"></param>
    public abstract void ClientConnect(UserToken token);
    
    /// <summary>
    /// �Ȥ���_�}
    /// </summary>
    /// <param name="token"></param>
    /// <param name="error"></param>
    public abstract void ClientClose(UserToken token, string error); //�ǤJstring�i�H�ΨӧP�_�_�}�s�u�O�D���_�}�β��`�_�}
    
    /// <summary>
    /// ����Ȥ�ݮ���
    /// </summary>
    /// <param name="token"></param>
    /// <param name="data"></param>
    public abstract void MessageReceive(UserToken token, byte[] data);
}
