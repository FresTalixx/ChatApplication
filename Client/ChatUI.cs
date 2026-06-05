using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ChatUI
{
    private string _address;
    private int _port;
    private string _currentUser;
    private string _targetUser = string.Empty;
    private List<string> _users = new();
    private List<Message> _chatHistory = new();

    private int _selectedUserIndex = 0;
    private string _inputBuffer = "";

    // UI State
    // 0 = Users Pane, 1 = Chat View (ReadOnly scrolling?), 2 = Input Box
    private int _focusedPane = 0; 

    private int _consoleWidth;
    private int _consoleHeight;

    private int _leftPaneWidth;
    private int _rightPaneWidth;
    private int _inputPaneHeight = 4;

    private object _syncRoot = new object();
    private CancellationTokenSource _cts;

    public ChatUI(string address, int port, string currentUser)
    {
        _address = address;
        _port = port;
        _currentUser = currentUser;

        _consoleWidth = Console.WindowWidth;
        _consoleHeight = Console.WindowHeight;
        _leftPaneWidth = _consoleWidth / 3;
        _rightPaneWidth = _consoleWidth - _leftPaneWidth;
    }

    public async Task RunAsync()
    {
        Console.Clear();
        Console.CursorVisible = false;

        _cts = new CancellationTokenSource();

        // 1. Fetch Users
        _users = await ChatEventHandlerClient.GetUsersAsync(_address, _port, _currentUser) ?? new List<string>();
        _users.RemoveAll(u => u == _currentUser);

        if (_users.Count > 0)
        {
            _targetUser = _users[0];
            await LoadChatHistoryAsync();
        }

        DrawUI();

        _ = PollMessagesAsync(_cts.Token);

        while (!_cts.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                HandleInput(keyInfo);
            }
            else
            {
                await Task.Delay(50);
            }
        }
    }

    private void HandleInput(ConsoleKeyInfo keyInfo)
    {
        lock (_syncRoot)
        {
            if (keyInfo.Key == ConsoleKey.Escape)
            {
                _cts.Cancel();
                Console.Clear();
                return;
            }

            if (keyInfo.Key == ConsoleKey.Tab)
            {
                _focusedPane = (_focusedPane + 1) % 2; // 0 = Users, 1 = Input
                DrawUI();
                return;
            }

            if (_focusedPane == 0) // Users Pane
            {
                if (keyInfo.Key == ConsoleKey.UpArrow && _selectedUserIndex > 0)
                {
                    _selectedUserIndex--;
                    DrawUI();
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow && _selectedUserIndex < _users.Count - 1)
                {
                    _selectedUserIndex++;
                    DrawUI();
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    ChangeTargetUser(_users[_selectedUserIndex]);
                }
            }
            else if (_focusedPane == 1) // Input Pane
            {
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    if (!string.IsNullOrWhiteSpace(_inputBuffer) && !string.IsNullOrEmpty(_targetUser))
                    {
                        var msgToSend = _inputBuffer;
                        _inputBuffer = "";
                        DrawUI();

                        _ = SendMessageFireAndForget(msgToSend);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (_inputBuffer.Length > 0)
                    {
                        _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
                        DrawUI();
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    if (_inputBuffer.Length < _rightPaneWidth - 4) // simple limit
                    {
                        _inputBuffer += keyInfo.KeyChar;
                        DrawUI();
                    }
                }
            }
        }
    }

    private async void ChangeTargetUser(string newTarget)
    {
        _targetUser = newTarget;
        await LoadChatHistoryAsync();
        lock (_syncRoot)
        {
            DrawUI();
        }
    }

    private async Task LoadChatHistoryAsync()
    {
        if (string.IsNullOrEmpty(_targetUser)) return;

        var hist = await ChatEventHandlerClient.GetChatHistoryAsync(_address, _port, _currentUser, _targetUser);
        lock (_syncRoot)
        {
            _chatHistory = hist ?? new List<Message>();
        }
    }

    private async Task SendMessageFireAndForget(string text)
    {
        var target = _targetUser;
        if (string.IsNullOrEmpty(target)) return;

        await ChatEventHandlerClient.SendMessageAsync(_address, _port, _currentUser, target, text);

        // Optimistically add to history
        lock (_syncRoot)
        {
            _chatHistory.Add(new Message
            {
                Sender = _currentUser,
                Recipient = target,
                Text = text,
                SendingDate = DateTime.Now
            });
            DrawUI();
        }
    }

    private async Task PollMessagesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // We shouldn't use ChatEventHandlerClient.PollMessagesAsync here because it writes to Console directly
                // Instead, let's poll manually.
                using var client = new TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Parse(_address), _port);
                using var stream = client.GetStream();

                await NetworkHelper.SendStringAsync("get_new_messages", stream);
                await NetworkHelper.SendStringAsync(_currentUser, stream);

                var msgs = await NetworkHelper.ReceiveObjectAsync<List<Message>>(stream);
                if (msgs != null && msgs.Count > 0)
                {
                    bool shouldRedraw = false;
                    lock (_syncRoot)
                    {
                        foreach (var msg in msgs)
                        {
                            if (msg.Sender == _targetUser)
                            {
                                _chatHistory.Add(msg);
                                shouldRedraw = true;
                            }
                        }
                        if (shouldRedraw) DrawUI();
                    }
                }
            }
            catch { /* Ignore network errors in polling loop */ }

            await Task.Delay(3000, token);
        }
    }

    private void DrawUI()
    {
        // Must be locked externally

        // If console is resized
        if (Console.WindowWidth != _consoleWidth || Console.WindowHeight != _consoleHeight)
        {
            Console.Clear();
            _consoleWidth = Console.WindowWidth;
            _consoleHeight = Console.WindowHeight;
            _leftPaneWidth = _consoleWidth / 3;
            _rightPaneWidth = _consoleWidth - _leftPaneWidth;
        }

        DrawBorders();
        DrawUsersPane();
        DrawChatPane();
        DrawInputPane();
    }

    private void DrawBorders()
    {
        for (int y = 0; y < _consoleHeight; y++)
        {
            SetCursorPosition(_leftPaneWidth, y);
            Console.Write("|");
        }

        int separatorY = _consoleHeight - _inputPaneHeight - 1;
        for (int x = _leftPaneWidth + 1; x < _consoleWidth; x++)
        {
            SetCursorPosition(x, separatorY);
            Console.Write("-");
        }
    }

    private void DrawUsersPane()
    {
        for (int y = 0; y < _consoleHeight; y++)
        {
            SetCursorPosition(0, y);
            Console.Write(new string(' ', _leftPaneWidth - 1));
        }

        SetCursorPosition(0, 0);
        if (_focusedPane == 0)
            Console.Write("=== USERS (Focused) ===");
        else
            Console.Write("=== USERS ===========");

        int startY = 2;
        for (int i = 0; i < _users.Count; i++)
        {
            if (i >= _consoleHeight - 3) break;
            SetCursorPosition(2, startY + i);

            if (i == _selectedUserIndex && _focusedPane == 0)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write($" [{_users[i]}] ");
                Console.ResetColor();
            }
            else if (i == _selectedUserIndex && _focusedPane != 0)
            {
                 Console.Write($" [{_users[i]}] ");
            }
            else
            {
                Console.Write($"  {_users[i]}  ");
            }

            if (_users[i] == _targetUser)
            {
                Console.Write(" *"); // Mark active chat
            }
        }
    }

    private void DrawChatPane()
    {
        int chatAreaHeight = _consoleHeight - _inputPaneHeight - 2;
        int startX = _leftPaneWidth + 2;

        for (int y = 0; y < chatAreaHeight + 1; y++)
        {
            SetCursorPosition(startX, y);
            Console.Write(new string(' ', _rightPaneWidth - 3));
        }

        // Title
        SetCursorPosition(startX, 0);
        Console.Write($"=== CHAT WITH {(_targetUser == "" ? "NOBODY" : _targetUser)} ===");

        // Draw last N messages
        int numMessagesToShow = Math.Min(_chatHistory.Count, chatAreaHeight - 1);
        int startIndex = _chatHistory.Count - numMessagesToShow;

        int rowY = 2;
        for (int i = startIndex; i < _chatHistory.Count; i++)
        {
            var msg = _chatHistory[i];
            string prefix = msg.Sender == _currentUser ? "You:" : $"{msg.Sender}:";
            string text = $"{prefix} {msg.Text}";
            if (text.Length > _rightPaneWidth - 4) text = text.Substring(0, _rightPaneWidth - 4);

            SetCursorPosition(startX, rowY);
            Console.Write(text);
            rowY++;
        }
    }

    private void DrawInputPane()
    {
        int startX = _leftPaneWidth + 2;
        int startY = _consoleHeight - _inputPaneHeight;

        for (int y = startY; y < _consoleHeight; y++)
        {
            SetCursorPosition(startX, y);
            Console.Write(new string(' ', _rightPaneWidth - 3));
        }

        SetCursorPosition(startX, startY);
        if (_focusedPane == 1)
            Console.Write("=== INPUT (Focused) ===");
        else
            Console.Write("=== INPUT ===");

        SetCursorPosition(startX, startY + 1);
        Console.Write("> " + _inputBuffer);

        if (_focusedPane == 1)
        {
            Console.CursorVisible = true;
            SetCursorPosition(startX + 2 + _inputBuffer.Length, startY + 1);
        }
        else
        {
            Console.CursorVisible = false;
        }
    }

    private void SetCursorPosition(int x, int y)
    {
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x >= _consoleWidth) x = _consoleWidth - 1;
        if (y >= _consoleHeight) y = _consoleHeight - 1;
        Console.SetCursorPosition(x, y);
    }
}
