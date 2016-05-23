//----------------------------------------------
//           NET        : NetManager
//           AUTHOR: zhu kun qian
//----------------------------------------------

using System.IO;

public class NetMsg
{
    // 有必要用id吗？
    //
    //public cmd.CmdType cmdType;
    public uint loop;// 唯一的循环id
    public byte[] data;// protobuf二进制数据
    public byte[] totalData;//完整的消息二进制数据
    public MemoryStream netdata;

    // 这里能避免一次array copy不？
    // TODO:暂不考虑这避免arry copy的消耗，以后有时间时，可以考虑处理下。
}