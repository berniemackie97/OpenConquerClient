using System.Reflection;
using OpenConquer.Platform.Windowing.Desktop;
using Silk.NET.Input;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopKeyboardInputTests
{
    [Theory]
    [InlineData(Key.Tab, DesktopKey.Tab)]
    [InlineData(Key.Enter, DesktopKey.Enter)]
    [InlineData(Key.KeypadEnter, DesktopKey.Enter)]
    [InlineData(Key.Escape, DesktopKey.Escape)]
    [InlineData(Key.Backspace, DesktopKey.Backspace)]
    [InlineData(Key.Delete, DesktopKey.Delete)]
    [InlineData(Key.Left, DesktopKey.Left)]
    [InlineData(Key.Right, DesktopKey.Right)]
    [InlineData(Key.Up, DesktopKey.Up)]
    [InlineData(Key.Down, DesktopKey.Down)]
    [InlineData(Key.Home, DesktopKey.Home)]
    [InlineData(Key.End, DesktopKey.End)]
    [InlineData(Key.PageUp, DesktopKey.PageUp)]
    [InlineData(Key.PageDown, DesktopKey.PageDown)]
    [InlineData(Key.A, DesktopKey.A)]
    [InlineData(Key.C, DesktopKey.C)]
    [InlineData(Key.V, DesktopKey.V)]
    [InlineData(Key.X, DesktopKey.X)]
    public void TranslateKey_MapsSupportedDesktopCommands(Key key, DesktopKey expected)
    {
        Assert.Equal(expected, DesktopKeyboardInput.TranslateKey(key));
    }

    [Theory]
    [InlineData(Key.Unknown)]
    [InlineData(Key.B)]
    [InlineData(Key.ShiftLeft)]
    public void TranslateKey_IgnoresKeysWithoutCommandSemantics(Key key)
    {
        Assert.Equal(DesktopKey.Unknown, DesktopKeyboardInput.TranslateKey(key));
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('é', true)]
    [InlineData('中', true)]
    [InlineData('\u0301', true)]
    [InlineData('\n', false)]
    [InlineData('\t', false)]
    [InlineData('\0', false)]
    [InlineData('\uD83D', false)]
    [InlineData('\uDE00', false)]
    public void IsTextCharacter_RejectsControlAndUnpairedSurrogateUnits(char character, bool expected)
    {
        Assert.Equal(expected, DesktopKeyboardInput.IsTextCharacter(character));
    }

    [Fact]
    public void Subscription_RoutesCommandsTextAndModifiersThenDetachesOnDispose()
    {
        IKeyboard keyboard = DispatchProxy.Create<IKeyboard, KeyboardProxy>();
        KeyboardProxy proxy = (KeyboardProxy)(object)keyboard;
        List<DesktopKeyEvent> pressed = [];
        List<DesktopKeyEvent> released = [];
        List<char> text = [];

        DesktopKeyboardInput input = new(keyboard, hasFocus: true, pressed.Add, released.Add, text.Add);

        proxy.PressedKeys.Add(Key.ShiftLeft);
        proxy.RaiseKeyDown(keyboard, Key.Tab);
        proxy.RaiseKeyDown(keyboard, Key.Unknown);
        proxy.RaiseKeyChar(keyboard, 'é');
        proxy.RaiseKeyChar(keyboard, '\n');
        proxy.RaiseKeyUp(keyboard, Key.Tab);

        Assert.Equal(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.Shift, false), Assert.Single(pressed));
        Assert.Equal(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.Shift, false), Assert.Single(released));
        Assert.Equal('é', Assert.Single(text));

        input.Dispose();
        input.Dispose();

        Assert.Equal(0, proxy.SubscriberCount);
        Assert.Throws<ObjectDisposedException>(input.Advance);
        Assert.Throws<ObjectDisposedException>(() => input.SetFocus(true));

        proxy.RaiseKeyDown(keyboard, Key.Tab);
        proxy.RaiseKeyChar(keyboard, 'x');
        Assert.Single(pressed);
        Assert.Single(text);
    }

    [Fact]
    public void FocusLoss_DiscardsCommandsTextAndHeldRepeatUntilFocusReturns()
    {
        IKeyboard keyboard = DispatchProxy.Create<IKeyboard, KeyboardProxy>();
        KeyboardProxy proxy = (KeyboardProxy)(object)keyboard;
        List<DesktopKeyEvent> pressed = [];
        List<DesktopKeyEvent> released = [];
        List<char> text = [];

        using DesktopKeyboardInput input = new(keyboard, hasFocus: true, pressed.Add, released.Add, text.Add);

        proxy.RaiseKeyDown(keyboard, Key.Backspace);
        input.SetFocus(false);
        proxy.RaiseKeyDown(keyboard, Key.Enter);
        proxy.RaiseKeyChar(keyboard, 'q');
        proxy.RaiseKeyUp(keyboard, Key.Backspace);
        input.Advance();

        Assert.Single(pressed);
        Assert.Empty(released);
        Assert.Empty(text);

        input.SetFocus(true);
        proxy.RaiseKeyDown(keyboard, Key.Enter);
        proxy.RaiseKeyChar(keyboard, 'a');

        Assert.Equal(2, pressed.Count);
        Assert.Equal(DesktopKey.Enter, pressed[1].Key);
        Assert.Equal('a', Assert.Single(text));
    }

    [Fact]
    public void ModifierMapping_PreservesCommandControlAltAndSuper()
    {
        IKeyboard keyboard = DispatchProxy.Create<IKeyboard, KeyboardProxy>();
        KeyboardProxy proxy = (KeyboardProxy)(object)keyboard;
        List<DesktopKeyEvent> pressed = [];

        using DesktopKeyboardInput input = new(keyboard, hasFocus: true, pressed.Add, _ => { }, _ => { });

        proxy.PressedKeys.UnionWith([Key.ShiftRight, Key.ControlLeft, Key.AltRight, Key.SuperLeft]);
        proxy.RaiseKeyDown(keyboard, Key.A);

        Assert.Equal(DesktopKeyModifiers.Shift | DesktopKeyModifiers.Control | DesktopKeyModifiers.Alt | DesktopKeyModifiers.Super, Assert.Single(pressed).Modifiers);
    }

    public class KeyboardProxy : DispatchProxy
    {
        private Action<IKeyboard, Key, int>? _keyDown;
        private Action<IKeyboard, Key, int>? _keyUp;
        private Action<IKeyboard, char>? _keyChar;

        public HashSet<Key> PressedKeys { get; } = [];

        public int SubscriberCount => (_keyDown?.GetInvocationList().Length ?? 0) + (_keyUp?.GetInvocationList().Length ?? 0) + (_keyChar?.GetInvocationList().Length ?? 0);

        public void RaiseKeyDown(IKeyboard keyboard, Key key) => _keyDown?.Invoke(keyboard, key, 0);
        public void RaiseKeyUp(IKeyboard keyboard, Key key) => _keyUp?.Invoke(keyboard, key, 0);
        public void RaiseKeyChar(IKeyboard keyboard, char character) => _keyChar?.Invoke(keyboard, character);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "add_KeyDown" => SubscribeKeyDown((Action<IKeyboard, Key, int>)args![0]!),
                "remove_KeyDown" => UnsubscribeKeyDown((Action<IKeyboard, Key, int>)args![0]!),
                "add_KeyUp" => SubscribeKeyUp((Action<IKeyboard, Key, int>)args![0]!),
                "remove_KeyUp" => UnsubscribeKeyUp((Action<IKeyboard, Key, int>)args![0]!),
                "add_KeyChar" => SubscribeKeyChar((Action<IKeyboard, char>)args![0]!),
                "remove_KeyChar" => UnsubscribeKeyChar((Action<IKeyboard, char>)args![0]!),
                "IsKeyPressed" => PressedKeys.Contains((Key)args![0]!),
                _ => throw new NotSupportedException($"Unexpected keyboard API call '{targetMethod?.Name}'."),
            };
        }

        private object? SubscribeKeyDown(Action<IKeyboard, Key, int> handler)
        {
            _keyDown += handler;
            return null;
        }

        private object? UnsubscribeKeyDown(Action<IKeyboard, Key, int> handler)
        {
            _keyDown -= handler;
            return null;
        }

        private object? SubscribeKeyUp(Action<IKeyboard, Key, int> handler)
        {
            _keyUp += handler;
            return null;
        }

        private object? UnsubscribeKeyUp(Action<IKeyboard, Key, int> handler)
        {
            _keyUp -= handler;
            return null;
        }

        private object? SubscribeKeyChar(Action<IKeyboard, char> handler)
        {
            _keyChar += handler;
            return null;
        }

        private object? UnsubscribeKeyChar(Action<IKeyboard, char> handler)
        {
            _keyChar -= handler;
            return null;
        }
    }
}
