//使用阻塞监听的方式来监听服务端口，一旦建立一个连接就新建一个线程，有这个线程单独监控这个连接
//问题退出的时候没有关闭所有线程和连接
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

namespace WindowsInternetServer
{
    class TCPServer//TCP server 服务
    {
        public int port;//需要监听的本机端口号
        public delegate void RecieveMsg(string IP_addr, int port, byte[] bytes, int length);//接受数据后，需要外部函数来处理
        public event RecieveMsg OnRecieve;
        public delegate void ExceptionMsg(string IP_addr, int port,Exception e);//发生异常函数
        public event ExceptionMsg OnException;

        private ThreadStart start;
        private Thread listenThread, clientThread;
        static private bool bListening = false;
        private TcpListener listener;
        ArrayList clientArray = new ArrayList();

        public TCPServer()//构造函数
        {
            port = 8081;//默认端口8081
            Control.CheckForIllegalCrossThreadCalls = false;           
        }

        public TCPServer(int l_port)//可以选择端口的构造函数
        {
            port = l_port;
            Control.CheckForIllegalCrossThreadCalls = false;
            
        }

        ~TCPServer()//析构函数
        {
            bListening = false;//释放所有线程
            for (int i = 0; i < clientArray.Count; i++)
            {
                Client client = (Client)clientArray[i];
                client.RelaseTcpClient();
            }
            listener.Stop();

        }

        public void Dispose()
        {
            bListening = false;//释放所有线程
        }

        public void StartListen()//开始监听
        {
            listener = new TcpListener(IPAddress.Any, port);
            start = new ThreadStart(ThreadStartListen);
            listenThread = new Thread(start); 
            bListening = true;
            listenThread.Start();//启动线程
        }

        public void StopListen()//停止监听
        {
            bListening = false;
            listener.Stop();
           // listenThread.Abort();//终止线程
            clientArray.Clear();
        }

        public int Send(string IP_addr, int port, byte[] bytes, int length)//发送数据到客户端
        {
            IP_Port ip_port = new IP_Port(IP_addr, port);
            int index = clientArray.IndexOf(ip_port);//查找对应的客户端
            if (index == -1) return -1;
            Client client = (Client)clientArray[index];
            client.tcp_client.Client.Send(bytes, length, SocketFlags.None);//发送数据
            return length;
        }

        public ArrayList GetClientList()//获取客户端列表
        {
            ArrayList client_list = new ArrayList();
            IP_Port ip_port;
            if (clientArray.Count <= 0)
                return null;
            for (int i = 0; i < clientArray.Count; i++)
            {
                ip_port = new IP_Port(((Client)clientArray[i]).IP_addr, ((Client)clientArray[i]).Port);
                client_list.Add(ip_port);
            }
            return client_list;
        }

        private void ThreadStartListen()
        {
            listener.Start();//开始监听
            //接收数据
            while (bListening)
            {
                //测试是否有数据
                try
                {
                    Client client = new Client();
                    client.tcp_client = listener.AcceptTcpClient();//停在这里一直等待连接
                    clientArray.Add(client);
                    ParameterizedThreadStart threadStart = new ParameterizedThreadStart(AcceptMsg);
                    clientThread = new Thread(threadStart);
                    clientThread.Start(client.tcp_client);
                }
                catch (Exception re)
                {
                    OnException("", 0, re);
                    clientArray.Clear();
                }
            }
            listener.Stop();
            listenThread.Abort();//执行该程式后，线程被释放了，要重新start必须要重新new一个
        }

        private void AcceptMsg(object arg)//单个线程，循环读取数据
        {

            TcpClient client = (TcpClient)arg;
            NetworkStream ns = client.GetStream();
            int port;
            string IP_addr;
            port = (client.Client.RemoteEndPoint as IPEndPoint).Port;//获取连接的端口号 explain by LC 2014.07.22 16:54
            IP_addr = (client.Client.RemoteEndPoint as IPEndPoint).Address.ToString();//获取连接的IP号 explain by LC 2014.07.22 16:55

            while (bListening)
            {
                try
                {
                    /*不能直接使用client.Connected 或 client.Client.Connected判断连接状态,先使用client.Client.Poll*/
                    if (((client.Client.Poll(1000, SelectMode.SelectRead) && (client.Client.Available == 0)) || !client.Client.Connected)) 
                        throw new ApplicationException("与客户端连接断开");
                    byte[] bytes = new byte[1024];
                    int bytesread = ns.Read(bytes, 0, bytes.Length);//读取数据
                    if (bytesread == 0)
                    { 
                        continue;
                    }
                    OnRecieve(IP_addr, port, bytes, bytesread);
             //       msg = IP_addr + ":" + port + ":" + Encoding.Default.GetString(bytes, 0, bytesread);
                    ns.Flush();
                    
                  // client.Client.Send(bytes);//发送数据到客户端 explain by LC 2014.07.23 8:53
                }
                catch (Exception re)
                {
                  //  MessageBox.Show("与客户端断开连接了");
                  //  re = new Exception("与客户端断开连接了");
                    OnException(IP_addr, port, re);
                    IP_Port ip_port = new IP_Port(IP_addr, port);
                    clientArray.Remove(ip_port);//移除对应的IP和端口号
                    break;
                }
            }
            Thread.CurrentThread.Abort();//释放当前运行的线程
        }
    }


    public class Client//将TcpClient作为父类 : TcpClient
    {
        private int _port;//端口号
        private string _IP_addr;//IP号

        public TcpClient tcp_client;

        public int Port//读取端口号
        {
            get 
            {
                _port = (tcp_client.Client.RemoteEndPoint as IPEndPoint).Port; 
                return _port;
            }
        }

        public string IP_addr
        {
            get
            {
                _IP_addr = (tcp_client.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
                return _IP_addr;
            }
        }

        public override bool Equals(Object obj)
        {
            IP_Port IP_port = obj as IP_Port;
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
            Client client = obj as Client;
            if (client != null)
            { 
                if((client.IP_addr == this.IP_addr) && (client.Port == this.Port))
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
                tcp_client.Client.Shutdown(SocketShutdown.Both);
                tcp_client.Client.Close();
                tcp_client.Close();
            }
            catch
            { 
                
            }
        }
    }

    public class IP_Port
    { 
        public string  IP_addr;
        public int port;

        public IP_Port(string e_IP, int e_port)
        {
            IP_addr = e_IP;
            port = e_port;
        }
    }
}
