﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;
using Newtonsoft.Json.Linq;
//using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// FileInfo FileVol = new FileInfo(DownloadPath);
// int SizeinKB = (int)(FileVol).Length / 1024 ;

// static readonly string[] SizeSuffixes =
//                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

//     static string SizeSuffix(Int64 value)
//     {
//         if (value < 0) { return "-" + SizeSuffix(-value); }
//         int i = 0;
//         decimal dValue = (decimal)value;
//         while (Math.Round(dValue / 1024) >= 1)
//         {
//             dValue /= 1024;
//             i++;
//         }
//         return string.Format("{0:n1} {1}", dValue, SizeSuffixes[i]);
//     }
/*
 *	based on RsbFile (C++ version from 1991, C# version 2005)
 */
namespace OsLib     // aka OsLibCore
{
	public enum EscapeMode { noEsc, blankEsc, paramEsc, backslashed };
	public enum OsType { UNIX, Windows };
	/// <remarks> 
	/// 2021-07-05: OsLib now works with different CloudStorage providers, not only Dropbox
	/// Dropbox still works (personal and business accounts)
	/// OneDrive is also supported now
	/// look for the following variables to set your prefenrences
	/// - dropboxType
	/// - cloudStorageType
	/// </remarks>
	public class Os
	{
		public static string HomeDir
		{
			get
			{
				if (homeDir == null)
					homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
				return homeDir;
			}
		}
		private static string homeDir = null;
		public static OsType Type
		{
			get
			{
				if (type == null)
					type = Os.DIRSEPERATOR == "/" ? OsType.UNIX : OsType.Windows;
				return (OsType)type;
			}
		}
		private static OsType? type = null;
		public static string TempDir
		{
			get
			{
				if (tempDir == null)
					tempDir = System.IO.Path.GetTempPath();
				return tempDir;
			}
		}
		private static string tempDir = null;
		public static string LocalBackupDir = "~/Backup/";   // needs to be a directory that is not on any kind of remote or synchronized dir
		public static string FindDropboxCommand = @"c:\bin\FindDropbox.exe";
		public static string NewSubscriberCommand = @"c:\bin\newsub.exe";
		private static string cloudStorageRootDir = null;
		private static string localDropboxFile = @"c:\bin\localDropbox.txt";
		private static string localDropboxFileUnix = "~/.dropbox/info.json";	// 2024-07-05: still ok for Dropbox v202.4.5551 which is beta for macOS Sonoma 14.5
		enum CloudStorageType { dropbox, onedrive };
		enum DropboxType { personal, business };
		private static DropboxType dropboxType = DropboxType.personal;
		private static CloudStorageType cloudStorageType = CloudStorageType.dropbox; // loudStorageType.onedrive; => not that easy with OneDrive; use Ms Graph API
		/// <summary>
		/// On a macOS, the dropboxPath is read from ~/.dropbox/info.json, which contains root_path for business subscriptions and path for personal subscriptions
		/// set dropboxType to personal or business to set the subscription you want to use for CloudStorage 
		/// </summary>
		#pragma warning disable
		private static string linkedCloudStorageDir = "/Users/Shared/Dropbox";
		#pragma warning restore
		//private static string linkedDroboxDir = null;
		/// <summary>
		/// Get the current server's Dropbox root or use SyncRootDir if not possible
		/// </summary>
		/// <value>returns "" or the path with Os.DIRSEPERATOR at the end</value>
		public static string CloudStorageRoot
		{
			get
			{
				if (cloudStorageRootDir == null)
				{
					#region MacOs
					if (Os.Type == OsType.UNIX)
					{
						if (cloudStorageType == CloudStorageType.onedrive)
							return new RaiFile("~/Library/CloudStorage/OneDrive/").Path;	// TODO: check if this is correct for various macOS systems/machines
						else
						{
							var tf = new TextFile(localDropboxFileUnix);
							if (tf != null && tf.Lines.Count > 0)
							{
								var jo = JObject.Parse(string.Join(" ", tf.Lines));
								var business = (JObject)jo["business"];
								var personal = (JObject)jo["personal"];
								cloudStorageRootDir =  (dropboxType == DropboxType.business ? (string)business["root_path"] : (string)personal["path"]) + Os.DIRSEPERATOR;
								return cloudStorageRootDir;
							}
						}
					}
					#endregion
					#region Windows
					else
					{
						if (!File.Exists(FindDropboxCommand) && !File.Exists(localDropboxFile))
							throw new FileNotFoundException("Missing external command to locate dropbox using " + FindDropboxCommand, new FileNotFoundException());
						try
						{
							string result;
							var call = new RaiSystem(Os.FindDropboxCommand);
							if (call.Exec(out result) == 0)
							{
								if (result.Contains("Exception") && result.Contains("Access") && result.Contains("denied"))
									throw new IOException("the running process is not allowed to access " + Os.FindDropboxCommand, new Exception(result));
								result = result.Trim();
								if (string.IsNullOrEmpty(result))
								{
									var dropboxLocationFile = new TextFile(localDropboxFile);
									if (!string.IsNullOrEmpty(dropboxLocationFile[0]) && dropboxLocationFile[0].Contains("ropbox"))
										result = dropboxLocationFile[0];
								}
								cloudStorageRootDir = result.Trim() + DIRSEPERATOR;  // removes \n\r
							}
						}
						catch (Exception)
						{
							if (File.Exists(localDropboxFile))
							{
								cloudStorageRootDir = new TextFile(localDropboxFile)[0];
								if (!cloudStorageRootDir.EndsWith(Os.DIRSEPERATOR))
									cloudStorageRootDir = Os.NormSeperator(cloudStorageRootDir + Os.DIRSEPERATOR);
							}
						}
					}
					#endregion
				}
				return cloudStorageRootDir;
			}
		}
		public static string DIRSEPERATOR
		{
			get
			{
				if (dIRSEPERATOR == null)
					dIRSEPERATOR = System.IO.Path.Combine("1", "2").Substring(1, 1);
				return dIRSEPERATOR;
			}
		}
		private static string dIRSEPERATOR = null;       // changed internal representation; use EscapeMode.backslashed to convert to "\\"
		public const string ESCAPECHAR = "\\";
		public const string DATEFORMAT = "yyyy-MM-dd HH.mm.ss"; // missing in older versions
		public static DateTimeOffset ParseDateTime(string datetimeInDATEFORMAT)
		{
			var a = datetimeInDATEFORMAT.Split(new char[] { '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			return new DateTimeOffset(new DateTime(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]), int.Parse(a[5])));
		}
		public static string escapeParam(string param)
		{        // "..."
			if (param[0] == '\"')
				return param;
			return '\"' + param + '\"';
		}
		public static string escapeBlank(string name)
		{     // every whitespace char will be escaped by insertion of ESCAPECHAR
			var s = name;
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] == ' ')
				{
					s = s.Insert(i, Os.ESCAPECHAR);
					i++;
				}
			}
			return s;
		}
		public static string winInternal(string fullname)
		{  // every / is replaced by a \ in the copy
			#region UNIX version
			char dirChar = Os.DIRSEPERATOR[0];
			#endregion
			if (fullname != null)
				fullname = fullname.Replace('/', dirChar); //fullname = fullname.Replace('/', '\\');
			return fullname;
		}
		public static string Escape(string s, EscapeMode mode)
		{
			if (mode == EscapeMode.noEsc)
				return s;
			if (mode == EscapeMode.blankEsc)
				return Os.escapeBlank(s);
			if (mode == EscapeMode.paramEsc)
				return Os.escapeParam(s);
			if (mode == EscapeMode.backslashed)
				return Os.winInternal(s);
			return s;
		}
		public static string NormSeperator(string s)
		{
			return s.Replace(@"\", DIRSEPERATOR);
		}
	}

	public static class RaiFileExtensions
	{
		/// <summary>
		/// MakePolicyCompliant
		/// \n will be removed – subsequent string will be put in a new line
		/// \t is allowed and will not be removed, even if multiple (beginning of line and end of line)
		/// 'multiple whitespaces will be reduced to 1, no matter where they occur (use \t for indention of value seperation)
		/// empty lines will be removed
		/// </summary>
		/// <returns>a new List of string</returns>
		public static List<string> MakePolicyCompliant(this List<string> lines, bool tabbed = true)
		{
			var buffer = new List<string>();
			List<string> multi;
			foreach (var line in lines)
			{
				multi = line.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				foreach (var one in multi)
				{
					if (tabbed)
						buffer.Add(one.Replace("    ", "\t").Replace("  ", "\t").Singularize(' ').Singularize('\t').Replace("\t ", "\t"));
					else buffer.Add(one.Singularize(' '));
				}
			}
			var compliant = new List<string>();
			foreach (var line in buffer)
				if (line.Length > 0)
					compliant.Add(line);
			return compliant;
		}
		public static string Singularize(this string line, char c, bool trim = true)
		{
			if (line.Length == 0)
				return line;
			var result = new List<char>();
			result.Add(line[0]);
			int j = 1;
			for (int i = 1; i < line.Length; i++)
			{
				if (result[j - 1] != c || line[i] != c)
				{
					result.Add(line[i]);
					j++;
				}
			}
			return trim ? new string(result.ToArray()).Trim(c) : new string(result.ToArray());
		}
		public static List<Dictionary<string, string>> CreateDictionariesFromCsvLines(this string lines, bool tabbed = true)
		{
			var tab = (new List<string> { lines }).MakePolicyCompliant(tabbed: true);
			var keys = tab[0].Split(new char[] { '\t' });
			var list = new List<Dictionary<string, string>>();
			for (int i = 1; tab.Count > i; i++)
			{
				var v = tab[i].Split(new char[] { '\t' });
				var dict = new Dictionary<string, string>();
				for (int j = 0; j < keys.Length; j++)
				{
					if (v.Length < j)
						throw new FieldAccessException($"csv input - out of bounds for index {j}; field list: { string.Join(',', keys.AsEnumerable()) }");
					dict.Add(keys[j], v[j]);
				}
				list.Add(dict);
			}
			return list;
		}
	}
	/// <summary>
	/// RaiPath – uses operator/ to add a subdirectory to a path
	/// just the path, no filename, no extension
	/// </summary>
	public class RaiPath
	{
		public string Path {
			get
			{
				return path.Path;
			}
			set
			{
				path = new RaiFile(value);
				path.Name = string.Empty;
				path.Ext = string.Empty;
			}
		}
		private RaiFile path;
		/// <summary>
		/// Using the / operator to add a subdirectory to a path
		/// </summary>
		/// <param name="self"></param>
		/// <param name="subDir">string</param>
		/// <returns>RaiPath object for daisy chaining reasons</returns>
		public static RaiPath operator /(RaiPath self, string subDir)
		{
			return new RaiPath(self.path.Path + subDir + Os.DIRSEPERATOR);
		}
		public RaiPath(string s)
		{
			Path = s;
		}
		public RaiPath(RaiFile f)
		{
			path = f;
			path.Name = string.Empty;
		}
		public override string ToString() => Path;
	}
	public class RaiFile
	{
		const int maxWaitCount = 60;  // raised from 15 as a result of a failed test case: TestComparePic9AndPic7ZoomTrees, 2012-12-23, RSB.
									  // raised from 20 as a result of a failed test case: TestUserRoleSubscriberAccess, Pic8, 2014-03-03, RSB.
									  // raised from 25 as a result of a failed test runs on Pic8 (which probably has a slow disk compared to other servers), 2014-03-16, RSB.
		private string name;
		public bool Cloud;
		/// <summary>
		/// // without dir structure and without extension
		/// </summary>				
		public virtual string Name
		{
			get { return string.IsNullOrEmpty(name) ? string.Empty : name; }
			set
			{   // sets name and ext; override to set more name components
				name = Os.NormSeperator(value);
				var pos = name.LastIndexOf("/");
				if (pos >= 0 && name.Length > pos)
					name = name.Remove(0, pos + 1);
				pos = name.LastIndexOf(".");
				if (pos >= 0)
				{
					if (name.Length > pos + 1)
					{
						ext = name.Substring(pos + 1);
						name = name.Remove(pos);
					}
					else ext = string.Empty;
				}
				else if (ext == null)
					ext = string.Empty;
			}
		}
		/// <summary>
		/// without dir structure but with "." and with extension, ie 123456.png
		/// </summary>				
		public virtual string NameWithExtension
		{
			get { return string.IsNullOrEmpty(name) && string.IsNullOrEmpty(Ext) ? string.Empty : Name + (string.IsNullOrEmpty(ext) ? string.Empty : "." + Ext); }
		}
		private string ext;
		/// <summary>
		/// extension of the picture without '.', ie "png"
		/// </summary>
		public string Ext
		{
			get { return ext; }
			set { ext = value; }
		}
		private string path;                // the source directory of the picture, ends with a dirSeperator
		/// <summary>
		/// the source directory of the file, ends with a dirSeperator; Ensure will be set to memorize if the file is in the cloud
		/// </summary>
		public virtual string Path
		{
			get { return path; }
			set
			{
				if (string.IsNullOrEmpty(value))
					path = string.Empty;
				else
				{
					path = Os.NormSeperator(value);
					if (path[path.Length - 1] != Os.DIRSEPERATOR[0])
						path = path + Os.DIRSEPERATOR;
					UpdateCloudFlag();
				}
			}
		}

		private void UpdateCloudFlag()
		{
			var lowercase = path.ToLower();
			Cloud = lowercase.Contains(".dropbox") ? false : lowercase.Contains("dropbox") || lowercase.Contains("onedrive") || lowercase.Contains("cloudstorage");    // sets Ensure also for files inside local .dropbox folder 
		}

		public virtual string FullName
		{
			get { return Path + NameWithExtension; }
		}
		/// <summary>
		/// Check if the file currently exists in the file system
		/// </summary>
		/// <returns></returns>
		public bool Exists()
		{
			return File.Exists(Os.winInternal(FullName));
		}
		public int rm()	// removes file from the file system 
		{
			var name = Os.winInternal(FullName);
			if (File.Exists(name))
			{
				File.Delete(name);
				#region double check if file is gone
				if (Cloud)
					return awaitFileVanishing(name);
				#endregion
			}
			return 0;
		}
		/// <summary>
		/// move file in the file system
		/// </summary>
		/// <param name="from"></param>
		/// <returns></returns>
		public int mv(RaiFile from)         // relocates file in the file system
		{
			// make sure from and this do not point to the same file
			if (from.FullName == FullName)
				return 0;
			mkdir(); // create destdir if necessary; applies ensure
			var newname = Os.winInternal(FullName);
			var oldname = Os.winInternal(from.FullName);
			rm(); // make sure it's really gone before we go ahead; applies ensure
			File.Move(oldname, newname);  // make sure the user that w3wp runs under has write/delete access to oldname, i.e. c:\dropbox\config\3.3.3\Users.xml
			#region double check if file has moved
			if (Cloud)
				return awaitFileVanishing(oldname) + awaitFileMaterializing(newname);
			#endregion
			return 0;
		}
		/// <summary>
		/// Copy file in the file system
		/// </summary>
		/// <param name="from">will be checked; exception will be thrown if file name does not match RsbFile form requirements</param>
		/// <returns>0 if everything went well</returns>
		public int cp(RaiFile from)
		{
		  // make sure from and this do not point to the same file
			if (from.FullName == FullName)
				return 0;
			var oldname = Os.winInternal(from.FullName);
			var newname = Os.winInternal(FullName);
			rm(); // make sure it's really gone before we go ahead; applies ensure
			File.Copy(oldname, newname, true);  // overwrite if exists (which should never happen since we just removed it)
			#region double check if file has moved
			if (Cloud)
				return awaitFileMaterializing(newname);
			#endregion
			return 0;
		}
		/// <summary>
		/// Copy file
		/// </summary>
		/// <param name="from">can be any valid file name - no form requirements</param>
		/// <returns></returns>
		public int cp(string from)
		{
			return cp(new RaiFile(from));
		}
		public bool IsDirectory() => FullName.EndsWith(Os.DIRSEPERATOR);
		/// <summary>
		/// Change current working directory to the path in the RaiFile or the FullName if it is a directory
		/// </summary>
		public void cd()
		{
			Directory.SetCurrentDirectory(IsDirectory() ? FullName : Path);
		}
		public bool HasAbsolutePath()
		{
			if (string.IsNullOrEmpty(Path))
				return false;
			if (Path.Length > 0 && (Path[0] == '/' || Path[0] == '\\'))
				return true;
			if (Path.Length > 1 && Path[1] == ':')
				return true;
			return false;
		}
		/// <summary>
		/// Cloud Space files sometimes take their time to vanish
		/// </summary>
		/// <returns></returns>
		public int AwaitVanishing()
		{
			return awaitFileVanishing(FullName);
		}
		/// <summary>
		/// Cloud Space files sometimes take their time to materialize
		/// </summary>
		/// <param name="newFileOldName"></param>
		/// <returns></returns>
		public int AwaitMaterializing(bool newFileOldName = false)
		{
			return awaitFileMaterializing(FullName, newFileOldName);
		}
		/// <summary>
		/// Cloud Space directories sometimes take their time to materialize
		/// </summary>
		/// <param name="dirName"></param>
		/// <returns></returns>
		private static int awaitDirMaterializing(string dirName)
		{
			var count = 0;
			var exists = false;
			while (count < maxWaitCount)
			{
				try
				{
					exists = Directory.Exists(dirName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirMaterializing of dir " + dirName + ".");
			return -count;
		}
		/// <summary>
		/// Cloud Space directories sometimes take their time to vanish
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		/// <exception cref="DirectoryNotFoundException"></exception>
		private static int awaitDirVanishing(string path)
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try
				{
					exists = Directory.Exists(path);
				}
				catch (Exception) { }
				if (!exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new DirectoryNotFoundException("ensure failed - timeout in awaitDirVanishing of dir " + path + ".");
			return -count;
		}
		private static int awaitFileMaterializing(string fileName, bool newFileOldName = false)
		{
			var count = 0;
			var done = false;
			var dt = new TimeSpan(0);
			while (count < maxWaitCount)
			{
				try
				{
					if (newFileOldName) {
						var info = new FileInfo(fileName);
						dt = DateTime.Now.Subtract(info.LastWriteTimeUtc);
						done = dt.TotalMilliseconds < 100;	// way too long - time the OS/FileSystem needs to update the FileInfo for a just rewritten file
					}
					else done = File.Exists(fileName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (done)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new FileNotFoundException("ensure failed - timeout.", fileName);
			return -count;
		}
		private static int awaitFileVanishing(string fileName)
		{
			var count = 0;
			var exists = true;
			while (count < maxWaitCount)
			{
				try
				{
					exists = File.Exists(fileName);
				}
				catch (Exception) { }   // device not ready exception if Win 2003
				if (!exists)
					break;
				Thread.Sleep(5);
				count++;
			}
			if (count >= maxWaitCount)
				throw new IOException("ensure failed - timeout in deleting " + fileName + ".");
			return -count;
		}
		/// <summary>
		/// does nothing if dir is not empty unless depth is set to > 0; assumes this RaiFile is a directory
		/// </summary>
		/// <param name="depth">deletes up to depth levels of subdirectories</param>
		public void rmdir(int depth = 0)
		{
			if (dirEmpty)
				rmdir(FullName);
			else if (depth > 0)
			{
				foreach (var subdir in Directory.EnumerateDirectories(FullName))
				{
					foreach (var file in Directory.EnumerateFiles(subdir))
						new RaiFile(file).rm();
					new RaiFile(subdir).rmdir(depth - 1);   // recursion
				}
				rmdir(FullName);
			}
		}
		/// <summary>
		/// throws exception if dir is not empty
		/// </summary>
		/// <param name="path">must point to a directory</param>
		private static void rmdir(string path)
		{
			path = Os.winInternal(path);
			var dir = new DirectoryInfo(path);
			if (dir.Exists)
			{
				Directory.Delete(path);
				if (path.ToLower().Contains("dropbox"))
					awaitDirVanishing(path);
			}
		}
		/// <summary>
		/// assumes that FullName points to a directory; check if it contains files
		/// </summary>
		/// <value>true if no files in this directory</value>
		public bool dirEmpty
		{
			get
			{
				return !Directory.EnumerateFileSystemEntries(FullName).Any();
			}
		}
		public DirectoryInfo mkdir()
		{
			return mkdir(Path);
		}
		/// <summary>Create a directory if it does not exist yet</summary>
		/// <param name="dirname"></param>
		/// <returns>DirectoryInfo structure; contains properties Exists and CreationDate</returns>
		public static DirectoryInfo mkdir(string dirname)
		{
			dirname = Os.winInternal(string.IsNullOrEmpty(dirname) ? Directory.GetCurrentDirectory() : dirname);
			var dir = new DirectoryInfo(dirname);
			if (!dir.Exists)  // TODO problems with network drives, i.e. IservSetting.RemoteRootDir
			{
				dir = Directory.CreateDirectory(dirname);
				if (dirname.ToLower().Contains("dropbox"))
					awaitDirMaterializing(dirname);
			}
			return dir;
		}
		/// <summary>
		/// zip this file into archive
		/// </summary>
		/// <returns>the archive name</returns>
		public RaiFile Zip()
		{
			var inFolder = new RaiFile(this.FullName);
			var file = new RaiFile(this.FullName);
			inFolder.Name = file.Name;
			inFolder.Path = inFolder.Path + inFolder.Name;
			inFolder.mv(file);
			file.Ext = file.Ext + ".zip";
			File.Delete(file.FullName);   // delete any pre-existing file
			try
			{
				ZipFile.CreateFromDirectory(inFolder.Path, file.FullName);
				//ZipFile.
			}
			catch (Exception)
			{
				return null;
			}
			return file;
		}
		/*
		/// <summary>
		/// usees 7zip
		/// </summary>
		/// <returns>zipArchive as RsbFile or null</returns>
		public RaiFile Zip7()
		{
			var zip7 = new RaiFile(FullName + ".7z");
			zip7.rm();  // remove it if an old version exists
						//var oldCurrentDir = Directory.GetCurrentDirectory();
						//Directory.SetCurrentDirectory("c:\\Program Files\\7-Zip\\");
			var call = new RaiSystem("7z", "a -mmt " + Os.escapeParam(zip7.FullName) + " " + Os.escapeParam(FullName));
			call.Exec();
			//Directory.SetCurrentDirectory(oldCurrentDir);
			if (call.ExitCode == 0 && zip7.Exists())
			{
				rm();
				return zip7;
			}
			return null;
		}
		*/
		/*
		/// <summary>
		/// usees 7zip with options -t7z -m0=lzma2 -mx=9 -aoa
		/// </summary>
		/// <returns>zipArchive as RsbFile or null</returns>
		public RaiFile ZipUltra()
		{
			var zip7 = new RaiFile(FullName + ".7z");
			zip7.rm();  // remove it if an old version exists
						//var oldCurrentDir = Directory.GetCurrentDirectory();
						//Directory.SetCurrentDirectory("c:\\Program Files\\7-Zip\\");
			var call = new RaiSystem("7z", "a -t7z -m0=lzma2 -mx=9 -aoa " + Os.escapeParam(zip7.FullName) + " " + Os.escapeParam(FullName));
			call.Exec();
			//Directory.SetCurrentDirectory(oldCurrentDir);
			if (call.ExitCode == 0 && zip7.Exists())
			{
				rm();
				return zip7;
			}
			return null;
		}
		*/
		/* 
		/// <summary>
		/// unzip an 7z archive using 7z commandline
		/// </summary>
		/// <returns>and RsbFile - use the Path to identify where the taget file(s) are located</returns>
		public RaiFile UnZip7()
		{
			var unzipped = new RaiFile(FullName.Substring(0, FullName.IndexOf(".7z")));
			var call = new RaiSystem("7z", "e " + Os.escapeParam(FullName) + " -o" + unzipped.Path);
			call.Exec();
			// do not remove 7z file
			return unzipped;
		}
		*/
		/// <summary>
		/// copies the file on disk identified by the current RaiFile object to multiple destinations
		/// </summary>
		/// <param name="destDirs"></param>
		/// <returns></returns>
		public bool CopyTo(string[] destDirs)
		{
			try
			{
				RaiFile dest;
				string destName;
				foreach (var destDir in destDirs)
				{
					dest = new RaiFile(FullName)
					{
						Path = destDir
					};
					destName = dest.FullName;
					dest.mkdir();
					if (File.Exists(destName))
						File.Delete(destName);
					File.Copy(FullName, destName);
				}
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}
		/// <summary>create a backup file</summary>
		/// <param name="copy">moves if false, copies otherwise</param>
		/// <returns>name of backupfile, if there was one created</returns>
		/// <remarks>the Os.LocalBackupDir will be used; make sure it's not in the Dropbox</remarks>
		public string backup(bool copy = false)
		{
			if (!File.Exists(FullName))
				return null;   // no file no backup
			var backupFile = new RaiFile(FullName);
			var idx = (backupFile.Path.Length > 2 && backupFile.Path[1] == ':') ? 3 : 0;     // works as expected for c:/123 or c:\123, but not for c:123
			var s = backupFile.Path.Substring(idx);
			backupFile.Path = (Os.LocalBackupDir + s).Replace("Dropbox/", "").Replace("dropbox/", "");   // eliminates Dropbox for LocalBackupDir to avoid ensure
			mkdir(backupFile.Path);
			backupFile.Name = backupFile.Name + " " + DateTimeOffset.UtcNow.ToString(Os.DATEFORMAT);
			backupFile.Ext = Ext;
			if (copy)
				backupFile.cp(this);
			else backupFile.mv(this);
			return backupFile.FullName;
		}

        public string[] DirList
        {
            get => Path.Split(Os.DIRSEPERATOR, StringSplitOptions.RemoveEmptyEntries); 
        }

        /// <summary>
        /// Constructor: auto-ensure mode for file systems that do not synchronously wait for the end of an IO operation i.e. Dropbox
        /// </summary>
        /// <remarks>only use the ensure mode if it has to be guaranteed that the IO operation was completely done
        /// when the method call returns; necessary e.g. for Dropbox directories since (currently) Dropbox first updates the
        /// file in the invisible . folder and then asynchronously updates the visible file and all the remote copies of it</remarks>
        /// <param name="filename"></param>
        public RaiFile(string filename)
		{
			// var lowercase = filename.ToLower();
			// Ensure = lowercase.Contains(".dropbox") ? false : lowercase.Contains("dropbox") || lowercase.Contains("onedrive") || lowercase.Contains("cloudstorage");	// sets Ensure also for files inside local .dropbox folder 
			path = string.Empty;
			name = string.Empty;
			ext = string.Empty;
			if (!string.IsNullOrEmpty(filename))
			{
				#region some unix conventions for convenience
				if (filename.StartsWith("~/"))
					filename = $"{Os.HomeDir}{filename.Substring(1)}";
				else if (filename.StartsWith("./"))
					filename = $"{Directory.GetCurrentDirectory()}{filename.Substring(1)}";
				else if (filename.StartsWith("../"))
				{
					var dir = Directory.GetCurrentDirectory();
					while (filename.StartsWith("../"))
					{
						dir = new RaiFile(dir.TrimEnd('/')).Path;   // one up
						filename = filename.Substring(3);           // remove first ../
					}
					filename = $"{dir}{filename}";
				}
				#endregion
				filename = Os.NormSeperator(filename);
				var k = filename.LastIndexOf(Os.DIRSEPERATOR);
				if (k >= 0)
				{
					path = filename.Substring(0, k + 1);
					Name = filename.Substring(k + 1);
				}
				else Name = filename;   // also takes care of ext
			}
			UpdateCloudFlag();
		}
	}
	public class TextFile : RaiFile
	{
		/// <summary>
		/// holds info if anything has changed in memory since last read
		/// </summary>
		public bool Changed { get; set; }
		private List<string> lines;
		public List<string> Lines
		{
			get
			{
				return lines == null ? Read() : lines;
			}
			set { lines = value; }
		}
		/// <summary>
		/// List automatically extends according to https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange?view=netcore-2.2
		/// if tests fail, AddRange could be employed
		/// </summary>
		/// <value></value>
		public string this[int i]
		{
			get
			{
				return Lines[i];
			}
			set
			{
				if (Lines.Capacity < i + 1)
					Lines.AddRange(Enumerable.Range(Lines.Count, i - Lines.Count + 1).Select(x => ""));
				Lines[i] = value;
				Changed = true; // even if Lines[i] had the same value before already
			}
		}
		public void Append(string line)
		{
			if (lines == null)
				Read();
			if (lines.Count == 1 && lines[0].Length == 0)   // make sure that we don't start in line two with an empty file
				lines[0] = line;
			else lines.Add(line);
			Changed = true;
		}
		public void Insert(int beforeLine, string line)
		{
			Lines.Insert(beforeLine, line);
			Changed = true;
		}
		public void Delete(int line)
		{
			Lines.RemoveAt(line);
			Changed = true;
		}
		public void DeleteAll()
		{
			lines = new List<string>();
			Append("");
			Changed = true;
		}
		public void Sort(bool reverse = false)
		{
			var lineArray = Lines.ToArray();
			Array.Sort(lineArray);
			if (reverse)
				Array.Reverse(lineArray);
			this.lines = new List<string>(lineArray);
			Changed = true;
		}
		public List<string> Read()
		{
			lines = File.Exists(FullName) ? new List<string>(File.ReadAllLines(FullName)) : new List<string>();
			Changed = false;
			return Lines;
		}
		/// <summary>
		/// Save the TextFile to disk, including dropbox locations
		/// </summary>
		/// <param name="backup">with backup == false the wait for materializing is not going to work; only use outside dropbox and alike</param>
		public void Save(bool backup = false)
		{
			if (Changed)
			{
				new RaiFile(FullName).mkdir();
				if (backup)
					this.backup(); // calls AwaitVanishing()
				else this.rm();   // calls AwaitVanishing()
				File.WriteAllLines(FullName, (lines == null ? new List<string>() : lines), Encoding.UTF8);
				AwaitMaterializing(true);
				Changed = false;
			}
		}
		public TextFile(string name)
			: base(name)
		{
		}
	}
	/// <summary>
	/// current settings: \t as field seperator, field values not quoted
	/// </summary>
	public class CsvFile : TextFile
	{  //UndefinedNumber and IsNumber now in DataImage, Extensions.cs
		private char[] fieldSplitter;
		private bool replaceBlanks = false;
		private Dictionary<string, int> Idx = new Dictionary<string, int>();
		public void AdjustColumnSelectors()
		{
			string line0 = replaceBlanks ?
				String.Join(fieldSplitter[0].ToString(), Lines[0].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
				: Lines[0];
			var q = from fieldName in line0.Split(fieldSplitter) select fieldName.Trim();
			string[] fields = q.ToArray();
			for (int i = 0; i < fields.Length; i++)
				Idx[fields[i]] = i;
		}
		public string[] FieldNames()
		{
			return (from _ in Idx orderby _.Value select _.Key).ToArray();
		}
		public List<JObject> Objects()
		{
			// TODO: combine several keys into one field Key
			// TODO: 
			var list = new List<JObject>();
			for (int i = 1; i < Lines.Count; i++)
				list.Add(Object(i));
			return list;
		}
		/// <summary>
		/// Get csv row as object
		/// </summary>
		/// <param name="idx">data starts at index 1</param>
		/// <returns></returns>
		public JObject Object(int idx)
		{
			var obj = new JObject();
			double d = 0.0;
			long l = 0;
			foreach (var elem in this[idx])
			{
				if (elem.Value.Contains("."))
				{
					if (double.TryParse(elem.Value, out d))
						obj[elem.Key] = d;
				}
				else if (long.TryParse(elem.Value, out l))
				{
					obj[elem.Key] = l;
				}
				else obj[elem.Key] = elem.Value;
			}
			return obj;
		}
		public new Dictionary<string, string> this[int i]
		{
			get
			{
				var result = new Dictionary<string, string>();
				if (i < 0 || i >= Lines.Count)
					return result;
				var line = replaceBlanks ?
						String.Join(fieldSplitter[0].ToString(), Lines[i].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
						: Lines[i];
				var fields = line.Split(fieldSplitter);
				foreach (var field in Idx)
					result.Add(field.Key, fields[field.Value]);
				return result;
			}
			set
			{
				Changed = true;
				throw new NotImplementedException();
			}
		}
		//public Dictionary<string, string> Item(int i)
		//{
		//	var result = new Dictionary<string, string>();
		//	if (i < 0 || i >= Lines.Count)
		//		return result;
		//	var fields = Lines[i].Split(fieldSplitter);
		//	foreach (var field in Idx)
		//		result.Add(field.Key, fields[field.Value]);
		//	return result;
		//}
		private void FixLineFeedsWithinFields()
		{
			string[] fields = null;
			for (int i = 1; i < Lines.Count; i++)
			{
				var line = replaceBlanks ?
						String.Join(fieldSplitter[0].ToString(), Lines[i].Split(fieldSplitter, StringSplitOptions.RemoveEmptyEntries))
						: Lines[i];
				fields = line.Split(fieldSplitter);
				if (fields.Length != Idx.Count)
				{
					while (line.Split(fieldSplitter).Length < Idx.Count && i < (Lines.Count - 1))
					{
						Lines[i] = line + Lines[i + 1];
						Delete(i + 1);
					}
					if (line.Split(fieldSplitter).Length != Idx.Count)
						Delete(i);
				}
			}
		}
		/// <summary>
		/// Read a csv file into memory
		/// </summary>
		/// <returns>number of rows; without the headline</returns>
		public int Read(string externalFieldNames = null, bool replaceBlanks = false)
		{
			this.replaceBlanks = replaceBlanks;
			base.Read();
			if (externalFieldNames != null)
				Insert(0, externalFieldNames);
			AdjustColumnSelectors();
			FixLineFeedsWithinFields();
			return Lines.Count - 1;
		}
		public void ToJsonFile(string destFileName = null)
		{
			var fName = string.IsNullOrEmpty(destFileName) ? new RaiFile(FullName) : new RaiFile(destFileName);
			fName.Ext = "json";
			var jsonFile = new TextFile(fName.FullName);
			jsonFile.rm();
			var fieldNames = FieldNames();
			Dictionary<string, string> item = null;
			string line;
			string value;
			long l;
			double d;
			jsonFile.Append("[");
			for (int i = 1; i < Lines.Count; i++)
			{
				line = "{";
				item = this[i];
				foreach (string name in fieldNames)
				{
					value = item[name];
					if (!(long.TryParse(value, out l) || double.TryParse(value, out d))) // problem? => will parse long until '.' and ignore rest
						value = "\"" + value + "\"";
					line += $"\"{name}\": {value},";
				}
				jsonFile.Append(line.Substring(0, line.Length - 1) + "},");
			}
			int llNr = jsonFile.Lines.Count - 1;
			var lastLine = jsonFile.Lines[llNr];
			jsonFile.Delete(llNr);
			jsonFile.Append(lastLine.Substring(0, lastLine.Length - 1) + "]");
			jsonFile.Save();
		}
		public CsvFile(string name, char seperator = '\t')
			: base(name)
		{
			fieldSplitter = new char[] { seperator };
		}
	}
	public class TmpFile : RaiFile
	{
		public void create()
		{
			var text = new TextFile(FullName);
			text.Append("");
			text.Save();
		}
		/// <summary>
		/// a file in the TempDir, located usually on the fastest drive of the system (SSD or RAM-Disk)
		/// </summary>
		/// <param name="fileName">no fileName given: the OS chooses a temp file name</param>
		/// <param name="ext">changes the system generated or given filename, if != null</param>
		public TmpFile(string fileName = null, string ext = null)
			: base(fileName ?? System.IO.Path.GetTempFileName())
		{
			this.Path = Os.TempDir; // ImageServer needs to have access if this library is used from within an IIS app
			if (ext != null)
				this.Ext = ext;
		}
	}
	/// <summary>
	/// Organizes the file into a directory based on its own name.
	/// For example, a file ./123456.json will be moved to ./123456/123456.json
	/// only checked in constructor, not in later operations like Path or Name changes
	/// </summary>
	public class CanonicalFile : RaiFile
	{
		/// <summary>
		/// A CononicalFile is a file that is inside a directory based on its own name.
		/// </summary>
		/// <param name="fullName">i.e. ~/StorageDir/AfricaStage/AfricaStage.pit or ~/StorageDir/AfricaStage.pit or ~/StorageDir/AfricaStage/</param>
		/// <param name="defaultExt">optional parameter if fullName doesn't come with an extension</param>
		/// <exception cref="Exception"></exception>
		public CanonicalFile(string fullName, string defaultExt = "pit") : base(fullName)
		{
			// Create a directory based on the file name
			var from = new RaiFile(fullName);
			Name = string.IsNullOrEmpty(from.Name) ? from.DirList[^1] : from.Name;
			Ext = string.IsNullOrEmpty(from.Ext) ? defaultExt : from.Ext;
			#region access file system and create directory if convention of canonicaFile is not met
			if (DirList[^1] != Name)
			{
				Path = (new RaiPath(Path) / Name).Path;
				//Path = Path + Name + Os.DIRSEPERATOR;
				if (!Directory.Exists(Path))
					Directory.CreateDirectory(Path);
				if (File.Exists(FullName))
					throw new Exception($"Tried to move {FullName} to {Path} but destination file already exists");
				else
				{
					File.Move(from.FullName, FullName);
				}
			}
			#endregion
		}
	}
} //namespace OsLib
