using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Linq;

[Serializable]
public struct PanelConfig
{
    public int id;
    public int startX;
    public int startY;
    public int width;
    public int height;
    public string ip;
    public int rxPort;
}

[Serializable]
public struct IPAddressConfig
{
    public string ip;
    public int port;
}

// filpdot config
[Serializable]
public struct FlipDotSettings
{
    public PanelConfig[] panels; /* = [
                                            {
                                               "startX": 0,
                                               "startY": 7,
                                               "width":  28,
                                               "height": 7
                                            },
                                            ...
                                         ]; */

    public string comPort;          // = "COM1";       
    public IPAddressConfig[] rxAddresses;   // ip:port addresses for udp / tcp
    public int baudRate;            // = 57600;
    public Parity parity;           // = 0; // = Parity.None;
    public int dataBits;            // = 8;
    public StopBits stopBits;       // = 1; // = Stopbits.One;
    public int lineStride;          // panelColCount * dotsPerRow
}

public class FlipDotIO : MonoBehaviour
{
    public enum FlipDotProtocol
    {
        SERIAL,
        UDP,
        TCP
    }

    [SerializeField]
    private string settingsFileName = "FlipdotSettings.json";
    [SerializeField]
    private FlipDotSettings settings;
    private int dotsInFlipdot;

    [SerializeField]
    private FlipDotProtocol protocol;
    [SerializeField]
    private IPAddressConfig[] ipAddresses;

    private SerialPort sp;
    private Dictionary<IPAddressConfig, object> portClientDictionary = new Dictionary<IPAddressConfig, object>();
    private UdpClient[] udpClients;
    private TcpClient[] tcpClients;

    private Thread sendThread;


    private void OnEnable()
    {
        LoadSettingsFile();

        switch (protocol)
        {
            case FlipDotProtocol.SERIAL:
            default:
                if (sp == null || !sp.IsOpen)
                {
                    sp = new SerialPort(settings.comPort, settings.baudRate, settings.parity, settings.dataBits, settings.stopBits);
                    sp.Open();
                }
                break;
            case FlipDotProtocol.UDP:
                udpClients = new UdpClient[ipAddresses.Length];
                for (int i = 0; i < ipAddresses.Length; i++)
                {
                    udpClients[i] = new UdpClient();
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipAddresses[i].ip), ipAddresses[i].port); // endpoint where server is listening
                    udpClients[i].Connect(ep);
                    portClientDictionary.Add(ipAddresses[i], udpClients[i]);
                    Debug.Log("UDP Connected:[" + ipAddresses[i].ip + ":" + ipAddresses[i].port + "]");
                }
                break;
            case FlipDotProtocol.TCP:
                tcpClients = new TcpClient[ipAddresses.Length];
                for (int i = 0; i < ipAddresses.Length; i++)
                {
                    tcpClients[i] = new TcpClient();
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipAddresses[i].ip), ipAddresses[i].port); // endpoint where server is listening
                    tcpClients[i].Connect(ep);
                    portClientDictionary.Add(ipAddresses[i], tcpClients[i]);
                }
                break;
        }
    }

    private void OnDisable()
    {
        switch (protocol)
        {
            case FlipDotProtocol.SERIAL:
                if (sp != null && sp.IsOpen)
                {
                    sp.Close();
                    sp.Dispose();
                }
                break;
            case FlipDotProtocol.UDP:
                if (udpClients != null)
                {
                    foreach (var udp in udpClients)
                    {
                        udp.Close();
                        udp.Dispose();
                    }
                }
                break;
            case FlipDotProtocol.TCP:
                if (tcpClients != null)
                {
                    foreach (var tcp in tcpClients)
                    {
                        tcp.Close();
                        tcp.Dispose();
                    }
                }
                break;
        }

        if (sendThread != null && sendThread.IsAlive)
        {
            sendThread.Abort();
        }
    }

    private void LoadSettingsFile()
    {
        string jsonFilePath = Path.Combine(Application.streamingAssetsPath, settingsFileName);
        string jsonString = File.ReadAllText(jsonFilePath);
        settings = JsonUtility.FromJson<FlipDotSettings>(jsonString);
        dotsInFlipdot = 0;
        for (int i = 0; i < settings.panels.Length; dotsInFlipdot += settings.panels[i].width * settings.panels[i].height, i++) ;

        ipAddresses = settings.rxAddresses;
    }

    private byte[][] Build_message(int[] fullImg, bool topToBottomStriding, int blackPoint)
    {
        // just simple check if the total data length = filpdot dots length
        if (fullImg.Length < dotsInFlipdot)
        {
            return new byte[0][]; // prevent null handling outside
        }

        // initialize the message array
        byte[][] msg = new byte[settings.panels.Length][];
        for (int i = 0; i < msg.Length; i++)
        {
            msg[i] = new byte[settings.panels[i].width];
            PanelConfig panel = settings.panels[i];
            int xs = panel.startX;
            int ys = panel.startY;
            int w = panel.width;
            int h = panel.height;

            int fullW = settings.lineStride;
            int fullH = fullImg.Length / settings.lineStride;
            //Debug.Log("[FullImg][" + fullW + "][" + fullH + "]");

            for (int x = xs; x < xs + w; x++)
            {
                byte cell = 0;
                for (int y = h - 1; y > -1; y--)
                {
                    byte pixel;
                    if (topToBottomStriding)
                    {
                        //for top-bottom striding input
                        pixel = (fullImg[x + (ys + y) * fullW] <= blackPoint ? (byte)0x00 : (byte)0x01);
                    }
                    else
                    {
                        //for bottom-top striding input
                        pixel = (fullImg[x + ((fullH - 1) - (ys + y)) * fullW] <= blackPoint ? (byte)0x00 : (byte)0x01);
                    }
                    cell = (byte)(cell << 1);
                    cell = (byte)(cell | pixel);
                }
                msg[i][x - xs] = cell;
            }
        }
        return msg;
    }

    // format the message 
    private byte[] Format_message(byte screen_id, byte[] data, bool refresh = true)
    {
        byte msg = 0x00;
        int dataLength = data.Length;
        switch (dataLength)
        {
            case 112:
                msg = (byte)(refresh ? 0x82 : 0x81);
                break;
            case 28:
                msg = (byte)(refresh ? 0x83 : 0x84);
                break;
            case 56:
                msg = (byte)(refresh ? 0x85 : 0x86);
                break;
        }
        // bytearray([0x80, msg, screen_id]) + data + bytearray([0x8F])
        byte[] result = new byte[1 + 1 + 1 + dataLength + 1];
        result[0] = 0x80;
        result[1] = msg;
        result[2] = screen_id;
        Buffer.BlockCopy(data, 0, result, 3, dataLength);
        result[3 + dataLength] = 0x8F;
        return result;
    }

    private void SendToScreen(byte screen_id, byte[] data, IPAddressConfig ipAddress = default, bool refresh = true)
    {
        byte[] formattedMsg = Format_message(screen_id, data, refresh);

        switch (protocol)
        {
            case FlipDotProtocol.SERIAL:
                if (sp.IsOpen)
                {
                    try
                    {
                        sp.Write(formattedMsg, 0, formattedMsg.Length);
                    }
                    catch (Exception e)
                    {
                        //Debug.LogWarning(e.Message);
                    }
                }
                break;
            case FlipDotProtocol.UDP:
                if (formattedMsg.Length > 0)
                {
                    object udp = default;

                    if (portClientDictionary.TryGetValue(ipAddress, out udp))
                    {
                        (udp as UdpClient).Send(formattedMsg, formattedMsg.Length);
                    }
                }
                break;
            case FlipDotProtocol.TCP:
                if (formattedMsg.Length > 0)
                {
                    TcpClient tcp = (TcpClient)portClientDictionary[ipAddress];

                    if (tcp.Connected)
                    {
                        try
                        {
                            NetworkStream stream = tcp.GetStream();

                            if (stream.CanWrite && formattedMsg.Length > 0)
                            {
                                stream.Write(formattedMsg, 0, formattedMsg.Length);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning(e.Message);
                        }
                    }
                }
                break;
        }
    }

    private void SendingThread(int[] imageData, bool topToBottomStriding, int blackPoint = 0)
    {
        byte[][] msg = Build_message(imageData, topToBottomStriding, blackPoint);

        switch (protocol)
        {
            case FlipDotProtocol.SERIAL:
                SendToScreen((byte)(1), msg[0]);
                for (int i = 0; i < msg.Length; i++)
                {
                    SendToScreen((byte)(i + 3), msg[i]);
                }
                break;
            case FlipDotProtocol.UDP:
            case FlipDotProtocol.TCP:
                foreach (var address in ipAddresses)
                {
                    //One block of dummy message because of unknown connection issue
                    SendToScreen((byte)(1), msg[0], address);
                }

                for (int i = 0; i < msg.Length; i++)
                {
                    SendToScreen((byte)settings.panels[i].id, msg[i], GetSavedIPAddress(settings.panels[i].ip, settings.panels[i].rxPort));
                }
                break;
        }
    }

    private IPAddressConfig GetSavedIPAddress(string _ip, int _port)
    {
        return ipAddresses.Where(x => x.ip == _ip && x.port == _port).FirstOrDefault();
    }

    public void SendFlipDotImage(int[] imageArray, bool topToBottomStriding = true)
    {
        switch (protocol)
        {
            case FlipDotProtocol.SERIAL:
                if (sp.IsOpen)
                {
                    sendThread = new Thread(() => SendingThread(imageArray, topToBottomStriding));
                    sendThread.IsBackground = true;
                    sendThread.Start();
                }
                break;
            case FlipDotProtocol.UDP:
            case FlipDotProtocol.TCP:
                sendThread = new Thread(() => SendingThread(imageArray, topToBottomStriding));
                sendThread.IsBackground = true;
                sendThread.Start();
                break;
        }
    }
}
