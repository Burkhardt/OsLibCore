using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

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
					if (va.Length > 0)
						Major = Int16.Parse(va[0]);
					if (va.Length > 1)
						Minor = Int16.Parse(va[1]);
					if (va.Length > 2)
						Revision = Int16.Parse(va[2]);
				}
			}
		}
	}
	public class CmdInfo
	{
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
		public string Echo(string s)
		{
			var dict = EnvironmentVariables;
			foreach (var item in dict)
			{
				s = s.Replace($"${item.Key}", item.Value);
			}
			return s;
		}
		private Dictionary<string, string> envDict = null;
		public Dictionary<string, string> EnvironmentVariables
		{
			get
			{
				if (envDict == null)
				{
					var p = new Process();
					envDict = new Dictionary<string, string>();
					foreach (DictionaryEntry item in p.StartInfo.EnvironmentVariables)
						envDict.Add((string)item.Key, (string)item.Value);
					p.Dispose();
				}
				return envDict;
			}
		}
		public string Path
		{
			get
			{
				return EnvironmentVariables["PATH"];
			}
			set
			{
				var list = new List<string>(EnvironmentVariables["PATH"].Split(new char[] { ':' }));
				if (!list.Contains(value))
				{
					#region try to make changes to PATH permanent => find the source
					var zshrc = new TextFile("~/.zshrc");
					#region add to .zshrc if this file exists
					if (zshrc.Exists())
					{
						int pathInserted = -1, exportExists = -1, mlwAliasExists = -1, lastAlias = -1;
						for (int i = 0; i < zshrc.Lines.Count; i++)
						{
							if (zshrc.Lines[i].Contains("path+="))
							{
								zshrc.Lines[i] = $"path+=~/.mlw:{zshrc.Lines[i].Substring(6)}";
								pathInserted = i;
							}
							if (zshrc.Lines[i].Contains("export PATH"))
								exportExists = i;
							if (zshrc.Lines[i].StartsWith("alias mlw='~/.mlw/mlw'"))
								mlwAliasExists = i;
							if (zshrc.Lines[i].StartsWith("alias"))
								lastAlias = i;
						}
						if (pathInserted < 0)
						{
							if (exportExists >= 0) 
							{
								zshrc.Lines.Insert(exportExists, "path+=~/.mlw");
							}
							else
							{
								zshrc.Lines.Add("path+=~/.mlw");
								zshrc.Lines.Add("export PATH");
							}
						}
						else
						{
							if (exportExists < pathInserted) 
							{
								zshrc.Lines.Add("export PATH");
							}
						}
						if (mlwAliasExists < 0 || lastAlias == zshrc.Lines.Count - 1) 
							zshrc.Lines.Add("alias mlw='~/.mlw/mlw'");
						else zshrc.Lines.Insert(lastAlias, "alias mlw='~/.mlw/mlw'");
					}
					#endregion
					zshrc.Save();
				}
			}
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