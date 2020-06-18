using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace OsLib
{
	public class VersionInfo
	{
		public string Info = null;
		public int Major = 0;
		public int Minor = 0;
		public int Revision = 0;
		public override string ToString()
		{
			return Info;    // $"{Major}.{Minor}.{Revision}";
		}
		public VersionInfo(string version)
		{
			Info = version.TrimEnd();
			var vs = version.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string s in vs)
			{
				if (s.Contains('.'))
				{
					var va = s.Split(new char[] { '.' });
					try
					{
						if (va.Length > 0)
							Major = Int16.Parse(va[0]);
						if (va.Length > 1)
							Minor = Int16.Parse(va[1]);
						if (va.Length > 2)
							Revision = Int16.Parse(va[2]);
					}
					catch (FormatException ex)
					{
						// do nothing
						var m = ex.Message;
					}
				}
			}
		}
	}
	public class CmdInfo
	{
		// TODO add ~/.bash_profile ~/.profile /etc/bashrc
		// TODO install
		private string versionCommand;
		public VersionInfo Version
		{
			get
			{
				string info;
				if (versionInfo == null)
				{
					new RaiSystem(versionCommand).Exec(out info);
					versionInfo = new VersionInfo(info);
				}
				return versionInfo;
			}
		}
		private VersionInfo versionInfo = null;
		private string execCommand = null;
		public string Exec(string parameters)
		{
			string result;
			new RaiSystem($"{execCommand} {parameters}").Exec(out result);
			return result;
		}
		/// <summary>
		/// echos something - replaces all environment variables
		/// </summary>
		/// <value></value>
		public static string Echo(string s)
		{
			var dict = EnvironmentVariables;
			foreach (var item in dict)
			{
				s = s.Replace($"${item.Key}", item.Value);
			}
			return s;
		}
		private static Dictionary<string, string> envDict = null;
		public static Dictionary<string, string> EnvironmentVariables
		{
			get
			{
				if (envDict == null)
				{
					var p = new Process();
					var startInfo = new ProcessStartInfo("zsh");
					envDict = new Dictionary<string, string>();
					// foreach (DictionaryEntry item in p.StartInfo.EnvironmentVariables)
					// 	envDict.Add((string)item.Key, (string)item.Value);
					foreach (DictionaryEntry item in startInfo.EnvironmentVariables)
						envDict.Add((string)item.Key, (string)item.Value);
					p.Dispose();
				}
				return envDict;
			}
		}
		/// <summary>
		/// Used by property Path
		/// </summary>
		/// <param name="rcFile">setup file as used in a source statement for the shell, i.e. "~/.zshrc </param>
		/// <param name="newPath">the directory path to add, i.e. ~/.mlw</param>
		/// <returns>true if Path was added or was there already</returns>
		public static string PATH(string newPath = null, string rcFile = "~/.zshrc")
		{
			var shrc = new TextFile(rcFile);
			if (!shrc.Exists())
				return null;
			if (!string.IsNullOrEmpty(newPath))
			{
				int pathInserted = -1, exportExists = -1;
				for (int i = 0; i < shrc.Lines.Count; i++)
				{
					if (shrc.Lines[i].StartsWith("path+=")) // TODO bashrc syntax
					{
						if (shrc.Lines[i] != $"path+={newPath}")
						{
							shrc.Insert(i, $"path+={newPath}");
							// TODO bashrc syntax
							pathInserted = i++;
						}
					}
					if (shrc.Lines[i].Contains("export PATH"))
						exportExists = i;
				}
				if (pathInserted < 0)
				{
					if (exportExists < 0)
					{
						shrc.Append($"path+={newPath}");
						shrc.Append("export PATH");
					}
				}
				else if (exportExists < pathInserted)
					shrc.Append("export PATH");
				shrc.Save();
			}
			return EnvironmentVariables["PATH"];
		}
		/// <summary>
		/// set an alias in a shell source file
		/// </summary>
		/// <param name="alias">the alias name to find in the shell source script (~/.zshrc or ~/.bashrc), i.e. mlw</param>
		/// <param name="resolvesTo">new value i.e. '~/.mlw/mlw'; null for just checking if alias exists</param>
		/// <param name="rcFile">shell source file to inspect for aliases</param>
		/// <returns>true, if the alias is there now (or was there already)</returns>
		public static bool Alias(string alias, string resolvesTo, string rcFile = "~/.zshrc")
		{
			var dict = Aliases(rcFile);
			return dict.ContainsKey(alias) && dict[alias] == resolvesTo;
		}
		/// <summary>
		/// get the aliases from a particular shell source file
		/// </summary>
		/// <param name="rcFile">i.e. ~/.zshrc</param>
		/// <returns>list of all defined aliases</returns>
		public static Dictionary<string, string> Aliases(string rcFile = "~/.zshrc")
		{
			var dict = new Dictionary<string, string>();
			var shrc = new TextFile(rcFile);
			if (shrc.Exists())
			{
				for (int i = 0; i < shrc.Lines.Count; i++)
				{
					if (shrc.Lines[i].StartsWith("alias")) // TODO bashrc syntax
					{
						var kvp = shrc.Lines[i].Substring(6).Split(new char[] { '=' });
						dict.Add(kvp[0], kvp[1]);
					}
				}
			}
			return dict;
		}
		public string Which
		{
			get
			{
				RaiFile f;
				var paths = EnvironmentVariables["PATH"];
				var pathArray = paths.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var path in pathArray)
				{
					f = new RaiFile($"{path}/{execCommand}");
					if (f.Exists())
						return f.FullName;
				}
				return "";
			}
		}
		public bool Installed
		{
			get
			{
				return Which.Length > 0;
			}
		}
		public CmdInfo(string cmd)
		{
			versionCommand = $"{cmd} --version";
			//whichCommand = $"which {cmd}";
			execCommand = cmd;
		}
	}
}