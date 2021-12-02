using LibGit2Sharp;

namespace LocalAgent.Utilities
{
    public class GitUtils
    {
        public static bool IsRepository(string path)
        {
            return Repository.IsValid(path);
        }

        public static Repository GetRepository(string path)
        {
            var discoverPath = Repository.Discover(path);

            return discoverPath != null 
                ? new Repository(discoverPath)
                : null;
        }

        public static string GetSourceBranchName(string path)
        {
            var repository = GetRepository(path);
            return repository?.Head.FriendlyName;
        }

        public static RepositoryStatus GetStatus(string path)
        {
            var repository = GetRepository(path);
            return repository?.RetrieveStatus();
        }

        public static string GetSourceVersion(string path)
        {
            var repository = GetRepository(path);
            return repository?.Head.Tip.Sha;
        }
    }
}
