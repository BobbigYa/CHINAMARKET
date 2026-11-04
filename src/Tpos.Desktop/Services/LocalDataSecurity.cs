using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Tpos.Desktop.Services;

/// <summary>
/// Restricts local credential files to the current Windows user, SYSTEM and administrators.
/// </summary>
public static class LocalDataSecurity
{
    public static void SecureDataDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var directory = Directory.CreateDirectory(directoryPath);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        foreach (var identity in AllowedIdentities())
        {
            security.AddAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        directory.SetAccessControl(security);
    }

    public static void SecureExistingDatabaseFiles(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        foreach (var path in Directory.EnumerateFiles(directoryPath, "tpos-auth.db*", SearchOption.TopDirectoryOnly))
        {
            var file = new FileInfo(path);
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            foreach (var identity in AllowedIdentities())
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    identity,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
            }

            file.SetAccessControl(security);
        }
    }

    private static IEnumerable<SecurityIdentifier> AllowedIdentities()
    {
        var currentUser = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("无法确定当前 Windows 用户。");

        yield return currentUser;
        yield return new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        yield return new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
    }
}
