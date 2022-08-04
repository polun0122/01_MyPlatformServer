using System.Collections;
using System.Collections.Generic;

public abstract class AbsHandlerCenter
{
    /// <summary>
    /// 客戶端連接
    /// </summary>
    /// <param name="token"></param>
    public abstract void ClientConnect(UserToken token);
    
    /// <summary>
    /// 客戶端斷開
    /// </summary>
    /// <param name="token"></param>
    /// <param name="error"></param>
    public abstract void ClientClose(UserToken token, string error); //傳入string可以用來判斷斷開連線是主動斷開或異常斷開
    
    /// <summary>
    /// 收到客戶端消息
    /// </summary>
    /// <param name="token"></param>
    /// <param name="data"></param>
    public abstract void MessageReceive(UserToken token, byte[] data);
}
