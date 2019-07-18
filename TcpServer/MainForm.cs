using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TcpServer.Properties;

namespace TcpServer
{
    public partial class MainForm : Form
    {
        private static readonly int RX_BUFFER_SIZE = 4 * 1024;
        private static readonly int SELECT_TIMEOUT = 100000; // in microseconds

        private bool connected = false;
        private bool keepRunning = true;

        private Dictionary<IPEndPoint, string> clientDictionary = new Dictionary<IPEndPoint, string>();
        private ConcurrentQueue<string> txQueue = new ConcurrentQueue<string>();

        public MainForm()
        {
            InitializeComponent();

            UpdateProductInfo();
            UpdateFormByConnectionState();
        }

        private void LogReceived(string s)
        {
            receivedTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\n");
        }

        private void LogSent(string s)
        {
            sentTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\n");
        }

        private void LogInfo(string s)
        {
            logTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\n");
        }

        public void UpdateProductInfo()
        {
            this.Text = $"{ProductInfo.PRODUCT_NAME} {ProductInfo.GetVersionString()}";
        }

        private void UpdateFormByConnectionState()
        {
            if (connected)
            {
                startButton.Text = Resources.Stop;
                startButton.BackColor = Color.Lime;
            }
            else
            {
                startButton.Text = Resources.Start;
                startButton.BackColor = Color.WhiteSmoke;
            }
        }

        private void ClientConnected(IPEndPoint clientEp)
        {
            clientDictionary.Add(clientEp, "");
        }

        private void ClientDisconnected(IPEndPoint clientEp)
        {
            clientDictionary.Remove(clientEp);
        }
        
        private void ListenHandler(object obj)
        {
            ArrayList rlist = null;

            try
            {
                var server = (Socket)obj;

                rlist = new ArrayList
                {
                    server
                };

                var checkRead = new ArrayList();
                var checkWrite = new ArrayList();

                checkRead.AddRange(rlist);

                var buffer = new byte[RX_BUFFER_SIZE];

                while (keepRunning && rlist.Count > 0)
                {
                    //LogInfo("Select ...");
                    Socket.Select(checkRead, checkWrite, null, SELECT_TIMEOUT);

                    foreach (var socket in checkRead)
                    {
                        if (socket == server)
                        {
                            Socket client = server.Accept();
                            rlist.Add(client);
                            
                            IPEndPoint ep = (IPEndPoint)client.RemoteEndPoint;
                            ClientConnected(ep);
                            LogInfo($"Connection[{ep.ToString()}] established.");
                        }
                        else
                        {
                            int count = ((Socket)socket).Receive(buffer);

                            if (count > 0)
                            {
                                string receivedStr = Encoding.UTF8.GetString(buffer, 0, count);
                                LogReceived($"{receivedStr.TrimEnd('\r', '\n')}");
                            }
                            else
                            {
                                IPEndPoint ep = (IPEndPoint)((Socket)socket).RemoteEndPoint;
                                ClientDisconnected(ep);
                                LogInfo($"Connection[{ep.ToString()}] broken.");
                                ((Socket)socket).Close();
                                rlist.Remove(socket);
                            }
                        }
                    }

                    foreach (var socket in checkWrite)
                    {
                        while (txQueue.TryDequeue(out string lineToSend))
                        {
                            ((Socket)socket).Send(Encoding.UTF8.GetBytes(lineToSend));
                            LogSent($"{lineToSend}");
                        }
                    }

                    checkRead.Clear();
                    checkWrite.Clear();

                    checkRead.AddRange(rlist);

                    if (txQueue.Count > 0)
                    {
                        foreach (var socket in rlist)
                        {
                            if (socket != server)
                            {
                                checkWrite.Add(socket);
                            }
                        }
                    }
                } // while (keepRunning && rlist.Count > 0)
            }
            catch (Exception ex)
            {
                LogInfo(ex.ToString());
            }
            finally
            {
                if (rlist != null)
                {
                    foreach (var socket in rlist)
                    {
                        try
                        {
                            ((Socket)socket).Close();
                        }
                        catch (Exception ex)
                        {
                            LogInfo(ex.ToString());
                        }
                    }

                    rlist.Clear();
                }

                clientDictionary.Clear();

                keepRunning = false;
                connected = false;

                LogInfo("Server stopped running.");

                UpdateFormByConnectionState();
            }
        }

        private Socket OpenConnection()
        {
            Socket server = null;

            try
            {
                var port = int.Parse(portTextBox.Text.Trim());

                server = new Socket(AddressFamily.InterNetwork,
                             SocketType.Stream,
                             ProtocolType.Tcp);
                server.Bind(new IPEndPoint(IPAddress.Any, port));
                server.Listen(2);

                connected = true;
            }
            catch (Exception ex)
            {
                LogInfo(ex.ToString());

                if (server != null)
                {
                    server.Close();
                    server = null;
                }
            }

            return server;
        }
        
        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    keepRunning = false;
                }
                else
                {
                    Socket server = OpenConnection();

                    keepRunning = true;

                    Thread thread = new Thread(ListenHandler);
                    thread.Start(server);

                    LogInfo($"Server listening on {server.LocalEndPoint.ToString()} ...");
                }
            }
            catch (Exception ex)
            {
                LogInfo(ex.ToString());
            }
            finally
            {
                UpdateFormByConnectionState();
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (clientDictionary.Count > 0)
                {
                    string lineToSend = $"{sendTextBox.Text}\n";
                    txQueue.Enqueue(lineToSend);
                }
            }
            catch (Exception ex)
            {
                LogInfo($"{ex.ToString()}");
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            keepRunning = false;
        }

        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendButton_Click(sender, e);
            }
        }

        private void ClearSentButton_Click(object sender, EventArgs e)
        {
            sentTextBox.Clear();
        }

        private void ClearReceivedButton_Click(object sender, EventArgs e)
        {
            receivedTextBox.Clear();
        }

        private void ClearLogButton_Click(object sender, EventArgs e)
        {
            logTextBox.Clear();
        }
    }
}
