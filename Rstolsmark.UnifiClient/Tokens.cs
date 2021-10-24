internal class Tokens
{
    string JwtToken { get; }

    public Tokens(string jwtToken, string csrfToken)
    {
        JwtToken = jwtToken;
        CsrfToken = csrfToken;
    }

    string CsrfToken { get; }
}