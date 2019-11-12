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

        private static readonly object stopLock = new object();

        public MainForm()
        {
            InitializeComponent();

            UpdateProductInfo();
            UpdateFormByConnectionState();
        }

        private void LogReceived(string s)
        {
            receivedTextBox.AppendText($"<{DateTime.Now.ToString("yy-MM-dd HH:mm:ss")}> <{s}>\r\n");
        }

        private void LogSent(string s)
        {
            sentTextBox.AppendText($"<{DateTime.Now.ToString("yy-MM-dd HH:mm:ss")}> <{s}>\r\n");
        }

        private void LogDebug(string s)
        {
            logTextBox.AppendText($"<{DateTime.Now.ToString("yy-MM-dd HH:mm:ss")}> <{s}>\r\n");
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
                            LogDebug($"Connection[{ep.ToString()}] established.");
                        }
                        else
                        {
                            try
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
                                    LogDebug($"Connection[{ep.ToString()}] closed.");
                                    ((Socket)socket).Close();
                                    rlist.Remove(socket);
                                }
                            }
                            catch (Exception e)
                            {
                                IPEndPoint ep = (IPEndPoint)((Socket)socket).RemoteEndPoint;
                                ClientDisconnected(ep);
                                LogDebug($"Connection[{ep.ToString()}] broken.");
                                ((Socket)socket).Close();
                                rlist.Remove(socket);
                            }
                        }
                    }

                    foreach (var socket in checkWrite)
                    {
                        try
                        {
                            while (txQueue.TryDequeue(out string lineToSend))
                            {
                                ((Socket)socket).Send(Encoding.UTF8.GetBytes($"{lineToSend}\r\n"));
                                LogSent($"{lineToSend}");
                            }
                        }
                        catch (Exception e)
                        {
                            IPEndPoint ep = (IPEndPoint)((Socket)socket).RemoteEndPoint;
                            ClientDisconnected(ep);
                            LogDebug($"Connection[{ep.ToString()}] broken.");
                            ((Socket)socket).Close();
                            rlist.Remove(socket);
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
                LogDebug(ex.ToString());
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
                            LogDebug(ex.ToString());
                        }
                    }

                    rlist.Clear();
                }

                clientDictionary.Clear();

                LogDebug("Server stopped running.");

                lock (stopLock)
                {
                    keepRunning = false;
                    connected = false;

                    Monitor.PulseAll(stopLock);
                }
            }
        }

        private Socket OpenConnection()
        {
            Socket server = null;

            try
            {
                var portStr = portTextBox.Text.Trim();
                if (string.IsNullOrEmpty(portStr))
                {
                    LogDebug("Port is empty.");
                    return null;
                }

                var port = int.Parse(portStr);

                server = new Socket(AddressFamily.InterNetwork,
                             SocketType.Stream,
                             ProtocolType.Tcp);
                server.Bind(new IPEndPoint(IPAddress.Any, port));
                server.Listen(2);

                connected = true;

                return server;
            }
            catch (Exception ex)
            {
                if (server != null)
                {
                    server.Close();
                }

                throw ex;
            }
        }

        private void CloseConnection()
        {
            lock (stopLock)
            {
                keepRunning = false;
                        
                while (connected) Monitor.Wait(stopLock);
            }
        }
        
        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    CloseConnection();

                    UpdateFormByConnectionState();
                }
                else
                {
                    Socket server = OpenConnection();

                    if (server != null)
                    {
                        keepRunning = true;

                        Thread thread = new Thread(ListenHandler);
                        thread.Start(server);

                        LogDebug($"Server listening on {server.LocalEndPoint.ToString()} ...");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug(ex.Message);
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
                    string lineToSend = $"{sendTextBox.Text}";
                    txQueue.Enqueue(lineToSend);
                }
            }
            catch (Exception ex)
            {
                LogDebug(ex.Message);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseConnection();
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
