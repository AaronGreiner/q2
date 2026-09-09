using System.Security.Cryptography;
using System.Text;
using Q2.Api.Features.Notifications;

namespace Q2.Api.UnitTests.Notifications;

/// <summary>
/// The encryption, checked against the specification's own answer.
/// </summary>
/// <remarks>
/// This is the test that makes writing Web Push by hand defensible rather than
/// reckless. RFC 8291 section 5 publishes a worked example with every input and
/// every intermediate value fixed, so the implementation is measured against
/// the specification instead of against my reading of it.
///
/// It is also why <see cref="WebPushCrypto.Encrypt"/> takes the salt and the
/// sender key as parameters: without that, the one thing worth testing could
/// not be reproduced. Every caller outside this file passes fresh randomness.
///
/// **The check is a decryption, not a transcribed constant.** The RFC prints
/// the finished body as 186 characters of base64url; copying that into a source
/// file is one slip away from a test that fails for a reason having nothing to
/// do with the code — which is exactly what happened on the first attempt here.
/// So the receiver's half is written out below from the specification's own
/// steps, its derived values are asserted against the short intermediates the
/// RFC also publishes, and the implementation's output is then decrypted with
/// it. A bug in the implementation cannot cancel out, because the two halves
/// share no code.
/// </remarks>
public class WebPushCryptoTests
{
    /// <summary>The plaintext from RFC 8291 section 5.</summary>
    private const string Plaintext = "When I grow up, I want to be a watermelon";

    private const string ReceiverPublic =
        "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";

    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";

    private const string SenderPublic =
        "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";

    private const string SenderPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";

    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";

    private const string ReceiverPrivate = "q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94";

    /// <summary>The intermediate values RFC 8291 section 5 prints alongside the body.</summary>
    private const string ExpectedSharedSecret = "kyrL1jIIOHEzg3sM2ZWRHDRB62YACZhhSlknJ672kSs";

    private const string ExpectedIkm = "S4lYMb_L0FxCeq0WhDx813KgSYqU26kOyzWUdsXYyrg";

    private const string ExpectedContentKey = "oIhVW04MRdy2XN9CiKLxTg";

    private const string ExpectedNonce = "4h_95klXJ5E_qnoN";

    [Fact]
    public void ItProducesABodyTheSubscriptionCanRead()
    {
        using var sender = SenderKey();

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            WebPushCrypto.Decode(ReceiverPublic),
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.Decode(Salt),
            sender);

        Assert.Equal(Plaintext, Decrypt(body));
    }

    /// <summary>
    /// The receiver's half, written out from the specification, agrees with the
    /// numbers the specification prints for it.
    /// </summary>
    /// <remarks>
    /// Without this the decryption above would only prove that the two halves
    /// agree with each other — which they would even if both were wrong in the
    /// same way. These four values are what tie them to RFC 8291.
    /// </remarks>
    [Fact]
    public void TheReceiversHalfMatchesTheSpecification()
    {
        var derived = ReceiverKeys(WebPushCrypto.Decode(SenderPublic), WebPushCrypto.Decode(Salt));

        Assert.Equal(ExpectedSharedSecret, WebPushCrypto.Encode(derived.Shared));
        Assert.Equal(ExpectedIkm, WebPushCrypto.Encode(derived.Ikm));
        Assert.Equal(ExpectedContentKey, WebPushCrypto.Encode(derived.ContentKey));
        Assert.Equal(ExpectedNonce, WebPushCrypto.Encode(derived.Nonce));
    }

    /// <summary>
    /// The header the browser reads before it can decrypt anything: the salt,
    /// the record size, and the sender's key as the key id.
    /// </summary>
    [Fact]
    public void TheHeaderCarriesTheSaltAndTheSendersKey()
    {
        using var sender = SenderKey();

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            WebPushCrypto.Decode(ReceiverPublic),
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.Decode(Salt),
            sender);

        Assert.Equal(WebPushCrypto.Decode(Salt), body[..16]);
        Assert.Equal(
            (uint)WebPushCrypto.RecordSize,
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
        Assert.Equal(65, body[20]);
        Assert.Equal(WebPushCrypto.Decode(SenderPublic), body[21..86]);
    }

    /// <summary>
    /// A one-off key and a one-off salt, or the same plaintext would encrypt to
    /// the same bytes twice — which is what a nonce is for.
    /// </summary>
    [Fact]
    public void TwoMessagesNeverLookAlike()
    {
        using var first = WebPushCrypto.NewSenderKey();
        using var second = WebPushCrypto.NewSenderKey();

        var one = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            WebPushCrypto.Decode(ReceiverPublic),
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.NewSalt(),
            first);

        var two = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            WebPushCrypto.Decode(ReceiverPublic),
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.NewSalt(),
            second);

        Assert.NotEqual(WebPushCrypto.Encode(one), WebPushCrypto.Encode(two));
    }

    [Fact]
    public void APayloadThatWouldNotFitIsRefusedRatherThanTruncated()
    {
        using var sender = WebPushCrypto.NewSenderKey();

        Assert.Throws<ArgumentException>(() => WebPushCrypto.Encrypt(
            new byte[WebPushCrypto.MaxPayloadBytes + 1],
            WebPushCrypto.Decode(ReceiverPublic),
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.NewSalt(),
            sender));
    }

    [Fact]
    public void AKeyThatIsNotAPointIsRefused()
    {
        using var sender = WebPushCrypto.NewSenderKey();

        Assert.Throws<ArgumentException>(() => WebPushCrypto.Encrypt(
            "hello"u8,
            new byte[10],
            WebPushCrypto.Decode(AuthSecret),
            WebPushCrypto.NewSalt(),
            sender));
    }

    /// <summary>
    /// The token is a return address rather than a secret, so what matters is
    /// that it says the right three things and is signed in the format JWS
    /// wants — r||s, not DER.
    /// </summary>
    [Fact]
    public void TheVapidTokenNamesTheServiceItWasMintedFor()
    {
        var (publicKey, privateKey) = NewVapidPair();
        using var signing = WebPushCrypto.ImportSigningKey(publicKey, privateKey);

        var expires = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var header = WebPushCrypto.CreateAuthorization(
            "https://push.example",
            "mailto:hallo@q2.example",
            signing,
            publicKey,
            expires);

        Assert.StartsWith("vapid t=", header, StringComparison.Ordinal);
        Assert.Contains($", k={publicKey}", header, StringComparison.Ordinal);

        var token = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        var parts = token.Split('.');

        Assert.Equal(3, parts.Length);

        var claims = Encoding.UTF8.GetString(WebPushCrypto.Decode(parts[1]));

        Assert.Contains("\"Aud\":\"https://push.example\"", claims, StringComparison.Ordinal);
        Assert.Contains("\"Sub\":\"mailto:hallo@q2.example\"", claims, StringComparison.Ordinal);
        Assert.Contains("\"Exp\":1800000000", claims, StringComparison.Ordinal);

        // A DER signature here is what every push service rejects with an
        // unhelpful 401, so its length is worth asserting: 64 bytes is r||s.
        Assert.Equal(64, WebPushCrypto.Decode(parts[2]).Length);

        Assert.True(signing.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            WebPushCrypto.Decode(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void TheAudienceIsTheOriginAndNothingElse()
    {
        Assert.Equal(
            "https://fcm.googleapis.com",
            WebPushCrypto.AudienceOf(new Uri("https://fcm.googleapis.com/fcm/send/abc?x=1")));
    }

    /// <summary>
    /// The browser's side of RFC 8291, written out from the specification's own
    /// steps and sharing no code with the implementation under test.
    /// </summary>
    private static string Decrypt(byte[] body)
    {
        var salt = body[..16];
        var senderPublicKey = body[21..(21 + body[20])];
        var sealedRecord = body[(21 + body[20])..];

        var keys = ReceiverKeys(senderPublicKey, salt);

        var record = new byte[sealedRecord.Length - 16];

        using (var aes = new AesGcm(keys.ContentKey, 16))
        {
            aes.Decrypt(keys.Nonce, sealedRecord[..^16], sealedRecord[^16..], record);
        }

        // The last record ends with 0x02; anything before it is padding, which
        // this implementation never adds.
        var end = Array.LastIndexOf(record, (byte)0x02);
        return Encoding.UTF8.GetString(record[..end]);
    }

    private static (byte[] Shared, byte[] Ikm, byte[] ContentKey, byte[] Nonce) ReceiverKeys(
        byte[] senderPublicKey,
        byte[] salt)
    {
        var receiverPublicKey = WebPushCrypto.Decode(ReceiverPublic);

        using var receiver = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = receiverPublicKey[1..33], Y = receiverPublicKey[33..65] },
            D = WebPushCrypto.Decode(ReceiverPrivate),
        });

        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublicKey[1..33], Y = senderPublicKey[33..65] },
        });

        var shared = receiver.DeriveRawSecretAgreement(sender.PublicKey);

        var authInfo = "WebPush: info\0"u8.ToArray()
            .Concat(receiverPublicKey)
            .Concat(senderPublicKey)
            .ToArray();

        var ikm = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            shared,
            32,
            WebPushCrypto.Decode(AuthSecret),
            authInfo);

        return (
            shared,
            ikm,
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray()),
            HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray()));
    }

    /// <summary>The sender key pair from the RFC, so the vector can be reproduced.</summary>
    private static ECDiffieHellman SenderKey()
    {
        var point = WebPushCrypto.Decode(SenderPublic);

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
            D = WebPushCrypto.Decode(SenderPrivate),
        });
    }

    private static (string PublicKey, string PrivateKey) NewVapidPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);

        var point = new byte[65];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);

        return (WebPushCrypto.Encode(point), WebPushCrypto.Encode(parameters.D!));
    }
}
