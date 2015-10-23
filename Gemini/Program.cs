#region using

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Gemini.Properties;

#endregion

namespace Gemini
{
	static class Program
	{
		#region Definitions

		private static bool IsNowRsync = false;
		private static TimeSpan WaitTimeSpan = TimeSpan.FromSeconds(1);
		private static ProcessStartInfo rsyncProcessStartInfo = new ProcessStartInfo();
		private static TimeSpan IntervalTimeSpan = TimeSpan.FromSeconds(Settings.Default.IntervalSeconds);

		#endregion

		#region Main

		/// <summary>
		/// Main EntryPoint
		/// </summary>
		static void Main()
		{
			#region CreateRsyncProcessStartInfo

			rsyncProcessStartInfo.FileName = Settings.Default.RsyncPath;
			rsyncProcessStartInfo.Arguments = CreateRsyncArgs();
			rsyncProcessStartInfo.CreateNoWindow = true;
			rsyncProcessStartInfo.UseShellExecute = false;
			rsyncProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

			#endregion

			#region Console

			Console.WriteLine($"watching:{Settings.Default.LocalPath}");

			#endregion

			#region IntervalMode

			if (Settings.Default.IntervalSeconds > 0)
			{
				new Thread(new ThreadStart(() =>
				{
					while (true)
					{
						Thread.Sleep(IntervalTimeSpan);

						if (Settings.Default.NotifyMode)
						{
							Console.WriteLine("rsync by interval");
						}

						Rsync(false);
					}
				})).Start();
			}

			#endregion

			#region Watch

			using (var fileSystemWatcher = new FileSystemWatcher(Settings.Default.LocalPath))
			{
				fileSystemWatcher.EnableRaisingEvents = true;
				fileSystemWatcher.Created += (o, e) =>
				{
					if (Settings.Default.NotifyMode)
					{
						Console.WriteLine($"rsync by created: {e.FullPath}");
					}

					Rsync();
				};
				fileSystemWatcher.Deleted += (o, e) =>
				{
					if (Settings.Default.NotifyMode)
					{
						Console.WriteLine($"rsync by deleted: {e.FullPath}");
					}

					Rsync();
				};
				fileSystemWatcher.Changed += (o, e) =>
				{
					if (Settings.Default.NotifyMode)
					{
						Console.WriteLine($"rsync by changed: {e.FullPath}");
					}

					Rsync();
				};
				fileSystemWatcher.Renamed += (o, e) =>
				{
					if (Settings.Default.NotifyMode)
					{
						Console.WriteLine($"rsync by renamed: {e.OldFullPath} -> {e.FullPath}");
					}

					Rsync();
				};

				while (true)
				{
					fileSystemWatcher.WaitForChanged(WatcherChangeTypes.All);
				}
			}

			#endregion
		}

		#endregion

		#region CreateRsyncArgs

		private static string CreateRsyncArgs()
		{
			return $"-e \"ssh -p {Settings.Default.SSHPort} -i {Settings.Default.KeyFilePath}\" --delete {string.Join(" ", Settings.Default.ExcludeInclude.Cast<string>())} {(Settings.Default.CompressMode ? "-az" : " - a")} {Settings.Default.LocalPath.ConvertToLinuxPath()} {Settings.Default.RemoteUser}@{Settings.Default.RemoteHost}:{Settings.Default.RemotePath}";
		}

		#endregion

		#region ConvertToLinuxPath

		private static string ConvertToLinuxPath(this string path)
		{
			return Regex.Replace(path, @"^(?<drive>[A-Za-z]):\\", "/${drive}/").Replace(@"\", "/");
		}

		#endregion

		#region RSync

		private static void Rsync(bool wait = true)
		{
			#region Check

			if (IsNowRsync)
			{
				if (!wait)
				{
					return;
				}

				#region Wait

				while (true)
				{
					Thread.Sleep(WaitTimeSpan);

					if (!IsNowRsync)
					{
						break;
					}
				}

				#endregion
			}

			#endregion

			#region Rsync

			IsNowRsync = true;

			try
			{
				var rsyncProcess = Process.Start(rsyncProcessStartInfo);
				rsyncProcess.WaitForExit();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			finally
			{
				IsNowRsync = false;
			}

			#endregion
		}

		#endregion
	}
}
