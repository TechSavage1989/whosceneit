using System.Reflection;
using System.Text.Json.Nodes;
using MediaBrowser.Model.Serialization;
using WhoSceneIt.Retention;

public static class RetentionCompatibilityFixture
{
    // Frozen schema-1 data, including every rule setting from the published 1.5.1 model.
    public static string Read()
    {
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("WhoSceneIt.Tests.retention-v1.5.1.json");
        using var reader = new StreamReader(input ?? throw new Exception("The saved-rule compatibility fixture is missing."));
        return reader.ReadToEnd();
    }
    public static void Write(string data)
    {
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data,"retention.json"),Read());
        File.WriteAllText(Path.Combine(data,"retention.json.bak"),Read()+"\n");
    }
    public static void AssertState(IJsonSerializer json, object actual)
    {
        var expected = JsonNode.Parse(json.SerializeToString(json.DeserializeFromString<RetentionState>(Read())));
        if (!JsonNode.DeepEquals(expected,JsonNode.Parse(json.SerializeToString(actual))))
            throw new Exception("An existing rule, enabled state, schedule, trigger, revision, owner, protection, review, hold, or history entry changed during upgrade/reload.");
    }
    public static void AssertFiles(string data)
    {
        if (File.ReadAllText(Path.Combine(data,"retention.json"))!=Read() || File.ReadAllText(Path.Combine(data,"retention.json.bak"))!=Read()+"\n")
            throw new Exception("Update installation, rollback, or health cleanup changed existing rule files or their backup.");
    }
}
