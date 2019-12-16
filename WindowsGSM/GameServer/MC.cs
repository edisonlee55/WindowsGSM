﻿using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using System.Net;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.GameServer
{
    class MC
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly string _serverId;

        private string _param;
        public string Error;
        public string Notice;

        public const string FullName = "Minecraft: Java Edition Server";
        public string port = "25565";
        public string defaultmap = "world";
        public string maxplayers = "20";
        public string additional = "-Xmx1024M -Xms1024M";

        public MC(string serverid)
        {
            _serverId = serverid;
        }

        public void CreateServerCFG(string serverName, string serverIP, string serverPort, string rcon_password)
        {
            //Create server.properties
            string configPath = Functions.Path.GetServerFiles(_serverId, "server.properties");
            File.Create(configPath).Dispose();

            using (TextWriter textwriter = new StreamWriter(configPath))
            {
                textwriter.WriteLine("#Minecraft server properties");
                textwriter.WriteLine("#Generated by WindowsGSM.exe");
                textwriter.WriteLine("spawn-protection=16");
                textwriter.WriteLine($"query.port={serverPort}");
                textwriter.WriteLine("generator-settings=");
                textwriter.WriteLine("force-gamemode=false");
                textwriter.WriteLine("allow-nether=true");
                textwriter.WriteLine("gamemode=survival");
                textwriter.WriteLine("broadcast-console-to-ops=true");
                textwriter.WriteLine("enable-query=false");
                textwriter.WriteLine("player-idle-timeout=0");
                textwriter.WriteLine("difficulty=easy");
                textwriter.WriteLine("broadcast-rcon-to-ops=true");
                textwriter.WriteLine("spawn-monsters=true");
                textwriter.WriteLine("op-permission-level=4");
                textwriter.WriteLine("pvp=true");
                textwriter.WriteLine("snooper-enabled=true");
                textwriter.WriteLine("level-type=default");
                textwriter.WriteLine("hardcore=false");
                textwriter.WriteLine("enable-command-block=false");
                textwriter.WriteLine("network-compression-threshold=256");
                textwriter.WriteLine($"max-players={maxplayers}");
                textwriter.WriteLine("max-world-size=29999984");
                textwriter.WriteLine("resource-pack-sha1=");
                textwriter.WriteLine("function-permission-level=2");
                textwriter.WriteLine($"rcon.port={(Int32.Parse(serverPort)+10).ToString()}");
                textwriter.WriteLine($"server-port={serverPort}");
                textwriter.WriteLine($"server-ip={serverIP}");
                textwriter.WriteLine("spawn-npcs=true");
                textwriter.WriteLine("allow-flight=false");
                textwriter.WriteLine($"level-name={defaultmap}");
                textwriter.WriteLine("view-distance=10");
                textwriter.WriteLine("resource-pack=");
                textwriter.WriteLine("spawn-animals=true");
                textwriter.WriteLine("white-list=false");
                textwriter.WriteLine($"rcon.password={rcon_password}");
                textwriter.WriteLine("generate-structures=true");
                textwriter.WriteLine("online-mode=true");
                textwriter.WriteLine("max-build-height=256");
                textwriter.WriteLine("level-seed=");
                textwriter.WriteLine("prevent-proxy-connections=false");
                textwriter.WriteLine("use-native-transport=true");
                textwriter.WriteLine($"motd={serverName}");
                textwriter.WriteLine("enable-rcon=false");
            }
        }

        public void SetParameter(string additional)
        {
            _param = additional;
        }

        public async Task<Process> Start()
        {
            int isJavaInstalled = IsJavaJREInstalled();
            if (isJavaInstalled == 0)
            {
                Error = "Java is not installed";
                return null;
            }

            string workingDir = Functions.Path.GetServerFiles(_serverId);

            string serverJarPath = Path.Combine(workingDir, "server.jar");
            if (!File.Exists(serverJarPath))
            {
                Error = $"server.jar not found ({serverJarPath})";
                return null;
            }

            string configPath = Path.Combine(workingDir, "server.properties");
            if (!File.Exists(configPath))
            {
                Notice = $"server.properties not found ({configPath}). Generated a new one.";
            }

            string javaPath = (isJavaInstalled == 1) ? "java" : "C:\\Program Files (x86)\\Java\\jre1.8.0_231\\bin\\java.exe";

            if (isJavaInstalled == 2)
            {
                WindowsFirewall firewall = new WindowsFirewall("java.exe", javaPath);
                if (!await firewall.IsRuleExist())
                {
                    firewall.AddRule();
                }
            }

            Process p = new Process();
            p.StartInfo.WorkingDirectory = workingDir;
            p.StartInfo.FileName = javaPath;
            p.StartInfo.Arguments = $"{_param} -jar server.jar nogui";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            p.Start();

            return p;
        }

        public static async Task<bool> Stop(Process p)
        {
            SetForegroundWindow(p.MainWindowHandle);
            SendKeys.SendWait("stop");
            SendKeys.SendWait("{ENTER}");
            SendKeys.SendWait("{ENTER}");
            SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);

            for (int i = 0; i < 10; i++)
            {
                if (p != null && p.HasExited)
                {
                    return true;
                }

                await Task.Delay(1000);
            }

            return false;
        }

        public async Task<bool> Install()
        {
            //EULA
            MessageBoxResult result = System.Windows.MessageBox.Show("By continuing you are indicating your agreement to the EULA.\n(https://account.mojang.com/documents/minecraft_eula)", "Agreement to the EULA", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                Error = "Disagree to the EULA";
                return false;
            }

            //Install JAVA if not installed
            if (IsJavaJREInstalled() == 0)
            {
                //Java
                result = System.Windows.MessageBox.Show("Java is not installed\n\nWould you like to install?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    Error = "Java is not installed";
                    return false;
                }

                if (!await DownloadJavaJRE())
                {
                    return false;
                }
            }

            try
            {
                WebClient webClient = new WebClient();
                const string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                string versionJson = webClient.DownloadString(manifestUrl);
                string latesetVersion = JObject.Parse(versionJson)["latest"]["release"].ToString();
                var versionObject = JObject.Parse(versionJson)["versions"];
                string packageUrl = null;

                foreach (var obj in versionObject)
                {
                    if (obj["id"].ToString() == latesetVersion)
                    {
                        packageUrl = obj["url"].ToString();
                        break;
                    }
                };

                if (packageUrl == null)
                {
                    Error = $"Fail to fetch packageUrl from {manifestUrl}";
                    return false;
                }

                //packageUrl example: https://launchermeta.mojang.com/v1/packages/6876d19c096de56d1aa2cf434ec6b0e66e0aba00/1.15.json
                var packageJson = webClient.DownloadString(packageUrl);

                //serverJarUrl example: https://launcher.mojang.com/v1/objects/e9f105b3c5c7e85c7b445249a93362a22f62442d/server.jar
                string serverJarUrl = JObject.Parse(packageJson)["downloads"]["server"]["url"].ToString();

                webClient.DownloadFileCompleted += InitiateServerJar;
                webClient.DownloadFileAsync(new Uri(serverJarUrl), Functions.Path.GetServerFiles(_serverId, "server.jar"));
            }
            catch
            {
                Error = $"Fail to install {FullName}";
                return false;
            }

            return true;
        }

        public async Task<bool> Update()
        {
            //Install JAVA if not installed
            if (IsJavaJREInstalled() == 0)
            {
                if (!await DownloadJavaJRE())
                {
                    return false;
                }
            }

            string serverJarPath = Functions.Path.GetServerFiles(_serverId, "server.jar");
            if (File.Exists(serverJarPath))
            {
                await Task.Run(() =>
                {
                    try
                    {
                        File.Delete(serverJarPath);
                    }
                    catch
                    {

                    }
                });
            }

            if (File.Exists(serverJarPath))
            {
                Error = "Fail to delete server.jar";
                return false;
            }

            try
            {
                WebClient webClient = new WebClient();
                const string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                string versionJson = webClient.DownloadString(manifestUrl);
                string latesetVersion = JObject.Parse(versionJson)["latest"]["release"].ToString();
                var versionObject = JObject.Parse(versionJson)["versions"];
                string packageUrl = null;

                foreach (var obj in versionObject)
                {
                    if (obj["id"].ToString() == latesetVersion)
                    {
                        packageUrl = obj["url"].ToString();
                        break;
                    }
                };

                if (packageUrl == null)
                {
                    Error = $"Fail to fetch packageUrl from {manifestUrl}";
                    return false;
                }

                //packageUrl example: https://launchermeta.mojang.com/v1/packages/6876d19c096de56d1aa2cf434ec6b0e66e0aba00/1.15.json
                var packageJson = webClient.DownloadString(packageUrl);

                //serverJarUrl example: https://launcher.mojang.com/v1/objects/e9f105b3c5c7e85c7b445249a93362a22f62442d/server.jar
                string serverJarUrl = JObject.Parse(packageJson)["downloads"]["server"]["url"].ToString();

                webClient.DownloadFileCompleted += InitiateServerJar;
                webClient.DownloadFileAsync(new Uri(serverJarUrl), Functions.Path.GetServerFiles(_serverId, "server.jar"));
            }
            catch
            {
                Error = $"Fail to install {FullName}";
                return false;
            }

            return true;
        }

        private int IsJavaJREInstalled()
        {
            /*
                Return

                0 - Not installed
                1 - Installed as global (java)
                2 - Installed with absouble path (C:\Program Files (x86)\Java\jre1.8.0_231\bin\java.exe)
            */

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.Arguments = "/c java -version";
                Process p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                if (output.Contains("java version") || error.Contains("java version"))
                {
                    return 1;
                }
            }
            catch
            {

            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.Arguments = "/c \"C:\\Program Files (x86)\\Java\\jre1.8.0_231\\bin\\java.exe\" -version";
                Process p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                if (output.Contains("java version") || error.Contains("java version"))
                {
                    return 2;
                }
            }
            catch
            {

            }

            return 0;
        }

        private async Task<bool> DownloadJavaJRE()
        {
            string serverFilesPath = Functions.Path.GetServerFiles(_serverId);
            string filename = "jre-8u231-windows-i586-iftw.exe";
            string installer = "https://javadl.oracle.com/webapps/download/AutoDL?BundleId=240725_5b13a193868b4bf28bcb45c792fce896";

            //Download jre-8u231-windows-i586-iftw.exe from https://www.java.com/en/download/manual.jsp
            string jrePath = Path.Combine(serverFilesPath, filename);
            try
            {
                WebClient webClient = new WebClient();
                //Run jre-8u231-windows-i586-iftw.exe to install Java
                webClient.DownloadFileCompleted += InstallJavaJRE;
                webClient.DownloadFileAsync(new Uri(installer), jrePath);
            }
            catch
            {
                Error = "Fail to download " + filename;
                return false;
            }

            //Wait until Java is installed
            while (true)
            {
                //Check jre-8u231-windows-i586-iftw.exe is deleted
                if (!File.Exists(jrePath))
                {
                    break;
                }

                await Task.Delay(1000);
            }

            return true;
        }

        private async void InstallJavaJRE(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            string installPath = Functions.Path.GetServerFiles(_serverId);
            string filename = "jre-8u231-windows-i586-iftw.exe";
            string jrePath = Path.Combine(installPath, filename);
            string javaPath = @"C:\Program Files (x86)\Java\jre1.8.0_231";

            if (File.Exists(jrePath))
            {
                ProcessStartInfo psi = new ProcessStartInfo(jrePath);
                psi.WorkingDirectory = installPath;
                psi.Arguments = $"INSTALL_SILENT=Enable INSTALLDIR=\"{javaPath}\"";
                Process p = Process.Start(psi);

                //Delete the jre-8u231-windows-i586-iftw.exe after installation
                await Task.Run(() =>
                {
                    while (File.Exists(jrePath)) 
                    {
                        if (p.HasExited)
                        {
                            try
                            {
                                File.Delete(jrePath);
                            }
                            catch
                            {

                            }
                        }

                        Task.Delay(1000);
                    }
                });
            }
        }

        private void InitiateServerJar(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            //Create eula.txt
            string eulaPath = Functions.Path.GetServerFiles(_serverId, "eula.txt");
            File.Create(eulaPath).Dispose();

            using (TextWriter textwriter = new StreamWriter(eulaPath))
            {
                textwriter.WriteLine("#By changing the setting below to TRUE you are indicating your agreement to our EULA (https://account.mojang.com/documents/minecraft_eula).");
                textwriter.WriteLine("#Generated by WindowsGSM.exe");
                textwriter.WriteLine("eula=true");
            }
        }
    }
}
