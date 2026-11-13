using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fluid.Benchmarks;

// Compares the ways the md5, sha1, sha256, hmac_sha1 and hmac_sha256 filters can hash a string:
//
// - GetBytes: Encoding.UTF8.GetBytes(value) into a new array, HashData returning a new array.
// - Pooled: encodes into a stackalloc'ed buffer or an ArrayPool<byte> array, HashData writing to the stack. This is
//   what the filters do on .NET.
// - StringStream: HashData reading a System.IO.StringStream (.NET 11), which encodes the string incrementally.
//
// .NET 11.0.0, Apple M4 Pro. The other algorithms compare the same way.
//
// | Method       | Algorithm | Length | NonAscii | Mean         | Ratio | Allocated |
// |------------- |---------- |------- |--------- |-------------:|------:|----------:|
// | GetBytes     | SHA256    | 25     | False    |     782.9 ns |  1.00 |     264 B |
// | Pooled       | SHA256    | 25     | False    |     779.2 ns |  1.00 |     152 B |
// | StringStream | SHA256    | 25     | False    |   1,372.4 ns |  1.75 |     272 B |
// | GetBytes     | SHA256    | 25     | True     |     757.8 ns |  1.00 |     280 B |
// | Pooled       | SHA256    | 25     | True     |     727.7 ns |  0.96 |     152 B |
// | StringStream | SHA256    | 25     | True     |   1,249.0 ns |  1.65 |     272 B |
// | GetBytes     | SHA256    | 1000   | False    |   1,103.8 ns |  1.00 |    1232 B |
// | Pooled       | SHA256    | 1000   | False    |   1,080.4 ns |  0.98 |     152 B |
// | StringStream | SHA256    | 1000   | False    |   1,576.9 ns |  1.43 |     272 B |
// | GetBytes     | SHA256    | 1000   | True     |   2,223.3 ns |  1.00 |    2032 B |
// | Pooled       | SHA256    | 1000   | True     |   2,097.8 ns |  0.94 |     152 B |
// | StringStream | SHA256    | 1000   | True     |   2,610.6 ns |  1.17 |     272 B |
// | GetBytes     | SHA256    | 100000 | False    |  54,513.8 ns |  1.00 |  100388 B |
// | Pooled       | SHA256    | 100000 | False    |  37,342.1 ns |  0.69 |     152 B |
// | StringStream | SHA256    | 100000 | False    |  44,674.6 ns |  0.82 |     472 B |
// | GetBytes     | SHA256    | 100000 | True     | 166,498.4 ns |  1.00 |  180510 B |
// | Pooled       | SHA256    | 100000 | True     | 137,960.0 ns |  0.83 |     153 B |
// | StringStream | SHA256    | 100000 | True     | 146,602.5 ns |  0.88 |     473 B |

[MemoryDiagnoser, ShortRunJob]
public class HashingBenchmarks
{
    public enum HashAlgorithm { MD5, SHA1, SHA256, HMACSHA1, HMACSHA256 }

    private const int MaxUtf8BytesPerChar = 3;
    private const int StackallocBytes = 256;
    private const int MaxUncountedBytes = 64 * 1024;
    private const string Key = "secret_key";

    private string _value;

    [Params(HashAlgorithm.MD5, HashAlgorithm.SHA1, HashAlgorithm.SHA256, HashAlgorithm.HMACSHA1, HashAlgorithm.HMACSHA256)]
    public HashAlgorithm Algorithm { get; set; }

    [Params(25, 1_000, 100_000)]
    public int Length { get; set; }

    [Params(false, true)]
    public bool NonAscii { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Two and three byte sequences and a surrogate pair, mixed with ASCII like most non-English text.
        var source = NonAscii ? "héllo wörld, 日本語のテキスト 😀 " : "this is some text to hash ";
        var builder = new StringBuilder(Length + source.Length);

        while (builder.Length < Length)
        {
            builder.Append(source);
        }

        // Not in the middle of the surrogate pair.
        var length = char.IsHighSurrogate(builder[Length - 1]) ? Length - 1 : Length;

        _value = builder.ToString(0, length);
    }

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms

    [Benchmark(Baseline = true)]
    public string GetBytes()
    {
        var bytes = Encoding.UTF8.GetBytes(_value);

        var hash = Algorithm switch
        {
            HashAlgorithm.MD5 => MD5.HashData(bytes),
            HashAlgorithm.SHA1 => SHA1.HashData(bytes),
            HashAlgorithm.SHA256 => SHA256.HashData(bytes),
            HashAlgorithm.HMACSHA1 => HMACSHA1.HashData(Encoding.UTF8.GetBytes(Key), bytes),
            HashAlgorithm.HMACSHA256 => HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), bytes),
            _ => throw new NotSupportedException()
        };

        return Utils.HexUtilities.ToHexLower(hash);
    }

    [Benchmark]
    public string Pooled()
    {
        var value = _value;

        byte[] rented = null;
        Span<byte> buffer = value.Length <= StackallocBytes / MaxUtf8BytesPerChar
            ? stackalloc byte[StackallocBytes]
            : (rented = ArrayPool<byte>.Shared.Rent(value.Length <= MaxUncountedBytes / MaxUtf8BytesPerChar
                ? value.Length * MaxUtf8BytesPerChar
                : Encoding.UTF8.GetByteCount(value)));

        ReadOnlySpan<byte> bytes = buffer.Slice(0, Encoding.UTF8.GetBytes(value, buffer));

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        int written;

        switch (Algorithm)
        {
            case HashAlgorithm.MD5: written = MD5.HashData(bytes, hash); break;
            case HashAlgorithm.SHA1: written = SHA1.HashData(bytes, hash); break;
            case HashAlgorithm.SHA256: written = SHA256.HashData(bytes, hash); break;
            case HashAlgorithm.HMACSHA1: written = HMACSHA1.HashData(GetKey(stackalloc byte[StackallocBytes]), bytes, hash); break;
            case HashAlgorithm.HMACSHA256: written = HMACSHA256.HashData(GetKey(stackalloc byte[StackallocBytes]), bytes, hash); break;
            default: throw new NotSupportedException();
        }

        if (rented != null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return Convert.ToHexStringLower(hash.Slice(0, written));
    }

    [Benchmark]
    public string StringStream()
    {
        using var stream = new StringStream(_value, Encoding.UTF8);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        int written;

        switch (Algorithm)
        {
            case HashAlgorithm.MD5: written = MD5.HashData(stream, hash); break;
            case HashAlgorithm.SHA1: written = SHA1.HashData(stream, hash); break;
            case HashAlgorithm.SHA256: written = SHA256.HashData(stream, hash); break;
            case HashAlgorithm.HMACSHA1: written = HMACSHA1.HashData(GetKey(stackalloc byte[StackallocBytes]), stream, hash); break;
            case HashAlgorithm.HMACSHA256: written = HMACSHA256.HashData(GetKey(stackalloc byte[StackallocBytes]), stream, hash); break;
            default: throw new NotSupportedException();
        }

        return Convert.ToHexStringLower(hash.Slice(0, written));
    }

#pragma warning restore CA5351
#pragma warning restore CA5350

    private static ReadOnlySpan<byte> GetKey(Span<byte> buffer)
    {
        return buffer.Slice(0, Encoding.UTF8.GetBytes(Key, buffer));
    }
}
