using System.Text;
using System.Text.Json;
using FeatureShift;

namespace Tests;

public sealed class ShiftTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "feature-shift-" + Guid.NewGuid().ToString("N"));
    public ShiftTests() => Directory.CreateDirectory(root);
    private string Data(params string[] rows) { var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl"); File.WriteAllText(path, string.Join('\n', rows) + "\n", new UTF8Encoding(false)); return path; }
    private static string Row(string id, double x, double c = 7) => JsonSerializer.Serialize(new { id, features = new { x, constant = c }, metadata = new { text = "private-body" } });
    private string Reference() => Data(Row("id-1", 1), Row("id-2", 3));
    private string Candidate() => Data(Row("id-2", 3), Row("id-3", 5));
    private static (int Code, string Output, string Error) Run(params string[] args) { using var output = new StringWriter(); using var error = new StringWriter(); var code = App.Run(args, output, error); return (code, output.ToString(), error.ToString()); }

    [Fact]
    public void HandComputedStatisticsAndOverlapPreserveInputs()
    {
        var reference = Reference(); var candidate = Candidate(); var a = File.ReadAllBytes(reference); var b = File.ReadAllBytes(candidate);
        var report = App.Compare(reference, candidate);
        Assert.Equal(2, report.ReferenceRows); Assert.Equal(2, report.CandidateRows); Assert.Equal(1, report.OverlappingIds);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(a)).ToLowerInvariant(), report.ReferenceSha256);
        var x = Assert.Single(report.Features, f => f.Name == "x");
        Assert.Equal(2, x.Reference.Mean, 12); Assert.Equal(1, x.Reference.PopulationStdDev, 12);
        Assert.Equal(4, x.Candidate.Mean, 12); Assert.Equal(1, x.Candidate.PopulationStdDev, 12);
        Assert.Equal(2, x.MeanDifference, 12); Assert.Equal(2, x.ReferenceStandardizedMeanDifference!.Value, 12);
        Assert.Equal(0, x.CandidateBelowReferenceMin); Assert.Equal(1, x.CandidateAboveReferenceMax);
        var constant = Assert.Single(report.Features, f => f.Name == "constant");
        Assert.Equal(0, constant.Reference.PopulationStdDev); Assert.Null(constant.ReferenceStandardizedMeanDifference);
        Assert.Equal(a, File.ReadAllBytes(reference)); Assert.Equal(b, File.ReadAllBytes(candidate));
    }

    [Fact]
    public void CliOutputsNoIdsOrRawMetadataAndCreatesNoFiles()
    {
        var a = Reference(); var b = Candidate(); var files = Directory.GetFileSystemEntries(root).Order().ToArray();
        var result = Run("--reference", a, "--candidate", b);
        Assert.Equal(0, result.Code); Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output); Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.DoesNotContain("id-1", result.Output); Assert.DoesNotContain("private-body", result.Output); Assert.DoesNotContain(Path.GetFileName(a), result.Output);
        Assert.Equal(files, Directory.GetFileSystemEntries(root).Order().ToArray());
    }

    [Fact]
    public void BoundaryValuesEqualToReferenceRangeAreNotCountedOutside()
    {
        var report = App.Compare(Reference(), Data(Row("a", 1), Row("b", 3), Row("c", 0), Row("d", 4)));
        var x = Assert.Single(report.Features, f => f.Name == "x"); Assert.Equal(1, x.CandidateBelowReferenceMin); Assert.Equal(1, x.CandidateAboveReferenceMax);
    }

    [Fact]
    public void ExactNamesIgnoreObjectOrderButNotCase()
    {
        var reference = Reference();
        var reordered = Data("{\"id\":\"a\",\"features\":{\"constant\":7,\"x\":2}}");
        Assert.Equal(1, App.Compare(reference, reordered).CandidateRows);
        var mismatch = Data("{\"id\":\"a\",\"features\":{\"constant\":7,\"X\":2}}");
        var result = Run("--reference", reference, "--candidate", mismatch);
        Assert.Equal(2, result.Code); Assert.Empty(result.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"a\",\"features\":{}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":null}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":\"1\"}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":1e999}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":1000000000001}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":1,\"x\":2}}")]
    [InlineData("{\"id\":\"a\",\"features\":{\"x\":1},\"meta\":{\"a\":1,\"a\":2}}")]
    [InlineData("{\"id\":\"\\ud800\",\"features\":{\"x\":1}}")]
    [InlineData("[1]")]
    public void InvalidRowsProduceNoPartialReport(string row)
    {
        var result = Run("--reference", Data(row), "--candidate", Candidate());
        Assert.Equal(2, result.Code); Assert.Empty(result.Output); Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void DuplicateIdsAreRejectedWithinEachDataset()
    {
        var repeated = Data(Row("same", 1), Row("same", 2));
        Assert.Equal(2, Run("--reference", Reference(), "--candidate", repeated).Code);
        Assert.Equal(2, Run("--reference", repeated, "--candidate", Candidate()).Code);
    }

    [Fact]
    public void Utf8BomHashAndBoundsAreExplicit()
    {
        var reference = Reference(); var bytes = File.ReadAllBytes(reference); File.WriteAllBytes(reference, [0xef, 0xbb, 0xbf, .. bytes]);
        var report = App.Compare(reference, Candidate());
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(reference))).ToLowerInvariant(), report.ReferenceSha256);
        var invalid = Data(); File.WriteAllBytes(invalid, [0xff]); Assert.Equal(2, Run("--reference", invalid, "--candidate", Candidate()).Code);
        using (var stream = new FileStream(invalid, FileMode.Create)) stream.SetLength(32L * 1024 * 1024 + 1);
        Assert.Equal(2, Run("--reference", invalid, "--candidate", Candidate()).Code);
    }

    [Fact]
    public void SameDatasetHasZeroShiftAndFullOverlap()
    {
        var input = Reference(); var report = App.Compare(input, input);
        Assert.Equal(2, report.OverlappingIds); Assert.All(report.Features, f => Assert.Equal(0, f.MeanDifference));
    }

    [Theory]
    [InlineData("")]
    [InlineData("--reference")]
    [InlineData("--unknown a")]
    [InlineData("--reference a --reference b")]
    public void InvalidOptionsReturnTwo(string arguments)
    {
        var result = Run(arguments.Length == 0 ? [] : arguments.Split(' ')); Assert.Equal(2, result.Code); Assert.Empty(result.Output);
    }
    public void Dispose() => Directory.Delete(root, true);
}
