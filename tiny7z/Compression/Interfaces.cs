namespace pdj.tiny7z.Compression
{
    /// <summary>
    /// Password-protected archive provider interface
    /// </summary>
    public interface IPasswordProvider
    {
        string CryptoGetTextPassword();
    }
}
