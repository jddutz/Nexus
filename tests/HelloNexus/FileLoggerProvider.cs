namespace HelloNexus;

using Microsoft.Extensions.Logging;

/// <summary>
/// Writes log messages to a file that is recreated when the provider starts.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    /// <summary>
    /// Creates a file logger provider and truncates the target file.
    /// </summary>
    /// <param name="path">The path of the log file.</param>
    public FileLoggerProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        path = Path.GetFullPath(path, AppContext.BaseDirectory);
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <param name="categoryName">The logger category.</param>
    /// <returns>A logger that writes to this provider.</returns>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>
    /// Disposes the file writer.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }

    /// <summary>
    /// Writes a formatted log entry when the provider accepts the level.
    /// </summary>
    /// <typeparam name="TState">The log state type.</typeparam>
    /// <param name="categoryName">The logger category.</param>
    /// <param name="logLevel">The event level.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="state">The event state.</param>
    /// <param name="exception">The associated exception, if any.</param>
    /// <param name="formatter">The message formatter.</param>
    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        lock (_sync)
        {
            _writer.Write(DateTimeOffset.Now.ToString("O"));
            _writer.Write(" [");
            _writer.Write(logLevel);
            _writer.Write("] ");
            _writer.Write(categoryName);
            _writer.Write(": ");
            _writer.WriteLine(formatter(state, exception));

            if (exception is not null)
                _writer.WriteLine(exception);
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        /// <summary>
        /// Determines whether a level is enabled by the configured logging filters.
        /// </summary>
        /// <param name="logLevel">The event level.</param>
        /// <returns><see langword="true"/> for enabled levels.</returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <summary>
        /// Begins a no-op logging scope.
        /// </summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A disposable no-op scope.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => EmptyScope.Instance;

        /// <summary>
        /// Writes a log event through the owning provider.
        /// </summary>
        /// <typeparam name="TState">The log state type.</typeparam>
        /// <param name="logLevel">The event level.</param>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="state">The event state.</param>
        /// <param name="exception">The associated exception, if any.</param>
        /// <param name="formatter">The message formatter.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (IsEnabled(logLevel))
                provider.Write(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        /// <summary>Gets the shared no-op scope.</summary>
        public static EmptyScope Instance { get; } = new();

        /// <summary>Disposes the no-op scope.</summary>
        public void Dispose() { }
    }
}