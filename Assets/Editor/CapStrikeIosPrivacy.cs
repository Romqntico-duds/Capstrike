#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class CapStrikeIosPrivacy
{
    [PostProcessBuild(100)]
    public static void Apply(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSLocalNetworkUsageDescription", "CapStrike searches for open game lobbies on your local Wi-Fi network.");
        plist.WriteToFile(plistPath);
    }
}
#endif
