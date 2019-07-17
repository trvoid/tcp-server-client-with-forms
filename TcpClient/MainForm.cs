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
                connectButton.Text = Resources.Disconnect;
                connectButton.BackColor = Color.Lime;
            }
            else
            {
                connectButton.Text = Resources.Connect;
                connectButton.BackColor = Color.WhiteSmoke;
            }

            sendButton.Enabled = connected;
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
            }
            catch (Exception ex)
            {
                LogInfo($"{ex.ToString()}");

                if (client != null)
                {
                    client.Close();
                    client = null;
                }
            }

            return client;
        }
        
        private void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    keepRunning = false;
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
                LogInfo($"{ex.ToString()}");
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
                        int count = ((Socket)socket).Receive(buffer);

                        if (count > 0)
                        {
                            string receivedStr = Encoding.UTF8.GetString(buffer, 0, count);
                            string[] lines = receivedStr.Split(
                                new[] { "\r\n", "\r", "\n" },
                                StringSplitOptions.None
                            );
                            foreach (var line in lines)
                            {
                                if (!string.IsNullOrEmpty(line))
                                {
                                    LogReceived($"{line}");
                                }
                            }
                        }
                        else
                        {
                            IPEndPoint ep = (IPEndPoint)((Socket)socket).RemoteEndPoint;
                            LogInfo($"Connection[{ep.ToString()}] broken.");
                            ((Socket)socket).Close();
                            rlist.Remove(socket);
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

                keepRunning = false;
                connected = false;

                LogInfo("Client stopped running.");

                UpdateFormByConnectionState();
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
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
    }
}
