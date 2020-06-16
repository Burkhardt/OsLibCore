﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RunProcessAsTask; // https://github.com/jamesmanning/RunProcessAsTask

//using Hangfire; // https://www.hangfire.io/overview.html
/*
*	based on RsbSystem (C++ version from 1991, C# version 2005, dotnet core 2019)
*/

namespace OsLib		// aka OsLibCore
{
	public static class ShellHelper
	{
		public static string Bash(this string cmd)
		{
			var escapedArgs = cmd.Replace("\"", "\\\"");

			var process = new Process()
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "/bin/bash",
					Arguments = $"-c \"{escapedArgs}\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return result;
		}
	}
    public class RaiSystem
	{
		string command = null;
		string param = null;
		string commandLine = null;
		public static string IndirectShellExecFile = new RaiFile("~/bin/start").FullName;
		public int ExitCode = 0;
		public string this[string environmentVariable]
		{
			get {
				var p = new Process();
				return p.StartInfo.EnvironmentVariables[environmentVariable];
			}
		}
		/// <summary>Exec for apps that don't want console output
		/// </summary>
		/// <param name="msg">returns output of called program</param>
		/// <returns>0 if ok</returns>
		/// <remarks>RsbSystem instance keeps the result in member ExitCode</remarks>
		public int Exec(out string msg)
		{
			msg = "";
			var p = new Process();
			p.StartInfo.FileName = command; // d:\\program files\\imagemagick-6.3.3-q16\\
			p.StartInfo.Arguments = param;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;
			p.EnableRaisingEvents = true;
			p.Start();
			if (!p.StandardOutput.EndOfStream)
				msg = p.StandardOutput.ReadToEnd();
			if (!p.StandardError.EndOfStream)
				msg += p.StandardError.ReadToEnd();
			p.WaitForExit(120000);  // this needs to come after readToEnd() RSB: https://msdn.microsoft.com/en-us/library/system.diagnostics.processstartinfo.redirectstandardoutput(v=vs.110).aspx
			ExitCode = p.ExitCode;
			p.Dispose();
			msg.TrimEnd();
			return ExitCode;
		}
		/// <summary>Exec for console apps</summary>
		/// <param name="wait">waits for the process to exit</param>
		/// <returns>null or process</returns>
		/// <remarks>RsbSystem instance keeps the result in member ExitCode if wait==true</remarks>
		public Process Exec(bool wait = true)
		{
			using (var p = new Process())
			{
				p.StartInfo.FileName = command; // d:\\program files\\imagemagick-6.3.3-q16\\
				p.StartInfo.Arguments = param;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				var result = p.Start();
				if (wait)
				{
					if (!p.StandardOutput.EndOfStream)
					{
						Console.WriteLine(p.StandardOutput.ReadToEnd());
					}
					p.WaitForExit(120000);  // don't wait more than 2 min
					ExitCode = p.ExitCode;
				}
				return p;
			}
			//return null;
		}
		public async Task<ProcessResults> Start()
		{
			var results = await ProcessEx.RunAsync(command, param);
			return results;
		}
		public string FireAndForget()
		{
			// var jobId = BackgroundJob.Enqueue(
			// 	() => Console.WriteLine("Fire-and-forget!"));
			// return jobId;
			return "not implemented";
		}
		public RaiSystem(string cmdLine)
		{
			commandLine = cmdLine;
			var pos = 0;
			if (cmdLine[0] == '"')
				pos = cmdLine.IndexOf("\" ") + 1;
			else pos = cmdLine.IndexOf(" ");
			command = pos > -1 ? cmdLine.Substring(0, pos).Trim() : cmdLine;
			param = pos > -1 ? cmdLine.Substring(pos + 1).TrimStart() : "";
		}
		public RaiSystem(string cmd, string p)
		{
			commandLine = cmd + " " + p;
			command = cmd;
			param = p;
		}
	}
	public class RaiNetDrive : RaiSystem
	{
		/// <summary></summary>
		/// <param name="drive">todo: describe drive parameter on Mount</param>
		/// <param name="path">todo: describe path parameter on Mount</param>
		/// <param name="user">todo: describe user parameter on Mount</param>
		/// <param name="pwd">todo: describe pwd parameter on Mount</param>
		/// <param name="msg">todo: describe msg parameter on Mount</param>
		public int Mount(string drive, string path, string user, string pwd, ref string msg)
		{
			if (System.IO.Directory.Exists(drive + ":\\"))
			{
				var devnul = new string(' ', 80);
				Unmount(drive, ref devnul);
			}
			using (var p = new Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = "net.exe";
				p.StartInfo.Arguments = " use " + drive + ": " + path + " /user:" + user + " " + pwd;
				p.Start();
				p.WaitForExit();
				ExitCode = p.ExitCode;
				msg = p.StandardOutput.ReadToEnd();
				msg += p.StandardError.ReadToEnd();
			}
			return ExitCode;
		}
		/// <summary>
		/// Unmount a network drive
		/// </summary>
		/// <param name="drive"></param>
		/// <param name="msg">todo: describe msg parameter on Unmount</param>
		/// <returns>0 if successful</returns>
		/// <remarks>replaces addDrive</remarks>
		public int Unmount(string drive, ref string msg)
		{
			using (var p = new System.Diagnostics.Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = "net.exe";
				p.StartInfo.Arguments = " use " + drive + ": /DELETE";
				p.Start();
				p.WaitForExit();
				ExitCode = p.ExitCode;
				msg = p.StandardOutput.ReadToEnd();
				msg += p.StandardError.ReadToEnd();
			}
			return ExitCode;
		}
		public RaiNetDrive()
			 : base("")
		{
		}
	}
}
