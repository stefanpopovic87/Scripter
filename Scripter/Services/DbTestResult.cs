namespace Scripter.Services;

public sealed record DbTestResult(
	bool Success,
	string? Server,
	string? Database,
	string? Version,
	long ElapsedMs,
	Exception? Error);