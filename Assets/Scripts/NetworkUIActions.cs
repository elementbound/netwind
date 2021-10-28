using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Netcode.Transports.Enet;

public class NetworkUIActions : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private InputField addressInput;

    public void Connect()
    {
        var networkManager = NetworkManager.Singleton;
        var networkTransport = networkManager.GetComponent<EnetTransport>();

        networkTransport.Address = addressInput.text;
        networkManager.StartClient();
    }

    public void Host()
    {
        var networkManager = NetworkManager.Singleton;

        networkManager.StartHost();
    }
}
