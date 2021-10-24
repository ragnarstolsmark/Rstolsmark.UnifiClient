internal class UnifiCredentials
{
    string JwtToken { get; }

    public UnifiCredentials(string jwtToken, string csrfToken)
    {
        JwtToken = jwtToken;
        CsrfToken = csrfToken;
    }

    string CsrfToken { get; }
}