using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSKubernetes
{
    class Kubernetes
    {
        public static void DeployMinikube(DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var baseDir = Utils.GetBinariesDir();
            var ps1Path = System.IO.Path.Combine(baseDir, "DeployMinikube.ps1");
            Utils.RunProcess("powershell.exe", string.Format("-NonInteractive -NoLogo -ExecutionPolicy RemoteSigned -File \"{0}\"", ps1Path), baseDir, false, onOutput, onError, onExit);

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

        public static void DraftUp(string projectDir, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
            Utils.RunProcess(draftPath, "up .", projectDir, false, onOutput, onError, onExit);
        }

        public static void DraftCreate(string projectDir, string packName, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null, EventHandler onExit = null)
        {
            var draftPath = System.IO.Path.Combine(Utils.GetBinariesDir(), "draft.exe");
            Utils.RunProcess(draftPath, String.Format("create . --pack {0}", packName), projectDir, false, onOutput, onError, onExit);
        }

        public static void DisableDraftWatch(string projectDir)
        {
            var draftTomlFileName = "draft.toml";
            var path = System.IO.Path.Combine(projectDir, draftTomlFileName);
            string text = System.IO.File.ReadAllText(path, System.Text.Encoding.ASCII);
            text = text.Replace("watch = true", "watch = false");
            System.IO.File.WriteAllText(path, text, System.Text.Encoding.ASCII);
        }

    }
}
