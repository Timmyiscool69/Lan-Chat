using System;
using System.IO;
using System.Diagnostics;

class LanChatInstaller
{
    static void Main()
    {
        const string RequiredPassword = "LanChat2025";
        const string InstallPath = "C:\\Program Files\\Lan Chat";
        
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║        Lan Chat Installer v1.0.0       ║");
        Console.WriteLine("║            Publisher: Tim OS            ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        
        Console.Write("Enter installer password: ");
        string input = ReadPassword();
        
        if (input != RequiredPassword)
        {
            Console.WriteLine("\n[ERROR] Incorrect password. Installation aborted.");
            System.Threading.Thread.Sleep(2000);
            return;
        }
        
        Console.WriteLine("\n[OK] Password accepted. Installing...\n");
        
        try
        {
            // Create install directory
            if (!Directory.Exists(InstallPath))
            {
                Directory.CreateDirectory(InstallPath);
                Console.WriteLine($"[+] Created directory: {InstallPath}");
            }
            
            // Copy files from current directory to install path
            string sourceDir = AppDomain.CurrentDomain.BaseDirectory;
            CopyDirectory(sourceDir, InstallPath);
            Console.WriteLine($"[+] Files copied to: {InstallPath}");
            
            // Create Start Menu shortcut
            CreateStartMenuShortcut(InstallPath);
            Console.WriteLine("[+] Start Menu shortcut created");
            
            Console.WriteLine("\n[SUCCESS] Installation complete!");
            Console.WriteLine($"Lan Chat is installed at: {InstallPath}");
            Console.WriteLine("You can launch it from the Start Menu.");
            
            // Ask to launch
            Console.Write("\nLaunch Lan Chat now? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                Process.Start(Path.Combine(InstallPath, "LanChat.exe"));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Installation failed: {ex.Message}");
            System.Threading.Thread.Sleep(3000);
        }
    }
    
    static string ReadPassword()
    {
        string password = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                    password = password.Substring(0, password.Length - 1);
            }
            else
            {
                password += key.KeyChar;
            }
        }
        return password;
    }
    
    static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);
        
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            if (Path.GetFileName(file).ToLower() != "lanchatchatinstaller.exe")
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
        }
    }
    
    static void CreateStartMenuShortcut(string installPath)
    {
        try
        {
            string startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft\\Windows\\Start Menu\\Programs");
            
            string lnkPath = Path.Combine(startMenuPath, "Lan Chat.lnk");
            
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    shortcut.TargetPath = Path.Combine(installPath, "LanChat.exe");
                    shortcut.WorkingDirectory = installPath;
                    shortcut.Description = "Local network chat application";
                    shortcut.Save();
                }
            }
        }
        catch { }
    }
}
