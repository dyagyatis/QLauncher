namespace MinecraftLauncher.Models
{
    public class ModItem
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public string Downloads { get; set; } = "";
        public string ProjectType { get; set; } = "mod";
    }
}
