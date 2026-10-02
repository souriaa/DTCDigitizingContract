using System.Security.Cryptography;
using System.Text;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
namespace ContractManagement.API.Domains.Recovery;
public sealed class AccountRecoveryCryptography(IOptions<AccountRecoveryOptions> options)
{
    public string CreateToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static byte[] HashToken(string scope, int accountId, RecoveryPurpose purpose, Guid publicId, string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"recovery-v1|{scope}|{accountId}|{(byte)purpose}|{publicId:D}|{token}"));
    public static byte[] HashEmail(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim()));
    public byte[] Encrypt(string value, string scope)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var input = Encoding.UTF8.GetBytes(value);
        var output = new byte[input.Length]; var tag = new byte[16];
        using var aes = new AesGcm(ReadKey(), 16);
        aes.Encrypt(nonce, input, output, tag, Encoding.UTF8.GetBytes($"account-recovery|{scope}"));
        return [.. nonce, .. tag, .. output];
    }
    public string Decrypt(byte[] value, string scope)
    {
        if (value.Length < 28) throw new CryptographicException("Invalid recovery envelope.");
        var output = new byte[value.Length - 28]; using var aes = new AesGcm(ReadKey(), 16);
        aes.Decrypt(value[..12], value[28..], value[12..28], output, Encoding.UTF8.GetBytes($"account-recovery|{scope}"));
        return Encoding.UTF8.GetString(output);
    }
    private byte[] ReadKey()
    {
        var key = Convert.FromBase64String(options.Value.EncryptionKey ?? "");
        return key.Length == 32 ? key : throw new InvalidOperationException("Recovery encryption key must have 32 bytes.");
    }
}
