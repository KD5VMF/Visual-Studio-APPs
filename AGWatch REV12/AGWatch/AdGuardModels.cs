namespace AdGuardLiveWatch;

public sealed class QueryRow
{
    public string TimeLocal { get; set; } = "";
    public string Client { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Elapsed { get; set; } = "";
    public bool Blocked { get; set; }
    public string Upstream { get; set; } = "";
}

public sealed class AppSettings
{
    public string BaseUrl { get; set; } = "http://192.168.1.206";
    public string Username { get; set; } = "sysop";
    public string Password { get; set; } = ""; // legacy field only; kept blank after REV12 save
    public string EncryptedPassword { get; set; } = "";
    public int PollSeconds { get; set; } = 5;
    public int Limit { get; set; } = 100;
    public bool AutoStart { get; set; } = false;
}

public sealed class BarItem
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public string Detail { get; set; } = "";
}
