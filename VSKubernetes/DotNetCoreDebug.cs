using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace VSKubernetes
{
    class DotNetCoreDebug: IVsDebuggerEvents
    {
        IServiceProvider serviceProvider;
        string sshUsername = "root";
        string sshHost;
        int sshPort;
        uint debuggerEventsCookie;
        Action debuggingEnded;

        public DotNetCoreDebug(IServiceProvider serviceProvider, string host, int port, Action debuggingEnded)
        {
            this.serviceProvider = serviceProvider;
            this.sshHost = host;
            this.sshPort = port;
            this.debuggingEnded = debuggingEnded;
        }

        string GetSSHKeyPath()
        {
            var projectDir = Path.GetDirectoryName(Utils.GetCurrentProject(serviceProvider).FileName);
            return Path.Combine(projectDir, "id_rsa_vscode");
        }

        uint GetProcessId()
        {
            var sshKeyPath = GetSSHKeyPath();

            int i = 0;
            do
            {
                try
                {
                    var cmd = "pgrep dotnet";
                    var output = Utils.RunSSHCommand(sshHost, sshUsername, sshKeyPath, cmd, sshPort);
                    return uint.Parse(output);
                }
                catch (Exception ex)
                {
                    // Wait for the connection to be ready
                    if (i < 20 && (
                            ex is SocketException && ((SocketException)ex).ErrorCode == 10061 ||
                            ex is SshConnectionException))
                    {
                        System.Threading.Thread.Sleep(1000);
                        i++;
                    }
                    else
                        throw;
                }
            }
            while (true);

        }

        public void StartDebugging()
        {
            var processId = GetProcessId();
            var sshPath = Path.Combine(Utils.GetSSHBinariesDir(), "ssh.exe");
            var sshKeyPath = GetSSHKeyPath();
            var remoteAppPath = "/app";

            dynamic options = new ExpandoObject();
            (options as IDictionary<string, Object>)["$adapter"] = sshPath;
            (options as IDictionary<string, Object>)["$adapterArgs"] = string.Format(
                "{0}@{1} -o PasswordAuthentication=no -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -p {2} -i \"{3}\" " +
                "/root/vsdbg/vsdbg --interpreter=vscode",
                sshUsername, sshHost, sshPort, sshKeyPath);
            var languageMappings = new ExpandoObject();
            (options as IDictionary<string, Object>)["$languageMappings"] = languageMappings;
            dynamic csharpLanguageMapping = new ExpandoObject();
            (languageMappings as IDictionary<string, Object>)["C#"] = new ExpandoObject();
            csharpLanguageMapping.languageId = "3F5162F8-07C6-11D3-9053-00C04FA302A1";
            csharpLanguageMapping.extensions = new string[] { "*" };
            dynamic exceptionCategoryMappings = new ExpandoObject();
            (options as IDictionary<string, Object>)["$exceptionCategoryMappings"] = exceptionCategoryMappings;
            exceptionCategoryMappings.CLR = "449EC4CC-30D2-4032-9256-EE18EB41B62B";
            exceptionCategoryMappings.MDA = "6ECE07A9-0EDE-45C4-8296-818D8FC401D4";
            options.name = ".NET Core Remote Attach";
            options.type = "coreclr";
            options.request = "attach";
            options.processId = processId;

            options.pipeTransport = new ExpandoObject();
            options.pipeTransport.pipeProgram = "zx";
            options.pipeTransport.pipeArgs = new object[] { sshUsername + "@" + sshHost,
                                              "-o", "PasswordAuthentication=no",
                                              "-o", "StrictHostKeyChecking=no",
                                              "-o", "UserKnownHostsFile=/dev/null",
                                              "-p", sshPort, "-i", sshKeyPath };
            options.pipeTransport.debuggerPath = "/root/vsdbg/vsdbg";
            options.pipeTransport.pipeCwd = remoteAppPath;
            options.pipeTransport.quoteArgs = true;

            string optionsStr = JsonConvert.SerializeObject(options);

            VsDebugTargetInfo4[] debugTargets = new VsDebugTargetInfo4[1];
            // __VSDBGLAUNCHFLAGS155.DBGLAUNCH_ParallelLaunch
            debugTargets[0].LaunchFlags = 33554432U;

            debugTargets[0].dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            debugTargets[0].bstrExe = "dotnet";
            debugTargets[0].bstrOptions = optionsStr;

            debugTargets[0].fSendToOutputWindow = 1;
            debugTargets[0].project = Utils.GetCurrentProject(this.serviceProvider) as IVsHierarchy;
            debugTargets[0].bstrCurDir = "/app";
            debugTargets[0].guidLaunchDebugEngine = new Guid("{2833D225-C477-4388-9353-544D168F6030}");

            VsDebugTargetProcessInfo[] processInfo = new VsDebugTargetProcessInfo[debugTargets.Length];

            IVsDebugger4 debugger = this.serviceProvider.GetService(typeof(IVsDebugger)) as IVsDebugger4;

            var hr = ((IVsDebugger)debugger).AdviseDebuggerEvents(this, out debuggerEventsCookie);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);

            debugger.LaunchDebugTargets4(1, debugTargets, processInfo);
        }

        public int OnModeChange(DBGMODE mode)
        {
            // Remove the DBGMODE.DBGMODE_Enc flag if present
            mode = mode & ~DBGMODE.DBGMODE_EncMask;

            switch (mode)
            {
                case DBGMODE.DBGMODE_Design:
                    debuggingEnded();
                    // No need for further events
                    var debugger = this.serviceProvider.GetService(typeof(IVsDebugger)) as IVsDebugger;
                    var hr = debugger.UnadviseDebuggerEvents(debuggerEventsCookie);
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
                    break;
                case DBGMODE.DBGMODE_Break:
                    break;
                case DBGMODE.DBGMODE_Run:
                    break;
            }
            return VSConstants.S_OK;
        }
    }
}
