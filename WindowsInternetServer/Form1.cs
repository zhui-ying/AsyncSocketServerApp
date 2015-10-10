//AsyncSocketServer 和 TCPServer 接口是一致的，可直接替换使用
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using AsyncSocketServername;

namespace WindowsInternetServer
{
    public partial class Form1 : Form
    {
        AsyncSocketServer tcp_server;
        //TCPServer tcp_server;
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            TcpServerInit();
            ClientListInit();
            btn_start.Enabled = true;
            btn_stop.Enabled = false;
        }

        private void TcpServerInit()
        {
            tcp_server = new AsyncSocketServer(1234);
            tcp_server.OnRecieve += new AsyncSocketServer.RecieveMsg(TcpRecieveHandle);
            tcp_server.OnException += new AsyncSocketServer.ExceptionMsg(TcpExceptionHandle);

            //tcp_server = new TCPServer(1234);
            //tcp_server.OnRecieve += new TCPServer.RecieveMsg(TcpRecieveHandle);
            //tcp_server.OnException += new TCPServer.ExceptionMsg(TcpExceptionHandle);
        }

        private void ClientListInit()
        {
            client_list.View = View.Details;
            client_list.Items.Clear();

            client_list.Columns.Add("IP addr");
            client_list.Columns.Add("port");
            client_list.Columns.Add("text");
            client_list.Columns[0].Width = 80;
            client_list.Columns[1].Width = 50;
            client_list.Columns[2].Width = 500;//调整宽度

            ListViewItem list = new ListViewItem();
            list.Text = "0.0.0.0";
            list.SubItems.Add("1234");
            list.SubItems.Add("dfghjkl");
            client_list.Items.Add(list);
        }

        private void UpdataClientList()//将IP+Port作为键值
        {
            ArrayList l_client_list;
            ListViewItem list;
            l_client_list = tcp_server.GetClientList();//获取LIST列表
            if (null == l_client_list)
            {
                client_list.Items.Clear();//全断开了就移除所有项
                return;
            }

            for (int j = 0; j < l_client_list.Count; j++)//添加新的
            {
                int i;
                AsyncSocketServername.IP_Port ip_port = (AsyncSocketServername.IP_Port)l_client_list[j];
                //WindowsInternetServer.IP_Port ip_port = (WindowsInternetServer.IP_Port)l_client_list[j];
                for (i = 0; i < client_list.Items.Count; i++)
                {
                    if (client_list.Items[i].SubItems[0].Text == ip_port.IP_addr && client_list.Items[i].SubItems[1].Text == ip_port.port.ToString())
                        break;//有记录
                }
                if (i == client_list.Items.Count || client_list.Items.Count == 0)//无记录就添加记录
                {
                    list = new ListViewItem();
                    list.Name = ip_port.IP_addr + ip_port.port.ToString();//注意名字，方便后面查找
                    list.Text = ip_port.IP_addr;
                    list.SubItems.Add(ip_port.port.ToString());
                    list.SubItems.Add("No data");
                    client_list.Items.Add(list);
                }
            }

            client_list.Update();
            for (int i = 0; i < client_list.Items.Count; i++)//删除旧的
            {
                int j;
                for (j = 0; j < l_client_list.Count; j++)
                {
                    AsyncSocketServername.IP_Port ip_port = (AsyncSocketServername.IP_Port)l_client_list[j];
                    //WindowsInternetServer.IP_Port ip_port = (WindowsInternetServer.IP_Port)l_client_list[j];
                    if (client_list.Items[i].SubItems[0].Text == ip_port.IP_addr && client_list.Items[i].SubItems[1].Text == ip_port.port.ToString())
                        break;
                }
                if (j == l_client_list.Count)//没找到，就删除
                {
                    client_list.Items[i].Remove();
                }
            }

        }

        private void TcpRecieveHandle(string IP_addr, int port, byte[] bytes, int length)
        {
            string data = Encoding.Default.GetString(bytes, 0, length);
            client_list.Items[IP_addr + port.ToString()].SubItems[2].Text = data;
            // txt_exception.AppendText(IP_addr + ":" + port.ToString() + ":" + data + "\r\n");
        }

        private void TcpExceptionHandle(string IP_addr, int port, Exception e)
        {
            txt_exception.AppendText(IP_addr + ":" + port.ToString() + ":" + e.ToString() + "\r\n");
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            try
            {
                tcp_server.port = int.Parse(txt_port.Text);

                tcp_server.StartListen();
                btn_start.Enabled = false;
                btn_stop.Enabled = true;
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); return; }
        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            tcp_server.StopListen();
            btn_start.Enabled = true;
            btn_stop.Enabled = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdataClientList();
        }

        private void Form1_Leave(object sender, EventArgs e)
        {
            btn_stop_Click(sender, e);

        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            int port;
            string IP_addr;
            ListView.SelectedIndexCollection indexes = client_list.SelectedIndices;
            if (0 == client_list.Items.Count)
            {
                MessageBox.Show("No client connect");
                return;
            }
            if (0 == indexes.Count)
            {
                MessageBox.Show("No client selected");
                return;
            }
            foreach (int index in indexes)
            {
                port = int.Parse(client_list.Items[index].SubItems[1].Text);
                IP_addr = client_list.Items[index].SubItems[0].Text;
                byte[] byteArray = System.Text.Encoding.Default.GetBytes(txt_send.Text);
                tcp_server.Send(IP_addr, port, byteArray, byteArray.Length);
            }
        }

    }
}
