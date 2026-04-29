using System;
using UnityEngine;

namespace FlatKit {
public static class NetworkManager {
    public static void GetVersion(Action<string> callback) {
        // Disable outbound editor requests in this project to avoid startup stalls on
        // SSL/network failures from third-party readme/version-check scripts.
        Debug.Log("[Flat Kit] Version check disabled in this project.");
        callback?.Invoke(null);
    }
}
}
