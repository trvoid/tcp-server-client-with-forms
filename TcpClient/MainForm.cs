using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TcpClient.Properties;

namespace TcpClient
{
    public partial class MainForm : Form
    {
        private static readonly int RX_BUFFER_SIZE = 4 * 1024;
        private static readonly int SELECT_TIMEOUT = 100000; // in microseconds

        private bool connected = false;
        private bool keepRunning = true;

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
            receivedTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\r\n");
        }

        private void LogSent(string s)
        {
            sentTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\r\n");
        }

        private void LogInfo(string s)
        {
            logTextBox.AppendText($"<{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}> <{s}>\r\n");
        }

        public void UpdateProductInfo()
        {
            this.Text = $"{ProductInfo.PRODUCT_NAME} {ProductInfo.GetVersionString()}";
        }

        private void UpdateFormByConnectionState()
        {
            if (connected)
            {
                connectButton.Text = Resources.Disconnect;
                connectButton.BackColor = Color.Lime;
            }
            else
            {
                connectButton.Text = Resources.Connect;
                connectButton.BackColor = Color.WhiteSmoke;
            }
        }

        private Socket OpenConnection()
        {
            Socket client = null;

            try
            {
                var address = addressTextBox.Text.Trim();
                var port = int.Parse(portTextBox.Text.Trim());

                client = new Socket(AddressFamily.InterNetwork,
                             SocketType.Stream,
                             ProtocolType.Tcp);
                client.Connect(address, port);
                connected = true;

                IPEndPoint ep = (IPEndPoint)client.RemoteEndPoint;
                LogInfo($"Connection[{ep.ToString()}] established.\n");

                return client;
            }
            catch (Exception ex)
            {
                if (client != null)
                {
                    client.Close();
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

        private void ConnectButton_Click(object sender, EventArgs e)
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
                    Socket client = OpenConnection();

                    keepRunning = true;

                    Thread thread = new Thread(ClientHandler);
                    thread.Start(client);
                }
            }
            catch (Exception ex)
            {
                LogInfo(ex.Message);
            }
            finally
            {
                UpdateFormByConnectionState();
            }
        }

        private void ClientHandler(object obj)
        {
            ArrayList rlist = null;

            try
            {
                var client = (Socket)obj;

                rlist = new ArrayList
                {
                    client
                };

                var checkRead = new ArrayList();
                var checkWrite = new ArrayList();

                var buffer = new byte[RX_BUFFER_SIZE];

                while (keepRunning && rlist.Count > 0)
                {
                    checkRead.AddRange(rlist);
                    if (txQueue.Count > 0)
                    {
                        checkWrite.Add(client);
                    }

                    //LogInfo("Select ...");
                    Socket.Select(checkRead, checkWrite, null, SELECT_TIMEOUT);

                    foreach (var socket in checkRead)
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
                                LogInfo($"Connection[{ep.ToString()}] broken.");
                                ((Socket)socket).Close();
                                rlist.Remove(socket);
                            }
                        }
                        catch (Exception e)
                        {
                            IPEndPoint ep = (IPEndPoint)((Socket)socket).RemoteEndPoint;
                            LogInfo($"Connection[{ep.ToString()}] broken.");
                            ((Socket)socket).Close();
                            rlist.Remove(socket);
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
                            LogInfo($"Connection[{ep.ToString()}] broken.");
                            ((Socket)socket).Close();
                            rlist.Remove(socket);
                        }
                    }

                    checkRead.Clear();
                    checkWrite.Clear();
                }
            }
            catch (Exception ex)
            {
                LogInfo($"{ex.ToString()}");
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

                LogInfo("Client stopped running.");

                lock (stopLock)
                {
                    keepRunning = false;
                    connected = false;

                    Monitor.PulseAll(stopLock);
                }

                UpdateFormByConnectionState();
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    string lineToSend = $"{sendTextBox.Text}";
                    txQueue.Enqueue(lineToSend);
                }
            }
            catch (Exception ex)
            {
                LogInfo(ex.Message);
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

        private void ClearReceivedButton_Click(object sender, EventArgs e)
        {
            receivedTextBox.Clear();
        }

        private void ClearSentButton_Click(object sender, EventArgs e)
        {
            sentTextBox.Clear();
        }

        private void ClearLogButton_Click(object sender, EventArgs e)
        {
            logTextBox.Clear();
        }
    }
}
