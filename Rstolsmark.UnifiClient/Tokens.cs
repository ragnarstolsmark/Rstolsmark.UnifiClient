public class Tokens
{
    public string JwtToken { get; }

    public Tokens(string jwtToken, string csrfToken)
    {
        JwtToken = jwtToken;
        CsrfToken = csrfToken;
    }

    public string CsrfToken { get; }
}