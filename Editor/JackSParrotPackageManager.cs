using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class JackSParrotPackageManager : EditorWindow
{
    class PackageData
    {
        public string Name = "Empty";
        public string Url = "Empty";
        public string RemoteHash = "EmptyRemote";
        public string LocalHash = "EmptyLocal";
        public bool Installed = false;
    }

    const string kUrl = "https://script.google.com/macros/s/AKfycbyTB22eh9Todbw66Xx6qk-pUb3jzBN8ysnPlaSwZESKFoYAGbE/exec";
    const string kPrefsKey = "JACKSPARROT_PACKAGES";

    List<PackageData> _packages = new List<PackageData>();
    string _status = string.Empty;
    bool _loading = true;

    [MenuItem("JackSParrot/Packages")]
    static void OpenWindow()
    {
        var window = (JackSParrotPackageManager)GetWindow(typeof(JackSParrotPackageManager));
        window.Initialize();
        window.Show();
    }

    void Initialize(bool forced = false)
    {
        _loading = true;
        if (!EditorPrefs.HasKey(kPrefsKey) || forced)
        {
            _ = DownloadLatestConfig();
        }
        else
        {
            ParseData(EditorPrefs.GetString(kPrefsKey));
        }
    }

    void ConfigDownloaded(UnityWebRequest r)
    {
        if (!string.IsNullOrEmpty(r.error))
        {
            _status = "Error: " + r.error;
            Debug.LogError(r.error);
            return;
        }
        string data = r.downloadHandler.text;
        EditorPrefs.SetString(kPrefsKey, data);
        ParseData(data);
    }

    void ParseData(string data)
    {
        _status = "Parsing received data";
        _packages.Clear();
        var lines = data.Split(';');
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            var package = new PackageData { Name = parts[0], Url = parts[1], RemoteHash = parts[2] };
            _packages.Add(package);
        }

        _ = LoadLocalData();
    }

    void OnGUI()
    {
        void DrawPackage(PackageData package)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(package.Name, GUILayout.Width(200));
            if (package.Installed)
            {
                if (package.RemoteHash.Equals(package.LocalHash))
                {
                    GUILayout.Label("Up to date", GUILayout.Width(80));
                }
                else if (GUILayout.Button("Update", GUILayout.Width(80)))
                {
                    _ = UpdatePackage(package);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    _ = RemovePackage(package);
                }
            }
            else if (GUILayout.Button("Install", GUILayout.Width(80)))
            {
                _ = AddPackage(package);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginVertical();
        if(_loading)
        {
            GUILayout.Label(_status);
        }
        else
        {
            if (GUILayout.Button("Refresh"))
            {
                Initialize(true);
            }
            foreach (var packageData in _packages)
            {
                DrawPackage(packageData);
            }
        }
        GUILayout.EndVertical();
    }

    async Task DownloadLatestConfig()
    {
        _status = "Requesting package config to server";
        var request = new UnityWebRequest(kUrl, "GET", new DownloadHandlerBuffer(), null);
        var handler = request.SendWebRequest();
        while (!handler.isDone)
        {
            await Task.Delay(16);
        }
        ConfigDownloaded(request);
    }

    async Task LoadLocalData()
    {
        _loading = true;
        _status = "Loading local packages";
        var listRequest = UnityEditor.PackageManager.Client.List(true, false);
        while (!listRequest.IsCompleted)
        {
            await Task.Delay(16);
        }
        foreach (var package in listRequest.Result)
        {
            var packageData = _packages.Find(p => p.Name.Equals(package.name));
            if (packageData != null)
            {
                packageData.LocalHash = package.git.hash;
                packageData.Installed = true;
            }
        }
        _status = string.Empty;
        _loading = false;
    }

    async Task UpdatePackage(PackageData package)
    {
        await RemovePackage(package);
        await AddPackage(package);
    }

    async Task RemovePackage(PackageData package)
    {
        _loading = true;
        _status = "Removing package " + package.Name;
        var request = UnityEditor.PackageManager.Client.Remove(package.Name);
        while (!request.IsCompleted)
        {
            await Task.Delay(16);
        }
        package.Installed = false;
        _status = string.Empty;
        _loading = false;
    }

    async Task AddPackage(PackageData package)
    {
        _loading = true;
        _status = "Installing package " + package.Name;
        var addReq = UnityEditor.PackageManager.Client.Add(package.Url + '#' + package.RemoteHash);
        while (!addReq.IsCompleted)
        {
            await Task.Delay(16);
        }
        package.Installed = true;
        await LoadLocalData();
        _status = string.Empty;
        _loading = false;
    }
}
