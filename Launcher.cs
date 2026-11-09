using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LauncherForm());
    }
}

internal sealed class EmbeddedSite : IDisposable
{
    private readonly TcpListener listener;
    private readonly Thread serverThread;
    private volatile bool running;
    private readonly Dictionary<string, string> resourceTypes;

    public int Port { get; private set; }
    public string Url { get { return "http://127.0.0.1:" + Port + "/E-MU_Control_Desk.html"; } }

    public EmbeddedSite()
    {
        resourceTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        resourceTypes.Add("E-MU_Control_Desk.html", "text/html; charset=utf-8");
        resourceTypes.Add("EMU-PROTEUS-2000.jpg", "image/jpeg");
        resourceTypes.Add("EMU-AUDITY-2000.jpg", "image/jpeg");
        resourceTypes.Add("EMU-VIRTUOSO-2000.jpg", "image/jpeg");
        resourceTypes.Add("EMU-XTREME-LEAD-1.jpg", "image/jpeg");
        resourceTypes.Add("E-MU-TURBOPHATT.jpg", "image/jpeg");
        resourceTypes.Add("E-MU-Orbit-3.jpg", "image/jpeg");
        resourceTypes.Add("E-MU-MOPHATT.jpg", "image/jpeg");
        resourceTypes.Add("E-MU-B-3.jpg", "image/jpeg");

        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        running = true;
        serverThread = new Thread(ListenLoop);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    private void ListenLoop()
    {
        while (running)
        {
            TcpClient client = null;
            try
            {
                client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(ServeRequest, client);
            }
            catch
            {
                if (client != null) client.Close();
                if (!running) return;
            }
        }
    }

    private void ServeRequest(object state)
    {
        using (TcpClient client = (TcpClient)state)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 5000;
                string requestLine = ReadRequestLine(stream);
                if (String.IsNullOrEmpty(requestLine)) return;
                for (int headerCount = 0; headerCount < 64; headerCount++)
                {
                    string header = ReadRequestLine(stream);
                    if (String.IsNullOrEmpty(header)) break;
                }
                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2 || parts[0] != "GET")
                {
                    WriteResponse(stream, "405 Method Not Allowed", "text/plain; charset=utf-8", new byte[0]);
                    return;
                }

                string path = new Uri(new Uri("http://127.0.0.1:" + Port + "/"), parts[1]).AbsolutePath.TrimStart('/');
                if (path.Length == 0) path = "E-MU_Control_Desk.html";
                string contentType;
                if (!resourceTypes.TryGetValue(path, out contentType))
                {
                    WriteResponse(stream, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"));
                    return;
                }

                byte[] content = ReadEmbeddedResource(path);
                if (content == null)
                {
                    WriteResponse(stream, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Embedded asset not found"));
                    return;
                }
                WriteResponse(stream, "200 OK", contentType, content);
            }
            catch
            {
            }
        }
    }

    private static string ReadRequestLine(NetworkStream stream)
    {
        StringBuilder builder = new StringBuilder();
        while (builder.Length < 4096)
        {
            int value = stream.ReadByte();
            if (value < 0 || value == '\n') break;
            if (value != '\r') builder.Append((char)value);
        }
        return builder.ToString();
    }

    private static byte[] ReadEmbeddedResource(string name)
    {
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (source == null) return null;
            using (MemoryStream target = new MemoryStream())
            {
                source.CopyTo(target);
                return target.ToArray();
            }
        }
    }

    private static void WriteResponse(NetworkStream stream, string status, string contentType, byte[] content)
    {
        string headers = "HTTP/1.1 " + status + "\r\n" +
            "Content-Type: " + contentType + "\r\n" +
            "Content-Length: " + content.Length + "\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (content.Length > 0) stream.Write(content, 0, content.Length);
        stream.Flush();
    }

    public void Dispose()
    {
        running = false;
        try { listener.Stop(); } catch { }
        try { if (serverThread.IsAlive) serverThread.Join(500); } catch { }
    }
}

internal sealed class LauncherForm : Form
{
    private const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string EdgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
    private EmbeddedSite site;
    private readonly Label statusLabel;
    private readonly Button openButton;

    public LauncherForm()
    {
        Text = "E-MU Control Desk";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(430, 142);
        BackColor = Color.FromArgb(25, 30, 28);
        ForeColor = Color.FromArgb(237, 240, 233);

        Label title = new Label();
        title.Text = "E-MU Control Desk";
        title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        title.Location = new Point(18, 14);
        title.AutoSize = true;
        Controls.Add(title);

        statusLabel = new Label();
        statusLabel.Text = "Starting local secure-context host...";
        statusLabel.Location = new Point(20, 52);
        statusLabel.Size = new Size(390, 34);
        statusLabel.Font = new Font("Segoe UI", 9);
        Controls.Add(statusLabel);

        openButton = new Button();
        openButton.Text = "Open Control Desk";
        openButton.Location = new Point(20, 98);
        openButton.Size = new Size(150, 30);
        openButton.Enabled = false;
        openButton.Click += delegate { OpenControlDesk(); };
        Controls.Add(openButton);

        Button exitButton = new Button();
        exitButton.Text = "Exit";
        exitButton.Location = new Point(180, 98);
        exitButton.Size = new Size(80, 30);
        exitButton.Click += delegate { Close(); };
        Controls.Add(exitButton);

        Shown += delegate { StartHost(); };
        FormClosed += delegate { if (site != null) site.Dispose(); };
    }

    private void StartHost()
    {
        try
        {
            site = new EmbeddedSite();
            statusLabel.Text = "Ready at localhost. Keep this launcher open while using the editor.";
            openButton.Enabled = true;
            OpenControlDesk();
        }
        catch (Exception error)
        {
            statusLabel.Text = "Could not start the local host: " + error.Message;
            MessageBox.Show(this, error.Message, "E-MU Control Desk", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenControlDesk()
    {
        if (site == null) return;
        string browser = File.Exists(ChromePath) ? ChromePath : (File.Exists(EdgePath) ? EdgePath : null);
        try
        {
            if (browser != null)
            {
                Process.Start(new ProcessStartInfo(browser, "--app=\"" + site.Url + "\" --new-window") { UseShellExecute = false });
            }
            else
            {
                Process.Start(new ProcessStartInfo(site.Url) { UseShellExecute = true });
            }
            statusLabel.Text = "Editor opened in Chromium. Allow MIDI and SysEx access when prompted.";
        }
        catch (Exception error)
        {
            statusLabel.Text = "Could not open the browser: " + error.Message;
        }
    }
}
