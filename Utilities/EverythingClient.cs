using System.Runtime.InteropServices;
using System.Text;

namespace tool_r1ng.Utilities;

public static class EverythingClient
{
    private const uint EverythingRequestFileName = 0x00000001;
    private const uint EverythingRequestPath = 0x00000002;

    private static readonly object QueryLock = new();

    public static IReadOnlyList<EverythingSearchResult> Search(string query, int maxResults, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        lock (QueryLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Everything_SetSearchW(query);
            Everything_SetMatchPath(true);
            Everything_SetMatchCase(false);
            Everything_SetMatchWholeWord(false);
            Everything_SetRegex(false);
            Everything_SetMax(maxResults);
            Everything_SetOffset(0);
            Everything_SetRequestFlags(EverythingRequestFileName | EverythingRequestPath);

            if (!Everything_QueryW(true))
            {
                throw new EverythingUnavailableException(GetLastErrorMessage());
            }

            var results = new List<EverythingSearchResult>();
            var count = Math.Min(maxResults, (int)Everything_GetNumResults());
            for (uint index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = GetFullPath(index);
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    results.Add(new EverythingSearchResult(fullPath, Everything_IsFolderResult(index)));
                }
            }

            return results;
        }
    }

    public static bool IsAvailable()
    {
        return TryCheckAvailability(out _);
    }

    public static bool TryCheckAvailability(out string message)
    {
        try
        {
            _ = Search("test", 1, CancellationToken.None);
            message = "Everything is ready";
            return true;
        }
        catch (DllNotFoundException)
        {
            message = "Everything64.dll was not found beside the app executable";
            return false;
        }
        catch (BadImageFormatException)
        {
            message = "Everything64.dll architecture does not match this app";
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            message = "Everything64.dll is not a compatible SDK DLL";
            return false;
        }
        catch (EverythingUnavailableException exception)
        {
            message = exception.Message;
            return false;
        }
        catch
        {
            message = "Everything is not available";
            return false;
        }
    }

    private static string GetLastErrorMessage()
    {
        return Everything_GetLastError() switch
        {
            2 => "Everything IPC is unavailable. Start Everything or enable Everything Service",
            6 => "Everything returned an invalid result index",
            7 => "Everything SDK was called before a query completed",
            var error => $"Everything query failed with SDK error {error}"
        };
    }

    private static string GetFullPath(uint index)
    {
        var buffer = new StringBuilder(32768);
        _ = Everything_GetResultFullPathNameW(index, buffer, (uint)buffer.Capacity);
        return buffer.ToString();
    }

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetSearchW(string search);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchPath(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchCase(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchWholeWord(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetRegex(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMax(int max);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetOffset(int offset);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetRequestFlags(uint requestFlags);

    [DllImport("Everything64.dll")]
    private static extern bool Everything_QueryW(bool wait);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetResultFullPathNameW(uint index, StringBuilder buffer, uint bufferSize);

    [DllImport("Everything64.dll")]
    private static extern bool Everything_IsFolderResult(uint index);
}

public sealed record EverythingSearchResult(string FullPath, bool IsFolder);

public sealed class EverythingUnavailableException : Exception
{
    public EverythingUnavailableException(string message)
        : base(message)
    {
    }
}
