namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class FileEditSessionTests
{
    [Fact]
    public void ApplyEdit_IncrementsRevisionAndMarksDirty()
    {
        var session = CreateSession();

        var changed = session.ApplyEdit(Bytes("changed"));

        Assert.Equal(1, changed.EditRevision);
        Assert.Equal(1, session.State.EditRevision);
        Assert.Equal(0, session.State.SavedEditRevision);
        Assert.True(session.State.IsDirty);
    }

    [Fact]
    public async Task BufferedFileSaveContent_OpenTwice_ReturnsIndependentStreams()
    {
        var content = new BufferedFileSaveContent(Bytes("abc"));

        await using var first = await content.OpenReadAsync();
        await using var second = await content.OpenReadAsync();
        _ = first.ReadByte();

        Assert.Equal((byte)'a', second.ReadByte());
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ApplyEdit_OverflowingRevision_Throws()
    {
        var session = new FileEditSession(
            new FileEditSnapshot(File(), long.MaxValue, Bytes("a")));

        Assert.Throws<OverflowException>(() => session.ApplyEdit(Bytes("b")));
    }

    internal static FileEditSession CreateSession(string value = "file")
        => new(
            new FileEditSnapshot(File(value), 0, Bytes("initial"), "text/plain", "utf-8"),
            new FileContentRevision("base-0"));

    internal static FileReference File(string value = "file") => new("test", value);

    internal static ReadOnlyMemory<byte> Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
