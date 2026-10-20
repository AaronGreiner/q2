using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A person's data, exported (#36): every table that names them is placed in
/// the export or left out for a written reason, and the export carries their
/// share of it — their pictures included, nobody else's.
/// </summary>
[Trait("Category", "Integration")]
public class DataExportTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private const string Export = "/api/auth/account/export";

    private sealed record Archive(JsonElement Data, IReadOnlyList<string> Files, HttpResponseMessage Response);

    private static async Task<Archive> ExportAsync(HttpClient client)
    {
        var response = await client.GetAsync(Export, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        await using var json = zip.GetEntry("data.json")!.Open();
        var data = (await JsonDocument.ParseAsync(json, cancellationToken: TestContext.Current.CancellationToken)).RootElement.Clone();

        return new Archive(data, [.. zip.Entries.Select(entry => entry.FullName)], response);
    }

    /// <summary>
    /// The inventory is the model's: every property that is a foreign key to a
    /// person or ends in <c>PersonId</c>, and nothing else.
    /// </summary>
    [Fact]
    public async Task EveryColumnThatNamesAPersonIsInTheInventory()
    {
        var model = new HashSet<string>();

        await Factory.WithDatabaseAsync(database =>
        {
            foreach (var entity in database.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.GetContainingForeignKeys().Any(key => key.PrincipalEntityType.ClrType == typeof(Person))
                        || property.Name.EndsWith("PersonId", StringComparison.Ordinal))
                    {
                        model.Add($"{entity.ClrType.Name}.{property.Name}");
                    }
                }
            }

            return Task.CompletedTask;
        });

        // The person's own row is the one column the model cannot point at.
        model.Add("Person.Id");

        var inventory = PersonalData.Columns.Select(column => $"{column.Entity}.{column.Property}").ToHashSet();

        Assert.Empty(model.Except(inventory));
        Assert.Empty(inventory.Except(model));
        Assert.All(PersonalData.Columns, column => Assert.True(
            column.Section is not null || !string.IsNullOrWhiteSpace(column.LeftOutBecause),
            $"{column.Entity}.{column.Property} is left out without a reason."));
    }

    [Fact]
    public async Task TheExportIsAZipThatIsNeverCachedAndHasEverySection()
    {
        var archive = await ExportAsync(Client);

        Assert.Equal("application/zip", archive.Response.Content.Headers.ContentType?.MediaType);
        Assert.Matches(@"^qdos-\d{4}-\d{2}-\d{2}\.zip$", archive.Response.Content.Headers.ContentDisposition?.FileNameStar
            ?? archive.Response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains("no-store", archive.Response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        Assert.All(PersonalData.Sections, section => Assert.True(
            archive.Data.TryGetProperty(section, out _), $"The export has no \"{section}\" section."));
    }

    [Fact]
    public async Task ItCarriesThePersonsOwnDataAndTheirOwnPictures()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);

        var archive = await ExportAsync(Client);
        var data = archive.Data;

        Assert.Equal(AutomatedTestSeed.CurrentPersonId, data.GetProperty("person").GetProperty("id").GetGuid());
        Assert.Equal(SeedAccounts.EmailFor("@test.one"), data.GetProperty("account").GetProperty("email").GetString());
        Assert.Contains(data.GetProperty("friendships").EnumerateArray(), f => f.GetProperty("with").GetGuid() == AutomatedTestSeed.FriendPersonId);
        Assert.Contains(data.GetProperty("people").EnumerateArray(), p => p.GetProperty("id").GetGuid() == AutomatedTestSeed.FriendPersonId);

        // The photograph just delivered: in its window, and its file packed.
        var photograph = data.GetProperty("goals").EnumerateArray()
            .SelectMany(goal => goal.GetProperty("windows").EnumerateArray())
            .SelectMany(window => window.GetProperty("photographs").EnumerateArray())
            .Single(entry => entry.GetProperty("id").GetGuid() == proof.Id);
        var file = photograph.GetProperty("image").GetString()!;

        Assert.StartsWith("images/", file, StringComparison.Ordinal);
        Assert.Contains(file, archive.Files);
        Assert.Contains(data.GetProperty("images").EnumerateArray(), image => image.GetProperty("file").GetString() == file);
    }

    [Fact]
    public async Task ItCarriesNoCredentialAndNothingOfAnybodyElses()
    {
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);
        const string theirs = "Automated test: a line of the friend's own";
        (await friend.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", new { text = theirs }))
            .EnsureSuccessStatusCode();

        var mine = "Automated test: a line of mine";
        (await Client.PostJsonAsync($"/api/chats/{AutomatedTestSeed.DirectConversationId}/messages", new { text = mine }))
            .EnsureSuccessStatusCode();

        var archive = await ExportAsync(Client);
        var text = archive.Data.GetRawText();

        Assert.Contains(mine, text, StringComparison.Ordinal);
        Assert.DoesNotContain(theirs, text, StringComparison.Ordinal);

        foreach (var secret in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "authSecret", "publicKey", "endpoint" })
        {
            Assert.DoesNotContain($"\"{secret}\"", text, StringComparison.OrdinalIgnoreCase);
        }

        // Only pictures that are the person's own.
        var ownImages = new HashSet<string>();
        await Factory.WithDatabaseAsync(async database =>
            ownImages.UnionWith((await database.Images
                    .Where(image => image.OwnerPersonId == AutomatedTestSeed.CurrentPersonId)
                    .Select(image => image.Id)
                    .ToListAsync(TestContext.Current.CancellationToken))
                .Select(id => id.ToString("N"))));

        Assert.All(
            archive.Files.Where(name => name.StartsWith("images/", StringComparison.Ordinal)),
            name => Assert.Contains(Path.GetFileNameWithoutExtension(name), ownImages));
    }

    [Fact]
    public async Task NobodyIsExportedWithoutASession()
    {
        var response = await AnonymousClient.GetAsync(Export, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
