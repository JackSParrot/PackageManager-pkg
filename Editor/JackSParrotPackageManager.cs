using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;

namespace JackSParrot.Editor
{
    public class JackSParrotPackageManager : EditorWindow
    {
        private class PackageData
        {
            public string Name = "Empty";
            public string Url = "Empty";
            public string RemoteHash = "EmptyRemote";
            public string LocalHash = "EmptyLocal";
            public bool Installed = false;
        }

        private const string kUrl =
            "https://script.google.com/macros/s/AKfycbyTB22eh9Todbw66Xx6qk-pUb3jzBN8ysnPlaSwZESKFoYAGbE/exec";

        private const string kPrefsDataKey = "JACKSPARROT_PACKAGES";
        private const string kPrefsTimeKey = "JACKSPARROT_PACKAGES_TIME";

        private List<PackageData> _packages = new List<PackageData>();
        private string _status = string.Empty;
        private bool _loading = true;

        [MenuItem("JackSParrot/Packages")]
        private static void OpenWindow()
        {
            JackSParrotPackageManager window = (JackSParrotPackageManager) GetWindow(typeof(JackSParrotPackageManager));
            _ = window.Initialize();
            window.Show();
        }

        private async Task Initialize(bool forced = false)
        {
            _loading = true;
            if (!EditorPrefs.HasKey(kPrefsDataKey) || forced)
            {
                await DownloadLatestConfig();
            }
            else
            {
                ParseData(EditorPrefs.GetString(kPrefsDataKey));
            }
        }

        private void OnFocus()
        {
            _ = Initialize();
        }

        private void ParseData(string data)
        {
            _status = "Parsing received data";
            _packages.Clear();
            var lines = data.Split(';');
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                var package = new PackageData {Name = parts[0], Url = parts[1], RemoteHash = parts[2]};
                _packages.Add(package);
            }

            _ = LoadLocalData();
        }

        private void OnGUI()
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
                        AddPackage(package);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        RemovePackage(package);
                    }
                }
                else if (GUILayout.Button("Install", GUILayout.Width(80)))
                {
                    AddPackage(package);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginVertical();
            if (_loading)
            {
                GUILayout.Label(_status);
            }
            else
            {
                if (GUILayout.Button("Refresh"))
                {
                    _ = Initialize(true);
                }

                foreach (var packageData in _packages)
                {
                    DrawPackage(packageData);
                }
            }

            GUILayout.EndVertical();
        }

        private async Task DownloadLatestConfig()
        {
            _status = "Requesting package config to server";
            var request = new UnityWebRequest(kUrl, "GET", new DownloadHandlerBuffer(), null);
            var handler = request.SendWebRequest();
            while (!handler.isDone)
            {
                await Task.Yield();
            }

            if (!string.IsNullOrEmpty(request.error))
            {
                _status = "Error: " + request.error;
                Debug.LogError(request.error);
                return;
            }

            string data = request.downloadHandler.text;
            EditorPrefs.SetString(kPrefsDataKey, data);
            ParseData(data);
        }

        private async Task LoadLocalData()
        {
            _loading = true;
            _status = "Loading local packages";
            var listRequest = UnityEditor.PackageManager.Client.List(true, false);
            while (!listRequest.IsCompleted)
            {
                await Task.Yield();
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

        private void RemovePackage(PackageData package)
        {
            _loading = true;
            _status = $"Removing package {package.Name}";
            UnityEditor.PackageManager.Client.Remove(package.Name);
        }

        private void AddPackage(PackageData package)
        {
            _loading = true;
            _status = $"Installing package {package.Name}";
            UnityEditor.PackageManager.Client.Add($"{package.Url}#{package.RemoteHash}");
        }
    }
}