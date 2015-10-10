//直接使用Socket的异步接收方法实现TCP/IP 的异步连接服务器
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Runtime;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;

namespace AsyncSocketServername
{
    class AsyncSocketServer
    {
        #region 公共成员
        public int port;//需要监听的本机端口号
        public delegate void RecieveMsg(string IP_addr, int port, byte[] bytes, int length);//接受数据后，需要外部函数来处理
        public event RecieveMsg OnRecieve;
        public delegate void SendMsg(string IP_addr, int port, int length);//接受数据后，需要外部函数来处理
        public event SendMsg OnSend;
        public delegate void ExceptionMsg(string IP_addr, int port, Exception e);//发生异常函数
        public event ExceptionMsg OnException;
        #endregion

        #region 私有成员
        private Socket socket;
        private ArrayList clients;
        static private bool bListening = false;
        Thread thread;
        System.Windows.Forms.Timer updata_time;

        #endregion

        #region 公共方法
        public AsyncSocketServer()
        {
            port = 1234;//默认端口号
            SystemInit();
        }

        public AsyncSocketServer(int _port)
        {
            port = _port;//默认端口号
            SystemInit();
        }

        ~AsyncSocketServer()//析构函数
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch { }
        }

        public void StartListen()//开始监听
        {
            if (bListening == false)
            {
                thread = new Thread(new ThreadStart(this.SocketInit));
                bListening = true;
                thread.Start();
            }
        }

        public void StopListen()//停止监听
        {
            if (bListening == true)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                socket.Close();
                thread.Abort();
                bListening = false;
                clients.Clear();
            }
        }

        public int Send(string IP_addr, int port, byte[] bytes, int length)//发送数据到客户端
        {
            if (clients.Count == 0) return -1;
            IP_Port ip_port = new IP_Port(IP_addr, port);
            int index = clients.IndexOf(ip_port);
            if (index < 0) return -1;
            Client client = (Client)clients[index];
            client.socket.BeginSend(bytes, 0, length, 0, new AsyncCallback(SendCallback), client);
            return length;
        }

        public ArrayList GetClientList()//获取客户端列表
        {
            ArrayList client_list = new ArrayList();
            IP_Port ip_port;
            if (clients.Count == 0) return null;
            foreach (Object obj in clients)
            {
                Client client = (Client)obj;
                ip_port = new IP_Port(client.IP_addr, client.Port);
                client_list.Add(ip_port);
            }
            return client_list;
        }
        #endregion

        private void SystemInit()
        {
            clients = new ArrayList();
            updata_time = new System.Windows.Forms.Timer();
            updata_time.Enabled = true;
            updata_time.Interval = 1000;
            updata_time.Tick += new System.EventHandler(this.updata_time_Tick);
        }
        private void SocketInit()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ip_end_point = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(ip_end_point);
            socket.Listen(int.MaxValue);//开始监听，可连接数量设为最大
            socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);
                EndPoint ip = handler.RemoteEndPoint;
                Client client = new Client();
                client.socket = handler;
                //IP_Port ip_port = new IP_Port((handler.RemoteEndPoint as IPEndPoint).Address.ToString() , (handler.RemoteEndPoint as IPEndPoint).Port);
                if (!clients.Contains(client))//判断原来有没有连接,若没有，则添加到客户端列表
                {
                    clients.Add(client);
                }
                handler.BeginReceive(client.rec_buffer, 0, Client.BufferSize, 0, new AsyncCallback(ReadCallback), client);
                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);//继续侦听其他连接
            }
            catch { }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;
            try
            {
                Socket handler = client.socket;
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)//接收到数据
                {
                    OnRecieve(client.IP_addr, client.Port, client.rec_buffer, bytesRead);//数据处理,由外部调用提供函数
                }
                handler.BeginReceive(client.rec_buffer, 0, Client.BufferSize, 0, new AsyncCallback(ReadCallback), client);
            }
            catch (Exception re)
            { OnException(client.IP_addr, client.Port, re); }
        }

        private void SendCallback(IAsyncResult ar)//发送成功后的回调函数
        {
            try
            {
                Client client = (Client)ar;
                int bytes_sent = client.socket.EndSend(ar);
                OnSend(client.IP_addr, client.Port, bytes_sent);//发送成功后返回
            }
            catch { }
        }

        private void updata_time_Tick(object sender, EventArgs e)//1s执行一次
        {
            if (clients.Count == 0) return;
            for (int i = 0; i < clients.Count; i++)
            {
                Client client = (Client)clients[i];
                if (((client.socket.Poll(1000, SelectMode.SelectRead) && (client.socket.Available == 0)) || !client.socket.Connected)) //连接中断
                {
                    client.socket.Shutdown(SocketShutdown.Both);
                    
                    //MessageBox.Show((socket.RemoteEndPoint as IPEndPoint).Address.ToString() + ":" + (socket.RemoteEndPoint as IPEndPoint).Port.ToString() + "连接中断");
                    clients.Remove(client);
                    client.socket.Close();
                }
            }
        }

    }

    public class IP_Port
    {
        public string IP_addr;
        public int port;

        public IP_Port(string e_IP, int e_port)
        {
            IP_addr = e_IP;
            port = e_port;
        }
    }

    public class Client
    {
        private int _port;//端口号
        private string _IP_addr;//IP号

        public Socket socket;
        public static int BufferSize = 1024;     // Receive buffer. 
        public byte[] rec_buffer = new byte[BufferSize];     // Received data string. 

        public int Port//读取端口号
        {
            get
            {
                try { _port = (socket.RemoteEndPoint as IPEndPoint).Port; }
                catch { }
                return _port;
            }
        }

        public string IP_addr
        {
            get
            {
                try { _IP_addr = (socket.RemoteEndPoint as IPEndPoint).Address.ToString(); }
                catch { }
                
                return _IP_addr;
            }
        }

        public override bool Equals(Object obj)
        {
            IP_Port IP_port = obj as IP_Port;//传入参数为 IP_Port
            if (IP_port != null)
            {
                if ((IP_port.IP_addr == this.IP_addr) && (IP_port.port == this.Port))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            Client client = obj as Client;//传入参数为Client
            if (client != null)
            {
                if ((client.IP_addr == this.IP_addr) && (client.Port == this.Port))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Port;
        }
        public void RelaseTcpClient()//释放TcpClient资源
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch
            {

            }
        }
    }
}
