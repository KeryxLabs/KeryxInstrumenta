using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models.Git;
using Dahomey.Cbor.Util;
using Microsoft.Extensions.Logging;

public class GitClient
{
    public const string ServiceName = "GitClient";

    private const byte _pipeSeparator = (byte)'|';
    private readonly ConcurrentDictionary<string, int> _emailIntern = new();
    private int _emailCounter = 0;
    private readonly DateTime _ninetyDaysAgo = DateTime.Now.AddDays(-90);
    private readonly ConcurrentDictionary<string, FileAccumulator> _collectedFiles = new();

    private readonly ILogger<GitClient> _logger;

    private readonly AdaptiveContextInstrumentation _instrumentation;

    public GitClient(ILogger<GitClient> logger, AdaptiveContextInstrumentation instrumentation)
    {
        _logger = logger;
        _instrumentation = instrumentation;
    }

    public async Task<Dictionary<string, GitHistory>?> ExtractHistory(
        string repoPath,
        string? fileName = null,
        CancellationToken ct = default
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(ExtractHistory));
        activity?.SetTag("git.history.repo", repoPath);

        var args = fileName is null
            ? GitCLI.PerRepoLogSearch
            : $"{GitCLI.PerFileLogSearch} {fileName}";
        var processStartInfo = new ProcessStartInfo
        {
            FileName = GitCLI.Git,
            Arguments = args,
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();

            var output = process.StandardOutput.BaseStream;

            var reader = PipeReader.Create(output);
            await ReadPipeAsync(reader, ct);

            var error = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Git exited with code {ExitCode}: {Error}",
                    process.ExitCode,
                    error
                );
                return null;
            }

            return _collectedFiles.Select(Extract).ToDictionary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run git on {RepoPath}", repoPath);
            activity?.AddEvent(
                new("git.history.exception", tags: new() { ["exception"] = ex.Message })
            );
            return null;
        }
    }

    private KeyValuePair<string, GitHistory> Extract(
        KeyValuePair<string, FileAccumulator> keyValuePair
    )
    {
        var (fileName, acc) = keyValuePair;
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(ExtractHistory));
        activity?.SetTag("history.file", acc.FileName);

        var contributorsCount = acc.Contributors.Count();
        var history = new GitHistory()
        {
            Created = (DateTime)acc.Created!,
            LastModified = (DateTime)acc.LastModified!,
            TotalCommits = acc.TotalCommits,
            Contributors = contributorsCount,
            AvgDaysBetweenChanges =
                acc.TotalCommits > 1 ? acc.TotalDaysBetween / (acc.TotalCommits - 1) : 0,
            RecentFrequency = acc.RecentCommits switch
            {
                0 or 1 or 2 => "low",
                <= 9 => "medium",
                _ => "high",
            },
        };

        activity?.SetTag("history.contributors", contributorsCount);
        activity?.SetTag("history.commits.recent", acc.RecentCommits);
        activity?.SetTag("history.commits.frequency", history.RecentFrequency);

        return new(fileName, history);
    }

    async Task ReadPipeAsync(PipeReader reader, CancellationToken ct)
    {
        int? currentId = null;
        DateTime? currentDate = null;

        while (!ct.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(ct);
            ReadOnlySequence<byte> buffer = result.Buffer;

            // Use SequenceReader to manage the buffer cursor safely
            SequenceReader<byte> sequenceReader = new(buffer);

            // TryReadTo moves the reader position past the delimiter (\n) automatically
            while (sequenceReader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
            {
                if (IsLineEmpty(ref line))
                    continue;

                // Check for the pipe separator within the current line
                if (line.PositionOf(_pipeSeparator) != null)
                {
                    var dateRes = ProcessDateAndEmail(ref line);
                    if (dateRes != null)
                    {
                        (currentId, currentDate) = (
                            dateRes.Value.emailId,
                            dateRes.Value.commitDate
                        );
                    }
                }
                else if (currentId.HasValue && currentDate.HasValue)
                {
                    ProcessFileEntry(line, currentId.Value, currentDate.Value);
                }
            }

            // Advance the PipeReader to the current position of our SequenceReader
            reader.AdvanceTo(sequenceReader.Position, buffer.End);

            if (result.IsCompleted)
                break;
        }

        await reader.CompleteAsync();
    }

    // async Task ReadPipeAsync(PipeReader reader, CancellationToken ct)
    // {
    //     int? currentId = null;
    //     DateTime? currentDate = null;
    //     while (true)
    //     {
    //         ReadResult result = await reader.ReadAsync();
    //         ReadOnlySequence<byte> buffer = result.Buffer;

    //         while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
    //         {
    //             // Process the line.

    //             var firstPipe = line.PositionOf(_pipeSeparator)?.GetInteger();

    //             if (firstPipe is not null)
    //             {
    //                 var dateRes = ProcessDateAndEmail(ref line);
    //                 if (dateRes is null)
    //                 {
    //                     break;
    //                 }
    //                 (currentId, currentDate) = (dateRes.Value.emailId, dateRes.Value.commitDate);
    //             }
    //             else
    //             {
    //                 var lookup = _collectedFiles.GetAlternateLookup<ReadOnlySpan<char>>();

    //                 Span<char> keyBuffer = stackalloc char[(int)line.Length];

    //                 int charsWritten = Encoding.UTF8.GetChars(line, keyBuffer);

    //                 ReadOnlySpan<char> lookupKey = keyBuffer[..charsWritten];

    //                 if (!lookup.TryGetValue(lookupKey, out var accumulator))
    //                 {
    //                     var fileName = Encoding.UTF8.GetString(line);
    //                     _logger.LogInformation("FileName: {fileName}", fileName);
    //                     _collectedFiles[fileName] = accumulator = new() { FileName = fileName };
    //                 }

    //                 var date = (DateTime)currentDate!;
    //                 accumulator.Contributors.Add((int)currentId!);
    //                 accumulator.Created ??= date;

    //                 accumulator.TotalCommits++;
    //                 if (accumulator.PrevCommitDate.HasValue)
    //                     accumulator.TotalDaysBetween += (
    //                         date - accumulator.PrevCommitDate.Value
    //                     ).TotalDays;

    //                 accumulator.PrevCommitDate = accumulator.LastModified;
    //                 accumulator.LastModified = date;

    //                 if (date >= _ninetyDaysAgo)
    //                     accumulator.RecentCommits++;
    //             }
    //         }

    //         // Tell the PipeReader how much of the buffer has been consumed.
    //         reader.AdvanceTo(buffer.Start, buffer.End);

    //         // Stop reading if there's no more data coming.
    //         if (result.IsCompleted)
    //         {
    //             break;
    //         }
    //     }

    //     // Mark the PipeReader as complete.
    //     await reader.CompleteAsync();
    // }

    bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        // Look for a EOL in the buffer.
        SequencePosition? position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        // Skip the line + the \n.
        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    // private (int emailId, DateTime commitDate)? ProcessDateAndEmail(
    //     ref ReadOnlySequence<byte> line,
    //     int firstPipe
    // )
    // {
    //     if (line.Length <= firstPipe)
    //     {
    //         _logger.LogInformation(
    //             "We are equal here... for {line} we have {firstPipe} with {lineLength}",
    //             Encoding.UTF8.GetString(line),
    //             firstPipe,
    //             line
    //         );
    //     }
    //     var secondPipe = line.Slice(firstPipe + 1).PositionOf(_pipeSeparator)?.GetInteger();

    //     if (secondPipe is null)
    //         return null;

    //     var date = line.Slice(firstPipe + 1, secondPipe.Value - firstPipe - 1);
    //     var email = line.Slice(secondPipe.Value + 1);

    //     var lookup = _emailIntern.GetAlternateLookup<ReadOnlySpan<char>>();

    //     Span<char> keyBuffer = stackalloc char[(int)line.Length];

    //     int charsWritten = Encoding.UTF8.GetChars(line, keyBuffer);

    //     ReadOnlySpan<char> lookupKey = keyBuffer[..charsWritten];

    //     if (!lookup.TryGetValue(lookupKey, out var id))
    //     {
    //         var emailStr = Encoding.UTF8.GetString(email);
    //         _logger.LogInformation("Email: {emailStr}", emailStr);
    //         _emailIntern[emailStr] = id = Interlocked.Increment(ref _emailCounter);
    //     }
    //     var dateStr = Encoding.UTF8.GetString(date);
    //     return (id, DateTime.Parse(dateStr));
    // }

    private (int emailId, DateTime commitDate)? ProcessDateAndEmail(ref ReadOnlySequence<byte> line)
    {
        var reader = new SequenceReader<byte>(line);

        // Read up to the first pipe (ID/Prefix)
        if (!reader.TryReadTo(out ReadOnlySequence<byte> _, _pipeSeparator))
            return null;

        // Read up to the second pipe (Date)
        if (!reader.TryReadTo(out ReadOnlySequence<byte> dateSeq, _pipeSeparator))
            return null;

        // The rest is the email
        ReadOnlySequence<byte> emailSeq = reader.UnreadSequence;

        try
        {
            string dateStr = Encoding.UTF8.GetString(dateSeq);

            var lookup = _emailIntern.GetAlternateLookup<ReadOnlySpan<char>>();

            Span<char> keyBuffer = stackalloc char[(int)line.Length];

            int charsWritten = Encoding.UTF8.GetChars(line, keyBuffer);

            ReadOnlySpan<char> lookupKey = keyBuffer[..charsWritten];
            if (!lookup.TryGetValue(lookupKey, out var id))
            {
                var emailStr = Encoding.UTF8.GetString(emailSeq);
                _logger.LogInformation("Email: {emailStr}", emailStr);
                _emailIntern[emailStr] = id = Interlocked.Increment(ref _emailCounter);
            }
            return (id, DateTime.Parse(dateStr));
        }
        catch
        {
            return null;
        }
    }

    private void ProcessFileEntry(ReadOnlySequence<byte> line, int currentId, DateTime currentDate)
    {
        // Use the alternate lookup with Span to avoid extra string allocations
        var lookup = _collectedFiles.GetAlternateLookup<ReadOnlySpan<char>>();

        // Safety check: only stackalloc for reasonable sizes
        char[]? rented = null;
        Span<char> keyBuffer =
            line.Length < 512
                ? stackalloc char[(int)line.Length]
                : (rented = ArrayPool<char>.Shared.Rent((int)line.Length));

        try
        {
            int charsWritten = Encoding.UTF8.GetChars(line, keyBuffer);
            ReadOnlySpan<char> lookupKey = keyBuffer[..charsWritten];

            if (!lookup.TryGetValue(lookupKey, out var accumulator))
            {
                var fileName = Encoding.UTF8.GetString(line).Trim();
                _collectedFiles[fileName] = accumulator = new() { FileName = fileName };
                _logger.LogInformation("FileName: {fileName}", fileName);
            }

            var date = (DateTime)currentDate!;
            accumulator.Contributors.Add((int)currentId!);
            accumulator.Created ??= date;

            accumulator.TotalCommits++;
            if (accumulator.PrevCommitDate.HasValue)
                accumulator.TotalDaysBetween += (date - accumulator.PrevCommitDate.Value).TotalDays;

            accumulator.PrevCommitDate = accumulator.LastModified;
            accumulator.LastModified = date;

            if (date >= _ninetyDaysAgo)
                accumulator.RecentCommits++;
        }
        finally
        {
            if (rented != null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    private bool IsLineEmpty(ref ReadOnlySequence<byte> line)
    {
        if (line.IsEmpty)
        {
            return true;
        }

        if (line.Length == 1)
        {
            byte firstByte = line.First.Span[0];
            if (firstByte == (byte)'\r')
            {
                return true;
            }
        }
        return false;
    }

    private record FileAccumulator
    {
        public string FileName { get; set; } = null!;
        public DateTime? Created { get; set; } // first commit seen
        public DateTime? LastModified { get; set; } // last commit seen (keep overwriting)
        public int TotalCommits { get; set; }
        public HashSet<int> Contributors { get; } = [];
        public DateTime? PrevCommitDate { get; set; } // for rolling avg
        public double TotalDaysBetween { get; set; } // accumulate as you go
        public int RecentCommits { get; set; } // within 90d window
    }
}
