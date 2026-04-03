namespace Garrard.Mcp.Explorer.Core.Interfaces;

public interface ISecretProtector
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
