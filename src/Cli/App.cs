using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeatureShift;

public sealed class InputException(string message) : Exception(message);
public sealed record Stats(int Rows, double Mean, double PopulationStdDev, double Min, double Max);
public sealed record FeatureReport(string Name, Stats Reference, Stats Candidate, double MeanDifference, double? ReferenceStandardizedMeanDifference, int CandidateBelowReferenceMin, int CandidateAboveReferenceMax);
public sealed record Report(int SchemaVersion, string ReferenceSha256, string CandidateSha256, int ReferenceRows, int CandidateRows, int OverlappingIds, FeatureReport[] Features);
internal sealed record Dataset(string Hash, string[] Names, string[] Ids, double[][] Values);

public static class App
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--version"]) { output.WriteLine("0.1.0"); return 0; }
        if (args is ["--help"] or ["-h"]) { output.WriteLine("feature-shift --reference reference.jsonl --candidate candidate.jsonl\nRead-only descriptive numeric-feature comparison; JSON stdout. Exit 0 success, 2 invalid input/file. No training or statistical drift test."); return 0; }
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                if (key is not ("--reference" or "--candidate") || ++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(key, args[i])) throw new InputException("Pass --reference and --candidate exactly once.");
            }
            if (options.Count != 2) throw new InputException("Pass --reference and --candidate exactly once.");
            var report = Compare(options["--reference"], options["--candidate"]);
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
            return 0;
        }
        catch (InputException exception) { error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { error.WriteLine("Cannot read input or invalid Unicode/arguments; check paths and UTF-8 JSONL."); return 2; }
    }

    public static Report Compare(string referencePath, string candidatePath)
    {
        var reference = Read(referencePath, null);
        var candidate = Read(candidatePath, reference.Names);
        var referenceIds = reference.Ids.ToHashSet(StringComparer.Ordinal);
        var overlap = candidate.Ids.Count(referenceIds.Contains);
        var features = new List<FeatureReport>();
        for (var j = 0; j < reference.Names.Length; j++)
        {
            var referenceStats = Summarize(reference.Values, j);
            var candidateStats = Summarize(candidate.Values, j);
            var difference = candidateStats.Mean - referenceStats.Mean;
            double? normalized = referenceStats.PopulationStdDev == 0 ? null : difference / referenceStats.PopulationStdDev;
            if (!double.IsFinite(difference) || (normalized.HasValue && !double.IsFinite(normalized.Value))) throw new InputException("Nonfinite statistics; review numeric scale.");
            features.Add(new(reference.Names[j], referenceStats, candidateStats, difference, normalized,
                candidate.Values.Count(row => row[j] < referenceStats.Min), candidate.Values.Count(row => row[j] > referenceStats.Max)));
        }
        return new(1, reference.Hash, candidate.Hash, reference.Ids.Length, candidate.Ids.Length, overlap, features.ToArray());
    }

    private static Stats Summarize(double[][] rows, int column)
    {
        var count = 0; var mean = 0.0; var sumSquares = 0.0; var min = double.PositiveInfinity; var max = double.NegativeInfinity;
        foreach (var row in rows)
        {
            var x = row[column]; min = Math.Min(min, x); max = Math.Max(max, x); count++;
            var delta = x - mean; mean += delta / count; sumSquares += delta * (x - mean);
        }
        var std = Math.Sqrt(Math.Max(0, sumSquares / count));
        if (!double.IsFinite(mean) || !double.IsFinite(std)) throw new InputException("Nonfinite statistics; review numeric scale.");
        return new(count, mean, std, min, max);
    }

    private static Dataset Read(string path, string[]? expected)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        const int limit = 32 * 1024 * 1024;
        if (file.Length > limit) throw new InputException("Input exceeds 32 MiB.");
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = file.Read(buffer)) > 0) { if (memory.Length + read > limit) throw new InputException("Input exceeds 32 MiB."); memory.Write(buffer, 0, read); }
        var bytes = memory.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string text;
        try { var offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0; text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException) { throw new InputException("Input must be valid UTF-8."); }
        using var reader = new StringReader(text); string? line;
        var rows = new List<double[]>(); var ids = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal); var names = expected;
        while ((line = reader.ReadLine()) is not null)
        {
            var lineNumber = rows.Count + 1;
            if (rows.Count >= 50_000 || line.Length > 1024 * 1024) throw new InputException("Input exceeds the 50,000-row or 1 Mi-character line limit.");
            try
            {
                using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
                var root = document.RootElement; if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
                RejectDuplicates(root);
                if (!root.TryGetProperty("id", out var idNode) || idNode.ValueKind != JsonValueKind.String || idNode.GetString() is not { Length: > 0 and <= 256 } id || string.IsNullOrWhiteSpace(id) || id.Any(char.IsControl)) throw new JsonException();
                if (!seen.Add(id)) throw new InputException($"Duplicate ID at line {lineNumber}.");
                if (!root.TryGetProperty("features", out var objectNode) || objectNode.ValueKind != JsonValueKind.Object) throw new JsonException();
                var actual = objectNode.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
                if (actual.Length is < 1 or > 32 || actual.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 64 || n.Any(char.IsControl))) throw new JsonException();
                names ??= actual;
                if (!names.SequenceEqual(actual, StringComparer.Ordinal)) throw new InputException($"Feature names differ at line {lineNumber}; matching is exact and case-sensitive.");
                var values = new double[names.Length];
                for (var j = 0; j < names.Length; j++)
                {
                    var number = objectNode.GetProperty(names[j]);
                    if (number.ValueKind != JsonValueKind.Number || !number.TryGetDouble(out var value) || !double.IsFinite(value) || Math.Abs(value) > 1e12) throw new JsonException();
                    values[j] = value;
                }
                rows.Add(values); ids.Add(id);
            }
            catch (JsonException) { throw new InputException($"Invalid JSON, ID or numeric features at line {lineNumber}; duplicate properties are prohibited."); }
            catch (InvalidOperationException) { throw new InputException($"Invalid Unicode or JSON fields at line {lineNumber}."); }
        }
        if (rows.Count == 0) throw new InputException("Dataset is empty.");
        return new(hash, names!, ids.ToArray(), rows.ToArray());
    }

    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException(); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) RejectDuplicates(child);
    }
}
