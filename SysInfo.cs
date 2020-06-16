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