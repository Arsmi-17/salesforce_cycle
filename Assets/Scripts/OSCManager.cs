using UnityEngine;
using extOSC;

public class OSCManager : MonoBehaviour
{
    public static OSCManager Instance { get; private set; }

    public string remoteHost = "127.0.0.1";
    public int remotePort = 9000; //transmitter target port
    public int localPort = 8000; // receiver listen port

    private OSCReceiver _receiver;
    private OSCTransmitter _transmitter;

    private void Awake()
    {
        Instance = this;
        _transmitter = gameObject.AddComponent<OSCTransmitter>();
        _transmitter.RemoteHost = remoteHost;
        _transmitter.RemotePort = remotePort;

        _receiver = gameObject.AddComponent<OSCReceiver>();
        _receiver.LocalPort = localPort;
    }

    private void Start()
    {
        _receiver.Bind("/start-game", OnStartGame);
        _receiver.Bind("/game-data", OnGameData);
    }

    private void OnGameData(OSCMessage message)
    {
        var values = new int[message.Values.Count];
        for (int i = 0; i < message.Values.Count; i++)
        {
            values[i] = Mathf.RoundToInt(message.Values[i].FloatValue);
        }
        Debug.Log("[OSC] " + string.Join(", ", values));

        if (CycleDataManager.Instance != null)
            CycleDataManager.Instance.ProcessGameData(values);
    }

    private void OnStartGame(OSCMessage message)
    {
        var names = new string[message.Values.Count];
        for (int i = 0; i < message.Values.Count; i++)
        {
            names[i] = message.Values[i].StringValue;
        }
        Debug.Log("[OSC] /start-game " + string.Join(", ", names));

        if (GameManager.Instance != null)
            GameManager.Instance.OnStartGameReceived(names);
    }

    public void SendSerialValue(string value)
    {
        var msg = new OSCMessage("/serial-value");
        msg.AddValue(OSCValue.String(value));
        Debug.LogWarning(msg);
        _transmitter.Send(msg);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            var msg = new OSCMessage("/serial-value");
            msg.AddValue(OSCValue.String("s"));
            Debug.LogWarning(msg);
            _transmitter.Send(msg);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            var msg = new OSCMessage("/serial-value");
            msg.AddValue(OSCValue.String("f"));
            Debug.LogWarning(msg);
            _transmitter.Send(msg);
        }
    }
}
