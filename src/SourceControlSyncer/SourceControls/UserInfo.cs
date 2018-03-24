namespace SourceControlSyncer.SourceControls
{
    public class UserInfo
    {
        public UserInfo(string username, string email, string password)
        {
            Username = username;
            Email = email;
            Password = password;
        }

        public string Username { get; }
        public string Email { get; }
        public string Password { get; }
    }
}