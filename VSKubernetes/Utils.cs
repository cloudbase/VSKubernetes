using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VSKubernetes
{
    internal class Utils
    {
        private static readonly Guid kubernetesPaneGuid = new Guid("2BE8BB60-E918-4C59-8717-B078A6927D34");

        public static string GetVSExtensionDir()
        {
            var extensionPath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return System.IO.Path.GetDirectoryName(extensionPath);
        }

        public static string GetBinariesDir()
        {
            return System.IO.Path.Combine(GetVSExtensionDir(), "Binaries");
        }

        public static string GetVSExtensionFilePath(string relPath)
        {
            return System.IO.Path.Combine(GetVSExtensionDir(), relPath);
        }

        public static Project GetCurrentProject(IServiceProvider serviceProvider)
        {
            DTE service = (DTE)serviceProvider.GetService(typeof(DTE));
            var activeSolutionProjects = (Array)service.ActiveSolutionProjects;
            return (Project)activeSolutionProjects.GetValue(0);
        }

        public static ProjectItem GetProjectItem(Project project, string name)
        {
            return GetProjectItem(project.ProjectItems, name);
        }

        public static ProjectItem GetProjectItem(ProjectItems items, string name)
        {
            foreach (ProjectItem p in items)
                if (p.Name == name)
                    return p;
            return null;
        }

        public static bool ProjectExists(Solution solution, string projectName)
        {
            foreach (Project p in solution.Projects)
                if (p.Name == projectName)
                    return true;
            return false;
        }

        public static string GetSSHBinariesDir()
        {
            // Using the SSH client available at https://github.com/PowerShell/Win32-OpenSSH causes an E_ABORT error
            // Temporarily using the one that comes with Git until the issue is solved
            return Environment.ExpandEnvironmentVariables("%SystemDrive%\\Program Files\\Git\\usr\\bin\\");
        }

        public static void GenerateSSHKeypair(string keyPath, DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null)
        {
            // ssh-keygen has no way to disable the overwrite prompt
            if (System.IO.File.Exists(keyPath))
                System.IO.File.Delete(keyPath);
            var pubKeyPath = keyPath + ".pub";
            if (System.IO.File.Exists(pubKeyPath))
                System.IO.File.Delete(pubKeyPath);

            var sshKeygenPath = Path.Combine(GetSSHBinariesDir(), "ssh-keygen.exe");
            var p = RunProcess(sshKeygenPath, string.Format("-t rsa -b 2048 -q -N \"\" -f \"{0}\"", keyPath), wait: true,
                               onOutput: onOutput, onError: onError);
            if (p.ExitCode != 0)
                throw new Exception("ssh-keygen failed");
        }

        public static string RunSSHCommand(string host, string username, string keyPath, string cmd, int port = 22)
        {
            // Workaround for a connection refused error when using localhost
            if (host == "localhost")
                host = "127.0.0.1";

            PrivateKeyFile[] keyFiles = { new PrivateKeyFile(keyPath) };
            using (var client = new SshClient(host, port, username, keyFiles))
            {
                // Ignore host key
                client.HostKeyReceived += (sender, e) => { e.CanTrust = true; };
                client.Connect();
                using (var sshCmd = client.CreateCommand(cmd))
                {
                    return sshCmd.Execute();
                }
            }
        }

        public static System.Diagnostics.Process RunProcess(string path, string arguments = "", string workingDirectory = "", bool wait = false,
                                                            DataReceivedEventHandler onOutput = null, DataReceivedEventHandler onError = null,
                                                            EventHandler onExit = null)
        {
            var p = new System.Diagnostics.Process();
            p.StartInfo.FileName = path;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            if (onOutput != null)
            {
                p.OutputDataReceived += onOutput;
            }
            if (onError != null)
            {
                p.ErrorDataReceived += onError;
            }
            if (onExit != null)
            {
                p.EnableRaisingEvents = true;
                p.Exited += onExit;
            }

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (wait)
            {
                p.WaitForExit();
            }
            return p;
        }

        public static void ShowWarningMessageBox(IServiceProvider serviceProvider, string message, string title = "Kubernetes for Visual Studio")
        {
            VsShellUtilities.ShowMessageBox(
                serviceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static IVsOutputWindowPane GetOutputPane(IServiceProvider serviceProvider, Guid paneGuid, string title, bool visible, bool clearWithSolution)
        {
            IVsOutputWindow output = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow)); IVsOutputWindowPane pane;
            output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));
            var hr = output.GetPane(ref paneGuid, out pane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return pane;
        }

        public static void WriteToOutputWindow(IServiceProvider serviceProvider, string message)
        {
            //var paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.DebugPane_guid;
            var paneGuid = kubernetesPaneGuid;
            var pane = GetOutputPane(serviceProvider, paneGuid, "Kubernetes", true, false);
            pane.Activate();
            pane.OutputString(message + "\n");
        }

        public static void AddItemsToProject(ProjectItems projectItems, IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {

                    var itemName = System.IO.Path.GetFileName(path);
                    if (System.IO.File.Exists(path))
                    {
                        if (Utils.GetProjectItem(projectItems, itemName) == null)
                            projectItems.AddFromFile(path);
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                        var childProjectItem = Utils.GetProjectItem(projectItems, itemName);
                        if (childProjectItem == null)
                            childProjectItem = projectItems.AddFromDirectory(path);
                        var childPaths = System.IO.Directory.GetFileSystemEntries(path);
                        AddItemsToProject(childProjectItem.ProjectItems, childPaths);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Item exists, ignore exception
                }
            }
        }
    }
}
