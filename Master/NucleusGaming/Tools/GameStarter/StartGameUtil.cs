﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using EasyHook;

namespace Nucleus.Gaming.Tools.GameStarter
{
    /// <summary>
    /// Util class for executing and reading output from the Nucleus.Coop.StartGame application
    /// </summary>
    public static class StartGameUtil
    {
        private static string lastLine;
        private static object locker = new object();

        public static string GetStartGamePath()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "StartGame.exe");
        }

        public static string GetArguments(string pathToGame, string args, int waitTime, string mutexType, params string[] mutex)
        {
            string mu = "";
            for (int i = 0; i < mutex.Length; i++)
            {
                mu += mutex[i];

                if (i != mutex.Length - 1)
                {
                    mu += ";";
                }
            }

            return "\"" + pathToGame + "\" \"" + args + "\" \"" + waitTime + "\" \"" + mutexType + "\" \"" + mu + "\"";
        }

        public static void KillMutex(Process p, string mutexType, bool partial, params string[] mutex)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = startGamePath;

                string mu = "";
                for (int i = 0; i < mutex.Length; i++)
                {
                    mu += mutex[i];

                    if (i != mutex.Length - 1)
                    {
                        mu += ";";
                    }
                }

                startInfo.Arguments = "\"proc:" + p.Id.ToString() + "\" \"partialmutex:" + partial + "\" \"mutextype:" + mutexType + "\" \"mutex:" + mu + "\"";
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                Process proc = Process.Start(startInfo);
                proc.OutputDataReceived += proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();
            }
        }

        public static bool MutexExists(Process p, string mutexType, bool partial, params string[] mutex)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = startGamePath;

                string mu = "";
                for (int i = 0; i < mutex.Length; i++)
                {
                    mu += mutex[i];

                    if (i != mutex.Length - 1)
                    {
                        mu += ";";
                    }
                }

                startInfo.Arguments = $"\"proc:{p.Id}\" \"partialmutex:{partial}\" \"mutextype:{mutexType}\" \"output:{mu}\"";
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process proc = Process.Start(startInfo);
                proc.OutputDataReceived += proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();

                bool result;
                bool.TryParse(lastLine, out result);

                return result;
            }
        }

        /// <summary>
        /// NOT THREAD SAFE
        /// </summary>
        /// <param name="pathToGame"></param>
        /// <param name="args"></param>
        /// <param name="waitTime"></param>
        /// <param name="mutex"></param>
        /// <returns></returns>
        public static int StartGame(string pathToGame, string args, bool hook, bool delay, bool renameMutex, string mutexNames, bool setWindow, string workingDir = null)
        {
            lock (locker)
            {
                string startGamePath = GetStartGamePath();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = startGamePath;

                if (!string.IsNullOrWhiteSpace(workingDir))
                {
                    workingDir = "|" + workingDir;
                }

                startInfo.Arguments = "\"" + "hook:" + hook + "\" \"delay:" + delay + "\" \"renamemutex:" + renameMutex + "\" \"mutextorename:" + mutexNames + "\" \"setwindow:" + setWindow + "\" \"game:" + pathToGame + workingDir + ";" + args + "\"";

                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                Process proc = Process.Start(startInfo);
                proc.OutputDataReceived += proc_OutputDataReceived;
                proc.BeginOutputReadLine();

                proc.WaitForExit();

                //parse the last line for the process ID
                return int.Parse(lastLine.Split(':')[1]);
            }
        }
        public static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }
            Console.WriteLine($"Redirected output: {e.Data}");
            lastLine = e.Data;
        }
    }
}
