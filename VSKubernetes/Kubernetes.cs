using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace VSKubernetes
{
    class Kubernetes
    {
        public static Process RunPowerShellProcess(string path, string workingDirectory, bool wait = false, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            return Utils.RunProcess("powershell.exe", string.Format("-NonInteractive -NoLogo -ExecutionPolicy RemoteSigned -File \"{0}\"", path),
                                    workingDirectory, wait, onOutput, onError, onExit);
        }

        public static void DeployMinikube(DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var baseDir = Utils.GetBinariesDir();
            var ps1Path = System.IO.Path.Combine(baseDir, "DeployMinikube.ps1");
            RunPowerShellProcess(ps1Path, baseDir, false, onOutput, onError, onExit);
        }

        public static Process DraftConnect(string projectDir, IList<KeyValuePair<int, int>> portMappings = null, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var overridePort = "";
            if (portMappings != null && portMappings.Count > 0)
            {
                foreach (var portMapping in portMappings)
                {
                    if (overridePort.Length > 0)
                        overridePort += ",";
                    overridePort += portMapping.Key + ":" + portMapping.Value;
                }
                overridePort = " --override-port " + overridePort;
            }

            var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
            return Utils.RunProcess(draftPath, "connect" + overridePort, projectDir, false, onOutput, onError, onExit);
        }

        public static void DraftUp(string projectDir, bool minikubeDockerEnv=false, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            if (minikubeDockerEnv)
            {
                var draftPs1Path = System.IO.Path.Combine(Utils.GetBinariesDir(), "draftUp.ps1");
                RunPowerShellProcess(draftPs1Path, projectDir, false, onOutput, onError, onExit);
            }
            else
            {
                var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
                Utils.RunProcess(draftPath, "up .", projectDir, false, onOutput, onError, onExit);
            }
        }

        public static void DraftCreate(string projectDir, string packName, string appName, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
            Utils.RunProcess(draftPath, String.Format("create . --pack {0} --app {1}", packName, appName), projectDir, false, onOutput, onError, onExit);
        }

        public static void DisableDraftWatch(string projectDir)
        {
            var draftTomlFileName = "draft.toml";
            var path = System.IO.Path.Combine(projectDir, draftTomlFileName);
            string text = System.IO.File.ReadAllText(path, System.Text.Encoding.ASCII);
            text = text.Replace("watch = true", "watch = false");
            System.IO.File.WriteAllText(path, text, System.Text.Encoding.ASCII);
        }


        static string GetK8sConfigPath()
        {
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return System.IO.Path.Combine(home, @".kube\config");
        }

        static YamlStream LoadKubeConfig()
        {
            var k8sConfigPath = GetK8sConfigPath();
            if (!System.IO.File.Exists(k8sConfigPath))
                return null;

            using (var r = System.IO.File.OpenText(k8sConfigPath))
            {
                var yaml = new YamlStream();
                yaml.Load(r);
                return yaml;
            }
        }

        public static string GetCurrentContext()
        {
            var yaml = LoadKubeConfig();
            if (yaml == null)
                return null;

            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            return ((YamlScalarNode)mapping["current-context"]).Value;
        }

        public static void SetCurrentContext(string context)
        {
            var kubectlPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "kubectl.exe");
            var p = Utils.RunProcess(kubectlPath, "config use-context \""  + context + "\"", "", true);
            if (p.ExitCode != 0)
            {
                throw new Exception("kubectl set-context failed");
            }
        }

        public static string[] GetContextNames()
        {
            IList<string> l = new List<string>();

            var yaml = LoadKubeConfig();
            if (yaml != null)
            {
                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
                var contexts = (YamlSequenceNode)mapping.Children[new YamlScalarNode("contexts")];
                foreach (YamlMappingNode context in contexts)
                {
                    l.Add(context["name"].ToString());
                }
            }

            return l.ToArray();
        }
    }
}
