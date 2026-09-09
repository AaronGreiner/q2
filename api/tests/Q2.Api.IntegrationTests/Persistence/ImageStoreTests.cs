using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Q2.Api.Features.Images;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// The file system store, against a real directory.
/// </summary>
/// <remarks>
/// Not a unit test, because there is nothing here but I/O: the whole class is
/// about what ends up on a disk. Substituting the file system would leave it
/// asserting that a fake behaves like a fake.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ImageStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"q2-image-store-test-{Guid.NewGuid():N}");

    private readonly FileSystemImageStore _store;

    public ImageStoreTests()
    {
        _store = new FileSystemImageStore(
            Options.Create(new ImageOptions { RootPath = _root }),
            new TestHostEnvironment());
    }

    [Fact]
    public async Task ReturnsWhatItWasGiven()
    {
        var id = Guid.NewGuid();
        var bytes = TestImages.Jpeg(64, 64, padding: 512);

        await _store.SaveAsync(id, bytes, TestContext.Current.CancellationToken);

        await using var stream = await _store.OpenAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(stream);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, buffer.ToArray());
    }

    [Fact]
    public async Task AnswersNullForSomethingItNeverStored()
    {
        Assert.Null(await _store.OpenAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SpreadsFilesOverSubdirectoriesRatherThanOneFlatOne()
    {
        // A single directory holding every picture in the product is the sort
        // of thing that works until it does not.
        foreach (var _ in Enumerable.Range(0, 24))
        {
            await _store.SaveAsync(Guid.NewGuid(), TestImages.Png(32, 32), TestContext.Current.CancellationToken);
        }

        Assert.True(Directory.GetDirectories(_root).Length > 1);
    }

    [Fact]
    public async Task StoresNothingWithAnExtensionAnythingWouldServe()
    {
        // The media type is a column, read out of the bytes. A ".jpg" on disk
        // would be an invitation to point a static file handler at the folder,
        // which is precisely the access-control bypass this design avoids.
        await _store.SaveAsync(Guid.NewGuid(), TestImages.Jpeg(32, 32), TestContext.Current.CancellationToken);

        Assert.All(
            Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories),
            file => Assert.Equal(".bin", Path.GetExtension(file)));
    }

    [Fact]
    public async Task ReplacesTheBytesOfAnIdItAlreadyHas()
    {
        var id = Guid.NewGuid();

        await _store.SaveAsync(id, TestImages.Png(32, 32), TestContext.Current.CancellationToken);

        var replacement = TestImages.Png(64, 64, padding: 100);
        await _store.SaveAsync(id, replacement, TestContext.Current.CancellationToken);

        await using var stream = await _store.OpenAsync(id, TestContext.Current.CancellationToken);
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        Assert.Equal(replacement, buffer.ToArray());
    }

    [Fact]
    public async Task LeavesNoTemporaryFilesBehind()
    {
        // The write goes to a neighbour and is moved into place, so a crash
        // cannot leave half a photograph readable. What must not survive a
        // *successful* write is the neighbour.
        await _store.SaveAsync(Guid.NewGuid(), TestImages.Jpeg(32, 32), TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DeletesWhatItHasAndShrugsAtWhatItDoesNot()
    {
        var id = Guid.NewGuid();
        await _store.SaveAsync(id, TestImages.Jpeg(32, 32), TestContext.Current.CancellationToken);

        await _store.DeleteAsync(id, TestContext.Current.CancellationToken);
        Assert.Null(await _store.OpenAsync(id, TestContext.Current.CancellationToken));

        // Idempotent: a delete that already half-happened has to be able to
        // finish rather than throw its way out of the transaction.
        await _store.DeleteAsync(id, TestContext.Current.CancellationToken);
        await _store.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Only reached when no root path is configured, which these tests never
    /// do — the store resolves its default against the content root.
    /// </summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "AutomatedTest";

        public string ApplicationName { get; set; } = "Q2.Api.IntegrationTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
