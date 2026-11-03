using System.Security.AccessControl;
using System.Security.Principal;
using Tpos.Desktop.Services;
using Xunit;

namespace Tpos.Tests;

public sealed class LocalDataSecurityTests
{
    [Fact]
    public void CredentialDirectoryAndFile_BlockInheritedAccess_AndAllowOnlyExpectedPrincipals()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TposAclTests", Guid.NewGuid().ToString("N"));
        try
        {
            LocalDataSecurity.SecureDataDirectory(directory);
            var databasePath = Path.Combine(directory, "tpos-auth.db");
            File.WriteAllText(databasePath, "ACL-test-only");
            LocalDataSecurity.SecureExistingDatabaseFiles(directory);

            var directoryAcl = new DirectoryInfo(directory).GetAccessControl();
            var fileAcl = new FileInfo(databasePath).GetAccessControl();
            Assert.True(directoryAcl.AreAccessRulesProtected);
            Assert.True(fileAcl.AreAccessRulesProtected);

            var currentUser = WindowsIdentity.GetCurrent().User;
            Assert.NotNull(currentUser);
            var expected = new[]
            {
                currentUser.Value,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
            };

            foreach (var sid in expected)
            {
                Assert.Contains(fileAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>(), rule => rule.IdentityReference.Equals(sid));
            }

            Assert.DoesNotContain(fileAcl.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>(), rule =>
                    rule.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null)) ||
                    rule.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.WorldSid, null)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
