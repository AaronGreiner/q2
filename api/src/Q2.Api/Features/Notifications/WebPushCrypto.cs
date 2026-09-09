using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// The two RFCs that make a push message.
/// </summary>
/// <remarks>
/// **Written out rather than taken from a package**, and that is a deliberate
/// choice in a repository whose dependency list is four lines long. Web Push is
/// two specifications on primitives .NET already ships —
/// <see cref="ECDiffieHellman"/>, <see cref="HKDF"/>, <see cref="AesGcm"/> and
/// <see cref="ECDsa"/> — and RFC 8291 publishes a worked example with every
/// intermediate value in it. That test vector is the reason this is safe to
/// write by hand: the implementation is checked against the specification's own
/// answer rather than against my reading of it (<c>WebPushCryptoTests</c>).
///
/// Two things are happening, and they are unrelated to each other:
///
/// - **RFC 8291** encrypts the payload *to the browser*. The keys come from the
///   subscription, so the push service — Google's, Mozilla's, Apple's — carries
///   bytes it cannot read. This is what makes it acceptable to put a goal title
///   in a notification at all.
/// - **RFC 8292 (VAPID)** signs a token identifying *this server* to the push
///   service. It is not secrecy, it is a return address: it is what lets a push
///   service rate-limit or contact whoever is sending.
/// </remarks>
public static class WebPushCrypto
{
    /// <summary>The record size this sends. One record, so it is also the maximum.</summary>
    /// <remarks>
    /// 4096 is what the specification uses in its example and what every push
    /// service accepts. q2's payloads are a few hundred bytes, so there is never
    /// a second record — <see cref="Encrypt"/> would have to grow a loop before
    /// this could be lowered.
    /// </remarks>
    public const int RecordSize = 4096;

    /// <summary>The longest payload that fits in one record, after padding and the tag.</summary>
    public const int MaxPayloadBytes = RecordSize - 17 - 16;

    private static readonly byte[] WebPushInfo = "WebPush: info\0"u8.ToArray();
    private static readonly byte[] KeyInfo = "Content-Encoding: aes128gcm\0"u8.ToArray();
    private static readonly byte[] NonceInfo = "Content-Encoding: nonce\0"u8.ToArray();

    /// <summary>
    /// Encrypts one payload for one subscription, per RFC 8291.
    /// </summary>
    /// <param name="salt">
    /// Sixteen random bytes. A parameter rather than generated inside, so the
    /// specification's test vector can be reproduced exactly — every other
    /// caller passes fresh randomness.
    /// </param>
    /// <param name="senderKey">
    /// The one-off key pair this message is sent under. Also a parameter for
    /// the test vector's sake; it must never be reused between messages, which
    /// is what <see cref="NewSenderKey"/> is for.
    /// </param>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> receiverPublicKey,
        ReadOnlySpan<byte> authSecret,
        ReadOnlySpan<byte> salt,
        ECDiffieHellman senderKey)
    {
        ArgumentNullException.ThrowIfNull(senderKey);

        if (payload.Length > MaxPayloadBytes)
        {
            throw new ArgumentException(
                $"A push payload is at most {MaxPayloadBytes} bytes.",
                nameof(payload));
        }

        if (salt.Length != 16)
        {
            throw new ArgumentException("A push salt is 16 bytes.", nameof(salt));
        }

        var senderPublicKey = UncompressedPoint(senderKey.PublicKey.ExportParameters());

        using var receiver = ImportPublicKey(receiverPublicKey);

        // The raw X coordinate, not a hashed agreement: RFC 8291 derives its own
        // key material from it and .NET's default DeriveKeyMaterial would have
        // already run a KDF over it.
        var shared = senderKey.DeriveRawSecretAgreement(receiver.PublicKey);

        /*
         * Two HKDF passes, and the order matters.
         *
         * The first mixes the subscription's auth secret into the ECDH result,
         * which is what binds the message to this subscription rather than
         * merely to this key. The second is plain RFC 8188 content encoding.
         */
        var authInfo = new byte[WebPushInfo.Length + receiverPublicKey.Length + senderPublicKey.Length];
        WebPushInfo.CopyTo(authInfo, 0);
        receiverPublicKey.CopyTo(authInfo.AsSpan(WebPushInfo.Length));
        senderPublicKey.CopyTo(authInfo.AsSpan(WebPushInfo.Length + receiverPublicKey.Length));

        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret.ToArray(), authInfo);

        var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt.ToArray(), KeyInfo);
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt.ToArray(), NonceInfo);

        // One record, so the delimiter is 0x02 ("last") rather than 0x01.
        var record = new byte[payload.Length + 1];
        payload.CopyTo(record);
        record[^1] = 0x02;

        var ciphertext = new byte[record.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(contentKey, tag.Length))
        {
            aes.Encrypt(nonce, record, ciphertext, tag);
        }

        /*
         * The aes128gcm header, per RFC 8188 section 2:
         *   salt (16) | record size (4, big endian) | key id length (1) | key id
         *
         * The key id is the sender's public key, which is how the browser knows
         * which key to run its half of the exchange against.
         */
        var body = new byte[16 + 4 + 1 + senderPublicKey.Length + ciphertext.Length + tag.Length];
        var cursor = body.AsSpan();

        salt.CopyTo(cursor);
        cursor = cursor[16..];

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(cursor, RecordSize);
        cursor = cursor[4..];

        cursor[0] = (byte)senderPublicKey.Length;
        cursor = cursor[1..];

        senderPublicKey.CopyTo(cursor);
        cursor = cursor[senderPublicKey.Length..];

        ciphertext.CopyTo(cursor);
        tag.CopyTo(cursor[ciphertext.Length..]);

        return body;
    }

    /// <summary>A fresh one-off key pair for exactly one message.</summary>
    public static ECDiffieHellman NewSenderKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>Sixteen random bytes for exactly one message.</summary>
    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(16);

    /// <summary>
    /// The VAPID <c>Authorization</c> header value for one push service.
    /// </summary>
    /// <param name="audience">
    /// The push service's origin — scheme and host of the endpoint, nothing
    /// more. A token minted for one service is not valid at another, which is
    /// the point of the claim.
    /// </param>
    /// <param name="subject">
    /// How to reach whoever runs this server: a <c>mailto:</c> or an https URL.
    /// It is not a secret and not a credential; it is the return address a push
    /// service uses when something is wrong with what we are sending.
    /// </param>
    public static string CreateAuthorization(
        string audience,
        string subject,
        ECDsa signingKey,
        string publicKey,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(signingKey);

        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new VapidHeader("JWT", "ES256")));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(
            new VapidClaims(audience, expiresAt.ToUnixTimeSeconds(), subject)));

        var signingInput = Encoding.ASCII.GetBytes($"{header}.{body}");

        // IeeeP1363 is r||s, which is what JWS ES256 wants. The .NET default is
        // DER, and a DER signature here is rejected by every push service with
        // an unhelpful 401.
        var signature = signingKey.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"vapid t={header}.{body}.{Encode(signature)}, k={publicKey}";
    }

    /// <summary>The origin of a push endpoint, which is what a VAPID token is minted for.</summary>
    public static string AudienceOf(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return $"{endpoint.Scheme}://{endpoint.Host}";
    }

    /// <summary>Base64url without padding, which is what both RFCs use throughout.</summary>
    public static string Encode(ReadOnlySpan<byte> value) => Base64Url.EncodeToString(value);

    /// <summary>The other direction, tolerating the padding some clients add.</summary>
    public static byte[] Decode(string value) => Base64Url.DecodeFromChars(value.TrimEnd('=').AsSpan());

    /// <summary>Turns a stored VAPID key pair into something that can sign.</summary>
    /// <remarks>
    /// Both halves are needed: .NET will not derive the public point from the
    /// private scalar on import, and a private key without its point cannot be
    /// loaded at all.
    /// </remarks>
    public static ECDsa ImportSigningKey(string publicKey, string privateKey)
    {
        var point = Decode(publicKey);

        if (point.Length != 65 || point[0] != 0x04)
        {
            throw new ArgumentException("A VAPID public key is a 65-byte uncompressed point.", nameof(publicKey));
        }

        return ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
            D = Decode(privateKey),
        });
    }

    private static ECDiffieHellman ImportPublicKey(ReadOnlySpan<byte> uncompressedPoint)
    {
        if (uncompressedPoint.Length != 65 || uncompressedPoint[0] != 0x04)
        {
            throw new ArgumentException(
                "A subscription key is a 65-byte uncompressed point.",
                nameof(uncompressedPoint));
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = uncompressedPoint[1..33].ToArray(),
                Y = uncompressedPoint[33..65].ToArray(),
            },
        });
    }

    private static byte[] UncompressedPoint(ECParameters parameters)
    {
        var point = new byte[65];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return point;
    }

    private sealed record VapidHeader(string Typ, string Alg);

    private sealed record VapidClaims(string Aud, long Exp, string Sub);
}
