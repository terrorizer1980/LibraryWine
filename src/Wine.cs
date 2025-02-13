﻿/* 
 * Copyright (c) 2021 Mirko Brombin <send@mirko.pm>.
 * Copyright (c) 2021 Bottles Developers <https://github.com/bottlesdevs>.
 * 
 * This program is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU General Public License as published by  
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License 
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace Bottles.LibraryWine
{
    public class Wine
    {
        public string WinePath { get; }
        public string WinePrefixPath { get; }
        public VerboseLevels VerboseLevel { get; }
        public SupportedTerminals Terminal { get; set; }
        private List<string> VerboseLevelsString = new List<string>()
        {
            "-all", "-warn+all", "fixme-all", "+all"
        };
        public enum VerboseLevels
        {
            N_ALL = 0,
            N_WARN_Y_ALL = 1,
            FIXME_N_ALL = 2,
            Y_ALL = 3
        }

        public List<string> BootStatesStrings = new List<string>()
        {
            "--end-session", "--force", "--init", "--kill", "--restart", "--shutdown", "--update"
        };
        public enum BootStates
        {
            END_SESSION = 0,
            FORCE = 1,
            INIT = 2,
            KILL = 3,
            RESTART = 4,
            SHUTDOWN = 5,
            UPDATE = 6
        }
        private Dictionary<string, string[]> SupportedTerminalsStrings = new Dictionary<string, string[]>()
        {
            {"NONE", new string[] {}},
            {"XTERM", new string[] {"xterm", "-e"}},
            {"KONSOLE", new string[] {"konsole", "-e"}},
            {"GNOME_TERMINAL", new string[] {"gnome-terminal", "--"}},
            {"XFCE4_TERMINAL", new string[] {"xfce4-terminal", "--command"}},
            {"MATE_TERMINAL", new string[] {"mate-terminal", "--command"}}
        };

        public enum SupportedTerminals
        {
            NONE = 0,
            XTERM = 1,
            KONSOLE = 2,
            GNOME_TERMINAL = 3,
            XFCE4_TERMINAL = 4,
            MATE_TERMINAL = 5
        }

        public Wine(
            string winePath,
            string winePrefixPath,
            VerboseLevels verboseLevel = VerboseLevels.N_ALL,
            bool isProton = false)
        {
            if (isProton)
            {
                if (Directory.Exists(Path.Combine(winePath, "dist")))
                    winePath = Path.Combine(winePath, "dist");
                else if (Directory.Exists(Path.Combine(winePath, "files")))
                    winePath = Path.Combine(winePath, "files");
                else
                    throw new Exception("Proton Path is not valid");
            }

            if (!WineHelpers.ValidateWinePath(winePath))
                if (isProton)
                    throw new Exception("Proton Path is not valid");
                else
                    throw new Exception("Wine Path is not valid");
            
            if (!WineHelpers.ValidateWinePrefixPath(winePrefixPath))
                throw new Exception("Wine Prefix Path is invalid and creation failed");

            this.WinePath = winePath;
            this.WinePrefixPath = winePrefixPath;
            this.VerboseLevel = verboseLevel;
        }

        public object ExecCommand(
            string command,
            string arguments = "",
            Dictionary<string, string> envVars = null,
            string[] sequence = null,
            bool getOutput = false,
            bool useTerminal = false,
            string workingDirectory = "")
        {
            if (workingDirectory == "")
                workingDirectory = this.WinePrefixPath;

            string fileName = $"{this.WinePath}/bin/wine64";
            string fileArguments = $"{command} {arguments}";
            bool useShellExecute = false;
            bool redirectStandardOutput = true;
            bool redirectStandardInput = false;

            if (useTerminal && Terminal != SupportedTerminals.NONE && !getOutput)
            {
                fileArguments = $"{SupportedTerminalsStrings[Terminal.ToString()][1]} {fileName} {fileArguments}";
                fileName = SupportedTerminalsStrings[Terminal.ToString()][0];
            }
            else {
                useShellExecute = false;
                redirectStandardOutput = true;
            }

            if (sequence != null)
                redirectStandardInput = true;

            Console.WriteLine($"Executing: {fileName} {fileArguments}");
            var startInfo = new ProcessStartInfo() 
            { 
                FileName = fileName, 
                Arguments = fileArguments,
                UseShellExecute = useShellExecute,
                RedirectStandardOutput = redirectStandardOutput,
                RedirectStandardInput = redirectStandardInput,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            startInfo.EnvironmentVariables["WINEPREFIX"] = WinePrefixPath;
            startInfo.EnvironmentVariables["WINEDEBUG"] = VerboseLevelsString[(int)VerboseLevel];

            if(envVars != null)
                foreach (var envVar in envVars
                    .Where(envVar => !(envVar.Key.ToLower() == "wineprefix" || 
                                       envVar.Key.ToLower() == "winedebug")))
                    {
                        startInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                    }
            
            var proc = new Process() { StartInfo = startInfo };
            
            try
            {
                proc.Start();

                if (sequence != null)
                {
                    foreach (var sequenceItem in sequence)
                    {
                        proc.StandardInput.WriteLine(sequenceItem);
                    }
                }

                proc.WaitForExit();

                if (redirectStandardOutput)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    if(getOutput)
                        return output;
                }

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERR: " + ex.Message);
                return false;
            }
        }
    }
}