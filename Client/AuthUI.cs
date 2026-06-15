using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class AuthUI
{
    private string _address;
    private int _port;

    public AuthUI(string address, int port)
    {
        _address = address;
        _port = port;
    }

    public async Task<User?> RunAsync()
    {
        int selectedOption = 0;
        string[] options = { "Log In", "Register", "Exit" };

        while (true)
        {
            Console.Clear();
            Console.CursorVisible = false;

            int w = Console.WindowWidth;
            int h = Console.WindowHeight;
            int startX = Math.Max(0, (w - 30) / 2);
            int startY = Math.Max(0, (h - 10) / 2);

            DrawAt(startX, startY, "==============================");
            DrawAt(startX, startY + 1, "       CHAT APPLICATION       ");
            DrawAt(startX, startY + 2, "==============================");

            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedOption)
                {
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Black;
                    DrawAt(startX + 10, startY + 4 + i, $"> {options[i]} <");
                    Console.ResetColor();
                }
                else
                {
                    DrawAt(startX + 10, startY + 4 + i, $"  {options[i]}  ");
                }
            }

            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                selectedOption = (selectedOption > 0) ? selectedOption - 1 : options.Length - 1;
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                selectedOption = (selectedOption < options.Length - 1) ? selectedOption + 1 : 0;
            }
            else if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (selectedOption == 0) // Login
                {
                    var user = await DoLoginAsync();
                    if (user != null) return user;
                }
                else if (selectedOption == 1) // Register
                {
                    var user = await DoRegisterAsync();
                    if (user != null) return user;
                }
                else if (selectedOption == 2) // Exit
                {
                    return null; // Signals exit
                }
            }
        }
    }

    private void DrawAt(int x, int y, string text)
    {
        if (x >= 0 && x < Console.WindowWidth && y >= 0 && y < Console.WindowHeight)
        {
            Console.SetCursorPosition(x, y);
            Console.Write(text);
        }
    }

    private async Task<User?> DoLoginAsync()
    {
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine("=== LOGIN ===");
        Console.Write("Enter username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Enter password: ");
        var password = ReadPassword();

        try 
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(_address), _port);
            using var stream = client.GetStream();

            var user = new User { Login = username, Password = password };

            await NetworkHelper.SendStringAsync("login", stream);
            await NetworkHelper.SendStringAsync(username, stream);
            await NetworkHelper.SendObjectAsync(user, stream);

            var authResult = await NetworkHelper.ReceiveObjectAsync<AuthResult>(stream);

            if (authResult != null && authResult.IsAuthenticated)
            {
                Console.WriteLine("\nLogin successful! Transitioning to chat...");
                await Task.Delay(1000);
                return user;
            }

            Console.WriteLine($"\nLogin failed: {authResult?.Message ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nConnection error: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to return...");
        Console.ReadKey(intercept: true);
        return null;
    }

    private async Task<User?> DoRegisterAsync()
    {
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine("=== REGISTER ===");
        Console.Write("Choose username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Choose password: ");
        var password = ReadPassword();

        try 
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(_address), _port);
            using var stream = client.GetStream();

            var newUser = new User { Login = username, Password = password };

            await NetworkHelper.SendStringAsync("register", stream);
            await NetworkHelper.SendStringAsync(username, stream);
            await NetworkHelper.SendObjectAsync(newUser, stream);

            var registerResult = await NetworkHelper.ReceiveObjectAsync<RegisterResult>(stream);

            if (registerResult != null && registerResult.IsRegistered)
            {
                Console.WriteLine("\nRegistration successful! Transitioning to chat...");
                await Task.Delay(1000);
                return newUser; 
            }

            Console.WriteLine($"\nRegistration failed: {registerResult?.Message ?? "Username already exists or unknown error"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nConnection error: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to return...");
        Console.ReadKey(intercept: true);
        return null;
    }

    private string ReadPassword()
    {
        string password = "";
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key == ConsoleKey.Enter) break;

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                password += keyInfo.KeyChar;
                Console.Write("*");
            }
        }
        return password;
    }
}
