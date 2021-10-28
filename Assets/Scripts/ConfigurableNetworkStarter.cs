using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Netcode.Transports.Enet;

public class ConfigurableNetworkStarter : MonoBehaviour
{
    private static readonly string DEFAULT_PORT_STR = "7777";

    private struct ParsedArguments
    {
        public Dictionary<string, string> parameters;
        public HashSet<string> flags;

        public bool HasToken(params string[] names)
        {
            foreach (var name in names)
                if (flags.Contains(name) || parameters.ContainsKey(name))
                    return true;

            return false;
        }

        public string GetParameter(params string[] names)
        {
            foreach (var name in names)
                if (parameters.ContainsKey(name))
                    return parameters[name];

            return null;
        }

        public bool GetFlag(params string[] names)
        {
            foreach (var name in names)
                if (flags.Contains(name))
                    return true;

            return false;
        }

        public static ParsedArguments Parse(string[] args)
        {
            var parameters = new Dictionary<string, string>();
            var flags = new HashSet<string>();

            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i].StartsWith("-"))
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                        flags.Add(args[i]);
                    else
                        parameters.Add(args[i], args[i + 1]);
                }
            }

            return new ParsedArguments()
            {
                flags = flags,
                parameters = parameters
            };
        }
    }

    void Start()
    {
        var networkManager = NetworkManager.Singleton;
        var networkTransport = networkManager.GetComponent<EnetTransport>();

        var args = ParsedArguments.Parse(System.Environment.GetCommandLineArgs());

        if (args.HasToken("--client", "-c"))
        {
            string hostString = args.GetParameter("--client", "-c");
            string portString = args.GetParameter("--port", "-p") ?? DEFAULT_PORT_STR;

            if (hostString == null)
            {
                Debug.LogError("Please specify a host address with --client!");
                return;
            }

            ushort port = ushort.Parse(portString);

            Debug.Log($"Connecting as client to {hostString}:{port}");

            networkTransport.Port = port;
            networkTransport.Address = hostString;
            networkManager.StartClient();
        }
    }
}
