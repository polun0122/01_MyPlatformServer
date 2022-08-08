using System.Collections;

public class HandlerCenter : AbsHandlerCenter
{
    ServerNetworkManager serverNetworkManager;
    public HandlerCenter(ServerNetworkManager manager)
    {
        serverNetworkManager = manager;
    }
    public override void ClientClose(UserToken token, string error)
    {
        serverNetworkManager.OnClientClose(token, error);
    }

    public override void ClientConnect(UserToken token)
    {
        serverNetworkManager.OnClientConnect(token);
    }

    public override void MessageReceive(UserToken token, byte[] data)
    {

    }
}
