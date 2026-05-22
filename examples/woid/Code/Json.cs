using System.Text.Json;

namespace Woid;

/// <summary>Shared JSON serializer settings. snake_case-friendly via JsonPropertyName attrs.</summary>
public static class Json
{
	public static readonly JsonSerializerOptions Opts = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false,
	};
}
