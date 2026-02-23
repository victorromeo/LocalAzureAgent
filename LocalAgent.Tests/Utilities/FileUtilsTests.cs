using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Xunit;
using LocalAgent.Utilities;
using System.Runtime.InteropServices;

namespace LocalAgent.Tests.Utilities
{
    public class FileUtilsTests
    {
        [Fact]
        public void GuardPreventDamageToSystemFolders_ShouldThrowException_ForPaths()
        {
            string[] paths = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ? 
            [
                Path.GetPathRoot(Environment.SystemDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                
             ] :
            [
                "/",
                "/bin",
                "/sbin",
                "/usr",
                "/usr/bin",
                "/usr/sbin",
                "/etc",
                "/var",
                "/home",
                "/root"
            ];

            foreach(var rootPath in paths)
            {
                // Act & Assert
                try
                {
                    var exception = Assert.Throws<ArgumentException>(() => FileUtils.GuardPreventDamageToSystemFolders(rootPath));
                    Assert.Contains("Refusing to operate on critical system folder", exception.Message);    
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Expected ArgumentException for path '{rootPath}', but got: {ex.GetType().Name} with message: {ex.Message}");
                }                
            }
        }

        [Fact]
        public void GuardPreventDamageToSystemFolders_ShouldNotThrowException_ForValidPath()
        {
            // Arrange
            string tempPath = Path.GetTempPath(); 

            // Act & Assert
            try
            {
                FileUtils.GuardPreventDamageToSystemFolders(tempPath);
            }
            catch (Exception ex)
            {
                Assert.True(false, $"Expected no exception, but got: {ex.Message}");
            }
        }
    }
}