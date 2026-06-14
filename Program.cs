using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.Net.Http;

namespace AurLand;

//publish:
//dotnet publish -r linux-x64 -c Release -o "$HOME/.local/bin" -p:DebugType=None -p:DebugSymbols=false

public class Program
{
	private static readonly HttpClient _HttpClient = new();
	
	private static string _ALDataPath = string.Empty;
	private static string _ALDataGitPath = string.Empty;
	private static string _ALDataTxtPath = string.Empty;
	
	private static Dictionary<string, PackageInfo> _ALPackages = new();
	
	private static Task<int> Main(string[] args)
	{
		RootCommand rootCommand = new("AurLand is an aur helper.");
		
		Option<string> dataPrefixAurData = new("--data-prefix", ["-dp"]);
		
		dataPrefixAurData.Description = "Path for an AurLand data prefix. By default (empty string), AurLand uses XDG_DATA_HOME.";
		dataPrefixAurData.DefaultValueFactory = (r)=>{return string.Empty;};
		dataPrefixAurData.Recursive = true;
		
		rootCommand.Options.Add(dataPrefixAurData);
		
		Command syncCommand = new("sync", "Sync already installed aur packages with AurLand.");
		syncCommand.SetAction(async result =>
		{
			if(SetUpDirectories(result))
			{
				await SyncOption();
				SaveAurLandPackages();
			}
		});
		Command updateCommand = new("update", "Update aur packages.");
		updateCommand.SetAction(result =>
		{
			if(SetUpDirectories(result))
			{
				UpdateOption();
				SaveAurLandPackages();
			}
		});
		
		Argument<string> packageArg = new("package")
		{
			Description = "Name of a package to install or remove."
		};
		Command installCommand = new("install", "Install aur package.");
		installCommand.SetAction(result =>
		{
			if(SetUpDirectories(result))
			{
				InstallOption(result.GetValue<string>("package"));
				SaveAurLandPackages();
			}
		});
		installCommand.Arguments.Add(packageArg);
		
		Command removeCommand = new("remove", "Remove aur package. Needs to be run with sudo.");
		removeCommand.SetAction(result =>
		{
			if(SetUpDirectories(result))
			{
				RemoveOption(result.GetValue<string>("package"));
				SaveAurLandPackages();
			}
		});
		removeCommand.Arguments.Add(packageArg);
		
		Command clearCommand = new("clear", "Clear AurLand git folder.");
		clearCommand.SetAction(async result =>
		{
			if(SetUpDirectories(result))
			{
				Log.InfoLine("Running clearCommand:");
				
				bool all = result.GetValue<bool>("--all");
				if(all)
				{
					Directory.Delete(_ALDataPath, true);
					Log.WriteLine($"Cleared: {_ALDataPath}");
				}
				else
				{
					Directory.Delete(_ALDataGitPath, true);
					Log.WriteLine($"Cleared: {_ALDataGitPath}");
				}
			}
		});
		
		Option<bool> allOption = new("--all", ["-a"]);
		allOption.Description = @"Clear everything in the AurLand folder and the folder itself.";
		allOption.DefaultValueFactory = (r) => { return false; };
		clearCommand.Options.Add(allOption);
		
		Option<bool> syncOption = new("--sync", ["-s"]);
		syncOption.Description = @"Sync already installed aur packages with AurLand. 
		Same as the sync command. 
		Runs first if multiple options are specified.";
		syncOption.DefaultValueFactory = (r) => { return false; };
		
		rootCommand.Options.Add(syncOption);
		
		Option<bool> updateOption = new("--update", ["-u"]);
		updateOption.Description = @"Update already installed aur packages with AurLand. 
		Same as the update command.
		Runs second if multiple options are specified.";
		updateOption.DefaultValueFactory = (r) => { return false; };
		
		rootCommand.Options.Add(updateOption);
		
		Option<string> installOption = new("--install", ["-i"]);
		installOption.Description = @"Install aur package with AurLand. 
		Same as the install command.
		Runs third if multiple options are specified.";
		installOption.DefaultValueFactory = (r) => { return string.Empty; };
		installOption.Required = true;
		installOption.HelpName = "package";
		
		rootCommand.Options.Add(installOption);		
		
		Option<string> removeOption = new("--remove", ["-r"]);
		removeOption.Description = @"Remove aur package with AurLand.
		Same as the remove command.
		Runs fourth if multiple options are specified.
		Needs to be run with sudo.";
		removeOption.DefaultValueFactory = (r) => { return string.Empty; };
		removeOption.Required = true;
		removeOption.HelpName = "package";
		
		rootCommand.Options.Add(removeOption);
		
		rootCommand.Subcommands.Add(syncCommand);
		rootCommand.Subcommands.Add(updateCommand);
		rootCommand.Subcommands.Add(installCommand);
		rootCommand.Subcommands.Add(removeCommand);
		rootCommand.Subcommands.Add(clearCommand);
		
		rootCommand.SetAction(async result =>
		{
			if(SetUpDirectories(result))
			{
				await ProccessOptions(result);
			}
		});
		
		ParseResult parseResult = rootCommand.Parse(args);
		return parseResult.InvokeAsync();
	}
	
	private static bool SetUpDirectories(ParseResult result)
	{
		Log.InfoLine("Running SetUpDirectories:");
		
		if(result.Tokens.Count == 0)
		{
			Log.ErrorLine("No tokens. Run 'aurland --help'.");
			return false;
		}
		
		string? aurLandData = result.GetValue<string>("--data-prefix");
		
		if(aurLandData == string.Empty)
		{
			Log.InfoLine("'--data-prefix' is empty. Using XDG_DATA_HOME.");
			
			aurLandData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

			if(aurLandData == null || aurLandData == string.Empty)
			{
				Log.WarningLine("XDG_DATA_HOME is null. Using HOME.");
				
				aurLandData = Environment.GetEnvironmentVariable("HOME");
				
				if(aurLandData == null || aurLandData == string.Empty)
				{
					Log.ErrorLine("XDG_DATA_HOME and HOME is null or empty! '--data-prefix' needs to be specified!");
					return false;
				}
				
				aurLandData += "/.local/share/";
			}
		}
		
		if(!Directory.Exists(aurLandData))
		{
			Log.ErrorLine($"Directory '{aurLandData}' does not exist!");
			return false;
		}
		
		aurLandData = Path.Combine(aurLandData, "AurLand");
		
		_ALDataPath = Path.GetFullPath(aurLandData);
		
		_ALDataGitPath = Path.Combine(_ALDataPath, "git");
		_ALDataTxtPath = Path.Combine(_ALDataPath, "packages.txt");
		
		Log.WriteLine($"AurLand data path: {_ALDataPath}");
		Log.WriteLine($"AurLand data git path: {_ALDataGitPath}");
		Log.WriteLine($"AurLand data packages.txt path: {_ALDataTxtPath}");
		
		if(!Directory.Exists(_ALDataPath))
		{
			Log.WriteLine("\tCreating AurLand folder.");
			Directory.CreateDirectory(_ALDataPath);
		}
		if(!Directory.Exists(_ALDataGitPath))
		{
			Log.WriteLine("\tCreating git folder.");
			Directory.CreateDirectory(_ALDataGitPath);
		}
		if(!File.Exists(_ALDataTxtPath))
		{
			Log.WriteLine("\tCreating packages.txt file.");
			File.CreateText(_ALDataTxtPath);
		}
		
		DirectoryInfo di = new(_ALDataGitPath);
		
		Log.WriteLine($"Size of git folder: {SizeSuffix(DirecorySize(di))}.");
		
		using(FileStream txt = File.OpenRead(_ALDataTxtPath))
		{
			Log.WriteLine($"Size of packages.txt: {SizeSuffix(txt.Length)}.");
		}
		
		GetAurLandPackages();
		
		return true;
	}
	private static long DirecorySize(DirectoryInfo di)
	{
		long diSize = 0;
		
		FileInfo[] fileInfos = di.GetFiles();
		DirectoryInfo[] directoryInfos = di.GetDirectories();
		
		for(int i = 0; i < fileInfos.Length; i++)
		{
			diSize += fileInfos[i].Length;
		}
		for(int i = 0; i < directoryInfos.Length; i++)
		{
			diSize += DirecorySize(directoryInfos[i]);
		}
		return diSize;
	}
	private static async Task<int> ProccessOptions(ParseResult result)
	{
		bool sync = result.GetValue<bool>("--sync");
		bool update = result.GetValue<bool>("--update");
		string? install = result.GetValue<string>("--install");
		string? remove = result.GetValue<string>("--remove");
		
		if(sync)
			await SyncOption();
		if(update)
			UpdateOption();
		if(install != null && install != string.Empty)
			InstallOption(install);
		if(remove != null && remove != string.Empty)
			RemoveOption(remove);
		
		SaveAurLandPackages();
		
		return 1;
	}
	private static async Task SyncOption()
	{
		Log.InfoLine("Running SyncOption:");
		
		List<string> listForeignPackages = new();
		
		using (Process pacmanProcess = new Process())
		{
			pacmanProcess.StartInfo.FileName = "pacman";
			pacmanProcess.StartInfo.UseShellExecute = false;
			pacmanProcess.StartInfo.RedirectStandardOutput = true;
			
			pacmanProcess.StartInfo.Arguments = " -Qqm";
			
			Log.InfoLine($"'{pacmanProcess.StartInfo.FileName}{pacmanProcess.StartInfo.Arguments}':");
			
			pacmanProcess.Start();
			
			using (StreamReader sr = pacmanProcess.StandardOutput)
			{
				while (sr.Peek() >= 0)
				{
					string _line = sr.ReadLine() ?? "null";
					
					listForeignPackages.Add(_line);
					
					Log.WriteLine($"{_line}");
				}
			}
			pacmanProcess.WaitForExit();
		}
		
		for(int i = 0; i < listForeignPackages.Count; i++)
		{
			if(!_ALPackages.ContainsKey(listForeignPackages[i]))
			{
				Log.InfoLine($"'Request https://aur.archlinux.org/rpc/v5/search/{listForeignPackages[i]}':");
				
				using HttpResponseMessage response = await _HttpClient.GetAsync($"https://aur.archlinux.org/rpc/v5/search/{listForeignPackages[i]}");
				
				response.EnsureSuccessStatusCode();
				
				string jsonResponse = await response.Content.ReadAsStringAsync();
				Log.WriteLine($"{jsonResponse}");
				
				jsonResponse = jsonResponse.Replace(" ", "");
				
				string _v = jsonResponse.Substring(jsonResponse.IndexOf("\"resultcount\":") + 14, 1);
				
				if(_v != "0")
					_ALPackages.Add(listForeignPackages[i], new(){ Name = listForeignPackages[i], GitCommint = string.Empty });
				else
					Log.WarningLine($"{listForeignPackages[i]} does not exist in aur. resultcount: {_v}. Ignoring.");
			}		
		}
		
		//Clear removed.
		foreach(KeyValuePair<string, PackageInfo> p in _ALPackages)
		{
			if(!listForeignPackages.Contains(p.Value.Name))
				_ALPackages.Remove(p.Value.Name);
		}
		
		Log.WriteLine("");
	}
	private static async void UpdateOption()
	{
		Log.InfoLine("Running UpdateOption:");
		
		foreach(KeyValuePair<string, PackageInfo> p in _ALPackages)
		{
			Log.WriteLine($"\t{p.Key}");
			
			string _packageDirectory = Path.Combine(_ALDataGitPath, p.Value.Name);
			
			if(!Directory.Exists(_packageDirectory))
			{
				using (Process gitProcess = new Process())
				{
					gitProcess.StartInfo.FileName = "git";
					gitProcess.StartInfo.WorkingDirectory = _ALDataGitPath;
					gitProcess.StartInfo.UseShellExecute = false;
					gitProcess.StartInfo.RedirectStandardOutput = true;
					
					gitProcess.StartInfo.Arguments = $" clone https://aur.archlinux.org/{p.Value.Name}.git";
					
					Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
					
					gitProcess.Start();
					
					using (StreamReader sr = gitProcess.StandardOutput)
					{
						while (sr.Peek() >= 0)
						{
							Log.WriteLine($"{sr.ReadLine()}");
						}
					}
					gitProcess.WaitForExit();
				}
			}
			else
			{
				using (Process gitProcess = new Process())
				{
					gitProcess.StartInfo.FileName = "git";
					gitProcess.StartInfo.WorkingDirectory = _packageDirectory;
					gitProcess.StartInfo.UseShellExecute = false;
					gitProcess.StartInfo.RedirectStandardOutput = true;
					
					gitProcess.StartInfo.Arguments = $" pull";
					
					Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
					
					gitProcess.Start();
					
					using (StreamReader sr = gitProcess.StandardOutput)
					{
						while (sr.Peek() >= 0)
						{
							Log.WriteLine($"{sr.ReadLine()}");
						}
					}
					gitProcess.WaitForExit();
				}
			}
			
			//Printing PKGBUILD
			Log.InfoLine($"printing PKGBUILD:");
			using(FileStream pkgbuild = File.OpenRead(Path.Combine(_packageDirectory, "PKGBUILD")))
			{
				using (StreamReader sr = new StreamReader(pkgbuild)) 
				{
					while (sr.Peek() >= 0)
					{
						Log.WriteLine($"{sr.ReadLine()}");
					}
				}
			}
			
			if(p.Value.GitCommint != string.Empty)
			{
				using (Process gitProcess = new Process())
				{
					gitProcess.StartInfo.FileName = "git";
					gitProcess.StartInfo.WorkingDirectory = _packageDirectory;
					gitProcess.StartInfo.UseShellExecute = false;
					gitProcess.StartInfo.RedirectStandardOutput = true;
					
					gitProcess.StartInfo.Arguments = $" diff {p.Value.GitCommint}";
					
					Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
					
					gitProcess.Start();
					
					using (StreamReader sr = gitProcess.StandardOutput)
					{
						while (sr.Peek() >= 0)
						{
							Log.WriteLine($"{sr.ReadLine()}");
						}
					}
					gitProcess.WaitForExit();
				}
			}
			
			string _currentCommit = string.Empty;
			
			using (Process gitProcess = new Process())
			{
				gitProcess.StartInfo.FileName = "git";
				gitProcess.StartInfo.WorkingDirectory = _packageDirectory;
				gitProcess.StartInfo.UseShellExecute = false;
				gitProcess.StartInfo.RedirectStandardOutput = true;
				
				gitProcess.StartInfo.Arguments = $" rev-parse HEAD";
				
				Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
				
				gitProcess.Start();
				
				using (StreamReader sr = gitProcess.StandardOutput)
				{
					while (sr.Peek() >= 0)
					{
						string _line = sr.ReadLine()??"null";
						
						Log.WriteLine($"{_line}");
						
						_currentCommit = _line;
					}
				}
				gitProcess.WaitForExit();
			}
			
			if(p.Value.GitCommint == string.Empty || p.Value.GitCommint != _currentCommit)
			{
				Log.WriteLine($"Update '{p.Value.Name}'? (Y/n)");
				
				ConsoleKeyInfo _key = Console.ReadKey(true);
				if(_key.Key == ConsoleKey.Y || _key.Key == ConsoleKey.Enter)
				{	
					using (Process makepkgProcess = new Process())
					{
						makepkgProcess.StartInfo.FileName = "makepkg";
						makepkgProcess.StartInfo.WorkingDirectory = _packageDirectory;
						makepkgProcess.StartInfo.UseShellExecute = false;
						makepkgProcess.StartInfo.RedirectStandardOutput = true;

						makepkgProcess.StartInfo.Arguments = $" -si";
						
						Log.InfoLine($"'{makepkgProcess.StartInfo.FileName}{makepkgProcess.StartInfo.Arguments}':");
						
						makepkgProcess.Start();
						
						using (StreamReader sr = makepkgProcess.StandardOutput)
						{
							while (sr.Peek() >= 0)
							{
								Log.WriteLine($"{sr.ReadLine()}");
							}
						}
						
						makepkgProcess.WaitForExit();
						
						if(makepkgProcess.ExitCode != 0)
						{
							Log.ErrorLine("makepkgProcess is aborted!");
							return;
						}
					}
					
					_ALPackages[p.Key].GitCommint = _currentCommit;
				}
				
				Log.WriteLine(string.Empty);
			}
			else
			{
				Log.WriteLine($"{p.Value.Name} latest is installed.");
			}
		}
		
		Log.WriteLine("");
	}
	private static async void InstallOption(string? package)
	{
		Log.InfoLine("Running InstallOption:");
		
		if(package == null)
		{
			Log.ErrorLine("package name is 'null'.");
			return;
		}
		
		using (Process gitProcess = new Process())
		{
			gitProcess.StartInfo.FileName = "git";
			gitProcess.StartInfo.WorkingDirectory = _ALDataGitPath;
			gitProcess.StartInfo.UseShellExecute = false;
			gitProcess.StartInfo.RedirectStandardOutput = true;
			
			gitProcess.StartInfo.Arguments = $" clone https://aur.archlinux.org/{package}.git";
			
			Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
			
			gitProcess.Start();
			
			using (StreamReader sr = gitProcess.StandardOutput)
			{
				while (sr.Peek() >= 0)
				{
					Log.WriteLine($"{sr.ReadLine()}");
				}
			}
			gitProcess.WaitForExit();
		}
		
		string packageDirectory = Path.Combine(_ALDataGitPath, package);
		
		//Printing PKGBUILD
		Log.InfoLine($"printing PKGBUILD:");
		using(FileStream pkgbuild = File.OpenRead(Path.Combine(packageDirectory, "PKGBUILD")))
		{
			using (StreamReader sr = new StreamReader(pkgbuild)) 
			{
				while (sr.Peek() >= 0)
				{
					Log.WriteLine($"{sr.ReadLine()}");
				}
			}
		}
		
		string currentCommit = string.Empty;
		
		using (Process gitProcess = new Process())
		{
			gitProcess.StartInfo.FileName = "git";
			gitProcess.StartInfo.WorkingDirectory = packageDirectory;
			gitProcess.StartInfo.UseShellExecute = false;
			gitProcess.StartInfo.RedirectStandardOutput = true;
			
			gitProcess.StartInfo.Arguments = $" rev-parse HEAD";
			
			Log.InfoLine($"'{gitProcess.StartInfo.FileName}{gitProcess.StartInfo.Arguments}':");
			
			gitProcess.Start();
			
			using (StreamReader sr = gitProcess.StandardOutput)
			{
				while (sr.Peek() >= 0)
				{
					string _line = sr.ReadLine() ?? "null";
					
					Log.WriteLine($"{_line}");
					
					currentCommit = _line;
				}
			}
			gitProcess.WaitForExit();
		}
		
		Log.WriteLine($"Install '{package}'? (Y/n)");
		
		ConsoleKeyInfo _key = Console.ReadKey(true);
		if(_key.Key == ConsoleKey.Y || _key.Key == ConsoleKey.Enter)
		{	
			using (Process makepkgProcess = new Process())
			{
				makepkgProcess.StartInfo.FileName = "makepkg";
				makepkgProcess.StartInfo.WorkingDirectory = packageDirectory;
				makepkgProcess.StartInfo.UseShellExecute = false;
				makepkgProcess.StartInfo.RedirectStandardOutput = true;
				
				makepkgProcess.StartInfo.Arguments = $" -si";
				
				Log.InfoLine($"'{makepkgProcess.StartInfo.FileName}{makepkgProcess.StartInfo.Arguments}':");
				
				makepkgProcess.Start();
				
				using (StreamReader sr = makepkgProcess.StandardOutput)
				{
					while (sr.Peek() >= 0)
					{
						Log.WriteLine($"{sr.ReadLine()}");
					}
				}
				
				makepkgProcess.WaitForExit();
				
				if(makepkgProcess.ExitCode != 0)
				{
					Log.ErrorLine("makepkgProcess is aborted!");
					return;
				}
			}
			
			_ALPackages.Add(package,new(){ Name = package, GitCommint = currentCommit});
		}
		
		Log.WriteLine("");
	}
	private static void RemoveOption(string? package)
	{
		Log.InfoLine("Running RemoveOption:");
		
		if(package == null)
		{
			Log.ErrorLine("package name is null");
			return;
		}
		
		if(!_ALPackages.ContainsKey(package))
		{
			Log.ErrorLine($"'{package}' package does not exist in AurLand txt. Try running 'aurland sync' first.");
			return;
		}
		string packageDirectory = Path.Combine(_ALDataGitPath, package);
		
		Log.WriteLine($"Remove '{package}'? (Y/n)");
		
		ConsoleKeyInfo _key = Console.ReadKey(true);
		
		if(_key.Key == ConsoleKey.Y || _key.Key == ConsoleKey.Enter)
		{	
			using (Process pacmanProcess = new Process())
			{
				pacmanProcess.StartInfo.FileName = "pacman";
				pacmanProcess.StartInfo.UseShellExecute = false;
				pacmanProcess.StartInfo.RedirectStandardOutput = true;
				
				pacmanProcess.StartInfo.Arguments = $" -Rs {package}";
				
				Log.InfoLine($"'{pacmanProcess.StartInfo.FileName}{pacmanProcess.StartInfo.Arguments}':");
				
				pacmanProcess.Start();
				
				using (StreamReader sr = pacmanProcess.StandardOutput)
				{
					while (sr.Peek() >= 0)
					{
						Log.WriteLine($"{sr.ReadLine()}");
					}
				}
				
				pacmanProcess.WaitForExit();
			}
			
			_ALPackages.Remove(package);
		}
		
		Log.WriteLine("");
	}
	
	private static void GetAurLandPackages()
	{
		Log.InfoLine("Running GetAurLandPackages:");
		
		_ALPackages.Clear();
		
		string[] aurlandPackages = File.ReadAllLines(_ALDataTxtPath);
		
		for(int i = 0; i < aurlandPackages.Length; i++)
		{
			string[] split = aurlandPackages[i].Split("|");
			
			_ALPackages.Add(split[0], new(){ Name = split[0], GitCommint = split.Length == 2 ? split[1] : string.Empty });
		}
		
		Log.WriteLine($"aur packages in packages.txt: {aurlandPackages.Length}");
		Log.WriteLine($"aur packages in _ALPackages: {_ALPackages.Count}");
		Log.WriteLine("");
	}
	
	private static void SaveAurLandPackages()
	{
		Log.InfoLine("Running SaveAurLandPackages:");
		
		File.WriteAllText(_ALDataTxtPath, String.Empty);
		
		Log.WriteLine("Updated packages.txt:");
		
		using (StreamWriter sw = File.AppendText(_ALDataTxtPath))
		{
			foreach(KeyValuePair<string, PackageInfo> p in _ALPackages)
			{
				sw.WriteLine($"{p.Value.Name}|{p.Value.GitCommint}");
				
				Log.WriteLine($"\t{p.Value.Name}|{p.Value.GitCommint}");
			}
		}
		Log.WriteLine("");
	}
	
	//https://stackoverflow.com/a/14488941
	static readonly string[] SizeSuffixes = 
	{ "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
	static string SizeSuffix(Int64 value, int decimalPlaces = 1)
	{
		if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
		if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); } 
		if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }
		
		// mag is 0 for bytes, 1 for KB, 2, for MB, etc.
		int mag = (int)Math.Log(value, 1024);
		
		// 1L << (mag * 10) == 2 ^ (10 * mag) 
		// [i.e. the number of bytes in the unit corresponding to mag]
		decimal adjustedSize = (decimal)value / (1L << (mag * 10));
		
		// make adjustment when the value is large enough that
		// it would round up to 1000 or more
		if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
		{
			mag += 1;
			adjustedSize /= 1024;
		}
		
		return string.Format("{0:n" + decimalPlaces + "} {1}", 
							 adjustedSize, 
					   SizeSuffixes[mag]);
	}
}

public class PackageInfo
{
	public string Name {get; set; } = string.Empty;
	public string GitCommint {get; set; } = string.Empty;
}
