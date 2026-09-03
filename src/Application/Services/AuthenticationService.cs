using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Security.Interfaces;

namespace Application.Services;

public interface IAuthenticationService
{
    Task<(bool Success, string Message, User? User)> LoginAsync(string username, string password);
    Task LogoutAsync(User user);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepo;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogRepository _auditRepo;

    public AuthenticationService(IUserRepository userRepo, IPasswordHasher hasher, IAuditLogRepository auditRepo)
    {
        _userRepo = userRepo;
        _hasher = hasher;
        _auditRepo = auditRepo;
    }

    public async Task<(bool Success, string Message, User? User)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Please enter both username and password.", null);

        var user = await _userRepo.GetByUsernameAsync(username.Trim());
        if (user == null)
            return (false, "Invalid username or password.", null);

        if (!user.IsActive)
            return (false, "User account has been deactivated.", null);

        bool isValid = _hasher.VerifyPassword(password, user.PasswordHash, user.Salt);
        if (!isValid)
            return (false, "Invalid username or password.", null);

        await _userRepo.UpdateLastLoginAsync(user.Id);

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Module = "Auth",
            Action = AuditActionType.Login,
            RecordId = user.Id.ToString(),
            NewValue = $"User {user.Username} logged in successfully.",
            Reason = "User Login"
        });

        return (true, "Login successful.", user);
    }

    public async Task LogoutAsync(User user)
    {
        if (user == null) return;

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId = user.Id,
            Username = user.Username,
            Module = "Auth",
            Action = AuditActionType.Logout,
            RecordId = user.Id.ToString(),
            NewValue = $"User {user.Username} logged out.",
            Reason = "User Logout"
        });
    }
}
