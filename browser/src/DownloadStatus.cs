namespace Nova;
internal static class DownloadStatus
{
    public static string Describe(Transfer item, DateTime now)
    {
        if (item.State == "Completed") return "Completed · saved";
        if (item.State == "Canceled") return "Canceled";
        if (item.State == "Interrupted") return "Stopped · " + Reason(item.InterruptReason);
        var size = $"{item.BytesReceived / 1048576d:0.00} MB" + (item.TotalBytes > 0 ? $" / {item.TotalBytes / 1048576d:0.00} MB" : "");
        if (item.State == "Paused") return "Paused · " + size;
        if (item.TotalBytes > 0 && item.BytesReceived >= item.TotalBytes)
            return "Received " + size + "\nWaiting for the browser to finish. Check Browser details.";
        var waiting = now - item.LastProgress > TimeSpan.FromSeconds(20);
        return (waiting ? "Waiting for data · " : "Downloading · ") + size;
    }
    public static string Reason(string reason) => reason switch {
        "FileNoSpace" => "Not enough disk space.",
        "FileAccessDenied" => "Windows denied access to the save location.",
        "FileMalicious" => "The browser blocked this file as unsafe.",
        "FileBlockedByPolicy" => "Blocked by a Windows or browser policy.",
        "FileSecurityCheckFailed" => "The browser could not finish its security check.",
        "NetworkDisconnected" => "The connection was lost.",
        "NetworkTimeout" => "The server timed out.",
        "ServerContentLengthMismatch" => "The server sent an incomplete file.",
        "UserCanceled" => "Canceled.",
        "UserShutdown" or "DownloadProcessCrashed" or "BrowserClosed" => "The browser session ended before completion.",
        "" or "None" => "Download did not finish. Open Browser details.",
        _ => reason + ". Open Browser details."
    };
}
