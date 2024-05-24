namespace Infrastructure.Configurations
{
    public record CredentialsOptions
    {
        public required string Username { get; set; }

        public required string Password { get; set; }
    }
}
