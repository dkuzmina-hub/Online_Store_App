namespace OnlineStoreApp.Data
{
    public static class Session
    {
        public static int    UserId   { get; set; }
        public static string UserName { get; set; } = "";
        public static string Role     { get; set; } = ""; // "admin" or "user"

        public static bool IsAdmin => Role == "admin";

        public static void Clear()
        {
            UserId = 0; UserName = ""; Role = "";
        }
    }
}
