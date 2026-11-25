// SPDX-License-Identifier: MIT
namespace Kilnwarden.Http;

/// <summary>A request the server refuses before any handler runs (bad syntax, too large, too slow...). The connection
/// answers <see cref="Status"/> with a short text and closes.</summary>
public sealed class HttpError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
