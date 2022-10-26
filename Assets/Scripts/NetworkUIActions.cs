using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Netcode.Transports.Enet;

public class NetworkUIActions : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private InputField addressInput;

    public void Connect()
    {
        string address = addressInput.text.Trim();
        address = address.Length > 0 ? address : "127.0.0.1";

        ushort port = 7777;

        var networkManager = NetworkManager.Singleton;
        
        var enetTransport = networkManager.GetComponent<EnetTransport>();
        var unityTransport = networkManager.GetComponent<UnityTransport>();

        if (enetTransport)
        {
            enetTransport.Address = address;
            enetTransport.Port = port;
        }
        else if (unityTransport)
            unityTransport.SetConnectionData(Unity.Networking.Transport.NetworkEndPoint.Parse(address, port));

        networkManager.StartClient();
    }

    public void Host()
    {
        var networkManager = NetworkManager.Singleton;

        networkManager.StartHost();
    }
}
