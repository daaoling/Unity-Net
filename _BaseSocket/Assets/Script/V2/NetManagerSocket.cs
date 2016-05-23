//----------------------------------------------
//           NET        : NetManager
//           AUTHOR: zhu kun qian
//----------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/*
 * 没有使用任何阻塞函数。
 * 
 * connect是block函数，所以改为使用ConnectAsync, beginConnect也是异步，也可以使用
 * 
 * 连接成功后，socket修改为non-blocking
*/
public class NetManagerSocket
{

    // 如果断开连接，直接new socket

    public string ip;
    public int port;

    private Socket socket;
    private byte[] readBuf = new byte[64 * 1024];// 最高缓存接收64k数据。

    private int readBufIndex = 0;//已经从readBuf中读取的字节数
    private int available = 0;// readbuf已经从socket中接收的字节数

    public SocketError socketError = SocketError.Success;// 这里应该是传递到上层，还是在这里处理？

    // ---------------------------------------
    // -- decoder相关
    private int length = 0;// protobuf消息体中的长度
    private int needRead = 0;// protobuf消息体中剩余需要读取的长度
    // ---------------------------------------

    public void connect()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 5000);// 设置5秒发送超时
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
        socket.NoDelay = true;
        // socket.Blocking = false;
        socket.Blocking = true;//设置为非阻塞

        socketError = SocketError.Success;

        Debug.Log("dontLinger:" + socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger));
        try
        {
            // 这里需要修改为异步connect
            // http://msdn.microsoft.com/en-us/library/d7ew360f%28v=vs.110%29.aspx
            // Connect method will block, unless you specifically set the Blocking property to false prior to calling Connect.
            // If you are using a connection-oriented protocol like TCP and you do disable blocking, Connect will throw a SocketException because it needs time to make the connection
            // 结论：很奇怪，并没有按文档上描述的执行，设置为blocking，并没有抛出异常。

            // socket.Connect(ip, port);

            // The asynchronous BeginConnect operation must be completed by calling the EndConnect method.
            // IAsyncResult result= socket.BeginConnect(ip, port, new AsyncCallback(connectCallback), socket);

            // Debug.Log("asyncResult is completed:"+result.IsCompleted);

            // 异步处理必须设置为blocking，why?
            //*
            SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
            eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            eventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(connectCallback2);
            eventArgs.UserToken = socket;

            socket.ConnectAsync(eventArgs);

            // */
            // 这里需要添加超时处理吗？
        }
        catch (Exception ex)
        {
            Debug.LogError("socket connection exception:" + ex);
            socketError = SocketError.ConnectionRefused;
        }
    }

    private void connectCallback2(object src, SocketAsyncEventArgs result)
    {
        Debug.Log("connectCallback2, isCompleted:" + result + " " + result.SocketError);
        if (result.SocketError != SocketError.Success)
        {
            socketError = result.SocketError;
        }
        else
        {
            Socket socket = (Socket)src;
            socket.Blocking = false;
        }
    }

    void connectCallback(IAsyncResult result)
    {
        Debug.Log("connectCallback22, isCompleted:" + result.IsCompleted);
        // connected = true;
        try
        {
            Socket socket = (Socket)result.AsyncState;
            socket.EndConnect(result);

            Debug.Log("socket is connected:" + socket.Connected);
            socket.Send(new byte[] { });
        }
        catch (Exception e)
        {
            Debug.LogError("error:" + e);
            socketError = SocketError.ConnectionRefused;
        }
    }

    public void disconnect()
    {
        if (socket != null)
        {
            socket.Close();
            socket = null;
        }
    }

    //

    public NetMsg read()
    {
        // 这里同步读也可以，但只读数据接收到足够一个消息的情况
        // 使用availed预判进行同步读，不会阻塞主线程
        // 解析消息放在这里。
        //if (socket != null && socket.Available > 0)
        //{
        //    if (available == 0)
        //    {
        //        if (socket.Available < 12)
        //        {
        //            // 一个数据包，最小为12字节
        //            return null;
        //        }
        //        // 开始从socket中读入一条数据
        //        // 如果足够，就直接解析为消息返回
        //        // 如果不够，就将数据放在cache中

        //        socketRead(2);
        //        if (socketError != SocketError.Success)
        //        {
        //            return null;
        //        }

        //        length = readUshort();

        //        if (length == 0)
        //        {
        //            socketRead(4);
        //            if (socketError != SocketError.Success)
        //            {
        //                return null;
        //            }
        //            length = readInt();
        //        }
        //        int socketAvailable = socket.Available;
        //        needRead = length;
        //        if (socketAvailable < needRead)
        //        {
        //            // 不足时，socket有多少缓存数据就读多少
        //            socketRead(socketAvailable);
        //            if (socketError != SocketError.Success)
        //            {
        //                return null;
        //            }

        //            needRead = needRead - socketAvailable;
        //            return null;
        //        }
        //        else
        //        {
        //            // 数据足够，解析数据
        //            socketRead(needRead);
        //            if (socketError != SocketError.Success)
        //            {
        //                return null;
        //            }
        //            return readMsg();
        //        }
        //    }
        //    else
        //    {
        //        // 继续从socket中接收数据
        //        int socketAvailable = socket.Available;
        //        if (socketAvailable < needRead)
        //        {
        //            // 数据依旧不足
        //            socketRead(socketAvailable);
        //            if (socketError != SocketError.Success)
        //            {
        //                return null;
        //            }

        //            needRead = needRead - socketAvailable;
        //            return null;
        //        }
        //        else
        //        {
        //            // 数据足够，解析数据
        //            socketRead(needRead);
        //            if (socketError != SocketError.Success)
        //            {
        //                return null;
        //            }

        //            return readMsg();
        //        }
        //    }
        //}
        return null;
    }

    private void readReset()
    {
        // 读入一个完整消息后，重置数据
        available = 0;
        readBufIndex = 0;
        length = 0;
        needRead = 0;
    }
    private NetMsg readMsg()
    {
        NetMsg netMsg = new NetMsg();

        //UInt32 loop = readUInt();
        //short cmdId = readShort();

        //byte[] protoData = null;
        //if (cmdId > 0)
        //{
        //    protoData = new byte[length - 10];
        //    Array.Copy(readBuf, readBufIndex, protoData, 0, length - 10);
        //}
        //else
        //{
        //    // 有压缩
        //    MemoryStream inms = new MemoryStream(readBuf, readBufIndex, length - 10);
        //    MemoryStream outms = new MemoryStream();
        //    //SevenZipTool.Unzip(inms, outms);
        //    protoData = outms.ToArray();
        //    cmdId = (short)-cmdId;
        //}


        //netMsg.loop = loop;
        //netMsg.cmdType = (cmd.CmdType)cmdId;
        //netMsg.data = protoData;
        //netMsg.netdata = new MemoryStream(protoData);
        //readReset();
        return netMsg;
    }

    //private short readShort()
    //{
    //    short ret = BitConverter.ToInt16(readBuf, readBufIndex);
    //    readBufIndex += 2;
    //    return Endian.SwapInt16(ret);
    //}
    //private ushort readUshort()
    //{
    //    ushort ret = BitConverter.ToUInt16(readBuf, readBufIndex);
    //    readBufIndex += 2;
    //    return Endian.SwapUInt16(ret);
    //}

    //private int readInt()
    //{
    //    int ret = BitConverter.ToInt32(readBuf, readBufIndex);
    //    readBufIndex += 4;
    //    return Endian.SwapInt32(ret);
    //}

    //private uint readUInt()
    //{
    //    uint ret = BitConverter.ToUInt32(readBuf, readBufIndex);
    //    readBufIndex += 4;
    //    return Endian.SwapUInt32(ret);

    //}
    private void socketRead(int readLen)
    {    //从socket中读入数据入在readBuf中
        socket.Receive(readBuf, available, readLen, SocketFlags.None, out socketError);
        if (socketError != SocketError.Success)
        {
            Debug.LogError("socket Read error:" + socketError);
        }
        available += readLen;
    }

    public int send(NetMsg netMsg, int offset)
    {
        if (netMsg.totalData == null)
        {
            encode(netMsg);
        }

        int sendNum = socket.Send(netMsg.totalData, offset, netMsg.totalData.Length - offset, SocketFlags.None, out socketError);
        if (socketError != SocketError.Success)
        {
            Debug.LogError("socket send error:" + socketError);
            return 0;
        }

        if (sendNum + offset == netMsg.totalData.Length)
        {
            return -1;//标志，全部发送完成
        }
        return sendNum;
    }


    private void encode(NetMsg netMsg)
    {
        //MemoryStream outstream = new MemoryStream();
        //byte[] _t = null;

        //int totalLength = netMsg.data.Length + 10;
        //if (totalLength > 65535)
        //{
        //    _t = BitConverter.GetBytes(Endian.SwapInt16((short)0));
        //    outstream.Write(_t, 0, _t.Length);
        //    _t = BitConverter.GetBytes(Endian.SwapInt32(totalLength));
        //    outstream.Write(_t, 0, _t.Length);
        //}
        //else
        //{
        //    _t = BitConverter.GetBytes(Endian.SwapInt16((short)totalLength));
        //    outstream.Write(_t, 0, _t.Length);
        //}

        //_t = BitConverter.GetBytes(Endian.SwapUInt32(netMsg.loop));
        //outstream.Write(_t, 0, _t.Length);
        //_t = BitConverter.GetBytes(Endian.SwapInt16((short)netMsg.cmdType));
        //outstream.Write(_t, 0, _t.Length);
        //outstream.Write(netMsg.data, 0, netMsg.data.Length);
        //_t = BitConverter.GetBytes(Endian.SwapInt32((int)0));
        //outstream.Write(_t, 0, _t.Length);

        //_t = outstream.ToArray();
        //netMsg.totalData = _t;
    }

    public bool isConnected()
    {
        return socket != null && (socket.Connected);
    }
}