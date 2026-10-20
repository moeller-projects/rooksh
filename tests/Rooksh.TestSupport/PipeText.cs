using System.Text;
using System.Threading.Channels;

namespace Rooksh.TestSupport;

/// <summary>A <see cref="TextWriter"/> that raises a callback for every completed line.</summary>
internal sealed class PipeTextWriter : TextWriter
{
    private readonly Action<string> _onLine;
    private readonly StringBuilder _buffer = new();

    public PipeTextWriter(Action<string> onLine)
    {
        _onLine = onLine;
        NewLine = "\n";
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
        {
            var line = _buffer.ToString().TrimEnd('\r');
            _buffer.Clear();
            _onLine(line);
            return;
        }

        _buffer.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var c in value)
        {
            Write(c);
        }
    }

    public override Task WriteLineAsync(string? value)
    {
        _onLine((value ?? string.Empty).TrimEnd('\r'));
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(char value)
    {
        _onLine(value.ToString());
        return Task.CompletedTask;
    }

    public override Task FlushAsync() => Task.CompletedTask;
}

/// <summary>A <see cref="TextReader"/> over a channel; returns null when the channel completes.</summary>
internal sealed class PipeTextReader : TextReader
{
    private readonly ChannelReader<string> _reader;

    public PipeTextReader(ChannelReader<string> reader)
    {
        _reader = reader;
    }

    public override async Task<string?> ReadLineAsync()
    {
        while (await _reader.WaitToReadAsync().ConfigureAwait(false))
        {
            if (_reader.TryRead(out var line))
            {
                return line;
            }
        }

        return null;
    }

    public override string? ReadLine()
    {
        while (_reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            if (_reader.TryRead(out var line))
            {
                return line;
            }
        }

        return null;
    }
}
