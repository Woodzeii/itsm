namespace itsm.Application.Common.Interfaces;
public interface ILdapService
{
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task<LdapUser?> FindUserAsync(string username);
    Task<bool> TestConnectionAsync();
}
public class LdapUser
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ObjectSid { get; set; } = string.Empty;
}