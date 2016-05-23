//----------------------------------------------
//           NET        : NetManager
//           AUTHOR: zhu kun qian
//----------------------------------------------

using System;
using System.Linq;
using System.Net.Sockets;

using UnityEngine;
using System.Collections.Generic;
using System.IO;


public class NetManager
{
    public uint loop;// 永不重复，一直加1

    public string ip = "";
    public int port = 0;

    private NetManagerSocket socket = null;

    private int maxQuerySize = 100;//接收和发送最多cache 100条消息
    private int maxReconnectTimes = 3;//重连最多重试3次

    // 发送相关
    public Queue<NetMsg> msgQuery = new Queue<NetMsg>(); // 发送消息队列
    public NetMsg reconnectTokenLogin = null;//重连发送的登陆消息
    public bool loginSuccess = false;

    private int sendBytes = 0;
    private float sendBytesTimeout = 0f; // 发送消息超时

    // 接收的消息队列
    private Queue<NetMsg> receviedMsgQueue = new Queue<NetMsg>();// 从服务器接收到的消息

    private int reconnectTryTimes = 0; // 重连次数
    private float connectTimeout = 0f; // 连接超时
    //public EventDelegate.Callback reconnectErrorCallback; // 如果出现内部无法处理的得连(1、连试3次无法重连成功 2、累积的消息数量过量，需要重启游戏客户端)


    // 如果断线后，是使用原socket重连，还是使用新的socket?新new出来
    public static NetManager Instance = null;
    public NetManager()
    {
        Instance = this;
    }

    public bool isConnected()
    {
        return socket != null && socket.isConnected();
    }
    public void connect()
    {
        if (socket != null)
        {
            Debug.LogError("socket is not closed,try close");
            socket.disconnect();
        }

        Debug.Log("start connect ip:" + ip + " port:" + port);

        socket = new NetManagerSocket();
        socket.ip = ip;
        socket.port = port;
        socket.connect();

        sendBytes = 0;
        sendBytesTimeout = 0f;

        connectTimeout = 0f;
    }

    public void disconnect()
    {
        if (socket == null)
        {
            Debug.LogError("socket is null");
            return;
        }
        socket.disconnect();
        socket = null;
    }

    public void onUpdate()
    {
        // 每祯执行，没有阻塞
        if (socket != null)
        {
            // 改为在socket中处理呢
            if (socket.socketError != SocketError.Success)
            {
                // 如果遇到sokcet错误，不需要等等超时，立即重连
                Debug.LogError("socket error:" + socket.socketError);
                tryReconnect();
                return;
            }

            if (socket.isConnected())
            {
                NetMsg msg = socket.read();
                if (msg != null)
                {
                    receviedMsgQueue.Enqueue(msg);
                }
                NetMsg netMsg = null;
                if (reconnectTokenLogin != null)
                {
                    netMsg = reconnectTokenLogin;
                }
                else
                {
                    if (loginSuccess)
                    {
                        lock (msgQuery)
                        {
                            if (msgQuery.Count > 0)
                            {
                                netMsg = msgQuery.First();
                            }
                        }
                    }
                }
                if (netMsg != null)
                {
                    socketSend(netMsg);
                }
            }
            else
            {
                if (connectTimeout == 0f)
                {
                    connectTimeout = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - connectTimeout > 5)
                {
                    // 连接5秒超时，处理重连
                    tryReconnect();
                }
            }
        }
    }

    private void tryReconnect()
    {
        //if (reconnectTryTimes >= 3)
        //{
        //    // 跳出错误处理回调，交给外部处理
        //    if (reconnectErrorCallback != null)
        //    {
        //        reconnectErrorCallback();
        //    }
        //    disconnect();
        //    return;
        //}
        //Debug.LogError("socket connect timeout, try reconnect:" + reconnectTryTimes + " " + socket.socketError);
        //reconnectTryTimes++;
        //disconnect();
        //connect();
        //// 重试需要，重新发送登陆消息505
        //LoginState.Instance.loginWithTokenSub(true);

    }

    public NetMsg readMsg()
    {
        if (receviedMsgQueue.Count == 0)
        {
            return null;
        }
        return receviedMsgQueue.Dequeue();
    }

    // true:超时
    private void socketSend(NetMsg netMsg)
    {
        // 发送数据
        //bool newMsg = false;// 新发送的消息
        //if (sendBytes == 0)
        //{
        //    newMsg = true;

        //}
        //int num = socket.send(netMsg, sendBytes);
        //if (num > 0)
        //{
        //    sendBytes += num;
        //}
        //if (num == -1)
        //{
        //    // 全部发送完成
        //    if (cmd.CmdType.tokenLogin == netMsg.cmdType)
        //    {
        //        reconnectTokenLogin = null;
        //    }
        //    else
        //    {
        //        lock (msgQuery)
        //        {
        //            msgQuery.Dequeue();
        //        }
        //    }
        //    sendBytes = 0;
        //    sendBytesTimeout = 0f;
        //}
        //else
        //{
        //    // 未发送完成,处理超时逻辑
        //    if (newMsg)
        //    {
        //        sendBytesTimeout = Time.realtimeSinceStartup;
        //    }
        //    else
        //    {
        //        // 检查时间是否超时
        //        if (Time.realtimeSinceStartup - sendBytesTimeout > 5)
        //        {
        //            // 超过5秒
        //            Debug.LogError("socket timeout.try reconnect");
        //            // 重连重发
        //            if (socket.socketError != SocketError.Success)
        //            {
        //                Debug.LogError("socket error:" + socket.socketError);
        //            }
        //            socket.socketError = SocketError.TimedOut;
        //        }
        //    }
        //}
    }

    //public void SendCmd<T>(cmd.CmdType cmdType, T protoObj)
    //{
    //    send(cmdType, protoObj);
    //}
    //public void send<T>(cmd.CmdType cmdType, T protoObj)
    //{
    //    NetMsg netMsg = new NetMsg();
    //    netMsg.loop = ++loop;
    //    netMsg.cmdType = cmdType;

    //    MemoryStream outms = new MemoryStream();
    //    ProtoBuf.Serializer.Serialize(outms, protoObj);
    //    netMsg.data = outms.ToArray();

    //    // todo:因为放在onupdate中，感觉这个lock也是可以避免掉的。暂时先加上，以后测试后再考虑去掉。
    //    // 只要能确认不会多线程操作，就可以去掉这个lock
    //    if (cmdType == cmd.CmdType.tokenLogin)
    //    {
    //        reconnectTokenLogin = netMsg;
    //        loginSuccess = false;
    //        return;
    //    }
    //    lock (msgQuery)
    //    {
    //        msgQuery.Enqueue(netMsg);
    //        // 在onupdate中发送，这样只差3ms，是可以接受的
    //    }

    //    if (msgQuery.Count > maxQuerySize)
    //    {
    //        Debug.LogError("msgQuery more than max size:" + msgQuery.Count);
    //    }
    //}
}